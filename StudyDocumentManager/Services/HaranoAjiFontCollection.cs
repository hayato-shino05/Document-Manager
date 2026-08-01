using System;
using Avalonia.Media.Fonts;

namespace StudyDocumentManager.Services;

public sealed class HaranoAjiFontCollection : EmbeddedFontCollection
{
    public HaranoAjiFontCollection()
        : base(
            new Uri("fonts:HaranoAji", UriKind.Absolute),
            new Uri("avares://StudyDocumentManager/Assets/Fonts", UriKind.Absolute))
    {
    }
}
