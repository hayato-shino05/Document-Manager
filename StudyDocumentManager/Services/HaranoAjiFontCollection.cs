using System;
using Avalonia.Media.Fonts;

namespace StudyDocumentManager.Services;

public sealed class HaranoAjiFontCollection : EmbeddedFontCollection
{
    private static readonly string AssemblyName = typeof(HaranoAjiFontCollection).Assembly.GetName().Name ?? "DocumentManager";

    public HaranoAjiFontCollection()
        : base(
            new Uri("fonts:HaranoAji", UriKind.Absolute),
            new Uri($"avares://{AssemblyName}/Assets/Fonts", UriKind.Absolute))
    {
    }
}
