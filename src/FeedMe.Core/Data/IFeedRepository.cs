using FeedMe.Core.Models;

namespace FeedMe.Core.Data;

/// <summary>
/// Storage seam for feeds and items. The SQLite implementation is the default,
/// but this interface is the only thing the rest of the app depends on.
/// </summary>
public interface IFeedRepository
{
    Task InitializeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Feed>> GetFeedsAsync(CancellationToken ct = default);
    Task<Feed> AddFeedAsync(string feedUrl, string title, string? siteUrl, string? category, CancellationToken ct = default);
    Task RemoveFeedAsync(long feedId, CancellationToken ct = default);
    Task SetCategoryAsync(long feedId, string? category, CancellationToken ct = default);
    Task SetLastFetchedAsync(long feedId, DateTimeOffset whenUtc, CancellationToken ct = default);

    /// <summary>Items for one feed, or all feeds when <paramref name="feedId"/> is null. Newest first.</summary>
    Task<IReadOnlyList<FeedItem>> GetItemsAsync(long? feedId = null, CancellationToken ct = default);

    /// <summary>Inserts items that don't already exist. Returns the number of newly added items.</summary>
    Task<int> UpsertItemsAsync(long feedId, IEnumerable<FetchedItem> items, CancellationToken ct = default);

    Task MarkReadAsync(long itemId, bool isRead, CancellationToken ct = default);

    /// <summary>Unread item count per feed id (feeds with zero unread are omitted).</summary>
    Task<IReadOnlyDictionary<long, int>> GetUnreadCountsAsync(CancellationToken ct = default);
}
