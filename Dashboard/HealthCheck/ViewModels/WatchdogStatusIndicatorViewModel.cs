using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Simurgh.Watchdog.Agent;

namespace Simurgh.Dashboard.HealthCheck.ViewModels;

public sealed partial class WatchdogStatusIndicatorViewModel : ObservableObject, IDisposable
{
    private readonly IWatchdogAgentMonitor _monitor;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private DateTimeOffset? _lastHeartbeat;

    /// <summary>
    /// Property that toggles/increments to notify the XAML storyboard to trigger a pulse animation.
    /// </summary>
    [ObservableProperty]
    private int _pulseTrigger;

    public WatchdogStatusIndicatorViewModel(IWatchdogAgentMonitor monitor)
    {
        _monitor = monitor;
        _isConnected = _monitor.IsConnected;
        _lastHeartbeat = _monitor.LastReportSentUtc;

        _monitor.ConnectionChanged += OnConnectionChanged;
        _monitor.ReportSent += OnReportSent;
    }

    private void OnConnectionChanged(bool connected)
    {
        // اطمینان از اعمال تغییرات روی ترد UI
        Application.Current?.Dispatcher.InvokeAsync(() => IsConnected = connected);
    }

    private void OnReportSent(DateTimeOffset timestamp)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LastHeartbeat = timestamp;
            PulseTrigger++; // تغییر مقدار برای تریگر کردن Storyboard در XAML
        });
    }

    public void Dispose()
    {
        _monitor.ConnectionChanged -= OnConnectionChanged;
        _monitor.ReportSent -= OnReportSent;
    }
}