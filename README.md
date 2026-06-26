# FeedMe

A lightweight desktop RSS/Atom reader that pulls your feeds together into one clean overview. Built with Avalonia and SukiUI on .NET 10, with local SQLite storage — no account, no server, no hosting.

## Features

- **Combined overview** — all your feeds in one place, newest first, with unread counts.
- **Categories** — group feeds into sections in the sidebar; assign on add, via right-click → *Move to category…*, or from imported OPML folders.
- **Reading pane** — read articles in-app via a native WebView, with a **Reader ⇄ Web** toggle: clean reader-mode (rendered from the feed's content, no ads/clutter) or the live web page.
- **OPML import** — bring in your subscriptions from a `.opml` file or straight from the clipboard.
- **Auto de-duplication** — refreshing only adds genuinely new items and preserves read state.
- **Robust** — single-instance guard and graceful WebView failure handling so a flaky browser component can't crash the app.

## Tech stack

| Area | Choice |
| --- | --- |
| UI | [Avalonia 12](https://avaloniaui.net/) + [SukiUI](https://github.com/kikipoulet/SukiUI) |
| MVVM | CommunityToolkit.Mvvm |
| Feed parsing | `System.ServiceModel.Syndication` (RSS + Atom) |
| Storage | SQLite (`Microsoft.Data.Sqlite`) |
| In-app browser | `Avalonia.Controls.WebView` (WebView2 on Windows) |
| Tests | xUnit |
| Target | .NET 10 |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Windows with the **Edge WebView2 Runtime** (preinstalled on Windows 11) for the reading pane

## Getting started

```bash
# Build
dotnet build src/FeedMe.slnx

# Run
dotnet run --project src/FeedMe.App

# Test
dotnet test src/FeedMe.Tests
```

On first run FeedMe seeds a few starter feeds so the overview isn't empty.

## Importing feeds

Use **+ Add → Import OPML file…** to pick a `.opml` file, or **Paste OPML** to import OPML text from the clipboard. Category folders in the OPML become FeedMe categories. A small [`sample.opml`](sample.opml) is included to try it out.

## Project structure

```
src/
├─ FeedMe.slnx            Solution
├─ FeedMe.Core/           Models, SQLite repository, feed fetching & services (no UI)
│  ├─ Models/             Feed, FeedItem, fetched DTOs
│  ├─ Data/               IFeedRepository + SqliteFeedRepository
│  └─ Services/           FeedFetcher, FeedService, OpmlParser, HtmlText
├─ FeedMe.App/            Avalonia + SukiUI desktop app (MVVM)
│  ├─ ViewModels/
│  └─ Views/
└─ FeedMe.Tests/          xUnit tests for Core
```

The app talks to the data layer only through `IFeedRepository`, keeping storage swappable.

## Data location

Your feeds, items, and read state live in a single SQLite file at:

```
%AppData%\FeedMe\feedme.db
```

Errors that are caught and recovered from are logged to `%AppData%\FeedMe\feedme.log`.
