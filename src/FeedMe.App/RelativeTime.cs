namespace FeedMe.App;

/// <summary>Formats timestamps as compact, human-friendly relative strings ("2h ago").</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset time)
    {
        var delta = DateTimeOffset.UtcNow - time.ToUniversalTime();
        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero;

        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays}d ago";
        return time.ToLocalTime().ToString("MMM d");
    }
}
