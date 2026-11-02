# Elden Ring companion: design-to-feature contract

This is a design and implementation acceptance contract, not a claim that the redesigned app is implemented. Preserve the existing launcher capabilities while replacing its layout. No decorative or unimplemented product actions are acceptable.

Copy direction from Max's review: no decorative eyebrows or redundant product breadcrumbs. Use functional titles: Seamless Co-op, Session settings, Gameplay, Enemy scaling, Advanced settings and Installation. Let the artwork, typography and control design supply the game's atmosphere; do not add poetic taglines or euphemisms for settings.

Figma: https://www.figma.com/design/uwB6ruY6RRFTEW6zuoSF6G?node-id=9-33

## Evidence and limits

Audited `MainWindow.cs`, `SettingPresentation.cs`, `StateStore.cs`, `SteamIntegration.cs`, `App.cs`, `OverlayDialog.cs`, `OnScreenKeyboard.cs`, README, and the player's existing v2.0.1 ZIP's default INI. Do not read or expose the player's password. All 17 default INI keys have named editable design controls; the Figma audit returned no missing keys.

Nexus v2.0.2 is newer than the GitHub mirror and has not yet been downloaded or inspected locally. Future/new INI keys must remain visible through the existing text fallback. Do not claim the 17-key inventory is an exhaustive v2.0.2 schema.

## Settings coverage

Each name below is also the corresponding row's Figma layer name. Settings save through `SaveSettings()` and retain the existing INI editor semantics.

| INI key | Design section | Control and behavior |
| --- | --- | --- |
| cooppassword | Session | Masked password editor; no reveal or copy action |
| allow_invaders | Session | Off/On switch, preserving 0/1 |
| allow_summons | Session | Off/On switch |
| death_debuffs | Session | Off/On switch |
| append_steam_id_to_players | Session | Off/On switch |
| always_spectate_on_death | Session | Off/On switch |
| overhead_player_display | Gameplay | Dropdown: Normal, Hidden, Player ping, Soul level, Death count, Soul level and ping; values 0–5 |
| skip_splash_screens | Gameplay | Off/On switch |
| default_boot_master_volume | Gameplay | Integer slider plus exact field, 0–10 |
| enemy_health_scaling | Enemy scaling | Slider plus exact integer percentage |
| enemy_damage_scaling | Enemy scaling | Slider plus exact integer percentage |
| enemy_posture_scaling | Enemy scaling | Slider plus exact integer percentage |
| boss_health_scaling | Enemy scaling | Slider plus exact integer percentage |
| boss_damage_scaling | Enemy scaling | Slider plus exact integer percentage |
| boss_posture_scaling | Enemy scaling | Slider plus exact integer percentage |
| save_file_extension | Advanced | Text; preserve the author's help and current value |
| mod_language_override | Advanced | Text; blank retains game-language default |

Scaling fields must retain custom integers beyond the initial slider range. Unknown keys, unknown sections and unfamiliar values in otherwise-known keys remain editable as text, with comments and original values preserved. Do not drop them during the new navigation/filtering. All tabs share the same edit buffer; switching a section must not silently reload or discard edits.

## Actions and destinations

| Designed action | Existing implementation or required binding |
| --- | --- |
| Play navigation | Local navigation to the play view; retain the same app/state |
| Co-op settings / Edit / Edit session settings | Local navigation to Session; Edit focuses the existing password editor |
| Session / Gameplay / Enemy scaling / Advanced | Local grouping of the existing `_editors`; no new mod capability |
| Installation / Manage installation | Local navigation to setup and maintenance |
| Launch Seamless Co-op / Launch installed mod | `Launch()`; saves settings, uses mod launcher with game working directory, closes companion in Big Picture mode |
| Save changes | `SaveSettings()`; real INI write and validation/error result |
| Choose folder | `BrowseAsync()` and `GameLocator.Validate()`; accepts root or Game folder |
| Check mod updates | `CheckAsync()`; GitHub metadata plus author VERSION advisory |
| Get latest release / Update Seamless Co-op | Installation view then `_modDownloadUrl`; Nexus when mirror lags or author status is unavailable |
| Nexus page / About the mod | `OpenPage(ReleaseCatalog.NexusUrl)`; external author page, no empty About route |
| Install from ZIP | `InstallFromZipAsync()`; player-downloaded file only, staging, safety checks, backup, settings merge |
| Detected ZIP Install | Existing `_found` behavior; offer only when an actual matching download exists |
| Add to Steam / Update Steam shortcut | `AddToSteamAsync()` and `UpdateSteamButton()`; existing confirmation before closing/reopening Steam, preserve user shortcut options/art |
| Added to Steam | Disabled status; hide Steam shortcut action when launched from Steam or in Big Picture mode |
| Restart to update | `RestartForLauncherUpdateAsync()`; show only when an actual verified launcher payload is ready; save/discard/cancel prompt retained |
| Window minimize/maximize/close | Native WPF window behaviors, implemented explicitly if custom chrome replaces the standard titlebar |
| Quit | Existing `Close()` behavior; controller B retains its confirmation, View quits |
| Dialog Install / Cancel | `OverlayDialog.Result`; install or abandon the pending ZIP operation |
| Keyboard letters/symbols/Shift/?123/Space/Delete/Done | Existing `OnScreenKeyboard` key set and handlers; B cancels without committing text |

Controller-supported is informational, not a button. Automatic game detection and launcher-update checks remain active even though their work does not require a new button. Backup location is status information, not a fake file-manager action.

## Required states and acceptance checks

- No mod: settings unavailable, install action visible, launch disabled.
- Missing/blank password: launch disabled; controller jump-to-launch routes to the password field.
- Installed/offline: preserve launch with installed mod + nonblank password; explicitly say version checks are unavailable. Steam still needs to run online for co-op.
- Mirror lag: display installed/GitHub/author versions separately; do not report up to date; Get latest release directs to Nexus. Keep installed-game launch available without promising the outdated mod will accept it.
- Unknown or changed install fingerprint: say version cannot be verified. A manually installed ZIP is not verified just because its filename contains a version.
- New Nexus ZIP without a matching GitHub digest: retain the unverified ZIP confirmation. Do not invent a GitHub verification badge.
- Installing/checking: visible progress; disable conflicting actions; reject replacing mod files while game/mod launcher runs.
- Installer failure: preserve backup/recovery behavior and show the actual failure. Successful install reloads settings and persists the record.
- Launcher update checking/downloading/ready/failure: quiet distinct status; ready exposes Restart; failures preserve the old executable and next-launch error reporting.
- Save validation/read failures: real in-window error, no fake saved toast; preserve unknown values/comments according to existing editor behavior.
- Controller: preserve D-pad/stick navigation, left/right adjustments, shoulder increments, A/B/X/Y, RT, Menu, View, right-stick scrolling, focus ring, modal focus trap, keyboard and dropdown cancel behavior. Fullscreen starts via existing Steam detection or `--gamepad`; a new unimplemented mode-switch button is not shown.
- Native folder/ZIP pickers still need mouse/keyboard. Existing detected-ZIP installation is the controller path; do not imply a controller-accessible filesystem browser exists.
- Figma navigation and author links are wired. Native file operations/game launch in a Figma study are not real operations; implementation acceptance must exercise every corresponding app handler after design approval.
- Verify at the current 680×540 minimum, the default desktop size, and Big Picture size before release. The current 1440×900 frames establish direction, not minimum-size acceptance.

## Removed speculative controls and claims

Removed password reveal, reset changes and a manual controller-mode switch. Corrected unsupported name-display value “Character name” to the real Normal option. No friends-online count, party presence, character stats, playtime, cloud sync, achievements or save-browser feature is designed. Steam running may use `SteamClient.IsRunning()`; it must never be represented as proof of online co-op connectivity.

Official scene artwork is a design reference. Resolve distribution rights or replace it before packaging. The design is pending Max's approval; no redesigned product UI has been written.
