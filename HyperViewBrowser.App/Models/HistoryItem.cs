namespace HyperViewBrowser.App.Models;

public class HistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset VisitedAt { get; set; } = DateTimeOffset.UtcNow;
}
