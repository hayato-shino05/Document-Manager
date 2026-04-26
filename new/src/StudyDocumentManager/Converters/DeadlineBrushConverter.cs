using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StudyDocumentManager.Converters;

internal static class DeadlineBrushResources
{
    public static IBrush? GetBrush(string key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true)
            return value as IBrush;

        return null;
    }
}

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
    public const string DeadlineOverdueBrushKey = "DeadlineOverdueBrush";
    public const string DeadlineUrgentBrushKey = "DeadlineUrgentBrush";
    public const string DeadlineUpcomingBrushKey = "DeadlineUpcomingBrush";

    public static readonly DeadlineBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime deadline)
            return Brushes.Transparent;

        var daysLeft = (deadline.Date - DateTime.Today).TotalDays;

        if (daysLeft < 0)
            return DeadlineBrushResources.GetBrush(DeadlineOverdueBrushKey) ?? Brushes.Transparent;
        if (daysLeft < 3)
            return DeadlineBrushResources.GetBrush(DeadlineUrgentBrushKey) ?? Brushes.Transparent;
        if (daysLeft < 7)
            return DeadlineBrushResources.GetBrush(DeadlineUpcomingBrushKey) ?? Brushes.Transparent;

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
    public const string DeadlineOverdueTextBrushKey = "DeadlineOverdueTextBrush";
    public const string DeadlineUrgentTextBrushKey = "DeadlineUrgentTextBrush";
    public const string DeadlineUpcomingTextBrushKey = "DeadlineUpcomingTextBrush";

    public static readonly DeadlineTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime deadline)
            return null; // use default

        var daysLeft = (deadline.Date - DateTime.Today).TotalDays;

        if (daysLeft < 0)
            return DeadlineBrushResources.GetBrush(DeadlineOverdueTextBrushKey);
        if (daysLeft < 3)
            return DeadlineBrushResources.GetBrush(DeadlineUrgentTextBrushKey);
        if (daysLeft < 7)
            return DeadlineBrushResources.GetBrush(DeadlineUpcomingTextBrushKey);

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
