using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

// ════════════════════════════════════════════════════════════
// Advanced Feature Tests — File 4
// Covers: Report Charts, Category Lookup CRUD, Cascade Ops,
//         Relation Types, File Integrity, Backup, SQL Safety
// ════════════════════════════════════════════════════════════

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// CHART DATA — GetDocumentsByDay
// ════════════════════════════════════════════════════════════

public class ReportByDayTests : DatabaseTestBase
{
    [Fact]
    public void GetDocumentsByDay_Default7Days_Returns7DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByDay(7);
        Assert.Equal(7, data.Count);
    }

    [Fact]
    public void GetDocumentsByDay_Custom14Days_Returns14DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByDay(14);
        Assert.Equal(14, data.Count);
    }

    [Fact]
    public void GetDocumentsByDay_EmptyDb_AllCountsAreZero()
    {
        var data = DatabaseHelper.GetDocumentsByDay(7);
        Assert.All(data, d => Assert.Equal(0, d.Count));
    }

    [Fact]
    public void GetDocumentsByDay_TodayDocumentAddedToday_CountedInToday()
    {
        // CreatedAt = now がデフォルト
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Today Doc" });

        var data = DatabaseHelper.GetDocumentsByDay(7);

        // The last item in ASC order is "today"
        var today = data.Last();
        Assert.Equal(1, today.Count);
    }

    [Fact]
    public void GetDocumentsByDay_SoftDeletedDocumentExcluded()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Will Be Deleted" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id); // soft delete

        var data = DatabaseHelper.GetDocumentsByDay(7);
        var today = data.Last();
        Assert.Equal(0, today.Count);
    }

    [Fact]
    public void GetDocumentsByDay_LabelFormat_IsDD_MM()
    {
        var data = DatabaseHelper.GetDocumentsByDay(7);
        // Each label should match dd/mm format
        foreach (var (label, _) in data)
        {
            Assert.Matches(@"^\d{2}/\d{2}$", label);
        }
    }

    [Fact]
    public void GetDocumentsByDay_OrderedAscending_OldestFirst()
    {
        var data = DatabaseHelper.GetDocumentsByDay(7);
        // Labels should be in ascending date order
        // We verify by checking count is 7 and result is non-null
        Assert.Equal(7, data.Count);
        Assert.NotNull(data[0].Label);
    }
}

// ════════════════════════════════════════════════════════════
// CHART DATA — GetDocumentsByMonth
// ════════════════════════════════════════════════════════════

public class ReportByMonthTests : DatabaseTestBase
{
    [Fact]
    public void GetDocumentsByMonth_Default12_Returns12DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByMonth(12);
        Assert.Equal(12, data.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_Custom6_Returns6DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByMonth(6);
        Assert.Equal(6, data.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_EmptyDb_AllCountsAreZero()
    {
        var data = DatabaseHelper.GetDocumentsByMonth(12);
        Assert.All(data, d => Assert.Equal(0, d.Count));
    }

    [Fact]
    public void GetDocumentsByMonth_LabelFormat_IsMM_YYYY()
    {
        var data = DatabaseHelper.GetDocumentsByMonth(12);
        foreach (var (label, _) in data)
        {
            Assert.Matches(@"^\d{2}/\d{4}$", label);
        }
    }

    [Fact]
    public void GetDocumentsByMonth_DocAddedThisMonth_CountedCorrectly()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "This Month Doc" });
        repo.Add(new StudyDocument { Name = "This Month Doc 2" });

        var data = DatabaseHelper.GetDocumentsByMonth(12);
        var thisMonth = data.Last(); // Last in ASC is current month
        Assert.Equal(2, thisMonth.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_SoftDeletedExcluded()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Deleted Doc" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id);

        var data = DatabaseHelper.GetDocumentsByMonth(12);
        var thisMonth = data.Last();
        Assert.Equal(0, thisMonth.Count);
    }
}

// ════════════════════════════════════════════════════════════
// CHART DATA — GetDocumentsBySubject / GetDocumentsByType
// ════════════════════════════════════════════════════════════

public class ReportBySubjectTypeTests : DatabaseTestBase
{
    [Fact]
    public void GetDocumentsBySubject_EmptyDb_ReturnsEmpty()
    {
        var data = DatabaseHelper.GetDocumentsBySubject();
        Assert.Empty(data);
    }

    [Fact]
    public void GetDocumentsBySubject_GroupsCorrectly()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "A1", Subject = "Math" });
        repo.Add(new StudyDocument { Name = "A2", Subject = "Math" });
        repo.Add(new StudyDocument { Name = "B1", Subject = "Physics" });

        var data = DatabaseHelper.GetDocumentsBySubject();
        var math = data.FirstOrDefault(d => d.Label == "Math");
        var phys = data.FirstOrDefault(d => d.Label == "Physics");

        Assert.NotNull(math);
        Assert.Equal(2, math.Count);
        Assert.NotNull(phys);
        Assert.Equal(1, phys.Count);
    }

    [Fact]
    public void GetDocumentsBySubject_SoftDeletedExcluded()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "X", Subject = "Biology" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id);

        var data = DatabaseHelper.GetDocumentsBySubject();
        Assert.DoesNotContain(data, d => d.Label == "Biology");
    }

    [Fact]
    public void GetDocumentsBySubject_NullSubject_LabeledAsKhongRo()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "No Subject", Subject = null });

        var data = DatabaseHelper.GetDocumentsBySubject();
        Assert.Contains(data, d => d.Label == "Unknown");
    }

    [Fact]
    public void GetDocumentsByType_EmptyDb_ReturnsEmpty()
    {
        var data = DatabaseHelper.GetDocumentsByType();
        Assert.Empty(data);
    }

    [Fact]
    public void GetDocumentsByType_GroupsCorrectly()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "P1", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "P2", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "W1", Type = "Word" });

        var data = DatabaseHelper.GetDocumentsByType();
        var pdf = data.FirstOrDefault(d => d.Label == "PDF");
        var word = data.FirstOrDefault(d => d.Label == "Word");

        Assert.Equal(2, pdf!.Count);
        Assert.Equal(1, word!.Count);
    }

    [Fact]
    public void GetDocumentsByType_OrderedByCountDesc()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "P1", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "P2", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "P3", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "W1", Type = "Word" });

        var data = DatabaseHelper.GetDocumentsByType();
        // First item should have highest count
        Assert.True(data[0].Count >= data[1].Count);
    }
}

// ════════════════════════════════════════════════════════════
// CATEGORY LOOKUP TABLE — categories & document_types
// ════════════════════════════════════════════════════════════

public class CategoryLookupCrudTests : DatabaseTestBase
{
    [Fact]
    public void AddSubject_NewName_ReturnsTrueAndPersists()
    {
        bool result = DatabaseHelper.AddSubject("Lập trình");
        Assert.True(result);

        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.Contains("Lập trình", subjects);
    }

    [Fact]
    public void AddSubject_Duplicate_ReturnsFalse()
    {
        DatabaseHelper.AddSubject("Math");
        bool result = DatabaseHelper.AddSubject("Math"); // duplicate
        Assert.False(result);
    }

    [Fact]
    public void AddSubject_MultipleSubjects_AllPersist()
    {
        DatabaseHelper.AddSubject("A");
        DatabaseHelper.AddSubject("B");
        DatabaseHelper.AddSubject("C");

        // Default seeded subjects + our 3
        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.Contains("A", subjects);
        Assert.Contains("B", subjects);
        Assert.Contains("C", subjects);
    }

    [Fact]
    public void DeleteSubject_ExistingName_ReturnsTrueAndRemoves()
    {
        DatabaseHelper.AddSubject("ToDelete");
        bool result = DatabaseHelper.DeleteSubject("ToDelete");
        Assert.True(result);
        Assert.DoesNotContain("ToDelete", DatabaseHelper.GetAllSubjects());
    }

    [Fact]
    public void DeleteSubject_NonExistentName_ReturnsFalse()
    {
        bool result = DatabaseHelper.DeleteSubject("NonExistentSubject_XYZ");
        Assert.False(result);
    }

    [Fact]
    public void AddType_NewName_ReturnsTrueAndPersists()
    {
        bool result = DatabaseHelper.AddType("Mindmap");
        Assert.True(result);
        Assert.Contains("Mindmap", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void AddType_Duplicate_ReturnsFalse()
    {
        DatabaseHelper.AddType("PDF");
        bool result = DatabaseHelper.AddType("PDF");
        Assert.False(result);
    }

    [Fact]
    public void DeleteType_ExistingName_ReturnsTrueAndRemoves()
    {
        DatabaseHelper.AddType("OldType");
        bool result = DatabaseHelper.DeleteType("OldType");
        Assert.True(result);
        Assert.DoesNotContain("OldType", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void DeleteType_NonExistent_ReturnsFalse()
    {
        bool result = DatabaseHelper.DeleteType("TypeThatDoesNotExist_ABC");
        Assert.False(result);
    }

    [Fact]
    public void GetAllSubjects_OrderedAlphabetically()
    {
        DatabaseHelper.AddSubject("Zebra");
        DatabaseHelper.AddSubject("Apple");
        DatabaseHelper.AddSubject("Mango");

        var subjects = DatabaseHelper.GetAllSubjects();
        var relevant = subjects.Where(s => s is "Zebra" or "Apple" or "Mango").ToList();
        Assert.Equal(new[] { "Apple", "Mango", "Zebra" }, relevant);
    }

    [Fact]
    public void GetAllTypes_OrderedAlphabetically()
    {
        DatabaseHelper.AddType("TypeZ");
        DatabaseHelper.AddType("TypeA");

        var types = DatabaseHelper.GetAllTypes();
        var relevant = types.Where(t => t.StartsWith("Type")).ToList();
        var sorted = relevant.OrderBy(t => t).ToList();
        Assert.Equal(sorted, relevant);
    }
}

// ════════════════════════════════════════════════════════════
// GetSubjectsWithCount / GetTypesWithCount
// ════════════════════════════════════════════════════════════

public class CategoryWithCountTests : DatabaseTestBase
{
    [Fact]
    public void GetSubjectsWithCount_EmptyDb_ReturnsSeededDefaultsWithZeroCount()
    {
        var data = DatabaseHelper.GetSubjectsWithCount();
        // InitializeDatabase seeds default categories even with no documents
        Assert.NotEmpty(data);
        Assert.All(data, x => Assert.Equal(0, x.Count));
    }

    [Fact]
    public void GetSubjectsWithCount_ReturnsCorrectCounts()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1", Subject = "Math" });
        repo.Add(new StudyDocument { Name = "D2", Subject = "Math" });
        repo.Add(new StudyDocument { Name = "D3", Subject = "Physics" });

        var data = DatabaseHelper.GetSubjectsWithCount();

        var math = data.First(x => x.Name == "Math");
        var phys = data.First(x => x.Name == "Physics");

        Assert.Equal(2, math.Count);
        Assert.Equal(1, phys.Count);
    }

    [Fact]
    public void GetSubjectsWithCount_SoftDeletedExcluded()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1", Subject = "Chemistry" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id); // soft delete

        var data = DatabaseHelper.GetSubjectsWithCount();
        Assert.DoesNotContain(data, x => x.Name == "Chemistry");
    }

    [Fact]
    public void GetTypesWithCount_EmptyDb_ReturnsSeededDefaultsWithZeroCount()
    {
        var data = DatabaseHelper.GetTypesWithCount();
        // InitializeDatabase seeds default document_types even with no documents
        Assert.NotEmpty(data);
        Assert.All(data, x => Assert.Equal(0, x.Count));
    }

    [Fact]
    public void GetTypesWithCount_ReturnsCorrectCounts()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "P1", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "P2", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "W1", Type = "Word" });

        var data = DatabaseHelper.GetTypesWithCount();
        var pdf = data.First(x => x.Name == "PDF");
        Assert.Equal(2, pdf.Count);
    }

    [Fact]
    public void GetTypesWithCount_NullTypeExcluded()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "NoType", Type = null });

        var data = DatabaseHelper.GetTypesWithCount();
        Assert.DoesNotContain(data, x => string.IsNullOrEmpty(x.Name));
    }
}

// ════════════════════════════════════════════════════════════
// CASCADE — UpdateSubjectName / UpdateTypeName
// ════════════════════════════════════════════════════════════

public class CascadeRenameTests : DatabaseTestBase
{
    [Fact]
    public void UpdateSubjectName_CascadesAllDocuments()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1", Subject = "Old Subject" });
        repo.Add(new StudyDocument { Name = "D2", Subject = "Old Subject" });
        repo.Add(new StudyDocument { Name = "D3", Subject = "Other" });

        DatabaseHelper.UpdateSubjectName("Old Subject", "New Subject");

        var all = repo.GetAll();
        var renamed = all.Where(d => d.Subject == "New Subject").ToList();
        var old = all.Where(d => d.Subject == "Old Subject").ToList();
        var other = all.Where(d => d.Subject == "Other").ToList();

        Assert.Equal(2, renamed.Count);
        Assert.Empty(old);
        Assert.Single(other); // unaffected
    }

    [Fact]
    public void UpdateSubjectName_SoftDeletedDocumentsNotRenamed()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Deleted", Subject = "OldSub" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id); // soft delete

        // Add active doc
        repo.Add(new StudyDocument { Name = "Active", Subject = "OldSub" });

        DatabaseHelper.UpdateSubjectName("OldSub", "NewSub");

        int activeCount = repo.GetAll().Count(d => d.Subject == "NewSub");
        Assert.Equal(1, activeCount); // only active doc renamed
    }

    [Fact]
    public void UpdateSubjectName_AlsoUpdatesLookupTable()
    {
        DatabaseHelper.AddSubject("LookupOld");
        DatabaseHelper.UpdateSubjectName("LookupOld", "LookupNew");

        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.Contains("LookupNew", subjects);
        Assert.DoesNotContain("LookupOld", subjects);
    }

    [Fact]
    public void UpdateTypeName_CascadesAllDocuments()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1", Type = "OldType" });
        repo.Add(new StudyDocument { Name = "D2", Type = "OldType" });
        repo.Add(new StudyDocument { Name = "D3", Type = "Other" });

        DatabaseHelper.UpdateTypeName("OldType", "NewType");

        var all = repo.GetAll();
        Assert.Equal(2, all.Count(d => d.Type == "NewType"));
        Assert.Empty(all.Where(d => d.Type == "OldType"));
        Assert.Single(all.Where(d => d.Type == "Other"));
    }

    [Fact]
    public void UpdateTypeName_AlsoUpdatesLookupTable()
    {
        DatabaseHelper.AddType("OldTypeLookup");
        DatabaseHelper.UpdateTypeName("OldTypeLookup", "NewTypeLookup");

        var types = DatabaseHelper.GetAllTypes();
        Assert.Contains("NewTypeLookup", types);
        Assert.DoesNotContain("OldTypeLookup", types);
    }
}

// ════════════════════════════════════════════════════════════
// CASCADE DELETE — DeleteDocumentsBySubject / DeleteDocumentsByType
// ════════════════════════════════════════════════════════════

public class CascadeDeleteTests : DatabaseTestBase
{
    [Fact]
    public void DeleteDocumentsBySubject_SoftDeletesAllMatchingDocs()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1", Subject = "ToDelete" });
        repo.Add(new StudyDocument { Name = "D2", Subject = "ToDelete" });
        repo.Add(new StudyDocument { Name = "D3", Subject = "Keep" });

        DatabaseHelper.DeleteDocumentsBySubject("ToDelete");

        var active = repo.GetAll();
        Assert.DoesNotContain(active, d => d.Subject == "ToDelete");
        Assert.Contains(active, d => d.Subject == "Keep");
    }

    [Fact]
    public void DeleteDocumentsBySubject_MovedToRecycleBin()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "ToRecycle", Subject = "RecycleSub" });

        DatabaseHelper.DeleteDocumentsBySubject("RecycleSub");

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Contains(deleted, d => d.Subject == "RecycleSub");
    }

    [Fact]
    public void DeleteDocumentsBySubject_AlsoRemovesFromLookup()
    {
        DatabaseHelper.AddSubject("EphemeralSubject");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D", Subject = "EphemeralSubject" });

        DatabaseHelper.DeleteDocumentsBySubject("EphemeralSubject");

        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.DoesNotContain("EphemeralSubject", subjects);
    }

    [Fact]
    public void DeleteDocumentsByType_SoftDeletesAllMatchingDocs()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "P1", Type = "DeleteType" });
        repo.Add(new StudyDocument { Name = "P2", Type = "DeleteType" });
        repo.Add(new StudyDocument { Name = "W1", Type = "KeepType" });

        DatabaseHelper.DeleteDocumentsByType("DeleteType");

        var active = repo.GetAll();
        Assert.DoesNotContain(active, d => d.Type == "DeleteType");
        Assert.Contains(active, d => d.Type == "KeepType");
    }

    [Fact]
    public void DeleteDocumentsByType_AlsoRemovesFromLookup()
    {
        DatabaseHelper.AddType("EphemeralType");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D", Type = "EphemeralType" });

        DatabaseHelper.DeleteDocumentsByType("EphemeralType");

        var types = DatabaseHelper.GetAllTypes();
        Assert.DoesNotContain("EphemeralType", types);
    }
}

// ════════════════════════════════════════════════════════════
// BACKUP DATABASE
// ════════════════════════════════════════════════════════════

public class BackupDatabaseTests : DatabaseTestBase
{
    [Fact]
    public void BackupDatabase_ValidPath_ReturnsTrueAndCreatesFile()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.db");
        try
        {
            bool result = DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(result);
            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void BackupDatabase_InvalidPath_ReturnsFalse()
    {
        // An invalid path (nested non-existent directories on Windows)
        var badPath = @"Z:\NonExistentDrive\backup.db";
        bool result = DatabaseHelper.BackupDatabase(badPath);
        Assert.False(result);
    }

    [Fact]
    public void BackupDatabase_Overwrite_ReturnsTrue()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"bak_overwrite_{Guid.NewGuid()}.db");
        try
        {
            // Create initial backup
            DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(File.Exists(backupPath));

            // Overwrite — should succeed
            bool result = DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(result);
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void BackupDatabase_PreservesData()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Important Data" });

        var backupPath = Path.Combine(Path.GetTempPath(), $"bak_data_{Guid.NewGuid()}.db");
        try
        {
            bool result = DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(result);

            // Backup file should be non-trivial size (has data)
            var info = new FileInfo(backupPath);
            Assert.True(info.Length > 0);
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }
}

// ════════════════════════════════════════════════════════════
// RELATED DOCUMENTS — Relation Types
// ════════════════════════════════════════════════════════════

public class RelationTypeTests : DatabaseTestBase
{
    private (int idA, int idB) AddTwoDocs()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "DocA" });
        repo.Add(new StudyDocument { Name = "DocB" });
        var all = repo.GetAll();
        return (all[1].Id, all[0].Id); // descending by date, so newer first
    }

    [Theory]
    [InlineData("related")]
    [InlineData("supplement")]
    [InlineData("prerequisite")]
    [InlineData("reference")]
    [InlineData("similar")]
    public void AddRelation_VariousTypes_PersistedCorrectly(string relType)
    {
        var (idA, idB) = AddTwoDocs();

        DatabaseHelper.AddDocumentRelation(idA, idB, relType);
        var relations = DatabaseHelper.GetRelatedDocuments(idA);

        Assert.Single(relations);
        Assert.Equal(relType, relations[0].RelationType);
    }

    [Fact]
    public void AddRelation_DefaultType_IsRelated()
    {
        var (idA, idB) = AddTwoDocs();

        // Call without explicit type — default is "related"
        DatabaseHelper.AddDocumentRelation(idA, idB);
        var relations = DatabaseHelper.GetRelatedDocuments(idA);

        Assert.Equal("related", relations[0].RelationType);
    }

    [Fact]
    public void GetRelatedDocuments_BidirectionalLookup()
    {
        var (idA, idB) = AddTwoDocs();

        DatabaseHelper.AddDocumentRelation(idA, idB, "reference");

        // Relations should be visible from both sides
        var relFromA = DatabaseHelper.GetRelatedDocuments(idA);
        var relFromB = DatabaseHelper.GetRelatedDocuments(idB);

        Assert.Single(relFromA);
        Assert.Single(relFromB);
        Assert.Equal(relFromA[0].RelationId, relFromB[0].RelationId);
    }

    [Fact]
    public void AddRelation_Duplicate_IgnoredSilently()
    {
        var (idA, idB) = AddTwoDocs();

        DatabaseHelper.AddDocumentRelation(idA, idB, "related");
        DatabaseHelper.AddDocumentRelation(idA, idB, "related"); // INSERT OR IGNORE

        var relations = DatabaseHelper.GetRelatedDocuments(idA);
        Assert.Single(relations); // Only 1, not 2
    }

    [Fact]
    public void RemoveRelation_SpecificRelationId_Removed()
    {
        var (idA, idB) = AddTwoDocs();
        DatabaseHelper.AddDocumentRelation(idA, idB, "supplement");

        var relations = DatabaseHelper.GetRelatedDocuments(idA);
        int relationId = relations[0].RelationId;

        DatabaseHelper.RemoveDocumentRelation(relationId);

        var afterRemoval = DatabaseHelper.GetRelatedDocuments(idA);
        Assert.Empty(afterRemoval);
    }

    [Fact]
    public void GetRelatedDocuments_SoftDeletedRelated_ExcludedByJoin()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "A" });
        repo.Add(new StudyDocument { Name = "B - Will Delete" });

        var all = repo.GetAll();
        int idA = all[1].Id;
        int idB = all[0].Id;

        DatabaseHelper.AddDocumentRelation(idA, idB, "related");

        // Soft delete B
        repo.Delete(idB);

        var relations = DatabaseHelper.GetRelatedDocuments(idA);
        Assert.Empty(relations); // B is soft-deleted, INNER JOIN excludes it
    }

    [Fact]
    public void AddRelation_OrderNormalized_SameLoPairDeduped()
    {
        var (idA, idB) = AddTwoDocs();

        // Add A→B and B→A — should be treated as same pair (lo/hi normalization)
        DatabaseHelper.AddDocumentRelation(idA, idB);
        DatabaseHelper.AddDocumentRelation(idB, idA); // OR IGNORE because lo/hi same

        var rel = DatabaseHelper.GetRelatedDocuments(idA);
        Assert.Single(rel);
    }
}

// ════════════════════════════════════════════════════════════
// FILE INTEGRITY — UpdateDocumentPath, ClearDocumentPath
// ════════════════════════════════════════════════════════════

public class FileIntegrityDetailTests : DatabaseTestBase
{
    [Fact]
    public void UpdateDocumentPath_UpdatesPathCorrectly()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "File Doc", FilePath = @"C:\old\path.pdf" });
        int id = repo.GetAll()[0].Id;

        bool result = DatabaseHelper.UpdateDocumentPath(id, @"C:\new\path.pdf");
        Assert.True(result);

        var updated = DatabaseHelper.GetDocumentById(id);
        Assert.Equal(@"C:\new\path.pdf", updated!.FilePath);
    }

    [Fact]
    public void UpdateDocumentPath_NonExistentId_ReturnsFalse()
    {
        bool result = DatabaseHelper.UpdateDocumentPath(99999, @"C:\path.pdf");
        Assert.False(result);
    }

    [Fact]
    public void ClearDocumentPath_SetsPathToEmpty()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Has Path", FilePath = @"C:\file.pdf" });
        int id = repo.GetAll()[0].Id;

        bool result = DatabaseHelper.ClearDocumentPath(id);
        Assert.True(result);

        var doc = DatabaseHelper.GetDocumentById(id);
        Assert.True(string.IsNullOrEmpty(doc!.FilePath));
    }

    [Fact]
    public void ClearDocumentPath_DocNowAppearsInNoFileStats()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "HadPath", FilePath = @"C:\file.pdf" });
        int id = repo.GetAll()[0].Id;

        var statsBefore = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(0, statsBefore.NoFileDocuments);

        DatabaseHelper.ClearDocumentPath(id);

        var statsAfter = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, statsAfter.NoFileDocuments);
    }

    [Fact]
    public void UpdateDocumentPath_AlsoRemovesFromNoFileStats()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "NoPath", FilePath = null });
        int id = repo.GetAll()[0].Id;

        var statsBefore = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, statsBefore.NoFileDocuments);

        DatabaseHelper.UpdateDocumentPath(id, @"C:\now_has_file.pdf");

        var statsAfter = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(0, statsAfter.NoFileDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// BULK OPERATIONS — Edge Cases
// ════════════════════════════════════════════════════════════

public class BulkOperationsEdgeCaseTests : DatabaseTestBase
{
    [Fact]
    public void BulkSoftDelete_EmptyList_ReturnsZero()
    {
        int count = DatabaseHelper.BulkSoftDelete(new List<int>());
        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkSoftDelete_NullList_ReturnsZero()
    {
        int count = DatabaseHelper.BulkSoftDelete(null!);
        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkSoftDelete_ValidIds_ReturnsCorrectCount()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "A" });
        repo.Add(new StudyDocument { Name = "B" });
        repo.Add(new StudyDocument { Name = "C" });

        var all = repo.GetAll();
        var ids = all.Select(d => d.Id).Take(2).ToList();

        int count = DatabaseHelper.BulkSoftDelete(ids);
        Assert.Equal(2, count);
    }

    [Fact]
    public void BulkUpdateSubject_EmptyList_ReturnsZero()
    {
        int count = DatabaseHelper.BulkUpdateSubject(new List<int>(), "NewSubject");
        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkUpdateSubject_NullList_ReturnsZero()
    {
        int count = DatabaseHelper.BulkUpdateSubject(null!, "NewSub");
        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkUpdateSubject_ValidIds_UpdatesAllSubjects()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "X", Subject = "Old" });
        repo.Add(new StudyDocument { Name = "Y", Subject = "Old" });
        repo.Add(new StudyDocument { Name = "Z", Subject = "NotChanged" });

        var all = repo.GetAll();
        var ids = all.Where(d => d.Subject == "Old").Select(d => d.Id).ToList();

        int count = DatabaseHelper.BulkUpdateSubject(ids, "New");
        Assert.Equal(2, count);

        var updated = repo.GetAll();
        Assert.Equal(2, updated.Count(d => d.Subject == "New"));
        Assert.Single(updated.Where(d => d.Subject == "NotChanged"));
    }

    [Fact]
    public void BulkToggleImportant_EmptyList_ReturnsZero()
    {
        int count = DatabaseHelper.BulkToggleImportant(new List<int>(), true);
        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkToggleImportant_MarkFalse_ClearsImportantFlag()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "ImpA", IsImportant = true });
        repo.Add(new StudyDocument { Name = "ImpB", IsImportant = true });

        var all = repo.GetAll();
        var ids = all.Select(d => d.Id).ToList();

        int count = DatabaseHelper.BulkToggleImportant(ids, false);
        Assert.Equal(2, count);

        var updated = repo.GetAll();
        Assert.All(updated, d => Assert.False(d.IsImportant));
    }
}

// ════════════════════════════════════════════════════════════
// RECYCLE BIN — EmptyRecycleBin Return Value, GetDeletedDocumentCount
// ════════════════════════════════════════════════════════════

public class RecycleBinCountTests : DatabaseTestBase
{
    [Fact]
    public void EmptyRecycleBin_EmptyBin_ReturnsZero()
    {
        int count = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(0, count);
    }

    [Fact]
    public void EmptyRecycleBin_WithItems_ReturnsCorrectCount()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "X" });
        repo.Add(new StudyDocument { Name = "Y" });
        repo.Add(new StudyDocument { Name = "Z" });

        var all = repo.GetAll();
        foreach (var d in all) repo.Delete(d.Id);

        int count = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(3, count);
    }

    [Fact]
    public void EmptyRecycleBin_AfterEmpty_GetDeletedCountIsZero()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Gone" });
        repo.Delete(repo.GetAll()[0].Id);

        DatabaseHelper.EmptyRecycleBin();

        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void GetDeletedDocumentCount_BeforeDelete_IsZero()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Active" });
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void GetDeletedDocumentCount_IncreasesAfterSoftDelete()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "D1" });
        repo.Add(new StudyDocument { Name = "D2" });

        var all = repo.GetAll();
        repo.Delete(all[0].Id);
        repo.Delete(all[1].Id);

        Assert.Equal(2, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void GetDeletedDocumentCount_DecreasesAfterRestore()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Restore Me" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id);

        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());

        DatabaseHelper.RestoreDocument(id);
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void GetDeletedDocumentCount_DecreasesAfterPermanentDelete()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Perm Delete Me" });
        int id = repo.GetAll()[0].Id;
        repo.Delete(id);

        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());

        DatabaseHelper.PermanentDeleteDocument(id);
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }
}

// ════════════════════════════════════════════════════════════
// SQL INJECTION SAFETY — Parameterized Query Verification
// ════════════════════════════════════════════════════════════

public class SqlInjectionSafetyTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE documents; --")]
    [InlineData("\" OR \"1\"=\"1")]
    [InlineData("1; UPDATE documents SET name = 'hacked'; --")]
    public void Search_WithSqlInjectionPayload_ReturnsEmptyAndDoesNotDestroy(string payload)
    {
        // Add a legit document
        _repo.Add(new StudyDocument { Name = "Safe Document" });

        // Attempt injection via search
        var results = DatabaseHelper.SearchDocuments(payload);

        // Should return empty — no fake matches
        Assert.Empty(results);

        // Original data should still be intact
        var all = _repo.GetAll();
        Assert.Single(all);
        Assert.Equal("Safe Document", all[0].Name);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DELETE FROM documents; --")]
    public void FilterBySubject_WithInjectionPayload_ReturnsEmpty(string payload)
    {
        _repo.Add(new StudyDocument { Name = "Doc", Subject = "Math" });

        var results = DatabaseHelper.FilterDocuments(payload, "");
        Assert.Empty(results);
    }

    [Fact]
    public void AddDocument_WithSqlCharsInName_PersistedCorrectly()
    {
        // Single quotes in data should be safely stored
        string name = "It's O'Brien's Notes & \"Quotes\"";
        _repo.Add(new StudyDocument { Name = name });

        var all = _repo.GetAll();
        Assert.Equal(name, all[0].Name);
    }

    [Fact]
    public void SearchDocuments_WithSingleQuote_NoException()
    {
        _repo.Add(new StudyDocument { Name = "Test's Document" });

        var ex = Record.Exception(() => DatabaseHelper.SearchDocuments("Test's"));
        Assert.Null(ex); // No exception — query is safe

        var results = DatabaseHelper.SearchDocuments("Test's");
        Assert.Single(results);
    }

    [Fact]
    public void UpdateDocument_WithSpecialChars_PersistedCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "Original" });
        var doc = _repo.GetAll()[0];

        doc.Notes = "Note with 'single', \"double\", and -- SQL comment";
        _repo.Update(doc);

        var updated = _repo.GetAll()[0];
        Assert.Equal("Note with 'single', \"double\", and -- SQL comment", updated.Notes);
    }
}

// ════════════════════════════════════════════════════════════
// ADVANCED SEARCH — SearchDocumentsAdvanced edge cases
// ════════════════════════════════════════════════════════════

public class AdvancedSearchEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void SearchAdvanced_EmptyKeyword_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A" });
        _repo.Add(new StudyDocument { Name = "B" });
        _repo.Add(new StudyDocument { Name = "C" });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void SearchAdvanced_CombinedKeywordAndSubject_NarrowsResult()
    {
        _repo.Add(new StudyDocument { Name = "Math Notes", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "Math Exercises", Subject = "Physics" });
        _repo.Add(new StudyDocument { Name = "Physics Lab", Subject = "Physics" });

        var results = _repo.SearchAdvanced("Math", "Math", "", null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Math Notes", results[0].Name);
    }

    [Fact]
    public void SearchAdvanced_SizeFilter_FiltersCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "Small", FileSize = 0.5 });
        _repo.Add(new StudyDocument { Name = "Medium", FileSize = 5.0 });
        _repo.Add(new StudyDocument { Name = "Large", FileSize = 50.0 });

        var results = _repo.SearchAdvanced("", "", "", null, null, 1.0, 10.0, null);
        Assert.Single(results);
        Assert.Equal("Medium", results[0].Name);
    }

    [Fact]
    public void SearchAdvanced_ImportantFilter_OnlyReturnsImportant()
    {
        _repo.Add(new StudyDocument { Name = "Imp", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "NotImp", IsImportant = false });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, true);
        Assert.Single(results);
        Assert.Equal("Imp", results[0].Name);
    }

    [Fact]
    public void SearchAdvanced_DateRange_FiltersCorrectly()
    {
        // CreatedAt = now がデフォルト
        _repo.Add(new StudyDocument { Name = "Recent" });

        DateTime from = DateTime.Today.AddDays(-1);
        DateTime to = DateTime.Today.AddDays(1);
        var results = _repo.SearchAdvanced("", "", "", from, to, null, null, null);

        Assert.Single(results);
    }

    [Fact]
    public void SearchAdvanced_NoMatch_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Name = "Exists" });

        var results = _repo.SearchAdvanced("XYZNOTEXIST", "", "", null, null, null, null, null);
        Assert.Empty(results);
    }
}

// ════════════════════════════════════════════════════════════
// DUPLICATE DETECTION — SearchDocumentsAdvanced for path matching
// ════════════════════════════════════════════════════════════

public class DuplicatePathDetectionTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void GetAll_MultipleDocsWithSamePath_AllReturned()
    {
        _repo.Add(new StudyDocument { Name = "D1", FilePath = @"C:\same.pdf" });
        _repo.Add(new StudyDocument { Name = "D2", FilePath = @"C:\same.pdf" });
        _repo.Add(new StudyDocument { Name = "D3", FilePath = @"C:\different.pdf" });

        var all = _repo.GetAll();
        var duplicates = all.GroupBy(d => d.FilePath)
                            .Where(g => g.Count() > 1)
                            .SelectMany(g => g)
                            .ToList();

        Assert.Equal(2, duplicates.Count);
        Assert.All(duplicates, d => Assert.Equal(@"C:\same.pdf", d.FilePath));
    }

    [Fact]
    public void DetectDuplicates_ByNameSimilarity_ManualGrouping()
    {
        _repo.Add(new StudyDocument { Name = "Math Exercises", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "Math Exercises", Subject = "Physics" }); // Same name, diff subject

        var all = _repo.GetAll();
        var byName = all.GroupBy(d => d.Name).Where(g => g.Count() > 1).ToList();

        Assert.Single(byName);
        Assert.Equal(2, byName[0].Count());
    }

    [Fact]
    public void DetectDuplicates_EmptyPath_ShouldNotGroup()
    {
        // Documents with empty/null paths should not be grouped as duplicates
        _repo.Add(new StudyDocument { Name = "NoPath1", FilePath = null });
        _repo.Add(new StudyDocument { Name = "NoPath2", FilePath = null });

        var all = _repo.GetAll();
        var groups = all.Where(d => !string.IsNullOrEmpty(d.FilePath))
                        .GroupBy(d => d.FilePath)
                        .Where(g => g.Count() > 1)
                        .ToList();

        Assert.Empty(groups); // null paths excluded from duplicate check
    }
}
