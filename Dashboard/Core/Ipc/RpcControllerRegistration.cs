using System.Text.Json;

namespace Simurgh.Dashboard.Core.Ipc;

public record RpcControllerRegistration(Type InterfaceType, string RoutePrefix);