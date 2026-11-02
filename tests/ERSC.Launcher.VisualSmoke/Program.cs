using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ERSC.Launcher;
using ERSC.Launcher.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }

    private static void Run(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the screenshot PNG path.");
        // Overlay dialogs complete through async continuations, which must come back to this UI thread.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        _ = new Application();
        var window = new MainWindow();
        var game = Path.Combine(Path.GetTempPath(), "ersc-visual-" + Guid.NewGuid().ToString("N"), "Game");
        try
        {
        Directory.CreateDirectory(Path.Combine(game, "SeamlessCoop"));
        File.WriteAllText(Path.Combine(game, "eldenring.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "ersc_launcher.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"), "[GAMEPLAY]\n; Allow players to invade the session.\nallow_invaders = 1\n; Apply a debuff after death.\ndeath_debuffs = 1\nallow_summons = 1\nappend_steam_id_to_players = 0\nalways_spectate_on_death = 0\nskip_splash_screens = 0\n; Player display mode.\noverhead_player_display = 5\n; Volume before loading.\ndefault_boot_master_volume = 7\n[SCALING]\n; Enemy health per player.\nenemy_health_scaling = 35\nenemy_damage_scaling = 0\nenemy_posture_scaling = 15\nboss_health_scaling = 100\nboss_damage_scaling = 0\nboss_posture_scaling = 20\n[SAVE]\nsave_file_extension = co2\n[LANGUAGE]\nmod_language_override = \n[PASSWORD]\n; Session password\ncooppassword = secret\n[OTHER]\nfuture_setting = custom\n");
        typeof(MainWindow).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, game);
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_gameText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = game;
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = "Seamless Co-Op is installed. Version check unavailable.";
        typeof(MainWindow).GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var settingsPanel = (System.Windows.Controls.StackPanel)typeof(MainWindow).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Console.WriteLine("settings: " + string.Join(" | ", settingsPanel.Children.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text)));
        typeof(MainWindow).GetMethod("UpdateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        Page(window, "Co-op settings", CompanionSection.Session);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(1440, 900));
        content.Arrange(new Rect(0, 0, 1440, 900));
        content.UpdateLayout();
        var controls = Descendants(content).ToArray();
        Console.WriteLine($"controls: switches={controls.OfType<System.Windows.Controls.CheckBox>().Count()} choices={controls.OfType<System.Windows.Controls.ComboBox>().Count()} sliders={controls.OfType<System.Windows.Controls.Slider>().Count()} passwords={controls.OfType<System.Windows.Controls.PasswordBox>().Count()}");
        if (controls.OfType<System.Windows.Controls.CheckBox>().Count() < 6 ||
            controls.OfType<System.Windows.Controls.ComboBox>().Count() < 1 ||
            controls.OfType<System.Windows.Controls.Slider>().Count() < 7 ||
            controls.OfType<System.Windows.Controls.PasswordBox>().Count() < 1)
        {
            Console.Error.WriteLine("Known INI settings did not render as switches, choices, sliders, and a password field.");
            Environment.ExitCode = 1;
            return;
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
        Directory.CreateDirectory(directory);
        foreach (var section in Enum.GetValues<CompanionSection>())
        {
            Page(window, "Co-op settings", section); content.UpdateLayout(); Save(content, Path.Combine(directory, section + ".png"));
        }
        Page(window, "Installation"); content.UpdateLayout(); Save(content, Path.Combine(directory, "installation.png"));
        Page(window, "Play"); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        using var file = File.Create(args[0]); encoder.Save(file);
        Console.WriteLine(args[0]);

        var combo = controls.OfType<System.Windows.Controls.ComboBox>().Single();
        if (combo.Background is not System.Windows.Media.SolidColorBrush background || background.Color != (Color)ColorConverter.ConvertFromString("#101411") ||
            combo.Foreground is not System.Windows.Media.SolidColorBrush foreground || foreground.Color != (Color)ColorConverter.ConvertFromString("#ECE6D8"))
            throw new Exception("Dropdown selected text lacks a high-contrast face.");
        Page(window, "Co-op settings", CompanionSection.Gameplay);
        content.UpdateLayout();
        var scroll = (ScrollViewer)typeof(MainWindow).GetField("_settingsScroll", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        scroll.ScrollToVerticalOffset(450);
        content.UpdateLayout();
        var choiceBitmap = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32);
        choiceBitmap.Render(content);
        var choiceEncoder = new PngBitmapEncoder(); choiceEncoder.Frames.Add(BitmapFrame.Create(choiceBitmap));
        var choicePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "dropdown.png");
        using (var choiceFile = File.Create(choicePath)) choiceEncoder.Save(choiceFile);
        Console.WriteLine(choicePath);

        controls.OfType<System.Windows.Controls.CheckBox>().First(c => AutomationProperties.GetName(c) == "Allow invasions").IsChecked = false;
        controls.OfType<System.Windows.Controls.ComboBox>().First().SelectedValue = "2";
        controls.OfType<System.Windows.Controls.Slider>().First(s => AutomationProperties.GetName(s) == "Volume before loading a save").Value = 4;
        controls.OfType<System.Windows.Controls.TextBox>().First(t => AutomationProperties.GetName(t) == "Enemy Health Scaling exact value").Text = "375";
        Page(window, "Play"); Page(window, "Installation"); Page(window, "Co-op settings", CompanionSection.Advanced);
        Check(controls.OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Enemy Health Scaling exact value").Text == "375", "Edits survive page and section navigation");
        var saved = (bool)typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
        var ini = File.ReadAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"));
        if (!saved || !ini.Contains("allow_invaders = 0") || !ini.Contains("overhead_player_display = 2") ||
            !ini.Contains("default_boot_master_volume = 4") || !ini.Contains("enemy_health_scaling = 375") ||
            !ini.Contains("; Allow players to invade the session.") || !ini.Contains("future_setting = custom"))
            throw new Exception("Settings controls did not preserve the edited INI values, comments, and unknown key.");
        Console.WriteLine("PASS Settings controls save values and preserve comments and unknown keys");

        VerifyStates(window, content, directory);
        RenderBigPicture(game, directory);
        RunGamepad(window, game, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "gamepad.png"));
        }
        finally
        {
            // Only this invocation's generated fixture directory is disposable.
            var root = Path.GetFullPath(Path.GetDirectoryName(game)!);
            if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("ersc-visual-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected fixture path.");
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // Drives the window with synthetic controller input. Keyboard focus needs a shown, active window,
    // so the content moves into a plain host window (MainWindow itself would start discovering the game).
    private static void RunGamepad(MainWindow window, string game, string screenshot)
    {
        var layers = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Window { Content = layers, Width = 1440, Height = 900, Left = 0, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Background = window.Background };
        host.Show(); host.Activate(); Pump();
        try
        {
            var controls = Descendants(layers).ToArray();
            T Named<T>(string name) where T : FrameworkElement => controls.OfType<T>().First(c => AutomationProperties.GetName(c) == name);

            Page(window, "Co-op settings", CompanionSection.Session); Pump();
            var invade = Named<CheckBox>("Allow invasions");
            invade.Focus(); Pump();
            Check(invade.IsKeyboardFocused, "The test window has keyboard focus");
            var wasChecked = invade.IsChecked == true;
            window.HandleGamepad(GamepadButton.A); Pump();
            Check(invade.IsChecked == !wasChecked, "A toggles a switch");
            Check(AdornerLayer.GetAdornerLayer(invade)?.GetAdorners(invade)?.Length > 0, "The focused control has a focus ring");
            window.HandleGamepad(GamepadButton.Down); Pump();
            Check(Keyboard.FocusedElement is CheckBox next && AutomationProperties.GetName(next) == "Allow spirit summons", "Down moves to the next setting");

            Page(window, "Co-op settings", CompanionSection.Gameplay); Pump();
            var volume = Named<Slider>("Volume before loading a save");
            volume.Focus(); Pump();
            var level = volume.Value;
            window.HandleGamepad(GamepadButton.Right); Pump();
            Check(volume.Value == level + 1, "Right raises a slider");
            window.HandleGamepad(GamepadButton.LeftShoulder); Pump();
            Check(volume.Value == Math.Max(0, level + 1 - 5), "A bumper moves a slider in large steps");

            var choice = controls.OfType<ComboBox>().Single();
            choice.Focus(); Pump();
            var index = choice.SelectedIndex;
            window.HandleGamepad(GamepadButton.Right); Pump();
            Check(choice.SelectedIndex == index + 1, "Right cycles a dropdown");
            window.HandleGamepad(GamepadButton.A); Pump();
            Check(choice.IsDropDownOpen, "A opens a dropdown");
            window.HandleGamepad(GamepadButton.Down); Pump();
            Check(choice.SelectedIndex == index + 2, "Down moves within an open dropdown");
            window.HandleGamepad(GamepadButton.B); Pump();
            Check(!choice.IsDropDownOpen && choice.SelectedIndex == index + 1 && choice.IsKeyboardFocused, "B closes a dropdown and keeps the earlier choice");

            Page(window, "Co-op settings", CompanionSection.Session); Pump();
            var password = controls.OfType<PasswordBox>().Single();
            password.Focus(); Pump();
            window.HandleGamepad(GamepadButton.A); Pump();
            var keyboard = Descendants(layers).OfType<OnScreenKeyboard>().SingleOrDefault();
            Check(keyboard is not null, "A on a text field opens the on-screen keyboard");
            Check(new[] { "Space", "Delete", "Done" }.All(label => Descendants(keyboard!).OfType<Button>().Any(b => b.Content as string == label)), "Keyboard action keys have visible labels");
            for (var i = 0; i < 12; i++) window.HandleGamepad(GamepadButton.X);
            window.HandleGamepad(GamepadButton.A);                                   // q
            window.HandleGamepad(GamepadButton.Right); window.HandleGamepad(GamepadButton.A); // w
            window.HandleGamepad(GamepadButton.Y); window.HandleGamepad(GamepadButton.A);     // W
            window.HandleGamepad(GamepadButton.Down); window.HandleGamepad(GamepadButton.A);  // s
            Pump();
            Check(keyboard!.Text == "qwWs", "The on-screen keyboard types, deletes and shifts");
            Save(layers, screenshot);
            window.HandleGamepad(GamepadButton.Start); Pump();
            Check(password.Password == "qwWs" && !Descendants(layers).OfType<OnScreenKeyboard>().Any(), "Start confirms the typed text");
            Check(password.IsKeyboardFocused, "Focus returns to the edited field");

            var ask = (Task<int>)typeof(MainWindow).GetMethod("AskAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, ["Test", "Pick one", new[] { "Yes", "No" }])!;
            Pump();
            window.HandleGamepad(GamepadButton.Right); window.HandleGamepad(GamepadButton.A); Pump();
            Check(ask.IsCompleted && ask.Result == 1, "A controller can answer an in-window dialog");
            Check(!Descendants(layers).OfType<OverlayDialog>().Any(), "The dialog closes after answering");

            window.HandleGamepad(GamepadButton.X); Pump();
            Check(File.ReadAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini")).Contains("cooppassword = qwWs"), "X saves settings");

            var launch = (Button)typeof(MainWindow).GetField("_launch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(launch.IsEnabled, "Launch is available with a password set");
            invade.Focus(); Pump();
            window.HandleGamepad(GamepadButton.RightTrigger); Pump();
            Check(launch.IsKeyboardFocused, "RT jumps to the Launch button");
            Page(window, "Co-op settings", CompanionSection.Session); invade.Focus(); Pump();
            window.HandleGamepad(GamepadButton.RightShoulder); Pump();
            Check(launch.IsKeyboardFocused, "RB jumps to the Launch button when not on a slider");

            window.HandleGamepad(GamepadButton.B); Pump();
            Check(Descendants(layers).OfType<OverlayDialog>().Any(), "B on the main screen asks to quit");
            window.HandleGamepad(GamepadButton.B); Pump();
            Check(!Descendants(layers).OfType<OverlayDialog>().Any() && launch.IsKeyboardFocused, "B again cancels quitting and restores focus");
            foreach (var page in new[] { "Play", "Co-op settings", "Installation" })
            {
                Page(window, page); Pump(); host.Width = 680; host.Height = 540; Pump();
                Save(layers, Path.Combine(Path.GetDirectoryName(screenshot)!, "minimum-" + page.Replace(" ", "-") + ".png"));
            }
            Console.WriteLine("PASS Controller navigates, edits, types and saves settings");
        }
        finally { host.Content = null; host.Close(); }
    }

    private static void VerifyStates(MainWindow window, FrameworkElement content, string directory)
    {
        void Set(string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
        T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        void Refresh() => typeof(MainWindow).GetMethod("UpdateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        Page(window, "Play");
        Set("_modUpdateRequired", true); Refresh(); content.UpdateLayout();
        Check(Field<Button>("_launch").Visibility == Visibility.Collapsed && Field<Button>("_launchInstalled").IsEnabled, "Known old mod directs to Update while installed launch stays available");
        Save(content, Path.Combine(directory, "update-required.png"));
        Set("_modUpdateRequired", false); Set("_busy", true); Refresh(); content.UpdateLayout();
        Check(!Field<Button>("_chooseFolder").IsEnabled && !Field<Button>("_install").IsEnabled && !Field<Button>("_launchInstalled").IsEnabled, "Busy operations disable conflicting folder, install and launch actions");
        Set("_busy", false); Set("_steamBusy", true); Refresh();
        Check(!Field<Button>("_steam").IsEnabled, "Refreshing other controls cannot re-enable a pending Steam operation");
        Set("_steamBusy", false); Refresh();
        var password = Field<PasswordBox>("_password"); var original = password.Password; password.Password = ""; Refresh();
        Check(!Field<Button>("_launch").IsEnabled && Field<Button>("_heroSetup").Visibility == Visibility.Visible, "Blank password exposes setup and blocks launch");
        typeof(MainWindow).GetMethod("SnapToLaunch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        Check(Field<ScrollViewer>("_settingsScroll").Visibility == Visibility.Visible, "Missing-password jump reveals Session settings");
        password.Password = original; Refresh();
        Console.WriteLine("PASS Busy, update advisory and missing-password states");
    }

    private static void RenderBigPicture(string game, string directory)
    {
        var window = new MainWindow(true);
        typeof(MainWindow).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, game);
        typeof(MainWindow).GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        typeof(MainWindow).GetMethod("UpdateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(1440, 900)); content.Arrange(new Rect(0, 0, 1440, 900)); content.UpdateLayout();
        Save(content, Path.Combine(directory, "big-picture.png"));
        foreach (var page in new[] { "Co-op settings", "Installation", "Play" })
        { Page(window, page); content.UpdateLayout(); }
        Check(Descendants(content).OfType<Button>().Count(b => (b.Content as string) is "Play" or "Co-op settings" or "Installation") >= 3, "Full-screen mode retains all page navigation");
        Console.WriteLine("PASS Full-screen layout and page navigation");
        window.Content = null;
    }

    private static void Page(MainWindow window, string page, CompanionSection section = CompanionSection.Session)
    {
        typeof(MainWindow).GetMethod("SelectPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [page]);
        typeof(MainWindow).GetMethod("SelectSettingsSection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [section]);
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception("Controller check failed: " + what);
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Save(FrameworkElement content, string path)
    {
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
        Console.WriteLine(path);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
