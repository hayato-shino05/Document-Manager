using System.Text.RegularExpressions;
using System.Xml.Linq;
using StudyDocumentManager.Core;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class LocalizationResourceIntegrityTests
{
    [Theory]
    [InlineData("Strings.vi.resx", "Collection_CreateTitle", "Tạo bộ sưu tập")]
    [InlineData("Strings.vi.resx", "Report_NoData", "Không có dữ liệu")]
    [InlineData("Strings.zh.resx", "Collection_CreateTitle", "创建收藏集")]
    [InlineData("Strings.zh.resx", "Report_NoData", "没有可用数据")]
    public void LocalizedResourceFiles_KeepExpectedDecodedStrings(string fileName, string key, string expected)
    {
        var resources = LoadResources(fileName);

        Assert.True(resources.TryGetValue(key, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.en.resx")]
    public void DefaultAndEnglishResources_ContainSlice4BDialogKeys(string fileName)
    {
        var resources = LoadResources(fileName);

        Assert.Contains("AddToCollection_Subtitle", resources.Keys);
        Assert.Contains("AddToCollection_SearchPlaceholder", resources.Keys);
        Assert.Contains("Bulk_BtnSelectAll", resources.Keys);
        Assert.Contains("Bulk_Selected", resources.Keys);
        Assert.Contains("Bulk_Documents", resources.Keys);
    }

    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.vi.resx")]
    [InlineData("Strings.zh.resx")]
    public void ResourceFiles_ContainI18nRepairKeys(string fileName)
    {
        var resources = LoadResources(fileName);

        Assert.Contains("MainWindow_Title", resources.Keys);
        Assert.Contains("AddEdit_PlaceholderTags", resources.Keys);
        Assert.Contains("ChangeCategory_DocumentLabel", resources.Keys);
        Assert.Contains("ChangeCategory_ChipHint", resources.Keys);
        Assert.Contains("ChangeCategory_EmptyState", resources.Keys);
        Assert.Contains("ChangeCategory_InputHint", resources.Keys);
        Assert.Contains("ChangeCategory_Placeholder", resources.Keys);
        Assert.Contains("ChangeCategory_BtnSave", resources.Keys);
        Assert.Contains("AddDocDialog_Title", resources.Keys);
        Assert.Contains("RelatedDocs_RelationType_related", resources.Keys);
        Assert.Contains("RelatedDocs_RelationType_reference", resources.Keys);
        Assert.Contains("RelatedDocs_RelationType_supplement", resources.Keys);
        Assert.Contains("RelatedDocs_RelationType_prerequisite", resources.Keys);
        Assert.Contains("RelatedDocs_RelationType_sequel", resources.Keys);
    }

    [Theory]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.vi.resx")]
    [InlineData("Strings.zh.resx")]
    public void LocalizedResourceFiles_ContainEveryCanonicalKeyWithNonEmptyValue(string fileName)
    {
        var canonical = LoadResources("Strings.resx");
        var localized = LoadResources(fileName);

        Assert.Equal(canonical.Keys.Order(), localized.Keys.Order());
        foreach (var (key, canonicalValue) in canonical)
        {
            var value = localized[key];
            Assert.False(string.IsNullOrWhiteSpace(value), $"{fileName} has an empty value for {key}");
            Assert.Equal(GetFormatItems(canonicalValue), GetFormatItems(value));
        }
    }

    [Fact]
    public void EnglishResourceFile_DoesNotContainCjkFallbackText()
    {
        var resources = LoadResources("Strings.en.resx");

        Assert.DoesNotContain(
            resources,
            pair => !pair.Key.StartsWith("Lang_", StringComparison.Ordinal)
                && Regex.IsMatch(pair.Value, "[\\p{IsCJKUnifiedIdeographs}\\p{IsHiragana}\\p{IsKatakana}]"));
    }

    [Theory]
    [InlineData(SupportedLanguage.English, "Strings.en.resx")]
    [InlineData(SupportedLanguage.Vietnamese, "Strings.vi.resx")]
    [InlineData(SupportedLanguage.Chinese, "Strings.zh.resx")]
    public void LocalizationService_ResolvesEveryLocalizedResourceValue(SupportedLanguage language, string fileName)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        var originalUiCulture = System.Globalization.CultureInfo.CurrentUICulture;

        try
        {
            var expected = LoadResources(fileName);
            var localization = new LocalizationService();
            localization.SetLanguage(language);

            foreach (var (key, value) in expected)
                Assert.Equal(NormalizeLineEndings(value), NormalizeLineEndings(localization[key]));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
            System.Globalization.CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void LocalizationService_UpdatesThreadCultureForDatePickerLocalization()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        var originalUiCulture = System.Globalization.CultureInfo.CurrentUICulture;

        try
        {
            var localization = new LocalizationService();

            Assert.Equal("ja-JP", System.Globalization.CultureInfo.CurrentCulture.Name);
            Assert.Equal("ja-JP", System.Globalization.CultureInfo.CurrentUICulture.Name);

            localization.SetLanguage(SupportedLanguage.English);

            Assert.Equal("en", System.Globalization.CultureInfo.CurrentCulture.Name);
            Assert.Equal("en", System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
            System.Globalization.CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.vi.resx")]
    [InlineData("Strings.zh.resx")]
    public void ResourceFiles_ContainEveryKnownRuntimeKey(string fileName)
    {
        var resources = LoadResources(fileName);
        var runtimeKeys = new[]
        {
            "BatchImport_BtnBrowse",
            "BatchImport_BtnScan",
            "BatchImport_ColPath",
            "BatchImport_ColSize",
            "BatchImport_LblDefaultSubject",
            "BatchImport_LblFolder",
            "Bulk_BtnDeselectAll",
            "BulkDelete_ColDateAdded",
            "Menu_ChangeLanguage",
            "Search_BtnSearch",
            "Search_Label"
        };

        foreach (var key in runtimeKeys)
        {
            Assert.True(resources.TryGetValue(key, out var value), $"{fileName} is missing {key}");
            Assert.False(string.IsNullOrWhiteSpace(value), $"{fileName} has an empty value for {key}");
        }
    }

    [Fact]
    public void FileTypeResourceValues_UseExpectedJapaneseAndVietnameseLabels()
    {
        var japanese = LoadResources("Strings.resx");
        var vietnamese = LoadResources("Strings.vi.resx");

        Assert.Equal("画像", japanese["FileType_Image"]);
        Assert.Equal("動画", japanese["FileType_Video"]);
        Assert.Equal("音声", japanese["FileType_Audio"]);
        Assert.Equal("文書", japanese["FileType_Document"]);
        Assert.Equal("Hình ảnh", vietnamese["FileType_Image"]);
        Assert.Equal("Video", vietnamese["FileType_Video"]);
        Assert.Equal("Âm thanh", vietnamese["FileType_Audio"]);
        Assert.Equal("Tài liệu", vietnamese["FileType_Document"]);
    }

    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.en.resx")]
    [InlineData("Strings.vi.resx")]
    [InlineData("Strings.zh.resx")]
    public void AboutText_UsesTheSameApplicationIntroductionAcrossEntryPoints(string fileName)
    {
        var resources = LoadResources(fileName);

        Assert.Equal(resources["Dashboard_About"], resources["Main_About"]);
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n");

    private static string[] GetFormatItems(string value)
        => Regex.Matches(value, "\\{(\\d+)(?:[^}]*)\\}")
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static Dictionary<string, string> LoadResources(string fileName)
    {
        var path = GetResourceFilePath(fileName);
        var document = XDocument.Load(path);

        return document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string GetResourceFilePath(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudyDocumentManager.sln")))
            {
                return Path.Combine(directory.FullName, "StudyDocumentManager", "Resources", fileName);
            }
        }

        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
