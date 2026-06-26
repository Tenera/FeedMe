namespace FeedMe.Core.Models;

/// <summary>A feed the user is subscribed to.</summary>
public sealed class Feed
{
    public long Id { get; init; }
    public required string Title { get; set; }
    public required string FeedUrl { get; init; }
    public string? SiteUrl { get; set; }

    /// <summary>Optional grouping category (e.g. "Tech"). Null/empty means uncategorized.</summary>
    public string? Category { get; set; }

    public DateTimeOffset? LastFetchedUtc { get; set; }
}
