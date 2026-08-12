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
        => Assert.Equal(
            SupportedLanguage.English,
            SupportedLanguageResolver.Resolve("English", new CultureInfo("ja-JP")));

    [Fact]
    public void Resolve_UsesOsCultureWhenLanguageIsMissing()
        => Assert.Equal(
            SupportedLanguage.Vietnamese,
            SupportedLanguageResolver.Resolve(null, new CultureInfo("vi-VN")));

    [Fact]
    public void Resolve_UsesOsCultureWhenSavedLanguageIsInvalid()
        => Assert.Equal(
            SupportedLanguage.Chinese,
            SupportedLanguageResolver.Resolve("unsupported", new CultureInfo("zh-CN")));
}
