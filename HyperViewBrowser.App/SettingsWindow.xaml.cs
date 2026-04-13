using System.Windows;
using System.Windows.Controls;
using HyperViewBrowser.App.Models;

namespace HyperViewBrowser.App;

public partial class SettingsWindow : Window
{
    public BrowserSettings Settings { get; private set; }

    public SettingsWindow(BrowserSettings current)
    {
        InitializeComponent();
        Settings = new BrowserSettings
        {
            Homepage = current.Homepage,
            Theme = current.Theme,
            UserAgent = current.UserAgent,
            EnableSuggestions = current.EnableSuggestions,
            EnableContextMenu = current.EnableContextMenu,
            EnableJavascript = current.EnableJavascript,
            AskForPermissions = current.AskForPermissions
        };

        HomepageBox.Text = Settings.Homepage;
        ThemeBox.SelectedIndex = Settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        UserAgentBox.Text = Settings.UserAgent;
        SuggestionsCheck.IsChecked = Settings.EnableSuggestions;
        ContextMenuCheck.IsChecked = Settings.EnableContextMenu;
        JsCheck.IsChecked = Settings.EnableJavascript;
        PermissionCheck.IsChecked = Settings.AskForPermissions;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.Homepage = HomepageBox.Text.Trim();
        Settings.Theme = ((ThemeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "System";
        Settings.UserAgent = UserAgentBox.Text.Trim();
        Settings.EnableSuggestions = SuggestionsCheck.IsChecked ?? true;
        Settings.EnableContextMenu = ContextMenuCheck.IsChecked ?? true;
        Settings.EnableJavascript = JsCheck.IsChecked ?? true;
        Settings.AskForPermissions = PermissionCheck.IsChecked ?? true;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
