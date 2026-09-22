using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class OfficeMetadataTests : DatabaseTestBase
{
    private readonly OfficeMetadataRepository _officeRepo;
    private readonly DocumentRepository _docRepo;
    private readonly LocalizationService _loc;

    public OfficeMetadataTests()
    {
        _officeRepo = new OfficeMetadataRepository(Db);
        _docRepo = new DocumentRepository(Db);
        _loc = new LocalizationService();
    }

    [Fact]
    public void Save_And_GetByDocumentId_PersistsAllFieldsCorrectly()
    {
        var doc = new StudyDocument { Name = "Contract Alpha" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Contract Alpha").Id;

        var effectiveDate = new DateTime(2026, 1, 15, 10, 0, 0);
        var expiryDate = new DateTime(2027, 1, 15, 18, 0, 0);

        var meta = new OfficeDocumentMetadata
        {
            DocumentId = docId,
            DocumentNumber = "CTR-2026-001",
            ContactName = "Taro Yamada",
            OrganizationOrProject = "Acme Corp",
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            ConfidentialityLevel = OfficeConfidentialityLevel.Confidential,
            ReminderEnabled = true,
            ReminderDaysBefore = 14
        };

        bool saved = _officeRepo.Save(meta);
        Assert.True(saved);

        var loaded = _officeRepo.GetByDocumentId(docId);
        Assert.NotNull(loaded);
        Assert.Equal(docId, loaded.DocumentId);
        Assert.Equal("CTR-2026-001", loaded.DocumentNumber);
        Assert.Equal("Taro Yamada", loaded.ContactName);
        Assert.Equal("Acme Corp", loaded.OrganizationOrProject);
        Assert.Equal(effectiveDate, loaded.EffectiveDate);
        Assert.Equal(expiryDate, loaded.ExpiryDate);
        Assert.Equal(OfficeConfidentialityLevel.Confidential, loaded.ConfidentialityLevel);
        Assert.True(loaded.ReminderEnabled);
        Assert.Equal(14, loaded.ReminderDaysBefore);
    }

    [Fact]
    public void Save_UpdateExistingMetadata_UpsertsCorrectly()
    {
        var doc = new StudyDocument { Name = "Invoice Beta" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Invoice Beta").Id;

        var meta = new OfficeDocumentMetadata
        {
            DocumentId = docId,
            DocumentNumber = "INV-001",
            ConfidentialityLevel = OfficeConfidentialityLevel.Internal
        };
        _officeRepo.Save(meta);

        // Update fields
        meta.DocumentNumber = "INV-001-REV";
        meta.ContactName = "Hanako Sato";
        meta.ConfidentialityLevel = OfficeConfidentialityLevel.Restricted;
        bool updated = _officeRepo.Save(meta);
        Assert.True(updated);

        var loaded = _officeRepo.GetByDocumentId(docId);
        Assert.NotNull(loaded);
        Assert.Equal("INV-001-REV", loaded.DocumentNumber);
        Assert.Equal("Hanako Sato", loaded.ContactName);
        Assert.Equal(OfficeConfidentialityLevel.Restricted, loaded.ConfidentialityLevel);
    }

    [Fact]
    public void DeleteByDocumentId_RemovesMetadata()
    {
        var doc = new StudyDocument { Name = "Receipt Gamma" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Receipt Gamma").Id;

        var meta = new OfficeDocumentMetadata
        {
            DocumentId = docId,
            DocumentNumber = "REC-999"
        };
        _officeRepo.Save(meta);

        bool deleted = _officeRepo.DeleteByDocumentId(docId);
        Assert.True(deleted);

        var loaded = _officeRepo.GetByDocumentId(docId);
        Assert.Null(loaded);
    }

    [Fact]
    public void GetUpcomingReminders_CalculatesOverdueDueSoonActiveCorrectly()
    {
        var today = new DateTime(2026, 9, 2);

        var docOverdue = new StudyDocument { Name = "Overdue Doc" };
        var docDueSoon = new StudyDocument { Name = "Due Soon Doc" };
        var docActive = new StudyDocument { Name = "Active Doc" };
        var docNoDate = new StudyDocument { Name = "No Date Doc" };

        _docRepo.Add(docOverdue);
        _docRepo.Add(docDueSoon);
        _docRepo.Add(docActive);
        _docRepo.Add(docNoDate);

        var all = _docRepo.GetAll();
        int idOverdue = all.Single(d => d.Name == "Overdue Doc").Id;
        int idDueSoon = all.Single(d => d.Name == "Due Soon Doc").Id;
        int idActive = all.Single(d => d.Name == "Active Doc").Id;
        int idNoDate = all.Single(d => d.Name == "No Date Doc").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = idOverdue,
            ExpiryDate = today.AddDays(-2),
            ReminderDaysBefore = 3
        });
        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = idDueSoon,
            ExpiryDate = today.AddDays(2),
            ReminderDaysBefore = 5
        });
        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = idActive,
            ExpiryDate = today.AddDays(30),
            ReminderDaysBefore = 5
        });
        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = idNoDate,
            ExpiryDate = null
        });

        var reminders = _officeRepo.GetUpcomingReminders(today);

        var itemOverdue = reminders.Single(r => r.DocumentId == idOverdue);
        Assert.Equal(OfficeExpiryState.Overdue, itemOverdue.ExpiryState);
        Assert.True(itemOverdue.DaysRemaining < 0);

        var itemDueSoon = reminders.Single(r => r.DocumentId == idDueSoon);
        Assert.Equal(OfficeExpiryState.DueSoon, itemDueSoon.ExpiryState);
        Assert.Equal(2, itemDueSoon.DaysRemaining);

        var itemActive = reminders.Single(r => r.DocumentId == idActive);
        Assert.Equal(OfficeExpiryState.Active, itemActive.ExpiryState);
        Assert.Equal(30, itemActive.DaysRemaining);

        var itemNoDate = reminders.Single(r => r.DocumentId == idNoDate);
        Assert.Equal(OfficeExpiryState.None, itemNoDate.ExpiryState);
    }

    [Fact]
    public void GetUpcomingReminders_HandlesTimezoneAndDateBoundariesPrecisely()
    {
        // 境界値テスト: 同日夜23:59:59の満了は当日扱い（DaysRemaining = 0、DueSoon）
        var today = new DateTime(2026, 9, 2, 8, 30, 0);
        var docSameDay = new StudyDocument { Name = "Same Day Expiry" };
        _docRepo.Add(docSameDay);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Same Day Expiry").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ExpiryDate = new DateTime(2026, 9, 2, 23, 59, 59),
            ReminderDaysBefore = 3
        });

        var reminders = _officeRepo.GetUpcomingReminders(today);
        var item = reminders.Single(r => r.DocumentId == docId);

        Assert.Equal(OfficeExpiryState.DueSoon, item.ExpiryState);
        Assert.Equal(0, item.DaysRemaining);
    }

    [Fact]
    public void OfficeWorkspaceModel_EmptyState_InitializesSafely()
    {
        var model = new OfficeWorkspaceModel(
            _officeRepo, _docRepo, new FakeProcessLauncher(), new FakeDialogService(), new FakeNavigationService(), _loc);

        Assert.Empty(model.FilteredRows);
        Assert.False(model.HasSelection);
        Assert.Equal(0, model.TotalCount);
        Assert.Equal(0, model.OverdueCount);
        Assert.Equal(0, model.DueSoonCount);
        Assert.Equal(0, model.ActiveCount);
        Assert.Equal(0, model.NoExpiryCount);
    }

    [Fact]
    public async Task OfficeWorkspaceModel_FilteringAndSaving_WorksEndToEnd()
    {
        var today = new DateTime(2026, 9, 2);

        var doc1 = new StudyDocument { Name = "NDA Tech Inc", Status = "in-progress" };
        var doc2 = new StudyDocument { Name = "Tax Report 2026", Status = "unread" };
        _docRepo.Add(doc1);
        _docRepo.Add(doc2);
        int id1 = _docRepo.GetAll().Single(d => d.Name == "NDA Tech Inc").Id;
        int id2 = _docRepo.GetAll().Single(d => d.Name == "Tax Report 2026").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = id1,
            DocumentNumber = "NDA-001",
            OrganizationOrProject = "Tech Inc",
            ConfidentialityLevel = OfficeConfidentialityLevel.Restricted,
            ExpiryDate = today.AddDays(1), // due soon
            ReminderDaysBefore = 3
        });

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = id2,
            DocumentNumber = "TAX-2026",
            OrganizationOrProject = "National Tax Agency",
            ConfidentialityLevel = OfficeConfidentialityLevel.Public,
            ExpiryDate = today.AddDays(-1) // overdue
        });

        var model = new OfficeWorkspaceModel(
            _officeRepo, _docRepo, new FakeProcessLauncher(), new FakeDialogService(), new FakeNavigationService(), _loc, today);

        Assert.Equal(2, model.TotalCount);
        Assert.Equal(1, model.OverdueCount);
        Assert.Equal(1, model.DueSoonCount);

        // Filter: due-soon
        model.SelectedExpiryFilter = model.ExpiryFilterOptions.Single(f => f.Key == "due-soon");
        Assert.Single(model.FilteredRows);
        Assert.Equal(id1, model.FilteredRows[0].DocumentId);

        // Filter: restricted confidentiality
        model.SelectedExpiryFilter = model.ExpiryFilterOptions.Single(f => f.Key == "all");
        model.SelectedConfidentialityFilter = model.ConfidentialityFilterOptions.Single(f => f.Key == OfficeConfidentialityLevel.Restricted);
        Assert.Single(model.FilteredRows);
        Assert.Equal(id1, model.FilteredRows[0].DocumentId);

        // Search text
        model.SelectedConfidentialityFilter = model.ConfidentialityFilterOptions.Single(f => f.Key == "all");
        model.SearchText = "National Tax";
        Assert.Single(model.FilteredRows);
        Assert.Equal(id2, model.FilteredRows[0].DocumentId);

        // Select and Edit Metadata
        model.SearchText = string.Empty;
        model.SelectedRow = model.FilteredRows.Single(r => r.DocumentId == id1);
        Assert.True(model.HasSelection);
        Assert.Equal("NDA-001", model.EditingDocumentNumber);

        model.EditingDocumentNumber = "NDA-001-APPROVED";
        model.EditingContactName = "John Doe";
        await model.SaveMetadataAsync();

        var reloaded = _officeRepo.GetByDocumentId(id1);
        Assert.NotNull(reloaded);
        Assert.Equal("NDA-001-APPROVED", reloaded.DocumentNumber);
        Assert.Equal("John Doe", reloaded.ContactName);
    }

    [Fact]
    public void GetUpcomingReminders_ExcludesDisabledReminders()
    {
        var today = new DateTime(2026, 9, 2);
        var doc = new StudyDocument { Name = "Disabled Reminder Doc" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Disabled Reminder Doc").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ExpiryDate = today.AddDays(2),
            ReminderEnabled = false,
            ReminderDaysBefore = 5
        });

        var reminders = _officeRepo.GetUpcomingReminders(today);
        Assert.DoesNotContain(reminders, r => r.DocumentId == docId);
    }

    [Fact]
    public void Save_InvalidConfidentialityLevel_CoercesToInternal()
    {
        var doc = new StudyDocument { Name = "Invalid Conf Doc" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Invalid Conf Doc").Id;

        var meta = new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ConfidentialityLevel = "invalid_level_xyz"
        };
        bool saved = _officeRepo.Save(meta);
        Assert.True(saved);

        var loaded = _officeRepo.GetByDocumentId(docId);
        Assert.NotNull(loaded);
        Assert.Equal(OfficeConfidentialityLevel.Internal, loaded.ConfidentialityLevel);
    }

    [Fact]
    public async Task OpenFileAsync_NonExistentFile_ShowsErrorDialog()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), $"sdm_non_existent_{Guid.NewGuid():N}.pdf");
        var doc = new StudyDocument { Name = "Missing File Doc", FilePath = fakePath };
        _docRepo.Add(doc);

        var dialog = new FakeDialogService();
        var model = new OfficeWorkspaceModel(
            _officeRepo, _docRepo, new FakeProcessLauncher(), dialog, new FakeNavigationService(), _loc);

        model.SelectedRow = model.FilteredRows.Single(r => r.Name == "Missing File Doc");
        await model.OpenFileAsync();

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Contains(fakePath, dialog.LastErrorMessage);
    }

    [Fact]
    public void OfficeWorkspaceModel_LanguageChanged_RelocalizesFilterOptionsAndRows()
    {
        var today = new DateTime(2026, 9, 2);
        var doc = new StudyDocument { Name = "I18n Doc", Status = DocumentStatus.Completed };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "I18n Doc").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ConfidentialityLevel = OfficeConfidentialityLevel.Confidential,
            ExpiryDate = today.AddDays(1),
            ReminderDaysBefore = 3
        });

        _loc.SetLanguage(SupportedLanguage.Japanese);
        var model = new OfficeWorkspaceModel(
            _officeRepo, _docRepo, new FakeProcessLauncher(), new FakeDialogService(), new FakeNavigationService(), _loc, today);

        var rowJp = model.FilteredRows.Single(r => r.DocumentId == docId);
        Assert.Equal(_loc["OW_Conf_Confidential"], rowJp.ConfidentialityLabel);
        Assert.Equal(_loc["DS_Kind_Completed"], rowJp.StatusLabel);

        _loc.SetLanguage(SupportedLanguage.English);

        var confOptionEn = model.ConfidentialityFilterOptions.Single(f => f.Key == OfficeConfidentialityLevel.Confidential);
        Assert.Equal("Confidential", confOptionEn.Label);

        var expiryOptionEn = model.ExpiryFilterOptions.Single(f => f.Key == "due-soon");
        Assert.Equal("Due Soon", expiryOptionEn.Label);

        var rowEn = model.FilteredRows.Single(r => r.DocumentId == docId);
        Assert.Equal("Confidential", rowEn.ConfidentialityLabel);
        Assert.Equal("Completed", rowEn.StatusLabel);

        _loc.SetLanguage(SupportedLanguage.Japanese);
    }

    [Fact]
    public void CanRestoreDatabase_WithOfficeMetadata_ValidatesRowData()
    {
        var doc = new StudyDocument { Name = "Office Backup Doc" };
        _docRepo.Add(doc);
        int docId = _docRepo.GetAll().Single(d => d.Name == "Office Backup Doc").Id;

        _officeRepo.Save(new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ConfidentialityLevel = OfficeConfidentialityLevel.Confidential,
            ReminderDaysBefore = 5,
            EffectiveDate = new DateTime(2026, 1, 1),
            ExpiryDate = new DateTime(2026, 12, 31)
        });

        var backupPath = Path.Combine(Path.GetTempPath(), $"office_backup_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            Assert.True(Db.CanRestoreDatabase(backupPath));

            using (var conn = new SqliteConnection($"Data Source={backupPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE office_document_metadata SET reminder_days_before = -1;";
                cmd.ExecuteNonQuery();
            }
            Assert.False(Db.CanRestoreDatabase(backupPath));

            using (var conn = new SqliteConnection($"Data Source={backupPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE office_document_metadata SET reminder_days_before = 3, confidentiality_level = 'bogus';";
                cmd.ExecuteNonQuery();
            }
            Assert.False(Db.CanRestoreDatabase(backupPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void Preflight_OfficeMetadata_ValidatesUniqueAndCascadeForeignKey()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sdm_preflight_office_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(dbPath));
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    DROP TABLE office_document_metadata;
                    CREATE TABLE office_document_metadata (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        document_id INTEGER NOT NULL,
                        document_number TEXT, contact_name TEXT, organization_or_project TEXT,
                        effective_date DATETIME, expiry_date DATETIME, confidentiality_level TEXT NOT NULL DEFAULT 'internal',
                        reminder_enabled INTEGER NOT NULL DEFAULT 1, reminder_days_before INTEGER NOT NULL DEFAULT 3,
                        created_at DATETIME, updated_at DATETIME,
                        FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
                    );
                    """;
                cmd.ExecuteNonQuery();
            }

            var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.RunMigrations($"Data Source={dbPath};Pooling=False"));
            Assert.Contains("Missing unique constraint in 'office_document_metadata'", ex.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncherService
    {
        public void OpenFile(string filePath) { }
        public void OpenFolder(string folderPath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? LastErrorTitle { get; private set; }
        public string? LastErrorMessage { get; private set; }

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message)
        {
            LastErrorTitle = title;
            LastErrorMessage = message;
            return Task.CompletedTask;
        }
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(defaultValue);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }
}
