using SimurghDashboard.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
                Interval = TimeSpan.FromSeconds(1)
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
            // fixed English format
            return ShowSeconds ? "HH:mm:ss" : "HH:mm";
        }

        private static string ToLatinDigits(string input)
        {
            // Invariant formatting usually already yields Latin digits,
            // but this guarantees it.
            var fa = new CultureInfo("fa-IR");
            return string.Create(input.Length, input, (span, src) =>
            {
                for (var i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    span[i] = c switch
                    {
                        '۰' => '0',
                        '۱' => '1',
                        '۲' => '2',
                        '۳' => '3',
                        '۴' => '4',
                        '۵' => '5',
                        '۶' => '6',
                        '۷' => '7',
                        '۸' => '8',
                        '۹' => '9',
                        _ => c
                    };
                }
            });
        }


        private static string FormatJalaliDate(DateTime dateTime)
        {
            var culture = new CultureInfo("fa-IR");
            culture.DateTimeFormat.Calendar = new PersianCalendar();

            var pattern = "dddd d MMMM yyyy "; // e.g. "dddd d MMMM yyyy"
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

        public string WeatherUrl
        {
            get => (string)GetValue(WeatherUrlProperty);
            set => SetValue(WeatherUrlProperty, value);
        }

        public static readonly DependencyProperty WeatherUrlProperty =
            DependencyProperty.Register(
                nameof(WeatherUrl),
                typeof(string),
                typeof(DigitalClockControl),
                new PropertyMetadata("https://wttr.in/Tehran?format=j1"));

        public IWeatherService WeatherService
        {
            get => (IWeatherService)GetValue(WeatherServiceProperty);
            set => SetValue(WeatherServiceProperty, value);
        }
        public static readonly DependencyProperty WeatherServiceProperty =
            DependencyProperty.Register(
                nameof(WeatherService),
                typeof(IWeatherService),
                typeof(DigitalClockControl),
                new PropertyMetadata(null));


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
