namespace FeedMe.Core.Models;

/// <summary>A single stored article/entry belonging to a <see cref="Feed"/>.</summary>
public sealed class FeedItem
{
    public long Id { get; init; }
    public long FeedId { get; init; }

    /// <summary>Stable id from the source feed (entry id or link), used for de-duplication.</summary>
    public required string ExternalId { get; init; }

    public required string Title { get; set; }
    public string? Author { get; set; }

    /// <summary>Short plain-text snippet for the overview list.</summary>
    public string? Summary { get; set; }

    /// <summary>Fuller article body (HTML) for the reading pane, when the feed provides it.</summary>
    public string? Content { get; set; }

    public string? Link { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public bool IsRead { get; set; }

    /// <summary>Title of the owning feed, populated for display (e.g. the "All feeds" view).</summary>
    public string? FeedTitle { get; set; }
}
