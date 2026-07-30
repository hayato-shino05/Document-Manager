using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BulkDeleteFlowTests
{
    [Fact]
    public void RowSelectionChange_NotifiesSelectedCount()
    {
        var repository = new BulkDocumentRepository([
            new StudyDocument { Id = 1, Name = "A", Subject = "Math", Type = "PDF" },
            new StudyDocument { Id = 2, Name = "B", Subject = "Math", Type = "PDF" }
        ]);
        var model = CreateModel(repository, new BulkOperationRepositoryStub(repository), new BulkDialogService());
        model.Initialize();

        var notified = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BulkDeleteModel.SelectedCount))
                notified = true;
        };

        model.Documents[0].IsSelected = true;

        Assert.True(notified);
        Assert.Equal(1, model.SelectedCount);
    }

    [Fact]
    public async Task MarkImportant_Success_RetainsSelectedIds()
    {
        var repository = new BulkDocumentRepository([
            new StudyDocument { Id = 1, Name = "A", Subject = "Math", Type = "PDF", IsImportant = false },
            new StudyDocument { Id = 2, Name = "B", Subject = "Math", Type = "PDF", IsImportant = false }
        ]);
        var dialog = new BulkDialogService();
        var model = CreateModel(repository, new BulkOperationRepositoryStub(repository), dialog);
        model.Initialize();
        model.Documents[0].IsSelected = true;
        model.Documents[1].IsSelected = true;

        await model.MarkImportantCommand.ExecuteAsync(null);

        Assert.Equal(2, model.SelectedCount);
        Assert.All(model.Documents, document => Assert.True(document.IsSelected));
        Assert.All(model.Documents, document => Assert.True(document.Document.IsImportant));
    }

    [Fact]
    public async Task ChangeSubject_Success_RetainsVisibleSelectedIds()
    {
        var repository = new BulkDocumentRepository([
            new StudyDocument { Id = 1, Name = "A", Subject = "Math", Type = "PDF" },
            new StudyDocument { Id = 2, Name = "B", Subject = "Math", Type = "PDF" }
        ]);
        var dialog = new BulkDialogService { ConfirmResult = true };
        var model = CreateModel(repository, new BulkOperationRepositoryStub(repository), dialog);
        model.Initialize();
        model.Documents[0].IsSelected = true;
        model.Documents[1].IsSelected = true;
        model.NewSubjectValue = "Physics";

        await model.ChangeSubjectCommand.ExecuteAsync(null);

        Assert.Equal(2, model.SelectedCount);
        Assert.All(model.Documents, document => Assert.True(document.IsSelected));
        Assert.All(model.Documents, document => Assert.Equal("Physics", document.Document.Subject));
    }

    [Fact]
    public void SelectedCountText_ReflectsCount()
    {
        var repository = new BulkDocumentRepository([
            new StudyDocument { Id = 1, Name = "A", Subject = "Math", Type = "PDF" },
            new StudyDocument { Id = 2, Name = "B", Subject = "Math", Type = "PDF" }
        ]);
        var model = CreateModel(repository, new BulkOperationRepositoryStub(repository), new BulkDialogService());
        model.Initialize();

        Assert.Equal("Selected: 0", model.SelectedCountText);
        model.Documents[0].IsSelected = true;
        Assert.Equal("Selected: 1", model.SelectedCountText);
    }

    private static BulkDeleteModel CreateModel(BulkDocumentRepository documentRepository, BulkOperationRepositoryStub bulkRepository, BulkDialogService dialog)
    {
        return new BulkDeleteModel(
            documentRepository,
            bulkRepository,
            new BulkCategoryRepository(),
            dialog,
            new BulkNavigationService(),
            new BulkLocalizationService());
    }

    private sealed class BulkDocumentRepository(List<StudyDocument> documents) : IDocumentRepository
    {
        private readonly List<StudyDocument> _documents = documents;

        public List<StudyDocument> GetAll() => _documents.Select(Clone).ToList();
        public StudyDocument? GetById(int id) => _documents.FirstOrDefault(document => document.Id == id);
        public List<StudyDocument> Search(string keyword) => GetAll();
        public List<StudyDocument> Filter(string subject, string type) => GetAll();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => GetAll();
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => _documents.Select(document => document.Subject).Where(subject => !string.IsNullOrWhiteSpace(subject)).Distinct().ToList();
        public List<string> GetDistinctTypes() => _documents.Select(document => document.Type).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct().ToList();
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }

        public void MarkImportant(List<int> ids, bool important)
        {
            foreach (var document in _documents.Where(document => ids.Contains(document.Id)))
                document.IsImportant = important;
        }

        public void ChangeSubject(List<int> ids, string subject)
        {
            foreach (var document in _documents.Where(document => ids.Contains(document.Id)))
                document.Subject = subject;
        }

        private static StudyDocument Clone(StudyDocument source)
            => new()
            {
                Id = source.Id,
                Name = source.Name,
                Subject = source.Subject,
                Type = source.Type,
                FilePath = source.FilePath,
                Notes = source.Notes,
                CreatedAt = source.CreatedAt,
                FileSize = source.FileSize,
                Author = source.Author,
                IsImportant = source.IsImportant,
                Tags = source.Tags,
                Deadline = source.Deadline
            };
    }

    private sealed class BulkOperationRepositoryStub(BulkDocumentRepository repository) : IBulkOperationRepository
    {
        public int BulkSoftDelete(List<int> ids) => ids.Count;
        public int BulkUpdateSubject(List<int> ids, string subject)
        {
            repository.ChangeSubject(ids, subject);
            return ids.Count;
        }
        public int BulkToggleImportant(List<int> ids, bool important)
        {
            repository.MarkImportant(ids, important);
            return ids.Count;
        }
    }

    private sealed class BulkCategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => ["Math", "Physics"];
        public List<string> GetAllTypes() => ["PDF"];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => true;
        public bool AddType(string name) => true;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class BulkDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; } = true;
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(ConfirmResult);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class BulkNavigationService : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class BulkLocalizationService : ILocalizationService
    {
        public string this[string key] => key switch
        {
            "Filter_AllItems" => "All",
            "Bulk_SelectedCount" => "Selected: {0}",
            _ => key
        };
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
