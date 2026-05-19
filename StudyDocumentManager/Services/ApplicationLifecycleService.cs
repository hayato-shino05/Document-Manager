using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace StudyDocumentManager.Services;

public class ApplicationLifecycleService : IApplicationLifecycleService
{
    public void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
