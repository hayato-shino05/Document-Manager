using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DesktopQualityBatch4Tests
{
    private sealed class FakeRecycleBinRepository : IRecycleBinRepository
    {
        public List<StudyDocument> DeletedDocs { get; } = new();

        public List<StudyDocument> GetDeletedDocuments() => DeletedDocs.ToList();
        public int GetDeletedDocumentCount() => DeletedDocs.Count;

        public bool RestoreDocument(int id)
        {
            var doc = DeletedDocs.FirstOrDefault(d => d.Id == id);
            if (doc == null) return false;
            DeletedDocs.Remove(doc);
            return true;
        }

        public bool PermanentDeleteDocument(int id)
        {
            var doc = DeletedDocs.FirstOrDefault(d => d.Id == id);
            if (doc == null) return false;
            DeletedDocs.Remove(doc);
            return true;
        }

        public int EmptyRecycleBin()
        {
            int count = DeletedDocs.Count;
            DeletedDocs.Clear();
            return count;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool ConfirmResponse { get; set; } = true;
        public string? LastMessage { get; private set; }

        public Task ShowMessageAsync(string title, string message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(ConfirmResponse);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(ConfirmResponse);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    [Fact]
    public void RecycleBinModel_SelectAllAndDeselectAll_UpdatesSelectionState()
    {
        var repo = new FakeRecycleBinRepository();
        repo.DeletedDocs.Add(new StudyDocument { Id = 1, Name = "Doc1" });
        repo.DeletedDocs.Add(new StudyDocument { Id = 2, Name = "Doc2" });
        repo.DeletedDocs.Add(new StudyDocument { Id = 3, Name = "Doc3" });

        var dialog = new FakeDialogService();
        var loc = new FakeLocalizationService();
        var model = new RecycleBinModel(repo, dialog, loc);

        Assert.Equal(3, model.DeletedItems.Count);
        Assert.False(model.HasCheckedItems);
        Assert.Equal(0, model.SelectedCount);

        model.SelectAll();
        Assert.True(model.HasCheckedItems);
        Assert.Equal(3, model.SelectedCount);
        Assert.True(model.HasSelection);

        model.DeselectAll();
        Assert.False(model.HasCheckedItems);
        Assert.Equal(0, model.SelectedCount);
    }

    [Fact]
    public async Task RecycleBinModel_RestoreSelected_RestoresAllCheckedDocuments()
    {
        var repo = new FakeRecycleBinRepository();
        repo.DeletedDocs.Add(new StudyDocument { Id = 1, Name = "Doc1" });
        repo.DeletedDocs.Add(new StudyDocument { Id = 2, Name = "Doc2" });
        repo.DeletedDocs.Add(new StudyDocument { Id = 3, Name = "Doc3" });

        var dialog = new FakeDialogService();
        var loc = new FakeLocalizationService();
        var model = new RecycleBinModel(repo, dialog, loc);

        // Check Doc1 and Doc3
        model.DeletedItems[0].IsSelected = true;
        model.DeletedItems[2].IsSelected = true;

        await model.RestoreCommand.ExecuteAsync(null);

        Assert.Single(repo.DeletedDocs);
        Assert.Equal(2, repo.DeletedDocs[0].Id);
        Assert.Single(model.DeletedItems);
        Assert.Equal(2, model.DeletedItems[0].Document.Id);
    }

    [Fact]
    public async Task RecycleBinModel_PermanentDeleteSelected_DeletesAllCheckedDocuments()
    {
        var repo = new FakeRecycleBinRepository();
        repo.DeletedDocs.Add(new StudyDocument { Id = 1, Name = "Doc1" });
        repo.DeletedDocs.Add(new StudyDocument { Id = 2, Name = "Doc2" });

        var dialog = new FakeDialogService();
        var loc = new FakeLocalizationService();
        var model = new RecycleBinModel(repo, dialog, loc);

        model.SelectAll();

        await model.PermanentDeleteCommand.ExecuteAsync(null);

        Assert.Empty(repo.DeletedDocs);
        Assert.Empty(model.DeletedItems);
        Assert.False(model.HasDeletedDocuments);
    }

    [Fact]
    public void RecycleBinView_HasBatchSelectionAndCheckboxColumn()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "RecycleBin.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"RecycleBin_SelectAll\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"RecycleBin_DeselectAll\"", xaml);
        Assert.Contains("DataGridCheckBoxColumn Header=\"✓\"", xaml);
    }

    [Fact]
    public void CategoryManagementView_HasEmptyStatesForSubjectsAndTypes()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "CategoryManagement.axaml"));
        Assert.Contains("IsVisible=\"{Binding !Subjects.Count}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding !Types.Count}\"", xaml);
    }

    [Fact]
    public void CollectionManagementView_HasEmptyStatesForCollectionsAndItems()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "CollectionManagement.axaml"));
        Assert.Contains("IsVisible=\"{Binding !Collections.Count}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding !DocumentsInCollection.Count}\"", xaml);
    }
}
