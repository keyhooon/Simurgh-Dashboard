using System;
using System.Windows;
using System.Windows.Controls;

namespace SimurghDashboard.Controls;

/// <summary>
/// Display-only weather badge control. All data is driven by the ViewModel through bindings.
/// No fetching logic — the ViewModel handles weather service calls and updates properties.
/// </summary>
public class WeatherBadgeControl : Control
{
    static WeatherBadgeControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(WeatherBadgeControl),
            new FrameworkPropertyMetadata(typeof(WeatherBadgeControl)));
    }

    #region Dependency Properties

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
            new PropertyMetadata(Visibility.Visible));

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
            new PropertyMetadata(Visibility.Visible));

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
            new PropertyMetadata(Visibility.Visible));

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
}
