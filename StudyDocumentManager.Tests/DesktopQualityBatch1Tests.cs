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
        var officeWorkspace = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "OfficeWorkspace.axaml"));

        var matches = System.Text.RegularExpressions.Regex.Matches(officeWorkspace, @"\{StaticResource\s+([A-Za-z0-9_]+)\}");
        var colorOrBrushTokens = matches
            .Select(m => m.Groups[1].Value)
            .Where(t => t.EndsWith("Text") || t.EndsWith("Border") || t.EndsWith("Brush") || t.EndsWith("Bg") || t.EndsWith("Background") || t == "Accent")
            .Distinct()
            .ToList();

        Assert.NotEmpty(colorOrBrushTokens);
        foreach (var token in colorOrBrushTokens)
        {
            Assert.Contains($"x:Key=\"{token}\"", colorTokens);
        }
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

        // Assert that the SkipButton element specifically contains IsCancel="True"
        var skipButtonStart = onboarding.IndexOf("Name=\"SkipButton\"", StringComparison.Ordinal);
        Assert.True(skipButtonStart >= 0);
        var skipButtonEnd = onboarding.IndexOf("/>", skipButtonStart, StringComparison.Ordinal);
        Assert.True(skipButtonEnd > skipButtonStart);
        var skipButtonXml = onboarding.Substring(skipButtonStart, skipButtonEnd - skipButtonStart);
        Assert.Contains("IsCancel=\"True\"", skipButtonXml);
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
