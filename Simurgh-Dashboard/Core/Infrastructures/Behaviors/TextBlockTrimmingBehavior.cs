using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimurghDashboard.Core.Infrastructures.Behaviors;

public static class TextBlockTrimmingBehavior
{
    public static readonly DependencyProperty MaxLinesProperty =
        DependencyProperty.RegisterAttached(
            "MaxLines",
            typeof(int),
            typeof(TextBlockTrimmingBehavior),
            new FrameworkPropertyMetadata(0, OnMaxLinesChanged),
            value => (int)value >= 0);

    public static int GetMaxLines(DependencyObject element) =>
        (int)element.GetValue(MaxLinesProperty);

    public static void SetMaxLines(DependencyObject element, int value) =>
        element.SetValue(MaxLinesProperty, value);

    private static void OnMaxLinesChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
            return;

        textBlock.Loaded -= OnLoaded;
        textBlock.SizeChanged -= OnSizeChanged;

        if ((int)e.NewValue <= 0)
        {
            // This behavior owns MaxHeight while enabled.
            textBlock.ClearValue(FrameworkElement.MaxHeightProperty);
            return;
        }

        textBlock.Loaded += OnLoaded;
        textBlock.SizeChanged += OnSizeChanged;

        ApplyLineLimit(textBlock);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) =>
        ApplyLineLimit((TextBlock)sender);

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyLineLimit((TextBlock)sender);

    private static void ApplyLineLimit(TextBlock textBlock)
    {
        int maxLines = GetMaxLines(textBlock);
        double lineHeight = textBlock.LineHeight;

        // Auto LineHeight is NaN; do not guess the font's line metrics.
        if (maxLines <= 0 ||
            double.IsNaN(lineHeight) ||
            double.IsInfinity(lineHeight) ||
            lineHeight <= 0)
        {
            return;
        }

        double maxHeight =
            maxLines * lineHeight +
            textBlock.Padding.Top +
            textBlock.Padding.Bottom;

        if (textBlock.MaxHeight != maxHeight)
        {
            // Only change layout; leave Text and its binding untouched.
            textBlock.SetCurrentValue(
                FrameworkElement.MaxHeightProperty,
                maxHeight);
        }
    }
}