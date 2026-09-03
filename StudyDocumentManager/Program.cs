using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using StudyDocumentManager.Services;

namespace StudyDocumentManager;

sealed class Program
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now}] [AppDomain] {e.ExceptionObject}\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now}] [TaskScheduler] {e.Exception}\n");
            }
            catch { }
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now}] [Main] {ex}\n");
            }
            catch { }
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .ConfigureFonts(fontManager =>
                fontManager.AddFontCollection(new HaranoAjiFontCollection()))
            .WithInterFont()
            .LogToTrace();
}
