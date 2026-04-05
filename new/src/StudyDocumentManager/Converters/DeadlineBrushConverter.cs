using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StudyDocumentManager.Converters;

/// <summary>
/// Converts a DateTime? deadline to a colored brush:
/// - Red (#DC2626) if overdue (past due date)
/// - Orange (#F59E0B) if due within 3 days
/// - Yellow (#EAB308) if due within 7 days
/// - Transparent otherwise
/// Matches legacy WinForms Dashboard behavior.
/// </summary>
public class DeadlineBrushConverter : IValueConverter
{
    public static readonly DeadlineBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime deadline)
            return Brushes.Transparent;

        var daysLeft = (deadline.Date - DateTime.Today).TotalDays;

        if (daysLeft < 0)
            return new SolidColorBrush(Color.Parse("#DC2626")); // Red - overdue
        if (daysLeft < 3)
            return new SolidColorBrush(Color.Parse("#F59E0B")); // Orange - urgent
        if (daysLeft < 7)
            return new SolidColorBrush(Color.Parse("#EAB308")); // Yellow - upcoming

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a DateTime? deadline to foreground text color:
/// - White text for overdue/urgent (dark backgrounds)
/// - Original color otherwise
/// </summary>
public class DeadlineTextConverter : IValueConverter
{
    public static readonly DeadlineTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime deadline)
            return null; // use default

        var daysLeft = (deadline.Date - DateTime.Today).TotalDays;

        if (daysLeft < 0)
            return new SolidColorBrush(Color.Parse("#FECACA")); // Light red text
        if (daysLeft < 3)
            return new SolidColorBrush(Color.Parse("#FEF3C7")); // Light amber text
        if (daysLeft < 7)
            return new SolidColorBrush(Color.Parse("#FEF9C3")); // Light yellow text

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
