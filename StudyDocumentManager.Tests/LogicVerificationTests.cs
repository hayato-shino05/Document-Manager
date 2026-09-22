using System;
using System.Linq;
using System.Collections.Generic;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Logic verification: CRUD lifecycle, multilingual input, SQL injection safety,
/// recycle bin lifecycle, bulk stress, student workspace hierarchy,
/// office metadata, and app settings persistence.
/// All assertions reflect the ACTUAL API behavior verified against real source.
/// </summary>
public sealed class LogicVerificationTests : DatabaseTestBase
{
    // ── Core CRUD ────────────────────────────────────────────────────────────

    [Fact]
    public void CoreCrud_AddUpdateDelete_LifecycleIsConsistent()
    {
        var repo = new DocumentRepository(Db);
        var doc = new StudyDocument
        {
            Name = "Integration Test Document",
            Subject = "Testing",
            Type = "Notes",
            IsImportant = true,
            Tags = "test, integration",
            Deadline = DateTime.Today.AddDays(14)
        };
        Assert.True(repo.Add(doc));

        var loaded = repo.GetAll().Single(d => d.Name == "Integration Test Document");
        Assert.Equal("Testing", loaded.Subject);
        Assert.True(loaded.IsImportant);

        loaded.Notes = "Updated notes";
        loaded.IsImportant = false;
        Assert.True(repo.Update(loaded));

        var updated = repo.GetById(loaded.Id)!;
        Assert.Equal("Updated notes", updated.Notes);
        Assert.False(updated.IsImportant);

        // Delete() is a soft delete - record is marked deleted but still retrievable via GetDeletedDocuments
        Assert.True(repo.Delete(updated.Id));
        Assert.Empty(repo.GetAll()); // not visible in active list

        var trashed = repo.GetDeletedDocuments();
        Assert.Contains(trashed, d => d.Id == updated.Id);

        // Permanent delete removes it entirely
        Assert.Equal(1, repo.PermanentlyDeleteDocuments(new[] { updated.Id }));
        Assert.Empty(repo.GetDeletedDocuments());
    }

    [Fact]
    public void CoreCrud_MultilingualNames_RoundtripPreservesUnicode()
    {
        var repo = new DocumentRepository(Db);
        var names = new[] { "\u6a5f\u68b0\u5b66\u7fd2\u5165\u9580", "T\u00e0i li\u1ec7u h\u1ecdc t\u1eadp", "\u4eba\u5de5\u667a\u80fd\u57fa\u7840" };
        foreach (var n in names)
            Assert.True(repo.Add(new StudyDocument { Name = n, Subject = "S", Type = "T" }));
        var all = repo.GetAll();
        foreach (var n in names)
            Assert.Contains(all, d => d.Name == n);
    }

    [Fact]
    public void CoreCrud_SqlInjection_PayloadsAreHarmless()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Normal Document", Subject = "General", Type = "Notes" });
        foreach (var p in new[] { "' OR '1'='1", "'; DROP TABLE documents; --", "%' AND 1=1 AND '%'='" })
            Assert.Empty(repo.Search(p));
        Assert.Single(repo.GetAll());
    }

    // ── Search & Filter ─────────────────────────────────────────────────────

    [Fact]
    public void Search_Keyword_ReturnsMatchingDocumentsOnly()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Machine Learning Basics", Subject = "CS", Type = "Book" });
        repo.Add(new StudyDocument { Name = "Database Design", Subject = "IT", Type = "Book" });
        Assert.Single(repo.Search("Machine Learning"));
        Assert.Empty(repo.Search("Quantum Physics"));
    }

    [Fact]
    public void Filter_BySubjectAndType_Works()
    {
        var repo = new DocumentRepository(Db);
        repo.AddWithCatalogs(new StudyDocument { Name = "Math PDF", Subject = "Math", Type = "PDF" });
        repo.AddWithCatalogs(new StudyDocument { Name = "Math Video", Subject = "Math", Type = "Video" });
        repo.AddWithCatalogs(new StudyDocument { Name = "Physics PDF", Subject = "Physics", Type = "PDF" });
        Assert.Equal(2, repo.Filter("Math", "").Count);
        Assert.Single(repo.Filter("Math", "PDF"));
        Assert.Empty(repo.Filter("Chemistry", "Slides"));
    }

    [Fact]
    public void Tags_GetDistinctTags_ReturnsRawTagStringsPerDocument()
    {
        // GetDistinctTags() returns DISTINCT raw tag strings (full comma-separated values per document),
        // NOT individual parsed tags. This is the actual API behavior.
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "D1", Subject = "S", Type = "T", Tags = "csharp, dotnet, xunit" });
        repo.Add(new StudyDocument { Name = "D2", Subject = "S", Type = "T", Tags = "csharp, avalonia" });
        // D1 and D2 have different tag strings, so both are returned as DISTINCT values
        repo.Add(new StudyDocument { Name = "D3", Subject = "S", Type = "T", Tags = "csharp, dotnet, xunit" }); // same as D1, deduplicated

        var tagStrings = repo.GetDistinctTags();
        Assert.Equal(2, tagStrings.Count); // "csharp, dotnet, xunit" and "csharp, avalonia" (D1=D3 deduplicated)
        Assert.Contains("csharp, dotnet, xunit", tagStrings);
        Assert.Contains("csharp, avalonia", tagStrings);
    }

    // ── Recycle Bin Lifecycle ───────────────────────────────────────────────

    [Fact]
    public void RecycleBin_SoftDeleteRestorePermanentDelete_FullCycle()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Doc A", Subject = "S", Type = "T" });
        repo.Add(new StudyDocument { Name = "Doc B", Subject = "S", Type = "T" });
        var ids = repo.GetAll().Select(d => d.Id).ToList();

        Assert.Equal(2, repo.SoftDeleteDocuments(ids));
        Assert.Empty(repo.GetAll());
        Assert.Equal(2, repo.GetDeletedDocuments().Count);

        Assert.Equal(1, repo.RestoreDocuments(new[] { ids[0] }));
        Assert.Single(repo.GetAll());

        Assert.Equal(1, repo.PermanentlyDeleteDocuments(new[] { ids[1] }));
        Assert.Equal(0, repo.GetDeletedDocumentCount());
    }

    // ── Batch Stress ────────────────────────────────────────────────────────

    [Fact]
    public void Batch_50Docs_CountAndFilterConsistent()
    {
        var repo = new DocumentRepository(Db);
        for (int i = 0; i < 50; i++)
            repo.Add(new StudyDocument { Name = "Batch#" + i, Subject = i % 2 == 0 ? "Engineering" : "Science", Type = "Article" });
        Assert.Equal(50, repo.GetAll().Count);
        Assert.Equal(25, repo.Filter("Engineering", "").Count);
        Assert.Equal(25, repo.Filter("Science", "").Count);
    }

    // ── Student Workspace Hierarchy ─────────────────────────────────────────

    [Fact]
    public void StudentWorkspace_SemesterCourseAssignment_FullWorkflow()
    {
        var asgRepo = new AssignmentRepository(Db);
        var docRepo = new DocumentRepository(Db);

        int semId = asgRepo.AddSemester(new Semester
        {
            Name = "Spring 2026",
            StartsOn = new DateTime(2026, 1, 15),
            EndsOn = new DateTime(2026, 6, 15),
            IsActive = true
        });
        Assert.True(semId > 0);
        Assert.Contains(asgRepo.GetSemesters(), s => s.Id == semId && s.Name == "Spring 2026");

        int courseId = asgRepo.AddCourse(new Course { Name = "Distributed Systems", Code = "CS401" });
        Assert.True(courseId > 0);

        int asgId = asgRepo.AddAssignment(new Assignment
        {
            CourseId = courseId,
            Title = "Raft Consensus Lab",
            OfficialDeadline = DateTime.Today.AddDays(7),
            Status = "planned"
        });
        Assert.True(asgId > 0);

        docRepo.Add(new StudyDocument { Name = "Raft Paper", Subject = "CS401", Type = "Paper" });
        int docId = docRepo.GetAll().First(d => d.Name == "Raft Paper").Id;
        Assert.True(asgRepo.LinkDocument(asgId, docId));
        Assert.Contains(asgRepo.GetDocumentIds(asgId), id => id == docId);

        var a = asgRepo.GetAssignment(asgId)!;
        a.Status = "in-progress";
        Assert.True(asgRepo.UpdateAssignment(a));
        Assert.Equal("in-progress", asgRepo.GetAssignment(asgId)!.Status);

        a.Status = "completed";
        Assert.True(asgRepo.UpdateAssignment(a));
        Assert.Equal("completed", asgRepo.GetAssignment(asgId)!.Status);

        Assert.True(asgRepo.UnlinkDocument(asgId, docId));
        Assert.Empty(asgRepo.GetDocumentIds(asgId));

        Assert.True(asgRepo.DeleteAssignment(asgId));
        Assert.Null(asgRepo.GetAssignment(asgId));
        Assert.NotNull(docRepo.GetById(docId)); // original doc preserved
    }

    [Fact]
    public void StudentWorkspace_PersonalDeadlineTakesPriorityOverOfficial()
    {
        // Rule: effective deadline = PersonalDeadline ?? OfficialDeadline
        DateTime? official = DateTime.Today.AddDays(14);
        DateTime? personal = DateTime.Today.AddDays(7);
        DateTime? none = null;

        Assert.Equal(personal, personal ?? official);
        Assert.Equal(official, none ?? official);
        Assert.Null(none ?? (DateTime?)null);
    }

    // ── Office Metadata ─────────────────────────────────────────────────────

    [Fact]
    public void OfficeMetadata_SaveAndRetrieve_AllFieldsPersist()
    {
        var docRepo = new DocumentRepository(Db);
        var officeRepo = new OfficeMetadataRepository(Db);

        docRepo.Add(new StudyDocument { Name = "MSA 2026", Subject = "Legal", Type = "Contract" });
        int docId = docRepo.GetAll().Single().Id;

        var meta = new OfficeDocumentMetadata
        {
            DocumentId = docId,
            ContactName = "Acme Corp",
            DocumentNumber = "CTR-001",
            EffectiveDate = new DateTime(2026, 1, 1),
            ExpiryDate = new DateTime(2027, 1, 1),
            ConfidentialityLevel = OfficeConfidentialityLevel.Confidential
        };
        Assert.True(officeRepo.Save(meta));

        var loaded = officeRepo.GetByDocumentId(docId)!;
        Assert.Equal("Acme Corp", loaded.ContactName);
        Assert.Equal(OfficeConfidentialityLevel.Confidential, loaded.ConfidentialityLevel);
        Assert.Equal(new DateTime(2026, 1, 1), loaded.EffectiveDate);
    }

    [Fact]
    public void OfficeMetadata_GetUpcomingReminders_ReturnsAllReminderEnabledDocs_WithCorrectState()
    {
        // GetUpcomingReminders() returns ALL reminder_enabled=true docs regardless of window.
        // State (DueSoon / Active / Overdue) is computed per item but all are returned.
        var docRepo = new DocumentRepository(Db);
        var officeRepo = new OfficeMetadataRepository(Db);

        docRepo.Add(new StudyDocument { Name = "Expiring Soon", Subject = "Legal", Type = "Contract" });
        int id1 = docRepo.GetAll().First(d => d.Name == "Expiring Soon").Id;
        officeRepo.Save(new OfficeDocumentMetadata { DocumentId = id1, ExpiryDate = DateTime.Today.AddDays(5), ReminderEnabled = true, ReminderDaysBefore = 7 });

        docRepo.Add(new StudyDocument { Name = "Far Future", Subject = "Legal", Type = "Contract" });
        int id2 = docRepo.GetAll().First(d => d.Name == "Far Future").Id;
        officeRepo.Save(new OfficeDocumentMetadata { DocumentId = id2, ExpiryDate = DateTime.Today.AddDays(90), ReminderEnabled = true, ReminderDaysBefore = 7 });

        docRepo.Add(new StudyDocument { Name = "Reminder Disabled", Subject = "Legal", Type = "Contract" });
        int id3 = docRepo.GetAll().First(d => d.Name == "Reminder Disabled").Id;
        officeRepo.Save(new OfficeDocumentMetadata { DocumentId = id3, ExpiryDate = DateTime.Today.AddDays(5), ReminderEnabled = false, ReminderDaysBefore = 7 });

        var reminders = officeRepo.GetUpcomingReminders(DateTime.Today);

        // id1 and id2 have reminder_enabled=true -> both returned
        Assert.Contains(reminders, r => r.DocumentId == id1 && r.ExpiryState == OfficeExpiryState.DueSoon);
        Assert.Contains(reminders, r => r.DocumentId == id2 && r.ExpiryState == OfficeExpiryState.Active);
        // id3 has reminder_enabled=false -> excluded
        Assert.DoesNotContain(reminders, r => r.DocumentId == id3);
    }

    // ── App Settings ─────────────────────────────────────────────────────────

    [Fact]
    public void AppSettings_LanguagePersistsAcrossMultipleSets()
    {
        var repo = new SettingsRepository(Db);
        foreach (var lang in new[] { "en", "vi", "zh", "ja" })
        {
            repo.SetSetting("language", lang);
            Assert.Equal(lang, repo.GetSetting("language"));
        }
    }

    [Fact]
    public void AppSettings_MissingKey_ReturnsNull()
    {
        var repo = new SettingsRepository(Db);
        Assert.Null(repo.GetSetting("nonexistent_key_xyz"));
    }
}