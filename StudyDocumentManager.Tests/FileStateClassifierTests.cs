using StudyDocumentManager.Core.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Pure classification proofs for the document file-state contract (Issue #45).
/// No filesystem access: probes are fakes that simulate OS failure kinds.
/// </summary>
public sealed class FileStateClassifierTests
{
    private static readonly string WindowsPath = @"C:\docs\report.pdf";
    private static readonly string UnixPath = "/home/user/report.pdf";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_EmptyPath_ReturnsNotSet(string? path)
    {
        var state = FileStateClassifier.Classify(path, _ => true);
        Assert.Equal(DocumentFileState.NotSet, state);
    }

    [Theory]
    [InlineData("report.pdf")]
    [InlineData("./report.pdf")]
    [InlineData("C:report.pdf")]
    public void Classify_RelativeOrRootlessPath_ReturnsInvalidPath(string path)
    {
        var state = FileStateClassifier.Classify(path, _ => true);
        Assert.Equal(DocumentFileState.InvalidPath, state);
    }

    [Fact]
    public void Classify_InvalidPathChars_ReturnsInvalidPath()
    {
        var path = $"C:\\docs\\bad{Path.GetInvalidPathChars()[0]}name.pdf";
        var state = FileStateClassifier.Classify(path, _ => true);
        Assert.Equal(DocumentFileState.InvalidPath, state);
    }

    [Fact]
    public void Classify_ProbeReturnsTrue_ReturnsOk()
    {
        Assert.Equal(DocumentFileState.Ok, FileStateClassifier.Classify(WindowsPath, _ => true));
        Assert.Equal(DocumentFileState.Ok, FileStateClassifier.Classify(UnixPath, _ => true));
    }

    [Fact]
    public void Classify_ProbeReturnsFalse_ReturnsMissing()
    {
        var state = FileStateClassifier.Classify(WindowsPath, _ => false);
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void Classify_UnauthorizedAccess_ReturnsAccessDenied()
    {
        var state = FileStateClassifier.Classify(WindowsPath, _ => throw new UnauthorizedAccessException());
        Assert.Equal(DocumentFileState.AccessDenied, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_ReturnsMissing()
    {
        var state = FileStateClassifier.Classify(WindowsPath,
            _ => throw new DirectoryNotFoundException());
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_HResult0x80070003_OnUnreadyRoot_ReturnsDriveDisconnected()
    {
        // Windows raises DirectoryNotFoundException 0x80070003 for any path on a
        // disconnected drive/share (e.g. Z:\missing\doc.pdf), same HResult as a
        // missing folder on a healthy drive — the root probe disambiguates.
        var state = FileStateClassifier.Classify(
            @"Z:\missing\doc.pdf",
            _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => false);
        Assert.Equal(DocumentFileState.DriveDisconnected, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_HResult0x80070003_OnReadyRoot_ReturnsMissing()
    {
        var state = FileStateClassifier.Classify(
            WindowsPath,
            _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => true);
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_WithoutRootProbe_KeepsMissingFallback()
    {
        var state = FileStateClassifier.Classify(
            WindowsPath,
            _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) });
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_HResult0x80070003_OnUnreadyUncShare_ReturnsDriveDisconnected()
    {
        var state = FileStateClassifier.Classify(
            @"\\server\share\missing\doc.pdf",
            _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => false);
        Assert.Equal(DocumentFileState.DriveDisconnected, state);
    }

    [Fact]
    public void Classify_DirectoryNotFound_HResult0x80070003_OnReachableUncShare_ReturnsMissing()
    {
        var state = FileStateClassifier.Classify(
            @"\\server\share\missing\doc.pdf",
            _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => true);
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void RootReadyProbe_HealthyDrive_ReturnsTrue()
    {
        Assert.True(FileStateClassifier.RootReadyProbe(Path.GetTempPath()));
    }

    [Fact]
    public void RootReadyProbe_UnresolvableUncShare_ReturnsFalse()
    {
        // A host that cannot resolve can never expose the share, so the probe must
        // report the root as not ready instead of assuming readiness.
        Assert.False(FileStateClassifier.RootReadyProbe(@"\\sdm-nonexistent-host-test\share"));
    }

    [Fact]
    public void RootReadyProbe_LocalMissingPath_StillReportsDriveReady()
    {
        // A missing folder on a healthy local drive must not flip readiness.
        var missing = Path.Combine(Path.GetTempPath(), $"sdm_probe_{Guid.NewGuid():N}", "gone.pdf");
        Assert.True(FileStateClassifier.RootReadyProbe(missing));
    }

    [Fact]
    public void Classify_DriveNotReady_ReturnsDriveDisconnected()
    {
        var state = FileStateClassifier.Classify(WindowsPath,
            _ => throw new IOException("drive not ready", HResultFromWin32(21)));
        Assert.Equal(DocumentFileState.DriveDisconnected, state);
    }

    [Theory]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(67)]
    public void Classify_NetworkPathFailures_ReturnsDriveDisconnected(int win32Error)
    {
        var state = FileStateClassifier.Classify(@"\\server\share\doc.pdf",
            _ => throw new IOException("network path failure", HResultFromWin32(win32Error)));
        Assert.Equal(DocumentFileState.DriveDisconnected, state);
    }

    [Fact]
    public void Classify_OtherIoFailure_ReturnsMissing()
    {
        var state = FileStateClassifier.Classify(WindowsPath,
            _ => throw new IOException("unknown io failure", HResultFromWin32(33)));
        Assert.Equal(DocumentFileState.Missing, state);
    }

    [Fact]
    public void Classify_PathFormatFailure_ReturnsInvalidPath()
    {
        var state = FileStateClassifier.Classify("C:\\ok\\path.pdf",
            _ => throw new ArgumentException("invalid"));
        Assert.Equal(DocumentFileState.InvalidPath, state);
    }

    [Fact]
    public void Classify_NonPathException_Propagates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FileStateClassifier.Classify(WindowsPath, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void ReadableProbe_MissingFile_ThrowsFileNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"sdm_probe_{Guid.NewGuid():N}.pdf");
        Assert.Throws<FileNotFoundException>(() => FileStateClassifier.ReadableProbe(missing));
    }

    [Fact]
    public void ReadableProbe_ExistingFile_ReturnsTrue()
    {
        var file = Path.Combine(Path.GetTempPath(), $"sdm_probe_{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllText(file, "probe");
            Assert.True(FileStateClassifier.ReadableProbe(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static int HResultFromWin32(int win32Error)
        => unchecked((int)(0x80070000u | (uint)win32Error));
}
