using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Converters;

/// <summary>
/// <see cref="WatcherStatus"/> の値をローカライズ済みの表示用ラベルへ変換する。
/// WatchedFolder 画面では生の列挙名の代わりにこのコンバータをバインドし、
/// 未翻訳の列挙名が UI に表示されるのを防ぐ。
/// </summary>
public sealed class WatcherStatusLabelConverter : IValueConverter
{
    public static readonly WatcherStatusLabelConverter Instance = new();

    private static string KeyFor(WatcherStatus status) => status switch
    {
        WatcherStatus.Running => "WF_ItemStatus_Running",
        WatcherStatus.Disabled => "WF_ItemStatus_Disabled",
        WatcherStatus.Error => "WF_ItemStatus_Error",
        WatcherStatus.Stopped => "WF_ItemStatus_Stopped",
        _ => "WF_ItemStatus_Unknown"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is WatcherStatus status &&
            Application.Current?.Resources["Loc"] is ILocalizationService loc)
        {
            return loc[KeyFor(status)];
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
