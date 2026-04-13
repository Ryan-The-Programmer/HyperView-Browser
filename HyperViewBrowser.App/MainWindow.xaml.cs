using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperViewBrowser.App.Models;
using HyperViewBrowser.App.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HyperViewBrowser.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly StorageService _storage = new();
    private readonly AddressBarService _addressBar = new();
    private readonly ObservableCollection<BookmarkItem> _bookmarks;
    private readonly ObservableCollection<HistoryItem> _history;
    private readonly ObservableCollection<DownloadItem> _downloads;
    private BrowserSettings _settings;
    private bool _isFullscreen;

    public ObservableCollection<BrowserTab> Tabs { get; } = [];

    private BrowserTab? _selectedTab;
    public BrowserTab? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value) return;
            _selectedTab = value;
            OnPropertyChanged();
            if (value != null)
            {
                AddressBox.Text = value.Address;
                UpdateLockIcon(value.Address);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings = _storage.LoadSettings();
        _bookmarks = new ObservableCollection<BookmarkItem>(_storage.LoadBookmarks());
        _history = new ObservableCollection<HistoryItem>(_storage.LoadHistory());
        _downloads = new ObservableCollection<DownloadItem>(_storage.LoadDownloads());

        ApplyTheme(_settings.Theme);
        ConfigureHotkeys();
        RestoreSessionOrStart();
    }

    private void RestoreSessionOrStart()
    {
        var session = _storage.LoadSession();
        if (session.OpenTabs.Count == 0)
        {
            CreateTab("about:newtab");
            return;
        }

        foreach (var url in session.OpenTabs) CreateTab(url);
        if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Tabs.Count)
        {
            SelectedTab = Tabs[session.SelectedTabIndex];
            Tabs.SelectedIndex = session.SelectedTabIndex;
        }
    }

    private async void WebHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl host || host.DataContext is not BrowserTab tab || tab.WebView != null) return;

        var userData = tab.IsIncognito
            ? Path.Combine(Path.GetTempPath(), $"HyperViewBrowser-Incognito-{tab.Id}")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyperViewBrowser", "webview-data");

        var environment = await CoreWebView2Environment.CreateAsync(null, userData);
        var webView = new WebView2();
        host.Content = webView;
        tab.WebView = webView;

        await webView.EnsureCoreWebView2Async(environment);
        ApplySettingsToWebView(tab);

        webView.NavigationStarting += (_, _) => tab.IsLoading = true;
        webView.NavigationCompleted += (_, e) =>
        {
            tab.IsLoading = false;
            if (!e.IsSuccess)
            {
                webView.NavigateToString($"<html><body style='font-family:sans-serif;background:#161b22;color:white;padding:20px;'><h2>Page failed to load</h2><p>{e.WebErrorStatus}</p></body></html>");
                return;
            }

            tab.Address = webView.Source?.ToString() ?? tab.Address;
            tab.Title = webView.CoreWebView2.DocumentTitle;
            AddressBox.Text = tab.Address;
            UpdateLockIcon(tab.Address);
            if (!tab.IsIncognito)
            {
                _history.Insert(0, new HistoryItem { Title = tab.Title, Url = tab.Address, VisitedAt = DateTimeOffset.UtcNow });
                _storage.SaveHistory([.. _history.Take(2000)]);
            }
        };

        webView.CoreWebView2.DocumentTitleChanged += (_, _) => tab.Title = webView.CoreWebView2.DocumentTitle;
        webView.CoreWebView2.FaviconChanged += (_, _) => tab.FaviconUrl = webView.CoreWebView2.FaviconUri;

        webView.CoreWebView2.ContextMenuRequested += (_, args) =>
        {
            if (!_settings.EnableContextMenu) args.Handled = true;
        };

        webView.CoreWebView2.PermissionRequested += (_, args) =>
        {
            if (!_settings.AskForPermissions)
            {
                args.State = CoreWebView2PermissionState.Allow;
                return;
            }

            var result = MessageBox.Show($"Allow {args.PermissionKind} for {args.Uri}?", "Permission", MessageBoxButton.YesNo);
            args.State = result == MessageBoxResult.Yes ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        };

        webView.CoreWebView2.DownloadStarting += (_, args) =>
        {
            var item = args.DownloadOperation;
            var entry = new DownloadItem
            {
                FileName = Path.GetFileName(item.ResultFilePath),
                TargetPath = item.ResultFilePath,
                Uri = item.Uri
            };

            _downloads.Insert(0, entry);
            item.BytesReceivedChanged += (_, _) =>
            {
                entry.BytesReceived = item.BytesReceived;
                entry.TotalBytesToReceive = item.TotalBytesToReceive;
            };
            item.StateChanged += (_, _) =>
            {
                entry.Completed = item.State == CoreWebView2DownloadState.Completed;
                _storage.SaveDownloads([.. _downloads.Take(500)]);
            };
        };

        if (tab.Address == "about:newtab") webView.NavigateToString(BuildNewTabPage());
        else webView.Source = new Uri(_addressBar.NormalizeToUrl(tab.Address));
    }

    private void ApplySettingsToWebView(BrowserTab tab)
    {
        var settings = tab.WebView!.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = _settings.EnableContextMenu;
        settings.IsScriptEnabled = _settings.EnableJavascript;
        if (!string.IsNullOrWhiteSpace(_settings.UserAgent))
        {
            tab.WebView.CoreWebView2.Settings.UserAgent = _settings.UserAgent;
        }
    }

    private string BuildNewTabPage()
    {
        var tops = string.Join("", _bookmarks.Take(8).Select(b => $"<li><a href='{b.Url}'>{b.Title}</a></li>"));
        return $"<html><body style='font-family:Segoe UI;background:#0d1117;color:#e6edf3;padding:24px;'><h1>New Tab</h1><p>HyperView Browser</p><h3>Bookmarks</h3><ul>{tops}</ul></body></html>";
    }

    private void CreateTab(string url, bool incognito = false)
    {
        var tab = new BrowserTab { Address = url, IsIncognito = incognito, Title = incognito ? "Incognito" : "New Tab" };
        Tabs.Add(tab);
        SelectedTab = tab;
        Tabs.SelectedItem = tab;
    }

    private void CloseTab(BrowserTab tab)
    {
        if (Tabs.Count == 1) return;
        tab.WebView?.Dispose();
        Tabs.Remove(tab);
        SelectedTab = Tabs.LastOrDefault();
    }

    private void NavigateCurrent(string input)
    {
        if (SelectedTab?.WebView == null) return;
        var url = _addressBar.NormalizeToUrl(input);
        SelectedTab.Address = url;
        SelectedTab.WebView.Source = new Uri(url);
    }

    private void ConfigureHotkeys()
    {
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => CreateTab(_settings.Homepage)), new KeyGesture(Key.T, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => { if (SelectedTab != null) CloseTab(SelectedTab); }), new KeyGesture(Key.W, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => AddressBox.Focus()), new KeyGesture(Key.L, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Find_Click(this, new RoutedEventArgs())), new KeyGesture(Key.F, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Fullscreen_Click(this, new RoutedEventArgs())), new KeyGesture(Key.F11)));
    }

    private void SaveSession()
    {
        _storage.SaveSession(new SessionState { OpenTabs = Tabs.Select(t => t.Address).ToList(), SelectedTabIndex = SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab) });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSession();
        _storage.SaveBookmarks([.. _bookmarks]);
        _storage.SaveDownloads([.. _downloads]);
        base.OnClosing(e);
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (SelectedTab?.WebView?.CanGoBack == true) SelectedTab.WebView.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (SelectedTab?.WebView?.CanGoForward == true) SelectedTab.WebView.GoForward(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => SelectedTab?.WebView?.Reload();
    private void Stop_Click(object sender, RoutedEventArgs e) => SelectedTab?.WebView?.CoreWebView2?.Stop();
    private void Home_Click(object sender, RoutedEventArgs e) => NavigateCurrent(_settings.Homepage);
    private void NewTab_Click(object sender, RoutedEventArgs e) => CreateTab(_settings.Homepage);
    private void NewIncognitoTab_Click(object sender, RoutedEventArgs e) => CreateTab(_settings.Homepage, true);
    private void CloseTab_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is BrowserTab tab) CloseTab(tab); }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is BrowserTab tab)
        {
            SelectedTab = tab;
            AddressBox.Text = tab.Address;
            LoadSuggestions(tab.Address);
        }
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateCurrent(AddressBox.Text);
            e.Handled = true;
        }
        else
        {
            LoadSuggestions(AddressBox.Text);
        }
    }

    private void AddressBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => LoadSuggestions(AddressBox.Text);

    private void LoadSuggestions(string text)
    {
        if (!_settings.EnableSuggestions) return;
        AddressBox.ItemsSource = _addressBar.BuildSuggestions(text, _history, _bookmarks).ToList();
        AddressBox.IsDropDownOpen = AddressBox.HasFocus && AddressBox.Items.Count > 0;
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTab == null) return;
        if (_bookmarks.Any(x => x.Url.Equals(SelectedTab.Address, StringComparison.OrdinalIgnoreCase))) return;
        _bookmarks.Add(new BookmarkItem { Title = SelectedTab.Title, Url = SelectedTab.Address });
        _storage.SaveBookmarks([.. _bookmarks]);
    }

    private void BookmarksMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        while (BookmarksMenu.Items.Count > 1) BookmarksMenu.Items.RemoveAt(1);

        foreach (var bookmark in _bookmarks)
        {
            var item = new MenuItem { Header = bookmark.Title, ToolTip = bookmark.Url };
            item.Click += (_, _) => NavigateCurrent(bookmark.Url);
            BookmarksMenu.Items.Add(item);
        }

        if (_bookmarks.Count > 0)
        {
            BookmarksMenu.Items.Add(new Separator());
            var clear = new MenuItem { Header = "Remove all bookmarks" };
            clear.Click += (_, _) =>
            {
                _bookmarks.Clear();
                _storage.SaveBookmarks([]);
            };
            BookmarksMenu.Items.Add(clear);
        }
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, _history.Take(100).Select(h => $"{h.VisitedAt.LocalDateTime:g} {h.Title} - {h.Url}"));
        MessageBox.Show(string.IsNullOrEmpty(text) ? "No history yet." : text, "History Viewer");
    }

    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, _downloads.Take(100).Select(d => $"{d.FileName} {d.Progress:N0}%"));
        if (MessageBox.Show(string.IsNullOrEmpty(text) ? "No downloads yet." : text + "\n\nOpen downloads folder?", "Download Manager", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings);
        if (dialog.ShowDialog() != true) return;

        _settings = dialog.Settings;
        _storage.SaveSettings(_settings);
        ApplyTheme(_settings.Theme);

        foreach (var tab in Tabs.Where(x => x.WebView != null)) ApplySettingsToWebView(tab);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) { if (SelectedTab?.WebView != null) SelectedTab.WebView.ZoomFactor += 0.1; }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { if (SelectedTab?.WebView != null) SelectedTab.WebView.ZoomFactor -= 0.1; }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        _isFullscreen = !_isFullscreen;
        WindowStyle = _isFullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        WindowState = _isFullscreen ? WindowState.Maximized : WindowState.Normal;
    }

    private async void Find_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedTab?.WebView?.CoreWebView2 == null) return;
        var term = Microsoft.VisualBasic.Interaction.InputBox("Find in page:", "Find", "");
        if (!string.IsNullOrWhiteSpace(term)) await SelectedTab.WebView.CoreWebView2.ExecuteScriptAsync($"window.find({JsonSerializer.Serialize(term)});");
    }

    private void UpdateLockIcon(string url)
    {
        HttpsLock.Text = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "🔒" : "🔓";
    }

    private void ApplyTheme(string theme)
    {
        var dark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        Background = dark ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
    }

    private void Tabs_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (Tabs.SelectedItem is BrowserTab tab)
        {
            DragDrop.DoDragDrop(Tabs, tab, DragDropEffects.Move);
        }
    }

    private void Tabs_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(BrowserTab))) return;
        var sourceTab = (BrowserTab)e.Data.GetData(typeof(BrowserTab))!;
        var target = ((FrameworkElement)e.OriginalSource).DataContext as BrowserTab;
        if (target == null || sourceTab == target) return;

        var oldIndex = Tabs.IndexOf(sourceTab);
        var newIndex = Tabs.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;

        Tabs.Move(oldIndex, newIndex);
        Tabs.SelectedItem = sourceTab;
    }

    private void ClearBrowsingData_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        _storage.ClearBrowsingData();
        foreach (var tab in Tabs.Where(t => t.WebView?.CoreWebView2 != null))
        {
            tab.WebView!.CoreWebView2.Profile.ClearBrowsingDataAsync();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}
