using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using StudyDocumentManager.Markup;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class Issue60ToolbarUiaNameTests
{
    [Fact]
    public void MainWindow_ToolbarButtons_DeclareLocalizedNameAndHelpText()
    {
        var mainWindow = LoadView("MainWindow.axaml");

        Assert.Contains("AutomationProperties.AutomationId=\"Toolbar_Language\"", mainWindow);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Menu_ChangeLanguage}\"", mainWindow);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_ChangeLanguage}\"", mainWindow);

        var toolbarStart = mainWindow.IndexOf("<!-- ═══ TOOLBAR", StringComparison.Ordinal);
        var statusBarStart = mainWindow.IndexOf("<!-- ═══ STATUS BAR", toolbarStart, StringComparison.Ordinal);
        Assert.True(toolbarStart >= 0);
        Assert.True(statusBarStart > toolbarStart);
        var toolbar = mainWindow[toolbarStart..statusBarStart];

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Toolbar_Add}\"", toolbar);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_AddNewDocument}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Toolbar_OpenFile}\"", toolbar);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_OpenDocument}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Toolbar_Export}\"", toolbar);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_ExportCsv}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Action_Refresh}\"", toolbar);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_RefreshList}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Action_Import}\"", toolbar);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Menu_BatchImport}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Menu_Statistics}\"", toolbar);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize TreeMap_Title}\"", toolbar);

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Menu_Undo}\"", toolbar);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Toolbar_GoBack}\"", toolbar);
    }

    [Fact]
    public void Dashboard_FilterAndQuickActionButtons_DeclareLocalizedNameAndHelpText()
    {
        var dashboard = LoadView("Dashboard.axaml");

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_Search\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnSearch}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipSearch}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_ToggleAdvancedFilter\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnAdvancedFilter}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipAdvancedFilter}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_ApplyFilter\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnApplyFilter}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipApplyFilter}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_ClearFilter\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnClearFilter}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipClearFilter}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickRefresh\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnRefresh}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipRefresh}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickUpcoming\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnUpcoming}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipUpcoming}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickOverdue\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnOverdue}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipOverdue}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickCopyPath\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_CtxCopyPath}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipCopyPath}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickOpenFolder\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Dashboard_BtnOpenFolder}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipOpenFolder}\"", dashboard);

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_QuickAbout\"", dashboard);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Menu_About}\"", dashboard);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize Dashboard_TipAbout}\"", dashboard);
    }

    [Fact]
    public void RelatedDocuments_HeaderAndListButtons_DeclareLocalizedName()
    {
        var related = LoadView("RelatedDocuments.axaml");

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize RelatedDocs_BtnBack}\"", related);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize RelatedDocs_BtnBack}\"", related);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize RelatedDocs_BtnAddLink}\"", related);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize RelatedDocs_BtnAddLink}\"", related);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize RelatedDocs_BtnRemove}\"", related);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize RelatedDocs_BtnRemove}\"", related);
    }

    [Fact]
    public void AffectedItemsPreviewDialog_ConfirmAndCancelExposeLocalizedName()
    {
        var dialog = LoadView("AffectedItemsPreviewDialog.axaml");

        Assert.Contains("AutomationProperties.Name=\"{loc:Localize Action_Delete}\"", dialog);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize BE_ConfirmApply}\"", dialog);
        Assert.Contains("AutomationProperties.Name=\"{loc:Localize BE_Cancel}\"", dialog);
        Assert.Contains("AutomationProperties.HelpText=\"{loc:Localize BE_Cancel}\"", dialog);
    }

    [AvaloniaFact]
    public void AffectedItemsPreviewDialog_RendersWithLocalizedNameAndHelpText()
    {
        Application.Current!.Resources["Loc"] = new LocalizationService();
        var dialog = new AffectedItemsPreviewDialog("Preview", 3, new List<string> { "a.pdf", "b.pdf", "c.pdf" }, "Note", new LocalizationService());
        try
        {
            dialog.Show();
            var confirmButton = dialog.GetVisualDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetAutomationId(b) == "AffectedPreview_Confirm");
            var cancelButton = dialog.GetVisualDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetAutomationId(b) == "AffectedPreview_Cancel");

            var confirmName = AutomationProperties.GetName(confirmButton);
            var confirmHelp = AutomationProperties.GetHelpText(confirmButton);
            Assert.False(string.IsNullOrWhiteSpace(confirmName));
            Assert.False(string.IsNullOrWhiteSpace(confirmHelp));
            Assert.DoesNotContain("StackPanel", confirmName);

            var cancelName = AutomationProperties.GetName(cancelButton);
            var cancelHelp = AutomationProperties.GetHelpText(cancelButton);
            Assert.False(string.IsNullOrWhiteSpace(cancelName));
            Assert.False(string.IsNullOrWhiteSpace(cancelHelp));
        }
        finally
        {
            dialog.Close();
        }
    }

    private static string LoadView(string fileName) =>
        File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", fileName));

    private static string GetSourceFilePath(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudyDocumentManager.sln")))
                return Path.Combine(directory.FullName, Path.Combine(pathSegments));
        }

        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
