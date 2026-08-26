using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxBackupValidatorTests : DatabaseTestBase
{
    [Fact]
    public void CanRestoreDatabase_AcceptsCurrentBackupWithImportInbox()
    {
        var inbox = new ImportInboxRepository(Db);
        inbox.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\current.pdf",
            DisplayName = "current",
            State = ImportInboxState.Pending,
            Type = "PDF"
        });

        var backupPath = Path.Combine(Path.GetTempPath(), $"inb_current_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            Assert.True(Db.CanRestoreDatabase(backupPath));
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void CanRestoreDatabase_AcceptsLegacyBackupWithoutImportInbox()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"inb_legacy_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));

            // Simulate a legacy backup that predates the import_inbox table.
            using (var conn = new SqliteConnection($"Data Source={backupPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DROP TABLE import_inbox;";
                cmd.ExecuteNonQuery();
            }

            Assert.True(Db.CanRestoreDatabase(backupPath));
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }
}
