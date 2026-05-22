using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Markup;

/// <summary>
/// XAML markup extension for real-time localization.
/// Usage: {loc:Localize Menu_File}
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; }

    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Application.Current?.Resources.TryGetResource("Loc", null, out var res) != true
            || res is not LocalizationService loc)
        {
            return $"[{Key}]";
        }

        // IProvideValueTarget でターゲットを取得し、言語変更時に直接 SetValue で更新
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt
            && pvt.TargetObject is AvaloniaObject ao
            && pvt.TargetProperty is AvaloniaProperty ap)
        {
            void OnLanguageChanged(object? sender, EventArgs e)
            {
                if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    ao.SetValue(ap, (object)loc[Key]);
                else
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ao.SetValue(ap, (object)loc[Key]));
            }

            loc.LanguageChanged += OnLanguageChanged;

            // ビジュアルツリーから切り離されたら購読解除（メモリリーク防止）
            if (ao is Control ctrl)
            {
                ctrl.DetachedFromVisualTree += (_, _) => loc.LanguageChanged -= OnLanguageChanged;
            }
        }

        // 初期値を返す（string型なのでキャスト問題なし）
        return loc[Key];
    }
}
