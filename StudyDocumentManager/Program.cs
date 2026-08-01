using Avalonia;
using System;

namespace StudyDocumentManager;

using StudyDocumentManager.Services;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .ConfigureFonts(fontManager =>
                fontManager.AddFontCollection(new HaranoAjiFontCollection()))
            .WithInterFont()
            .LogToTrace();
}
