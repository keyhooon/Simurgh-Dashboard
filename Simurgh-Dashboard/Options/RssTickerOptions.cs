using System.ComponentModel.DataAnnotations;

namespace SimurghDashboard.Options;

public sealed class RssTickerOptions
{
    public const string SectionName = "RssTicker";

    /// <summary>RSS feed URLs, fetched in parallel.</summary>
    [Required, MinLength(1)]
    public string[] FeedUrls { get; set; } =
    [
        "https://www.irna.ir/rss/tp/14",
        "https://www.mehrnews.com/rss/dp/18"
    ];

    /// <summary>How often (in milliseconds) to re-fetch all feeds.</summary>
    [Range(60_000, int.MaxValue)]
    public int RefreshIntervalMs { get; set; } = 600_000; // 10 min

    /// <summary>Pixels-per-second scroll rate for the ticker strip.</summary>
    [Range(1.0, 1000.0)]
    public double ScrollSpeed { get; set; } = 50.0;
}