using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;

namespace StudyDocumentManager.Data.Services;

public sealed class PersonalDocumentArchiveService : IPersonalDocumentArchiveService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly PersonalDocumentArchiveRepository _repository;

    public PersonalDocumentArchiveService(PersonalDocumentArchiveRepository repository)
        => _repository = repository;

    public Task<ArchiveExportReport> ExportAsync(string destinationZip, ArchiveExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(destinationZip))
            return Task.FromResult(Failure("invalid-destination", "Destination ZIP path is required."));
        if (options is null)
            return Task.FromResult(Failure("invalid-options", "Export options are required."));

        var conflicts = new List<ArchiveConflict>();
        var missingFiles = new List<ArchiveMissingFile>();
        var validationErrors = new List<ArchiveReportItem>();
        var stagedFiles = new List<StagedFile>();
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "study-document-archive", Guid.NewGuid().ToString("N"));

        try
        {
            var destination = Path.GetFullPath(destinationZip);
            if (File.Exists(destination))
            {
                conflicts.Add(new ArchiveConflict("destination-exists", "Destination ZIP already exists.", null, destination));
                return Task.FromResult(new ArchiveExportReport(false, 0, [], conflicts, []));
            }

            var snapshot = _repository.ReadSnapshot(options.DocumentIds, options.IncludeDeleted);
            var exportKeys = snapshot.Documents.ToDictionary(item => item.Document.Id, item => GetStableExportKey(item.Document.Id));
            var archiveFiles = new List<DocumentArchiveFile>();
            var checksums = new List<DocumentArchiveChecksum>();

            Directory.CreateDirectory(stagingDirectory);
            foreach (var source in snapshot.Documents)
            {
                var key = exportKeys[source.Document.Id];
                var originalPath = source.Document.FilePath ?? string.Empty;
                var fileName = SafeFileName(originalPath, $"document-{source.Document.Id}.bin");
                var archivePath = $"files/{key}/{fileName}";
                if (archiveFiles.Any(file => string.Equals(file.ArchivePath, archivePath, StringComparison.Ordinal)))
                {
                    conflicts.Add(new ArchiveConflict("archive-path-collision", "Two documents map to the same archive path.", key, archivePath));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
                {
                    archiveFiles.Add(new DocumentArchiveFile(key, archivePath, originalPath, true));
                    missingFiles.Add(new ArchiveMissingFile(key, archivePath));
                    continue;
                }

                var stagedPath = Path.Combine(stagingDirectory, key, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                File.Copy(originalPath, stagedPath, overwrite: false);
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(stagedPath))).ToLowerInvariant();
                archiveFiles.Add(new DocumentArchiveFile(key, archivePath, originalPath, false));
                checksums.Add(new DocumentArchiveChecksum(archivePath, hash));
                stagedFiles.Add(new StagedFile(archivePath, stagedPath));
            }

            var manifest = new DocumentArchiveManifest(
                DocumentArchiveManifest.CurrentSchemaVersion,
                snapshot.Documents.Select(source => ToManifestDocument(source, exportKeys[source.Document.Id])).ToArray(),
                archiveFiles.OrderBy(file => file.ArchivePath, StringComparer.Ordinal).ToArray(),
                snapshot.Notes
                    .Where(note => exportKeys.ContainsKey(note.Note.DocumentId))
                    .Select(note => new DocumentArchiveNote(exportKeys[note.Note.DocumentId], note.Note.NoteType, note.Note.Content, note.Note.IsPinned, note.Note.IsDeleted))
                    .OrderBy(note => note.DocumentExportKey, StringComparer.Ordinal)
                    .ThenBy(note => note.NoteType, StringComparer.Ordinal)
                    .ThenBy(note => note.Content, StringComparer.Ordinal)
                    .ToArray(),
                snapshot.Collections
                    .Select(collection => new DocumentArchiveCollection(collection.Name, collection.DocumentIds.Where(exportKeys.ContainsKey).Select(id => exportKeys[id]).OrderBy(key => key, StringComparer.Ordinal).ToArray()))
                    .ToArray(),
                snapshot.Relations
                    .Where(relation => exportKeys.ContainsKey(relation.SourceId) && exportKeys.ContainsKey(relation.TargetId))
                    .Select(relation => new DocumentArchiveRelation(exportKeys[relation.SourceId], exportKeys[relation.TargetId], relation.RelationType))
                    .OrderBy(relation => relation.SourceDocumentExportKey, StringComparer.Ordinal)
                    .ThenBy(relation => relation.TargetDocumentExportKey, StringComparer.Ordinal)
                    .ThenBy(relation => relation.RelationType, StringComparer.Ordinal)
                    .ToArray(),
                checksums.OrderBy(checksum => checksum.ArchivePath, StringComparer.Ordinal).ToArray());

            var validation = manifest.Validate();
            validationErrors.AddRange(validation.ValidationErrors);
            if (!validation.IsValid || conflicts.Count > 0)
                return Task.FromResult(new ArchiveExportReport(false, manifest.Documents.Count, missingFiles, conflicts, validationErrors));

            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var archive = ZipFile.Open(destination, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var stream = manifestEntry.Open())
                    stream.Write(manifestBytes);

                foreach (var staged in stagedFiles.OrderBy(file => file.ArchivePath, StringComparer.Ordinal))
                {
                    var entry = archive.CreateEntry(staged.ArchivePath, CompressionLevel.Optimal);
                    using var input = File.OpenRead(staged.StagedPath);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }
            }

            return Task.FromResult(new ArchiveExportReport(true, manifest.Documents.Count, missingFiles, conflicts, validationErrors));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            validationErrors.Add(new ArchiveReportItem("export-failed", "The archive could not be created."));
            return Task.FromResult(new ArchiveExportReport(false, 0, missingFiles, conflicts, validationErrors));
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    public Task<ArchiveImportReport> ImportAsync(string sourceZip, ArchiveImportOptions options)
        => Task.FromResult(new ArchiveImportReport(false, 0, 0, [], [],
            [new ArchiveReportItem("import-not-implemented", "Archive import is not implemented yet.")],
            ArchiveTransactionOutcome.NotStarted));

    private static DocumentArchiveDocument ToManifestDocument(PersonalDocumentArchiveRepository.ArchiveDocumentSource source, string exportKey)
    {
        var document = source.Document;
        return new DocumentArchiveDocument(exportKey, document.Id, document.Name, document.Subject, document.Type,
            document.Notes, document.FilePath, document.CreatedAt, document.FileSize, document.Author,
            document.IsImportant, document.Tags, document.Deadline, document.Status, source.IsDeleted);
    }

    private static string GetStableExportKey(int id)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"study-document:{id}"));
        return new Guid(hash.AsSpan(0, 16)).ToString("N");
    }

    private static string SafeFileName(string path, string fallback)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) || safe is "." or ".." ? fallback : safe;
    }

    private static ArchiveExportReport Failure(string code, string message)
        => new(false, 0, [], [], [new ArchiveReportItem(code, message)]);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record StagedFile(string ArchivePath, string StagedPath);
}
