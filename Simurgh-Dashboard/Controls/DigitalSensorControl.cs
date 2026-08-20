using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimurghDashboard.Controls.Sensors;

namespace SimurghDashboard.Controls
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
                    "Sensor Module",                              // default value
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

        #region Dependency Properties – Architecture & State

        // -------------------------------------------------------------------------
        // CONFIGURATION
        // -------------------------------------------------------------------------

        public static readonly DependencyProperty ConfigurationProperty =
            DependencyProperty.Register(
                nameof(Configuration),
                typeof(SensorModuleConfigurationModel),
                typeof(DigitalSensorControl),
                new PropertyMetadata(null, OnConfigurationChanged));

        /// <summary>
        /// Immutable structural definition of the sensor module (measurement set, units, thresholds).
        /// Changing this triggers a rebuild of the display items collection.
        /// </summary>
        public SensorModuleConfigurationModel? Configuration
        {
            get => (SensorModuleConfigurationModel?)GetValue(ConfigurationProperty);
            set => SetValue(ConfigurationProperty, value);
        }

        private static void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Whenever the hardware configuration is (re)assigned, regenerate the placeholder display state.
            if (d is DigitalSensorControl control)
            {
                control.RebuildDisplayItems();
            }
        }

        // -------------------------------------------------------------------------
        // RAW TELEMETRY (The reactive entry point)
        // -------------------------------------------------------------------------

        public static readonly DependencyProperty RawTelemetryProperty =
            DependencyProperty.Register(
                nameof(RawTelemetry),
                typeof(ImmutableArray<MeasurementRawTelemetry>),
                typeof(DigitalSensorControl),
                new PropertyMetadata(ImmutableArray<MeasurementRawTelemetry>.Empty, OnRawTelemetryChanged));

        /// <summary>
        /// The raw telemetry data fed from the ViewModel.
        /// On change, the control re-evaluates thresholds and updates display state atomically.
        /// </summary>
        public ImmutableArray<MeasurementRawTelemetry> RawTelemetry
        {
            get => (ImmutableArray<MeasurementRawTelemetry>)GetValue(RawTelemetryProperty);
            set => SetValue(RawTelemetryProperty, value);
        }

        private static void OnRawTelemetryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Route the DP path to the same centralized processing method used by the command path.
            if (d is DigitalSensorControl control)
            {
                control.ApplyRawTelemetry((ImmutableArray<MeasurementRawTelemetry>)e.NewValue);
            }
        }

        // -------------------------------------------------------------------------
        // READ-ONLY EXPOSED STATES (For ControlTemplate Triggers and Binding)
        // -------------------------------------------------------------------------

        private static readonly DependencyPropertyKey StatePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(State),
                typeof(ModuleState),
                typeof(DigitalSensorControl),
                new PropertyMetadata(ModuleState.Offline));

        public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

        /// <summary>
        /// The evaluated overall module state (Online, Warning, Error, etc.).
        /// Read-only: it is exclusively updated by the internal evaluation engine.
        /// </summary>
        public ModuleState State => (ModuleState)GetValue(StateProperty);

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
            SetValue(StatePropertyKey, payload.State);

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
            // The command is always executable; no can-execute gating is required here.
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute((T?)parameter);
        }

        #endregion
    }

    /// <summary>
    /// Represents the merged state of Configuration and current Telemetry for UI binding within the ControlTemplate.
    /// Each instance corresponds to one physical measurement being rendered by the ItemsControl.
    /// </summary>
    public record SensorMeasurementDisplayItem(
        SensorMeasurementConfig Config,
        string FormattedValue,
        bool IsAlarmActive);
}
