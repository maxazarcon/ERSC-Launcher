# Changelog

## 1.7.0 — 2026-09-23

- The launcher no longer downloads Seamless Co-Op. Select **Get latest release** (or **Nexus page**) to download the ZIP yourself, then **Install from ZIP…**. The mod's author does not allow redistribution, so the mod now always comes from the author's own pages.
- A ZIP is checked against the SHA-256 digest GitHub publishes for each release. A ZIP that matches no recent release can still be installed after a warning.
- When the latest release ZIP is already in your Downloads folder, the launcher finds it and offers to install it, which works with a controller.
- Say clearly that the launcher is unofficial. The app, installer and Steam shortcut are now named "Seamless Co-Op Launcher (Unofficial)". **Update Steam shortcut** renames an existing shortcut and keeps its launch options and custom art.

## 1.6.0 — 2026-09-23

- Give the Steam shortcut its own library artwork: a portrait cover, wide cover, banner and logo. Art you set yourself in Steam is kept, and removing the shortcut only deletes the launcher's own images.

## 1.5.0 — 2026-09-23

- Press B on the main screen to quit (with a confirmation). View still quits too.
- Show the Quit button whenever a controller is connected, not only in full screen.
- Press RT to jump straight to the Launch button. RB does the same unless a slider is selected. If launching isn't possible yet, focus goes to what's missing instead.

## 1.4.0 — 2026-09-23

- Add a per-user Windows installer with a Start Menu entry, optional desktop shortcut, and uninstaller. The portable EXE is still published.
- Add the launcher to Steam as a non-Steam game from the installer or the new **Add to Steam** button, closing and reopening Steam when needed.
- Add controller support: move through every setting, change switches, sliders and dropdowns, type with an on-screen keyboard, save, and launch.
- Open full screen with a larger layout in Big Picture mode, on Steam Deck, or with `--gamepad`, and close after starting the game.
- Show confirmations and errors inside the launcher window so they work with a controller.
- Add an app icon.

## 1.3.0 — 2026-09-23

- Move launcher update status into a slim footer so the game folder and installation come first.
- Group settings into one card per section with dividers instead of a card per setting.
- Add hover and pressed states to buttons, a progress bar while checking or installing, and a stronger type hierarchy.
- Keep password and text fields at a readable width instead of stretching across the window.
- Restyle dropdowns to match the dark theme, with gold highlight on the hovered option.

## 1.2.0 — 2026-09-22

- Check for stable launcher releases on startup and download a verified update in the background.
- Restart to replace the portable EXE at its current path, with a backup of the prior version.
- Publish versioned Windows builds from GitHub Actions when a matching version tag is pushed.

## 1.1.1 — 2026-09-22

- Make dropdown text and menu options readable with explicit high-contrast colors.

## 1.1.0 — 2026-09-22

- Show known 0/1 settings as switches, player display as a dropdown, and volume and scaling as sliders with exact value fields.
- Keep unfamiliar or unexpected setting values editable as text without discarding them.
- Handle an unreadable saved launcher state when starting.

## 1.0.0 — 2026-09-22

- Initial portable installer, updater, configurator, and launcher.
