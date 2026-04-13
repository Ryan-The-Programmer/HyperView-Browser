namespace HyperViewBrowser.App.Models;

public class DownloadItem
{
    public string FileName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long TotalBytesToReceive { get; set; }
    public bool Completed { get; set; }

    public double Progress => TotalBytesToReceive == 0 ? 0 : (double)BytesReceived / TotalBytesToReceive * 100;
}
