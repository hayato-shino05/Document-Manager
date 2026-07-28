using System.Text;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class CsvExportService : IExportService
{
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

        try
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            await writer.WriteLineAsync("ID,Name,Subject,Type,FilePath,Author,Tags,IsImportant,FileSize (MB),CreatedAt,Deadline,Notes");

            foreach (var doc in documents)
            {
                string line = string.Join(",",
                    doc.Id,
                    EscapeCsv(doc.Name),
                    EscapeCsv(doc.Subject),
                    EscapeCsv(doc.Type),
                    EscapeCsv(doc.FilePath),
                    EscapeCsv(doc.Author),
                    EscapeCsv(doc.Tags),
                    doc.IsImportant ? _loc["Dashboard_CsvYes"] : _loc["Dashboard_CsvNo"],
                    doc.FileSize?.ToString("F2") ?? "",
                    doc.CreatedAt.ToString("dd/MM/yyyy"),
                    doc.Deadline?.ToString("dd/MM/yyyy") ?? "",
                    EscapeCsv(doc.Notes)
                );
                await writer.WriteLineAsync(line);
            }

            return new ExportResult(true, path, documents.Count);
        }
        catch (Exception ex)
        {
            return new ExportResult(false, Error: ex.Message);
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
