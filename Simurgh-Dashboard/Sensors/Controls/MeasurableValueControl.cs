using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimurghDashboard.Sensors.Controls;

/// <summary>
/// Templated display row for an individual measurable value/channel.
/// Encapsulates segmented digital typography, icon geometry, unit readouts, and state brushes.
/// Completely decoupled from domain models and ViewModels via DependencyProperties.
/// </summary>
public class MeasurableValueControl : Control
{
    static MeasurableValueControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MeasurableValueControl),
            new FrameworkPropertyMetadata(typeof(MeasurableValueControl)));
    }

    #region Positional & Identification

    public static readonly DependencyProperty ChannelIndexProperty =
        DependencyProperty.Register(
            nameof(ChannelIndex),
            typeof(int),
            typeof(MeasurableValueControl),
            new PropertyMetadata(0));

    public int ChannelIndex
    {
        get => (int)GetValue(ChannelIndexProperty);
        set => SetValue(ChannelIndexProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(MeasurableValueControl),
            new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(
            nameof(IconData),
            typeof(Geometry),
            typeof(MeasurableValueControl),
            new PropertyMetadata(null));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    #endregion

    #region Value & Segment Display

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(
            nameof(ValueText),
            typeof(string),
            typeof(MeasurableValueControl),
            new PropertyMetadata("--.-"));

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(MeasurableValueControl),
            new PropertyMetadata("88.8"));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(string),
            typeof(MeasurableValueControl),
            new PropertyMetadata(string.Empty));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    #endregion

    #region Visual Brushes

    public static readonly DependencyProperty DigitBrushProperty =
        DependencyProperty.Register(
            nameof(DigitBrush),
            typeof(Brush),
            typeof(MeasurableValueControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76))));

    public Brush DigitBrush
    {
        get => (Brush)GetValue(DigitBrushProperty);
        set => SetValue(DigitBrushProperty, value);
    }

    public static readonly DependencyProperty PlaceholderBrushProperty =
        DependencyProperty.Register(
            nameof(PlaceholderBrush),
            typeof(Brush),
            typeof(MeasurableValueControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF))));

    public Brush PlaceholderBrush
    {
        get => (Brush)GetValue(PlaceholderBrushProperty);
        set => SetValue(PlaceholderBrushProperty, value);
    }

    public static readonly DependencyProperty UnitBrushProperty =
        DependencyProperty.Register(
            nameof(UnitBrush),
            typeof(Brush),
            typeof(MeasurableValueControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70))));

    public Brush UnitBrush
    {
        get => (Brush)GetValue(UnitBrushProperty);
        set => SetValue(UnitBrushProperty, value);
    }

    #endregion
}
