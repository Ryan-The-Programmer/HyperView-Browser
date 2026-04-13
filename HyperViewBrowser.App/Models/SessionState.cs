namespace HyperViewBrowser.App.Models;

public class SessionState
{
    public List<string> OpenTabs { get; set; } = [];
    public int SelectedTabIndex { get; set; }
}
