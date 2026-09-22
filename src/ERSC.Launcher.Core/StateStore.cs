using System.Text.Json;

namespace ERSC.Launcher.Core;

public sealed class LauncherState
{
    public string? SelectedGame { get; set; }
    public Dictionary<string, InstalledRecord> Installs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
public sealed record InstalledRecord(string Tag, Dictionary<string, string> Files);

public sealed class StateStore(string directory)
{
    private string FilePath => Path.Combine(directory, "state.json");
    public LauncherState Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (JsonException) { return new(); }
    }
    public void Save(LauncherState state)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public static InstalledRecord Record(string tag, string game, string staged) => new(tag,
        ModFingerprint.PackageFiles(staged).ToDictionary(relative => relative, relative => ModFingerprint.Hash(Path.Combine(game, relative)), StringComparer.OrdinalIgnoreCase));
    public static bool Matches(string game, InstalledRecord record) => ModFingerprint.IsInstalled(game) && record.Files is { Count: > 0 }
        && record.Files.All(item => File.Exists(Path.Combine(game, item.Key)) && ModFingerprint.Hash(Path.Combine(game, item.Key)).Equals(item.Value, StringComparison.OrdinalIgnoreCase));
}
