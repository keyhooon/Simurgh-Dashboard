using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SimurghDashboard.Patient.Options;

namespace SimurghDashboard.Patient.Models;

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



    #region Brushes

    public Brush PrimaryBrush
    {
        get => _primaryBrush;
        set => SetProperty(ref _primaryBrush, FreezeOrFallback(value, DefaultPrimaryBrush));
    }

    public Brush SecondaryBrush
    {
        get => _secondaryBrush;
        set => SetProperty(ref _secondaryBrush, FreezeOrFallback(value, DefaultSecondaryBrush));
    }

    #endregion

    #region Configured Visibility Toggles

    public bool IsPatientIdVisible
    {
        get => _isPatientIdVisible;
        set => SetProperty(ref _isPatientIdVisible, value);
    }

    public bool IsFullNameVisible
    {
        get => _isFullNameVisible;
        set => SetProperty(ref _isFullNameVisible, value);
    }

    public bool IsDateOfBirthVisible
    {
        get => _isDateOfBirthVisible;
        set => SetProperty(ref _isDateOfBirthVisible, value);
    }

    public bool IsAgeVisible
    {
        get => _isAgeVisible;
        set => SetProperty(ref _isAgeVisible, value);
    }

    public bool IsSexVisible
    {
        get => _isSexVisible;
        set => SetProperty(ref _isSexVisible, value);
    }

    public bool IsProcedureVisible
    {
        get => _isProcedureVisible;
        set => SetProperty(ref _isProcedureVisible, value);
    }

    public bool IsPhysicianVisible
    {
        get => _isPhysicianVisible;
        set => SetProperty(ref _isPhysicianVisible, value);
    }

    public bool IsAccessionNumberVisible
    {
        get => _isAccessionNumberVisible;
        set => SetProperty(ref _isAccessionNumberVisible, value);
    }

    public bool IsSpecialNeedsVisible
    {
        get => _isSpecialNeedsVisible;
        set => SetProperty(ref _isSpecialNeedsVisible, value);
    }
    public bool IsMedicalAlertVisible
    {
        get => _isMedicalAlertVisible;
        set => SetProperty(ref _isMedicalAlertVisible, value);
    }
    public bool IsPatientCommentVisible
    {
        get => _isPatientCommentVisible;
        set => SetProperty(ref _isPatientCommentVisible, value);
    }
    public bool IsContrastAllergiesVisible
    {
        get => _isContrastAllergiesVisible;
        set => SetProperty(ref _isContrastAllergiesVisible, value);
    }

    #endregion

    #region Configuration

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

    #endregion

    #region Brush Helpers

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

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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