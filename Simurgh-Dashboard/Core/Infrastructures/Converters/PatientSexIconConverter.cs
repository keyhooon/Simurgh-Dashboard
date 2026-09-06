using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Core.Infrastructures.Converters;

/// <summary>
/// Converts a patient sex enum value to its corresponding vector <see cref="Geometry"/> resource (Mars/Venus/Diverse) defined in Icons.xaml.
/// Implements <see cref="MarkupExtension"/> to enable direct in-place XAML markup binding without boilerplate StaticResource declarations.
/// </summary>
[ValueConversion(typeof(BiologicalSex), typeof(Geometry))]
public sealed class PatientSexIconConverter : MarkupExtension, IValueConverter
{
    private static PatientSexIconConverter? _instance;

    /// <summary>
    /// Returns the singleton instance of the converter for XAML markup extension evaluation.
    /// </summary>
    /// <param name="serviceProvider">A service provider helper that can provide services for the markup extension.</param>
    /// <returns>The static instance of <see cref="PatientSexIconConverter"/>.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) => _instance ??= new PatientSexIconConverter();

    /// <summary>
    /// Evaluates the incoming patient sex and resolves the matching gender <see cref="Geometry"/> from application resources.
    /// </summary>
    /// <param name="value">The patient sex enum value passed by the data binding target.</param>
    /// <param name="targetType">The type of the binding target property (expected <see cref="Geometry"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>A resolved gender <see cref="Geometry"/> resource or the default fallback <c>IconGenderOther</c>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not BiologicalSex sex)
            return Application.Current.TryFindResource("IconGenderOther") as Geometry;

        var resourceKey = sex switch
        {
            BiologicalSex.Male => "IconGenderMale",
            BiologicalSex.Female => "IconGenderFemale",
            BiologicalSex.Other => "IconGenderOther",
            _ => "IconGenderOther" // Unknown/undefined state maps to diverse symbol
        };

        return Application.Current.TryFindResource(resourceKey) as Geometry
               ?? Application.Current.TryFindResource("IconGenderOther") as Geometry;
    }

    /// <summary>
    /// One-way conversion assertion; converting a vector Geometry back to a PatientSex is unsupported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}