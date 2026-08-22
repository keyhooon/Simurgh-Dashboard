using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SimurghDashboard.ViewModels;

namespace SimurghDashboard.Controls
{
    public class DigitalClockControl : Control
    {
        private readonly DispatcherTimer _timer;

        static DigitalClockControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(DigitalClockControl),
                new FrameworkPropertyMetadata(typeof(DigitalClockControl)));
        }

        public DigitalClockControl()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMicroseconds(100)
            };

            _timer.Tick += OnTimerTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // -----------------------------
        // Lifecycle
        // -----------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClock();

            if (!_timer.IsEnabled)
                _timer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

            if (_timer.IsEnabled)
                _timer.Stop();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        // -----------------------------
        // Clock update logic
        // -----------------------------

        private void UpdateClock()
        {
            var now = DateTime.Now;

            // Time must always be English/Latin digits and culture-neutral
            var invariant = CultureInfo.InvariantCulture;

            var timePattern = ResolveTimeFormat();
            var timeText = now.ToString(timePattern, invariant);

            // Force Latin digits if needed
            timeText = ToLatinDigits(timeText);

            // Persian / Jalali date
            var dateText = FormatJalaliDate(now);

            Time = now;
            TimeText = timeText;
            DateText = dateText;
        }

        private string ResolveTimeFormat()
        {
            // Fixed English format
            return ShowSeconds ? "HH:mm:ss" : "HH:mm";
        }

        private static string ToLatinDigits(string input)
        {
            return string.Create(input.Length, input, (span, src) =>
            {
                for (var i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    span[i] = c switch
                    {
                        '\u06F0' => '0',
                        '\u06F1' => '1',
                        '\u06F2' => '2',
                        '\u06F3' => '3',
                        '\u06F4' => '4',
                        '\u06F5' => '5',
                        '\u06F6' => '6',
                        '\u06F7' => '7',
                        '\u06F8' => '8',
                        '\u06F9' => '9',
                        _ => c
                    };
                }
            });
        }

        private static string FormatJalaliDate(DateTime dateTime)
        {
            var culture = new CultureInfo("fa-IR")
            {
                DateTimeFormat =
                {
                    Calendar = new PersianCalendar()
                }
            };

            var pattern = "dddd d MMMM yyyy ";
            return dateTime.ToString(pattern, culture);
        }

        // -----------------------------
        // Dependency Properties
        // -----------------------------

        public DateTime Time
        {
            get => (DateTime)GetValue(TimeProperty);
            private set => SetValue(TimePropertyKey, value);
        }

        private static readonly DependencyPropertyKey TimePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(Time),
                typeof(DateTime),
                typeof(DigitalClockControl),
                new PropertyMetadata(DateTime.Now));

        public static readonly DependencyProperty TimeProperty = TimePropertyKey.DependencyProperty;

        public string TimeText
        {
            get => (string)GetValue(TimeTextProperty);
            private set => SetValue(TimeTextPropertyKey, value);
        }

        private static readonly DependencyPropertyKey TimeTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(TimeText),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("88:88:88"));

        public static readonly DependencyProperty TimeTextProperty = TimeTextPropertyKey.DependencyProperty;

        public string DateText
        {
            get => (string)GetValue(DateTextProperty);
            private set => SetValue(DateTextPropertyKey, value);
        }

        private static readonly DependencyPropertyKey DateTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(DateText),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DateTextProperty = DateTextPropertyKey.DependencyProperty;

        public bool ShowSeconds
        {
            get => (bool)GetValue(ShowSecondsProperty);
            set => SetValue(ShowSecondsProperty, value);
        }

        public static readonly DependencyProperty ShowSecondsProperty =
            DependencyProperty.Register(
                nameof(ShowSeconds),
                typeof(bool),
                typeof(DigitalClockControl),
                new PropertyMetadata(true));

        public Brush DigitBrush
        {
            get => (Brush)GetValue(DigitBrushProperty);
            set => SetValue(DigitBrushProperty, value);
        }

        public static readonly DependencyProperty DigitBrushProperty =
            DependencyProperty.Register(
                nameof(DigitBrush),
                typeof(Brush),
                typeof(DigitalClockControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0x00, 0x00))));

        public Brush PlaceholderBrush
        {
            get => (Brush)GetValue(PlaceholderBrushProperty);
            set => SetValue(PlaceholderBrushProperty, value);
        }

        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(
                nameof(PlaceholderBrush),
                typeof(Brush),
                typeof(DigitalClockControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0x7D, 0x00, 0x00))));

        public Brush DateBrush
        {
            get => (Brush)GetValue(DateBrushProperty);
            set => SetValue(DateBrushProperty, value);
        }

        public static readonly DependencyProperty DateBrushProperty =
            DependencyProperty.Register(
                nameof(DateBrush),
                typeof(Brush),
                typeof(DigitalClockControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0x00, 0x00))));

        // Weather pass-through properties (set by ViewModel via DataTemplate binding)

        public string Temperature
        {
            get => (string)GetValue(TemperatureProperty);
            set => SetValue(TemperatureProperty, value);
        }
        public static readonly DependencyProperty TemperatureProperty =
            DependencyProperty.Register(
                nameof(Temperature),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("--"));

        public string ConditionText
        {
            get => (string)GetValue(ConditionTextProperty);
            set => SetValue(ConditionTextProperty, value);
        }
        public static readonly DependencyProperty ConditionTextProperty =
            DependencyProperty.Register(
                nameof(ConditionText),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("Unknown"));

        public string ConditionIcon
        {
            get => (string)GetValue(ConditionIconProperty);
            set => SetValue(ConditionIconProperty, value);
        }
        public static readonly DependencyProperty ConditionIconProperty =
            DependencyProperty.Register(
                nameof(ConditionIcon),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("\u2601"));

        public string Humidity
        {
            get => (string)GetValue(HumidityProperty);
            set => SetValue(HumidityProperty, value);
        }
        public static readonly DependencyProperty HumidityProperty =
            DependencyProperty.Register(
                nameof(Humidity),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("--%"));

        public string Wind
        {
            get => (string)GetValue(WindProperty);
            set => SetValue(WindProperty, value);
        }
        public static readonly DependencyProperty WindProperty =
            DependencyProperty.Register(
                nameof(Wind),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("-- km/h"));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(DigitalClockControl),
                new PropertyMetadata(false));

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }
        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.Register(
                nameof(HasError),
                typeof(bool),
                typeof(DigitalClockControl),
                new PropertyMetadata(false));
    }
}
