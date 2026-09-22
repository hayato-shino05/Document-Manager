namespace StudyDocumentManager.Core.Interfaces;

public enum ToastType { Success, Error, Warning, Info }

public interface IToastService
{
    void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000);
}
