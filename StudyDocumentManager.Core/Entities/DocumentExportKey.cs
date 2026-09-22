namespace StudyDocumentManager.Core.Entities;

public sealed record DocumentExportKey(string Value)
{
    public static DocumentExportKey Create() => new(Guid.NewGuid().ToString("N"));

    public static bool TryParse(string? value, out DocumentExportKey exportKey)
    {
        if (Guid.TryParseExact(value, "N", out var parsedValue))
        {
            exportKey = new DocumentExportKey(parsedValue.ToString("N"));
            return true;
        }

        exportKey = null!;
        return false;
    }

    public override string ToString() => Value;
}
