using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace StudyDocumentManager.Models;

/// <summary>
/// カテゴリツリーの各ノードを表すUIモデル
/// Presentation層専用 — Avalonia UIバインディング用
/// </summary>
public class CategoryTreeItem
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public string IconKey { get; set; } = "IconCategory";
    public string FilterType { get; set; } = "";
    public string FilterValue { get; set; } = "";
    public bool IsIndented { get; set; }
    public bool IsHeader { get; set; }

    public string DisplayText => IsHeader ? Name : $"{Name} ({Count})";
    public Thickness IndentMargin => IsIndented ? new Thickness(16, 0, 0, 0) : new Thickness(0);

    /// <summary>
    /// ツリーノードに対応するアイコンを返す
    /// "type"フィルター → Assets内のPNG（DocumentTypeIconConverter経由）
    /// その他 → リソース辞書のDrawingImage
    /// </summary>
    public IImage? ResolvedIconSource
    {
        get
        {
            if (FilterType == "type")
            {
                return Converters.DocumentTypeIconConverter.Instance
                    .Convert(IconKey, typeof(IImage), null,
                             System.Globalization.CultureInfo.InvariantCulture)
                    as IImage;
            }

            var app = Application.Current;
            if (app == null) return null;

            if (app.Resources.TryGetResource(IconKey, ThemeVariant.Default, out var resource) &&
                resource is IImage img)
            {
                return img;
            }

            foreach (var style in app.Styles)
            {
                if (style is Styles styleGroup &&
                    styleGroup.Resources.TryGetResource(IconKey, ThemeVariant.Default, out var res) &&
                    res is IImage img2)
                {
                    return img2;
                }
            }
            return null;
        }
    }

    public IImage? IconSource => ResolvedIconSource;
}
