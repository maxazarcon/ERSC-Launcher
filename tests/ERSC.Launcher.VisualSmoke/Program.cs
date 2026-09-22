using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
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
