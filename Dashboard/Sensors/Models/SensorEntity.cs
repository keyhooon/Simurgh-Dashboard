using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Simurgh.Dashboard.Sensors.Options;

namespace Simurgh.Dashboard.Sensors.Models;

/// <summary>
/// Domain model for an entire sensor module identified by its positional array index.
/// Directly implements INotifyPropertyChanged, INotifyCollectionChanged, and IReadOnlyList{MeasurableValueEntity}.
/// UI elements (e.g. ItemsControl) can bind directly to the SensorEntity instance itself.
/// </summary>
public sealed class SensorEntity : IReadOnlyList<MeasurableValueEntity>, INotifyPropertyChanged, INotifyCollectionChanged
{
    private readonly ObservableCollection<MeasurableValueEntity> _measurableValues = [];

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the collection changes.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Gets the zero-based index of the sensor.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the title of the sensor.
    /// </summary>
    public string Title
    {
        get => _title;
        private set => SetField(ref _title, value);
    }

    /// <summary>
    /// Gets the read-only list of measurable value entities.
    /// </summary>
    public IReadOnlyList<MeasurableValueEntity> MeasurableValues => this;

    /// <summary>
    /// Gets the number of measurable value entities.
    /// </summary>
    public int Count => _measurableValues.Count;

    /// <summary>
    /// Gets the measurable value entity at the specified index.
    /// </summary>
    /// <param name="index">The index of the measurable value entity.</param>
    /// <returns>The measurable value entity.</returns>
    public MeasurableValueEntity this[int index] => _measurableValues[index];

    /// <summary>
    /// Initializes a new instance of the SensorEntity class.
    /// </summary>
    /// <param name="index">The zero-based index of the sensor.</param>
    /// <param name="initialOptions">The initial configuration options for the sensor.</param>
    public SensorEntity(int index, SensorOptions? initialOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        Index = index;
        _lastSeenUtc = DateTimeOffset.UtcNow;

        ((INotifyCollectionChanged)_measurableValues).CollectionChanged += (_, args) =>
        {
            CollectionChanged?.Invoke(this, args);
        };

        ((INotifyPropertyChanged)_measurableValues).PropertyChanged += (_, args) =>
        {
            PropertyChanged?.Invoke(this, args);
        };

        if (initialOptions is not null)
        {
            ApplyConfiguration(initialOptions);
        }
    }

    /// <summary>
    /// Applies configuration options to the sensor entity.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <returns>True if the configuration was applied; otherwise, false.</returns>
    public bool ApplyConfiguration(SensorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Title = options.Title ?? string.Empty;

        var configuredCount = options.MeasurableValues.Count;

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

        while (_measurableValues.Count > configuredCount)
        {
            _measurableValues.RemoveAt(_measurableValues.Count - 1);
        }

        return true;
    }

    /// <summary>
    /// Updates the state of the sensor.
    /// </summary>
    /// <param name="newState">The new state of the sensor.</param>
    /// <param name="timestamp">The timestamp of the state update.</param>
    public void UpdateState(ModuleState newState, DateTimeOffset? timestamp = null)
    {
        State = newState;
        LastSeenUtc = timestamp ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Ingests channel telemetry into the sensor entity.
    /// </summary>
    /// <param name="channelIndex">The zero-based index of the channel.</param>
    /// <param name="value">The telemetry value.</param>
    /// <param name="timestamp">The timestamp of the telemetry.</param>
    /// <returns>True if the telemetry was ingested; otherwise, false.</returns>
    public bool IngestChannelTelemetry(int channelIndex, double value, DateTimeOffset? timestamp = null)
    {
        if (channelIndex < 0 || channelIndex >= _measurableValues.Count)
        {
            return false;
        }

        _measurableValues[channelIndex].UpdateTelemetry(value, timestamp);
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
