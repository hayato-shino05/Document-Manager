using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class CategoryManagementSelectionGapTests : DatabaseTestBase
{
    [Fact]
    public async Task DeleteSubjects_MultiSelection_SoftDeletesAllAndShowsCountedSuccess()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });
        Repo.Add(new StudyDocument { Name = "A2", Subject = "Math101" });
        Repo.Add(new StudyDocument { Name = "B1", Subject = "Physics101" });

        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        var math = model.Subjects.First(s => s.Name == "Math101");
        var physics = model.Subjects.First(s => s.Name == "Physics101");
        model.SelectedSubjects = new List<CategoryItem> { math, physics };

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.DoesNotContain(Repo.GetAll(), d => d.Subject == "Math101" || d.Subject == "Physics101");
        Assert.Equal(3, Db.GetDeletedDocuments().Count);
        Assert.Equal(1, dialog.ConfirmCalls);
        Assert.Contains("2", dialog.LastConfirmMessage);
        Assert.Equal("Deleted 2 subjects", dialog.LastMessage);
        Assert.Empty(model.SelectedSubjects);
        Assert.DoesNotContain(model.Subjects, s => s.Name == "Math101" || s.Name == "Physics101");
    }

    [Fact]
    public async Task DeleteSubject_SingleSelection_DeletesOnlyThatSubject()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });
        Repo.Add(new StudyDocument { Name = "B1", Subject = "Physics101" });

        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        model.SelectedSubjects = new List<CategoryItem> { model.Subjects.First(s => s.Name == "Math101") };

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        var active = Repo.GetAll();
        Assert.DoesNotContain(active, d => d.Subject == "Math101");
        Assert.Single(active, d => d.Subject == "Physics101");
        Assert.Single(Db.GetDeletedDocuments(), d => d.Subject == "Math101");
        Assert.Equal("Deleted Math101", dialog.LastMessage);
        Assert.Empty(model.SelectedSubjects);
        Assert.Contains(model.Subjects, s => s.Name == "Physics101");
        Assert.DoesNotContain(model.Subjects, s => s.Name == "Math101");
    }

    [Fact]
    public async Task DeleteSubject_NoSelection_IsNoOp()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });

        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.ConfirmCalls);
        Assert.Equal(0, dialog.MessageCalls);
        Assert.Equal(0, dialog.ErrorCalls);
        Assert.Single(Repo.GetAll());
        Assert.Empty(Db.GetDeletedDocuments());
    }

    [Fact]
    public async Task DeleteSubject_EmptyMultiWithSingleFallback_DeletesFallbackSubject()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });
        Repo.Add(new StudyDocument { Name = "B1", Subject = "Physics101" });

        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "Math101");

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        var active = Repo.GetAll();
        Assert.DoesNotContain(active, d => d.Subject == "Math101");
        Assert.Single(active, d => d.Subject == "Physics101");
        Assert.Single(Db.GetDeletedDocuments(), d => d.Subject == "Math101");
        Assert.Equal("Deleted Math101", dialog.LastMessage);
        Assert.Empty(model.SelectedSubjects);
    }

    [Fact]
    public async Task DeleteTypes_MultiSelection_SoftDeletesAllAndShowsCountedSuccess()
    {
        Repo.Add(new StudyDocument { Name = "P1", Subject = "Math101", Type = "TypeA" });
        Repo.Add(new StudyDocument { Name = "P2", Subject = "Math101", Type = "TypeA" });
        Repo.Add(new StudyDocument { Name = "W1", Subject = "Physics101", Type = "TypeB" });

        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        var typeA = model.Types.First(t => t.Name == "TypeA");
        var typeB = model.Types.First(t => t.Name == "TypeB");
        model.SelectedTypes = new List<CategoryItem> { typeA, typeB };

        await model.DeleteTypeCommand.ExecuteAsync(null);

        Assert.DoesNotContain(Repo.GetAll(), d => d.Type == "TypeA" || d.Type == "TypeB");
        Assert.Equal(3, Db.GetDeletedDocuments().Count);
        Assert.Equal(1, dialog.ConfirmCalls);
        Assert.Contains("2", dialog.LastConfirmMessage);
        Assert.Equal("Deleted 2 types", dialog.LastMessage);
        Assert.Empty(model.SelectedTypes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Math101")]
    public async Task RenameSubject_NoOpInputs_DoNotTouchRepository(string? input)
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });

        var dialog = new SelectionDialogService { InputResult = input };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "Math101");

        await model.RenameSubjectCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.InputCalls);
        Assert.Equal(0, dialog.MessageCalls);
        Assert.Equal(0, dialog.ErrorCalls);
        Assert.Contains("Math101", Db.GetSubjectsWithCount().Select(subject => subject.Name));
        Assert.Single(Repo.GetAll(), d => d.Subject == "Math101");
        Assert.Contains(model.Subjects, s => s.Name == "Math101");
    }

    [Theory]
    [InlineData("Math101")]
    [InlineData("math101")]
    public async Task AddSubject_DuplicateName_ShowsAlreadyExistsWithoutAdding(string input)
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });

        var dialog = new SelectionDialogService { InputResult = input };
        var model = CreateModel(dialog);
        var beforeCount = model.Subjects.Count;

        await model.AddSubjectCommand.ExecuteAsync(null);

        Assert.Equal("Already exists: " + input.Trim(), dialog.LastMessage);
        Assert.Equal(0, dialog.ErrorCalls);
        Assert.Equal(beforeCount, model.Subjects.Count);
        Assert.Equal(1, Db.GetSubjectsWithCount().Count(s => s.Name.Equals("Math101", StringComparison.OrdinalIgnoreCase)));
    }

    private CategoryManagementModel CreateModel(SelectionDialogService dialog)
        => new(Repo, new CategoryRepository(Db), dialog, new CategorySelectionLocalizationService());
}

public sealed class SelectionDialogService : IDialogService
{
    public bool ConfirmResult { get; set; }
    public string? InputResult { get; set; }
    public int ConfirmCalls { get; private set; }
    public int MessageCalls { get; private set; }
    public int ErrorCalls { get; private set; }
    public int InputCalls { get; private set; }
    public string? LastConfirmMessage { get; private set; }
    public string? LastMessage { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public Task ShowMessageAsync(string title, string message)
    {
        MessageCalls++;
        LastMessage = message;
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        ErrorCalls++;
        LastErrorMessage = message;
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string title, string message)
    {
        ConfirmCalls++;
        LastConfirmMessage = message;
        return Task.FromResult(ConfirmResult);
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
    {
        ConfirmCalls++;
        LastConfirmMessage = message;
        return Task.FromResult(ConfirmResult);
    }

    public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
    {
        InputCalls++;
        return Task.FromResult(InputResult);
    }
}

public sealed class CategorySelectionLocalizationService : ILocalizationService
{
    public string this[string key] => key switch
    {
        "Category_SelectedCount" => "Selected {0} items",
        "Category_DeleteConfirmMsg" => "{0}",
        "Category_DeleteWithDocsMsg" => "{0} ({1} docs)",
        "Category_DeletedSubject" => "Deleted {0}",
        "Category_DeletedSubjects" => "Deleted {0} subjects",
        "Category_DeletedType" => "Deleted {0}",
        "Category_DeletedTypes" => "Deleted {0} types",
        "Category_AlreadyExists" => "Already exists: {0}",
        _ => key
    };

    public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
    public event EventHandler? LanguageChanged { add { } remove { } }
    public void SetLanguage(SupportedLanguage language) { }
}