using System.Security.Cryptography;

namespace ERSC.Launcher.Core;

public static class ModFingerprint
{
    public static bool IsInstalled(string game) => File.Exists(Path.Combine(game, "ersc_launcher.exe"))
        && File.Exists(Path.Combine(game, "SeamlessCoop", "ersc.dll"))
        && File.Exists(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"));

    public static bool Matches(string game, string staged) => IsInstalled(game) && IsInstalled(staged) &&
        PackageFiles(staged).All(relative => FileEquals(Path.Combine(game, relative), Path.Combine(staged, relative)));

    public static IEnumerable<string> PackageFiles(string staged) => Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories)
        .Select(file => Path.GetRelativePath(staged, file))
        .Where(relative => !relative.Equals(Path.Combine("SeamlessCoop", "ersc_settings.ini"), StringComparison.OrdinalIgnoreCase));

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static bool FileEquals(string a, string b) => File.Exists(a) && File.Exists(b) && Hash(a).Equals(Hash(b), StringComparison.OrdinalIgnoreCase);
}

public static class ModInstaller
{
    public static string Install(string game, string staged, string backupRoot)
    {
        if (GameLocator.Validate(game) != Path.GetFullPath(game)) throw new InvalidDataException("Select a valid Elden Ring Game folder.");
        if (!ModFingerprint.IsInstalled(staged)) throw new InvalidDataException("Staged release is incomplete.");
        Directory.CreateDirectory(backupRoot);
        var backup = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var launcher = Path.Combine(game, "ersc_launcher.exe"); var mod = Path.Combine(game, "SeamlessCoop");
        if (File.Exists(launcher)) File.Copy(launcher, Path.Combine(backup, "ersc_launcher.exe"));
        if (Directory.Exists(mod)) CopyDirectory(mod, Path.Combine(backup, "SeamlessCoop"));
        try
        {
            if (Directory.Exists(mod)) Directory.Delete(mod, true);
            CopyDirectory(Path.Combine(staged, "SeamlessCoop"), mod);
            var previousSettings = Path.Combine(backup, "SeamlessCoop", "ersc_settings.ini");
            if (File.Exists(previousSettings))
            {
                var installedSettings = Path.Combine(mod, "ersc_settings.ini");
                var merged = IniDocument.Load(installedSettings);
                merged.MergeValuesFrom(IniDocument.Load(previousSettings));
                merged.Save(installedSettings);
            }
            File.Copy(Path.Combine(staged, "ersc_launcher.exe"), launcher, true);
            if (!ModFingerprint.IsInstalled(game)) throw new IOException("Installed mod files are incomplete.");
            return backup;
        }
        catch
        {
            Restore(game, backup);
            throw;
        }
    }

    public static void Restore(string game, string backup)
    {
        var launcher = Path.Combine(game, "ersc_launcher.exe"); var mod = Path.Combine(game, "SeamlessCoop");
        if (Directory.Exists(mod)) Directory.Delete(mod, true);
        if (File.Exists(launcher)) File.Delete(launcher);
        if (Directory.Exists(Path.Combine(backup, "SeamlessCoop"))) CopyDirectory(Path.Combine(backup, "SeamlessCoop"), mod);
        if (File.Exists(Path.Combine(backup, "ersc_launcher.exe"))) File.Copy(Path.Combine(backup, "ersc_launcher.exe"), launcher);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
