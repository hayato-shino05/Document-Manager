using System;
using System.Globalization;
using Avalonia.Data.Converters;
using StudyDocumentManager.Core;

namespace StudyDocumentManager.Converters;

/// <summary>
/// Converts SupportedLanguage enum to native display name for the status bar ComboBox.
/// </summary>
public class LangDisplayConverter : IValueConverter
{
    public static readonly LangDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SupportedLanguage lang
            ? lang switch
            {
                SupportedLanguage.Japanese   => "日本語",
                SupportedLanguage.English    => "English",
                SupportedLanguage.Vietnamese => "Tiếng Việt",
                SupportedLanguage.Chinese    => "中文",
                _                            => lang.ToString()
            }
            : value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
