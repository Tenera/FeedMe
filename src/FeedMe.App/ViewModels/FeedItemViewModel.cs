using CommunityToolkit.Mvvm.ComponentModel;
using FeedMe.Core.Models;

namespace FeedMe.App.ViewModels;

/// <summary>A single article, shown both as an overview card and in the reading pane.</summary>
public partial class FeedItemViewModel : ViewModelBase
{
    private readonly FeedItem _item;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnreadDot))]
    private bool _isRead;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnreadDot))]
    private bool _isSelected;

    public FeedItemViewModel(FeedItem item)
    {
        _item = item;
        _isRead = item.IsRead;
        Meta = BuildMeta(item);
    }

    public long Id => _item.Id;
    public string Title => _item.Title;
    public string? Summary => _item.Summary;
    public string? Link => _item.Link;

    /// <summary>Raw article HTML (from content:encoded) for the reader-mode document.</summary>
    public string? ContentHtml => _item.Content;

    /// <summary>Absolute link as a <see cref="Uri"/> for the reading-pane WebView, or null if unparseable.</summary>
    public Uri? LinkUri =>
        Uri.TryCreate(_item.Link, UriKind.Absolute, out var uri) ? uri : null;

    /// <summary>The unread marker shows only when unread and not the current selection.</summary>
    public bool ShowUnreadDot => !IsRead && !IsSelected;

    /// <summary>Source · author · relative time, joined for the subtitle line.</summary>
    public string Meta { get; }

    private static string BuildMeta(FeedItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.FeedTitle)) parts.Add(item.FeedTitle!);
        if (!string.IsNullOrWhiteSpace(item.Author)) parts.Add(item.Author!);
        if (item.PublishedUtc.HasValue) parts.Add(RelativeTime.Format(item.PublishedUtc.Value));
        return string.Join("   ·   ", parts);
    }
}
