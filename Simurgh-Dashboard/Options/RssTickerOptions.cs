using System.ComponentModel.DataAnnotations;

namespace SimurghDashboard.Options;

/// <summary>
/// Configuration options for the general Ticker system.
/// Renamed from RssTickerOptions to reflect a broader scope.
/// </summary>
public sealed class TickerOptions
{
    public const string SectionName = "Ticker";

    /// <summary>
    /// Feed URLs (like RSS or Atom) to be fetched in parallel.
    /// </summary>
    [Required, MinLength(1)]
    public string[] FeedUrls { get; set; } =
        [
            "https://www.irna.ir/rss/tp/14",
            "https://www.mehrnews.com/rss/dp/18"
        ];

    /// <summary>
    /// How often (in milliseconds) to re-fetch all feeds.
    /// Default is 10 minutes.
    /// </summary>
    [Range(60_000, int.MaxValue)]
    public int RefreshIntervalMs { get; set; } = 600_000;

    /// <summary>
    /// Pixels-per-second scroll rate for the ticker strip.
    /// </summary>
    [Range(1.0, 1000.0)]
    public double ScrollSpeed { get; set; } = 50.0;
}