namespace StudyDocumentManager.Core.Entities;

public sealed record DocumentArchiveManifest(
    int SchemaVersion,
    IReadOnlyList<DocumentArchiveDocument> Documents,
    IReadOnlyList<DocumentArchiveFile> Files,
    IReadOnlyList<DocumentArchiveNote> Notes,
    IReadOnlyList<DocumentArchiveCollection> Collections,
    IReadOnlyList<DocumentArchiveRelation> Relations,
    IReadOnlyList<DocumentArchiveChecksum> Checksums)
{
    public const int CurrentSchemaVersion = 1;

    public ArchiveValidationReport Validate()
    {
        var errors = new List<ArchiveReportItem>();
        var documents = Documents ?? [];
        var files = Files ?? [];
        var notes = Notes ?? [];
        var collections = Collections ?? [];
        var relations = Relations ?? [];
        var checksums = Checksums ?? [];

        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add(new ArchiveReportItem("unsupported-schema-version", $"Schema version {SchemaVersion} is not supported."));

        var exportKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            if (!DocumentExportKey.TryParse(document.ExportKey, out _))
                errors.Add(new ArchiveReportItem("malformed-export-key", "Document export key is malformed.", document.ExportKey));
            else if (!exportKeys.Add(document.ExportKey))
                errors.Add(new ArchiveReportItem("duplicate-export-key", "Document export key is duplicated.", document.ExportKey));
        }

        var archivePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!exportKeys.Contains(file.DocumentExportKey))
                errors.Add(new ArchiveReportItem("invalid-file-document", "File references an unknown document.", file.DocumentExportKey, file.ArchivePath));
            if (!IsSafeArchivePath(file.ArchivePath))
                errors.Add(new ArchiveReportItem("invalid-archive-path", "Archive path must be relative and cannot traverse parent directories.", file.DocumentExportKey, file.ArchivePath));
            if (!archivePaths.Add(file.ArchivePath))
                errors.Add(new ArchiveReportItem("duplicate-archive-path", "Archive file path is duplicated.", file.DocumentExportKey, file.ArchivePath));
        }

        foreach (var note in notes)
        {
            if (!exportKeys.Contains(note.DocumentExportKey))
                errors.Add(new ArchiveReportItem("invalid-note-document", "Note references an unknown document.", note.DocumentExportKey));
            if (!NoteType.TryParse(note.NoteType, out _))
                errors.Add(new ArchiveReportItem("unsupported-note-type", "Note type is not supported.", note.DocumentExportKey));
        }

        foreach (var collection in collections)
        {
            foreach (var documentExportKey in collection.DocumentExportKeys ?? [])
            {
                if (!exportKeys.Contains(documentExportKey))
                    errors.Add(new ArchiveReportItem("invalid-collection-document", "Collection references an unknown document.", documentExportKey));
            }
        }

        foreach (var relation in relations)
        {
            if (!exportKeys.Contains(relation.SourceDocumentExportKey) || !exportKeys.Contains(relation.TargetDocumentExportKey))
                errors.Add(new ArchiveReportItem("invalid-relation-endpoint", "Relation references an unknown document."));
        }

        foreach (var checksum in checksums)
        {
            if (!archivePaths.Contains(checksum.ArchivePath))
                errors.Add(new ArchiveReportItem("invalid-checksum-path", "Checksum references an unknown archive path.", null, checksum.ArchivePath));
            if (!IsSha256(checksum.Sha256))
                errors.Add(new ArchiveReportItem("invalid-checksum", "Checksum must be a SHA-256 value.", null, checksum.ArchivePath));
        }

        return new ArchiveValidationReport(errors.Count == 0, errors);
    }

    private static bool IsSafeArchivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        return !normalized.StartsWith('/') && !Path.IsPathFullyQualified(normalized) && !normalized.Contains(':')
            && normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "." and not "..");
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed record DocumentArchiveDocument(
    string ExportKey,
    int DatabaseId,
    string Name,
    string Subject,
    string Type,
    string Notes,
    string FilePath,
    DateTime CreatedAt,
    double? FileSize,
    string Author,
    bool IsImportant,
    string Tags,
    DateTime? Deadline,
    string Status,
    bool IsDeleted,
    DateTime? DeletedAt = null);

public sealed record DocumentArchiveFile(
    string DocumentExportKey,
    string ArchivePath,
    string OriginalPath,
    bool IsMissing);

public sealed record DocumentArchiveNote(
    string DocumentExportKey,
    string NoteType,
    string Content,
    bool IsPinned,
    bool IsDeleted);

public sealed record DocumentArchiveCollection(
    string Name,
    IReadOnlyList<string> DocumentExportKeys);

public sealed record DocumentArchiveRelation(
    string SourceDocumentExportKey,
    string TargetDocumentExportKey,
    string RelationType);

public sealed record DocumentArchiveChecksum(
    string ArchivePath,
    string Sha256);
