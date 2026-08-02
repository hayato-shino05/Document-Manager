using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Converters;

public sealed class DashboardFilterLabelConverter : IValueConverter
{
    public static readonly DashboardFilterLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string valueText && parameter is string key && valueText == key &&
            Application.Current?.Resources["Loc"] is ILocalizationService localization)
        {
            return localization[key];
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
