using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimurghDashboard.Services.Ticker.Options
{
    /// <summary>
    /// Configuration options for the RSS worker.
    /// This would typically be bound to appsettings.json.
    /// </summary>
    public class RssWorkerOptions
    {


        public static string SectionName = "RssWorker";
        // A list of RSS/Atom feed URLs to poll.
        public List<string> FeedUrls { get; set; } = [
                                                         "https://www.yjc.ir/fa/rss/7/57",
                                                         "https://www.yjc.ir/fa/rss/7/161",
                                                         "https://www.yjc.ir/fa/rss/7/83"];

        // How often to check for new items (e.g., every 5 minutes).
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

        // How long an RSS item should live in the ticker before it expires.
        public TimeSpan ItemTtl { get; set; } = TimeSpan.FromHours(2);
    }
}
