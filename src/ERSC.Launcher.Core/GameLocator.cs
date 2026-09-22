using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ERSC.Launcher.Core;

public static class GameLocator
{
    public static string? Validate(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected)) return null;
        var path = Path.GetFullPath(selected.Trim().Trim('"'));
        if (File.Exists(Path.Combine(path, "eldenring.exe"))) return path;
        var child = Path.Combine(path, "Game");
        return File.Exists(Path.Combine(child, "eldenring.exe")) ? Path.GetFullPath(child) : null;
    }

    public static IEnumerable<string> FindInSteam(string steamRoot)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            var content = File.ReadAllText(vdf);
            foreach (Match match in Regex.Matches(content, "\"path\"\\s+\"((?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
                roots.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
            foreach (Match match in Regex.Matches(content, "\"\\d+\"\\s+\"((?:\\\\.|[^\"])*)\""))
                roots.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (var root in roots)
        {
            var manifest = Path.Combine(root, "steamapps", "appmanifest_1245620.acf");
            if (!File.Exists(manifest)) continue;
            var value = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!value.Success) continue;
            var game = Validate(Path.Combine(root, "steamapps", "common", value.Groups[1].Value));
            if (game is not null) yield return game;
        }
    }

    public static IReadOnlyList<string> FindInstalled()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var key in new[] { Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"), Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") })
            {
                using (key)
                {
                    var path = key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(path)) roots.Add(path);
                }
            }
        }
        catch { /* Registry discovery is best effort; the browse control remains available. */ }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return roots.Where(Directory.Exists).SelectMany(root => FindInSteam(root)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
