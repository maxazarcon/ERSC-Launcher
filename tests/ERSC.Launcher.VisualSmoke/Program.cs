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
        Directory.CreateDirectory(Path.Combine(game, "SeamlessCoop"));
        File.WriteAllText(Path.Combine(game, "eldenring.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "ersc_launcher.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"), "[GAMEPLAY]\n; Allow players to invade the session.\nallow_invaders = 1\n; Apply a debuff after death.\ndeath_debuffs = 1\n; Player display mode.\noverhead_player_display = 5\n; Volume before loading.\ndefault_boot_master_volume = 7\n[SCALING]\n; Enemy health per player.\nenemy_health_scaling = 35\n[PASSWORD]\n; Session password\ncooppassword = secret\n[OTHER]\nfuture_setting = custom\n");
        typeof(MainWindow).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, game);
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_gameText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = game;
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = "Seamless Co-Op is installed. Version check unavailable.";
        typeof(MainWindow).GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var settingsPanel = (System.Windows.Controls.StackPanel)typeof(MainWindow).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Console.WriteLine("settings: " + string.Join(" | ", settingsPanel.Children.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text)));
        typeof(MainWindow).GetMethod("UpdateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(860, 760));
        content.Arrange(new Rect(0, 0, 860, 760));
        content.UpdateLayout();
        var controls = Descendants(content).ToArray();
        Console.WriteLine($"controls: switches={controls.OfType<System.Windows.Controls.CheckBox>().Count()} choices={controls.OfType<System.Windows.Controls.ComboBox>().Count()} sliders={controls.OfType<System.Windows.Controls.Slider>().Count()} passwords={controls.OfType<System.Windows.Controls.PasswordBox>().Count()}");
        if (controls.OfType<System.Windows.Controls.CheckBox>().Count() < 2 ||
            controls.OfType<System.Windows.Controls.ComboBox>().Count() < 1 ||
            controls.OfType<System.Windows.Controls.Slider>().Count() < 2 ||
            controls.OfType<System.Windows.Controls.PasswordBox>().Count() < 1)
        {
            Console.Error.WriteLine("Known INI settings did not render as switches, choices, sliders, and a password field.");
            Environment.ExitCode = 1;
            return;
        }
        var bitmap = new RenderTargetBitmap(860, 760, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        using var file = File.Create(args[0]); encoder.Save(file);
        Console.WriteLine(args[0]);

        var combo = controls.OfType<System.Windows.Controls.ComboBox>().Single();
        if (combo.Background is not System.Windows.Media.SolidColorBrush background || background.Color != (Color)ColorConverter.ConvertFromString("#141713") ||
            combo.Foreground is not System.Windows.Media.SolidColorBrush foreground || foreground.Color != (Color)ColorConverter.ConvertFromString("#EEEDE3"))
            throw new Exception("Dropdown selected text lacks a high-contrast face.");
        var scroll = controls.OfType<System.Windows.Controls.ScrollViewer>().First(s => s.Content is System.Windows.Controls.StackPanel);
        scroll.ScrollToVerticalOffset(450);
        content.UpdateLayout();
        var choiceBitmap = new RenderTargetBitmap(860, 760, 96, 96, PixelFormats.Pbgra32);
        choiceBitmap.Render(content);
        var choiceEncoder = new PngBitmapEncoder(); choiceEncoder.Frames.Add(BitmapFrame.Create(choiceBitmap));
        var choicePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "dropdown.png");
        using (var choiceFile = File.Create(choicePath)) choiceEncoder.Save(choiceFile);
        Console.WriteLine(choicePath);

        controls.OfType<System.Windows.Controls.CheckBox>().First(c => AutomationProperties.GetName(c) == "Allow invasions").IsChecked = false;
        controls.OfType<System.Windows.Controls.ComboBox>().First().SelectedValue = "2";
        controls.OfType<System.Windows.Controls.Slider>().First(s => AutomationProperties.GetName(s) == "Volume before loading a save").Value = 4;
        controls.OfType<System.Windows.Controls.TextBox>().First(t => AutomationProperties.GetName(t) == "Enemy Health Scaling exact value").Text = "375";
        var saved = (bool)typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
        var ini = File.ReadAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"));
        if (!saved || !ini.Contains("allow_invaders = 0") || !ini.Contains("overhead_player_display = 2") ||
            !ini.Contains("default_boot_master_volume = 4") || !ini.Contains("enemy_health_scaling = 375") ||
            !ini.Contains("; Allow players to invade the session.") || !ini.Contains("future_setting = custom"))
            throw new Exception("Settings controls did not preserve the edited INI values, comments, and unknown key.");
        Console.WriteLine("PASS Settings controls save values and preserve comments and unknown keys");

        RunGamepad(window, game, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "gamepad.png"));
    }

    // Drives the window with synthetic controller input. Keyboard focus needs a shown, active window,
    // so the content moves into a plain host window (MainWindow itself would start discovering the game).
    private static void RunGamepad(MainWindow window, string game, string screenshot)
    {
        var layers = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Window { Content = layers, Width = 860, Height = 760, Left = 0, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Background = window.Background };
        host.Show(); host.Activate(); Pump();
        try
        {
            var controls = Descendants(layers).ToArray();
            T Named<T>(string name) where T : FrameworkElement => controls.OfType<T>().First(c => AutomationProperties.GetName(c) == name);

            var invade = Named<CheckBox>("Allow invasions");
            invade.Focus(); Pump();
            Check(invade.IsKeyboardFocused, "The test window has keyboard focus");
            var wasChecked = invade.IsChecked == true;
            window.HandleGamepad(GamepadButton.A); Pump();
            Check(invade.IsChecked == !wasChecked, "A toggles a switch");
            Check(AdornerLayer.GetAdornerLayer(invade)?.GetAdorners(invade)?.Length > 0, "The focused control has a focus ring");
            window.HandleGamepad(GamepadButton.Down); Pump();
            Check(Keyboard.FocusedElement is CheckBox next && AutomationProperties.GetName(next) == "Death debuffs", "Down moves to the next setting");

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

            var password = controls.OfType<PasswordBox>().Single();
            password.Focus(); Pump();
            window.HandleGamepad(GamepadButton.A); Pump();
            var keyboard = Descendants(layers).OfType<OnScreenKeyboard>().SingleOrDefault();
            Check(keyboard is not null, "A on a text field opens the on-screen keyboard");
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

            var launch = controls.OfType<Button>().First(b => b.Content as string == "Launch Seamless Co-Op");
            Check(launch.IsEnabled, "Launch is available with a password set");
            invade.Focus(); Pump();
            window.HandleGamepad(GamepadButton.RightTrigger); Pump();
            Check(launch.IsKeyboardFocused, "RT jumps to the Launch button");
            invade.Focus(); Pump();
            window.HandleGamepad(GamepadButton.RightShoulder); Pump();
            Check(launch.IsKeyboardFocused, "RB jumps to the Launch button when not on a slider");

            window.HandleGamepad(GamepadButton.B); Pump();
            Check(Descendants(layers).OfType<OverlayDialog>().Any(), "B on the main screen asks to quit");
            window.HandleGamepad(GamepadButton.B); Pump();
            Check(!Descendants(layers).OfType<OverlayDialog>().Any() && launch.IsKeyboardFocused, "B again cancels quitting and restores focus");
            Console.WriteLine("PASS Controller navigates, edits, types and saves settings");
        }
        finally { host.Content = null; host.Close(); }
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
