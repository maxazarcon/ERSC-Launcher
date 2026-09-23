# Changelog

## Unreleased

- Move launcher update status into a slim footer so the game folder and installation come first.
- Group settings into one card per section with dividers instead of a card per setting.
- Add hover and pressed states to buttons, a progress bar while checking or installing, and a stronger type hierarchy.
- Keep password and text fields at a readable width instead of stretching across the window.

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
