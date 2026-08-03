using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class AddEditI18nRefreshTests
{
    [Fact]
    public async Task AddEdit_LanguageChange_RefreshesValidationMessageAndEditTitle()
    {
        var localization = new LocalizationService();
        var model = new AddEditModel(
            new DocumentRepositoryStub(new StudyDocument { Id = 7, Name = "Notes" }),
            new CategoryRepositoryStub(),
            new DialogServiceStub(),
            new FileDialogServiceStub(),
            new NavigationServiceStub(),
            localization);

        model.LoadDocument(7);
        Assert.Equal(localization["AddEdit_PageTitleEdit"], model.PageTitle);

        model.Name = string.Empty;
        await model.SaveCommand.ExecuteAsync(null);

        Assert.True(model.HasNameValidationError);
        Assert.Equal(localization["AddEdit_NameRequired"], model.NameValidationMessage);

        localization.SetLanguage(SupportedLanguage.English);

        Assert.Equal(localization["AddEdit_NameRequired"], model.NameValidationMessage);
        Assert.Equal(localization["AddEdit_PageTitleEdit"], model.PageTitle);
    }

    private sealed class DocumentRepositoryStub(StudyDocument document) : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => id == document.Id ? document : null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => false;
        public bool AddWithCatalogs(StudyDocument document) => false;
        public bool Update(StudyDocument document) => false;
        public bool Delete(int id) => false;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class CategoryRepositoryStub : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => false;
        public bool AddType(string name) => false;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class DialogServiceStub : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class FileDialogServiceStub : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class NavigationServiceStub : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }
}
