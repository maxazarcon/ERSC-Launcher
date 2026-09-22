using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ERSC.Launcher.Core;
using Microsoft.Win32;

namespace ERSC.Launcher;

public sealed class MainWindow : Window
{
    private static readonly Brush BackgroundBrush = Color("#141713");
    private static readonly Brush PanelBrush = Color("#20251F");
    private static readonly Brush TextBrush = Color("#EEEDE3");
    private static readonly Brush MutedBrush = Color("#AAB3A3");
    private static readonly Brush GoldBrush = Color("#D7B66B");
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERSC Launcher");
    private readonly StateStore _store;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private readonly TextBlock _gameText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _version = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _settings = new() { Orientation = Orientation.Vertical };
    private readonly Button _install = new();
    private readonly Button _save = new();
    private readonly Button _launch = new();
    private readonly Button _check = new();
    private readonly List<(IniEntry Entry, Func<string> Read)> _editors = [];
    private LauncherState _state;
    private string? _game;
    private ModRelease? _release;
    private ModAsset? _asset;
    private string? _stage;
    private IniDocument? _ini;
    private bool _busy;
    private bool _current;

    public MainWindow()
    {
        _store = new StateStore(_dataDir);
        _state = _store.Load();
        Title = "Seamless Co-Op Launcher";
        Width = 860; Height = 760; MinWidth = 680; MinHeight = 540;
        Background = BackgroundBrush; Foreground = TextBrush;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildScreen();
        Loaded += async (_, _) => await InitializeAsync();
        Closed += (_, _) => { _http.Dispose(); CleanupStage(); };
    }

    private UIElement BuildScreen()
    {
        var root = new DockPanel { Margin = new Thickness(28, 24, 28, 24) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        _save.Content = "Save settings"; _save.Click += (_, _) => SaveSettings();
        _launch.Content = "Launch Seamless Co-Op"; _launch.Click += (_, _) => Launch();
        foreach (var b in new[] { _save, _launch }) { StyleButton(b); b.Margin = new Thickness(8, 0, 0, 0); footer.Children.Add(b); }
        _launch.Background = GoldBrush; _launch.Foreground = BackgroundBrush;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        var body = new StackPanel(); scroll.Content = body;
        body.Children.Add(Label("SEAMLESS CO-OP", 13, GoldBrush, new Thickness(0, 0, 0, 4)));
        body.Children.Add(Label("Your way into the Lands Between", 26, TextBrush, new Thickness(0, 0, 0, 20)));

        var location = Panel(); body.Children.Add(Wrap(location));
        location.Children.Add(Label("Elden Ring game folder", 17, TextBrush, new Thickness(0, 0, 0, 6)));
        _gameText.Foreground = MutedBrush; location.Children.Add(_gameText);
        var locateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        location.Children.Add(locateRow);
        var browse = new Button { Content = "Choose folder" }; StyleButton(browse); browse.Click += async (_, _) => await BrowseAsync(); locateRow.Children.Add(browse);
        _check.Content = "Check updates"; StyleButton(_check); _check.Margin = new Thickness(8, 0, 0, 0); _check.Click += async (_, _) => await CheckAsync(); locateRow.Children.Add(_check);

        var state = Panel(); var stateCard = Wrap(state); stateCard.Margin = new Thickness(0, 14, 0, 0); body.Children.Add(stateCard);
        state.Children.Add(Label("Installation", 17, TextBrush, new Thickness(0, 0, 0, 6)));
        _status.Foreground = MutedBrush; state.Children.Add(_status);
        _version.Foreground = MutedBrush; _version.Margin = new Thickness(0, 5, 0, 0); state.Children.Add(_version);
        _install.Content = "Install Seamless Co-Op"; StyleButton(_install); _install.Margin = new Thickness(0, 14, 0, 0); _install.HorizontalAlignment = HorizontalAlignment.Left;
        _install.Click += async (_, _) => await InstallAsync(); state.Children.Add(_install);

        body.Children.Add(Label("Settings", 20, TextBrush, new Thickness(0, 24, 0, 7)));
        body.Children.Add(Label("Changes are saved to the mod's settings file in your game folder.", 13, MutedBrush, new Thickness(0, 0, 0, 12)));
        body.Children.Add(_settings);
        UpdateButtons();
        return root;
    }

    private async Task InitializeAsync()
    {
        try
        {
            var saved = GameLocator.Validate(_state.SelectedGame);
            var found = GameLocator.FindInstalled();
            await SetGameAsync(saved ?? found.FirstOrDefault());
        }
        catch (Exception ex) { SetStatus("Could not discover Elden Ring: " + ex.Message); }
    }

    private async Task BrowseAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the Elden Ring Game folder" };
        if (dialog.ShowDialog(this) != true) return;
        var game = GameLocator.Validate(dialog.FolderName);
        if (game is null) { MessageBox.Show(this, "Choose the folder containing eldenring.exe, or its parent ELDEN RING folder.", "Invalid game folder"); return; }
        await SetGameAsync(game);
    }

    private async Task SetGameAsync(string? game)
    {
        _game = game; _release = null; _asset = null; _current = false; CleanupStage();
        _gameText.Text = game ?? "No Steam installation found. Choose the folder containing eldenring.exe.";
        _state.SelectedGame = game; _store.Save(_state);
        LoadSettings(); UpdateButtons();
        if (game is not null) await CheckAsync();
        else SetStatus("Select your Elden Ring folder to begin.");
    }

    private async Task CheckAsync()
    {
        if (_game is null || _busy) return;
        _busy = true; UpdateButtons(); SetStatus("Checking published releases…");
        try
        {
            _release = await new GitHubReleases(_http).GetNewestAsync();
            _asset = ReleaseCatalog.SelectZip(_release);
            _current = false;
            if (ModFingerprint.IsInstalled(_game))
            {
                if (_state.Installs.TryGetValue(_game, out var record) && StateStore.Matches(_game, record))
                {
                    _current = record.Tag.Equals(_release.Tag, StringComparison.OrdinalIgnoreCase);
                    SetStatus(_current ? "Seamless Co-Op is up to date." : $"Version {record.Tag} is installed. {_release.Tag} is available.");
                }
                else
                {
                    await PrepareAsync();
                    _current = ModFingerprint.Matches(_game, _stage!);
                    SetStatus(_current ? "The installed mod files match the latest release." : "Seamless Co-Op is installed, but its version cannot be verified. You can install the latest release.");
                }
            }
            else SetStatus("Seamless Co-Op is not installed. Install the latest release to continue.");
            _version.Text = "Latest published release: " + _release.Tag + (_release.Prerelease ? " (beta)" : "");
        }
        catch (Exception ex)
        {
            _release = null; _asset = null; _current = false;
            _version.Text = "Latest version unavailable";
            SetStatus((ModFingerprint.IsInstalled(_game) ? "Installed settings and Launch remain available. " : "") + "Could not check GitHub: " + ex.Message);
        }
        finally { _busy = false; UpdateButtons(); }
    }

    private async Task PrepareAsync()
    {
        if (_asset is null) throw new InvalidOperationException("No release asset was selected.");
        if (_stage is not null && ModFingerprint.IsInstalled(_stage)) return;
        CleanupStage();
        var folder = Path.Combine(_dataDir, "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var zip = Path.Combine(folder, "release.zip");
            await ModPackage.DownloadAsync(_http, _asset, zip);
            var extracted = Path.Combine(folder, "extracted");
            ModPackage.Extract(zip, extracted);
            _stage = extracted;
        }
        catch { Directory.Delete(folder, true); throw; }
    }

    private async Task InstallAsync()
    {
        if (_game is null || _release is null || _busy || _current) return;
        var existing = ModFingerprint.IsInstalled(_game);
        if (existing && MessageBox.Show(this, "Install " + _release.Tag + "? Existing mod files and settings will be backed up before replacement.", "Update Seamless Co-Op", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (Process.GetProcessesByName("eldenring").Length > 0 || Process.GetProcessesByName("ersc_launcher").Length > 0)
        { MessageBox.Show(this, "Close Elden Ring before installing or updating the mod.", "Game is running"); return; }
        _busy = true; UpdateButtons(); SetStatus(existing ? "Updating Seamless Co-Op…" : "Installing Seamless Co-Op…");
        try
        {
            await PrepareAsync();
            var backup = ModInstaller.Install(_game, _stage!, Path.Combine(_dataDir, "backups", SafeFolderName(_game)));
            _state.Installs[_game] = StateStore.Record(_release.Tag, _game, _stage!);
            _store.Save(_state);
            LoadSettings();
            _current = true;
            SetStatus("Installed " + _release.Tag + ". Backup: " + backup);
        }
        catch (Exception ex) { SetStatus("Install failed: " + ex.Message); MessageBox.Show(this, ex.Message, "Install failed", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { CleanupStage(); _busy = false; UpdateButtons(); }
    }

    private void LoadSettings()
    {
        _settings.Children.Clear(); _editors.Clear(); _ini = null;
        if (_game is null || !ModFingerprint.IsInstalled(_game)) { _settings.Children.Add(Label("Install the mod to edit its settings.", 14, MutedBrush, new Thickness(0))); return; }
        try
        {
            _ini = IniDocument.Load(Path.Combine(_game, "SeamlessCoop", "ersc_settings.ini"));
            string? section = null;
            foreach (var entry in _ini.Entries)
            {
                if (section != entry.Section)
                {
                    section = entry.Section;
                    _settings.Children.Add(Label(section.Length == 0 ? "General" : section.Replace('_', ' '), 17, GoldBrush, new Thickness(0, 12, 0, 6)));
                }
                var card = Panel(); var wrapped = Wrap(card); wrapped.Margin = new Thickness(0, 0, 0, 8); _settings.Children.Add(wrapped);
                card.Children.Add(Label(entry.Key.Replace('_', ' '), 14, TextBrush, new Thickness(0, 0, 0, 6)));
                if (entry.Description is not null) card.Children.Add(Label(entry.Description, 12, MutedBrush, new Thickness(0, 0, 0, 7)));
                if (entry.Key.Equals("cooppassword", StringComparison.OrdinalIgnoreCase))
                {
                    var password = new PasswordBox { Password = entry.Value, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = MutedBrush, Padding = new Thickness(8, 5, 8, 5) };
                    password.PasswordChanged += (_, _) => UpdateButtons(); card.Children.Add(password);
                    _editors.Add((entry, () => password.Password));
                }
                else
                {
                    var input = new TextBox { Text = entry.Value, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = MutedBrush, Padding = new Thickness(8, 5, 8, 5) };
                    card.Children.Add(input); _editors.Add((entry, () => input.Text));
                }
            }
        }
        catch (Exception ex) { _settings.Children.Add(Label("Could not read settings: " + ex.Message, 14, MutedBrush, new Thickness(0))); }
    }

    private bool SaveSettings()
    {
        if (_game is null || _ini is null) return false;
        try
        {
            foreach (var (entry, read) in _editors) _ini.SetAt(entry.LineIndex, read());
            _ini.Save(Path.Combine(_game, "SeamlessCoop", "ersc_settings.ini"));
            SetStatus("Settings saved."); UpdateButtons(); return true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }

    private void Launch()
    {
        if (_game is null || !CanLaunch()) return;
        try
        {
            if (!SaveSettings()) return;
            Process.Start(new ProcessStartInfo(Path.Combine(_game, "ersc_launcher.exe")) { WorkingDirectory = _game, UseShellExecute = true });
            SetStatus("Starting Seamless Co-Op…");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not launch", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private bool CanLaunch() => _game is not null && ModFingerprint.IsInstalled(_game) &&
        _editors.Any(e => e.Entry.Key.Equals("cooppassword", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(e.Read()));
    private void UpdateButtons()
    {
        _check.IsEnabled = !_busy && _game is not null;
        _install.IsEnabled = !_busy && _game is not null && _release is not null && !_current;
        _install.Content = _game is not null && ModFingerprint.IsInstalled(_game) ? "Install latest release" : "Install Seamless Co-Op";
        _save.IsEnabled = !_busy && _ini is not null;
        _launch.IsEnabled = !_busy && CanLaunch();
    }
    private void SetStatus(string message) => _status.Text = message;
    private void CleanupStage()
    {
        if (_stage is null) return;
        var folder = Directory.GetParent(_stage)?.FullName;
        _stage = null;
        try { if (folder is not null && Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }
    private static string SafeFolderName(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16];
    private static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static TextBlock Label(string text, double size, Brush color, Thickness margin) => new() { Text = text, FontSize = size, Foreground = color, Margin = margin, TextWrapping = TextWrapping.Wrap };
    private static StackPanel Panel() => new() { Orientation = Orientation.Vertical };
    private static Border Wrap(StackPanel panel) => new() { Background = PanelBrush, Padding = new Thickness(16), Child = panel, CornerRadius = new CornerRadius(4) };
    private static void StyleButton(Button button)
    {
        button.Background = PanelBrush; button.Foreground = TextBrush; button.BorderBrush = GoldBrush;
        button.BorderThickness = new Thickness(1); button.Padding = new Thickness(13, 7, 13, 7); button.Cursor = System.Windows.Input.Cursors.Hand;
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(disabled);
        button.Template = template;
    }
}
