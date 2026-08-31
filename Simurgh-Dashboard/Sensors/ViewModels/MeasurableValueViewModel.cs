using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.ViewModels;

/// <summary>
/// Presentation ViewModel wrapping <see cref="MeasurableValueEntity"/>.
/// Listens to domain entity property changes and exposes UI-specific computed states,
/// alarm colors, display texts, and formatting bindings.
/// </summary>
public sealed partial class MeasurableValueViewModel : ObservableObject, IDisposable
{
    private readonly MeasurableValueEntity _model;
    private bool _isDisposed;

    #region Static Alarm Brushes

    private static readonly SolidColorBrush NormalBrush;
    private static readonly SolidColorBrush WarningBrush;
    private static readonly SolidColorBrush CriticalBrush;

    static MeasurableValueViewModel()
    {
        NormalBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xE6, 0x76)); // Material Green Accent
        NormalBrush.Freeze();

        WarningBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xAB, 0x00)); // Amber Accent
        WarningBrush.Freeze();

        CriticalBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x17, 0x44)); // Vivid Red Accent
        CriticalBrush.Freeze();
    }

    #endregion

    #region Constructor & Model Subscription

    public MeasurableValueViewModel(MeasurableValueEntity model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    #endregion

    #region Positional Identity

    public int Index => _model.Index;

    #endregion

    #region Passthrough & Formatting Properties

    public SensorType Type => _model.Type;

    public string Unit => _model.Unit;

    public double RealValue => _model.RealValue;

    public string FormattedValue => _model.FormattedValue;

    public Brush DigitBrush => _model.DigitBrush;

    public Brush PlaceholderBrush => _model.PlaceholderBrush;

    public DateTimeOffset LastUpdated => _model.LastUpdated;

    #endregion

    #region UI Presentation Computed Properties

    /// <summary>
    /// Combined display string: e.g. "24.5 °C" or "-- %".
    /// </summary>
    public string DisplayText => string.IsNullOrWhiteSpace(_model.Unit)
        ? _model.FormattedValue
        : $"{_model.FormattedValue} {_model.Unit}";

    /// <summary>
    /// Indicates whether the channel is currently in any abnormal threshold state.
    /// </summary>
    public bool HasAlarm => _model.IsInWarning || _model.IsInCritical;

    /// <summary>
    /// Contextual alarm brush based on domain entity severity evaluation.
    /// </summary>
    public Brush StatusBrush => _model.IsInCritical
        ? CriticalBrush
        : _model.IsInWarning
            ? WarningBrush
            : NormalBrush;

    /// <summary>
    /// Human-readable alarm text badge for tooltips or status bars.
    /// </summary>
    public string StatusDescription => _model.IsInCritical
        ? "Critical"
        : _model.IsInWarning
            ? "Warning"
            : "Normal";

    #endregion

    #region Event Bridge & Notification Mapping

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MeasurableValueEntity.FormattedValue):
            case nameof(MeasurableValueEntity.Unit):
                OnPropertyChanged(nameof(FormattedValue));
                OnPropertyChanged(nameof(Unit));
                OnPropertyChanged(nameof(DisplayText));
                break;

            case nameof(MeasurableValueEntity.RealValue):
                OnPropertyChanged(nameof(RealValue));
                break;

            case nameof(MeasurableValueEntity.IsInCritical):
            case nameof(MeasurableValueEntity.IsInWarning):
                OnPropertyChanged(nameof(HasAlarm));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusDescription));
                break;

            case nameof(MeasurableValueEntity.Type):
                OnPropertyChanged(nameof(Type));
                break;

            case nameof(MeasurableValueEntity.DigitBrush):
                OnPropertyChanged(nameof(DigitBrush));
                break;

            case nameof(MeasurableValueEntity.PlaceholderBrush):
                OnPropertyChanged(nameof(PlaceholderBrush));
                break;

            case nameof(MeasurableValueEntity.LastUpdated):
                OnPropertyChanged(nameof(LastUpdated));
                break;

            case null or "":
                // Broadcast full property refresh
                OnPropertyChanged(string.Empty);
                break;
        }
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _model.PropertyChanged -= OnModelPropertyChanged;
    }

    #endregion
}
