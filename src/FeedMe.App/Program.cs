using Avalonia;
using System;
using System.Threading;

namespace FeedMe.App;

sealed class Program
{
    // Held for the process lifetime to enforce a single running instance. Running two
    // instances would make them fight over the WebView2 user-data folder lock.
    private static Mutex? _singleInstanceMutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\FeedMe.SingleInstance", out var createdNew);
        if (!createdNew)
            return; // Another FeedMe instance already owns this session; let it be.

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
