using StudyDocumentManager.Core.DTOs;

namespace StudyDocumentManager.Core.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task CheckSilentlyAsync();
    Task HandleUpdateAsync(UpdateInfo update);
}
