using System.IO;
using ERSC.Launcher.Core;

namespace ERSC.Launcher;

// Shared by the in-app button and the installer's --add-to-steam / --remove-from-steam calls.
public static class SteamIntegration
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(60);

    public static bool LaunchedBySteam =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamGameId")) || Environment.GetEnvironmentVariable("SteamClientLaunch") == "1";

    public static bool BigPictureRequested =>
        Environment.GetEnvironmentVariable("SteamTenfoot") == "1" || Environment.GetEnvironmentVariable("SteamDeck") == "1";

    private static string Exe => Environment.ProcessPath ?? throw new IOException("Could not locate the running launcher.");

    private static string[] Roots() => GameLocator.FindSteamRoots().Where(root => SteamShortcuts.ShortcutFiles(root).Count > 0).ToArray();

    public static bool IsAdded()
    {
        try { return Roots().Any(root => SteamShortcuts.Contains(root, Exe)); }
        catch { return false; }
    }

    /// <summary>Adds or removes the Steam shortcut, closing and reopening Steam when needed. Returns a message for the user.</summary>
    public static async Task<string> ChangeAsync(bool add, Func<string, Task<bool>> confirm)
    {
        var roots = Roots();
        if (roots.Length == 0)
            return add ? "Steam was not found, or no account has signed in to it on this PC yet." : "Steam was not found.";
        if (!add && !roots.Any(root => SteamShortcuts.Contains(root, Exe))) return "The launcher is not in Steam.";

        var wasRunning = SteamClient.IsRunning();
        if (wasRunning)
        {
            var question = add
                ? "Steam needs to close for a moment so the launcher can be added to your library. Close and reopen Steam now?"
                : "Steam needs to close for a moment so the launcher can be removed from your library. Close and reopen Steam now?";
            if (!await confirm(question)) return "Steam was left open, so nothing changed.";
            if (!await SteamClient.ShutdownAsync(roots[0], ShutdownTimeout))
                return "Steam did not close, so nothing changed. Close Steam and try again.";
        }
        try
        {
            var count = roots.Sum(root => add ? SteamShortcuts.Add(root, Exe) : SteamShortcuts.Remove(root, Exe));
            return add
                ? $"Added to Steam for {count} account{(count == 1 ? "" : "s")}. Find it in your library as \"{SteamShortcuts.AppName}\"."
                : "Removed from Steam.";
        }
        finally
        {
            if (wasRunning) SteamClient.Start(roots[0]);
        }
    }
}
