using System.Text;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class CsvExportService : IExportService
{
    private const string Header = "ID,Name,Subject,Type,FilePath,Author,Tags,IsImportant,FileSize (MB),CreatedAt,Deadline,Notes";

    private readonly IFileDialogService _fileDialogService;
    private readonly ILocalizationService _loc;

    public CsvExportService(IFileDialogService fileDialogService, ILocalizationService localizationService)
    {
        _fileDialogService = fileDialogService;
        _loc = localizationService;
    }

    public async Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName)
    {
        var path = await _fileDialogService.ShowSaveFileAsync(
            _loc["Dashboard_ExportTitle"],
            suggestedFileName ?? "documents_export.csv",
            _loc["Dashboard_CsvFileFilter"]);

        if (string.IsNullOrWhiteSpace(path))
            return new ExportResult(false);

        string? stagingPath = null;
        try
        {
            var destinationPath = Path.GetFullPath(path);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
                return new ExportResult(false, Error: _loc["Dashboard_ExportWriteFailed"]);

            stagingPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await writer.WriteLineAsync(Header);
                foreach (var document in documents)
                    await writer.WriteLineAsync(BuildRow(document));
            }

            File.Move(stagingPath, destinationPath, overwrite: true);
            stagingPath = null;
            return new ExportResult(true, destinationPath, documents.Count);
        }
        catch
        {
            return new ExportResult(false, Error: _loc["Dashboard_ExportWriteFailed"]);
        }
        finally
        {
            if (stagingPath is not null && File.Exists(stagingPath))
                File.Delete(stagingPath);
        }
    }

    private string BuildRow(StudyDocument document)
        => string.Join(",",
            document.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EscapeCsv(document.Name),
            EscapeCsv(document.Subject),
            EscapeCsv(document.Type),
            EscapeCsv(document.FilePath),
            EscapeCsv(document.Author),
            EscapeCsv(document.Tags),
            document.IsImportant ? _loc["Dashboard_CsvYes"] : _loc["Dashboard_CsvNo"],
            document.FileSize?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            document.CreatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            document.Deadline?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            EscapeCsv(document.Notes));

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalizedValue = value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        return normalizedValue.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{normalizedValue.Replace("\"", "\"\"")}\""
            : normalizedValue;
    }
}
