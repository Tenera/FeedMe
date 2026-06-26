using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FeedMe.App.ViewModels;
using FeedMe.App.Views;
using FeedMe.Core.Data;
using FeedMe.Core.Services;

namespace FeedMe.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var service = BuildFeedService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(service),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A WebView2 failure (e.g. a locked user-data folder) must not crash the whole app.
        if (IsRecoverableWebViewFailure(e.Exception))
        {
            CrashLog.Write("Recoverable WebView failure", e.Exception);
            e.Handled = true;
        }
    }

    private static bool IsRecoverableWebViewFailure(Exception exception)
        => exception is COMException
           || (exception.StackTrace?.Contains("WebView", StringComparison.OrdinalIgnoreCase) ?? false);

    private static FeedService BuildFeedService()
    {
        var repository = new SqliteFeedRepository(GetDatabasePath());

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FeedMe/1.0 (+https://github.com/feedme)");

        return new FeedService(repository, new FeedFetcher(http));
    }

    private static string GetDatabasePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FeedMe");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "feedme.db");
    }
}
