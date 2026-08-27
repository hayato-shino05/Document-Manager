using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxViewTests
{
    private static T? FindByAutomationId<T>(Control root, string id) where T : Control
    {
        return root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void RefreshButton_ResolvesLocalizedText_NotMissingKeyPlaceholder()
    {
        var loc = App.Services!.GetRequiredService<ILocalizationService>();
        var repo = new FakeRepo();
        var model = new ImportInboxModel(repo, new FakeLauncher(), new FakeNav(), loc);
        var view = new ImportInbox { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var refresh = FindByAutomationId<Button>(view, "ImportInbox_Refresh");
        Assert.NotNull(refresh);
        Assert.NotNull(refresh!.Content);
        Assert.NotEqual("[Btn_Refresh]", refresh.Content);
        Assert.False(string.IsNullOrWhiteSpace(refresh.Content as string));

        window.Close();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ActionPanel_HiddenWhenEmpty_VisibleWhenItems()
    {
        var loc = App.Services!.GetRequiredService<ILocalizationService>();
        var repo = new FakeRepo();
        var model = new ImportInboxModel(repo, new FakeLauncher(), new FakeNav(), loc);
        var view = new ImportInbox { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = FindByAutomationId<StackPanel>(view, "ImportInbox_ActionPanel");
        Assert.NotNull(panel);
        Assert.False(panel!.IsVisible);

        repo.Items.Add(new ImportInboxItem { Id = 1, SourcePath = "x", DisplayName = "x", State = ImportInboxState.Pending });
        model.RefreshCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.IsVisible);

        window.Close();
    }

    private sealed class FakeRepo : IImportInboxRepository
    {
        public List<ImportInboxItem> Items { get; } = new();
        public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false) => includeProcessed ? Items : Items.Where(i => i.State != ImportInboxState.Processed).ToList();
        public ImportInboxItem? GetById(int id) => Items.FirstOrDefault(i => i.Id == id);
        public int Add(ImportInboxItem item) { item.Id = Items.Count + 1; Items.Add(item); return item.Id; }
        public bool Update(ImportInboxItem item) => true;
        public bool UpdateState(int id, ImportInboxState state, string? failureCode = null) { var it = GetById(id); if (it is not null) { it.State = state; it.FailureCode = failureCode; } return true; }
        public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes) => documentIds.Count;
    }

    private sealed class FakeLauncher : IProcessLauncherService { public void OpenFile(string p) { } public void RevealInExplorer(string p) { } public void OpenUrl(string u) { } }
    private sealed class FakeNav : INavigationService { public bool CanGoBack => true; public void NavigateTo(string v) { } public void NavigateTo(string v, object? p) { } public void GoBack() { } }
}
