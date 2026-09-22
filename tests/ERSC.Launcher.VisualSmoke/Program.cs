using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ERSC.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the screenshot PNG path.");
        _ = new Application();
        var window = new MainWindow();
        var game = Path.Combine(Path.GetTempPath(), "ersc-visual-" + Guid.NewGuid().ToString("N"), "Game");
        Directory.CreateDirectory(Path.Combine(game, "SeamlessCoop"));
        File.WriteAllText(Path.Combine(game, "eldenring.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "ersc_launcher.exe"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll"), "fixture");
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"), "[GAMEPLAY]\n; Allow players to invade the session.\nallow_invaders = 1\n; Apply a debuff after death.\ndeath_debuffs = 1\n[PASSWORD]\n; Session password\ncooppassword = secret\n");
        typeof(MainWindow).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, game);
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_gameText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = game;
        ((System.Windows.Controls.TextBlock)typeof(MainWindow).GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = "Seamless Co-Op is installed. Version check unavailable.";
        typeof(MainWindow).GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        typeof(MainWindow).GetMethod("UpdateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(860, 760));
        content.Arrange(new Rect(0, 0, 860, 760));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(860, 760, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        using var file = File.Create(args[0]); encoder.Save(file);
        Console.WriteLine(args[0]);
    }
}
