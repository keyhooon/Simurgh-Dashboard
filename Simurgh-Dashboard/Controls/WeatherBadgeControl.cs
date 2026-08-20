using SimurghDashboard.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SimurghDashboard.Controls;

public class WeatherBadgeControl : Control
{
    private readonly DispatcherTimer _refreshTimer;
    private CancellationTokenSource? _cts;

    static WeatherBadgeControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(WeatherBadgeControl),
            new FrameworkPropertyMetadata(typeof(WeatherBadgeControl)));
    }

    public WeatherBadgeControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // تنظیم تایمر برای آپدیت خودکار
        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Tick += async (s, e) => await RefreshAsync();
    }

    #region Dependency Properties

    // اضافه شدن سرویس برای تزریق وابستگی (DI)
    public IWeatherService WeatherService
    {
        get => (IWeatherService)GetValue(WeatherServiceProperty);
        set => SetValue(WeatherServiceProperty, value);
    }
    public static readonly DependencyProperty WeatherServiceProperty =
        DependencyProperty.Register(
            nameof(WeatherService),
            typeof(IWeatherService),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(null, OnConfigChanged));

    // بازه زمانی آپدیت خودکار
    public TimeSpan RefreshInterval
    {
        get => (TimeSpan)GetValue(RefreshIntervalProperty);
        set => SetValue(RefreshIntervalProperty, value);
    }
    public static readonly DependencyProperty RefreshIntervalProperty =
        DependencyProperty.Register(
            nameof(RefreshInterval),
            typeof(TimeSpan),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(TimeSpan.FromMinutes(30), OnIntervalChanged));

    public string WeatherUrl
    {
        get => (string)GetValue(WeatherUrlProperty);
        set => SetValue(WeatherUrlProperty, value);
    }
    public static readonly DependencyProperty WeatherUrlProperty =
        DependencyProperty.Register(
            nameof(WeatherUrl),
            typeof(string),
            typeof(WeatherBadgeControl),
            new PropertyMetadata("https://wttr.in/Tehran?format=j1", OnConfigChanged));

    public string Temperature
    {
        get => (string)GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }
    public static readonly DependencyProperty TemperatureProperty =
        DependencyProperty.Register(
            nameof(Temperature),
            typeof(string),
            typeof(WeatherBadgeControl),
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
            typeof(WeatherBadgeControl),
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
            typeof(WeatherBadgeControl),
            new PropertyMetadata("☁"));

    public string Humidity
    {
        get => (string)GetValue(HumidityProperty);
        set => SetValue(HumidityProperty, value);
    }
    public static readonly DependencyProperty HumidityProperty =
        DependencyProperty.Register(
            nameof(Humidity),
            typeof(string),
            typeof(WeatherBadgeControl),
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
            typeof(WeatherBadgeControl),
            new PropertyMetadata("-- km/h"));

    public Visibility TemperatureVisibility
    {
        get => (Visibility)GetValue(TemperatureVisibilityProperty);
        set => SetValue(TemperatureVisibilityProperty, value);
    }
    public static readonly DependencyProperty TemperatureVisibilityProperty =
        DependencyProperty.Register(
            nameof(TemperatureVisibility),
            typeof(Visibility),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(Visibility.Visible));

    public Visibility ConditionTextVisibility
    {
        get => (Visibility)GetValue(ConditionTextVisibilityProperty);
        set => SetValue(ConditionTextVisibilityProperty, value);
    }
    public static readonly DependencyProperty ConditionTextVisibilityProperty =
        DependencyProperty.Register(
            nameof(ConditionTextVisibility),
            typeof(Visibility),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility HumidityVisibility
    {
        get => (Visibility)GetValue(HumidityVisibilityProperty);
        set => SetValue(HumidityVisibilityProperty, value);
    }
    public static readonly DependencyProperty HumidityVisibilityProperty =
        DependencyProperty.Register(
            nameof(HumidityVisibility),
            typeof(Visibility),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility WindVisibility
    {
        get => (Visibility)GetValue(WindVisibilityProperty);
        set => SetValue(WindVisibilityProperty, value);
    }
    public static readonly DependencyProperty WindVisibilityProperty =
        DependencyProperty.Register(
            nameof(WindVisibility),
            typeof(Visibility),
            typeof(WeatherBadgeControl),
            new PropertyMetadata(Visibility.Collapsed));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(WeatherBadgeControl),
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
            typeof(WeatherBadgeControl),
            new PropertyMetadata(false));

    #endregion

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WeatherBadgeControl { IsLoaded: true } control)
        {
            _ = control.RefreshAsync();
        }
    }

    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WeatherBadgeControl control)
        {
            control._refreshTimer.Interval = (TimeSpan)e.NewValue;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Interval = RefreshInterval;
        _refreshTimer.Start();
        await RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // توقف تایمر و لغو درخواست وب هنگام بسته شدن کنترل برای جلوگیری از Memory Leak
        _refreshTimer.Stop();
        CancelCurrentOperation();
    }

    public async Task RefreshAsync()
    {
        // سرویس باید از طریق DI (از پنجره اصلی) تزریق شده باشد
        if (WeatherService == null || string.IsNullOrWhiteSpace(WeatherUrl))
            return;

        CancelCurrentOperation();
        _cts = new CancellationTokenSource();

        IsLoading = true;
        HasError = false;

        try
        {
            var response = await WeatherService.GetWeatherAsync(WeatherUrl, _cts.Token);
            var current = response?.CurrentCondition?.FirstOrDefault();

            if (current is null)
            {
                HasError = true;
                return;
            }

            var description = current.WeatherDesc?.FirstOrDefault()?.Value ?? "Unknown";

            Temperature = $"{current.TempC}°";
            ConditionText = description;
            ConditionIcon = MapIcon(description);
            Humidity = $"{current.Humidity}%";
            Wind = $"{current.WindspeedKmph} km/h";
        }
        catch (OperationCanceledException)
        {
            // درخواست لغو شده است (هنگام بستن فرم رخ می‌دهد)، نیازی به مدیریت نیست
        }
        catch
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelCurrentOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private static string MapIcon(string condition)
    {
        var normalized = condition.Trim().ToLower(CultureInfo.InvariantCulture);

        if (normalized.Contains("sun") || normalized.Contains("clear"))
            return "\uf00d"; // wi-day-sunny

        if (normalized.Contains("partly"))
            return "\uf002"; // wi-day-cloudy

        if (normalized.Contains("cloud") || normalized.Contains("overcast"))
            return "\uf013"; // wi-cloudy

        if (normalized.Contains("rain") || normalized.Contains("drizzle") || normalized.Contains("shower"))
            return "\uf019"; // wi-rain

        if (normalized.Contains("thunder") || normalized.Contains("storm"))
            return "\uf01e"; // wi-thunderstorm

        if (normalized.Contains("snow") || normalized.Contains("sleet") || normalized.Contains("ice"))
            return "\uf01b"; // wi-snow

        if (normalized.Contains("mist") || normalized.Contains("fog") || normalized.Contains("haze"))
            return "\uf014"; // wi-fog

        return "\uf03e"; // wi-na (حالت نامشخص)
    }
}
