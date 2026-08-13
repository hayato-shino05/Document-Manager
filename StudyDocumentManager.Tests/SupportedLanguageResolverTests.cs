using System.Globalization;
using StudyDocumentManager.Core;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class SupportedLanguageResolverTests
{
    [Theory]
    [InlineData("ja-JP", SupportedLanguage.Japanese)]
    [InlineData("en-US", SupportedLanguage.English)]
    [InlineData("vi-VN", SupportedLanguage.Vietnamese)]
    [InlineData("zh-CN", SupportedLanguage.Chinese)]
    [InlineData("fr-FR", SupportedLanguage.Japanese)]
    public void FromCulture_ReturnsExpectedLanguage(string cultureName, SupportedLanguage expected)
        => Assert.Equal(expected, SupportedLanguageResolver.FromCulture(new CultureInfo(cultureName)));

    [Fact]
    public void FromCulture_NullFallsBackToJapanese()
        => Assert.Equal(SupportedLanguage.Japanese, SupportedLanguageResolver.FromCulture(null));

    [Fact]
    public void FromCulture_HandlesNeutralCulture()
        => Assert.Equal(SupportedLanguage.English, SupportedLanguageResolver.FromCulture(new CultureInfo("en")));

    [Fact]
    public void Resolve_PrefersSavedLanguage()
    {
        var resolution = SupportedLanguageResolver.Resolve("English", new CultureInfo("ja-JP"));

        Assert.Equal(SupportedLanguage.English, resolution.Language);
        Assert.True(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void TryClaimInstallerLanguage_DeletesHandoffOnlyAfterCompletion()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        var consumingPath = filePath + ".consuming";
        File.WriteAllText(filePath, "[Installer]\nLanguage=Vietnamese\n");

        try
        {
            using var handoff = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData);

            Assert.NotNull(handoff);
            Assert.Equal(nameof(SupportedLanguage.Vietnamese), handoff.Language);
            Assert.False(File.Exists(filePath));
            Assert.True(File.Exists(consumingPath));

            handoff.Complete();
            Assert.False(File.Exists(consumingPath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void TryClaimInstallerLanguage_RestoresHandoffWhenNotCompleted()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        File.WriteAllText(filePath, "[Installer]\nLanguage=Vietnamese\n");

        try
        {
            using (var handoff = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData))
                Assert.NotNull(handoff);

            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void ReadInstallerLanguage_RestoresMalformedHandoffFile()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        File.WriteAllText(filePath, "[Installer]\nOther=Value\n");

        try
        {
            Assert.Null(SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void ReadInstallerLanguage_RestoresUnsupportedLanguageFile()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        File.WriteAllText(filePath, "[Installer]\nLanguage=Unsupported\n");

        try
        {
            Assert.Null(SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void ReadInstallerLanguage_RecoversStaleConsumingFile()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        var consumingPath = filePath + ".consuming";
        File.WriteAllText(consumingPath, "[Installer]\nLanguage=English\n");

        try
        {
            using var handoff = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData);

            Assert.NotNull(handoff);
            Assert.Equal(nameof(SupportedLanguage.English), handoff.Language);
            handoff.Complete();
            Assert.False(File.Exists(filePath));
            Assert.False(File.Exists(consumingPath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void ReadInstallerLanguage_PrefersFreshHandoffOverStaleClaim()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        var consumingPath = filePath + ".consuming";
        File.WriteAllText(filePath, "[Installer]\nLanguage=Vietnamese\n");
        File.WriteAllText(consumingPath, "[Installer]\nLanguage=English\n");

        try
        {
            using var handoff = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData);

            Assert.NotNull(handoff);
            Assert.Equal(nameof(SupportedLanguage.Vietnamese), handoff.Language);
            handoff.Complete();
            Assert.False(File.Exists(filePath));
            Assert.False(File.Exists(consumingPath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void TryClaimInstallerLanguage_DoesNotClaimWhileAnotherClaimIsActive()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        File.WriteAllText(filePath, "[Installer]\nLanguage=English\n");

        try
        {
            using var first = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData);
            SupportedLanguageResolver.InstallerLanguageHandoff? second = null;
            var thread = new Thread(() => second = SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData));

            Assert.NotNull(first);
            thread.Start();
            thread.Join();
            Assert.Null(second);
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void ReadInstallerLanguage_IgnoresLanguageKeyOutsideInstallerSection()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"sdm-language-{Guid.NewGuid():N}");
        var directory = Path.Combine(localAppData, "StudyDocumentManager");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "installer-language.ini");
        File.WriteAllText(filePath, "[Installer]\nOther=Value\n[Other]\nLanguage=English\n");

        try
        {
            Assert.Null(SupportedLanguageResolver.TryClaimInstallerLanguage(localAppData));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(localAppData))
                Directory.Delete(localAppData, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesInstallerLanguageBeforeOsCulture()
    {
        var resolution = SupportedLanguageResolver.Resolve(null, nameof(SupportedLanguage.English), new CultureInfo("vi-VN"));

        Assert.Equal(SupportedLanguage.English, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_PrefersSavedLanguageBeforeInstallerLanguage()
    {
        var resolution = SupportedLanguageResolver.Resolve(nameof(SupportedLanguage.Chinese), nameof(SupportedLanguage.English), new CultureInfo("ja-JP"));

        Assert.Equal(SupportedLanguage.Chinese, resolution.Language);
        Assert.True(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_UsesOsCultureWhenLanguageIsMissing()
    {
        var resolution = SupportedLanguageResolver.Resolve(null, new CultureInfo("vi-VN"));

        Assert.Equal(SupportedLanguage.Vietnamese, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_UsesOsCultureWhenInstallerLanguageIsInvalid()
    {
        var resolution = SupportedLanguageResolver.Resolve(null, "unsupported", new CultureInfo("zh-CN"));

        Assert.Equal(SupportedLanguage.Chinese, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_FallsBackToJapaneseWhenSavedAndInstallerLanguagesAreInvalid()
    {
        var resolution = SupportedLanguageResolver.Resolve("unsupported", "also-unsupported", new CultureInfo("fr-FR"));

        Assert.Equal(SupportedLanguage.Japanese, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_UsesOsCultureWhenSavedLanguageIsInvalid()
    {
        var resolution = SupportedLanguageResolver.Resolve("unsupported", new CultureInfo("zh-CN"));

        Assert.Equal(SupportedLanguage.Chinese, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_UsesOsCultureWhenSavedLanguageIsNumeric()
    {
        var resolution = SupportedLanguageResolver.Resolve("3", new CultureInfo("en-US"));

        Assert.Equal(SupportedLanguage.English, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void Resolve_PrefersSavedLanguageWhenWhitespaceCanBeTrimmed()
    {
        var resolution = SupportedLanguageResolver.Resolve(" English ", new CultureInfo("ja-JP"));

        Assert.Equal(SupportedLanguage.English, resolution.Language);
        Assert.True(resolution.UsedSavedLanguage);
    }
}
