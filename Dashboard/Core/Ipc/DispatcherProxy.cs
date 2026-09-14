using System.Reflection;
using System.Windows;

namespace Simurgh.Dashboard.Core.Ipc;

public class DispatcherProxy : DispatchProxy
{
    private object _target = null!;

    public static object Create(Type interfaceType, object target)
    {
        var proxy = (DispatcherProxy)typeof(DispatchProxy)
            .GetMethod(nameof(DispatchProxy.Create))!
            .MakeGenericMethod(interfaceType, typeof(DispatcherProxy))
            .Invoke(null, null)!;

        proxy._target = target;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;

        if (Application.Current.Dispatcher.CheckAccess())
            return targetMethod.Invoke(_target, args);

        return Application.Current.Dispatcher.Invoke(() => targetMethod.Invoke(_target, args));
    }
}