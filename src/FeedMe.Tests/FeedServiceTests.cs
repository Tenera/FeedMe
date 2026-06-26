using FeedMe.Core.Data;
using FeedMe.Core.Services;
using Microsoft.Data.Sqlite;

namespace FeedMe.Tests;

public sealed class FeedServiceTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"feedme-test-{Guid.NewGuid():N}.db");
    private readonly SqliteFeedRepository _repo;
    private readonly FakeFeedFetcher _fetcher = new();
    private readonly FeedService _service;

    public FeedServiceTests()
    {
        _repo = new SqliteFeedRepository(_dbPath);
        _service = new FeedService(_repo, _fetcher);
    }

    public Task InitializeAsync() => _repo.InitializeAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddFeed_StoresFeedItemsAndCategory()
    {
        _fetcher.Add("https://f.example/rss", Sample.Feed("My Feed", Sample.Item("1"), Sample.Item("2")));

        var feed = await _service.AddFeedAsync("https://f.example/rss", "Tech");

        Assert.Equal("My Feed", feed.Title);
        Assert.Equal("Tech", (await _service.GetFeedsAsync()).Single().Category);
        Assert.Equal(2, (await _service.GetItemsAsync(feed.Id)).Count);
    }

    [Fact]
    public async Task AddFeed_BlankCategory_IsStoredAsUncategorized()
    {
        _fetcher.Add("https://f.example/rss", Sample.Feed("F"));

        var feed = await _service.AddFeedAsync("https://f.example/rss", "   ");

        Assert.Null((await _service.GetFeedsAsync()).Single().Category);
    }

    [Fact]
    public async Task RefreshAll_CountsNewItems_AndContinuesPastFailingFeeds()
    {
        await _repo.AddFeedAsync("https://ok.example/rss", "OK", null, null);
        await _repo.AddFeedAsync("https://bad.example/rss", "Bad", null, null);
        _fetcher.Add("https://ok.example/rss", Sample.Feed("OK", Sample.Item("1"), Sample.Item("2")));
        _fetcher.Fail("https://bad.example/rss");

        var result = await _service.RefreshAllAsync();

        Assert.Equal(2, result.NewItems);
        Assert.Single(result.Failures);
        Assert.Contains("Bad", result.Failures[0]);
    }

    [Fact]
    public async Task ImportOpml_ImportsWithCategories_AndSkipsDuplicates()
    {
        // Pre-existing subscription that also appears in the OPML -> should be skipped.
        await _repo.AddFeedAsync("https://hn.example/feed", "HN", null, null);

        const string opml =
            """
            <opml version="2.0"><body>
              <outline text="Tech" title="Tech">
                <outline type="rss" xmlUrl="https://ars.example/feed"/>
                <outline type="rss" xmlUrl="https://verge.example/feed"/>
              </outline>
              <outline type="rss" xmlUrl="https://hn.example/feed"/>
            </body></opml>
            """;

        var result = await _service.ImportOpmlAsync(opml);

        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);

        var feeds = await _service.GetFeedsAsync();
        Assert.Equal("Tech", feeds.Single(f => f.FeedUrl == "https://ars.example/feed").Category);
    }

    [Fact]
    public async Task SeedDefaults_SeedsOnlyWhenEmpty()
    {
        Assert.True(await _service.SeedDefaultsIfEmptyAsync());
        Assert.Equal(FeedService.DefaultFeeds.Count, (await _service.GetFeedsAsync()).Count);

        Assert.False(await _service.SeedDefaultsIfEmptyAsync());
        Assert.Equal(FeedService.DefaultFeeds.Count, (await _service.GetFeedsAsync()).Count);
    }
}
