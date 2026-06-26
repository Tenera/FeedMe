using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FeedMe.App.ViewModels;
using SukiUI.Controls;

namespace FeedMe.App.Views;

public partial class MainWindow : SukiWindow
{
    private static readonly Uri FallbackBaseUri = new("https://localhost/");
    private bool _webViewReady;

    public MainWindow()
    {
        InitializeComponent();

        // The native WebView adapter is created lazily (once the pane becomes visible).
        Reader.AdapterCreated += (_, _) => { _webViewReady = true; ApplyReader(); };
        Reader.AdapterDestroyed += (_, _) => _webViewReady = false;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            _ = vm.InitializeAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedItem)
            or nameof(MainWindowViewModel.IsReaderMode))
        {
            ApplyReader();
        }
    }

    /// <summary>Renders the selected article in the WebView, as reader-mode content or the live page.</summary>
    private void ApplyReader()
    {
        if (!_webViewReady || DataContext is not MainWindowViewModel vm)
            return;

        var item = vm.SelectedItem;
        if (item is null)
            return; // The pane is hidden when nothing is selected.

        if (vm.IsReaderMode || item.LinkUri is null)
        {
            var html = ReaderDocument.ForItem(item.Title, item.Meta, item.ContentHtml);
            Reader.NavigateToString(html, item.LinkUri ?? FallbackBaseUri);
        }
        else
        {
            Reader.Navigate(item.LinkUri);
        }
    }

    private void OnAllFeedsTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SelectFeedCommand.Execute(vm.AllFeeds);
    }

    private void OnFeedRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FeedListItemViewModel feed } &&
            DataContext is MainWindowViewModel vm)
        {
            vm.SelectFeedCommand.Execute(feed);
        }
    }

    private async void OnMoveFeedClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FeedListItemViewModel feed } ||
            DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var result = await new CategoryPrompt(feed.Category).ShowDialog<string?>(this);
        if (result is null)
            return; // cancelled

        await vm.MoveFeedToCategoryAsync(feed, string.IsNullOrWhiteSpace(result) ? null : result);
    }

    private void OnItemCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FeedItemViewModel item } &&
            DataContext is MainWindowViewModel vm)
        {
            vm.SelectItemCommand.Execute(item);
        }
    }

    private async void OnImportOpmlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var topLevel = GetTopLevel(this);
        if (topLevel is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import OPML",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("OPML / XML") { Patterns = new[] { "*.opml", "*.xml" } }
            }
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        await vm.ImportOpmlAsync(stream);
    }

    private async void OnPasteOpmlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var clipboard = GetTopLevel(this)?.Clipboard;
        var text = clipboard is null ? null : await clipboard.TryGetTextAsync();
        await vm.ImportOpmlTextAsync(text ?? string.Empty);
    }

    private void OnFeedUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.AddFeedCommand.Execute(null);
            e.Handled = true;
        }
    }
}
