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

        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"recycle\""));
        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"bulk-delete\""));
        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"recentfiles\""));
        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"duplicates\""));
        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"report\""));
        Assert.Equal(1, Count(mainWindow, "CommandParameter=\"treemap\""));
        Assert.Equal(1, Count(mainWindow, "Command=\"{Binding BackupDatabaseCommand}\""));
    }

    private static string LoadMainWindow()
        => File.ReadAllText(@"D:\Github-Project\study-document-manager\StudyDocumentManager\Views\MainWindow.axaml");

    private static int Count(string value, string fragment)
        => (value.Length - value.Replace(fragment, string.Empty, StringComparison.Ordinal).Length) / fragment.Length;
}
