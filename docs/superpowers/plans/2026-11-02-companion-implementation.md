# Approved ERSC companion implementation

Approved by Max on November 2 after reviewing the actual Figma frames. Implement on `dev/companion-redesign` from `595e13d`; preserve the separate `1.7.1` update-check fix. Max subsequently authorized public release so existing clients receive it; the original replacement artwork was separately approved in Figma.

## Scope

Figma file `uwB6ruY6RRFTEW6zuoSF6G`, Companion redesign page. Main frames: Play `9:33`, Session `9:165`, Gameplay `16:31`, Scaling `16:151`, Advanced `16:312`, Installation `16:420`, Controller `19:39`, ZIP confirmation `19:123`, keyboard `19:204`. Follow the repository feature contract. Functional titles; no decorative eyebrows. Original replacement artwork in release frames `31:57` and `31:133` is approved; official screenshot study is not bundled.

- [x] Prepare exact Figma assets and licensed static font resources; no runtime Figma URLs.
- [x] Add a failing settings-section regression test for all 17 existing keys, unknown keys, and unfamiliar values; implement the pure classification policy without altering saved INI data.
- [x] Replace the WPF layout with a partial-class companion shell. Reuse real install/update/launch/editor/controller handlers; preserve edit buffers across navigation. Bind summaries and states to live app data.
- [x] Update meaningful visual/controller checks for section navigation and saving, test all existing logic, build, and inspect rendered desktop, minimum-size and Big Picture views against Figma. Review the completed branch before committing.

## Review focus

- Unknown settings and unexpected values stay reachable; navigation cannot discard edits.
- Offline/unverified/author-policy failures do not block installed-game launch.
- Missing game, missing password, busy install, mod update and launcher update have distinct actionable states.
- Controller focus can reach hidden-section fields, return to launch, cancel dropdowns/keyboard/dialogs, and save.
- Native folder/ZIP pickers remain mouse/keyboard surfaces; no fake picker or telemetry is added.

Code Connect publishing was attempted at Max's request, but Figma rejected it because this library is not on an Organization/Enterprise plan. Keep local source references; do not claim published mappings.

## Verification and rollout

November 2: core checks and WPF visual/controller checks passed. Rendered all settings sections, desktop Play/Installation, known-old update, 680x540 layouts and actual full-screen constructor. Review found and corrected busy folder selection, stale version labels, mirror-versus-installed age, stale save feedback and overlapping Steam operations; targeted re-review found no remaining issue in those fixes.

The author Nexus v2.0.2 ZIP contains the same 17 defaults. Installed through the redesigned handler; old backup hashes match, new binaries match the downloaded ZIP, all 17 existing values including the masked password were preserved. Max confirmed the game ran. This does not establish multiplayer-session acceptance or a physical gamepad test.

Public release authorized after original artwork approval. Release/updater verification follows the GitHub release workflow; source plan completion does not itself claim a published asset.
