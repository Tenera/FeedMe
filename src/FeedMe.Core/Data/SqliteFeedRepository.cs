using System.Globalization;
using FeedMe.Core.Models;
using Microsoft.Data.Sqlite;

namespace FeedMe.Core.Data;

/// <summary>SQLite-backed <see cref="IFeedRepository"/>. One database file, no server.</summary>
public sealed class SqliteFeedRepository : IFeedRepository
{
    private const DateTimeStyles UtcStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly string _connectionString;

    public SqliteFeedRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Feeds (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Title           TEXT    NOT NULL,
                FeedUrl         TEXT    NOT NULL UNIQUE,
                SiteUrl         TEXT,
                Category        TEXT,
                LastFetchedUtc  TEXT
            );

            CREATE TABLE IF NOT EXISTS Items (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                FeedId          INTEGER NOT NULL REFERENCES Feeds(Id) ON DELETE CASCADE,
                ExternalId      TEXT    NOT NULL,
                Title           TEXT    NOT NULL,
                Author          TEXT,
                Summary         TEXT,
                Content         TEXT,
                Link            TEXT,
                PublishedUtc    TEXT,
                IsRead          INTEGER NOT NULL DEFAULT 0,
                UNIQUE (FeedId, ExternalId)
            );

            CREATE INDEX IF NOT EXISTS IX_Items_FeedId ON Items (FeedId);
            """;
        await command.ExecuteNonQueryAsync(ct);

        await EnsureColumnAsync(connection, "Items", "Content", "TEXT", ct);
        await EnsureColumnAsync(connection, "Feeds", "Category", "TEXT", ct);
    }

    /// <summary>Adds a column to an existing table if it isn't already present (lightweight migration).</summary>
    private static async Task EnsureColumnAsync(
        SqliteConnection connection, string table, string column, string type, CancellationToken ct)
    {
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await check.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type};";
        await alter.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Feed>> GetFeedsAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Title, FeedUrl, SiteUrl, Category, LastFetchedUtc FROM Feeds ORDER BY Title COLLATE NOCASE;";

        var feeds = new List<Feed>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            feeds.Add(new Feed
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                FeedUrl = reader.GetString(2),
                SiteUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                LastFetchedUtc = ReadDate(reader, 5)
            });
        }
        return feeds;
    }

    public async Task<Feed> AddFeedAsync(string feedUrl, string title, string? siteUrl, string? category, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Feeds (Title, FeedUrl, SiteUrl, Category)
            VALUES ($title, $url, $site, $category)
            ON CONFLICT (FeedUrl) DO UPDATE SET
                Title = excluded.Title,
                SiteUrl = excluded.SiteUrl,
                Category = excluded.Category
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$url", feedUrl);
        command.Parameters.AddWithValue("$site", (object?)siteUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);

        var id = (long)(await command.ExecuteScalarAsync(ct))!;
        return new Feed { Id = id, Title = title, FeedUrl = feedUrl, SiteUrl = siteUrl, Category = category };
    }

    public async Task SetCategoryAsync(long feedId, string? category, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Feeds SET Category = $category WHERE Id = $id;";
        command.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", feedId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveFeedAsync(long feedId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Feeds WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", feedId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SetLastFetchedAsync(long feedId, DateTimeOffset whenUtc, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Feeds SET LastFetchedUtc = $when WHERE Id = $id;";
        command.Parameters.AddWithValue("$when", FormatDate(whenUtc));
        command.Parameters.AddWithValue("$id", feedId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<FeedItem>> GetItemsAsync(long? feedId = null, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.Id, i.FeedId, i.ExternalId, i.Title, i.Author, i.Summary, i.Link, i.PublishedUtc, i.IsRead, f.Title, i.Content
            FROM Items i
            JOIN Feeds f ON f.Id = i.FeedId
            WHERE ($feedId IS NULL OR i.FeedId = $feedId)
            ORDER BY (i.PublishedUtc IS NULL), i.PublishedUtc DESC, i.Id DESC
            LIMIT 500;
            """;
        command.Parameters.AddWithValue("$feedId", (object?)feedId ?? DBNull.Value);

        var items = new List<FeedItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new FeedItem
            {
                Id = reader.GetInt64(0),
                FeedId = reader.GetInt64(1),
                ExternalId = reader.GetString(2),
                Title = reader.GetString(3),
                Author = reader.IsDBNull(4) ? null : reader.GetString(4),
                Summary = reader.IsDBNull(5) ? null : reader.GetString(5),
                Link = reader.IsDBNull(6) ? null : reader.GetString(6),
                PublishedUtc = ReadDate(reader, 7),
                IsRead = reader.GetInt64(8) != 0,
                FeedTitle = reader.GetString(9),
                Content = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }
        return items;
    }

    public async Task<int> UpsertItemsAsync(long feedId, IEnumerable<FetchedItem> items, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO Items (FeedId, ExternalId, Title, Author, Summary, Content, Link, PublishedUtc, IsRead)
            VALUES ($feedId, $extId, $title, $author, $summary, $content, $link, $published, 0);
            """;

        command.Parameters.AddWithValue("$feedId", feedId);
        var pExtId = command.Parameters.Add("$extId", SqliteType.Text);
        var pTitle = command.Parameters.Add("$title", SqliteType.Text);
        var pAuthor = command.Parameters.Add("$author", SqliteType.Text);
        var pSummary = command.Parameters.Add("$summary", SqliteType.Text);
        var pContent = command.Parameters.Add("$content", SqliteType.Text);
        var pLink = command.Parameters.Add("$link", SqliteType.Text);
        var pPublished = command.Parameters.Add("$published", SqliteType.Text);

        var newCount = 0;
        foreach (var item in items)
        {
            pExtId.Value = item.ExternalId;
            pTitle.Value = item.Title;
            pAuthor.Value = (object?)item.Author ?? DBNull.Value;
            pSummary.Value = (object?)item.Summary ?? DBNull.Value;
            pContent.Value = (object?)item.Content ?? DBNull.Value;
            pLink.Value = (object?)item.Link ?? DBNull.Value;
            pPublished.Value = item.PublishedUtc.HasValue ? FormatDate(item.PublishedUtc.Value) : DBNull.Value;
            newCount += await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return newCount;
    }

    public async Task MarkReadAsync(long itemId, bool isRead, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Items SET IsRead = $read WHERE Id = $id;";
        command.Parameters.AddWithValue("$read", isRead ? 1 : 0);
        command.Parameters.AddWithValue("$id", itemId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, int>> GetUnreadCountsAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FeedId, COUNT(*) FROM Items WHERE IsRead = 0 GROUP BY FeedId;";

        var counts = new Dictionary<long, int>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            counts[reader.GetInt64(0)] = reader.GetInt32(1);
        return counts;
    }

    private static string FormatDate(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, UtcStyles);
}
