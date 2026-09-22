using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using StudyDocumentManager.Converters;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// WatchedFolder 画面の状態ラベルが、生の列挙名ではなくローカライズされた
/// リソースから解決されることを確認する。
/// </summary>
public sealed class WatcherStatusLabelConverterTests
{
    [AvaloniaFact]
    public void Convert_ReturnsLocalizedLabel_NotRawEnum()
    {
        var localization = new LocalizationService();
        Application.Current!.Resources["Loc"] = localization;
        var converter = WatcherStatusLabelConverter.Instance;

        Assert.Equal(
            localization["WF_ItemStatus_Running"],
            converter.Convert(WatcherStatus.Running, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(
            localization["WF_ItemStatus_Error"],
            converter.Convert(WatcherStatus.Error, typeof(string), null, CultureInfo.InvariantCulture));
        // 生の列挙名（"Running" 等）がそのまま表示されることを防ぐ。
        Assert.NotEqual(
            nameof(WatcherStatus.Running),
            (string)converter.Convert(WatcherStatus.Running, typeof(string), null, CultureInfo.InvariantCulture)!);
    }
}
