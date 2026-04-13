using System.IO;
using System.Text.Json;
using HyperViewBrowser.App.Models;

namespace HyperViewBrowser.App.Services;

public class StorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _baseDir;

    public StorageService()
    {
        _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyperViewBrowser");
        Directory.CreateDirectory(_baseDir);
    }

    public string DownloadsPath => Path.Combine(_baseDir, "downloads.json");

    public BrowserSettings LoadSettings() => Load<BrowserSettings>("settings.json") ?? new BrowserSettings();
    public void SaveSettings(BrowserSettings settings) => Save("settings.json", settings);

    public List<BookmarkItem> LoadBookmarks() => Load<List<BookmarkItem>>("bookmarks.json") ?? [];
    public void SaveBookmarks(List<BookmarkItem> bookmarks) => Save("bookmarks.json", bookmarks);

    public List<HistoryItem> LoadHistory() => Load<List<HistoryItem>>("history.json") ?? [];
    public void SaveHistory(List<HistoryItem> history) => Save("history.json", history);

    public List<DownloadItem> LoadDownloads() => Load<List<DownloadItem>>("downloads.json") ?? [];
    public void SaveDownloads(List<DownloadItem> downloads) => Save("downloads.json", downloads);

    public SessionState LoadSession() => Load<SessionState>("session.json") ?? new SessionState();
    public void SaveSession(SessionState session) => Save("session.json", session);

    public void ClearBrowsingData()
    {
        foreach (var file in new[] { "history.json", "session.json" })
        {
            var path = Path.Combine(_baseDir, file);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private T? Load<T>(string file)
    {
        var path = Path.Combine(_baseDir, file);
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
    }

    private void Save<T>(string file, T data)
    {
        var path = Path.Combine(_baseDir, file);
        File.WriteAllText(path, JsonSerializer.Serialize(data, _jsonOptions));
    }
}
