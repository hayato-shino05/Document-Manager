using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SelectCollectionDialogStateTests
{
    private sealed class StubLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _strings = new()
        {
            ["SelectCollection_None"] = "(none selected)",
            ["SelectCollection_Selected"] = "Selected: {0}"
        };

        public string this[string key] => _strings.TryGetValue(key, out var v) ? v : key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.English;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.English];
        public event EventHandler? LanguageChanged;
        public void SetLanguage(SupportedLanguage language) { }
    }

    [Fact]
    public void Select_UpdatesSelectedIdLabelAndConfirmState()
    {
        var loc = new StubLocalizationService();
        var state = new SelectCollectionDialogState(
        [
            (1, "Math", 3),
            (2, "Report", 0)
        ], loc);

        state.Select(2);

        Assert.Equal(2, state.SelectedId);
        Assert.Equal("Selected: Report", state.SelectedLabel);
        Assert.True(state.CanConfirm);
    }

    [Fact]
    public void BuildChipLabel_AppendsCountOnlyWhenPositive()
    {
        Assert.Equal("Math  (3)", SelectCollectionDialogState.BuildChipLabel("Math", 3));
        Assert.Equal("Report", SelectCollectionDialogState.BuildChipLabel("Report", 0));
    }
}
