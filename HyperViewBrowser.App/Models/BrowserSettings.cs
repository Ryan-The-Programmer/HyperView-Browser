namespace HyperViewBrowser.App.Models;

public class BrowserSettings
{
    public string Homepage { get; set; } = "https://www.bing.com";
    public string Theme { get; set; } = "System";
    public string UserAgent { get; set; } = string.Empty;
    public bool EnableSuggestions { get; set; } = true;
    public bool EnableContextMenu { get; set; } = true;
    public bool EnableJavascript { get; set; } = true;
    public bool AskForPermissions { get; set; } = true;
}
