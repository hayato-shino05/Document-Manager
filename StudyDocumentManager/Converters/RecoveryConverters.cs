using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Converters;

/// <summary>
/// Formats byte counts as a compact human readable size (B / KB / MB / GB).
/// </summary>
public sealed class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes < 0)
            return string.Empty;

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024d:0.0} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024):0.0} MB",
            _ => $"{bytes / (1024d * 1024 * 1024):0.00} GB"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

/// <summary>
/// Renders a backup validity flag as a localized status label.
/// </summary>
public sealed class BackupStatusConverter : IValueConverter
{
    public static readonly BackupStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isValid = value is true;
        var key = isValid ? "RC_StatusValid" : "RC_StatusInvalid";
        var loc = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
        return loc is null ? key : loc[key];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
