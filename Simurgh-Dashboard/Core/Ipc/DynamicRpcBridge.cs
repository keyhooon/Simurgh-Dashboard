using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;
using Expression = System.Linq.Expressions.Expression;

namespace SimurghDashboard.Core.Ipc;

/// <summary>
/// Marks an interface as an RPC controller to be exposed over Named Pipe IPC.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class RpcControllerAttribute : Attribute
{
    public string RoutePrefix { get; }

    public RpcControllerAttribute(string routePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);
        RoutePrefix = routePrefix;
    }
}

/// <summary>
/// Holds metadata describing an RPC route and its corresponding ViewModel command property.
/// </summary>
public sealed record RpcEndpoint(
    string Route,
    Type ServiceType,
    PropertyInfo Property,
    Type? ParameterType);

/// <summary>
/// Bridges incoming JSON-RPC calls directly to WPF MVVM ICommand instances.
/// Ensures proper thread dispatching and type conversion across protocols.
/// </summary>
public sealed class DynamicRpcBridge
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicRpcBridge> _logger;
    private readonly ConcurrentDictionary<string, RpcEndpoint> _endpoints;
    private readonly JsonSerializerOptions _jsonOptions;

    public IReadOnlyDictionary<string, RpcEndpoint> Endpoints => _endpoints;

    internal DynamicRpcBridge(
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, RpcEndpoint> endpoints,
        ILogger<DynamicRpcBridge> logger)
    {
        _serviceProvider = serviceProvider;
        _endpoints = endpoints;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    /// <summary>
    /// Binds route-specific closures to the JsonRpc instance using dynamically compiled delegates.
    /// </summary>
    public void BindToRpc(JsonRpc rpc)
    {
        foreach (var (route, endpoint) in _endpoints)
        {
            _logger.LogInformation("Registering IPC endpoint: {Route} -> ParameterType: {ParamType}",
                route, endpoint.ParameterType?.Name ?? "void");

            var canRoute = GetCanExecuteRoute(route);

            if (endpoint.ParameterType == null)
            {
                var target = new VoidEndpointTarget(this, endpoint);

                var executeMethod = typeof(VoidEndpointTarget).GetMethod(nameof(VoidEndpointTarget.ExecuteAsync))!;
                var canExecuteMethod = typeof(VoidEndpointTarget).GetMethod(nameof(VoidEndpointTarget.CanExecuteAsync))!;

                rpc.AddLocalRpcMethod(route, executeMethod, target);
                rpc.AddLocalRpcMethod(canRoute, canExecuteMethod, target);
            }
            else
            {
                var targetType = typeof(TypedEndpointTarget<>).MakeGenericType(endpoint.ParameterType);
                var target = Activator.CreateInstance(targetType, this, endpoint)!;

                var executeMethod = targetType.GetMethod(nameof(TypedEndpointTarget<object>.ExecuteAsync))!;
                var canExecuteMethod = targetType.GetMethod(nameof(TypedEndpointTarget<object>.CanExecuteAsync))!;

                rpc.AddLocalRpcMethod(route, executeMethod, target);
                rpc.AddLocalRpcMethod(canRoute, canExecuteMethod, target);
            }

        }
    }



    private static string GetCanExecuteRoute(string route)
    {
        var dotIndex = route.IndexOf('.');
        return dotIndex >= 0
            ? string.Concat(route.AsSpan(0, dotIndex), ".can.", route.AsSpan(dotIndex + 1))
            : $"can.{route}";
    }

    private async Task<object?> ExecuteCoreAsync(
        RpcEndpoint endpoint,
        object? parameter,
        bool isCanExecute)
    {
        // Resolve target viewmodel or controller instance from DI container
        var serviceInstance = _serviceProvider.GetRequiredService(endpoint.ServiceType);
        var commandObj = endpoint.Property.GetValue(serviceInstance) as ICommand;

        if (commandObj is null)
        {
            throw new LocalRpcException($"Property '{endpoint.Property.Name}' on '{endpoint.ServiceType.Name}' does not provide a valid ICommand instance.")
            {
                ErrorCode = (int)JsonRpcErrorCode.InternalError
            };
        }

        // Evaluate CanExecute predicate on WPF Dispatcher thread
        var canExecute = await Application.Current.Dispatcher.InvokeAsync(() => commandObj.CanExecute(parameter));

        if (isCanExecute)
        {
            return canExecute;
        }

        if (!canExecute)
        {
            _logger.LogWarning("Command execution denied by CanExecute predicate for endpoint: {Route}", endpoint.Route);
            return false;
        }

        // Execute asynchronous or synchronous command on WPF Dispatcher thread
        if (commandObj is IAsyncRelayCommand asyncRelay)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => asyncRelay.ExecuteAsync(parameter));
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                commandObj.Execute(parameter);
            });
        }

        return true;
    }
    #region Explicit RPC Targets

    public sealed class VoidEndpointTarget(DynamicRpcBridge bridge, RpcEndpoint endpoint)
    {
        public Task<object?> ExecuteAsync() =>
            bridge.ExecuteCoreAsync(endpoint, parameter: null, isCanExecute: false);

        public Task<object?> CanExecuteAsync() =>
            bridge.ExecuteCoreAsync(endpoint, parameter: null, isCanExecute: true);
    }

    public sealed class TypedEndpointTarget<T>(DynamicRpcBridge bridge, RpcEndpoint endpoint)
    {
        public Task<object?> ExecuteAsync(T parameter) =>
            bridge.ExecuteCoreAsync(endpoint, parameter, isCanExecute: false);

        public Task<object?> CanExecuteAsync(T parameter) =>
            bridge.ExecuteCoreAsync(endpoint, parameter, isCanExecute: true);
    }

    #endregion
}

/// <summary>
/// Hosted background service managing Named Pipe client connections and binding JSON-RPC dispatchers.
/// Uses raw duplex stream binding to prevent buffer deadlock and pipe closure.
/// </summary>
public sealed class NamedPipeRpcServerHostedService(
    DynamicRpcBridge bridge,
    ILogger<NamedPipeRpcServerHostedService> logger,
    string pipeName = "SimurghDashboard_IPC")
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NamedPipe IPC Server listening on pipe: \\\\.\\pipe\\{PipeName}", pipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;

            try
            {
                // Create asynchronous full-duplex NamedPipe stream
                serverStream = new NamedPipeServerStream(
                    pipeName: pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                // Wait for an incoming client to connect
                await serverStream.WaitForConnectionAsync(stoppingToken);

                logger.LogInformation("Incoming client connected to NamedPipe IPC.");

                // Process client session independently in background task to unblock listener loop
                _ = ProcessClientSessionAsync(serverStream, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                serverStream?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception encountered in NamedPipe listener loop.");
                serverStream?.Dispose();
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    private async Task ProcessClientSessionAsync(NamedPipeServerStream pipeStream, CancellationToken cancellationToken)
    {
        using (pipeStream)
        {
            try
            {
                var formatter = new SystemTextJsonFormatter();
                formatter.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                formatter.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;

                var handler = new HeaderDelimitedMessageHandler(pipeStream, pipeStream, formatter);
                using var rpc = new JsonRpc(handler);

                // Bind all discovered DynamicRpcBridge endpoints
                bridge.BindToRpc(rpc);

                rpc.Disconnected += (s, e) =>
                {
                    logger.LogInformation("IPC Client disconnected. Reason: {Reason}, Description: {Description}",
                        e.Reason, e.Description);
                };

                // Start message processing loop
                rpc.StartListening();

                // Keep stream alive until client disconnects or host shuts down
                using (cancellationToken.Register(() => pipeStream.Dispose()))
                {
                    await rpc.Completion;
                }
            }
            catch (ObjectDisposedException)
            {
                // Normal teardown during connection close
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error during IPC client session.");
            }
        }
    }
}

/// <summary>
/// Extension methods for dependency injection registration.
/// </summary>
public static class DynamicRpcServiceExtensions
{
    /// <summary>
    /// Scans provided assemblies for [RpcController] interfaces and registers the DynamicRpcBridge singleton.
    /// </summary>
    public static IServiceCollection AddDynamicRpcBridge(this IServiceCollection services, params Assembly[] assemblies)
    {
        var targetAssemblies = ResolveTargetAssemblies(assemblies);
        var endpoints = new ConcurrentDictionary<string, RpcEndpoint>(StringComparer.OrdinalIgnoreCase);

        var controllerInterfaces = targetAssemblies
            .SelectMany(GetExportedTypesSafe)
            .Where(t => t.IsInterface && t.GetCustomAttribute<RpcControllerAttribute>() != null);

        foreach (var iface in controllerInterfaces)
        {
            var attr = iface.GetCustomAttribute<RpcControllerAttribute>()!;
            var prefix = (attr.RoutePrefix ?? iface.Name).Trim().Trim('.');

            var commandProps = iface.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(ICommand).IsAssignableFrom(p.PropertyType));

            foreach (var prop in commandProps)
            {
                var actionName = prop.Name;
                if (actionName.EndsWith("Command", StringComparison.OrdinalIgnoreCase) && actionName.Length > 7)
                {
                    actionName = actionName[..^7];
                }

                var route = string.IsNullOrEmpty(prefix)
                    ? actionName.ToLowerInvariant()
                    : $"{prefix}.{actionName}".ToLowerInvariant();

                var paramType = ResolveCommandParameterType(prop.PropertyType);
                endpoints[route] = new RpcEndpoint(route, iface, prop, paramType);
            }
        }

        services.TryAddSingleton(sp => new DynamicRpcBridge(
            sp,
            endpoints,
            sp.GetRequiredService<ILogger<DynamicRpcBridge>>()));

        return services;
    }

    /// <summary>
    /// Registers DynamicRpcBridge, auto-binds interface implementations, and starts the NamedPipe background listener.
    /// </summary>
    public static IServiceCollection AddSimurghIpcServer(
        this IServiceCollection services,
        string pipeName = "SimurghDashboard_IPC",
        params Assembly[] assemblies)
    {
        var targetAssemblies = ResolveTargetAssemblies(assemblies);

        // Auto-register interface implementations as Singletons if found in target assemblies
        var controllerInterfaces = targetAssemblies
            .SelectMany(GetExportedTypesSafe)
            .Where(t => t.IsInterface && t.GetCustomAttribute<RpcControllerAttribute>() != null);

        foreach (var iface in controllerInterfaces)
        {
            var implementationType = targetAssemblies
                .SelectMany(GetExportedTypesSafe)
                .FirstOrDefault(t => t is { IsAbstract: false, IsInterface: false } && iface.IsAssignableFrom(t));

            if (implementationType != null)
            {
                services.TryAddSingleton(iface, implementationType);
            }
        }

        // Register core bridge
        services.AddDynamicRpcBridge(targetAssemblies);

        // Register NamedPipe background service
        services.AddHostedService(sp => new NamedPipeRpcServerHostedService(
            sp.GetRequiredService<DynamicRpcBridge>(),
            sp.GetRequiredService<ILogger<NamedPipeRpcServerHostedService>>(),
            pipeName));

        return services;
    }

    private static Assembly[] ResolveTargetAssemblies(Assembly[]? assemblies)
    {
        if (assemblies != null && assemblies.Length > 0)
        {
            return assemblies.Distinct().ToArray();
        }

        var entry = Assembly.GetEntryAssembly();
        var executing = Assembly.GetExecutingAssembly();

        return entry != null && entry != executing
            ? [entry, executing]
            : [executing];
    }

    private static IEnumerable<Type> GetExportedTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static Type? ResolveCommandParameterType(Type commandPropertyType)
    {
        if (commandPropertyType.IsGenericType)
        {
            return commandPropertyType.GetGenericArguments()[0];
        }

        var genericRelayInterface = commandPropertyType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IRelayCommand<>) ||
                 i.GetGenericTypeDefinition() == typeof(IAsyncRelayCommand<>)));

        return genericRelayInterface?.GetGenericArguments()[0];
    }
}
