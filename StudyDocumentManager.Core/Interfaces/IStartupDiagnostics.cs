namespace StudyDocumentManager.Core.Interfaces;

public interface IStartupDiagnostics
{
    void RecordDatabaseInitializationSucceeded();

    void RecordDatabaseInitializationFailed(Exception exception);
}
