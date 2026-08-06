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
        var toolbarStart = mainWindow.IndexOf("Padding=\"6,3\"", StringComparison.Ordinal);
        var statusBarStart = mainWindow.IndexOf("<!-- ═══ STATUS BAR", toolbarStart, StringComparison.Ordinal);

        Assert.True(toolbarStart >= 0);
        Assert.True(statusBarStart > toolbarStart);

        var toolbar = mainWindow[toolbarStart..statusBarStart];
        Assert.DoesNotContain("CommandParameter=\"recycle\"", toolbar);
        Assert.DoesNotContain("CommandParameter=\"bulk-delete\"", toolbar);
        Assert.DoesNotContain("CommandParameter=\"recentfiles\"", toolbar);
        Assert.DoesNotContain("CommandParameter=\"duplicates\"", toolbar);
        Assert.DoesNotContain("CommandParameter=\"report\"", toolbar);
        Assert.DoesNotContain("CommandParameter=\"treemap\"", toolbar);
        Assert.DoesNotContain("Command=\"{Binding BackupDatabaseCommand}\"", toolbar);
        Assert.Contains("Command=\"{Binding RefreshCommand}\"", toolbar);
        Assert.Contains("CommandParameter=\"batch-import\"", toolbar);
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
