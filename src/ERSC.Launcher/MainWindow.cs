using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ERSC.Launcher.Core;
using Microsoft.Win32;

namespace ERSC.Launcher;

public sealed partial class MainWindow : Window
{
    private const string LauncherRepository = "maxazarcon/ERSC-Launcher";
    internal static readonly Brush BackgroundBrush = Color("#101411");
    internal static readonly Brush PanelBrush = Color("#181C17");
    internal static readonly Brush TextBrush = Color("#ECE6D8");
    internal static readonly Brush MutedBrush = Color("#A4AA9B");
    internal static readonly Brush GoldBrush = Color("#C4AD77");
    internal static readonly Brush DividerBrush = Color("#343B30");
    private static readonly Lazy<Style?> ChoiceStyle = new(() => { try { return (Style)XamlReader.Parse(ChoiceXaml); } catch { return null; } });
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERSC Launcher");
    private readonly StateStore _store;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private readonly TextBlock _gameText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _version = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _launcherUpdateStatus = new() { Text = "Checking for launcher updates…", TextWrapping = TextWrapping.Wrap };
    private readonly Button _restartUpdate = new();
    private readonly StackPanel _settings = new() { Orientation = Orientation.Vertical };
    private readonly Button _install = new();
    private readonly Button _getRelease = new();
    private readonly Button _nexus = new();
    private readonly Button _save = new();
    private readonly Button _launch = new();
    private readonly Button _check = new();
    private readonly Button _steam = new();
    private readonly Button _quit = new();
    private readonly WrapPanel _hints = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Grid _layers = new();
    private readonly List<Overlay> _overlays = [];
    private readonly bool _gamepadMode;
    private readonly FocusRing _focusRing;
    private Gamepad? _gamepad;
    private readonly UIElement _main;
    private ScrollViewer? _scroll;
    private PasswordBox? _password;
    private int _comboOriginal = -1;
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 3, Margin = new Thickness(0, 12, 0, 0), Foreground = GoldBrush, Background = BackgroundBrush, BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
    private readonly List<(IniEntry Entry, Func<string> Read)> _editors = [];
    private LauncherState _state;
    private string? _game;
    private IReadOnlyList<ModRelease>? _releases;
    private ModRelease? _release;
    private string? _found;
    private string? _stage;
    private IniDocument? _ini;
    private bool _busy;
    private bool _steamBusy;
    private bool _current;
    private string? _launcherPayload;
    private string? _launcherUpdateTag;
    private string? _modDownloadUrl;

    public MainWindow() : this(false) { }

    /// <param name="gamepadMode">Full-screen, larger layout for Big Picture mode. The launcher closes after starting the game.</param>
    public MainWindow(bool gamepadMode)
    {
        _gamepadMode = gamepadMode;
        _store = new StateStore(_dataDir);
        _state = _store.Load();
        Title = SteamShortcuts.AppName;
        Width = 1440; Height = 900; MinWidth = 680; MinHeight = 540;
        Background = BackgroundBrush; Foreground = TextBrush;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        // The gold focus ring replaces the default dotted focus rectangle.
        foreach (var type in new[] { typeof(Button), typeof(CheckBox), typeof(Slider), typeof(TextBox), typeof(PasswordBox), typeof(ComboBox) })
            Resources.Add(type, new Style(type) { Setters = { new Setter(FocusVisualStyleProperty, null) } });
        _main = BuildScreen();
        _layers.Children.Add(_main);
        _focusRing = new FocusRing(_layers);
        if (gamepadMode)
        {
            WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; ResizeMode = ResizeMode.NoResize;
            // The companion layout uses controller-sized targets without scaling the desktop shell.
        }
        Content = _layers;
        Loaded += async (_, _) =>
        {
            _gamepad = new Gamepad(Dispatcher);
            _gamepad.Pressed += HandleGamepad;
            _gamepad.ConnectionChanged += _ => UpdateHints();
            _gamepad.Scrolled += axis => _scroll?.ScrollToVerticalOffset(_scroll.VerticalOffset - axis * 24);
            await ShowPreviousUpdateErrorsAsync();
            _ = CheckLauncherAsync();
            await InitializeAsync();
            if (!_gamepadMode) return;
            if (!_main.IsKeyboardFocusWithin && _overlays.Count == 0) FocusPreferred();
            _focusRing.Show();
        };
        Activated += (_, _) => { if (_gamepad is not null) _gamepad.Enabled = true; };
        Deactivated += (_, _) => { if (_gamepad is not null) _gamepad.Enabled = false; };
        Closed += (_, _) => { _gamepad?.Dispose(); _http.Dispose(); CleanupStage(); };
    }

    private UIElement BuildScreen() => BuildCompanionScreen();

    private async Task CheckLauncherAsync()
    {
        var installed = typeof(App).Assembly.GetName().Version ?? new Version(1, 2, 0);
        var current = new Version(installed.Major, installed.Minor, installed.Build);
        _launcherUpdateStatus.Text = $"Launcher {current}: checking for updates…";
        try
        {
            var release = await new GitHubLauncherReleases(_http, LauncherRepository).GetNewerAsync(current);
            if (release is null) { _launcherUpdateStatus.Text = $"Launcher {current} is up to date."; return; }
            var asset = LauncherUpdates.SelectAsset(release, LauncherRepository);
            var folder = Path.Combine(_dataDir, "launcher-updates", release.Tag);
            Directory.CreateDirectory(folder);
            var payload = Path.Combine(folder, asset.Name);
            _launcherUpdateStatus.Text = $"Downloading launcher {release.Tag}…";
            await LauncherUpdates.DownloadAsync(_http, asset, payload);
            _launcherPayload = payload; _launcherUpdateTag = release.Tag;
            _launcherUpdateStatus.Text = $"Launcher {release.Tag} is ready. Restart to update.";
            _restartUpdate.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            _launcherUpdateStatus.Text = $"Launcher update check unavailable: {ex.Message}";
        }
    }

    private async Task ShowPreviousUpdateErrorsAsync()
    {
        var updates = Path.Combine(_dataDir, "launcher-updates");
        if (!Directory.Exists(updates)) return;
        foreach (var folder in Directory.EnumerateDirectories(updates))
        {
            string? message = null;
            try { message = LauncherUpdateErrors.Take(folder); }
            catch { }
            if (message is null) continue;
            _launcherUpdateStatus.Text = message;
            await AlertAsync("Launcher update failed", message);
        }
    }

    private async Task RestartForLauncherUpdateAsync()
    {
        if (_launcherPayload is null || _launcherUpdateTag is null) return;
        if (_ini is not null && _editors.Any(e => e.Read() != e.Entry.Value))
        {
            var choice = await AskAsync("Launcher update", "Save changed mod settings before restarting?", ["Save", "Don't save", "Cancel"]);
            if (choice == 2 || (choice == 0 && !SaveSettings())) return;
        }
        try
        {
            var target = Environment.ProcessPath ?? throw new IOException("Could not locate the running launcher.");
            var folder = Path.GetDirectoryName(_launcherPayload)!;
            var probe = target + ".writecheck-" + Guid.NewGuid().ToString("N");
            try { using var stream = File.Create(probe); } finally { if (File.Exists(probe)) File.Delete(probe); }
            var helper = Path.Combine(folder, "updater.exe");
            File.Copy(target, helper, true);
            var info = new ProcessStartInfo(helper) { WorkingDirectory = folder, UseShellExecute = false };
            foreach (var arg in new[] { "--apply-launcher-update", target, _launcherPayload, Path.Combine(folder, "previous.exe"), Environment.ProcessId.ToString(CultureInfo.InvariantCulture) })
                info.ArgumentList.Add(arg);
            Process.Start(info);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _launcherUpdateStatus.Text = $"Could not start update: {ex.Message}";
            await AlertAsync("Could not update launcher", ex.Message);
        }
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
        if (_busy) return;
        var dialog = new OpenFolderDialog { Title = "Choose the Elden Ring Game folder" };
        if (dialog.ShowDialog(this) != true) return;
        var game = GameLocator.Validate(dialog.FolderName);
        if (game is null) { await AlertAsync("Invalid game folder", "Choose the folder containing eldenring.exe, or its parent ELDEN RING folder."); return; }
        await SetGameAsync(game);
    }

    private async Task SetGameAsync(string? game)
    {
        _verifiedInstalledTag = null; _modUpdateRequired = false;
        _game = game; _release = null; _found = null; _current = false; CleanupStage();
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
        _modDownloadUrl = ReleaseCatalog.NexusUrl; _modUpdateRequired = false;
        try
        {
            _releases = await new GitHubReleases(_http).GetRecentAsync();
            _release = ReleaseCatalog.SelectNewest(_releases);
            var asset = ReleaseCatalog.SelectZip(_release);
            _current = false; _found = null;
            var installed = ModFingerprint.IsInstalled(_game);
            _state.Installs.TryGetValue(_game, out var record);
            var recorded = installed && record is not null && StateStore.Matches(_game, record);
            _verifiedInstalledTag = recorded && record!.Tag != StateStore.UnverifiedTag ? record!.Tag : null;
            if (recorded) _current = record!.Tag.Equals(_release.Tag, StringComparison.OrdinalIgnoreCase);
            if (!_current) _found = await Task.Run(() => ModPackage.FindDownloaded(DownloadsFolder(), asset));
            if (installed && !recorded && _found is not null)
            {
                // Installed by hand or by an older launcher: compare against the matching ZIP the player already has.
                try
                {
                    await Task.Run(() => Stage(_found));
                    _current = ModFingerprint.Matches(_game, _stage!);
                    if (_current) _found = null;
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { _found = null; }
                finally { CleanupStage(); }
            }
            var next = _found is not null ? $" Found {Path.GetFileName(_found)} in your Downloads folder, ready to install."
                : $" Select Get latest release, download the {_release.Tag} ZIP, then choose Install from ZIP.";
            if (_current) SetStatus($"Installed files match GitHub {_release.Tag}. Checking the author's version policy…");
            else if (recorded && record!.Tag == StateStore.UnverifiedTag) SetStatus($"A ZIP that could not be verified is installed. {_release.Tag} is the latest release." + next);
            else if (recorded) SetStatus($"Version {record!.Tag} is installed. {_release.Tag} is available." + next);
            else if (installed) SetStatus("Seamless Co-Op is installed, but its version cannot be verified." + next);
            else SetStatus("Seamless Co-Op is not installed." + next);
            _version.Text = "GitHub mirror: " + _release.Tag + (_release.Prerelease ? " (beta)" : "");
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var minimum = await new ModVersionPolicy(_http).GetMinimumAsync(timeout.Token);
                var behind = ModVersionPolicy.MirrorIsBehind(_release.Tag, minimum);
                if (behind == true)
                {
                    // The mirror can lag even when a player installed a newer Nexus ZIP.
                    // Only replace Play's Launch action when the installed version is actually known to be old.
                    _modUpdateRequired = installed && (_current || (_verifiedInstalledTag is not null && ModVersionPolicy.MirrorIsBehind(_verifiedInstalledTag, minimum) == true));
                    _current = false; _found = null;
                    SetStatus($"The author requires v{minimum}. GitHub still lists {_release.Tag}. Get the current ZIP from Nexus, then choose Install from ZIP.");
                    _version.Text += $" · Author minimum: v{minimum}";
                }
                else if (behind is null)
                {
                    _current = false; _found = null;
                    SetStatus("The author's version could not be compared with the GitHub release. Check Nexus for the current ZIP.");
                }
                else
                {
                    _modDownloadUrl = ReleaseCatalog.ReleasePageUrl(_release.Tag);
                    if (_current) SetStatus("Installed files match the current GitHub release and the author's version policy.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException)
            {
                _current = false; _found = null;
                SetStatus((installed ? "Seamless Co-Op is installed. " : "Seamless Co-Op is not installed. ") + "The author's version policy could not be checked. Check Nexus for the current ZIP; installed-game launch remains available.");
                _version.Text += " · Author version unavailable";
            }
        }
        catch (Exception ex)
        {
            _releases = null; _release = null; _found = null; _current = false;
            _version.Text = "Latest version unavailable";
            SetStatus((ModFingerprint.IsInstalled(_game) ? "Installed settings and Launch remain available. " : "") + "Could not check GitHub: " + ex.Message);
        }
        finally { _busy = false; UpdateButtons(); }
    }

    private void Stage(string zip)
    {
        CleanupStage();
        var folder = Path.Combine(_dataDir, "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var extracted = Path.Combine(folder, "extracted");
            ModPackage.Extract(zip, extracted);
            _stage = extracted;
        }
        catch { Directory.Delete(folder, true); throw; }
    }

    /// <summary>Installs a ZIP the player downloaded. Without a path, asks for one.</summary>
    private async Task InstallFromZipAsync(string? zip)
    {
        if (_game is null || _busy) return;
        if (zip is null)
        {
            var dialog = new OpenFileDialog { Title = "Choose the Seamless Co-Op ZIP you downloaded", Filter = "ZIP archives (*.zip)|*.zip", InitialDirectory = DownloadsFolder() };
            if (dialog.ShowDialog(this) != true) return;
            zip = dialog.FileName;
        }
        if (Process.GetProcessesByName("eldenring").Length > 0 || Process.GetProcessesByName("ersc_launcher").Length > 0)
        { await AlertAsync("Game is running", "Close Elden Ring before installing or updating the mod."); return; }
        var existing = ModFingerprint.IsInstalled(_game);
        var backupNote = existing ? " Existing mod files and settings will be backed up before replacement." : "";
        _busy = true; UpdateButtons(); SetStatus("Checking " + Path.GetFileName(zip) + "…");
        try
        {
            var release = await Task.Run(() => ModPackage.Identify(zip, _releases ?? []));
            if (release is null)
            {
                var reason = _releases is null
                    ? "The published releases could not be checked, so this ZIP can't be verified."
                    : "This ZIP doesn't match any recent Seamless Co-Op release on GitHub.";
                if (await AskAsync("Unverified ZIP", reason + " Only install files from the mod's Nexus page or its GitHub releases. Install it anyway?" + backupNote, ["Install", "Cancel"]) != 0)
                { SetStatus("Nothing was installed."); return; }
            }
            else if (existing && await AskAsync("Update Seamless Co-Op", "Install " + release.Tag + "?" + backupNote, ["Install", "Cancel"]) != 0)
            { SetStatus("Nothing was installed."); return; }
            var tag = release?.Tag ?? StateStore.UnverifiedTag;
            SetStatus(existing ? "Updating Seamless Co-Op…" : "Installing Seamless Co-Op…");
            await Task.Run(() => Stage(zip));
            var backup = ModInstaller.Install(_game, _stage!, Path.Combine(_dataDir, "backups", SafeFolderName(_game)));
            _state.Installs[_game] = StateStore.Record(tag, _game, _stage!);
            _verifiedInstalledTag = release?.Tag;
            _modUpdateRequired = false;
            _store.Save(_state);
            LoadSettings();
            _current = release is not null && _release is not null && release.Tag.Equals(_release.Tag, StringComparison.OrdinalIgnoreCase);
            _found = null;
            SetStatus((release is null ? "Installed the unverified ZIP." : "Installed " + tag + ".") + " Backup: " + backup);
        }
        catch (Exception ex) { SetStatus("Install failed: " + ex.Message); await AlertAsync("Install failed", ex.Message); }
        finally { CleanupStage(); _busy = false; UpdateButtons(); }
    }

    private void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _ = AlertAsync("Could not open the page", url + "\n" + ex.Message); }
    }

    private static string DownloadsFolder()
    {
        try
        {
            if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var path) == 0) return path;
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string path);

    private void LoadSettings()
    {
        _saveFeedback.Text = "Changes apply on the next launch.";
        _settings.Children.Clear(); _editors.Clear(); _settingRows.Clear(); _ini = null; _password = null;
        if (_game is null || !ModFingerprint.IsInstalled(_game))
        { _settings.Children.Add(Label("Install the mod to edit its settings.", 14, MutedBrush, new Thickness(0))); RefreshCompanionState(); return; }
        try
        {
            _ini = IniDocument.Load(Path.Combine(_game, "SeamlessCoop", "ersc_settings.ini"));
            foreach (var entry in _ini.Entries.OrderBy(PresentationOrder))
            {
                var presentation = SettingPresentation.For(entry);
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var copy = new StackPanel();
                var name = Label(presentation.Label, 14, TextBrush, new Thickness(0, 0, 0, 3)); name.FontWeight = FontWeights.SemiBold; copy.Children.Add(name);
                if (presentation.Help is not null) copy.Children.Add(Label(presentation.Help, 12, MutedBrush, new Thickness(0)));
                row.Children.Add(copy);
                var (control, read) = CreateEditor(entry, presentation);
                var editor = (FrameworkElement)control; editor.MinWidth = 0; editor.MaxWidth = double.PositiveInfinity;
                editor.HorizontalAlignment = presentation.Kind == SettingKind.Toggle ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
                if (editor is ComboBox or TextBox or PasswordBox) editor.Width = double.NaN;
                editor.MinHeight = 48;
                row.Children.Add(editor); LayoutSettingRow(row);
                editor.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => SettingsChanged()));
                if (editor is CheckBox toggle) { toggle.Checked += (_, _) => SettingsChanged(); toggle.Unchecked += (_, _) => SettingsChanged(); }
                if (editor is ComboBox choice) choice.SelectionChanged += (_, _) => SettingsChanged();
                if (editor is PasswordBox password) password.PasswordChanged += (_, _) => SettingsChanged();
                if (editor is Grid grid) foreach (var slider in grid.Children.OfType<Slider>()) slider.ValueChanged += (_, _) => SettingsChanged();
                _settings.Children.Add(row); _editors.Add((entry, read)); _settingRows.Add((entry, row, CompanionSettings.SectionFor(entry)));
            }
            SelectSettingsSection(_selectedSection); RefreshCompanionState();
        }
        catch (Exception ex) { _settings.Children.Add(Label("Could not read settings: " + ex.Message, 14, MutedBrush, new Thickness(0))); }
    }

    private void SettingsChanged() { _saveFeedback.Text = "Unsaved changes."; RefreshCompanionState(); }

    private (UIElement Control, Func<string> Read) CreateEditor(IniEntry entry, SettingPresentation presentation)
    {
        switch (presentation.Kind)
        {
            case SettingKind.Toggle:
            {
                var toggle = new CheckBox { IsChecked = entry.Value == "1", Content = entry.Value == "1" ? "On" : "Off", Foreground = TextBrush, Cursor = Cursors.Hand };
                StyleToggle(toggle);
                AutomationProperties.SetName(toggle, presentation.Label);
                toggle.Checked += (_, _) => toggle.Content = "On";
                toggle.Unchecked += (_, _) => toggle.Content = "Off";
                return (toggle, () => toggle.IsChecked == true ? "1" : "0");
            }
            case SettingKind.Choice:
            {
                var choice = new ComboBox
                {
                    ItemsSource = presentation.Options, DisplayMemberPath = nameof(SettingOption.Label), SelectedValuePath = nameof(SettingOption.Value),
                    SelectedValue = entry.Value, Width = 280, HorizontalAlignment = HorizontalAlignment.Left
                };
                if (ChoiceStyle.Value is { } style) choice.Style = style;
                else { choice.Background = Brushes.White; choice.Foreground = Brushes.Black; }
                AutomationProperties.SetName(choice, presentation.Label);
                return (choice, () => choice.SelectedValue?.ToString() ?? entry.Value);
            }
            case SettingKind.Slider:
            {
                // Slider first so focus reaches it before the exact-value field.
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var numeric = new TextBox
                {
                    Text = entry.Value, Width = 66, TextAlignment = TextAlignment.Right, Margin = new Thickness(12, 0, 0, 0),
                    Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = DividerBrush, Padding = new Thickness(12, 10, 12, 10), VerticalContentAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(numeric, presentation.Label + " exact value");
                var slider = new Slider
                {
                    Style = CompanionSliderStyle.Value,
                    Minimum = presentation.Minimum, Maximum = presentation.Maximum,
                    Value = int.Parse(entry.Value, CultureInfo.InvariantCulture),
                    TickFrequency = 1, IsSnapToTickEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = GoldBrush
                };
                AutomationProperties.SetName(slider, presentation.Label);
                row.Children.Add(slider);
                Grid.SetColumn(numeric, 1); row.Children.Add(numeric);
                if (presentation.Unit is not null)
                {
                    var unit = Label(presentation.Unit, 14, MutedBrush, new Thickness(5, 5, 0, 0));
                    Grid.SetColumn(unit, 2); row.Children.Add(unit);
                }
                var syncing = false;
                slider.ValueChanged += (_, _) =>
                {
                    if (syncing) return;
                    syncing = true; numeric.Text = ((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture); syncing = false;
                };
                numeric.TextChanged += (_, _) =>
                {
                    if (syncing || !int.TryParse(numeric.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return;
                    if (presentation.Unit == "%") { slider.Minimum = Math.Min(slider.Minimum, value); slider.Maximum = Math.Max(slider.Maximum, value); }
                    if (value < slider.Minimum || value > slider.Maximum) return;
                    syncing = true; slider.Value = value; syncing = false;
                };
                return (row, () =>
                {
                    if (!int.TryParse(numeric.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        throw new InvalidDataException(presentation.Label + " must be a whole number.");
                    if (presentation.Unit is null && (value < presentation.Minimum || value > presentation.Maximum))
                        throw new InvalidDataException(presentation.Label + " must be between " + presentation.Minimum + " and " + presentation.Maximum + ".");
                    return value.ToString(CultureInfo.InvariantCulture);
                });
            }
            case SettingKind.Password:
            {
                var password = new PasswordBox { Password = entry.Value, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 280, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = DividerBrush, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(12, 10, 12, 10) };
                AutomationProperties.SetName(password, presentation.Label);
                password.PasswordChanged += (_, _) => UpdateButtons();
                _password = password;
                return (password, () => password.Password);
            }
            default:
            {
                var input = new TextBox { Text = entry.Value, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 280, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = DividerBrush, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(12, 10, 12, 10) };
                AutomationProperties.SetName(input, presentation.Label);
                return (input, () => input.Text);
            }
        }
    }

    private bool SaveSettings()
    {
        if (_game is null || _ini is null) return false;
        try
        {
            foreach (var (entry, read) in _editors) _ini.SetAt(entry.LineIndex, read());
            _ini.Save(Path.Combine(_game, "SeamlessCoop", "ersc_settings.ini"));
            SetStatus("Settings saved."); _saveFeedback.Text = "Settings saved."; UpdateButtons(); return true;
        }
        catch (Exception ex) { _ = AlertAsync("Could not save settings", ex.Message); return false; }
    }

    private void Launch()
    {
        if (_game is null || !CanLaunch()) return;
        try
        {
            if (!SaveSettings()) return;
            Process.Start(new ProcessStartInfo(Path.Combine(_game, "ersc_launcher.exe")) { WorkingDirectory = _game, UseShellExecute = true });
            SetStatus("Starting Seamless Co-Op…");
            if (_gamepadMode) Close(); // Hand control back to Steam so Elden Ring gets its own controller layout.
        }
        catch (Exception ex) { _ = AlertAsync("Could not launch", ex.Message); }
    }

    private async Task AddToSteamAsync()
    {
        if (_busy || _steamBusy) return;
        _steamBusy = true; _steam.IsEnabled = false;
        try
        {
            var message = await SteamIntegration.ChangeAsync(add: true, async question => await AskAsync("Add to Steam", question, ["Close Steam", "Cancel"]) == 0);
            await AlertAsync("Add to Steam", message);
        }
        catch (Exception ex) { await AlertAsync("Could not add to Steam", ex.Message); }
        finally { _steamBusy = false; UpdateSteamButton(); }
    }

    private void UpdateSteamButton()
    {
        var added = SteamIntegration.IsAdded();
        var rename = added && SteamIntegration.HasLegacyName();
        _steam.Content = rename ? "Update Steam shortcut" : added ? "Added to Steam" : "Add to Steam";
        _steam.IsEnabled = !_busy && !_steamBusy && (!added || rename);
    }

    private void UpdateHints()
    {
        // Full screen has no title bar to close from, and a controller can't reach one.
        var controller = _gamepadMode || _gamepad?.Connected == true;
        _hints.Visibility = controller ? Visibility.Visible : Visibility.Collapsed;
        _quit.Visibility = controller ? Visibility.Visible : Visibility.Collapsed;
    }

    private Task<int> AskAsync(string title, string message, string[] buttons)
    {
        var dialog = new OverlayDialog(title, message, buttons);
        return ShowOverlayAsync(dialog, dialog.Result);
    }

    private Task AlertAsync(string title, string message) => AskAsync(title, message, ["OK"]);

    private async Task EditTextAsync(Control field)
    {
        var password = field as PasswordBox;
        var keyboard = new OnScreenKeyboard(AutomationProperties.GetName(field), password?.Password ?? ((TextBox)field).Text, password is not null);
        var text = await ShowOverlayAsync(keyboard, keyboard.Result);
        if (text is null) return;
        if (password is not null) password.Password = text;
        else ((TextBox)field).Text = text;
    }

    private async Task<T> ShowOverlayAsync<T>(Overlay overlay, Task<T> result)
    {
        var previous = Keyboard.FocusedElement as UIElement;
        _layers.Children.Add(overlay); _overlays.Add(overlay);
        _ = Dispatcher.BeginInvoke(overlay.FocusInitial, System.Windows.Threading.DispatcherPriority.Input);
        try { return await result; }
        finally
        {
            _layers.Children.Remove(overlay); _overlays.Remove(overlay);
            if (previous is not null && previous.IsVisible && _main.IsAncestorOf(previous)) previous.Focus();
            else if (_overlays.Count > 0) _overlays[^1].FocusInitial();
        }
    }

    /// <summary>Controller input. Overlays take it first; otherwise it moves focus or acts on the focused control.</summary>
    public void HandleGamepad(GamepadButton button)
    {
        _focusRing.Show();
        if (_overlays.Count > 0) { _overlays[^1].Handle(button); return; }
        var focused = Keyboard.FocusedElement;
        if (GamepadNavigator.OpenComboFor(focused) is { } open)
        {
            switch (button)
            {
                case GamepadButton.Up or GamepadButton.Left: GamepadNavigator.Step(open, -1); break;
                case GamepadButton.Down or GamepadButton.Right: GamepadNavigator.Step(open, 1); break;
                case GamepadButton.B:
                    if (_comboOriginal >= 0) open.SelectedIndex = _comboOriginal;
                    goto case GamepadButton.A;
                case GamepadButton.A:
                    open.IsDropDownOpen = false; _comboOriginal = -1; open.Focus(); break;
            }
            return;
        }
        var inside = focused is DependencyObject element && _main.IsAncestorOf(element);
        if (!inside && button is GamepadButton.Up or GamepadButton.Down or GamepadButton.Left or GamepadButton.Right or GamepadButton.A)
        {
            FocusPreferred();
            return;
        }
        switch (button)
        {
            case GamepadButton.Up: GamepadNavigator.Move(false); break;
            case GamepadButton.Down: GamepadNavigator.Move(true); break;
            case GamepadButton.Left or GamepadButton.Right:
                var direction = button == GamepadButton.Left ? -1 : 1;
                if (!GamepadNavigator.Adjust(focused, direction, large: false)) GamepadNavigator.Move(direction > 0);
                break;
            case GamepadButton.LeftShoulder: GamepadNavigator.Adjust(focused, -1, large: true); break;
            case GamepadButton.RightShoulder: if (!GamepadNavigator.Adjust(focused, 1, large: true)) SnapToLaunch(); break;
            case GamepadButton.RightTrigger: SnapToLaunch(); break;
            case GamepadButton.B: _ = ConfirmQuitAsync(); break;
            case GamepadButton.A:
                if (focused is TextBox or PasswordBox) _ = EditTextAsync((Control)focused);
                else
                {
                    if (focused is ComboBox combo) _comboOriginal = combo.SelectedIndex;
                    GamepadNavigator.Activate(focused);
                }
                break;
            case GamepadButton.X: if (_save.IsEnabled) SaveSettings(); break;
            case GamepadButton.Y: if (_check.IsEnabled) _ = CheckAsync(); break;
            case GamepadButton.Start: if (_launch.IsEnabled) Launch(); break;
            case GamepadButton.Back: _ = ConfirmQuitAsync(); break;
        }
    }

    private async Task ConfirmQuitAsync()
    {
        if (await AskAsync("Quit", "Close the launcher?", ["Quit", "Cancel"]) == 0) Close();
    }

    /// <summary>Jumps to the Launch button, or to whatever still blocks launching.</summary>
    private void SnapToLaunch()
    {
        if (_launch.IsEnabled)
        {
            if (_modUpdateRequired) { SelectPage("Installation"); _launchInstalled.Focus(); }
            else { SelectPage("Play"); _launch.Focus(); }
        }
        else FocusPreferred();
    }

    /// <summary>Puts focus on the next action, including navigating to its page.</summary>
    private void FocusPreferred()
    {
        if (_password is { } password && string.IsNullOrWhiteSpace(password.Password))
        { SelectPage("Co-op settings"); SelectSettingsSection(CompanionSection.Session); password.Focus(); }
        else if (_launch.IsEnabled) SnapToLaunch();
        else if (_game is not null && !ModFingerprint.IsInstalled(_game))
        { SelectPage("Installation"); if (_found is not null && _install.IsEnabled) _install.Focus(); else _getRelease.Focus(); }
        else { SelectPage("Play"); _heroSetup.Focus(); }
    }

    private bool CanLaunch() => _game is not null && ModFingerprint.IsInstalled(_game) &&
        _editors.Any(e => e.Entry.Key.Equals("cooppassword", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(e.Read()));
    private void UpdateButtons()
    {
        _check.IsEnabled = !_busy && _game is not null;
        _install.IsEnabled = !_busy && _game is not null;
        _install.Content = _found is not null ? "Install " + Path.GetFileName(_found) : "Install from ZIP…";
        _save.IsEnabled = !_busy && _ini is not null;
        _launch.IsEnabled = !_busy && CanLaunch();
        UpdateSteamButton(); _steam.IsEnabled &= !_busy;
        _progress.Visibility = _busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshCompanionState();
    }
    private void SetStatus(string message) { _status.Text = message; RefreshCompanionState(); }
    private void CleanupStage()
    {
        if (_stage is null) return;
        var folder = Directory.GetParent(_stage)?.FullName;
        _stage = null;
        try { if (folder is not null && Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }
    private static string SafeFolderName(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16];
    private static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    internal static TextBlock Label(string text, double size, Brush color, Thickness margin) => new() { Text = text, FontSize = size, Foreground = color, Margin = margin, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45 };
    private static StackPanel Panel() => new() { Orientation = Orientation.Vertical };
    private static Border Wrap(StackPanel panel) => new() { Background = PanelBrush, Padding = new Thickness(16), Child = panel, CornerRadius = new CornerRadius(6) };
    // Dark dropdown matching the text fields. The stock theme ignores Background on the closed face, so it needs its own template.
    private const string ChoiceXaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
          <Setter Property="Background" Value="#101411"/>
          <Setter Property="Foreground" Value="#ECE6D8"/>
          <Setter Property="BorderBrush" Value="#A4AA9B"/>
          <Setter Property="Cursor" Value="Hand"/>
          <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
          <Setter Property="SnapsToDevicePixels" Value="True"/>
          <Setter Property="ItemContainerStyle">
            <Setter.Value>
              <Style TargetType="ComboBoxItem">
                <Setter Property="Foreground" Value="#ECE6D8"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ComboBoxItem">
                      <Border x:Name="Row" Background="Transparent" Padding="10,7" CornerRadius="2">
                        <ContentPresenter/>
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                          <Setter TargetName="Row" Property="Background" Value="#343B30"/>
                        </Trigger>
                        <Trigger Property="IsHighlighted" Value="True">
                          <Setter TargetName="Row" Property="Background" Value="#C4AD77"/>
                          <Setter Property="Foreground" Value="#101411"/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
            </Setter.Value>
          </Setter>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ComboBox">
                <Grid>
                  <ToggleButton Focusable="False" ClickMode="Press" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                                IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                    <ToggleButton.Template>
                      <ControlTemplate TargetType="ToggleButton">
                        <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="3">
                          <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0" Data="M 0 0 L 4 4 L 8 0" Stroke="#C4AD77" StrokeThickness="1.5"/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#C4AD77"/>
                          </Trigger>
                          <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#C4AD77"/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </ToggleButton.Template>
                  </ToggleButton>
                  <ContentPresenter IsHitTestVisible="False" Margin="10,6,32,6" VerticalAlignment="Center"
                                    Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                    ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
                  <Popup IsOpen="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
                    <Border Background="#181C17" BorderBrush="#A4AA9B" BorderThickness="1" CornerRadius="3" Margin="0,2,0,0" Padding="3"
                            MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                      <ScrollViewer>
                        <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                      </ScrollViewer>
                    </Border>
                  </Popup>
                </Grid>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsEnabled" Value="False">
                    <Setter Property="Opacity" Value="0.4"/>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """;

    private static TextBlock Heading(string text)
    {
        var heading = Label(text, 16, TextBrush, new Thickness(0, 0, 0, 6));
        heading.FontWeight = FontWeights.SemiBold;
        return heading;
    }

    internal static void StyleButton(Button button, bool primary = false)
    {
        button.Background = primary ? GoldBrush : PanelBrush; button.Foreground = primary ? BackgroundBrush : TextBrush; button.BorderBrush = primary ? GoldBrush : DividerBrush;
        button.BorderThickness = new Thickness(1); button.Padding = new Thickness(24, 15, 24, 15); button.MinHeight = 48; button.FontFamily = BodyFont; button.FontWeight = FontWeights.Bold; button.Cursor = Cursors.Hand;
        if (primary) button.FontWeight = FontWeights.SemiBold;
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Color(primary ? "#D5BE89" : "#2C3329"), "Chrome"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, Color(primary ? "#A58E5C" : "#363E33"), "Chrome"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(disabled);
        button.Template = template;
    }

    private static void StyleToggle(CheckBox toggle)
    {
        var grid = new FrameworkElementFactory(typeof(Grid)); grid.SetValue(FrameworkElement.WidthProperty, 54.0); grid.SetValue(FrameworkElement.HeightProperty, 30.0);
        var off = new FrameworkElementFactory(typeof(Image)); off.Name = "OffImage"; off.SetValue(Image.SourceProperty, Asset("switch-off.png")); grid.AppendChild(off);
        var on = new FrameworkElementFactory(typeof(Image)); on.Name = "OnImage"; on.SetValue(Image.SourceProperty, Asset("switch-on.png")); on.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed); grid.AppendChild(on);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = grid };
        var active = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        active.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, "OffImage")); active.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "OnImage")); template.Triggers.Add(active);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(OpacityProperty, .4)); template.Triggers.Add(disabled);
        toggle.Template = template; toggle.Cursor = Cursors.Hand; toggle.MinHeight = 48;
    }
}
