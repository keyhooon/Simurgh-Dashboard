using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.ViewModels;

/// <summary>
/// Presentation ViewModel representing a complete sensor module.
/// Maintains dynamic synchronization with child <see cref="MeasurableValueEntity"/> collections,
/// wraps child items in <see cref="MeasurableValueViewModel"/>, and computes aggregate UI state.
/// </summary>
public sealed partial class SensorViewModel : ObservableObject, IDisposable
{
    private readonly SensorEntity _model;
    private readonly ObservableCollection<MeasurableValueViewModel> _measurableValues = [];
    private bool _isDisposed;

    #region Static State Brushes

    private static readonly SolidColorBrush OnlineBrush;
    private static readonly SolidColorBrush OfflineBrush;
    private static readonly SolidColorBrush WarningBrush;
    private static readonly SolidColorBrush ErrorBrush;

    static SensorViewModel()
    {
        OnlineBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xE6, 0x76));
        OnlineBrush.Freeze();

        OfflineBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x75, 0x75, 0x75));
        OfflineBrush.Freeze();

        WarningBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xAB, 0x00));
        WarningBrush.Freeze();

        ErrorBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x17, 0x44));
        ErrorBrush.Freeze();
    }

    #endregion

    #region Properties

    public int Index => _model.Index;

    public string Title => _model.Title;

    public ModuleState State => _model.State;

    public DateTimeOffset LastSeenUtc => _model.LastSeenUtc;

    public ReadOnlyObservableCollection<MeasurableValueViewModel> MeasurableValues { get; }

    public bool IsOnline => _model.State == ModuleState.Online;

    public Brush StateBrush => _model.State switch
    {
        ModuleState.Online => OnlineBrush,
        ModuleState.Warning => WarningBrush,
        ModuleState.Error => ErrorBrush,
        _ => OfflineBrush
    };

    public bool HasActiveAlarm => _measurableValues.Any(v => v.HasAlarm);

    #endregion

    public SensorViewModel(SensorEntity model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        MeasurableValues = new ReadOnlyObservableCollection<MeasurableValueViewModel>(_measurableValues);

        // Populate initial channel ViewModels
        for (var i = 0; i < _model.Count; i++)
        {
            var vm = new MeasurableValueViewModel(_model[i]);
            vm.PropertyChanged += OnChildViewModelPropertyChanged;
            _measurableValues.Add(vm);
        }

        // Subscribe to domain model events
        _model.PropertyChanged += OnModelPropertyChanged;
        _model.CollectionChanged += OnModelCollectionChanged;
    }

    #region Event Bridge & Collection Synchronization

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SensorEntity.Title):
                OnPropertyChanged(nameof(Title));
                break;

            case nameof(SensorEntity.State):
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(StateBrush));
                break;

            case nameof(SensorEntity.LastSeenUtc):
                OnPropertyChanged(nameof(LastSeenUtc));
                break;

            case null or "":
                OnPropertyChanged(string.Empty);
                break;
        }
    }

    private void OnModelCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    for (var i = 0; i < e.NewItems.Count; i++)
                    {
                        var entity = (MeasurableValueEntity)e.NewItems[i]!;
                        var insertIndex = e.NewStartingIndex + i;
                        var vm = new MeasurableValueViewModel(entity);
                        vm.PropertyChanged += OnChildViewModelPropertyChanged;
                        _measurableValues.Insert(insertIndex, vm);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    for (var i = 0; i < e.OldItems.Count; i++)
                    {
                        var removedVm = _measurableValues[e.OldStartingIndex];
                        removedVm.PropertyChanged -= OnChildViewModelPropertyChanged;
                        removedVm.Dispose();
                        _measurableValues.RemoveAt(e.OldStartingIndex);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                foreach (var vm in _measurableValues)
                {
                    vm.PropertyChanged -= OnChildViewModelPropertyChanged;
                    vm.Dispose();
                }
                _measurableValues.Clear();

                for (var i = 0; i < _model.Count; i++)
                {
                    var vm = new MeasurableValueViewModel(_model[i]);
                    vm.PropertyChanged += OnChildViewModelPropertyChanged;
                    _measurableValues.Add(vm);
                }
                break;
        }

        OnPropertyChanged(nameof(HasActiveAlarm));
    }

    private void OnChildViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeasurableValueViewModel.HasAlarm))
        {
            OnPropertyChanged(nameof(HasActiveAlarm));
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.CollectionChanged -= OnModelCollectionChanged;

        foreach (var vm in _measurableValues)
        {
            vm.PropertyChanged -= OnChildViewModelPropertyChanged;
            vm.Dispose();
        }
        _measurableValues.Clear();
    }

    #endregion
}
