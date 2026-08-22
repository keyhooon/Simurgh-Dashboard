using SimurghDashboard.Controls.Timers;
using SimurghDashboard.Infrastructures;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimurghDashboard.Controls
{
    /// <summary>
    /// Represents the UI Control and Engine for the Digital Timer.
    /// Operates purely on the TimerConfigurationModel payload and reacts to RequestedAction.
    /// </summary>
    public sealed class DigitalTimerControl : Control
    {
        private readonly DispatcherTimer _uiTimer;
        private DigitalTimerEngine? _engine;
        private bool _isLoading;
        private bool _isUpdatingConfiguration;
        private CancellationTokenSource? _saveCts;

        static DigitalTimerControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(DigitalTimerControl),
                new FrameworkPropertyMetadata(typeof(DigitalTimerControl)));
        }

        public DigitalTimerControl()
        {
            _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };

            _uiTimer.Tick += OnUiTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
            PauseCommand = new AsyncRelayCommand(PauseAsync, CanPause);
            ResumeCommand = new AsyncRelayCommand(ResumeAsync, CanResume);
            StopCommand = new AsyncRelayCommand(StopAsync, CanStop);
            ResetCommand = new AsyncRelayCommand(ResetAsync, CanReset);
        }

        public ITimerStateStore? StateStore { get; set; }

        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ResetCommand { get; }

        public event EventHandler? Completed;

        #region Dependency Properties - Styling

        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(
                nameof(PlaceholderBrush),
                typeof(Brush),
                typeof(DigitalTimerControl),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(45, 38, 50, 56)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush PlaceholderBrush
        {
            get => (Brush)GetValue(PlaceholderBrushProperty);
            set => SetValue(PlaceholderBrushProperty, value);
        }

        public static readonly DependencyProperty DigitBrushProperty =
            DependencyProperty.Register(
                nameof(DigitBrush),
                typeof(Brush),
                typeof(DigitalTimerControl),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(Color.FromRgb(99, 215, 255)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush DigitBrush
        {
            get => (Brush)GetValue(DigitBrushProperty);
            set => SetValue(DigitBrushProperty, value);
        }
        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(
                nameof(HeaderText),
                typeof(string),
                typeof(DigitalTimerControl),
                new FrameworkPropertyMetadata(
                    "Timer Module",                              // default value
                    FrameworkPropertyMetadataOptions.None,
                    OnHeaderTextChanged));                     // optional change callback

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        // Fires whenever the bound value changes — remove if not needed
        private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // cast to concrete type for direct access if required
            // var ctrl = (DigitalSensorControl)d;
        }

        #endregion

        #region Dependency Properties - Architecture & State

        private static readonly DependencyPropertyKey StatePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(State),
                typeof(TimerRunState),
                typeof(DigitalTimerControl),
                new PropertyMetadata(TimerRunState.Stopped));

        public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

        public TimerRunState State => (TimerRunState)GetValue(StateProperty);

        private static readonly DependencyPropertyKey CurrentValuePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(CurrentValue),
                typeof(TimeSpan),
                typeof(DigitalTimerControl),
                new PropertyMetadata(TimeSpan.Zero));

        public static readonly DependencyProperty CurrentValueProperty = CurrentValuePropertyKey.DependencyProperty;

        public TimeSpan CurrentValue => (TimeSpan)GetValue(CurrentValueProperty);

        private static readonly DependencyPropertyKey TimeTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(TimeText),
                typeof(string),
                typeof(DigitalTimerControl),
                new PropertyMetadata("00:00:00"));

        public static readonly DependencyProperty TimeTextProperty = TimeTextPropertyKey.DependencyProperty;

        public string TimeText => (string)GetValue(TimeTextProperty);

        // Core Data Model replacing individual configuration DPs
        public static readonly DependencyProperty ConfigurationProperty =
            DependencyProperty.Register(
                nameof(Configuration),
                typeof(TimerConfigurationModel),
                typeof(DigitalTimerControl),
                new PropertyMetadata(null, OnConfigurationModelChanged));

        public TimerConfigurationModel? Configuration
        {
            get => (TimerConfigurationModel?)GetValue(ConfigurationProperty);
            set => SetValue(ConfigurationProperty, value);
        }

        private static void OnConfigurationModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DigitalTimerControl control)
            {
                control._isUpdatingConfiguration = true;

                try
                {
                    // If the UI timer is active and we shouldn't disrupt a running timer, 
                    // additional checks can be added here.
                }
                finally
                {
                    control._isUpdatingConfiguration = false;

                    if (control.IsInitialized && control.State != TimerRunState.Running)
                    {
                        control.RebuildEngine();
                    }
                }
            }
        }

        // Trigger payload bound to ViewModel
        public static readonly DependencyProperty RequestedActionProperty =
            DependencyProperty.Register(
                nameof(RequestedAction),
                typeof(TimerAction),
                typeof(DigitalTimerControl),
                new FrameworkPropertyMetadata(TimerAction.None, OnRequestedActionChanged));

        public TimerAction RequestedAction
        {
            get => (TimerAction)GetValue(RequestedActionProperty);
            set => SetValue(RequestedActionProperty, value);
        }

        private static async void OnRequestedActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DigitalTimerControl control && e.NewValue is TimerAction action && action != TimerAction.None)
            {
                switch (action)
                {
                    case TimerAction.Start:
                        if (control.CanStart()) await control.StartAsync();
                        break;
                    case TimerAction.Pause:
                        if (control.CanPause()) await control.PauseAsync();
                        break;
                    case TimerAction.Resume:
                        if (control.CanResume()) await control.ResumeAsync();
                        break;
                    case TimerAction.Stop:
                        if (control.CanStop()) await control.StopAsync();
                        break;
                    case TimerAction.Reset:
                        if (control.CanReset()) await control.ResetAsync();
                        break;
                }

                // Reset the action to allow subsequent identical commands
                control.SetCurrentValue(RequestedActionProperty, TimerAction.None);
            }
        }

        #endregion

        #region Engine Operations

        public async Task StartAsync()
        {
            EnsureEngine();

            var nowUtc = DateTimeOffset.UtcNow;

            // Replaced old DPs with direct access to the Configuration model
            // NOTE: Assuming DigitalTimerEngine has a standard Create/Start based on the new TimerMode
            bool started = _engine!.Start(nowUtc);

            if (started)
            {
                await SaveSnapshotAsync();
                RefreshDisplay(raiseCompletedEvent: false);
            }
        }

        public async Task PauseAsync()
        {
            EnsureEngine();

            if (_engine!.Pause(DateTimeOffset.UtcNow))
            {
                await SaveSnapshotAsync();
                RefreshDisplay(raiseCompletedEvent: false);
            }
        }

        public async Task ResumeAsync()
        {
            EnsureEngine();

            if (_engine!.Resume(DateTimeOffset.UtcNow))
            {
                await SaveSnapshotAsync();
                RefreshDisplay(raiseCompletedEvent: false);
            }
        }

        public async Task StopAsync()
        {
            EnsureEngine();

            if (_engine!.Stop(DateTimeOffset.UtcNow))
            {
                await SaveSnapshotAsync();
                RefreshDisplay(raiseCompletedEvent: false);
            }
        }

        public async Task ResetAsync()
        {
            EnsureEngine();
            _engine!.Reset();
            await SaveSnapshotAsync();
            RefreshDisplay(raiseCompletedEvent: false);
        }

        #endregion

        #region Lifecycle & Event Handlers

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            try
            {
                // Requires a TimerId. In real usage, ensure Configuration or a fallback ID exists.
                string timerId = "DefaultTimer"; // Replace with Configuration ID if added to TimerConfigurationModel

                var snapshot = StateStore is null
                    ? null
                    : await StateStore.LoadAsync(timerId);

                _engine = snapshot is null
                    ? CreateConfiguredEngine()
                    : DigitalTimerEngine.Restore(snapshot, DateTimeOffset.UtcNow);

                var completedJustNow = _engine.State == TimerRunState.Completed &&
                                       snapshot is not null &&
                                       snapshot.State == TimerRunState.Running;

                if (completedJustNow)
                {
                    await SaveSnapshotAsync();
                }

                RefreshDisplay(raiseCompletedEvent: completedJustNow);
            }
            finally
            {
                _isLoading = false;
                RaiseCommandStates();
            }

            if (Configuration?.AutoStart == true && _engine is not null && _engine.State == TimerRunState.Stopped)
            {
                await StartAsync();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _uiTimer.Stop();
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = null;
        }

        private async void OnUiTick(object? sender, EventArgs e)
        {
            if (_engine is null)
            {
                return;
            }

            var result = _engine.Tick(DateTimeOffset.UtcNow);

            if (result.CompletedJustNow)
            {
                await SaveSnapshotAsync();
            }

            RefreshDisplay(raiseCompletedEvent: result.CompletedJustNow);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            if (State != TimerRunState.Running)
            {
                RebuildEngine();
            }
        }

        #endregion

        #region Helpers

        private void RefreshDisplay(bool raiseCompletedEvent)
        {
            EnsureEngine();

            var value = _engine!.GetCurrentValue(DateTimeOffset.UtcNow);
            var state = _engine.State;

            SetValue(StatePropertyKey, state);
            SetValue(CurrentValuePropertyKey, value);
            SetValue(TimeTextPropertyKey, FormatTime(value));

            if (state == TimerRunState.Running)
            {
                // Dynamically update UI interval based on Configuration payload
                var interval = Configuration?.UpdateInterval ?? TimeSpan.FromSeconds(1);
                if (_uiTimer.Interval != interval)
                {
                    _uiTimer.Interval = interval;
                }

                if (!_uiTimer.IsEnabled)
                {
                    _uiTimer.Start();
                }
            }
            else
            {
                _uiTimer.Stop();
            }

            RaiseCommandStates();

            if (raiseCompletedEvent && state == TimerRunState.Completed)
            {
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task SaveSnapshotAsync()
        {
            if (_isLoading || StateStore is null || _engine is null)
            {
                return;
            }

            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();

            // Needs an ID string. Typically stored in Configuration or external service.
            string timerId = "DefaultTimer";
            await StateStore.SaveAsync(timerId, _engine.Snapshot, _saveCts.Token);
        }

        private void EnsureEngine()
        {
            _engine ??= CreateConfiguredEngine();
        }

        private void RebuildEngine()
        {
            _engine = CreateConfiguredEngine();
            RefreshDisplay(raiseCompletedEvent: false);
        }

        private DigitalTimerEngine CreateConfiguredEngine()
        {
            // Extract everything strictly from the Configuration Model
            var config = Configuration ?? new TimerConfigurationModel();

            // NOTE: Adjusted creation method signature assuming DigitalTimerEngine aligns with standard mode
            return DigitalTimerEngine.Create(
                config.Mode,
                config.InitialValue,
                config.TargetValue ?? TimeSpan.Zero);
        }

        private bool CanStart() => _engine is not null && (_engine.State == TimerRunState.Stopped || _engine.State == TimerRunState.Completed);
        private bool CanPause() => _engine is not null && _engine.State == TimerRunState.Running;
        private bool CanResume() => _engine is not null && _engine.State == TimerRunState.Paused;
        private bool CanStop() => _engine is not null && _engine.State != TimerRunState.Stopped;
        private bool CanReset() => _engine is not null;

        private void RaiseCommandStates()
        {
            (StartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (PauseCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ResetCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private static string FormatTime(TimeSpan value)
        {
            value = value < TimeSpan.Zero ? TimeSpan.Zero : value;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}",
                (long)value.TotalHours,
                value.Minutes,
                value.Seconds);
        }

        #endregion
    }
}
