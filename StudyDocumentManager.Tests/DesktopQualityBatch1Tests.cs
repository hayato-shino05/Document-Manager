using System;
using System.IO;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DesktopQualityBatch1Tests
{
    [Fact]
    public void ColorTokens_DefinesAllSemanticBrushesReferencedByOfficeWorkspace()
    {
        var colorTokens = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Themes", "ColorTokens.axaml"));

        Assert.Contains("x:Key=\"DangerText\"", colorTokens);
        Assert.Contains("x:Key=\"DangerBorder\"", colorTokens);
        Assert.Contains("x:Key=\"WarningText\"", colorTokens);
        Assert.Contains("x:Key=\"WarningBorder\"", colorTokens);
        Assert.Contains("x:Key=\"SuccessText\"", colorTokens);
        Assert.Contains("x:Key=\"SuccessBorder\"", colorTokens);
        Assert.Contains("x:Key=\"Accent\"", colorTokens);
    }

    [Fact]
    public void MainWindow_MenuFile_HasHotKeysMatchingInputGestures()
    {
        var mainWindow = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "MainWindow.axaml"));

        Assert.Contains("InputGesture=\"Ctrl+N\" HotKey=\"Ctrl+N\"", mainWindow);
        Assert.Contains("InputGesture=\"Ctrl+O\" HotKey=\"Ctrl+O\"", mainWindow);
        Assert.Contains("InputGesture=\"Ctrl+U\" HotKey=\"Ctrl+U\"", mainWindow);
        Assert.Contains("InputGesture=\"Delete\" HotKey=\"Delete\"", mainWindow);
        Assert.Contains("InputGesture=\"Ctrl+E\" HotKey=\"Ctrl+E\"", mainWindow);
        Assert.Contains("InputGesture=\"Ctrl+Shift+I\" HotKey=\"Ctrl+Shift+I\"", mainWindow);
        Assert.Contains("InputGesture=\"F5\" HotKey=\"F5\"", mainWindow);
        Assert.Contains("InputGesture=\"Ctrl+M\" HotKey=\"Ctrl+M\"", mainWindow);
    }

    [Fact]
    public void OnboardingDialog_SkipButton_HasIsCancel()
    {
        var onboarding = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "OnboardingDialog.axaml"));

        Assert.Contains("Name=\"SkipButton\"", onboarding);
        Assert.Contains("IsCancel=\"True\"", onboarding);
    }

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
