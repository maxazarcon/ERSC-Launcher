using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using ERSC.Launcher.Core;
using Microsoft.Win32;

namespace ERSC.Launcher;

public sealed class MainWindow : Window
{
    private const string LauncherRepository = "maxazarcon/ERSC-Launcher";
    private static readonly Brush BackgroundBrush = Color("#141713");
    private static readonly Brush PanelBrush = Color("#20251F");
    private static readonly Brush TextBrush = Color("#EEEDE3");
    private static readonly Brush MutedBrush = Color("#AAB3A3");
    private static readonly Brush GoldBrush = Color("#D7B66B");
    private static readonly Brush DividerBrush = Color("#2F362D");
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
    private readonly Button _save = new();
    private readonly Button _launch = new();
    private readonly Button _check = new();
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 3, Margin = new Thickness(0, 12, 0, 0), Foreground = GoldBrush, Background = BackgroundBrush, BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
    private readonly List<(IniEntry Entry, Func<string> Read)> _editors = [];
    private LauncherState _state;
    private string? _game;
    private ModRelease? _release;
    private ModAsset? _asset;
    private string? _stage;
    private IniDocument? _ini;
    private bool _busy;
    private bool _current;
    private string? _launcherPayload;
    private string? _launcherUpdateTag;

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
        Loaded += (_, _) => { ShowPreviousUpdateErrors(); _ = InitializeAsync(); _ = CheckLauncherAsync(); };
        Closed += (_, _) => { _http.Dispose(); CleanupStage(); };
    }

    private UIElement BuildScreen()
    {
        var root = new DockPanel { Margin = new Thickness(28, 24, 28, 20) };

        // Footer: quiet launcher-update status on the left, primary actions on the right.
        var footer = new DockPanel();
        var footerBar = new Border { BorderBrush = DividerBrush, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, 0), Margin = new Thickness(0, 16, 0, 0), Child = footer };
        DockPanel.SetDock(footerBar, Dock.Bottom); root.Children.Add(footerBar);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        _save.Content = "Save settings"; _save.Click += (_, _) => SaveSettings();
        _launch.Content = "Launch Seamless Co-Op"; _launch.Click += (_, _) => Launch();
        StyleButton(_save); StyleButton(_launch, primary: true);
        _save.Margin = new Thickness(12, 0, 0, 0); _launch.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(_save); actions.Children.Add(_launch);
        _restartUpdate.Content = "Restart to update"; StyleButton(_restartUpdate); _restartUpdate.Margin = new Thickness(0, 0, 10, 0);
        _restartUpdate.Visibility = Visibility.Collapsed; _restartUpdate.Click += (_, _) => RestartForLauncherUpdate();
        DockPanel.SetDock(_restartUpdate, Dock.Left); footer.Children.Add(_restartUpdate);
        _launcherUpdateStatus.Foreground = MutedBrush; _launcherUpdateStatus.FontSize = 12; _launcherUpdateStatus.VerticalAlignment = VerticalAlignment.Center;
        _launcherUpdateStatus.TextWrapping = TextWrapping.NoWrap; _launcherUpdateStatus.TextTrimming = TextTrimming.CharacterEllipsis;
        _launcherUpdateStatus.SetBinding(ToolTipProperty, new Binding(nameof(TextBlock.Text)) { RelativeSource = RelativeSource.Self });
        footer.Children.Add(_launcherUpdateStatus);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        var body = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; scroll.Content = body;
        body.Children.Add(Label("SEAMLESS CO-OP", 12, GoldBrush, new Thickness(0, 0, 0, 4)));
        var heading = Label("Your way into the Lands Between", 26, TextBrush, new Thickness(0, 0, 0, 20)); heading.FontWeight = FontWeights.SemiBold;
        body.Children.Add(heading);

        var location = Panel(); body.Children.Add(Wrap(location));
        location.Children.Add(Heading("Elden Ring game folder"));
        _gameText.Foreground = MutedBrush; location.Children.Add(_gameText);
        var locateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        location.Children.Add(locateRow);
        var browse = new Button { Content = "Choose folder" }; StyleButton(browse); browse.Click += async (_, _) => await BrowseAsync(); locateRow.Children.Add(browse);
        _check.Content = "Check updates"; StyleButton(_check); _check.Margin = new Thickness(8, 0, 0, 0); _check.Click += async (_, _) => await CheckAsync(); locateRow.Children.Add(_check);

        var state = Panel(); var stateCard = Wrap(state); stateCard.Margin = new Thickness(0, 12, 0, 0); body.Children.Add(stateCard);
        state.Children.Add(Heading("Installation"));
        _status.Foreground = MutedBrush; state.Children.Add(_status);
        _version.Foreground = MutedBrush; _version.FontSize = 12; _version.Margin = new Thickness(0, 4, 0, 0); state.Children.Add(_version);
        state.Children.Add(_progress);
        _install.Content = "Install Seamless Co-Op"; StyleButton(_install); _install.Margin = new Thickness(0, 14, 0, 0); _install.HorizontalAlignment = HorizontalAlignment.Left;
        _install.Click += async (_, _) => await InstallAsync(); state.Children.Add(_install);

        var settingsHeading = Label("Settings", 20, TextBrush, new Thickness(0, 28, 0, 4)); settingsHeading.FontWeight = FontWeights.SemiBold;
        body.Children.Add(settingsHeading);
        body.Children.Add(Label("Changes are saved to the mod's settings file in your game folder.", 13, MutedBrush, new Thickness(0, 0, 0, 4)));
        body.Children.Add(_settings);
        UpdateButtons();
        return root;
    }

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

    private void ShowPreviousUpdateErrors()
    {
        var updates = Path.Combine(_dataDir, "launcher-updates");
        if (!Directory.Exists(updates)) return;
        foreach (var folder in Directory.EnumerateDirectories(updates))
        {
            try
            {
                var message = LauncherUpdateErrors.Take(folder);
                if (message is not null)
                {
                    _launcherUpdateStatus.Text = message;
                    MessageBox.Show(this, message, "Launcher update failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch { }
        }
    }

    private void RestartForLauncherUpdate()
    {
        if (_launcherPayload is null || _launcherUpdateTag is null) return;
        if (_ini is not null && _editors.Any(e => e.Read() != e.Entry.Value))
        {
            var choice = MessageBox.Show(this, "Save changed mod settings before restarting?", "Launcher update", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel || (choice == MessageBoxResult.Yes && !SaveSettings())) return;
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
            MessageBox.Show(this, ex.Message, "Could not update launcher", MessageBoxButton.OK, MessageBoxImage.Error);
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
            StackPanel? group = null;
            foreach (var entry in _ini.Entries)
            {
                var presentation = SettingPresentation.For(entry);
                if (group is null || section != entry.Section)
                {
                    section = entry.Section;
                    _settings.Children.Add(Label((section.Length == 0 ? "General" : section.Replace('_', ' ')).ToUpperInvariant(), 12, GoldBrush, new Thickness(0, 20, 0, 8)));
                    group = Panel(); var card = Wrap(group); card.Padding = new Thickness(16, 2, 16, 2); _settings.Children.Add(card);
                }
                if (group.Children.Count > 0) group.Children.Add(new Border { Height = 1, Background = DividerBrush });
                var row = new StackPanel { Margin = new Thickness(0, 12, 0, 14) }; group.Children.Add(row);
                var name = Label(presentation.Label, 14, TextBrush, new Thickness(0, 0, 0, 4)); name.FontWeight = FontWeights.SemiBold; row.Children.Add(name);
                if (presentation.Help is not null) row.Children.Add(Label(presentation.Help, 12, MutedBrush, new Thickness(0, 0, 0, 8)));
                var (control, read) = CreateEditor(entry, presentation);
                row.Children.Add(control);
                _editors.Add((entry, read));
            }
        }
        catch (Exception ex) { _settings.Children.Add(Label("Could not read settings: " + ex.Message, 14, MutedBrush, new Thickness(0))); }
    }

    private (UIElement Control, Func<string> Read) CreateEditor(IniEntry entry, SettingPresentation presentation)
    {
        switch (presentation.Kind)
        {
            case SettingKind.Toggle:
            {
                var toggle = new CheckBox { IsChecked = entry.Value == "1", Content = entry.Value == "1" ? "On" : "Off", Foreground = TextBrush, Cursor = System.Windows.Input.Cursors.Hand };
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
                var row = new DockPanel { LastChildFill = true };
                var numeric = new TextBox
                {
                    Text = entry.Value, Width = 66, TextAlignment = TextAlignment.Right, Margin = new Thickness(12, 0, 0, 0),
                    Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = MutedBrush, Padding = new Thickness(5)
                };
                AutomationProperties.SetName(numeric, presentation.Label + " exact value");
                if (presentation.Unit is not null)
                {
                    var unit = Label(presentation.Unit, 14, MutedBrush, new Thickness(5, 5, 0, 0));
                    DockPanel.SetDock(unit, Dock.Right); row.Children.Add(unit);
                }
                DockPanel.SetDock(numeric, Dock.Right); row.Children.Add(numeric);
                var slider = new Slider
                {
                    Minimum = presentation.Minimum, Maximum = presentation.Maximum,
                    Value = int.Parse(entry.Value, CultureInfo.InvariantCulture),
                    TickFrequency = 1, IsSnapToTickEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = GoldBrush
                };
                AutomationProperties.SetName(slider, presentation.Label);
                row.Children.Add(slider);
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
                var password = new PasswordBox { Password = entry.Value, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 280, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = MutedBrush, Padding = new Thickness(8, 5, 8, 5) };
                AutomationProperties.SetName(password, presentation.Label);
                password.PasswordChanged += (_, _) => UpdateButtons();
                return (password, () => password.Password);
            }
            default:
            {
                var input = new TextBox { Text = entry.Value, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 280, Background = BackgroundBrush, Foreground = TextBrush, BorderBrush = MutedBrush, Padding = new Thickness(8, 5, 8, 5) };
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
        _progress.Visibility = _busy ? Visibility.Visible : Visibility.Collapsed;
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
    private static Border Wrap(StackPanel panel) => new() { Background = PanelBrush, Padding = new Thickness(16), Child = panel, CornerRadius = new CornerRadius(6) };
    // Dark dropdown matching the text fields. The stock theme ignores Background on the closed face, so it needs its own template.
    private const string ChoiceXaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
          <Setter Property="Background" Value="#141713"/>
          <Setter Property="Foreground" Value="#EEEDE3"/>
          <Setter Property="BorderBrush" Value="#AAB3A3"/>
          <Setter Property="Cursor" Value="Hand"/>
          <Setter Property="SnapsToDevicePixels" Value="True"/>
          <Setter Property="ItemContainerStyle">
            <Setter.Value>
              <Style TargetType="ComboBoxItem">
                <Setter Property="Foreground" Value="#EEEDE3"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ComboBoxItem">
                      <Border x:Name="Row" Background="Transparent" Padding="10,7" CornerRadius="2">
                        <ContentPresenter/>
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                          <Setter TargetName="Row" Property="Background" Value="#2F362D"/>
                        </Trigger>
                        <Trigger Property="IsHighlighted" Value="True">
                          <Setter TargetName="Row" Property="Background" Value="#D7B66B"/>
                          <Setter Property="Foreground" Value="#141713"/>
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
                          <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0" Data="M 0 0 L 4 4 L 8 0" Stroke="#D7B66B" StrokeThickness="1.5"/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#D7B66B"/>
                          </Trigger>
                          <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#D7B66B"/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </ToggleButton.Template>
                  </ToggleButton>
                  <ContentPresenter IsHitTestVisible="False" Margin="10,6,32,6" VerticalAlignment="Center"
                                    Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                    ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
                  <Popup IsOpen="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
                    <Border Background="#20251F" BorderBrush="#AAB3A3" BorderThickness="1" CornerRadius="3" Margin="0,2,0,0" Padding="3"
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

    private static void StyleButton(Button button, bool primary = false)
    {
        button.Background = primary ? GoldBrush : PanelBrush; button.Foreground = primary ? BackgroundBrush : TextBrush; button.BorderBrush = GoldBrush;
        button.BorderThickness = new Thickness(1); button.Padding = new Thickness(14, 8, 14, 8); button.Cursor = System.Windows.Input.Cursors.Hand;
        if (primary) button.FontWeight = FontWeights.SemiBold;
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
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
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Color(primary ? "#E6C986" : "#2C3329"), "Chrome"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, Color(primary ? "#C09F55" : "#363E33"), "Chrome"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(disabled);
        button.Template = template;
    }

    private static void StyleToggle(CheckBox toggle)
    {
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var track = new FrameworkElementFactory(typeof(Border));
        track.Name = "Track";
        track.SetValue(FrameworkElement.WidthProperty, 44.0);
        track.SetValue(FrameworkElement.HeightProperty, 24.0);
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        track.SetValue(Border.BackgroundProperty, MutedBrush);
        var knob = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        knob.Name = "Knob";
        knob.SetValue(FrameworkElement.WidthProperty, 18.0);
        knob.SetValue(FrameworkElement.HeightProperty, 18.0);
        knob.SetValue(FrameworkElement.MarginProperty, new Thickness(3));
        knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        knob.SetValue(System.Windows.Shapes.Shape.FillProperty, BackgroundBrush);
        track.AppendChild(knob); row.AppendChild(track);
        var text = new FrameworkElementFactory(typeof(ContentPresenter));
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 2, 0, 0));
        text.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(CheckBox.ContentProperty));
        row.AppendChild(text);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = row };
        var on = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty, GoldBrush, "Track"));
        on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Knob"));
        template.Triggers.Add(on);
        toggle.Template = template;
    }
}
