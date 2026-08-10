using Xunit;

namespace StudyDocumentManager.Tests;

public class IconResourceConsistencyTests
{
    [Fact]
    public void PrimarySaveViews_UseWhiteSaveIcon()
    {
        var addEdit = LoadView("AddEdit.axaml");
        var personalNote = LoadView("PersonalNote.axaml");

        Assert.Contains("IconSaveWhite", addEdit);
        Assert.Contains("IconSaveWhite", personalNote);
    }

    [Fact]
    public void BatchImport_SelectAll_UsesNeutralCheckIcon()
    {
        var batchImport = LoadView("BatchImport.axaml");

        Assert.Contains("IconCheckNeutral", batchImport);
    }

    [Fact]
    public void FileIntegrityCheck_UsesIntegrityIconResource()
    {
        var fileIntegrity = LoadView("FileIntegrityCheck.axaml");

        Assert.DoesNotContain("IconShield", fileIntegrity);
        Assert.Contains("IconIntegrity", fileIntegrity);
    }

    [Fact]
    public void MainWindow_ToolbarUsesSharedToolbarIconClassForAddAndBack()
    {
        var mainWindow = LoadView("MainWindow.axaml");

        Assert.Contains("<Image Source=\"{StaticResource IconAdd}\" Classes=\"toolbar-btn-icon\"/>", mainWindow);
        Assert.Contains("<Image Source=\"{StaticResource IconBack}\" Classes=\"toolbar-btn-icon\"/>", mainWindow);
    }

    [Fact]
    public void AppTheme_DefinesWhiteAndNeutralIconVariants()
    {
        var appTheme = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Themes", "AppTheme.axaml"));

        Assert.Contains("x:Key=\"IconTreeMap\"", appTheme);
        Assert.Contains("x:Key=\"IconSaveWhite\"", appTheme);
        Assert.Contains("x:Key=\"IconCheckNeutral\"", appTheme);
        Assert.Contains("x:Key=\"IconSearchWhite\"", appTheme);
        Assert.Contains("x:Key=\"IconScanWhite\"", appTheme);
        Assert.Contains("x:Key=\"IconImportWhite\"", appTheme);
    }

    [Fact]
    public void PrimaryScanAndImportActions_UseWhiteIcons()
    {
        var batchImport = LoadView("BatchImport.axaml");
        var duplicateDetection = LoadView("DuplicateDetection.axaml");
        var fileIntegrity = LoadView("FileIntegrityCheck.axaml");
        var bulkDelete = LoadView("BulkDelete.axaml");

        Assert.Contains("IconScanWhite", batchImport);
        Assert.Contains("IconImportWhite", batchImport);
        Assert.Contains("IconSearchWhite", duplicateDetection);
        Assert.Contains("IconSearchWhite", fileIntegrity);
        Assert.Contains("IconSearchWhite", bulkDelete);

        var dashboard = LoadView("Dashboard.axaml");
        Assert.Contains("IconSearchWhite", dashboard);
        Assert.DoesNotContain("Opacity=\"0.5\"", dashboard);
    }

    private static string LoadView(string fileName)
        => File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", fileName));

    private static string GetSourceFilePath(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudyDocumentManager.sln")))
                return Path.Combine(directory.FullName, Path.Combine(pathSegments));
        }

        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
