using System.Globalization;

namespace ERSC.Launcher.Core;

public enum SettingKind { Toggle, Choice, Slider, Password, Text }
public sealed record SettingOption(string Value, string Label);
public sealed record SettingPresentation(
    SettingKind Kind,
    string Label,
    string? Help = null,
    IReadOnlyList<SettingOption>? Choices = null,
    int Minimum = 0,
    int Maximum = 0,
    string? Unit = null)
{
    public IReadOnlyList<SettingOption> Options => Choices ?? [];

    private static readonly IReadOnlyList<SettingOption> OverheadOptions =
    [
        new("0", "Normal"),
        new("1", "Hidden"),
        new("2", "Player ping"),
        new("3", "Soul level"),
        new("4", "Death count"),
        new("5", "Soul level and ping")
    ];

    public static SettingPresentation For(IniEntry entry)
    {
        var key = entry.Key.ToLowerInvariant();
        if (key == "cooppassword") return new(SettingKind.Password, "Co-op password", "Share this password only with the players you want to join.");
        if (key == "overhead_player_display")
            return OverheadOptions.Any(o => o.Value == entry.Value)
                ? new(SettingKind.Choice, "Player name display", "Choose what appears above other players.", OverheadOptions)
                : Text(entry);
        if (key == "default_boot_master_volume")
            return ParseInt(entry.Value, out var volume) && volume is >= 0 and <= 10
                ? new(SettingKind.Slider, "Volume before loading a save", "0 is muted; 10 is maximum.", Minimum: 0, Maximum: 10)
                : Text(entry);
        if (key is "allow_invaders" or "death_debuffs" or "allow_summons" or "skip_splash_screens" or "append_steam_id_to_players" or "always_spectate_on_death")
        {
            var (label, help) = key switch
            {
                "allow_invaders" => ("Allow invasions", "Let other players invade your session."),
                "death_debuffs" => ("Death debuffs", "Apply Rot Essence after death until you rest."),
                "allow_summons" => ("Allow spirit summons", "Use spirit summons during multiplayer."),
                "skip_splash_screens" => ("Skip intro logos", "Go straight to the game when it starts."),
                "append_steam_id_to_players" => ("Show player Steam IDs", "Add Steam IDs to overhead player names."),
                _ => ("Always spectate after death", "Stay in spectator mode until the party wipes or rests.")
            };
            return entry.Value is "0" or "1" ? new(SettingKind.Toggle, label, help) : Text(entry);
        }
        if (key is "enemy_health_scaling" or "enemy_damage_scaling" or "enemy_posture_scaling" or
            "boss_health_scaling" or "boss_damage_scaling" or "boss_posture_scaling")
            return ParseInt(entry.Value, out var scaling)
                ? new(SettingKind.Slider, Title(key), entry.Description, Minimum: Math.Min(0, scaling), Maximum: Math.Max(300, scaling), Unit: "%")
                : Text(entry);
        return Text(entry);
    }

    private static SettingPresentation Text(IniEntry entry) => new(SettingKind.Text, Title(entry.Key), entry.Description);
    private static string Title(string key) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    private static bool ParseInt(string raw, out int value) => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
