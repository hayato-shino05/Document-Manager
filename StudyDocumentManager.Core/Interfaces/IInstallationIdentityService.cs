namespace StudyDocumentManager.Core.Interfaces;

public interface IInstallationIdentityService
{
    string GetInstallationId();

    void DeleteInstallationId();
}
