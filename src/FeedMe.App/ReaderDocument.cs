using System.Net;

namespace FeedMe.App;

/// <summary>
/// Builds a clean, theme-aware "reader mode" HTML document from a feed item's stored content,
/// for display in the WebView via NavigateToString.
/// </summary>
internal static class ReaderDocument
{
    public static string ForItem(string title, string meta, string? contentHtml)
    {
        var body = string.IsNullOrWhiteSpace(contentHtml)
            ? "<p class=\"muted\">This feed didn't include article text. Switch to the web page or open it in your browser.</p>"
            : contentHtml;

        return Template
            .Replace("__TITLE__", WebUtility.HtmlEncode(title))
            .Replace("__META__", WebUtility.HtmlEncode(meta))
            .Replace("__BODY__", body);
    }

    private const string Template =
        """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          :root { color-scheme: light dark; }
          html, body { margin: 0; }
          body {
            font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            line-height: 1.7; font-size: 17px; padding: 30px 34px;
            background: #ffffff; color: #1a1a1f;
          }
          .wrap { max-width: 720px; margin: 0 auto; }
          h1 { font-size: 1.95em; line-height: 1.25; margin: 0 0 .15em; }
          h2, h3 { line-height: 1.3; }
          .meta { opacity: .6; font-size: .85em; margin: 0 0 1.6em; }
          .muted { opacity: .6; }
          img, video, iframe { max-width: 100%; height: auto; border-radius: 10px; }
          figure { margin: 1.2em 0; }
          figcaption { opacity: .6; font-size: .8em; margin-top: .4em; }
          a { color: #2563eb; text-decoration: none; }
          a:hover { text-decoration: underline; }
          pre { padding: 14px; overflow: auto; border-radius: 8px; background: #f1f1f4; }
          code { padding: .1em .3em; border-radius: 4px; background: #f1f1f4; }
          blockquote { border-left: 3px solid #3b82f6; margin: 1em 0; padding: .2em 0 .2em 1em; opacity: .85; }
          hr { border: none; border-top: 1px solid rgba(128,128,128,.25); margin: 1.5em 0; }
          @media (prefers-color-scheme: dark) {
            body { background: #1b1b20; color: #e7e7ec; }
            a { color: #6aa6ff; }
            pre, code { background: #26262e; }
          }
        </style>
        </head>
        <body>
          <div class="wrap">
            <h1>__TITLE__</h1>
            <div class="meta">__META__</div>
            __BODY__
          </div>
        </body>
        </html>
        """;
}
