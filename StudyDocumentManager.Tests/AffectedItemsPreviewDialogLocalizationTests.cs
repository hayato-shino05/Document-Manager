using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AffectedItemsPreviewDialogLocalizationTests
{
    [AvaloniaFact]
    public void Constructor_ResolvesBareResourceKeys()
    {
        var loc = new MutableLocalizationStub();
        loc.Strings["PV_DeleteCollection"] = "Delete Collection";
        loc.Strings["PV_MembershipNote"] = "Membership note EN";
        loc.Strings["BE_PreviewAffected"] = "Documents: {0}";
        var dialog = new AffectedItemsPreviewDialog(
            "PV_DeleteCollection", 3, new List<string> { "Guide.pdf" }, "PV_MembershipNote", loc);

        try
        {
            dialog.Show();
            Flush();

            Assert.Equal("Delete Collection", dialog.Title);
            Assert.Equal("Delete Collection", dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal("Membership note EN", dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);
            Assert.Equal("Documents: 3", dialog.FindControl<TextBlock>("AffectedNote")!.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void LanguageChanged_RefreshesWindowTitleAndNotes()
    {
        var loc = new MutableLocalizationStub();
        loc.Strings["PV_DeleteCollection"] = "Delete Collection";
        loc.Strings["PV_MembershipNote"] = "Membership note EN";
        loc.Strings["BE_PreviewAffected"] = "Documents: {0}";
        var dialog = new AffectedItemsPreviewDialog(
            "PV_DeleteCollection", 3, new List<string> { "Guide.pdf" }, "PV_MembershipNote", loc);

        try
        {
            dialog.Show();
            Flush();

            Assert.Equal("Delete Collection", dialog.Title);
            Assert.Equal("Documents: 3", dialog.FindControl<TextBlock>("AffectedNote")!.Text);

            loc.Strings["PV_DeleteCollection"] = "コレクション削除";
            loc.Strings["PV_MembershipNote"] = "メンバーシップノート JA";
            loc.Strings["BE_PreviewAffected"] = "ドキュメント: {0}";
            loc.SwitchTo(SupportedLanguage.Japanese);
            Flush();

            Assert.Equal("コレクション削除", dialog.Title);
            Assert.Equal("コレクション削除", dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal("メンバーシップノート JA", dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);
            Assert.Equal("ドキュメント: 3", dialog.FindControl<TextBlock>("AffectedNote")!.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void LanguageChanged_AfterClose_DoesNotAlterTextsAndDoesNotThrow()
    {
        var loc = new MutableLocalizationStub();
        loc.Strings["PV_DeleteCollection"] = "Delete Collection";
        loc.Strings["PV_MembershipNote"] = "Membership note EN";
        var dialog = new AffectedItemsPreviewDialog(
            "PV_DeleteCollection", 1, new List<string> { "Guide.pdf" }, "PV_MembershipNote", loc);

        try
        {
            dialog.Show();
            Flush();
            dialog.Close();

            var titleAfterClose = dialog.FindControl<TextBlock>("TitleText")!.Text;
            var noteAfterClose = dialog.FindControl<TextBlock>("ReversibilityNote")!.Text;

            loc.Strings["PV_DeleteCollection"] = "Changed After Close";
            loc.Strings["PV_MembershipNote"] = "Note Changed After Close";
            loc.SwitchTo(SupportedLanguage.Japanese);
            Flush();

            Assert.Equal(titleAfterClose, dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal(noteAfterClose, dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void ComposedNonKeySource_PassesThroughVerbatimAcrossLanguageChanges()
    {
        var loc = new MutableLocalizationStub();
        loc.Strings["PV_CascadeTitle"] = "Delete targets: {0}";
        loc.Strings["PV_RecycleBinNote"] = "Recycle bin note EN";
        var composed = string.Format(loc.Strings["PV_CascadeTitle"], "Alpha, Beta");
        var composedNote = loc.Strings["PV_RecycleBinNote"];
        var dialog = new AffectedItemsPreviewDialog(
            composed, 2, new List<string> { "Guide.pdf" }, composedNote, loc);

        try
        {
            dialog.Show();
            Flush();

            Assert.Equal(composed, dialog.Title);
            Assert.Equal(composed, dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal(composedNote, dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);

            loc.SwitchTo(SupportedLanguage.Japanese);
            Flush();

            Assert.Equal(composed, dialog.Title);
            Assert.Equal(composed, dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal(composedNote, dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void SourceProvidedDialog_ReformatsWithArgsOnLanguageChange()
    {
        var loc = new MutableLocalizationStub();
        loc.Strings["PV_CascadeTitle"] = "Delete targets: {0}";
        loc.Strings["PV_RecycleBinNote"] = "Recycle bin note EN";
        var dialog = new AffectedItemsPreviewDialog(
            string.Empty, 2, new List<string> { "Guide.pdf" }, string.Empty, loc,
            PreviewTextSource.Key("PV_CascadeTitle", "Alpha, Beta"),
            PreviewTextSource.Key("PV_RecycleBinNote"));

        try
        {
            dialog.Show();
            Flush();

            Assert.Equal("Delete targets: Alpha, Beta", dialog.Title);
            Assert.Equal("Delete targets: Alpha, Beta", dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal("Recycle bin note EN", dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);

            loc.Strings["PV_CascadeTitle"] = "削除対象: {0}";
            loc.Strings["PV_RecycleBinNote"] = "ごみ箱ノート JA";
            loc.SwitchTo(SupportedLanguage.Japanese);
            Flush();

            Assert.Equal("削除対象: Alpha, Beta", dialog.Title);
            Assert.Equal("削除対象: Alpha, Beta", dialog.FindControl<TextBlock>("TitleText")!.Text);
            Assert.Equal("ごみ箱ノート JA", dialog.FindControl<TextBlock>("ReversibilityNote")!.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task CallerPath_CategoryDelete_PassesCascadeKeySource_AndDialogReformatsLive()
    {
        var capturedTitles = new List<PreviewTextSource>();
        var dialogs = new PreviewCapturingCustomDialogs(capturedTitles);
        var documents = new CallerPathDocuments(
        [
            new StudyDocument { Id = 1, Name = "Guide.pdf", Subject = "Math101" },
            new StudyDocument { Id = 2, Name = "Notes.pdf", Subject = "Other" }
        ]);
        var model = new CategoryManagementModel(documents, new CallerPathCategories(), new NoOpDialogs(),
            new MutableLocalizationStub(), dialogs);

        var target = model.Subjects.First(s => s.Name == "Math101");
        model.SelectedSubjects = new List<CategoryItem> { target };

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        var titleSource = Assert.Single(capturedTitles);
        Assert.Equal("PV_CascadeTitle", titleSource.KeyOrText);
        Assert.Equal(new[] { "'Math101'" }, titleSource.FormatArgs);

        var loc = new MutableLocalizationStub();
        loc.Strings["PV_CascadeTitle"] = "Delete targets: {0}";
        loc.Strings["PV_RecycleBinNote"] = "Recycle bin note EN";
        var dialog = new AffectedItemsPreviewDialog(
            string.Empty, 1, new List<string> { "Guide.pdf" }, string.Empty, loc,
            titleSource, PreviewTextSource.Key("PV_RecycleBinNote"));

        try
        {
            dialog.Show();
            Flush();

            Assert.Equal("Delete targets: 'Math101'", dialog.Title);

            loc.Strings["PV_CascadeTitle"] = "削除対象: {0}";
            loc.SwitchTo(SupportedLanguage.Japanese);
            Flush();

            Assert.NotEqual("Delete targets: 'Math101'", dialog.Title);
            Assert.Equal("削除対象: 'Math101'", dialog.Title);
        }
        finally
        {
            dialog.Close();
        }
    }

    private sealed class MutableLocalizationStub : ILocalizationService
    {
        private SupportedLanguage _current = SupportedLanguage.English;

        public Dictionary<string, string> Strings { get; } =
            new(System.StringComparer.Ordinal);

        public string this[string key] =>
            Strings.TryGetValue(key, out var value) ? value : $"[{key}]";

        public SupportedLanguage CurrentLanguage => _current;

        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } =
            new[] { SupportedLanguage.Japanese, SupportedLanguage.English };

        public event EventHandler? LanguageChanged;

        public void SetLanguage(SupportedLanguage language) => _current = language;

        public void SwitchTo(SupportedLanguage language)
        {
            _current = language;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

file sealed class PreviewCapturingCustomDialogs(List<PreviewTextSource> capturedTitles) : ICustomDialogService
{
    public Task<bool> ShowAffectedItemsPreviewAsync(int totalCount, IReadOnlyList<string> itemNames, PreviewTextSource title, PreviewTextSource reversibilityNote)
    {
        capturedTitles.Add(title);
        return Task.FromResult(false);
    }

    public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
        => Task.FromResult<string?>(null);

    public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections)
        => Task.FromResult(-1);

    public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection)
        => Task.FromResult<List<StudyDocument>?>(null);

    public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types)
        => Task.FromResult<AddDocumentDraft?>(null);
}

file sealed class CallerPathDocuments(List<StudyDocument> documents) : IDocumentRepository
{
    public List<StudyDocument> GetAll() => documents;
    public StudyDocument? GetById(int id) => null;
    public List<StudyDocument> Search(string keyword) => [];
    public List<StudyDocument> Filter(string subject, string type) => [];
    public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
    public bool Add(StudyDocument document) => true;
    public bool AddWithCatalogs(StudyDocument document) => true;
    public bool Update(StudyDocument document) => true;
    public bool Delete(int id) => true;
    public List<string> GetDistinctSubjects() => [];
    public List<string> GetDistinctTypes() => [];
    public List<string> GetDistinctTags() => [];
    public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
    public List<StudyDocument> GetOverdueDocuments() => [];
    public void EnsureSubjectExists(string subject) { }
    public void EnsureTypeExists(string type) { }
}

file sealed class CallerPathCategories : ICategoryRepository
{
    public List<string> GetAllSubjects() => [];
    public List<string> GetAllTypes() => [];
    public List<(string Name, int Count)> GetSubjectsWithCount() => [("Math101", 1)];
    public List<(string Name, int Count)> GetTypesWithCount() => [];
    public bool AddSubject(string name) => false;
    public bool AddType(string name) => false;
    public bool UpdateSubjectName(string oldName, string newName) => false;
    public bool UpdateTypeName(string oldName, string newName) => false;
    public bool DeleteDocumentsBySubject(string subjectName) => true;
    public bool DeleteDocumentsByType(string typeName) => true;
    public int GetTotalDocumentCount() => 2;
}

file sealed class NoOpDialogs : IDialogService
{
    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
    public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
}
