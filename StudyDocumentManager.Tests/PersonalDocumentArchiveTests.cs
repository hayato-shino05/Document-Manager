using StudyDocumentManager.Core.Entities;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class PersonalDocumentArchiveTests
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
