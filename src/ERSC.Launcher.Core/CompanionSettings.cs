namespace ERSC.Launcher.Core;

public enum CompanionSection { Session, Gameplay, Scaling, Advanced }

/// <summary>View grouping only. Never rewrites INI values or removes unknown entries.</summary>
public static class CompanionSettings
{
    public static CompanionSection SectionFor(IniEntry entry)
    {
        // A future/custom value in a known key must stay reachable in the text fallback.
        if (SettingPresentation.For(entry).Kind == SettingKind.Text) return CompanionSection.Advanced;
        return entry.Key.ToLowerInvariant() switch
        {
            "cooppassword" or "allow_invaders" or "death_debuffs" or "allow_summons" or
            "append_steam_id_to_players" or "always_spectate_on_death" => CompanionSection.Session,
            "overhead_player_display" or "skip_splash_screens" or "default_boot_master_volume" => CompanionSection.Gameplay,
            "enemy_health_scaling" or "enemy_damage_scaling" or "enemy_posture_scaling" or
            "boss_health_scaling" or "boss_damage_scaling" or "boss_posture_scaling" => CompanionSection.Scaling,
            _ => CompanionSection.Advanced
        };
    }
}
