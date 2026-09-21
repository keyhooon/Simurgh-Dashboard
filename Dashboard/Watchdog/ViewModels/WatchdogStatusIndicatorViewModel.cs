// Path: Simurgh.Dashboard.Watchdog.ViewModels/WatchdogStatusIndicatorViewModel.cs

using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Simurgh.Watchdog.Agent;

namespace Simurgh.Dashboard.Watchdog.ViewModels;

/// <summary>
/// Minimalist ViewModel focused solely on the physical NamedPipe connection state (Pipeline).
/// </summary>
public sealed partial class WatchdogStatusIndicatorViewModel : ObservableObject, IDisposable
{
    private readonly IAgentRpcSessionAccessor _sessionAccessor;

    [ObservableProperty]
    private bool _isConnected;

    public WatchdogStatusIndicatorViewModel(IAgentRpcSessionAccessor sessionAccessor)
    {
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));

        // Initial snapshot
        _isConnected = _sessionAccessor.Current is not null && !_sessionAccessor.Current.Rpc.Completion.IsCompleted;

        // Subscribe to connection state changes
        _sessionAccessor.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(bool connected)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsConnected = connected;
        });
    }

    public void Dispose()
    {
        _sessionAccessor.ConnectionChanged -= OnConnectionChanged;
    }
}