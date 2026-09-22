namespace StudyDocumentManager.Core.DTOs;

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}
