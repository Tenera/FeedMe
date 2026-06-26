using FeedMe.Core.Services;

namespace FeedMe.Tests;

public class OpmlParserTests
{
    private const string Opml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <opml version="2.0">
          <head><title>subs</title></head>
          <body>
            <outline text="Tech" title="Tech">
              <outline type="rss" text="Ars" xmlUrl="https://ars.example/feed"/>
              <outline type="rss" text="Verge" xmlUrl="https://verge.example/feed"/>
            </outline>
            <outline type="rss" text="HN" xmlUrl="https://hn.example/feed"/>
            <outline text="Empty folder"/>
          </body>
        </opml>
        """;

    [Fact]
    public void ExtractsEveryFeedUrl()
    {
        var feeds = OpmlParser.ExtractFeeds(Opml);

        Assert.Equal(3, feeds.Count);
        Assert.Contains(feeds, f => f.Url == "https://ars.example/feed");
        Assert.Contains(feeds, f => f.Url == "https://verge.example/feed");
        Assert.Contains(feeds, f => f.Url == "https://hn.example/feed");
    }

    [Fact]
    public void AssignsCategoryFromParentFolder()
    {
        var feeds = OpmlParser.ExtractFeeds(Opml);

        Assert.Equal("Tech", feeds.Single(f => f.Url == "https://ars.example/feed").Category);
        Assert.Equal("Tech", feeds.Single(f => f.Url == "https://verge.example/feed").Category);
        Assert.Null(feeds.Single(f => f.Url == "https://hn.example/feed").Category);
    }

    [Fact]
    public void DeduplicatesByUrl()
    {
        const string opml =
            """
            <opml><body>
              <outline xmlUrl="https://a.example/feed"/>
              <outline xmlUrl="https://a.example/feed"/>
            </body></opml>
            """;

        Assert.Single(OpmlParser.ExtractFeeds(opml));
    }

    [Fact]
    public void SupportsXmlUrlCasingVariant()
    {
        const string opml = """<opml><body><outline xmlURL="https://a.example/feed"/></body></opml>""";

        Assert.Equal("https://a.example/feed", OpmlParser.ExtractFeeds(opml).Single().Url);
    }

    [Fact]
    public void IgnoresOutlinesWithoutFeedUrl()
    {
        const string opml = """<opml><body><outline text="just a folder"/></body></opml>""";

        Assert.Empty(OpmlParser.ExtractFeeds(opml));
    }
}
