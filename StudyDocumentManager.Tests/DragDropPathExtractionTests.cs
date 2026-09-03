using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DragDropPathExtractionTests
{
    [Fact]
    public void GetFilePaths_NullData_ReturnsEmptyList()
    {
        var paths = MainWindow.GetFilePathsFromDataObject(null);
        Assert.Empty(paths);
    }

    [Fact]
    public void GetFilePaths_WithFileNames_ExtractsDecodedPaths()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var dataObject = new DataObject();
            dataObject.Set(DataFormats.FileNames, new[] { tempFile });

            var paths = MainWindow.GetFilePathsFromDataObject(dataObject);

            Assert.Single(paths);
            Assert.Equal(Path.GetFullPath(tempFile), paths[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetFilePaths_WithLinuxUriList_ExtractsAndUnescapesPaths()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"linux test file-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(tempFile, "sample");
        try
        {
            var uri = new Uri(tempFile).AbsoluteUri;
            var uriListContent = $"# GNOME XDnD drop\r\n{uri}\r\n";

            var dataObject = new DataObject();
            dataObject.Set("text/uri-list", uriListContent);

            var paths = MainWindow.GetFilePathsFromDataObject(dataObject);

            Assert.Single(paths);
            Assert.Equal(Path.GetFullPath(tempFile), paths[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetFilePaths_WithMultipleLinuxUris_ExtractsAllDistinct()
    {
        var temp1 = Path.Combine(Path.GetTempPath(), $"linux-1-{Guid.NewGuid():N}.pdf");
        var temp2 = Path.Combine(Path.GetTempPath(), $"linux-2-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(temp1, "1");
        File.WriteAllText(temp2, "2");
        try
        {
            var uri1 = new Uri(temp1).AbsoluteUri;
            var uri2 = new Uri(temp2).AbsoluteUri;
            var uriListContent = $"{uri1}\n{uri2}\n";

            var dataObject = new DataObject();
            dataObject.Set("text/uri-list", uriListContent);

            var paths = MainWindow.GetFilePathsFromDataObject(dataObject);

            Assert.Equal(2, paths.Count);
            Assert.Contains(Path.GetFullPath(temp1), paths);
            Assert.Contains(Path.GetFullPath(temp2), paths);
        }
        finally
        {
            if (File.Exists(temp1)) File.Delete(temp1);
            if (File.Exists(temp2)) File.Delete(temp2);
        }
    }

    [Fact]
    public void GetFilePaths_WithTextContainingFileUri_ExtractsFallback()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"fallback-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(tempFile, "fallback");
        try
        {
            var uri = new Uri(tempFile).AbsoluteUri;
            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Text, uri);

            var paths = MainWindow.GetFilePathsFromDataObject(dataObject);

            Assert.Single(paths);
            Assert.Equal(Path.GetFullPath(tempFile), paths[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
