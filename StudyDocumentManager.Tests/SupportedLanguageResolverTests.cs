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
    public void Resolve_UsesOsCultureWhenLanguageIsMissing()
    {
        var resolution = SupportedLanguageResolver.Resolve(null, new CultureInfo("vi-VN"));

        Assert.Equal(SupportedLanguage.Vietnamese, resolution.Language);
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
