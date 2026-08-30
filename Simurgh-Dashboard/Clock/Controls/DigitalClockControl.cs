using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SimurghDashboard.Clock.Models;

namespace SimurghDashboard.Clock.Controls;

public sealed class DigitalClockControl : Control
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
            // The displayed time changes at most once per second.
            Interval = TimeSpan.FromMilliseconds(500)
        };

        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateClock();

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    #endregion

    #region Clock

    private void UpdateClock()
    {
        var now = DateTime.Now;

        Time = now;
        TimeText = FormatTime(now);
        DateText = FormatJalaliDate(now);
    }

    private string FormatTime(DateTime dateTime)
    {
        var pattern = ShowSeconds
            ? "HH:mm:ss"
            : "HH:mm";

        var formattedTime = dateTime.ToString(
            pattern,
            CultureInfo.InvariantCulture);

        return ToLatinDigits(formattedTime);
    }

    private static string ToLatinDigits(string value)
    {
        return string.Create(
            value.Length,
            value,
            static (destination, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    destination[index] = source[index] switch
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
                        _ => source[index]
                    };
                }
            });
    }

    private static string FormatJalaliDate(DateTime dateTime)
    {
        var culture = new CultureInfo("fa-IR");

        culture.DateTimeFormat.Calendar = new PersianCalendar();

        return dateTime.ToString(
            "dddd d MMMM yyyy",
            culture);
    }

    private static void OnShowSecondsChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var control = (DigitalClockControl)dependencyObject;

        // Refresh the displayed time immediately after the format changes.
        if (control.IsLoaded)
        {
            control.UpdateClock();
        }
    }

    #endregion

    #region Clock Dependency Properties

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
            new FrameworkPropertyMetadata(DateTime.Now));

    public static readonly DependencyProperty TimeProperty =
        TimePropertyKey.DependencyProperty;

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
            new FrameworkPropertyMetadata("88:88:88"));

    public static readonly DependencyProperty TimeTextProperty =
        TimeTextPropertyKey.DependencyProperty;

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
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DateTextProperty =
        DateTextPropertyKey.DependencyProperty;

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
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnShowSecondsChanged));

    #endregion

    #region Weather Dependency Property

    public WeatherState WeatherState
    {
        get => (WeatherState)GetValue(WeatherStateProperty);
        set => SetValue(WeatherStateProperty, value);
    }

    public static readonly DependencyProperty WeatherStateProperty =
        DependencyProperty.Register(
            nameof(WeatherState),
            typeof(WeatherState),
            typeof(DigitalClockControl),
            new FrameworkPropertyMetadata(
                new WeatherState()));

    #endregion

    #region Appearance Dependency Properties

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
            new FrameworkPropertyMetadata(
                CreateDefaultBrush(0xFF, 0x7D, 0x00, 0x00)));

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
            new FrameworkPropertyMetadata(
                CreateDefaultBrush(0x33, 0x7D, 0x00, 0x00)));

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
            new FrameworkPropertyMetadata(
                CreateDefaultBrush(0xFF, 0x7D, 0x00, 0x00)));

    private static SolidColorBrush CreateDefaultBrush(
        byte alpha,
        byte red,
        byte green,
        byte blue)
    {
        var brush = new SolidColorBrush(
            Color.FromArgb(alpha, red, green, blue));

        // Freeze immutable default brushes for better rendering performance.
        brush.Freeze();

        return brush;
    }

    #endregion
}
