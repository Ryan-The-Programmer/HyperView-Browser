using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.Wpf;

namespace HyperViewBrowser.App.Models;

public class BrowserTab : INotifyPropertyChanged
{
    private string _title = "New Tab";
    private string _address = "about:newtab";
    private bool _isLoading;
    private string? _faviconUrl;

    public Guid Id { get; } = Guid.NewGuid();
    public bool IsIncognito { get; set; }
    public WebView2? WebView { get; set; }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Address
    {
        get => _address;
        set => SetField(ref _address, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string? FaviconUrl
    {
        get => _faviconUrl;
        set => SetField(ref _faviconUrl, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
