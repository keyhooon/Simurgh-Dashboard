using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.ViewModels;

/// <summary>
/// Root presentation ViewModel managing the entire collection of sensor modules.
/// Consumes <see cref="ISensorAccessor"/> and projects domain entities into <see cref="SensorViewModel"/>
/// with positional delta synchronization, aggregate health indicators, and memory safety.
/// </summary>
public sealed partial class SensorsRootViewModel : ObservableObject, IDisposable
{
    private readonly ISensorAccessor _sensorAccessor;
    private readonly ObservableCollection<SensorViewModel> _sensors = [];
    private bool _isDisposed;

    #region Properties

    /// <summary>
    /// Observable read-only collection of sensor view models for direct UI items binding.
    /// </summary>
    public ReadOnlyObservableCollection<SensorViewModel> Sensors { get; }

    /// <summary>
    /// Total count of configured active sensor modules.
    /// </summary>
    public int TotalSensorsCount => _sensors.Count;

    /// <summary>
    /// Count of currently online modules.
    /// </summary>
    public int OnlineSensorsCount => _sensors.Count(s => s.IsOnline);

    /// <summary>
    /// Global flag indicating if any module or sub-channel is in an abnormal/alarm state.
    /// </summary>
    public bool HasSystemAlarm => _sensors.Any(s => s.HasActiveAlarm);

    #endregion

    public SensorsRootViewModel(ISensorAccessor sensorAccessor)
    {
        _sensorAccessor = sensorAccessor ?? throw new ArgumentNullException(nameof(sensorAccessor));
        Sensors = new ReadOnlyObservableCollection<SensorViewModel>(_sensors);

        // Populate initial sensor module ViewModels
        for (var i = 0; i < _sensorAccessor.Count; i++)
        {
            var entity = _sensorAccessor[i];
            var vm = new SensorViewModel(entity);
            vm.PropertyChanged += OnSensorViewModelPropertyChanged;
            _sensors.Add(vm);
        }

        // Subscribe to ISensorAccessor observable collection and property changes
        _sensorAccessor.CollectionChanged += OnAccessorCollectionChanged;
        _sensorAccessor.PropertyChanged += OnAccessorPropertyChanged;
    }

    #region O(1) Positional Lookup

    /// <summary>
    /// Direct O(1) lookup for child SensorViewModel by its hardware/positional slot index.
    /// </summary>
    public SensorViewModel? GetSensorByIndex(int index)
    {
        if (index < 0 || index >= _sensors.Count)
        {
            return null;
        }

        return _sensors[index];
    }

    #endregion

    #region Event Handlers & Delta Synchronization

    private void OnAccessorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISensorAccessor.Count))
        {
            OnPropertyChanged(nameof(TotalSensorsCount));
            OnPropertyChanged(nameof(OnlineSensorsCount));
        }
    }

    private void OnAccessorCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    for (var i = 0; i < e.NewItems.Count; i++)
                    {
                        var entity = (SensorEntity)e.NewItems[i]!;
                        var insertIndex = e.NewStartingIndex + i;
                        var vm = new SensorViewModel(entity);
                        vm.PropertyChanged += OnSensorViewModelPropertyChanged;
                        _sensors.Insert(insertIndex, vm);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    for (var i = 0; i < e.OldItems.Count; i++)
                    {
                        var removedVm = _sensors[e.OldStartingIndex];
                        removedVm.PropertyChanged -= OnSensorViewModelPropertyChanged;
                        removedVm.Dispose();
                        _sensors.RemoveAt(e.OldStartingIndex);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems is not null && e.OldItems is not null)
                {
                    var oldVm = _sensors[e.OldStartingIndex];
                    oldVm.PropertyChanged -= OnSensorViewModelPropertyChanged;
                    oldVm.Dispose();

                    var newEntity = (SensorEntity)e.NewItems[0]!;
                    var newVm = new SensorViewModel(newEntity);
                    newVm.PropertyChanged += OnSensorViewModelPropertyChanged;
                    _sensors[e.OldStartingIndex] = newVm;
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                CleanupChildViewModels();
                for (var i = 0; i < _sensorAccessor.Count; i++)
                {
                    var entity = _sensorAccessor[i];
                    var vm = new SensorViewModel(entity);
                    vm.PropertyChanged += OnSensorViewModelPropertyChanged;
                    _sensors.Add(vm);
                }
                break;
        }

        OnPropertyChanged(nameof(TotalSensorsCount));
        OnPropertyChanged(nameof(OnlineSensorsCount));
        OnPropertyChanged(nameof(HasSystemAlarm));
    }

    private void OnSensorViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SensorViewModel.IsOnline):
            case nameof(SensorViewModel.State):
                OnPropertyChanged(nameof(OnlineSensorsCount));
                break;

            case nameof(SensorViewModel.HasActiveAlarm):
                OnPropertyChanged(nameof(HasSystemAlarm));
                break;
        }
    }

    private void CleanupChildViewModels()
    {
        foreach (var vm in _sensors)
        {
            vm.PropertyChanged -= OnSensorViewModelPropertyChanged;
            vm.Dispose();
        }
        _sensors.Clear();
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _sensorAccessor.CollectionChanged -= OnAccessorCollectionChanged;
        _sensorAccessor.PropertyChanged -= OnAccessorPropertyChanged;

        CleanupChildViewModels();
    }

    #endregion
}
