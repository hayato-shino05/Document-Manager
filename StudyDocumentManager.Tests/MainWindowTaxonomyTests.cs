using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class MainWindowTaxonomyTests
{
    [Fact]
    public void MainWindow_UsesHybridMenuHeaders()
    {
        var mainWindow = LoadMainWindow();

        Assert.Contains("Menu_File", mainWindow);
        Assert.Contains("Menu_Organize", mainWindow);
        Assert.Contains("Menu_Import", mainWindow);
        Assert.Contains("Menu_Maintenance", mainWindow);
        Assert.Contains("Menu_Analytics", mainWindow);
        Assert.Contains("Menu_Help", mainWindow);
        Assert.DoesNotContain("Header=\"{loc:Localize Menu_Tools}\"", mainWindow);
    }

    [Fact]
    public void MainWindow_KeepsLowFrequencyRoutesOutOfToolbar()
    {
        var mainWindow = LoadMainWindow();
        var toolbarStart = mainWindow.IndexOf("<!-- ═══ TOOLBAR", StringComparison.Ordinal);
        var statusBarStart = mainWindow.IndexOf("<!-- ═══ STATUS BAR", toolbarStart, StringComparison.Ordinal);

        Assert.True(toolbarStart >= 0);
        Assert.True(statusBarStart > toolbarStart);

        var toolbar = mainWindow[toolbarStart..statusBarStart];
        Assert.Contains("Command=\"{Binding NavigateCommand}\" CommandParameter=\"add\"", toolbar);
        Assert.Contains("Command=\"{Binding OpenFileCommand}\"", toolbar);
        Assert.Contains("Command=\"{Binding ExportCsvCommand}\"", toolbar);
        Assert.Contains("Command=\"{Binding RefreshCommand}\"", toolbar);
        Assert.Contains("CommandParameter=\"batch-import\"", toolbar);
        Assert.Contains("CommandParameter=\"report\"", toolbar);
        Assert.Contains("CommandParameter=\"treemap\"", toolbar);
        Assert.DoesNotContain("Toolbar_Edit", toolbar);
        Assert.DoesNotContain("Toolbar_Delete", toolbar);
    }

    [Fact]
    public void MainWindow_ExposesStableAutomationIdsForShellMenusAndToolbar()
    {
        var mainWindow = LoadMainWindow();
        var automationIds = new[]
        {
            "Shell_MainWindow",
            "Menu_File",
            "Menu_Organize",
            "Menu_Import",
            "Menu_Maintenance",
            "Menu_Analytics",
            "Menu_Help",
            "Toolbar_Add",
            "Toolbar_Open",
            "Toolbar_Export",
            "Toolbar_Refresh",
            "Toolbar_Import",
            "Toolbar_Report",
            "Toolbar_TreeMap",
            "Toolbar_Back",
            "MainShellRoot"
        };

        foreach (var automationId in automationIds)
            Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", mainWindow);

        var dashboard = File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", "Dashboard.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"Screen_Dashboard\"", dashboard);
        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_SearchInput\"", dashboard);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"MainShellRoot\"", dashboard);

        var titleIds = new[]
        {
            ("AddEdit.axaml", "Title_AddEdit"),
            ("BatchImport.axaml", "Title_BatchImport"),
            ("BulkDelete.axaml", "Title_BulkDelete"),
            ("CategoryManagement.axaml", "Title_CategoryManagement"),
            ("CollectionManagement.axaml", "Title_CollectionManagement"),
            ("DuplicateDetection.axaml", "Title_DuplicateDetection"),
            ("PersonalNote.axaml", "Title_PersonalNote"),
            ("RecentFiles.axaml", "Title_RecentFiles"),
            ("RecycleBin.axaml", "Title_RecycleBin"),
            ("Report.axaml", "Title_Report"),
            ("TreeMap.axaml", "Title_TreeMap")
        };

        foreach (var (fileName, titleId) in titleIds)
        {
            var view = File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", fileName));
            Assert.Contains($"AutomationProperties.AutomationId=\"{titleId}\"", view);
        }
    }

    [Fact]
    public void Dashboard_ContextMenuExposesAllDocumentActions()
    {
        var dashboard = File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", "Dashboard.axaml"));
        var automationIds = new[]
        {
            "Context_OpenFile",
            "Context_EditDocument",
            "Context_ChangeCategory",
            "Context_DeleteToTrash",
            "Context_CopyName",
            "Context_CopyPath",
            "Context_OpenFolder",
            "Context_EditTags",
            "Context_MarkImportant",
            "Context_PersonalNote",
            "Context_AddToCollection",
            "Context_RelatedDocuments"
        };

        foreach (var automationId in automationIds)
            Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", dashboard);
    }

    private static string LoadMainWindow()
        => File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", "MainWindow.axaml"));

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
