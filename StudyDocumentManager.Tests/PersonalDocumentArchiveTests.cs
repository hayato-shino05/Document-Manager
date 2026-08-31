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

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

            Assert.True(report.Success, string.Join("; ", report.ValidationErrors.Select(error => error.Code + ":" + error.Message)));
            Assert.Equal(1, report.ImportedDocuments);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(Repo.GetAll().Single().FilePath), StringComparison.OrdinalIgnoreCase);
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
            Assert.True((await service.ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()))).Success);
            var before = File.ReadAllText(documentPath);

            var report = await service.ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

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

            var report = await CreateService(Db).ImportAsync(tamperedPath, new ArchiveImportOptions(Path.GetTempPath()));

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

    [Fact]
    public async Task Export_Import_RoundTrip_PreservesDocumentsFilesAndManifest()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var targetDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_target_{Guid.NewGuid():N}.db");
        var filePaths = new[]
        {
            Path.Combine(Path.GetTempPath(), $"sdm_archive_file1_{Guid.NewGuid():N}.txt"),
            Path.Combine(Path.GetTempPath(), $"sdm_archive_file2_{Guid.NewGuid():N}.txt")
        };
        var originalContents = new[] { "round-trip-content-1", "round-trip-content-2" };
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            var sourceRepo = new DocumentRepository(sourceDb);
            File.WriteAllText(filePaths[0], originalContents[0]);
            File.WriteAllText(filePaths[1], originalContents[1]);
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "Doc1", FilePath = filePaths[0] }));
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "Doc2", FilePath = filePaths[1] }));

            var exportReport = await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions());
            Assert.True(exportReport.Success, string.Join("; ", exportReport.ValidationErrors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(2, exportReport.ExportedDocuments);
            Assert.NotNull(exportReport.Manifest);
            Assert.Equal(2, exportReport.Manifest!.Documents.Count);
            Assert.Equal(2, exportReport.Manifest.Files.Count);
            Assert.Equal(2, exportReport.Manifest.Checksums.Count);
            File.Delete(filePaths[0]);
            File.Delete(filePaths[1]);

            var targetDb = new DatabaseHelper();
            targetDb.SetDatabasePath(targetDbPath);
            targetDb.InitializeDatabase();
            var targetRepo = new DocumentRepository(targetDb);

            var importReport = await CreateService(targetDb).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));
            Assert.True(importReport.Success, string.Join("; ", importReport.ValidationErrors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(2, importReport.ImportedDocuments);
            Assert.Empty(importReport.Conflicts);
            Assert.False(importReport.RolledBack);

            var imported = targetRepo.GetAll();
            Assert.Equal(2, imported.Count);
            foreach (var doc in imported)
            {
                Assert.True(File.Exists(doc.FilePath), $"restored file missing: {doc.FilePath}");
            }
            var content1 = File.ReadAllText(filePaths[0]);
            var content2 = File.ReadAllText(filePaths[1]);
            Assert.Contains(originalContents[0], new[] { content1, content2 });
            Assert.Contains(originalContents[1], new[] { content1, content2 });

            foreach (var checksum in exportReport.Manifest.Checksums)
            {
                var matchingFile = exportReport.Manifest.Files.First(f => f.ArchivePath == checksum.ArchivePath);
                var keyValue = matchingFile.DocumentExportKey;
                var importedDoc = imported.Single(d => d.ExportKey?.Value == keyValue);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var actual = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(importedDoc.FilePath))).ToLowerInvariant();
                Assert.Equal(checksum.Sha256, actual);
            }

            targetDb.CloseAllConnections();
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(targetDbPath);
            TryDelete(filePaths[0]);
            TryDelete(filePaths[1]);
        }
    }


    [Fact]
    public async Task Export_Import_PreservesDeletedAt()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_deleted_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_deleted_{Guid.NewGuid():N}.zip");
        var documentPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_deleted_file_{Guid.NewGuid():N}.txt");
        var targetDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_deleted_target_{Guid.NewGuid():N}.db");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            File.WriteAllText(documentPath, "deleted-content");
            var sourceRepo = new DocumentRepository(sourceDb);
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "Deleted", FilePath = documentPath }));
            var sourceDocument = Assert.Single(sourceRepo.GetAll());
            Assert.True(sourceRepo.Delete(sourceDocument.Id));
            var deletedAt = Assert.Single(sourceRepo.GetDeletedDocuments()).DeletedAt;
            Assert.NotNull(deletedAt);
            var exportReport = await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions { IncludeDeleted = true });
            Assert.True(exportReport.Success);
            Assert.Equal(deletedAt, exportReport.Manifest!.Documents.Single().DeletedAt);
            File.Delete(documentPath);

            var targetDb = new DatabaseHelper();
            targetDb.SetDatabasePath(targetDbPath);
            targetDb.InitializeDatabase();
            var importReport = await CreateService(targetDb).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

            Assert.True(importReport.Success, string.Join("; ", importReport.ValidationErrors.Select(error => error.Code)));
            Assert.Equal(deletedAt, Assert.Single(new DocumentRepository(targetDb).GetDeletedDocuments()).DeletedAt);
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(targetDbPath);
            TryDelete(archivePath);
            TryDelete(documentPath);
        }
    }

    [Fact]
    public async Task Export_NullExportKey_PersistsStableKeyAcrossExports()
    {
        var documentPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_key_{Guid.NewGuid():N}.txt");
        var firstArchive = Path.Combine(Path.GetTempPath(), $"sdm_archive_key1_{Guid.NewGuid():N}.zip");
        var secondArchive = Path.Combine(Path.GetTempPath(), $"sdm_archive_key2_{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllText(documentPath, "stable-key");
            var document = new StudyDocument { Name = "Stable", FilePath = documentPath };
            Assert.True(Repo.AddWithCatalogs(document));
            var service = CreateService(Db);
            var first = await service.ExportAsync(firstArchive, new ArchiveExportOptions());
            var second = await service.ExportAsync(secondArchive, new ArchiveExportOptions());
            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.Equal(first.Manifest!.Documents.Single().ExportKey, second.Manifest!.Documents.Single().ExportKey);
            Assert.NotNull(Repo.GetById(document.Id)!.ExportKey);
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(documentPath);
            TryDelete(firstArchive);
            TryDelete(secondArchive);
        }
    }

    [Fact]
    public async Task Import_MixedConflicts_DoesNotPartiallyCommit()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var firstPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_first_{Guid.NewGuid():N}.txt");
        var secondPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_second_{Guid.NewGuid():N}.txt");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");
            var sourceRepo = new DocumentRepository(sourceDb);
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "First", FilePath = firstPath }));
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "Second", FilePath = secondPath }));
            var exportReport = await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions());
            Assert.True(exportReport.Success);
            File.Delete(firstPath);
            File.Delete(secondPath);
            var manifest = exportReport.Manifest!;
            var existingKey = DocumentExportKey.TryParse(manifest.Documents.Single(document => document.Name == "First").ExportKey, out var parsedKey)
                ? parsedKey
                : throw new InvalidOperationException("Expected archive key.");
            Assert.True(Repo.Add(new StudyDocument { Name = "Existing first", ExportKey = existingKey, FilePath = firstPath }));
            var service = CreateService(Db);

            var report = await service.ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

            Assert.False(report.Success);
            Assert.Single(report.Conflicts);
            Assert.Equal("stable-key-conflict", report.Conflicts[0].Code);
            Assert.Equal(ArchiveTransactionOutcome.NotStarted, report.TransactionOutcome);
            Assert.Single(Repo.GetAll());
            Assert.Equal("Existing first", Repo.GetAll()[0].Name);
            Assert.False(File.Exists(secondPath));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(firstPath);
            TryDelete(secondPath);
        }
    }

    [Fact]
    public async Task Import_UppercaseEquivalentKeys_ReportsCanonicalDuplicate()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_duplicate_{Guid.NewGuid():N}.zip");
        try
        {
            var manifest = CreateManifest();
            var first = manifest.Documents[0];
            var duplicate = first with { ExportKey = first.ExportKey.ToUpperInvariant(), DatabaseId = 43 };
            var duplicateManifest = manifest with { Documents = [first, duplicate] };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("manifest.json");
                using var stream = entry.Open();
                JsonSerializer.Serialize(stream, duplicateManifest);
            }
            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));
            Assert.False(report.Success);
            Assert.Contains(report.ValidationErrors, error => error.Code == "duplicate-export-key");
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(archivePath);
        }
    }

    [Fact]
    public async Task Import_FilesystemFailure_RemovesNewDestinationDirectories()
    {
        var sourceDbPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_source_{Guid.NewGuid():N}.db");
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var firstPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_new_{Guid.NewGuid():N}", "nested", "first.txt");
        var secondPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_existing_{Guid.NewGuid():N}.txt");
        try
        {
            var sourceDb = new DatabaseHelper();
            sourceDb.SetDatabasePath(sourceDbPath);
            sourceDb.InitializeDatabase();
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");
            var sourceRepo = new DocumentRepository(sourceDb);
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "First", FilePath = firstPath }));
            Assert.True(sourceRepo.AddWithCatalogs(new StudyDocument { Name = "Second", FilePath = secondPath }));
            Assert.True((await CreateService(sourceDb).ExportAsync(archivePath, new ArchiveExportOptions())).Success);
            File.Delete(firstPath);
            File.WriteAllText(secondPath, "existing");
            var newRoot = Path.GetDirectoryName(Path.GetDirectoryName(firstPath)!)!;
            Directory.Delete(Path.GetDirectoryName(firstPath)!, recursive: true);

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

            Assert.False(report.Success);
            Assert.True(report.RolledBack);
            Assert.Empty(Repo.GetAll());
            Assert.False(File.Exists(firstPath));
            Assert.False(Directory.Exists(newRoot));
            Assert.Equal("existing", File.ReadAllText(secondPath));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(sourceDbPath);
            TryDelete(archivePath);
            TryDelete(firstPath);
            TryDelete(secondPath);
            TryDeleteDirectory(Path.GetDirectoryName(firstPath)!);
        }
    }

    [Fact]
    public async Task Import_OversizedMalformedManifest_FailsBeforeMutation()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_{Guid.NewGuid():N}.zip");
        var destinationPath = Path.Combine(Path.GetTempPath(), $"sdm_archive_destination_{Guid.NewGuid():N}", "document.txt");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("manifest.json");
                using var stream = entry.Open();
                stream.Write(new byte[8 * 1024 * 1024 + 1]);
            }

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(Path.GetTempPath()));

            Assert.False(report.Success);
            Assert.True(report.RolledBack);
            Assert.Contains(report.ValidationErrors, error => error.Code == "manifest-too-large");
            Assert.Empty(Repo.GetAll());
            Assert.False(File.Exists(destinationPath));
            Assert.False(Directory.Exists(Path.GetDirectoryName(destinationPath)!));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(archivePath);
            TryDeleteDirectory(Path.GetDirectoryName(destinationPath)!);
        }
    }

    [Theory]
    [InlineData("C:/outside/document.pdf")]
    [InlineData("../outside/document.pdf")]
    [InlineData(".")]
    [InlineData("linked/")]
    public async Task Import_RejectsRootedOrTraversalDocumentPathBeforeMutation(string filePath)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_invalid_path_{Guid.NewGuid():N}.zip");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"sdm_archive_destination_{Guid.NewGuid():N}");
        try
        {
            var manifest = CreateManifest() with
            {
                Documents = [CreateDocument("11111111111111111111111111111111", 42) with { FilePath = filePath }]
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("manifest.json");
                using var stream = new StreamWriter(entry.Open());
                stream.Write(JsonSerializer.Serialize(manifest));
            }

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(destinationRoot));

            Assert.False(report.Success);
            Assert.Contains(report.ValidationErrors, error => error.Code == "invalid-destination-path");
            Assert.Empty(Repo.GetAll());
            Assert.False(Directory.Exists(destinationRoot));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(archivePath);
            TryDeleteDirectory(destinationRoot);
        }
    }

    [Fact]
    public async Task Import_RejectsExistingReparsePointBeforeMutation()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"sdm_archive_reparse_{Guid.NewGuid():N}.zip");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"sdm_archive_destination_{Guid.NewGuid():N}");
        var targetRoot = Path.Combine(Path.GetTempPath(), $"sdm_archive_target_{Guid.NewGuid():N}");
        var linkPath = Path.Combine(destinationRoot, "linked");
        try
        {
            Directory.CreateDirectory(destinationRoot);
            Directory.CreateDirectory(targetRoot);
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetRoot);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            var manifest = CreateManifest() with
            {
                Documents = [CreateDocument("11111111111111111111111111111111", 42) with { FilePath = "linked/document.pdf" }]
            };
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("manifest.json");
                using var stream = new StreamWriter(entry.Open());
                stream.Write(JsonSerializer.Serialize(manifest));
            }

            var report = await CreateService(Db).ImportAsync(archivePath, new ArchiveImportOptions(destinationRoot));

            Assert.False(report.Success);
            Assert.Contains(report.ValidationErrors, error => error.Code == "invalid-destination-path");
            Assert.Empty(Repo.GetAll());
            Assert.Empty(Directory.EnumerateFileSystemEntries(targetRoot));
        }
        finally
        {
            Db.CloseAllConnections();
            TryDelete(archivePath);
            TryDeleteDirectory(linkPath);
            TryDeleteDirectory(destinationRoot);
            TryDeleteDirectory(targetRoot);
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

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (IOException) { }
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
