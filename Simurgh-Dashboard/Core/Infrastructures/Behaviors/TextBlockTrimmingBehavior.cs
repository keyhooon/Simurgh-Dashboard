using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimurghDashboard.Core.Infrastructures.Behaviors;

public static class TextBlockTrimmingBehavior
{
    // Attached dependency property tracking max line count limitation
    public static readonly DependencyProperty MaxLinesProperty =
        DependencyProperty.RegisterAttached(
            "MaxLines",
            typeof(int),
            typeof(TextBlockTrimmingBehavior),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnMaxLinesChanged));

    // Internal property storing raw unmodified text payload
    private static readonly DependencyProperty OriginalTextProperty =
        DependencyProperty.RegisterAttached(
            "OriginalText",
            typeof(string),
            typeof(TextBlockTrimmingBehavior),
            new PropertyMetadata(null));

    public static int GetMaxLines(DependencyObject element) => (int)element.GetValue(MaxLinesProperty);
    public static void SetMaxLines(DependencyObject element, int value) => element.SetValue(MaxLinesProperty, value);

    private static string? GetOriginalText(DependencyObject element) => (string?)element.GetValue(OriginalTextProperty);
    private static void SetOriginalText(DependencyObject element, string? value) => element.SetValue(OriginalTextProperty, value);

    private static void OnMaxLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        textBlock.Loaded -= OnTextBlockUpdated;
        textBlock.SizeChanged -= OnTextBlockUpdated;

        if ((int)e.NewValue > 0)
        {
            textBlock.Loaded += OnTextBlockUpdated;
            textBlock.SizeChanged += OnTextBlockUpdated;
            TruncateText(textBlock);
        }
    }

    private static void OnTextBlockUpdated(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            TruncateText(textBlock);
        }
    }

    private static void TruncateText(TextBlock textBlock)
    {
        int maxLines = GetMaxLines(textBlock);
        if (maxLines <= 0 || textBlock.ActualWidth <= 0) return;

        // Cache unmodified bound text before mutative display trims
        string? original = GetOriginalText(textBlock);
        if (original == null)
        {
            original = textBlock.Text;
            SetOriginalText(textBlock, original);
        }
        else if (textBlock.Text.EndsWith("…") == false && textBlock.Text != original)
        {
            // Upstream binding model changed payload
            original = textBlock.Text;
            SetOriginalText(textBlock, original);
        }

        if (string.IsNullOrEmpty(original)) return;

        double availableWidth = textBlock.ActualWidth - textBlock.Padding.Left - textBlock.Padding.Right;
        if (availableWidth <= 0) return;

        var typeface = new Typeface(
            textBlock.FontFamily,
            textBlock.FontStyle,
            textBlock.FontWeight,
            textBlock.FontStretch);

        double pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;

        // Measure formatted geometry against layout constraints
        FormattedText Measure(string candidate)
        {
            var ft = new FormattedText(
                candidate,
                CultureInfo.CurrentCulture,
                textBlock.FlowDirection,
                typeface,
                textBlock.FontSize,
                textBlock.Foreground,
                pixelsPerDip)
            {
                MaxTextWidth = availableWidth
            };

            if (textBlock.LineHeight > 0)
            {
                ft.LineHeight = textBlock.LineHeight;
            }

            return ft;
        }

        FormattedText fullMeasure = Measure(original);

        // Check if baseline layout already fits constraint
        var lineCount = fullMeasure.Height / (textBlock.LineHeight > 0 ? textBlock.LineHeight : fullMeasure.Height);
        if (fullMeasure.GetLineCount() <= maxLines)
        {
            textBlock.Text = original;
            return;
        }

        // Binary search bounds to fit max lines with terminal ellipsis
        const string ellipsis = "…";
        int low = 0;
        int high = original.Length;
        int bestLength = 0;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string testCandidate = original[..mid] + ellipsis;
            FormattedText ft = Measure(testCandidate);

            if (ft.GetLineCount() <= maxLines)
            {
                bestLength = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        textBlock.Text = original[..bestLength].TrimEnd() + ellipsis;
    }

    private static int GetLineCount(this FormattedText formattedText)
    {
        // Compute discrete line count from underlying text geometry vertical bounds
        var textLineCount = 0;
        var geometry = formattedText.BuildGeometry(new Point(0, 0));

        // Use intrinsic line count measurement via FormattedText height bounds
        double lineHeight = formattedText.LineHeight > 0 ? formattedText.LineHeight : formattedText.Height;
        if (lineHeight <= 0) return 1;

        return (int)Math.Ceiling(formattedText.Height / lineHeight);
    }
}
