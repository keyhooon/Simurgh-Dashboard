using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Simurgh.Dashboard.Core.Ipc;

public class IpcControllerBuilder(IServiceCollection services, SimurghIpcRegistry registry)
{
    /// <summary>
    /// نگاشت کنترلر فقط با اینترفیس (در صورتی که پیاده‌سازی قبلاً در DI ثبت شده باشد)
    /// </summary>
    public IpcControllerBuilder MapController<TInterface>(string routePrefix)
        where TInterface : class
    {
        registry.Register(typeof(TInterface), routePrefix);
        return this;
    }

    /// <summary>
    /// نگاشت کنترلر همراه با ثبت پیاده‌سازی Singleton در کانتینر
    /// </summary>
    public IpcControllerBuilder MapController<TInterface, TImplementation>(string routePrefix)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.TryAddSingleton<TInterface, TImplementation>();
        registry.Register(typeof(TInterface), routePrefix);
        return this;
    }
}