using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Simurgh.Dashboard.Patient.Options;

namespace Simurgh.Dashboard.Patient.Models;

/// <summary>
/// Holds UI-specific configuration state including brushes and user-configured field visibility preferences.
/// </summary>
public class PatientUiEntity : INotifyPropertyChanged
{
    private static readonly Brush DefaultPrimaryBrush = CreateFrozenBrush(Color.FromRgb(33, 150, 243));
    private static readonly Brush DefaultSecondaryBrush = CreateFrozenBrush(Color.FromRgb(158, 158, 158));

    private Brush _primaryBrush = DefaultPrimaryBrush;
    private Brush _secondaryBrush = DefaultSecondaryBrush;

    private bool _isPatientIdVisible = true;
    private bool _isFullNameVisible = true;
    private bool _isDateOfBirthVisible = true;
    private bool _isAgeVisible = true;
    private bool _isSexVisible = true;
    private bool _isProcedureVisible = true;
    private bool _isPhysicianVisible = true;
    private bool _isAccessionNumberVisible = true;
    private bool _isSpecialNeedsVisible = true;
    private bool _isMedicalAlertVisible = true;
    private bool _isPatientCommentVisible = true;
    private bool _isContrastAllergiesVisible = true;

    /// <summary>
    /// Gets or sets the primary brush.
    /// </summary>
    public Brush PrimaryBrush
    {
        get => _primaryBrush;
        set => SetProperty(ref _primaryBrush, FreezeOrFallback(value, DefaultPrimaryBrush));
    }

    /// <summary>
    /// Gets or sets the secondary brush.
    /// </summary>
    public Brush SecondaryBrush
    {
        get => _secondaryBrush;
        set => SetProperty(ref _secondaryBrush, FreezeOrFallback(value, DefaultSecondaryBrush));
    }

    /// <summary>
    /// Gets or sets whether the patient ID is visible.
    /// </summary>
    public bool IsPatientIdVisible
    {
        get => _isPatientIdVisible;
        set => SetProperty(ref _isPatientIdVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the full name is visible.
    /// </summary>
    public bool IsFullNameVisible
    {
        get => _isFullNameVisible;
        set => SetProperty(ref _isFullNameVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the date of birth is visible.
    /// </summary>
    public bool IsDateOfBirthVisible
    {
        get => _isDateOfBirthVisible;
        set => SetProperty(ref _isDateOfBirthVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the age is visible.
    /// </summary>
    public bool IsAgeVisible
    {
        get => _isAgeVisible;
        set => SetProperty(ref _isAgeVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the sex is visible.
    /// </summary>
    public bool IsSexVisible
    {
        get => _isSexVisible;
        set => SetProperty(ref _isSexVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the procedure is visible.
    /// </summary>
    public bool IsProcedureVisible
    {
        get => _isProcedureVisible;
        set => SetProperty(ref _isProcedureVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the physician is visible.
    /// </summary>
    public bool IsPhysicianVisible
    {
        get => _isPhysicianVisible;
        set => SetProperty(ref _isPhysicianVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the accession number is visible.
    /// </summary>
    public bool IsAccessionNumberVisible
    {
        get => _isAccessionNumberVisible;
        set => SetProperty(ref _isAccessionNumberVisible, value);
    }

    /// <summary>
    /// Gets or sets whether special needs are visible.
    /// </summary>
    public bool IsSpecialNeedsVisible
    {
        get => _isSpecialNeedsVisible;
        set => SetProperty(ref _isSpecialNeedsVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the medical alert is visible.
    /// </summary>
    public bool IsMedicalAlertVisible
    {
        get => _isMedicalAlertVisible;
        set => SetProperty(ref _isMedicalAlertVisible, value);
    }

    /// <summary>
    /// Gets or sets whether the patient comment is visible.
    /// </summary>
    public bool IsPatientCommentVisible
    {
        get => _isPatientCommentVisible;
        set => SetProperty(ref _isPatientCommentVisible, value);
    }

    /// <summary>
    /// Gets or sets whether contrast allergies are visible.
    /// </summary>
    public bool IsContrastAllergiesVisible
    {
        get => _isContrastAllergiesVisible;
        set => SetProperty(ref _isContrastAllergiesVisible, value);
    }

    /// <summary>
    /// Applies configuration options to the UI entity.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public void ApplyConfiguration(PatientDemographicOptions? options)
    {
        if (options is null)
        {
            return;
        }

        PrimaryBrush = TryParseBrush(options.Brushes?.Primary, DefaultPrimaryBrush);
        SecondaryBrush = TryParseBrush(options.Brushes?.Secondary, DefaultSecondaryBrush);

        if (options.Visibility is not null)
        {
            IsPatientIdVisible = options.Visibility.PatientId;
            IsFullNameVisible = options.Visibility.FullName;
            IsDateOfBirthVisible = options.Visibility.DateOfBirth;
            IsAgeVisible = options.Visibility.Age;
            IsSexVisible = options.Visibility.Sex;
            IsProcedureVisible = options.Visibility.Procedure;
            IsPhysicianVisible = options.Visibility.Physician;
            IsSpecialNeedsVisible = options.Visibility.SpecialNeeds;
            IsMedicalAlertVisible = options.Visibility.MedicalAlert;
            IsPatientCommentVisible = options.Visibility.PatientComment;
            IsContrastAllergiesVisible = options.Visibility.ContrastAllergies;
        }
    }

    /// <summary>
    /// Attempts to parse a hex string into a SolidColorBrush. Returns fallback if parsing fails.
    /// </summary>
    /// <param name="value">The hex string to parse.</param>
    /// <param name="fallback">The fallback brush.</param>
    /// <returns>The parsed brush or the fallback brush.</returns>
    private static Brush TryParseBrush(string? value, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var converter = new BrushConverter();
            return converter.ConvertFromInvariantString(value) is Brush brush
                ? FreezeOrFallback(brush, fallback)
                : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Ensures brushes are frozen and safe across rendering and background worker threads.
    /// </summary>
    /// <param name="source">The source brush.</param>
    /// <param name="fallback">The fallback brush.</param>
    /// <returns>The frozen brush or the fallback brush.</returns>
    private static Brush FreezeOrFallback(Brush? source, Brush fallback)
    {
        if (source is null)
        {
            return fallback;
        }

        if (!source.CheckAccess())
        {
            return fallback;
        }

        if (source.IsFrozen)
        {
            return source;
        }

        var clone = source.CloneCurrentValue();
        if (!clone.CanFreeze)
        {
            return fallback;
        }

        clone.Freeze();
        return clone;
    }

    /// <summary>
    /// Creates a frozen SolidColorBrush from a Color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>A frozen SolidColorBrush.</returns>
    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    #region INotifyPropertyChanged

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the value of a property and raises the PropertyChanged event if the value changes.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="refField">The reference to the property field.</param>
    /// <param name="value">The new value of the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>True if the value changed; otherwise, false.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
