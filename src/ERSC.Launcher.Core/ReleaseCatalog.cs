using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ERSC.Launcher.Core;

public sealed record ModAsset(string Name, string Url, long Size, string? Digest);
public sealed record ModRelease(string Tag, bool Prerelease, bool Draft, DateTimeOffset PublishedAt, IReadOnlyList<ModAsset> Assets);

public static class ReleaseCatalog
{
    public const string Repository = "yuiamoroll/EldenRingSeamlessCoopRelease";
    public const string NexusUrl = "https://www.nexusmods.com/eldenring/mods/510";

    /// <summary>The GitHub page a player downloads the release ZIP from.</summary>
    public static string ReleasePageUrl(string? tag) => string.IsNullOrWhiteSpace(tag)
        ? $"https://github.com/{Repository}/releases/latest"
        : $"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}";

    public static ModRelease SelectNewest(IEnumerable<ModRelease> releases) => releases
        .Where(r => !r.Draft && !string.IsNullOrWhiteSpace(r.Tag))
        .OrderByDescending(r => r.PublishedAt)
        .FirstOrDefault() ?? throw new InvalidDataException("No published Seamless Co-Op release was found.");

    public static ModAsset SelectZip(ModRelease release)
    {
        var zips = release.Assets.Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            && a.Url.StartsWith("https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases/download/", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (zips.Length != 1) throw new InvalidDataException($"Release {release.Tag} must have exactly one mod ZIP asset; found {zips.Length}.");
        return zips[0];
    }
}

public sealed class GitHubReleases(HttpClient client)
{
    private const string Url = "https://api.github.com/repos/" + ReleaseCatalog.Repository + "/releases?per_page=20";

    // Only release metadata is read here. The ZIP itself comes from the player's own download.
    public async Task<ModRelease> GetNewestAsync(CancellationToken cancellationToken = default) =>
        ReleaseCatalog.SelectNewest(await GetRecentAsync(cancellationToken));

    public async Task<IReadOnlyList<ModRelease>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        request.Headers.UserAgent.ParseAdd("ERSC-Launcher/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken: cancellationToken);
        return (data ?? []).Select(x => new ModRelease(x.Tag ?? "", x.Prerelease, x.Draft, x.PublishedAt,
            (x.Assets ?? []).Select(a => new ModAsset(a.Name ?? "", a.Url ?? "", a.Size, a.Digest)).ToArray())).ToArray();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? Tag { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    }
    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
