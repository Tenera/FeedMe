using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FeedMe.App.ViewModels;

/// <summary>A row in the sidebar: a single feed, or the "All feeds" entry.</summary>
public partial class FeedListItemViewModel : ViewModelBase
{
    public long? FeedId { get; }
    public string Title { get; }
    public string? Category { get; }
    public bool IsAllFeeds { get; init; }

    /// <summary>Set by the owner so the sidebar's context menu can remove this feed.</summary>
    public IAsyncRelayCommand<FeedListItemViewModel?>? RemoveCommand { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isSelected;

    public bool HasUnread => UnreadCount > 0;

    public FeedListItemViewModel(long? feedId, string title, int unreadCount, string? category = null)
    {
        FeedId = feedId;
        Title = title;
        _unreadCount = unreadCount;
        Category = category;
    }
}
