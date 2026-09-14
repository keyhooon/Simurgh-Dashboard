using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Simurgh.Dashboard.Core.Ipc;

public static class SimurghIpcExtensions
{
    public static IServiceCollection AddSimurghIpcServer(
        this IServiceCollection services,
        Action<IpcControllerBuilder> configure,
        string pipeName = "SimurghDashboard_IPC")
    {
        // اطمینان از سینگلتون بودن رجیستری در تمام طول چرخه DI
        var registryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SimurghIpcRegistry));
        SimurghIpcRegistry registry;

        if (registryDescriptor?.ImplementationInstance is SimurghIpcRegistry existingRegistry)
        {
            registry = existingRegistry;
        }
        else
        {
            registry = new SimurghIpcRegistry();
            services.AddSingleton(registry);
        }

        var builder = new IpcControllerBuilder(services, registry);
        configure(builder);

        // اطمینان از ثبت تنها یک نسخه از HostedService حتی در صورت فراخوانی مکرر
        if (!services.Any(d => d.ImplementationType == typeof(SimurghIpcServerHostedService) ||
                               (d.ImplementationFactory != null && d.ServiceType == typeof(IHostedService))))
        {
            services.AddHostedService(sp => new SimurghIpcServerHostedService(
                sp,
                sp.GetRequiredService<SimurghIpcRegistry>(),
                sp.GetRequiredService<ILogger<SimurghIpcServerHostedService>>(),
                pipeName));
        }

        return services;
    }
}