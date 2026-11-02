using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using ERSC.Launcher.Core;

namespace ERSC.Launcher;

// Layout and navigation for the approved Figma companion. Operational handlers stay in MainWindow.cs.
public sealed partial class MainWindow
{
    private static readonly FontFamily BodyFont = new(new Uri("pack://application:,,,/"), "/ERSCLauncher;component/Assets/Fonts/#Manrope");
    private static readonly FontFamily DisplayFont = new(new Uri("pack://application:,,,/"), "/ERSCLauncher;component/Assets/Fonts/#Cormorant Garamond");
    private static readonly FontFamily BrandFont = new(new Uri("pack://application:,,,/"), "/ERSCLauncher;component/Assets/Fonts/#Cinzel");
    private readonly Dictionary<string, Button> _navigation = [];
    private readonly Dictionary<CompanionSection, Button> _sectionButtons = [];
    private readonly List<(IniEntry Entry, Grid Row, CompanionSection Section)> _settingRows = [];
    private readonly TextBlock _settingsTitle = new();
    private readonly TextBlock _settingsHelp = new();
    private readonly TextBlock _saveFeedback = new();
    private readonly TextBlock _homePassword = new();
    private readonly TextBlock _homeInstalledVersion = new();
    private readonly Dictionary<string, TextBlock> _homeRules = [];
    private readonly Button _launchInstalled = new();
    private Button _chooseFolder = null!;
    private string? _verifiedInstalledTag;
    private readonly Button _heroSetup = new();
    private readonly Button _heroUpdate = new();
    private readonly Grid _companionRoot = new();
    private readonly Grid _pageLayer = new();
    private readonly StackPanel _playBody = new();
    private readonly StackPanel _settingsBody = new();
    private readonly StackPanel _installationBody = new();
    private readonly WrapPanel _overview = new();
    private readonly TextBlock _heroHeading = new();
    private readonly TextBlock _heroHint = new();
    private readonly Border _settingsVeil = new() { Background = Color("#101411"), Opacity = .96, Margin = new Thickness(0, 110, 0, 0), IsHitTestVisible = false };
    private readonly Grid _footer = new();
    private ScrollViewer _playScroll = null!;
    private ScrollViewer _settingsScroll = null!;
    private ScrollViewer _installationScroll = null!;
    private Border _sessionCard = null!;
    private Border _installationCard = null!;
    private Border _sidebar = null!;
    private Border _overviewGap = null!;
    private CompanionSection _selectedSection;
    private string _selectedPage = "Play";
    private bool _modUpdateRequired;
    private double _contentWidth = 1088;

    private UIElement BuildCompanionScreen()
    {
        FontFamily = BodyFont;
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6) });
        _companionRoot.Background = BackgroundBrush;
        _companionRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_gamepadMode ? 0 : 240) });
        _companionRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _sidebar = BuildSidebar();
        _sidebar.Visibility = _gamepadMode ? Visibility.Collapsed : Visibility.Visible;
        _companionRoot.Children.Add(_sidebar);

        var area = new Grid { ClipToBounds = true };
        Grid.SetColumn(area, 1); _companionRoot.Children.Add(area);
        var artwork = new Grid { Height = 590, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        artwork.Children.Add(new Image { Source = Asset("canopy.png"), Stretch = Stretch.UniformToFill });
        artwork.Children.Add(new Border { Background = new LinearGradientBrush(new GradientStopCollection
        {
            new(ColorFrom("#101411", .94), 0), new(ColorFrom("#101411", .30), .52), new(ColorFrom("#101411", .06), 1)
        }, new Point(0, 0), new Point(1, 0)) });
        artwork.Children.Add(new Border { Background = new LinearGradientBrush(new GradientStopCollection
        {
            new(ColorFrom("#101411", .10), 0), new(ColorFrom("#101411", .25), .56), new(ColorFrom("#101411", 1), 1)
        }, new Point(0, 0), new Point(0, 1)) });
        area.Children.Add(artwork); area.Children.Add(_settingsVeil);
        area.Children.Add(_pageLayer);
        _pageLayer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _pageLayer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _playScroll = PageScroll(_playBody); _settingsScroll = PageScroll(_settingsBody); _installationScroll = PageScroll(_installationBody);
        _pageLayer.Children.Add(_playScroll); _pageLayer.Children.Add(_settingsScroll); _pageLayer.Children.Add(_installationScroll);
        BuildPlay(); BuildSettingsPage(); BuildInstallationPage(); BuildFooter();
        Grid.SetRow(_footer, 1); _pageLayer.Children.Add(_footer);

        if (!_gamepadMode)
        {
            var caption = new Border { Height = 44, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent };
            caption.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            area.Children.Add(caption);
            var chrome = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0) };
            foreach (var (name, icon, action) in new (string, string, Action)[]
            {
                ("Minimize", "minimize.png", () => WindowState = WindowState.Minimized),
                ("Maximize", "maximize.png", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized),
                ("Close", "close.png", Close)
            })
            {
                var button = ActionButton(name, action); button.Content = new Image { Source = Asset(icon), Width = 16, Height = 16 };
                button.Width = 44; button.Height = 44; button.MinHeight = 44; button.Padding = new Thickness(0); button.Background = Brushes.Transparent; button.BorderBrush = Brushes.Transparent;
                chrome.Children.Add(button);
            }
            area.Children.Add(chrome);
        }
        _companionRoot.SizeChanged += (_, _) => UpdateCompanionLayout();
        SelectPage("Play"); UpdateButtons(); UpdateHints();
        return _companionRoot;
    }

    private static ScrollViewer PageScroll(Panel body)
    {
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, IsTabStop = false };
        scroll.Resources.Add(typeof(ScrollBar), CompanionScrollBarStyle.Value); return scroll;
    }
    private Border BuildSidebar()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var brand = new StackPanel { Margin = new Thickness(28, 44, 20, 0) };
        var wordmark = Label("ELDEN RING", 24, GoldBrush, new Thickness(0)); wordmark.FontFamily = BrandFont; brand.Children.Add(wordmark);
        var product = Label("Seamless Co-op", 14, TextBrush, new Thickness(0, 3, 0, 0)); product.FontWeight = FontWeights.SemiBold; brand.Children.Add(product);
        brand.Children.Add(Label("Unofficial companion", 11, MutedBrush, new Thickness(0, 3, 0, 0))); grid.Children.Add(brand);
        var nav = new StackPanel { Margin = new Thickness(16, 56, 16, 0) };
        foreach (var name in new[] { "Play", "Co-op settings", "Installation" })
        {
            var item = ActionButton(name, () => SelectPage(name)); item.HorizontalContentAlignment = HorizontalAlignment.Left; item.HorizontalAlignment = HorizontalAlignment.Stretch;
            item.Padding = new Thickness(16); item.Margin = new Thickness(0, 0, 0, 7); nav.Children.Add(item); _navigation[name] = item;
        }
        Grid.SetRow(nav, 1); grid.Children.Add(nav);
        var utility = new StackPanel { Margin = new Thickness(28, 16, 24, 40) };
        utility.Children.Add(LinkButton("About the mod", () => OpenPage(ReleaseCatalog.NexusUrl)));
        utility.Children.Add(Label("Controller supported", 12, MutedBrush, new Thickness(0, 16, 0, 18)));
        utility.Children.Add(new Border { Height = 1, Background = DividerBrush, Margin = new Thickness(0, 0, 0, 14) });
        var v = typeof(App).Assembly.GetName().Version!;
        utility.Children.Add(Label($"Launcher {v.Major}.{v.Minor}.{v.Build}", 11, MutedBrush, new Thickness(0)));
        Grid.SetRow(utility, 2); grid.Children.Add(utility);
        return new Border { Background = Color("#101310"), Child = grid };
    }

    private void BuildPlay()
    {
        var hero = new StackPanel();
        _heroHeading.Foreground = TextBrush; _heroHeading.Text = "Seamless Co-op"; _heroHeading.FontFamily = DisplayFont; _heroHeading.FontWeight = FontWeights.Medium; _heroHeading.FontSize = 60; _heroHeading.LineHeight = 87;
        hero.Children.Add(_heroHeading);
        hero.Children.Add(Label("Launch Elden Ring with Seamless Co-op.", 16, TextBrush, new Thickness(0, 14, 0, 0)));
        var actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        _launch.Content = "Launch Seamless Co-op"; _launch.Click += (_, _) => Launch(); StyleButton(_launch, true); _launch.Margin = new Thickness(0, 0, 12, 0); actions.Children.Add(_launch);
        _heroUpdate.Content = "Update Seamless Co-op"; _heroUpdate.Click += (_, _) => SelectPage("Installation"); StyleButton(_heroUpdate, true); _heroUpdate.Margin = new Thickness(0, 0, 12, 0); actions.Children.Add(_heroUpdate);
        _heroSetup.Click += async (_, _) =>
        {
            if (_game is null) { SelectPage("Installation"); await BrowseAsync(); }
            else if (!ModFingerprint.IsInstalled(_game)) SelectPage("Installation");
            else { SelectPage("Co-op settings"); SelectSettingsSection(CompanionSection.Session); _password?.Focus(); }
        };
        StyleButton(_heroSetup, true); _heroSetup.Margin = new Thickness(0, 0, 12, 0); actions.Children.Add(_heroSetup);
        actions.Children.Add(ActionButton("Co-op settings", () => SelectPage("Co-op settings")));
        _quit.Content = "Quit"; _quit.Click += (_, _) => Close(); StyleButton(_quit); _quit.Margin = new Thickness(12, 0, 0, 0); actions.Children.Add(_quit);
        hero.Children.Add(actions);
        _heroHint.FontSize = 12; _heroHint.Foreground = MutedBrush; _heroHint.Margin = new Thickness(0, 14, 0, 0); _heroHint.TextWrapping = TextWrapping.Wrap; hero.Children.Add(_heroHint);
        _playBody.Children.Add(hero);
        _overviewGap = new Border { Height = _gamepadMode ? 94 : 124 }; _playBody.Children.Add(_overviewGap);
        var session = new StackPanel(); session.Children.Add(Display("Your session", 28));
        var passwordRow = new WrapPanel { Margin = new Thickness(0, 16, 0, 16) };
        passwordRow.Children.Add(Label("Co-op password", 13, MutedBrush, new Thickness(0, 0, 28, 0)));
        _homePassword.Foreground = TextBrush; _homePassword.FontSize = 17; _homePassword.FontWeight = FontWeights.SemiBold; _homePassword.Margin = new Thickness(0, 0, 28, 0); passwordRow.Children.Add(_homePassword);
        passwordRow.Children.Add(LinkButton("Edit", () => { SelectPage("Co-op settings"); SelectSettingsSection(CompanionSection.Session); _password?.Focus(); })); session.Children.Add(passwordRow);
        session.Children.Add(new Border { Background = DividerBrush, Height = 1 });
        var rules = new UniformGrid { Columns = 3, Margin = new Thickness(0, 16, 0, 16) };
        foreach (var (key, name) in new[] { ("allow_invaders", "Invasions"), ("allow_summons", "Spirit summons"), ("death_debuffs", "Death debuffs") })
        {
            var stack = new StackPanel(); stack.Children.Add(Label(name, 12, MutedBrush, new Thickness(0, 0, 0, 5)));
            var value = Label("Not available", 15, TextBrush, new Thickness(0)); value.FontWeight = FontWeights.SemiBold; stack.Children.Add(value); _homeRules[key] = value; rules.Children.Add(stack);
        }
        session.Children.Add(rules); session.Children.Add(LinkButton("Edit session settings", () => { SelectPage("Co-op settings"); SelectSettingsSection(CompanionSection.Session); }));
        _sessionCard = Card(session, 24); _overview.Children.Add(_sessionCard);
        var installed = new StackPanel(); installed.Children.Add(Display("Installation", 28));
        _homeInstalledVersion.FontSize = 14; _homeInstalledVersion.Foreground = GoldBrush; _homeInstalledVersion.FontWeight = FontWeights.SemiBold; _homeInstalledVersion.Margin = new Thickness(0, 12, 0, 12); _homeInstalledVersion.TextWrapping = TextWrapping.Wrap; installed.Children.Add(_homeInstalledVersion);
        var status = Label("", 13, MutedBrush, new Thickness(0, 0, 0, 12)); status.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = _status }); installed.Children.Add(status);
        installed.Children.Add(ActionButton("Manage installation", () => SelectPage("Installation")));
        _installationCard = Card(installed, 24); _overview.Children.Add(_installationCard); _playBody.Children.Add(_overview);
    }

    private void BuildSettingsPage()
    {
        _settingsTitle.Foreground = TextBrush; _settingsTitle.FontFamily = DisplayFont; _settingsTitle.FontSize = 48; _settingsTitle.FontWeight = FontWeights.Medium; _settingsTitle.LineHeight = 69.6; _settingsBody.Children.Add(_settingsTitle);
        _settingsHelp.FontSize = 14; _settingsHelp.Foreground = MutedBrush; _settingsHelp.TextWrapping = TextWrapping.Wrap; _settingsHelp.Margin = new Thickness(0, 18, 0, 18); _settingsBody.Children.Add(_settingsHelp);
        var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        foreach (var (section, name) in new[] { (CompanionSection.Session, "Session"), (CompanionSection.Gameplay, "Gameplay"), (CompanionSection.Scaling, "Enemy scaling"), (CompanionSection.Advanced, "Advanced") })
        {
            var tab = LinkButton(name, () => SelectSettingsSection(section)); tab.FontSize = 14; tab.Margin = new Thickness(0, 0, 32, 0); tab.MinHeight = 32; _sectionButtons[section] = tab; tabs.Children.Add(tab);
        }
        _settingsBody.Children.Add(tabs); _settingsBody.Children.Add(new Border { Background = DividerBrush, Height = 1, Margin = new Thickness(0, 0, 0, 16) });
        _settingsBody.Children.Add(_settings);
        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        _save.Content = "Save changes"; _save.Click += (_, _) => SaveSettings(); StyleButton(_save, true); actions.Children.Add(_save);
        _saveFeedback.Text = "Changes apply on the next launch."; _saveFeedback.Foreground = MutedBrush; _saveFeedback.FontSize = 12; _saveFeedback.Margin = new Thickness(16, 14, 0, 0); actions.Children.Add(_saveFeedback); _settingsBody.Children.Add(actions);
    }

    private void BuildInstallationPage()
    {
        _installationBody.Children.Add(Display("Installation", 48));
        _installationBody.Children.Add(Label("Install the mod, choose your game folder and manage launcher updates.", 14, MutedBrush, new Thickness(0, 18, 0, 18)));
        var mod = new StackPanel(); mod.Children.Add(Display("Seamless Co-op", 24));
        _version.Foreground = GoldBrush; _version.FontSize = 14; _version.Margin = new Thickness(0, 12, 0, 12); mod.Children.Add(_version);
        _status.Foreground = MutedBrush; _status.FontSize = 13; _status.LineHeight = 18.85; mod.Children.Add(_status); mod.Children.Add(_progress);
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        _getRelease.Content = "Get latest release"; _getRelease.Click += (_, _) => OpenPage(_modDownloadUrl ?? ReleaseCatalog.NexusUrl); StyleButton(_getRelease, true);
        _install.Content = "Install from ZIP…"; _install.Click += async (_, _) => await InstallFromZipAsync(_found); StyleButton(_install);
        _nexus.Content = "Nexus page"; _nexus.Click += (_, _) => OpenPage(ReleaseCatalog.NexusUrl); StyleButton(_nexus);
        _launchInstalled.Content = "Launch installed mod"; _launchInstalled.Click += (_, _) => Launch(); StyleButton(_launchInstalled);
        foreach (var button in new[] { _getRelease, _install, _nexus, _launchInstalled }) { button.Margin = new Thickness(0, 0, 12, 8); actions.Children.Add(button); }
        mod.Children.Add(actions); _installationBody.Children.Add(Card(mod, 20));
        var location = new StackPanel(); location.Children.Add(Display("Elden Ring game folder", 24));
        _gameText.Foreground = MutedBrush; _gameText.FontSize = 13; _gameText.Margin = new Thickness(0, 12, 0, 12); location.Children.Add(_gameText);
        var folderActions = new WrapPanel(); _chooseFolder = ActionButton("Choose folder", async () => await BrowseAsync()); folderActions.Children.Add(_chooseFolder);
        _steam.Click += async (_, _) => await AddToSteamAsync(); StyleButton(_steam); _steam.Margin = new Thickness(12, 0, 0, 0); folderActions.Children.Add(_steam); UpdateSteamButton();
        if (_gamepadMode || SteamIntegration.LaunchedBySteam) _steam.Visibility = Visibility.Collapsed;
        _check.Content = "Check mod updates"; _check.Click += async (_, _) => await CheckAsync(); StyleButton(_check); _check.Margin = new Thickness(12, 0, 0, 0); folderActions.Children.Add(_check);
        location.Children.Add(folderActions); var locationCard = Card(location, 20); locationCard.Margin = new Thickness(0, 12, 0, 0); _installationBody.Children.Add(locationCard);
        var launcher = new StackPanel(); launcher.Children.Add(Display("Launcher update", 24));
        _launcherUpdateStatus.Foreground = MutedBrush; _launcherUpdateStatus.FontSize = 13; _launcherUpdateStatus.Margin = new Thickness(0, 12, 0, 12); launcher.Children.Add(_launcherUpdateStatus);
        _restartUpdate.Content = "Restart to update"; _restartUpdate.Click += async (_, _) => await RestartForLauncherUpdateAsync(); StyleButton(_restartUpdate, true); _restartUpdate.Visibility = Visibility.Collapsed; launcher.Children.Add(_restartUpdate);
        var launcherCard = Card(launcher, 20); launcherCard.Margin = new Thickness(0, 12, 0, 0); _installationBody.Children.Add(launcherCard);
    }

    private void BuildFooter()
    {
        var stack = new StackPanel(); stack.Children.Add(new Border { Height = 1, Background = DividerBrush, Margin = new Thickness(0, 0, 0, 14) });
        if (!_gamepadMode)
        {
            var row = new WrapPanel(); row.Children.Add(Label("Unofficial companion. Not affiliated with FromSoftware or the mod author.", 11, MutedBrush, new Thickness(0, 4, 32, 0)));
            row.Children.Add(LinkButton("Installation", () => SelectPage("Installation")));
            var check = LinkButton("Check mod updates", async () => await CheckAsync()); check.Margin = new Thickness(32, 0, 0, 0); row.Children.Add(check); stack.Children.Add(row);
        }
        if (_gamepadMode)
        {
            var navigation = _hints;
            foreach (var page in new[] { "Play", "Co-op settings", "Installation" })
            {
                var link = LinkButton(page, () => SelectPage(page)); link.MinHeight = 48; link.FontSize = 14; link.Margin = new Thickness(0, 0, 24, 0); navigation.Children.Add(link);
            }

        }
        foreach (var (key, action) in new[] { ("A", "Select"), ("B", "Back / Quit"), ("X", "Save"), ("Y", "Check updates"), ("RT", "Go to Launch"), ("Menu", "Launch"), ("View", "Quit") })
            _hints.Children.Add(Label(key + " " + action, _gamepadMode ? 14 : 12, MutedBrush, new Thickness(0, 0, 16, 0)));
        stack.Children.Add(_hints);
        if (_gamepadMode) stack.Children.Add(Label("The launcher closes after starting the game in Big Picture mode.", 13, MutedBrush, new Thickness(0, 12, 0, 0)));
        _footer.Children.Add(stack);
    }

    private void SelectPage(string page)
    {
        _selectedPage = page;
        _playScroll.Visibility = page == "Play" ? Visibility.Visible : Visibility.Collapsed;
        _settingsScroll.Visibility = page == "Co-op settings" ? Visibility.Visible : Visibility.Collapsed;
        _installationScroll.Visibility = page == "Installation" ? Visibility.Visible : Visibility.Collapsed;
        _settingsVeil.Visibility = page == "Play" ? Visibility.Collapsed : Visibility.Visible;
        _scroll = page == "Play" ? _playScroll : page == "Co-op settings" ? _settingsScroll : _installationScroll;
        foreach (var (name, button) in _navigation)
        {
            button.Background = name == page ? Color("#252B21") : Brushes.Transparent;
            button.BorderBrush = name == page ? Color("#414B36") : Brushes.Transparent;
            button.Foreground = name == page ? GoldBrush : MutedBrush;
        }
        if (page == "Co-op settings") SelectSettingsSection(_selectedSection);
        RefreshCompanionState();
    }

    private void SelectSettingsSection(CompanionSection section)
    {
        _selectedSection = section;
        _settingsTitle.Text = section switch { CompanionSection.Session => "Session settings", CompanionSection.Gameplay => "Gameplay", CompanionSection.Scaling => "Enemy scaling", _ => "Advanced settings" };
        _settingsHelp.Text = section switch
        {
            CompanionSection.Session => "Set the rules for your co-op session. Everyone joining uses the same password.",
            CompanionSection.Gameplay => "Display and startup preferences for your next session.",
            CompanionSection.Scaling => "Percentage added per extra player. Exact fields retain custom values beyond the slider range.",
            _ => "Existing save and locale settings. Additional INI settings remain editable here."
        };
        foreach (var (_, row, target) in _settingRows) row.Visibility = target == section ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (target, button) in _sectionButtons) { button.Foreground = target == section ? GoldBrush : MutedBrush; button.FontWeight = target == section ? FontWeights.Bold : FontWeights.Medium; }
    }

    private void UpdateCompanionLayout()
    {
        var width = _companionRoot.ActualWidth;
        if (width <= 0) return;
        var narrow = width < 1050;
        var side = _gamepadMode ? 0 : narrow ? 180 : 240;
        _companionRoot.ColumnDefinitions[0].Width = new GridLength(side);
        var pad = _gamepadMode ? 72 : narrow ? 24 : 56;
        _contentWidth = Math.Max(280, width - side - pad * 2 - 12);
        _playBody.Margin = new Thickness(pad, narrow ? 72 : _gamepadMode ? 142 : 144, pad, 24);
        _settingsBody.Margin = _installationBody.Margin = new Thickness(pad, narrow ? 64 : 130, pad, 24);
        _footer.Margin = new Thickness(pad, 16, pad, _gamepadMode ? 44 : 40);
        _heroHeading.FontSize = narrow ? 44 : 60; _heroHeading.LineHeight = _heroHeading.FontSize * 1.45;
        _overviewGap.Height = narrow ? 56 : _gamepadMode ? 78 : 124;
        var stackCards = _contentWidth < 850;
        _sessionCard.Width = stackCards ? _contentWidth : (_contentWidth - 32) * 590 / 1056;
        _installationCard.Width = stackCards ? _contentWidth : (_contentWidth - 32) * 466 / 1056;
        _sessionCard.Margin = stackCards ? new Thickness(0, 0, 0, 16) : new Thickness(0, 0, 32, 0);
        foreach (var (_, row, _) in _settingRows) LayoutSettingRow(row);
    }

    private void LayoutSettingRow(Grid row)
    {
        var narrow = _contentWidth < 780;
        row.ColumnDefinitions[0].Width = new GridLength(narrow ? 1 : 720, GridUnitType.Star);
        row.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(44);
        row.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(324, GridUnitType.Star);
        var editor = (FrameworkElement)row.Children[1];
        Grid.SetColumn(editor, narrow ? 0 : 2); Grid.SetRow(editor, narrow ? 1 : 0);
        editor.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        row.Margin = new Thickness(0, 0, 0, narrow ? 24 : 18);
    }

    private void RefreshCompanionState()
    {
        var installed = _game is not null && ModFingerprint.IsInstalled(_game);
        var launchable = CanLaunch();
        _launchInstalled.IsEnabled = !_busy && launchable;
        if (_chooseFolder is not null) _chooseFolder.IsEnabled = !_busy;
        _heroUpdate.Visibility = installed && _modUpdateRequired ? Visibility.Visible : Visibility.Collapsed;
        _launch.Visibility = launchable && !_modUpdateRequired ? Visibility.Visible : Visibility.Collapsed;
        _heroSetup.Visibility = !launchable && !_modUpdateRequired ? Visibility.Visible : Visibility.Collapsed;
        _heroSetup.Content = _game is null ? "Choose game folder" : !installed ? "Install Seamless Co-op" : "Set co-op password";
        _heroSetup.IsEnabled = _heroUpdate.IsEnabled = !_busy;
        _heroHint.Text = _busy ? _status.Text : _modUpdateRequired ? "The author requires a newer mod version before connecting." : !installed ? "Install the mod from a ZIP downloaded from the author's pages." : !launchable ? "Set a co-op password before launching." : "Steam must be running and online to play.";
        _homePassword.Text = _password is null || string.IsNullOrWhiteSpace(_password.Password) ? "Not set" : "••••••••••";
        _homeInstalledVersion.Text = !installed ? "Mod not installed" : _verifiedInstalledTag is not null ? "Verified installed mod " + _verifiedInstalledTag : "Installed version not verified";
        foreach (var (key, text) in _homeRules)
        {
            var entry = _editors.FirstOrDefault(e => e.Entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            var value = entry.Entry is null ? null : entry.Read();
            text.Text = value == "1" ? "On" : value == "0" ? "Off" : value is null ? "Not available" : "Custom";
        }
    }

    private static readonly string[] SettingOrder = ["cooppassword", "allow_invaders", "allow_summons", "death_debuffs", "append_steam_id_to_players", "always_spectate_on_death", "overhead_player_display", "skip_splash_screens", "default_boot_master_volume", "enemy_health_scaling", "enemy_damage_scaling", "enemy_posture_scaling", "boss_health_scaling", "boss_damage_scaling", "boss_posture_scaling", "save_file_extension", "mod_language_override"];
    private static int PresentationOrder(IniEntry entry)
    {
        var index = Array.FindIndex(SettingOrder, key => key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    private static Border Card(UIElement child, double padding) => new() { Background = PanelBrush, Padding = new Thickness(padding), Child = child };
    private static TextBlock Display(string text, double size) { var label = Label(text, size, TextBrush, new Thickness(0)); label.FontFamily = DisplayFont; label.FontWeight = FontWeights.Medium; return label; }
    private static ImageSource Asset(string name) => new BitmapImage(new Uri("pack://application:,,,/ERSCLauncher;component/Assets/" + name, UriKind.Absolute));
    private static System.Windows.Media.Color ColorFrom(string hex, double opacity) { var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex); c.A = (byte)Math.Round(opacity * 255); return c; }
    private static Button ActionButton(string name, Action action, bool primary = false)
    {
        var button = new Button { Content = name, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(button, name); StyleButton(button, primary); button.Click += (_, _) => action(); return button;
    }
    private static Button LinkButton(string name, Action action)
    {
        var button = ActionButton(name, action); button.Background = Brushes.Transparent; button.BorderBrush = Brushes.Transparent; button.Foreground = GoldBrush;
        button.Padding = new Thickness(0); button.MinHeight = 24; button.FontSize = 12; button.FontWeight = FontWeights.SemiBold; return button;
    }
}
