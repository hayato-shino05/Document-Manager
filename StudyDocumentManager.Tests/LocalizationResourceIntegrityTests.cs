using System.Xml.Linq;
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
