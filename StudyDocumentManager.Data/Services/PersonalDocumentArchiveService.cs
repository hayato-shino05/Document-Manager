using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
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
            var exportKeys = snapshot.Documents.ToDictionary(item => item.Document.Id, item => _repository.EnsureStableExportKey(item.Document));
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
                return Task.FromResult(new ArchiveExportReport(false, manifest.Documents.Count, missingFiles, conflicts, validationErrors)
                {
                    Manifest = manifest
                });

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

            return Task.FromResult(new ArchiveExportReport(true, manifest.Documents.Count, missingFiles, conflicts, validationErrors)
            {
                Manifest = manifest
            });
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
    {
        var missingFiles = new List<ArchiveMissingFile>();
        var conflicts = new List<ArchiveConflict>();
        var errors = new List<ArchiveReportItem>();
        var staged = new List<StagedFile>();
        var createdFiles = new List<string>();
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "study-document-archive", Guid.NewGuid().ToString("N"));
        ArchiveTransactionOutcome outcome = ArchiveTransactionOutcome.NotStarted;

        try
        {
            if (string.IsNullOrWhiteSpace(sourceZip) || !File.Exists(sourceZip))
                return Task.FromResult(ImportFailure("invalid-source", "Source ZIP path is required and must exist."));
            if (options is null)
                return Task.FromResult(ImportFailure("invalid-options", "Import options are required."));

            using var archive = ZipFile.OpenRead(Path.GetFullPath(sourceZip));
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                var normalized = NormalizeArchivePath(entry.FullName);
                if (normalized is null)
                {
                    errors.Add(new ArchiveReportItem("invalid-archive-path", "Archive paths must be relative and cannot traverse parent directories.", null, entry.FullName));
                    continue;
                }
                if (!entries.TryAdd(normalized, entry))
                    errors.Add(new ArchiveReportItem("duplicate-archive-entry", "ZIP contains duplicate archive names.", null, normalized));
                if (normalized != "manifest.json")
                    totalBytes += entry.Length;
            }

            if (entries.Count > MaxImportEntries)
                errors.Add(new ArchiveReportItem("too-many-entries", "Archive contains too many entries."));
            if (totalBytes > MaxImportTotalBytes)
                errors.Add(new ArchiveReportItem("archive-too-large", "Archive exceeds the total import size limit."));
            if (!entries.TryGetValue("manifest.json", out var manifestEntry))
                errors.Add(new ArchiveReportItem("missing-manifest", "Archive manifest.json is required.", null, "manifest.json"));
            if (errors.Count > 0)
                return Task.FromResult(new ArchiveImportReport(false, 0, 0, missingFiles, conflicts, errors, ArchiveTransactionOutcome.RolledBack));

            using var manifestStream = manifestEntry!.Open();
            using var manifestMemory = new MemoryStream();
            manifestStream.CopyTo(manifestMemory);
            var manifestBytes = manifestMemory.ToArray();
            if (!HasUniqueJsonProperties(manifestBytes))
                errors.Add(new ArchiveReportItem("duplicate-json-key", "Manifest contains duplicate JSON properties."));

            DocumentArchiveManifest? manifest = null;
            try
            {
                manifest = JsonSerializer.Deserialize<DocumentArchiveManifest>(manifestBytes, ManifestJsonOptions);
            }
            catch (JsonException)
            {
                errors.Add(new ArchiveReportItem("malformed-manifest", "Manifest JSON is malformed."));
            }

            if (manifest is null)
                errors.Add(new ArchiveReportItem("missing-manifest-data", "Manifest content is required."));
            else
            {
                manifest = CanonicalizeManifest(manifest);
                errors.AddRange(manifest.Validate().ValidationErrors);
                ValidateArchiveShape(manifest, entries, errors);
            }

            if (errors.Count > 0 || manifest is null)
                return Task.FromResult(new ArchiveImportReport(false, 0, 0, missingFiles, conflicts, errors, ArchiveTransactionOutcome.RolledBack));

            var filesByKey = manifest.Files.ToDictionary(file => file.DocumentExportKey, StringComparer.Ordinal);
            Directory.CreateDirectory(stagingDirectory);
            long stagedBytes = 0;
            foreach (var file in manifest.Files)
            {
                if (file.IsMissing)
                {
                    missingFiles.Add(new ArchiveMissingFile(file.DocumentExportKey, file.ArchivePath));
                    continue;
                }

                var entry = entries[NormalizeArchivePath(file.ArchivePath)!];
                if (entry.Length > MaxImportEntryBytes)
                {
                    errors.Add(new ArchiveReportItem("entry-too-large", "Archive entry exceeds the import size limit.", file.DocumentExportKey, file.ArchivePath));
                    continue;
                }
                stagedBytes += entry.Length;
                if (stagedBytes > MaxImportTotalBytes)
                {
                    errors.Add(new ArchiveReportItem("archive-too-large", "Archive exceeds the total import size limit."));
                    break;
                }
                var stagedPath = Path.Combine(stagingDirectory, file.DocumentExportKey, Path.GetFileName(file.ArchivePath));
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                using (var input = entry.Open())
                using (var output = File.Create(stagedPath))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
                var bytes = File.ReadAllBytes(stagedPath);
                var checksum = manifest.Checksums.Single(item => NormalizeArchivePath(item.ArchivePath) == NormalizeArchivePath(file.ArchivePath));
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), checksum.Sha256, StringComparison.OrdinalIgnoreCase) || bytes.LongLength != entry.Length)
                    errors.Add(new ArchiveReportItem("checksum-mismatch", "Archive entry checksum or length does not match the manifest.", file.DocumentExportKey, file.ArchivePath));
                staged.Add(new StagedFile(file.ArchivePath, stagedPath));
            }

            if (errors.Count > 0)
                return Task.FromResult(new ArchiveImportReport(false, 0, 0, missingFiles, conflicts, errors, ArchiveTransactionOutcome.RolledBack));

            var existing = _repository.GetExistingDocuments();
            var importDocuments = new List<DocumentArchiveDocument>();
            foreach (var document in manifest.Documents)
            {
                var existingByKey = existing.FirstOrDefault(item => string.Equals(item.ExportKey?.Value, document.ExportKey, StringComparison.Ordinal));
                var normalizedPath = NormalizeFilePath(document.FilePath);
                var existingByPath = string.IsNullOrWhiteSpace(normalizedPath)
                    ? null
                    : existing.FirstOrDefault(item => string.Equals(NormalizeFilePath(item.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
                if (existingByKey is not null || existingByPath is not null)
                {
                    conflicts.Add(new ArchiveConflict(existingByKey is not null ? "stable-key-conflict" : "path-conflict", "An existing document has the same stable key or normalized file path.", document.ExportKey, document.FilePath));
                    continue;
                }
                importDocuments.Add(document);
            }

            if (options.ValidateOnly || conflicts.Count > 0)
                return Task.FromResult(new ArchiveImportReport(conflicts.Count == 0, 0, conflicts.Count, missingFiles, conflicts, errors, ArchiveTransactionOutcome.NotStarted));

            foreach (var document in importDocuments)
            {
                if (!filesByKey.TryGetValue(document.ExportKey, out var file) || file.IsMissing)
                    continue;
                var source = staged.Single(item => string.Equals(NormalizeArchivePath(item.ArchivePath), NormalizeArchivePath(file.ArchivePath), StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(document.FilePath))
                    continue;
                var destination = Path.GetFullPath(document.FilePath);
                if (File.Exists(destination))
                    throw new IOException("Import destination already exists.");
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.Copy(source.StagedPath, destination, overwrite: false);
                createdFiles.Add(destination);
            }

            _repository.ImportGraph(manifest, importDocuments);
            outcome = ArchiveTransactionOutcome.Committed;
            return Task.FromResult(new ArchiveImportReport(true, importDocuments.Count, 0, missingFiles, conflicts, errors, outcome));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException or SqliteException or InvalidOperationException)
        {
            errors.Add(new ArchiveReportItem("import-failed", "Archive import failed and all changes were rolled back."));
            outcome = ArchiveTransactionOutcome.RolledBack;
            return Task.FromResult(new ArchiveImportReport(false, 0, conflicts.Count, missingFiles, conflicts, errors, outcome));
        }
        finally
        {
            if (outcome != ArchiveTransactionOutcome.Committed)
            {
                foreach (var path in createdFiles)
                {
                    try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private const long MaxImportEntryBytes = 64L * 1024 * 1024;
    private const int MaxImportEntries = 5000;
    private const long MaxImportTotalBytes = 256L * 1024 * 1024;

    private static ArchiveImportReport ImportFailure(string code, string message)
        => new(false, 0, 0, [], [], [new ArchiveReportItem(code, message)], ArchiveTransactionOutcome.NotStarted);

    private static void ValidateArchiveShape(DocumentArchiveManifest manifest, IReadOnlyDictionary<string, ZipArchiveEntry> entries, ICollection<ArchiveReportItem> errors)
    {
        if (manifest.Documents is null || manifest.Files is null || manifest.Notes is null || manifest.Collections is null || manifest.Relations is null || manifest.Checksums is null)
        {
            errors.Add(new ArchiveReportItem("missing-manifest-entry", "Manifest arrays are required."));
            return;
        }
        if (entries.Count > MaxImportEntries)
            errors.Add(new ArchiveReportItem("too-many-entries", "Archive contains too many entries."));

        var archivePaths = manifest.Files.Select(file => NormalizeArchivePath(file.ArchivePath)).Where(path => path is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
        var checksumPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var checksum in manifest.Checksums)
        {
            var normalized = NormalizeArchivePath(checksum.ArchivePath);
            if (normalized is null || !archivePaths.Contains(normalized))
                errors.Add(new ArchiveReportItem("invalid-checksum-path", "Checksum references an unknown archive path.", null, checksum.ArchivePath));
            else if (!checksumPaths.Add(normalized))
                errors.Add(new ArchiveReportItem("duplicate-checksum-path", "Checksum path is duplicated.", null, checksum.ArchivePath));
        }
        foreach (var file in manifest.Files)
        {
            var normalized = NormalizeArchivePath(file.ArchivePath);
            if (normalized is null || (!file.IsMissing && !entries.ContainsKey(normalized)))
                errors.Add(new ArchiveReportItem("missing-archive-entry", "A required archive file entry is missing.", file.DocumentExportKey, file.ArchivePath));
            if (!file.IsMissing && !checksumPaths.Contains(normalized ?? string.Empty))
                errors.Add(new ArchiveReportItem("missing-checksum", "A required archive file checksum is missing.", file.DocumentExportKey, file.ArchivePath));
        }
    }

    private static string? NormalizeArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathFullyQualified(normalized) || normalized.Contains(':')) return null;
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or "..")) return null;
        return string.Join('/', segments);
    }

    private static string? NormalizeFilePath(string path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());

    private static bool HasUniqueJsonProperties(byte[] bytes)
    {
        var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
        var scopes = new Stack<HashSet<string>>();
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject) scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) scopes.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && !scopes.Peek().Add(reader.GetString()!)) return false;
            }
            return scopes.Count == 0;
        }
        catch (JsonException) { return true; }
    }

    private static DocumentArchiveManifest CanonicalizeManifest(DocumentArchiveManifest manifest)
    {
        var sourceDocuments = manifest.Documents ?? [];
        var sourceFiles = manifest.Files ?? [];
        var sourceNotes = manifest.Notes ?? [];
        var sourceCollections = manifest.Collections ?? [];
        var sourceRelations = manifest.Relations ?? [];
        var sourceChecksums = manifest.Checksums ?? [];
        var keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var documents = sourceDocuments.Select(document =>
        {
            var canonical = DocumentExportKey.TryParse(document.ExportKey, out var key) ? key.Value : document.ExportKey;
            keyMap[document.ExportKey] = canonical;
            return document with { ExportKey = canonical };
        }).ToArray();
        var files = sourceFiles.Select(file => file with
        {
            DocumentExportKey = keyMap.GetValueOrDefault(file.DocumentExportKey, file.DocumentExportKey),
            ArchivePath = NormalizeArchivePath(file.ArchivePath) ?? file.ArchivePath
        }).ToArray();
        var notes = sourceNotes.Select(note => note with { DocumentExportKey = keyMap.GetValueOrDefault(note.DocumentExportKey, note.DocumentExportKey) }).ToArray();
        var collections = sourceCollections.Select(collection => collection with { DocumentExportKeys = (collection.DocumentExportKeys ?? []).Select(key => keyMap.GetValueOrDefault(key, key)).ToArray() }).ToArray();
        var relations = sourceRelations.Select(relation => relation with
        {
            SourceDocumentExportKey = keyMap.GetValueOrDefault(relation.SourceDocumentExportKey, relation.SourceDocumentExportKey),
            TargetDocumentExportKey = keyMap.GetValueOrDefault(relation.TargetDocumentExportKey, relation.TargetDocumentExportKey)
        }).ToArray();
        var checksums = sourceChecksums.Select(checksum => checksum with { ArchivePath = NormalizeArchivePath(checksum.ArchivePath) ?? checksum.ArchivePath }).ToArray();
        return manifest with { Documents = documents, Files = files, Notes = notes, Collections = collections, Relations = relations, Checksums = checksums };
    }

    private static DocumentArchiveDocument ToManifestDocument(PersonalDocumentArchiveRepository.ArchiveDocumentSource source, string exportKey)
    {
        var document = source.Document;
        return new DocumentArchiveDocument(exportKey, document.Id, document.Name, document.Subject, document.Type,
            document.Notes, document.FilePath, document.CreatedAt, document.FileSize, document.Author,
            document.IsImportant, document.Tags, document.Deadline, document.Status, source.IsDeleted);
    }

    private static string GetStableExportKey(StudyDocument document)
        => document.ExportKey?.Value ?? DocumentExportKey.Create().Value;

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
