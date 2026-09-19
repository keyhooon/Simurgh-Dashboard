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
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    pipeName: pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);

                // سپردن کلاینت به Task جداگانه بدون متوقف کردن لوپ لیسنر
                _ = ProcessClientSessionAsync(serverStream, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                serverStream?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accepting IPC client on pipe {PipeName}", pipeName);
                serverStream?.Dispose();
                await Task.Delay(200, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessClientSessionAsync(NamedPipeServerStream pipeStream, CancellationToken hostStoppingToken)
    {
        // استفاده از using برای مدیریت عمر استریم
        await using (pipeStream.ConfigureAwait(false))
        {
            JsonRpc? rpc = null;
            try
            {
                var formatter = new SystemTextJsonFormatter();
                formatter.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                formatter.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;

                var handler = new HeaderDelimitedMessageHandler(pipeStream, pipeStream, formatter);
                rpc = new JsonRpc(handler);

                // دفاع در برابر null بودن registry
                if (registry?.Registrations != null)
                {
                    foreach (var reg in registry.Registrations)
                    {
                        // اطمینان از اینکه InterfaceType نال نیست
                        if (reg?.InterfaceType == null) continue;

                        var serviceInstance = serviceProvider.GetService(reg.InterfaceType);

                        if (serviceInstance == null)
                        {
                            logger.LogWarning("Service {Interface} not registered in DI container.", reg.InterfaceType.Name);
                            continue;
                        }

                        var options = new JsonRpcTargetOptions
                        {
                            MethodNameTransform = methodName => string.IsNullOrEmpty(reg.RoutePrefix)
                                ? methodName.ToLowerInvariant()
                                : $"{reg.RoutePrefix}.{methodName}".ToLowerInvariant()
                        };

                        rpc.AddLocalRpcTarget(reg.InterfaceType, serviceInstance, options);
                    }
                }

                rpc.StartListening();

                // استفاده از لینک توکن برای مدیریت توقف
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(hostStoppingToken);
                using var regDispose = cts.Token.Register(() => rpc.Dispose());

                await rpc.Completion.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* مورد انتظار هنگام خروج */ }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in IPC client session on pipe {PipeName}", pipeName);
            }
            finally
            {
                // rpc? اینجا امن است اما مطمئن می‌شویم قبل از Dispose چک شود
                rpc?.Dispose();
            }
        }
    }
}
