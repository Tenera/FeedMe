namespace FeedMe.App.ViewModels;

/// <summary>A category section in the sidebar: a header and the feeds under it.</summary>
public sealed class FeedGroupViewModel
{
    public string Name { get; }
    public IReadOnlyList<FeedListItemViewModel> Feeds { get; }

    public FeedGroupViewModel(string name, IReadOnlyList<FeedListItemViewModel> feeds)
    {
        Name = name;
        Feeds = feeds;
    }
}
