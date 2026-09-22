using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ERSC.Launcher.Core;

public sealed record LauncherAsset(string Name, string Url, long Size, string? Digest);
public sealed record LauncherRelease(string Tag, bool Prerelease, bool Draft, IReadOnlyList<LauncherAsset> Assets);

public static partial class LauncherUpdates
{
    [GeneratedRegex("^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$")]
    private static partial Regex VersionTag();

    public static LauncherRelease? SelectNewer(IEnumerable<LauncherRelease> releases, Version installed) => releases
        .Where(r => !r.Draft && !r.Prerelease && ParseVersion(r.Tag) is not null)
        .Select(r => (Release: r, Version: ParseVersion(r.Tag)!))
        .Where(x => x.Version > installed)
        .OrderByDescending(x => x.Version)
        .Select(x => x.Release)
        .FirstOrDefault();

    public static LauncherRelease? SelectNewerUsable(IEnumerable<LauncherRelease> releases, Version installed, string repository)
    {
        var candidates = releases.Where(r => !r.Draft && !r.Prerelease && ParseVersion(r.Tag) is not null)
            .OrderByDescending(r => ParseVersion(r.Tag));
        foreach (var release in candidates)
        {
            if (ParseVersion(release.Tag)! <= installed) break;
            try { SelectAsset(release, repository); return release; }
            catch (InvalidDataException) { }
        }
        return null;
    }

    public static Version? ParseVersion(string tag)
    {
        var match = VersionTag().Match(tag);
        return match.Success && Version.TryParse(tag[1..], out var version) ? version : null;
    }

    public static LauncherAsset SelectAsset(LauncherRelease release, string repository)
    {
        if (ParseVersion(release.Tag) is null || !Regex.IsMatch(repository, "^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$"))
            throw new InvalidDataException("Invalid launcher release or repository.");
        var name = $"ERSCLauncher-{release.Tag[1..]}-win-x64.exe";
        var assets = release.Assets.Where(a => a.Name == name).ToArray();
        if (assets.Length != 1) throw new InvalidDataException("Launcher release must have one Windows executable asset.");
        var asset = assets[0];
        var expected = $"https://github.com/{repository}/releases/download/{release.Tag}/{name}";
        if (!asset.Url.Equals(expected, StringComparison.OrdinalIgnoreCase) || asset.Size <= 0 || asset.Size > 300_000_000 ||
            asset.Digest is null || !Regex.IsMatch(asset.Digest, "^sha256:[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("Launcher release asset metadata is invalid.");
        return asset;
    }

    public static async Task DownloadAsync(HttpClient client, LauncherAsset asset, string destination, CancellationToken cancellationToken = default)
    {
        if (asset.Size <= 0 || asset.Size > 300_000_000 || asset.Digest is null || !Regex.IsMatch(asset.Digest, "^sha256:[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("Launcher asset metadata is invalid.");
        var temp = destination + ".partial";
        try
        {
            using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != asset.Size) throw new InvalidDataException("Launcher download size differs from release metadata.");
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(temp);
            using var sha = SHA256.Create();
            var buffer = new byte[128 * 1024]; long total = 0; int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += count;
                if (total > asset.Size) throw new InvalidDataException("Launcher download exceeds expected size.");
                sha.TransformBlock(buffer, 0, count, null, 0);
                await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            if (total != asset.Size) throw new InvalidDataException("Launcher download is incomplete.");
            sha.TransformFinalBlock([], 0, 0);
            if (!Convert.ToHexString(sha.Hash!).Equals(asset.Digest[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Launcher download digest does not match.");
            await file.FlushAsync(cancellationToken);
            file.Close();
            File.Move(temp, destination, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed class GitHubLauncherReleases(HttpClient client, string repository)
{
    public async Task<LauncherRelease?> GetNewerAsync(Version installed, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases?per_page=30");
        request.Headers.UserAgent.ParseAdd("ERSC-Launcher/1.2");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var releases = await response.Content.ReadFromJsonAsync<List<ReleaseDto>>(cancellationToken: cancellationToken);
        return LauncherUpdates.SelectNewerUsable((releases ?? []).Select(r => new LauncherRelease(r.Tag ?? "", r.Prerelease, r.Draft,
            (r.Assets ?? []).Select(a => new LauncherAsset(a.Name ?? "", a.Url ?? "", a.Size, a.Digest)).ToArray())), installed, repository);
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? Tag { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto>? Assets { get; set; }
    }
    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
