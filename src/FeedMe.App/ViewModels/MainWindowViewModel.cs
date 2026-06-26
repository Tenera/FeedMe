using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedMe.Core.Services;

namespace FeedMe.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string UncategorizedName = "Uncategorized";

    private readonly FeedService? _feeds;

    /// <summary>The "All feeds" sidebar entry (a stable instance, sits above the categories).</summary>
    public FeedListItemViewModel AllFeeds { get; } = new(null, "All feeds", 0) { IsAllFeeds = true };

    /// <summary>Feeds grouped into category sections for the sidebar.</summary>
    public ObservableCollection<FeedGroupViewModel> Groups { get; } = new();

    public ObservableCollection<FeedItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private FeedListItemViewModel? _selectedFeed;

    [ObservableProperty]
    private FeedItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _newFeedUrl = string.Empty;

    [ObservableProperty]
    private string _newFeedCategory = string.Empty;

    /// <summary>True = clean reader-mode content; false = the live web page.</summary>
    [ObservableProperty]
    private bool _isReaderMode = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    private bool _hasLoaded;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasNoItems => HasLoaded && !IsBusy && Items.Count == 0;

    /// <summary>Runtime constructor.</summary>
    public MainWindowViewModel(FeedService feeds) => _feeds = feeds;

    /// <summary>Design-time constructor: sample data for the XAML previewer only.</summary>
    public MainWindowViewModel()
    {
        AllFeeds.UnreadCount = 12;
        AllFeeds.IsSelected = true;
        Groups.Add(new FeedGroupViewModel("Tech", new[]
        {
            new FeedListItemViewModel(1, "Hacker News", 5, "Tech"),
            new FeedListItemViewModel(2, "The Verge", 7, "Tech")
        }));
        Groups.Add(new FeedGroupViewModel("News", new[]
        {
            new FeedListItemViewModel(3, "BBC News", 3, "News")
        }));
        _selectedFeed = AllFeeds;
        HasLoaded = true;
    }

    public async Task InitializeAsync()
    {
        if (_feeds is null)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Loading…";
            await _feeds.InitializeAsync();
            var seeded = await _feeds.SeedDefaultsIfEmptyAsync();
            await ReloadAsync();
            StatusMessage = seeded ? "Added a few starter feeds to get you going." : null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Startup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_feeds is null)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Refreshing…";
            var result = await _feeds.RefreshAllAsync();
            await ReloadAsync();
            StatusMessage = DescribeRefresh(result);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddFeedAsync()
    {
        if (_feeds is null)
            return;

        var url = NewFeedUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Adding feed…";
            var feed = await _feeds.AddFeedAsync(url, NewFeedCategory);
            NewFeedUrl = string.Empty;
            NewFeedCategory = string.Empty;
            await ReloadAsync();
            StatusMessage = $"Added “{feed.Title}”.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't add that feed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Imports feeds from an OPML file chosen via the file picker (called from the view).</summary>
    public Task ImportOpmlAsync(Stream opml) => RunImportAsync(() => _feeds!.ImportOpmlAsync(opml));

    /// <summary>Imports feeds from OPML text pasted from the clipboard (called from the view).</summary>
    public Task ImportOpmlTextAsync(string opml)
    {
        if (string.IsNullOrWhiteSpace(opml))
        {
            StatusMessage = "Nothing to import — the clipboard is empty.";
            return Task.CompletedTask;
        }

        return RunImportAsync(() => _feeds!.ImportOpmlAsync(opml));
    }

    private async Task RunImportAsync(Func<Task<OpmlImportResult>> import)
    {
        if (_feeds is null)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Importing OPML…";
            var result = await import();
            await ReloadAsync();
            StatusMessage = DescribeImport(result);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasNoItems));
        }
    }

    [RelayCommand]
    private async Task RemoveFeedAsync(FeedListItemViewModel? feed)
    {
        if (_feeds is null || feed is null || feed.IsAllFeeds || feed.FeedId is null)
            return;

        await _feeds.RemoveFeedAsync(feed.FeedId.Value);
        await ReloadAsync();
        StatusMessage = $"Removed “{feed.Title}”.";
    }

    /// <summary>Selects a sidebar entry (All feeds or a single feed) and loads its items.</summary>
    [RelayCommand]
    private void SelectFeed(FeedListItemViewModel? feed)
    {
        if (feed is not null)
            ApplySelection(feed);
    }

    /// <summary>Moves a feed to a category (null/blank clears it). Called from the view after prompting.</summary>
    public async Task MoveFeedToCategoryAsync(FeedListItemViewModel feed, string? category)
    {
        if (_feeds is null || feed.FeedId is null)
            return;

        await _feeds.SetCategoryAsync(feed.FeedId.Value, category);
        await ReloadAsync();
        StatusMessage = $"Moved “{feed.Title}” to {(string.IsNullOrWhiteSpace(category) ? UncategorizedName : category)}.";
    }

    /// <summary>Opens an article in the reading pane and marks it read.</summary>
    [RelayCommand]
    private async Task SelectItemAsync(FeedItemViewModel? item)
    {
        if (item is null)
            return;

        if (SelectedItem is { } previous && !ReferenceEquals(previous, item))
            previous.IsSelected = false;

        item.IsSelected = true;
        SelectedItem = item;

        if (_feeds is not null && !item.IsRead)
        {
            await _feeds.MarkReadAsync(item.Id, true);
            item.IsRead = true;
            await RefreshUnreadCountsAsync();
        }
    }

    /// <summary>Opens the original article in the system browser.</summary>
    [RelayCommand]
    private void OpenItem(FeedItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Link))
            return;

        OpenInBrowser(item.Link);
    }

    private void ApplySelection(FeedListItemViewModel? feed)
    {
        SelectedFeed = feed;
        foreach (var item in AllItems())
            item.IsSelected = ReferenceEquals(item, feed);
        _ = LoadItemsAsync();
    }

    private IEnumerable<FeedListItemViewModel> AllItems()
    {
        yield return AllFeeds;
        foreach (var group in Groups)
            foreach (var feed in group.Feeds)
                yield return feed;
    }

    private async Task LoadItemsAsync()
    {
        if (_feeds is null)
            return;

        try
        {
            var feedId = SelectedFeed is null || SelectedFeed.IsAllFeeds ? (long?)null : SelectedFeed.FeedId;
            var items = await _feeds.GetItemsAsync(feedId);

            SelectedItem = null;
            Items.Clear();
            foreach (var item in items)
                Items.Add(new FeedItemViewModel(item));

            HasLoaded = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load items: {ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(HasNoItems));
        }
    }

    private async Task ReloadAsync()
    {
        if (_feeds is null)
            return;

        var previousId = SelectedFeed?.FeedId;
        var previousWasAll = SelectedFeed?.IsAllFeeds ?? true;

        var unread = await _feeds.GetUnreadCountsAsync();
        var feeds = await _feeds.GetFeedsAsync();

        AllFeeds.UnreadCount = unread.Values.Sum();

        Groups.Clear();
        var grouped = feeds
            .GroupBy(feed => string.IsNullOrWhiteSpace(feed.Category) ? UncategorizedName : feed.Category!.Trim())
            .OrderBy(group => group.Key == UncategorizedName ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var items = group
                .OrderBy(feed => feed.Title, StringComparer.OrdinalIgnoreCase)
                .Select(feed => new FeedListItemViewModel(feed.Id, feed.Title, unread.GetValueOrDefault(feed.Id), feed.Category)
                {
                    RemoveCommand = RemoveFeedCommand
                })
                .ToList();
            Groups.Add(new FeedGroupViewModel(group.Key, items));
        }

        var restored = previousWasAll
            ? AllFeeds
            : AllItems().FirstOrDefault(item => item.FeedId == previousId) ?? AllFeeds;
        ApplySelection(restored);
    }

    private async Task RefreshUnreadCountsAsync()
    {
        if (_feeds is null)
            return;

        var unread = await _feeds.GetUnreadCountsAsync();
        AllFeeds.UnreadCount = unread.Values.Sum();
        foreach (var feed in AllItems().Where(item => !item.IsAllFeeds))
            feed.UnreadCount = unread.GetValueOrDefault(feed.FeedId ?? -1);
    }

    private static string DescribeRefresh(RefreshResult result)
    {
        var headline = result.NewItems switch
        {
            0 => "You're all caught up — no new items.",
            1 => "1 new item.",
            _ => $"{result.NewItems} new items."
        };
        return result.Failures.Count == 0
            ? headline
            : $"{headline} ({result.Failures.Count} feed(s) couldn't be reached.)";
    }

    private static string DescribeImport(OpmlImportResult result)
    {
        var parts = new List<string> { $"Imported {result.Imported} feed(s)" };
        if (result.Skipped > 0) parts.Add($"{result.Skipped} already added");
        if (result.Failed > 0) parts.Add($"{result.Failed} failed");
        return string.Join(", ", parts) + ".";
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't open the link: {ex.Message}";
        }
    }
}
