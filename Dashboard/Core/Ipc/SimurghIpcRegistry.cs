namespace Simurgh.Dashboard.Core.Ipc;

public class SimurghIpcRegistry
{
    private readonly List<RpcControllerRegistration> _registrations = [];
    public IReadOnlyList<RpcControllerRegistration> Registrations => _registrations;

    public void Register(Type interfaceType, string routePrefix)
    {
        var prefix = routePrefix.Trim().TrimEnd('.');
        if (!_registrations.Any(r => r.InterfaceType == interfaceType))
        {
            _registrations.Add(new RpcControllerRegistration(interfaceType, prefix));
        }
    }
}