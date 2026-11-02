using System.Text.RegularExpressions;

namespace ERSC.Launcher.Core;

/// <summary>The author's compatibility table can move ahead of the release mirror.
/// Its legacy 2.02 notation corresponds to release v2.0.2; do not interpret it as SemVer 2.2.</summary>
public sealed class ModVersionPolicy(HttpClient client)
{
    public const string Url = "https://raw.githubusercontent.com/yuiamoroll/EldenRingSeamlessCoopRelease/main/VERSION";

    public async Task<Version> GetMinimumAsync(CancellationToken cancellationToken = default) =>
        Parse(await client.GetStringAsync(Url, cancellationToken));

    public static Version Parse(string text)
    {
        if (text.Length > 16_384) throw new InvalidDataException("Author version policy is too large.");
        Version? minimum = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"^(>?)(\d{1,3})\.(\d)(\d)\s+([01])$", RegexOptions.CultureInvariant);
            if (!match.Success || !seen.Add(match.Groups[2].Value + "." + match.Groups[3].Value + match.Groups[4].Value))
                throw new InvalidDataException("Unrecognized author version policy.");
            if (match.Groups[1].Value != ">") continue;
            if (minimum is not null || match.Groups[5].Value != "1")
                throw new InvalidDataException("Unrecognized author version threshold.");
            minimum = new Version(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
        }
        return minimum ?? throw new InvalidDataException("Author version threshold is unavailable.");
    }

    public static bool? MirrorIsBehind(string tag, Version minimum)
    {
        var match = Regex.Match(tag, @"^v?(\d{1,3})\.([0-9])\.([0-9])$", RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)) < minimum;
    }
}
