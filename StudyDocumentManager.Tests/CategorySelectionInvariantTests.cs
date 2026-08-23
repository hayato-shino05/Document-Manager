using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class CategorySelectionInvariantTests : DatabaseTestBase
{
    [Fact]
    public async Task Refresh_PreservesSelection_WhenSubjectStillExists()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "A" });
        var dialog = new SelectionDialogService { InputResult = "B" };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "A");

        await model.AddSubjectCommand.ExecuteAsync(null);

        Assert.NotNull(model.SelectedSubject);
        Assert.Equal("A", model.SelectedSubject!.Name);
        Assert.Same(model.Subjects.First(s => s.Name == "A"), model.SelectedSubject);
    }

    [Fact]
    public async Task Delete_ClearsSelection_WhenSubjectGone()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "A" });
        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "A");

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.DoesNotContain(model.Subjects, s => s.Name == "A");
        Assert.Null(model.SelectedSubject);
    }

    [Fact]
    public async Task Rename_ClearsOldNameSelection_ByPreserveByNameInvariant()
    {
        Repo.Add(new StudyDocument { Name = "A1", Subject = "Math101" });
        var dialog = new SelectionDialogService { InputResult = "Math102" };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "Math101");

        await model.RenameSubjectCommand.ExecuteAsync(null);

        Assert.Contains(model.Subjects, s => s.Name == "Math102");
        Assert.DoesNotContain(model.Subjects, s => s.Name == "Math101");
        Assert.Null(model.SelectedSubject);
    }

    [Fact]
    public async Task SubjectDeletion_DoesNotClearTypeSelection_WhenTypeStillExists()
    {
        Repo.Add(new StudyDocument { Name = "P1", Subject = "S1", Type = "T1" });
        Repo.Add(new StudyDocument { Name = "P2", Subject = "S2", Type = "T1" });
        var dialog = new SelectionDialogService { ConfirmResult = true };
        var model = CreateModel(dialog);
        model.SelectedSubject = model.Subjects.First(s => s.Name == "S1");
        model.SelectedType = model.Types.First(t => t.Name == "T1");

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.Null(model.SelectedSubject);
        Assert.NotNull(model.SelectedType);
        Assert.Equal("T1", model.SelectedType!.Name);
        Assert.Same(model.Types.First(t => t.Name == "T1"), model.SelectedType);
    }

    private CategoryManagementModel CreateModel(SelectionDialogService dialog)
        => new(Repo, new CategoryRepository(Db), dialog, new CategorySelectionLocalizationService());
}
