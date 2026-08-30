using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace SimurghDashboard.Core.Infrastructures;

/// <summary>
/// Converts null or empty (or whitespace-only) strings to Visibility enumeration.
/// Inherits from MarkupExtension to allow direct inline XAML usage without static resource declarations.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class StringNullOrEmptyToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Visibility when string is null, empty, or whitespace. Defaults to Collapsed.
    /// </summary>
    public Visibility NullOrEmptyVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Visibility when string contains meaningful text. Defaults to Visible.
    /// </summary>
    public Visibility NotEmptyVisibility { get; set; } = Visibility.Visible;

    /// <summary>
    /// If true, treats string with only whitespace characters as empty. Defaults to true.
    /// </summary>
    public bool TreatWhitespaceAsEmpty { get; set; } = true;

    /// <summary>
    /// Inverts the output logic (e.g. Visible when empty, Collapsed when populated).
    /// </summary>
    public bool IsInverted { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty;

        if (value is null)
        {
            isEmpty = true;
        }
        else if (value is string str)
        {
            isEmpty = TreatWhitespaceAsEmpty ? string.IsNullOrWhiteSpace(str) : string.IsNullOrEmpty(str);
        }
        else
        {
            // Fallback for non-string objects: evaluate ToString()
            string? fallback = value.ToString();
            isEmpty = TreatWhitespaceAsEmpty ? string.IsNullOrWhiteSpace(fallback) : string.IsNullOrEmpty(fallback);
        }

        if (IsInverted)
        {
            isEmpty = !isEmpty;
        }

        return isEmpty ? NullOrEmptyVisibility : NotEmptyVisibility;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way binding converter for UI layout presentation
        return DependencyProperty.UnsetValue;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}