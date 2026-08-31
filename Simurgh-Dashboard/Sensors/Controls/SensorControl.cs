using System.Collections;
using System.Windows;
using System.Windows.Controls;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Controls;

/// <summary>
/// Digital sensor card container control.
/// Acts strictly as an architectural container for sensor modules.
/// Inner measurement visuals (digits, brushes, icons) are delegated completely to child item templates or controls.
/// </summary>
public class SensorControl : Control
{
    static SensorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SensorControl),
            new FrameworkPropertyMetadata(typeof(SensorControl)));
    }

    #region Header & Identification

    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(SensorControl),
            new PropertyMetadata(string.Empty));

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public static readonly DependencyProperty SlotIndexProperty =
        DependencyProperty.Register(
            nameof(SlotIndex),
            typeof(int),
            typeof(SensorControl),
            new PropertyMetadata(0));

    public int SlotIndex
    {
        get => (int)GetValue(SlotIndexProperty);
        set => SetValue(SlotIndexProperty, value);
    }

    #endregion

    #region Operational State

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(ModuleState),
            typeof(SensorControl),
            new PropertyMetadata(ModuleState.Offline));

    public ModuleState State
    {
        get => (ModuleState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    #endregion

    #region Items Delegation (Templating)

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(SensorControl),
            new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(SensorControl),
            new PropertyMetadata(null));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(SensorControl),
            new PropertyMetadata(null));

    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    public static readonly DependencyProperty ItemsPanelProperty =
        DependencyProperty.Register(
            nameof(ItemsPanel),
            typeof(ItemsPanelTemplate),
            typeof(SensorControl),
            new PropertyMetadata(null));

    public ItemsPanelTemplate? ItemsPanel
    {
        get => (ItemsPanelTemplate?)GetValue(ItemsPanelProperty);
        set => SetValue(ItemsPanelProperty, value);
    }

    #endregion
}
