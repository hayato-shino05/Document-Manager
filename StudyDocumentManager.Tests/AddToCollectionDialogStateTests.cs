using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AddToCollectionDialogStateTests
{
    private sealed class StubLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _strings = new()
        {
            ["Collection_DocCount"] = "{0} documents",
            ["Collection_DocCountFiltered"] = "{0} / {1} documents",
            ["Collection_SelectAll"] = "Select All",
            ["Collection_DeselectAll"] = "Deselect All"
        };

        public string this[string key] => _strings.TryGetValue(key, out var v) ? v : key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.English;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.English];
        public event EventHandler? LanguageChanged;
        public void SetLanguage(SupportedLanguage language) { }
    }

    [Fact]
    public void ApplyFilter_UpdatesVisibleItemsAndCountText()
    {
        var loc = new StubLocalizationService();
        var items = new[]
        {
            new SelectableDocumentItem(new StudyDocument { Id = 1, Name = "Math Advanced", Subject = "Study", Type = "PDF" }),
            new SelectableDocumentItem(new StudyDocument { Id = 2, Name = "Financial Report", Subject = "Work", Type = "Excel" })
        };

        var state = new AddToCollectionDialogState(items, loc);

        state.ApplyFilter("Math");

        Assert.Single(state.VisibleItems);
        Assert.Equal("1 / 2 documents", state.CountText);
    }

    [Fact]
    public void ToggleSelectAllVisible_SelectsVisibleItemsAndUpdatesFooter()
    {
        var loc = new StubLocalizationService();
        var items = new[]
        {
            new SelectableDocumentItem(new StudyDocument { Id = 1, Name = "A" }),
            new SelectableDocumentItem(new StudyDocument { Id = 2, Name = "B" })
        };

        var state = new AddToCollectionDialogState(items, loc);
        state.ApplyFilter(string.Empty);

        state.ToggleSelectAllVisible();

        Assert.All(state.VisibleItems, item => Assert.True(item.IsSelected));
        Assert.Equal("2", state.SelectedCountText);
        Assert.True(state.CanConfirm);
        Assert.Equal("Deselect All", state.SelectAllButtonText);
        Assert.True(state.HeaderCheckState);
    }
}
