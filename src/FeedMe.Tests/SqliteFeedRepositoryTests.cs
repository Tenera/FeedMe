using FeedMe.Core.Data;
using FeedMe.Core.Models;
using Microsoft.Data.Sqlite;

namespace FeedMe.Tests;

public sealed class SqliteFeedRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"feedme-test-{Guid.NewGuid():N}.db");
    private readonly SqliteFeedRepository _repo;

    public SqliteFeedRepositoryTests() => _repo = new SqliteFeedRepository(_dbPath);

    public Task InitializeAsync() => _repo.InitializeAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools(); // release the file handle so it can be deleted
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await _repo.InitializeAsync(); // second call must not throw
        Assert.Empty(await _repo.GetFeedsAsync());
    }

    [Fact]
    public async Task AddFeed_RoundTripsWithCategory()
    {
        var added = await _repo.AddFeedAsync("https://f.example/rss", "My Feed", "https://f.example", "Tech");

        Assert.True(added.Id > 0);
        var feed = Assert.Single(await _repo.GetFeedsAsync());
        Assert.Equal("My Feed", feed.Title);
        Assert.Equal("https://f.example/rss", feed.FeedUrl);
        Assert.Equal("Tech", feed.Category);
    }

    [Fact]
    public async Task UpsertItems_IgnoresDuplicates_AndReportsNewCount()
    {
        var feed = await _repo.AddFeedAsync("https://f.example/rss", "F", null, null);
        var items = new[] { Sample.Item("a"), Sample.Item("b") };

        Assert.Equal(2, await _repo.UpsertItemsAsync(feed.Id, items));
        Assert.Equal(0, await _repo.UpsertItemsAsync(feed.Id, items)); // same items again
        Assert.Equal(2, (await _repo.GetItemsAsync(feed.Id)).Count);
    }

    [Fact]
    public async Task GetItems_FiltersByFeed_AndPopulatesFeedTitle()
    {
        var a = await _repo.AddFeedAsync("https://a.example/rss", "Feed A", null, null);
        var b = await _repo.AddFeedAsync("https://b.example/rss", "Feed B", null, null);
        await _repo.UpsertItemsAsync(a.Id, new[] { Sample.Item("a1") });
        await _repo.UpsertItemsAsync(b.Id, new[] { Sample.Item("b1"), Sample.Item("b2") });

        var itemsForA = await _repo.GetItemsAsync(a.Id);
        Assert.Single(itemsForA);
        Assert.Equal("Feed A", itemsForA[0].FeedTitle);
        Assert.Equal(3, (await _repo.GetItemsAsync()).Count); // all feeds
    }

    [Fact]
    public async Task MarkRead_UpdatesUnreadCounts()
    {
        var feed = await _repo.AddFeedAsync("https://f.example/rss", "F", null, null);
        await _repo.UpsertItemsAsync(feed.Id, new[] { Sample.Item("a"), Sample.Item("b") });

        Assert.Equal(2, (await _repo.GetUnreadCountsAsync())[feed.Id]);

        var first = (await _repo.GetItemsAsync(feed.Id))[0];
        await _repo.MarkReadAsync(first.Id, true);

        Assert.Equal(1, (await _repo.GetUnreadCountsAsync())[feed.Id]);
    }

    [Fact]
    public async Task SetCategory_UpdatesFeed()
    {
        var feed = await _repo.AddFeedAsync("https://f.example/rss", "F", null, null);

        await _repo.SetCategoryAsync(feed.Id, "News");

        Assert.Equal("News", (await _repo.GetFeedsAsync()).Single().Category);
    }

    [Fact]
    public async Task RemoveFeed_CascadesToItems()
    {
        var feed = await _repo.AddFeedAsync("https://f.example/rss", "F", null, null);
        await _repo.UpsertItemsAsync(feed.Id, new[] { Sample.Item("a"), Sample.Item("b") });

        await _repo.RemoveFeedAsync(feed.Id);

        Assert.Empty(await _repo.GetFeedsAsync());
        Assert.Empty(await _repo.GetItemsAsync());
    }
}
