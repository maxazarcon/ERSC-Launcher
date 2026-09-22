using System.IO.Compression;
using System.Security.Cryptography;

namespace ERSC.Launcher.Core;

public static class ModPackage
{
    public static async Task DownloadAsync(HttpClient client, ModAsset asset, string destination, CancellationToken cancellationToken = default)
    {
        if (asset.Size <= 0 || asset.Size > 256_000_000) throw new InvalidDataException("Release ZIP size is invalid.");
        using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != asset.Size) throw new InvalidDataException("Download size differs from the release metadata.");
        var temp = destination + ".partial";
        try
        {
            await using (var file = File.Create(temp))
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[128 * 1024]; long total = 0; int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > asset.Size) throw new InvalidDataException("Download exceeds the release size.");
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != asset.Size) throw new InvalidDataException("Download is incomplete.");
            }
            if (!string.IsNullOrWhiteSpace(asset.Digest))
            {
                if (!asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsupported release digest.");
                var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp)));
                if (!actual.Equals(asset.Digest[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release digest does not match.");
            }
            File.Move(temp, destination, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
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
