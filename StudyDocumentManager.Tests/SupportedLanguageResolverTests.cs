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
