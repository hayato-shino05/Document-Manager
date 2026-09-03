using System;
using System.IO;
using System.Threading.Tasks;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class LinuxPlatformPolishTests
{
    [Fact]
    public void HaranoAjiFontCollection_ConstructsWithCorrectKey()
    {
        var collection = new HaranoAjiFontCollection();
        Assert.NotNull(collection.Key);
        Assert.Equal("fonts:HaranoAji", collection.Key.ToString());
    }

    [Fact]
    public void DialogService_GetLocalPath_NullItem_ReturnsNull()
    {
        var result = DialogService.GetLocalPath(null);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("/home/user/document.pdf", "/home/user/document.pdf")]
    [InlineData("file:///home/user/my%20documents/test%20file.pdf", "/home/user/my documents/test file.pdf")]
    public void DialogService_DecodeStoragePath_DecodesProperly(string? input, string? expected)
    {
        var result = DialogService.DecodeStoragePath(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DialogService_DecodeStoragePath_WindowsUri_NormalizesToLocalPath()
    {
        var result = DialogService.DecodeStoragePath("file:///C:/Users/User%20Name/Documents/Doc.pdf");
        Assert.NotNull(result);
        Assert.Contains("User Name", result);
        Assert.DoesNotContain("%20", result);
    }

    [Fact]
    public void DatabaseHelper_PathsReferToSameFile_SamePaths_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var fullPath = Path.GetFullPath(tempFile);
            var isSame = DatabaseHelper.PathsReferToSameFile(tempFile, fullPath);
            Assert.True(isSame);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ClipboardService_SetTextAsync_DoesNotThrow()
    {
        var service = new ClipboardService();
        await service.SetTextAsync("test clipboard payload");
    }
}
