using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimurghDashboard.Sensors.Controls.Sensors;
using SimurghDashboard.Sensors.Options;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Domain model for an entire sensor module identified by its positional array index.
/// Directly implements INotifyPropertyChanged, INotifyCollectionChanged, and IReadOnlyList{MeasurableValueEntity}.
/// UI elements (e.g. ItemsControl) can bind directly to the SensorEntity instance itself.
/// </summary>
public sealed class SensorEntity : IReadOnlyList<MeasurableValueEntity>, INotifyPropertyChanged, INotifyCollectionChanged
{
    private readonly ObservableCollection<MeasurableValueEntity> _measurableValues = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    #region 1. Positional Identity
    /// <summary>
    /// Zero-based immutable module index matching hardware slot / array offset.
    /// </summary>
    public int Index { get; }
    #endregion

    #region 2. Configuration State
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        private set => SetField(ref _title, value);
    }

    /// <summary>
    /// Explicit read-only exposure of child collection for standard sub-property bindings.
    /// </summary>
    public IReadOnlyList<MeasurableValueEntity> MeasurableValues => this;
    #endregion

    #region 3. Real-Time Telemetry / Operational State
    private ModuleState _state = ModuleState.Offline;
    public ModuleState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    private DateTimeOffset _lastSeenUtc;
    public DateTimeOffset LastSeenUtc
    {
        get => _lastSeenUtc;
        private set => SetField(ref _lastSeenUtc, value);
    }
    #endregion

    #region IReadOnlyList<MeasurableValueEntity> Delegation
    public int Count => _measurableValues.Count;
    public MeasurableValueEntity this[int index] => _measurableValues[index];
    public IEnumerator<MeasurableValueEntity> GetEnumerator() => _measurableValues.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion

    public SensorEntity(int index, SensorOptions? initialOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        Index = index;
        _lastSeenUtc = DateTimeOffset.UtcNow;

        // Bridge internal collection events directly to SensorEntity consumers
        ((INotifyCollectionChanged)_measurableValues).CollectionChanged += (_, args) =>
        {
            CollectionChanged?.Invoke(this, args);
        };

        ((INotifyPropertyChanged)_measurableValues).PropertyChanged += (_, args) =>
        {
            // Propagate Count and indexer changes to external listeners
            PropertyChanged?.Invoke(this, args);
        };

        if (initialOptions is not null)
        {
            ApplyConfiguration(initialOptions);
        }
    }

    /// <summary>
    /// Performs in-place positional delta update of title and child measurement channels.
    /// </summary>
    public bool ApplyConfiguration(SensorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Title = options.Title ?? string.Empty;

        var configuredCount = options.MeasurableValues.Count;

        // 1. In-place update existing channels or append newly added channels
        for (var i = 0; i < configuredCount; i++)
        {
            var channelOptions = options.MeasurableValues[i];

            if (i < _measurableValues.Count)
            {
                _measurableValues[i].ApplyConfiguration(channelOptions);
            }
            else
            {
                _measurableValues.Add(new MeasurableValueEntity(i, channelOptions));
            }
        }

        // 2. Truncate excess channel entities if configuration size shrank
        while (_measurableValues.Count > configuredCount)
        {
            _measurableValues.RemoveAt(_measurableValues.Count - 1);
        }

        return true;
    }

    public void UpdateState(ModuleState newState, DateTimeOffset? timestamp = null)
    {
        State = newState;
        LastSeenUtc = timestamp ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Ingests live telemetry into a child channel using zero-based positional channel indexing.
    /// </summary>
    public bool IngestChannelTelemetry(int channelIndex, double rawValue, DateTimeOffset? timestamp = null)
    {
        if (channelIndex < 0 || channelIndex >= _measurableValues.Count)
        {
            return false;
        }

        _measurableValues[channelIndex].UpdateTelemetry(rawValue, timestamp);
        LastSeenUtc = timestamp ?? DateTimeOffset.UtcNow;
        return true;
    }

    #region INotifyPropertyChanged Helpers
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}
