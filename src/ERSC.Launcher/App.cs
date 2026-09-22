using System.Windows;
using System.Diagnostics;
using System.IO;
using ERSC.Launcher.Core;

namespace ERSC.Launcher;

public static class App
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 5 && args[0] == "--apply-launcher-update")
        {
            ApplyUpdate(args);
            return;
        }
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new MainWindow());
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
        }
        catch (Exception ex)
        {
            try { LauncherUpdateErrors.Write(Path.GetDirectoryName(backup)!, "The launcher update failed: " + ex.Message + "\nPrevious version: " + backup); } catch { }
            try { if (File.Exists(target)) Process.Start(new ProcessStartInfo(target) { WorkingDirectory = Path.GetDirectoryName(target)!, UseShellExecute = true }); } catch { }
        }
    }
}
