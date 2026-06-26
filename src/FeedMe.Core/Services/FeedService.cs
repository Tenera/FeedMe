using FeedMe.Core.Data;
using FeedMe.Core.Models;

namespace FeedMe.Core.Services;

/// <summary>Outcome of a refresh run: how many new items, and any feeds that failed.</summary>
public sealed record RefreshResult(int NewItems, IReadOnlyList<string> Failures);

/// <summary>Outcome of an OPML import: feeds added, already-present, and failed.</summary>
public sealed record OpmlImportResult(int Imported, int Skipped, int Failed);

/// <summary>
/// Application-facing facade combining the repository (storage) and the fetcher (network).
/// The UI talks only to this.
/// </summary>
public sealed class FeedService
{
    /// <summary>Feeds added on first run so the app isn't empty.</summary>
    public static readonly IReadOnlyList<string> DefaultFeeds = new[]
    {
        "https://hnrss.org/frontpage",
        "https://www.theverge.com/rss/index.xml",
        "https://feeds.bbci.co.uk/news/world/rss.xml"
    };

    private readonly IFeedRepository _repository;
    private readonly IFeedFetcher _fetcher;

    public FeedService(IFeedRepository repository, IFeedFetcher fetcher)
    {
        _repository = repository;
        _fetcher = fetcher;
    }

    public Task InitializeAsync(CancellationToken ct = default) => _repository.InitializeAsync(ct);

    public Task<IReadOnlyList<Feed>> GetFeedsAsync(CancellationToken ct = default) => _repository.GetFeedsAsync(ct);

    public Task<IReadOnlyList<FeedItem>> GetItemsAsync(long? feedId = null, CancellationToken ct = default)
        => _repository.GetItemsAsync(feedId, ct);

    public Task<IReadOnlyDictionary<long, int>> GetUnreadCountsAsync(CancellationToken ct = default)
        => _repository.GetUnreadCountsAsync(ct);

    public Task MarkReadAsync(long itemId, bool isRead, CancellationToken ct = default)
        => _repository.MarkReadAsync(itemId, isRead, ct);

    public Task RemoveFeedAsync(long feedId, CancellationToken ct = default)
        => _repository.RemoveFeedAsync(feedId, ct);

    public Task SetCategoryAsync(long feedId, string? category, CancellationToken ct = default)
        => _repository.SetCategoryAsync(feedId, Normalize(category), ct);

    /// <summary>Fetches a feed, stores it (optionally under a category) and its items, and returns the new feed.</summary>
    public async Task<Feed> AddFeedAsync(string feedUrl, string? category = null, CancellationToken ct = default)
    {
        var fetched = await _fetcher.FetchAsync(feedUrl, ct);
        var feed = await _repository.AddFeedAsync(feedUrl, fetched.Title, fetched.SiteUrl, Normalize(category), ct);
        await _repository.UpsertItemsAsync(feed.Id, fetched.Items, ct);
        await _repository.SetLastFetchedAsync(feed.Id, DateTimeOffset.UtcNow, ct);
        return feed;
    }

    private static string? Normalize(string? category)
        => string.IsNullOrWhiteSpace(category) ? null : category.Trim();

    /// <summary>Refreshes every feed. A single failing feed does not abort the rest.</summary>
    public async Task<RefreshResult> RefreshAllAsync(CancellationToken ct = default)
    {
        var feeds = await _repository.GetFeedsAsync(ct);
        var newItems = 0;
        var failures = new List<string>();

        foreach (var feed in feeds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fetched = await _fetcher.FetchAsync(feed.FeedUrl, ct);
                newItems += await _repository.UpsertItemsAsync(feed.Id, fetched.Items, ct);
                await _repository.SetLastFetchedAsync(feed.Id, DateTimeOffset.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add($"{feed.Title}: {ex.Message}");
            }
        }

        return new RefreshResult(newItems, failures);
    }

    /// <summary>Imports feeds from an OPML stream, skipping ones already subscribed.</summary>
    public Task<OpmlImportResult> ImportOpmlAsync(Stream opml, CancellationToken ct = default)
        => ImportFeedsAsync(OpmlParser.ExtractFeeds(opml), ct);

    /// <summary>Imports feeds from pasted OPML text, skipping ones already subscribed.</summary>
    public Task<OpmlImportResult> ImportOpmlAsync(string opml, CancellationToken ct = default)
        => ImportFeedsAsync(OpmlParser.ExtractFeeds(opml), ct);

    private async Task<OpmlImportResult> ImportFeedsAsync(IReadOnlyList<OpmlFeed> feeds, CancellationToken ct)
    {
        var existing = (await _repository.GetFeedsAsync(ct))
            .Select(feed => feed.FeedUrl)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0, failed = 0;
        foreach (var feed in feeds)
        {
            ct.ThrowIfCancellationRequested();
            if (!existing.Add(feed.Url))
            {
                skipped++;
                continue;
            }

            try
            {
                await AddFeedAsync(feed.Url, feed.Category, ct);
                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
            }
        }

        return new OpmlImportResult(imported, skipped, failed);
    }

    /// <summary>On first run (no feeds yet), adds the default feeds. Returns true if it seeded.</summary>
    public async Task<bool> SeedDefaultsIfEmptyAsync(CancellationToken ct = default)
    {
        var existing = await _repository.GetFeedsAsync(ct);
        if (existing.Count > 0)
            return false;

        foreach (var url in DefaultFeeds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await AddFeedAsync(url, ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort seeding; a starter feed being unreachable shouldn't block startup.
            }
        }

        return true;
    }
}
