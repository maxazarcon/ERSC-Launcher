using System.Windows;
using System.Diagnostics;
using System.IO;
using ERSC.Launcher.Core;
using Microsoft.Win32;

namespace ERSC.Launcher;

public static class App
{
    // Must match AppId in installer/ERSCLauncher.iss; Inno Setup appends "_is1" to the uninstall key.
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F1C2D84-3B7A-4E59-9C0D-8A2E5B7F4C13}_is1";

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 5 && args[0] == "--apply-launcher-update")
        {
            ApplyUpdate(args);
            return;
        }
        if (args.Length == 1 && args[0] is "--add-to-steam" or "--remove-from-steam")
        {
            ChangeSteamShortcut(args[0] == "--add-to-steam");
            return;
        }
        var gamepad = args.Contains("--gamepad", StringComparer.OrdinalIgnoreCase) || SteamIntegration.BigPictureRequested;
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new MainWindow(gamepad));
    }

    // Called by the installer and uninstaller, which have no window of their own to report through.
    private static void ChangeSteamShortcut(bool add)
    {
        var title = add ? "Add to Steam" : "Remove from Steam";
        try
        {
            var message = SteamIntegration.ChangeAsync(add, question =>
                Task.FromResult(MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)).GetAwaiter().GetResult();
            if (add) MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // Keep Apps & Features showing the running version after an in-place update of an installed copy.
    private static void UpdateInstalledVersion(string target)
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
        if (key?.GetValue("InstallLocation") is not string location ||
            !Path.GetFullPath(location).TrimEnd('\\').Equals(Path.GetDirectoryName(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase)) return;
        var version = FileVersionInfo.GetVersionInfo(target).FileVersion;
        if (Version.TryParse(version, out var parsed)) key.SetValue("DisplayVersion", $"{parsed.Major}.{parsed.Minor}.{parsed.Build}");
    }

    private static void ApplyUpdate(string[] args)
    {
        var target = args[1]; var payload = args[2]; var backup = args[3];
        try
        {
            if (!int.TryParse(args[4], out var processId)) throw new InvalidDataException("Invalid updater process ID.");
            try { using var process = Process.GetProcessById(processId); if (!process.WaitForExit(30000)) throw new IOException("The launcher did not close in time."); }
            catch (ArgumentException) { }
            LauncherReplacement.Replace(target, payload, backup, path =>
            {
                if (Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = true }) is null)
                    throw new IOException("Could not restart the launcher.");
            });
            try { File.Delete(payload); } catch { }
            try { UpdateInstalledVersion(target); } catch { }
        }
        catch (Exception ex)
        {
            try { LauncherUpdateErrors.Write(Path.GetDirectoryName(backup)!, "The launcher update failed: " + ex.Message + "\nPrevious version: " + backup); } catch { }
            try { if (File.Exists(target)) Process.Start(new ProcessStartInfo(target) { WorkingDirectory = Path.GetDirectoryName(target)!, UseShellExecute = true }); } catch { }
        }
    }
}
