namespace SimurghDashboard.Services;

/// <summary>
/// Represents a platform-agnostic RSS feed item.
/// </summary>
public class RssItemModel
{
    public string Author { get; set; }
    public string Title { get; set; }
    public string Description{ get; set; }
    public string PublishDate { get; set; }
    public string Link { get; set; }
}