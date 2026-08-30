using System.IO.Compression;
using System.Text.Json;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Data.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class PersonalDocumentArchiveTests : DatabaseTestBase
{
    [Fact]
    public void Manifest_UsesSchemaVersionOne_AndStableExportKeys()
    {
        var manifest = CreateManifest();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.All(manifest.Documents, item => Assert.Matches("^[a-z0-9][a-z0-9-]{15,}$", item.ExportKey));
        Assert.DoesNotContain(manifest.Documents, item => item.ExportKey == item.DatabaseId.ToString());
    }

    [Fact]
    public void Validate_RejectsUnsupportedSchemaVersion()
    {
        var manifest = CreateManifest() with { SchemaVersion = 2 };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "unsupported-schema-version");
    }

    [Fact]
    public void Validate_RejectsMalformedStableKey()
    {
        var manifest = CreateManifest() with
        {
            Documents = [CreateDocument("not-a-stable-key")]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "malformed-export-key");
    }

    [Fact]
    public void Validate_RejectsDuplicateStableKey()
    {
        const string exportKey = "11111111111111111111111111111111";
        var manifest = CreateManifest() with
        {
            Documents = [CreateDocument(exportKey, 1), CreateDocument(exportKey, 2)]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "duplicate-export-key");
    }

    [Fact]
    public void Validate_RejectsDuplicateArchivePath()
    {
        const string exportKey = "11111111111111111111111111111111";
        var manifest = CreateManifest() with
        {
            Files =
            [
                new DocumentArchiveFile(exportKey, "files/document.pdf", "C:/source/document.pdf", false),
                new DocumentArchiveFile(exportKey, "files/document.pdf", "C:/source/other.pdf", false)
            ]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "duplicate-archive-path");
    }

    [Fact]
    public void Validate_RejectsTraversalAndAbsoluteArchivePaths()
    {
        const string exportKey = "11111111111111111111111111111111";
        var manifest = CreateManifest() with
        {
            Files =
            [
                new DocumentArchiveFile(exportKey, "../outside.txt", "C:/source/document.pdf", false),
                new DocumentArchiveFile(exportKey, "C:/outside.txt", "C:/source/other.pdf", false)
            ]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "invalid-archive-path");
    }

    [Fact]
    public void Validate_RejectsUnsupportedNoteType()
    {
        const string exportKey = "11111111111111111111111111111111";
        var manifest = CreateManifest() with
        {
            Notes = [new DocumentArchiveNote(exportKey, "unknown", "content", false, false)]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "unsupported-note-type");
    }

    [Fact]
    public void Validate_RejectsRelationWithUnknownEndpoint()
    {
        var manifest = CreateManifest() with
        {
            Relations =
            [
                new DocumentArchiveRelation(
                    "11111111111111111111111111111111",
                    "22222222222222222222222222222222",
                    "related")
            ]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "invalid-relation-endpoint");
    }

    [Fact]
    public void Validate_RejectsInvalidChecksumFormat()
    {
        var manifest = CreateManifest() with
        {
            Checksums = [new DocumentArchiveChecksum("files/document.pdf", "not-a-sha256")]
        };

        var report = manifest.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.ValidationErrors, item => item.Code == "invalid-checksum");
    }

    [Fact]
    public async Task Import_ValidArchive_RestoresDocumentAndFile()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var documentPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_file_{Guid.NewGuid():N}.txt");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            File.WriteAllText(documentPath, "archive-content");
            var sourceRepository = new DocumentRepository(sourceDb);
            var document = new StudyDocument { Name = "Archive document", FilePath = documentPath };
            Assert.True(sourceRepository.AddWithCatalogs(document));
            Assert.True((await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions())).Success);
            File.Delete(documentPath);

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions());

            Assert.True(report.Success, string.Join("; ", report.ValidationErrors.Select(error => error.Code + ":" + error.Message)));
            Assert.Equal(1, report.ImportedDocuments);
            Assert.True(File.Exists(documentPath));
            Assert.Equal("archive-content", File.ReadAllText(documentPath));
            Assert.Single(Repo.GetAll());
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(documentPath);
        }
    }

    [Fact]
    public async Task Import_ExistingStableKeyAndPath_ReportsConflictWithoutOverwrite()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var documentPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_file_{Guid.NewGuid():N}.txt");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            File.WriteAllText(documentPath, "original");
            var sourceRepository = new DocumentRepository(sourceDb);
            Assert.True(sourceRepository.AddWithCatalogs(new StudyDocument { Name = "Conflict", FilePath = documentPath }));
            Assert.True((await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions())).Success);
            File.Delete(documentPath);
            var service = CreateService(Db);
            Assert.True((await service.ImportAsync(archivePath, new ArchiveImportOptions())).Success);
            var before = File.ReadAllText(documentPath);

            var report = await service.ImportAsync(archivePath, new ArchiveImportOptions());

            Assert.False(report.Success);
            Assert.NotEmpty(report.Conflicts);
            Assert.All(report.Conflicts, conflict => Assert.DoesNotContain("overwrite", conflict.Message, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(before, File.ReadAllText(documentPath));
            Assert.Single(Repo.GetAll());
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(documentPath);
        }
    }

    [Fact]
    public async Task Import_TamperedChecksum_RollsBackDatabaseAndFilesystem()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var tamperedPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_tampered_{Guid.NewGuid():N}.zip");
        var documentPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_file_{Guid.NewGuid():N}.txt");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            File.WriteAllText(documentPath, "archive-content");
            var sourceRepository = new DocumentRepository(sourceDb);
            Assert.True(sourceRepository.AddWithCatalogs(new StudyDocument { Name = "Tampered", FilePath = documentPath }));
            Assert.True((await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions())).Success);
            File.Delete(documentPath);
            using (var input = ZipFile.OpenRead(archivePath))
            using (var output = ZipFile.Open(tamperedPath, ZipArchiveMode.Create))
            {
                foreach (var entry in input.Entries)
                {
                    var copy = output.CreateEntry(entry.FullName);
                    using var destination = copy.Open();
                    if (entry.FullName != "manifest.json")
                    {
                        destination.WriteByte(0x58);
                    }
                    else
                    {
                        using var source = entry.Open();
                        source.CopyTo(destination);
                    }
                }
            }

            var report = await CreateService(Db).ImportAsync(tamperedPath, new ArchiveImportOptions());

            Assert.False(report.Success);
            Assert.True(report.RolledBack);
            Assert.Empty(Repo.GetAll());
            Assert.False(File.Exists(documentPath));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(tamperedPath);
            TryDelete(documentPath);
        }
    }

    private static PersonalDocumentArchiveService CreateService(DatabaseHelper database)
    {
        var documents = new DocumentRepository(database);
        var repository = new PersonalDocumentArchiveRepository(
            documents,
            documents,
            new PersonalNoteRepository(database),
            new CollectionRepository(database),
            new RelatedDocumentRepository(database),
            documents,
            database);
        return new PersonalDocumentArchiveService(repository);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }

    private static DocumentArchiveManifest CreateManifest()
    {
        const string exportKey = "11111111111111111111111111111111";
        return new DocumentArchiveManifest(
            1,
            [CreateDocument(exportKey, 42)],
            [new DocumentArchiveFile(exportKey, "files/document.pdf", "C:/source/document.pdf", false)],
            [],
            [],
            [],
            [new DocumentArchiveChecksum("files/document.pdf", new string('a', 64))]);
    }

    private static DocumentArchiveDocument CreateDocument(string exportKey, int databaseId = 42)
        => new(exportKey, databaseId, "Document", "Subject", "Type", "", "", DateTime.UnixEpoch, null, "", false, "", null, "unread", false);
}
