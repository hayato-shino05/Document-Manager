using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
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
