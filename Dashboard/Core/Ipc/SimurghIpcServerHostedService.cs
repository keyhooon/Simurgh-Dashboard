using System.IO.Pipes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace Simurgh.Dashboard.Core.Ipc;

public sealed class SimurghIpcServerHostedService(
    IServiceProvider serviceProvider,
    SimurghIpcRegistry registry,
    ILogger<SimurghIpcServerHostedService> logger,
    string pipeName) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Simurgh IPC Server listening on pipe: \\\\.\\pipe\\{PipeName}", pipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var serverStream = new NamedPipeServerStream(
                    pipeName: pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(stoppingToken);
                _ = ProcessClientAsync(serverStream, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in IPC listener loop.");
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    private async Task ProcessClientAsync(NamedPipeServerStream pipeStream, CancellationToken cancellationToken)
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

                foreach (var reg in registry.Registrations)
                {
                    var serviceInstance = serviceProvider.GetRequiredService(reg.InterfaceType);
                    var proxiedInstance = DispatcherProxy.Create(reg.InterfaceType, serviceInstance);

                    var options = new JsonRpcTargetOptions
                    {
                        MethodNameTransform = methodName => string.IsNullOrEmpty(reg.RoutePrefix)
                            ? methodName.ToLowerInvariant()
                            : $"{reg.RoutePrefix}.{methodName}".ToLowerInvariant()
                    };

                    rpc.AddLocalRpcTarget(reg.InterfaceType, proxiedInstance, options);
                }

                rpc.StartListening();

                using (cancellationToken.Register(() => pipeStream.Dispose()))
                {
                    await rpc.Completion;
                }
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                logger.LogError(ex, "Error in IPC client session.");
            }
        }
    }
}