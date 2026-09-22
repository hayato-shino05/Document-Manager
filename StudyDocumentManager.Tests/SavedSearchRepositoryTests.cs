using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SavedSearchRepositoryTests : DatabaseTestBase
{
    private readonly SavedSearchRepository SearchRepo;

    public SavedSearchRepositoryTests()
    {
        SearchRepo = new SavedSearchRepository(Db);
    }

    [Fact]
    public void Add_GetById_PreservesNameCriteriaJsonAndCreatedAt()
    {
        var criteria = new SavedSearchCriteria
        {
            Kind = SavedSearchKinds.Standard,
            Keyword = "量子レポート",
            MinSize = 1.5,
            IsImportant = true,
            RecentDays = 14
        };
        var json = criteria.ToJson();

        var id = SearchRepo.Add(new SavedSearch { Name = "Physics reports", CriteriaJson = json });

        Assert.True(id > 0);
        var loaded = SearchRepo.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal("Physics reports", loaded!.Name);
        Assert.Equal(json, loaded.CriteriaJson);
        Assert.True((DateTime.Now - loaded.CreatedAt).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetAll_OrdersNamesCaseInsensitively()
    {
        SearchRepo.Add(new SavedSearch { Name = "Bravo" });
        SearchRepo.Add(new SavedSearch { Name = "alpha" });
        SearchRepo.Add(new SavedSearch { Name = "Charlie" });

        var names = SearchRepo.GetAll().Select(search => search.Name).ToList();

        Assert.Equal(["alpha", "Bravo", "Charlie"], names);
    }

    [Fact]
    public void NameExists_MatchesExistingNamesCaseInsensitively()
    {
        SearchRepo.Add(new SavedSearch { Name = "Report" });

        Assert.True(SearchRepo.NameExists("Report"));
        Assert.True(SearchRepo.NameExists("report"));
        Assert.True(SearchRepo.NameExists("REPORT"));
        Assert.False(SearchRepo.NameExists("missing"));
    }

    [Fact]
    public void Update_ChangesNameAndJson_AndReturnsFalseForUnknownId()
    {
        var id = SearchRepo.Add(new SavedSearch { Name = "Old name", CriteriaJson = "{\"Kind\":\"standard\"}" });

        Assert.True(SearchRepo.Update(new SavedSearch { Id = id, Name = "New name", CriteriaJson = "{\"Kind\":\"due-soon\"}" }));

        var updated = SearchRepo.GetById(id);
        Assert.NotNull(updated);
        Assert.Equal("New name", updated!.Name);
        Assert.Equal("{\"Kind\":\"due-soon\"}", updated.CriteriaJson);

        Assert.False(SearchRepo.Update(new SavedSearch { Id = id + 1000, Name = "Ghost", CriteriaJson = "{}" }));
    }

    [Fact]
    public void Delete_RemovesRow_AndReturnsFalseForUnknownId()
    {
        var id = SearchRepo.Add(new SavedSearch { Name = "Temporary", CriteriaJson = "{}" });

        Assert.True(SearchRepo.Delete(id));
        Assert.Null(SearchRepo.GetById(id));
        Assert.False(SearchRepo.Delete(id + 1000));
    }

    [Fact]
    public void DoubleInitializeDatabase_PreservesSavedSearchRows()
    {
        var id = SearchRepo.Add(new SavedSearch { Name = "Stable search", CriteriaJson = "{\"Kind\":\"standard\"}" });

        Db.InitializeDatabase();

        var loaded = SearchRepo.GetById(id);
        Assert.NotNull(loaded);
        Assert.Equal("Stable search", loaded!.Name);
        Assert.Single(SearchRepo.GetAll());
    }

    [Fact]
    public void InitializeDatabase_CreatesSavedSearchesTable()
    {
        using var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'saved_searches'";
        Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
    }

    [Fact]
    public void GetUncategorizedDocuments_ReturnsOnlyActiveDocumentsWithoutSubject()
    {
        Repo.Add(new StudyDocument { Name = "Categorized", Subject = "Math" });
        Repo.Add(new StudyDocument { Name = "Empty subject active", Subject = "" });
        Repo.Add(new StudyDocument { Name = "Deleted uncategorized" });
        var deletedId = Repo.GetAll().Single(document => document.Name == "Deleted uncategorized").Id;
        Repo.Delete(deletedId);

        var results = Repo.GetUncategorizedDocuments();

        Assert.Single(results);
        Assert.DoesNotContain(results, document => document.Name == "Categorized");
        Assert.DoesNotContain(results, document => document.Name == "Deleted uncategorized");
        Assert.Contains(results, document => document.Name == "Empty subject active");
    }

    [Fact]
    public void GetDocumentsWithMissingMetadata_ReturnsActiveDocumentsMissingAnyField()
    {
        Repo.Add(new StudyDocument { Name = "No subject" });
        Repo.Add(new StudyDocument { Name = "No type", Subject = "Math" });
        Repo.Add(new StudyDocument { Name = "No tags", Subject = "Math", Type = "PDF" });
        Repo.Add(new StudyDocument { Name = "Complete", Subject = "Math", Type = "PDF", Tags = "exam" });

        var results = Repo.GetDocumentsWithMissingMetadata();

        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, document => document.Name == "Complete");
        Assert.Contains(results, document => document.Name == "No subject");
        Assert.Contains(results, document => document.Name == "No type");
        Assert.Contains(results, document => document.Name == "No tags");
    }

    [Fact]
    public void BackupValidator_AcceptsSchemaVersion4Candidate()
    {
        var id = SearchRepo.Add(new SavedSearch { Name = "Backup proof", CriteriaJson = "{\"Keyword\":\"backup\"}" });
        var candidatePath = CreateTemporaryPath("v4.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            Assert.True(Db.CanRestoreDatabase(candidatePath));

            Assert.True(Db.RestoreDatabase(candidatePath));
            Assert.NotNull(SearchRepo.GetById(id));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }

    [Fact]
    public void RestoreDatabase_LegacyVersion3CandidateWithoutSavedSearchesTable_IsAccepted()
    {
        var candidatePath = CreateTemporaryPath("legacy-v3.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE saved_searches; UPDATE app_settings SET value = '3' WHERE key = 'schema_version';";
                command.ExecuteNonQuery();
            }

            Assert.True(Db.CanRestoreDatabase(candidatePath));
            Assert.True(Db.RestoreDatabase(candidatePath));
            Assert.Empty(SearchRepo.GetAll());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }

    [Fact]
    public void Add_GetById_PreservesUnvalidatedCriteriaVerbatim()
    {
        var original = new SavedSearchCriteria
        {
            Kind = "time-travel",
            RecentDays = -5,
            DeadlineDays = -5,
            FromDate = new DateTime(2026, 12, 31),
            ToDate = new DateTime(2026, 1, 1)
        };
        var json = original.ToJson();

        var id = SearchRepo.Add(new SavedSearch { Name = "Raw criteria", CriteriaJson = json });

        var loaded = SearchRepo.GetById(id);

        Assert.NotNull(loaded);
        Assert.Equal(json, loaded!.CriteriaJson);
        var restored = SavedSearchCriteria.FromJson(loaded.CriteriaJson);
        Assert.NotNull(restored);
        Assert.Equal("time-travel", restored!.Kind);
        Assert.Equal(-5, restored.RecentDays);
        Assert.Equal(-5, restored.DeadlineDays);
        Assert.Equal(new DateTime(2026, 12, 31), restored.FromDate);
        Assert.Equal(new DateTime(2026, 1, 1), restored.ToDate);
        Assert.True(restored.FromDate > restored.ToDate);
    }

    private static string CreateTemporaryPath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}_{suffix}");

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

public class SavedSearchCriteriaTests
{
    [Fact]
    public void ToJson_FromJson_RoundTripsAllValues()
    {
        var original = new SavedSearchCriteria
        {
            Kind = SavedSearchKinds.RecentlyAdded,
            Keyword = "k",
            Subject = "Math",
            Type = "PDF",
            FromDate = new DateTime(2026, 1, 2),
            ToDate = new DateTime(2026, 2, 3),
            MinSize = 0.5,
            MaxSize = 10.25,
            IsImportant = true,
            RecentDays = 30,
            DeadlineDays = 14
        };

        var restored = SavedSearchCriteria.FromJson(original.ToJson());

        Assert.NotNull(restored);
        Assert.Equivalent(original, restored);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        Assert.Null(SavedSearchCriteria.FromJson("not-json"));
        Assert.Null(SavedSearchCriteria.FromJson(""));
    }

    [Fact]
    public void NewCriteria_HasDefaultKindAndWindows()
    {
        var criteria = new SavedSearchCriteria();

        Assert.Equal(SavedSearchKinds.Standard, criteria.Kind);
        Assert.Equal(7, criteria.RecentDays);
        Assert.Equal(7, criteria.DeadlineDays);
    }
}

public class SavedSearchCriteriaCharacterizationTests
{
    [Fact]
    public void FromJson_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SavedSearchCriteria.FromJson(null!));
    }

    [Fact]
    public void FromJson_EmptyObject_ReturnsDefaults()
    {
        var restored = SavedSearchCriteria.FromJson("{}");

        Assert.NotNull(restored);
        Assert.Equal(SavedSearchKinds.Standard, restored!.Kind);
        Assert.Null(restored.Keyword);
        Assert.Null(restored.Subject);
        Assert.Null(restored.Type);
        Assert.Null(restored.FromDate);
        Assert.Null(restored.ToDate);
        Assert.Null(restored.MinSize);
        Assert.Null(restored.MaxSize);
        Assert.Null(restored.IsImportant);
        Assert.Equal(7, restored.RecentDays);
        Assert.Equal(7, restored.DeadlineDays);
    }

    [Fact]
    public void Roundtrip_UnsupportedKind_PreservedVerbatim()
    {
        var original = new SavedSearchCriteria { Kind = "time-travel" };

        var restored = SavedSearchCriteria.FromJson(original.ToJson());

        Assert.NotNull(restored);
        Assert.Equal("time-travel", restored!.Kind);

        var fromRaw = SavedSearchCriteria.FromJson("{\"Kind\":\"time-travel\"}");
        Assert.NotNull(fromRaw);
        Assert.Equal("time-travel", fromRaw!.Kind);
        Assert.Contains("\"Kind\":\"time-travel\"", fromRaw.ToJson());
    }

    [Fact]
    public void Roundtrip_NegativeDayWindows_NotClamped()
    {
        var original = new SavedSearchCriteria { RecentDays = -5, DeadlineDays = -5 };

        var restored = SavedSearchCriteria.FromJson(original.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(-5, restored!.RecentDays);
        Assert.Equal(-5, restored.DeadlineDays);
        Assert.Contains("\"RecentDays\":-5", original.ToJson());
    }

    [Fact]
    public void Roundtrip_ReversedDates_PreservedAsIs()
    {
        var original = new SavedSearchCriteria
        {
            FromDate = new DateTime(2026, 12, 31),
            ToDate = new DateTime(2026, 1, 1)
        };

        var restored = SavedSearchCriteria.FromJson(original.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(new DateTime(2026, 12, 31), restored!.FromDate);
        Assert.Equal(new DateTime(2026, 1, 1), restored.ToDate);
        Assert.True(restored.FromDate > restored.ToDate);
    }

    [Fact]
    public void FromJson_UnknownProperties_Ignored()
    {
        var restored = SavedSearchCriteria.FromJson("{\"Kind\":\"standard\",\"hackerField\":1}");

        Assert.NotNull(restored);
        Assert.Equal(SavedSearchKinds.Standard, restored!.Kind);
        Assert.Null(restored.Keyword);
    }
}
