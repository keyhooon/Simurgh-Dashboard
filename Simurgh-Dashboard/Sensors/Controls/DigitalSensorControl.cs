using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimurghDashboard.Sensors.Controls.Sensors;

namespace SimurghDashboard.Sensors.Controls
{
    /// <summary>
    /// Represents the UI Control for a multiplexed Digital Sensor.
    /// Consumes an immutable <see cref="SensorModuleConfigurationModel"/> for structural definition,
    /// and reacts to raw telemetry (<see cref="MeasurementRawTelemetry"/>) which is internally
    /// transformed into a render-ready payload via the <see cref="SensorModuleEvaluationEngine"/>.
    ///
    /// The control is deliberately stateless regarding domain logic:
    /// it only renders what the evaluation engine produces.
    /// </summary>
    public sealed class DigitalSensorControl : Control
    {
        /// <summary>
        /// Internal evaluation engine responsible for transforming raw hardware data
        /// into a validated, immutable <see cref="SensorModuleDataPayload"/>.
        /// </summary>
        private readonly SensorModuleEvaluationEngine _engine = new();

        static DigitalSensorControl()
        {
            // Associate the control with its dedicated resource dictionary ControlTemplate.
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(DigitalSensorControl),
                new FrameworkPropertyMetadata(typeof(DigitalSensorControl)));
        }

        public DigitalSensorControl()
        {
            // Exposes a push-style command that accepts an immutable batch of raw telemetry,
            // enabling direct data injection without strict DependencyProperty binding.
            UpdateDataCommand = new RelayCommand<ImmutableArray<MeasurementRawTelemetry>>(UpdateRawTelemetry);
        }

        #region Commands

        /// <summary>
        /// Command that accepts an immutable array of raw hardware measurements.
        /// Serves as an alternative ingestion path to the <see cref="RawTelemetry"/> DP for ViewModel-driven updates.
        /// </summary>
        public ICommand UpdateDataCommand { get; }

        #endregion

        #region Dependency Properties – Identity

        /// <summary>
        /// Unique identifier for the sensor instance.
        /// </summary>
        public string Id
        {
            get => (string)GetValue(IdProperty);
            set => SetValue(IdProperty, value);
        }

        public static readonly DependencyProperty IdProperty =
            DependencyProperty.Register(
                nameof(Id),
                typeof(string),
                typeof(DigitalSensorControl),
                new PropertyMetadata(string.Empty));

        #endregion

        #region Dependency Properties – Styling

        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(
                nameof(PlaceholderBrush),
                typeof(Brush),
                typeof(DigitalSensorControl),
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
                typeof(DigitalSensorControl),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(Color.FromRgb(255, 120, 120)),
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
                typeof(DigitalSensorControl),
                new FrameworkPropertyMetadata(
                    "Sensor Module",
                    FrameworkPropertyMetadataOptions.None,
                    OnHeaderTextChanged));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        #endregion

        #region Dependency Properties – Architecture & State

        public static readonly DependencyProperty ConfigurationProperty =
            DependencyProperty.Register(
                nameof(Configuration),
                typeof(SensorModuleConfigurationModel),
                typeof(DigitalSensorControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnConfigurationChanged));

        public SensorModuleConfigurationModel? Configuration
        {
            get => (SensorModuleConfigurationModel?)GetValue(ConfigurationProperty);
            set => SetValue(ConfigurationProperty, value);
        }

        private static void OnConfigurationChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is DigitalSensorControl control)
            {
                control.RebuildDisplayItems();
            }
        }

        public static readonly DependencyProperty RawTelemetryProperty =
            DependencyProperty.Register(
                nameof(RawTelemetry),
                typeof(ImmutableArray<MeasurementRawTelemetry>),
                typeof(DigitalSensorControl),
                new FrameworkPropertyMetadata(
                    ImmutableArray<MeasurementRawTelemetry>.Empty,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnRawTelemetryChanged));

        public ImmutableArray<MeasurementRawTelemetry> RawTelemetry
        {
            get => (ImmutableArray<MeasurementRawTelemetry>)GetValue(RawTelemetryProperty);
            set => SetValue(RawTelemetryProperty, value);
        }

        private static void OnRawTelemetryChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is DigitalSensorControl control)
            {
                var newValue = (ImmutableArray<MeasurementRawTelemetry>)e.NewValue;
                if (newValue.IsDefaultOrEmpty) return;
                control.ApplyRawTelemetry(newValue);
            }
        }

        private static readonly DependencyPropertyKey StatePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(State),
                typeof(ModuleState),
                typeof(DigitalSensorControl),
                new PropertyMetadata(ModuleState.Offline));

        public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

        public ModuleState State
        {
            get => (ModuleState)GetValue(StateProperty);
            private set => SetValue(StatePropertyKey, value);
        }

        private static readonly DependencyPropertyKey DisplayItemsPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(DisplayItems),
                typeof(ImmutableArray<SensorMeasurementDisplayItem>),
                typeof(DigitalSensorControl),
                new PropertyMetadata(ImmutableArray<SensorMeasurementDisplayItem>.Empty));

        public static readonly DependencyProperty DisplayItemsProperty = DisplayItemsPropertyKey.DependencyProperty;

        /// <summary>
        /// A merged, render-ready collection of Configuration + evaluated Telemetry.
        /// Consumed directly by an ItemsControl inside the ControlTemplate.
        /// </summary>
        public ImmutableArray<SensorMeasurementDisplayItem> DisplayItems =>
            (ImmutableArray<SensorMeasurementDisplayItem>)GetValue(DisplayItemsProperty);

        #endregion

        #region Core Logic

        /// <summary>
        /// Centralized processing entry point.
        /// Invoked from both the <see cref="RawTelemetry"/> DP callback and the <see cref="UpdateDataCommand"/>.
        /// Performs: evaluation → state resolution → display items reconstruction → visual state transition.
        /// </summary>
        private void ApplyRawTelemetry(ImmutableArray<MeasurementRawTelemetry> rawReadings)
        {
            // Guard: no structural configuration means nothing can be rendered.
            if (Configuration is null) return;

            // 1. Transform raw hardware data into an evaluated, immutable payload.
            var payload = _engine.Evaluate(Configuration, rawReadings);

            // 2. Publish the resolved overall module state (drives template visual triggers).
            State = payload.State;

            // 3. Merge configuration with the evaluated telemetry into display-bound items.
            var updatedDisplayItems = Configuration.Measurements.Select(config =>
            {
                // Locate the matching evaluated telemetry entry for this measurement.
                var telemetry = payload.TelemetryData.FirstOrDefault(t => t.MeasurementId == config.MeasurementId);

                return new SensorMeasurementDisplayItem(
                    Config: config,
                    FormattedValue: telemetry?.FormattedValue ?? "--.-",
                    IsAlarmActive: telemetry?.IsAlarmActive ?? false
                );
            }).ToImmutableArray();

            // 4. Attempt the visual state transition (drives the ControlTemplate's VisualStateManager).
            var newState = payload.State switch
            {
                ModuleState.Warning => "Warning",
                ModuleState.Error => "Error",
                _ => "Normal",
            };
            VisualStateManager.GoToState(this, newState, useTransitions: true);

            // 5. Atomically publish the fully-formed collection for rendering.
            SetValue(DisplayItemsPropertyKey, updatedDisplayItems);
        }

        /// <summary>
        /// Rebuilds the display items collection with placeholder values.
        /// Invoked when the configuration is (re)assigned but no telemetry has been received yet.
        /// </summary>
        private void RebuildDisplayItems()
        {
            if (Configuration is null)
            {
                // No configuration: expose an empty collection so the ItemsControl renders nothing.
                SetValue(DisplayItemsPropertyKey, ImmutableArray<SensorMeasurementDisplayItem>.Empty);
                return;
            }

            // Create placeholder display items when configuration is set but no payload is received yet.
            var defaultDisplayItems = Configuration.Measurements.Select(config =>
                new SensorMeasurementDisplayItem(
                    Config: config,
                    FormattedValue: "--.-",
                    IsAlarmActive: false
                )).ToImmutableArray();

            SetValue(DisplayItemsPropertyKey, defaultDisplayItems);
        }

        /// <summary>
        /// Command handler: delegates to the same centralized method used by the DP path.
        /// </summary>
        private void UpdateRawTelemetry(ImmutableArray<MeasurementRawTelemetry> rawReadings)
        {
            ApplyRawTelemetry(rawReadings);
        }

        #endregion

        #region Internal Infrastructure

        /// <summary>
        /// Minimal relay command implementation to support ICommand binding
        /// without pulling an external MVVM framework into the control layer.
        /// </summary>
        private class RelayCommand<T>(Action<T?> execute) : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute((T?)parameter);
        }

        #endregion
    }
}
