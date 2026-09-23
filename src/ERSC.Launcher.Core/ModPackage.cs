using System.IO.Compression;
using System.Security.Cryptography;

namespace ERSC.Launcher.Core;

public static class ModPackage
{
    public const long MaxZipSize = 256_000_000;

    /// <summary>Finds the published release a ZIP the player downloaded came from, by its SHA-256 digest. Null when none match.</summary>
    public static ModRelease? Identify(string zipPath, IEnumerable<ModRelease> releases)
    {
        var length = new FileInfo(zipPath).Length;
        if (length <= 0 || length > MaxZipSize) throw new InvalidDataException("The ZIP is empty or too large to be a Seamless Co-Op release.");
        string? hash = null;
        foreach (var release in releases)
            foreach (var asset in release.Assets.Where(a => a.Size == length && HasDigest(a)))
            {
                hash ??= Sha256(zipPath);
                if (hash.Equals(asset.Digest![7..], StringComparison.OrdinalIgnoreCase)) return release;
            }
        return null;
    }

    /// <summary>Looks in a folder (normally Downloads) for a ZIP matching the release asset, newest first. Null when there is none.</summary>
    public static string? FindDownloaded(string folder, ModAsset asset)
    {
        if (!HasDigest(asset) || !Directory.Exists(folder)) return null;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.zip").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try { if (new FileInfo(file).Length == asset.Size && Sha256(file).Equals(asset.Digest![7..], StringComparison.OrdinalIgnoreCase)) return file; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private static bool HasDigest(ModAsset asset) => asset.Digest is { Length: 71 } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase);

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static void Extract(string zipPath, string destination)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > 5000) throw new InvalidDataException("Release ZIP contains too many files.");
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray();
        foreach (var name in names)
        {
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(p => p == ".." || p == "."))
                throw new InvalidDataException("Release ZIP contains an unsafe path.");
        }
        var launcher = names.SingleOrDefault(n => n.EndsWith("/ersc_launcher.exe", StringComparison.OrdinalIgnoreCase) || n.Equals("ersc_launcher.exe", StringComparison.OrdinalIgnoreCase));
        if (launcher is null) throw new InvalidDataException("Release ZIP has no ersc_launcher.exe.");
        var prefix = launcher[..^"ersc_launcher.exe".Length];
        var required = new[] { prefix + "SeamlessCoop/ersc.dll", prefix + "SeamlessCoop/ersc_settings.ini" };
        if (required.Any(r => !names.Contains(r, StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Release ZIP is missing required mod files.");
        var entries = zip.Entries.Where(e => e.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Sum(e => e.Length) > 512_000_000) throw new InvalidDataException("Release ZIP expands beyond the allowed size.");
        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            var name = entry.FullName.Replace('\\', '/')[prefix.Length..];
            if (name.Length == 0 || name.EndsWith('/')) continue;
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("Release ZIP contains a symbolic link.");
            var target = Path.GetFullPath(Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release ZIP escapes staging.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
    }
}
