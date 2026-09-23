# Seamless Co-Op Launcher (Unofficial)

A Windows launcher for the standard Steam installation of Elden Ring. It locates the game in Steam libraries, installs the [Seamless Co-Op](https://www.nexusmods.com/eldenring/mods/510) ZIP you download, lets you edit every setting in `ersc_settings.ini`, and starts the mod's own launcher.

This launcher is unofficial. It is not made by or affiliated with the Seamless Co-Op author, and it never includes, hosts or redistributes the mod. You download the mod yourself from its [Nexus Mods page](https://www.nexusmods.com/eldenring/mods/510) or its [official GitHub releases](https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases). If you enjoy the mod, endorse it on Nexus.

## Use

1. Download `ERSCLauncher-1.7.0-setup-win-x64.exe` from [GitHub Releases](https://github.com/maxazarcon/ERSC-Launcher/releases) and run it on Windows 10 or 11. It installs for your Windows account only (no administrator prompt) into `%LOCALAPPDATA%\Programs\ERSC Launcher`, adds a Start Menu entry, and can add the launcher to Steam. Uninstall it from **Settings > Apps**. If you prefer no installation, download the portable `ERSCLauncher-1.7.0-win-x64.exe` instead.
2. Confirm the detected `ELDEN RING\Game` folder, or choose one containing `eldenring.exe`.
3. Select **Get latest release** to open the mod's GitHub release page (or **Nexus page** for Nexus Mods) and download the ZIP. Then select **Install from ZIP…** and choose it. If the ZIP from the latest release is already in your Downloads folder, the launcher finds it and the button becomes **Install** followed by its file name, which also works with a controller. The launcher checks the ZIP against the SHA-256 digest GitHub publishes for each release. A ZIP that matches no recent release can still be installed after a warning. Updates work the same way and require confirmation.
4. Enter a co-op password, save settings, then select **Launch Seamless Co-Op**. Steam must be running and online. Everyone in the session needs the same game version, mod version, and password.

The launcher works offline with an installed mod, although it cannot confirm whether a newer release exists or verify a ZIP. It reads the list of published releases from GitHub to tell you when an update is out; it never downloads the mod itself. Backups are kept under `%LOCALAPPDATA%\ERSC Launcher\backups`. The app remembers the selected game folder and version records under `%LOCALAPPDATA%\ERSC Launcher\state.json`.

Known settings appear as switches, dropdowns, or sliders. Scaling sliders also have an exact percentage field. Settings added by a future mod release remain editable as text, and the launcher keeps comments and unfamiliar entries when saving.

The launcher checks for its own stable releases at startup. A new version downloads in the background; select **Restart to update** to replace the EXE in place, whether installed or portable. The previous version is saved under `%LOCALAPPDATA%\ERSC Launcher\launcher-updates\<version>\previous.exe`. If the EXE is in a protected or read-only folder, move it to a folder you can write to before updating. Launcher updates and Seamless Co-Op mod updates are independent. The current release supports the normal Steam game folder layout and does not manage Mod Engine 2 setups.

## Steam and Big Picture mode

Tick **Add to Steam** in the installer, or select **Add to Steam** in the launcher, to add it to your Steam library as a non-Steam game called "Seamless Co-Op Launcher (Unofficial)". Steam must close for a moment to do this; the launcher asks first, then reopens it. Launch options or other properties you later change in Steam are kept if you add it again. Uninstalling removes the shortcut. If the Steam shortcut is added from the portable EXE, it points at wherever that EXE is stored. The shortcut comes with its own cover, banner and logo in the Steam library. Art you choose yourself in Steam is never replaced, and removing the shortcut leaves it in place. Shortcuts added by 1.6.0 or earlier were named "Seamless Co-Op Launcher"; select **Update Steam shortcut** in the launcher, or reinstall with **Add to Steam** ticked, to rename it. Your launch options and custom art carry over.

When started from Big Picture mode or on a Steam Deck, the launcher opens full screen with larger text. Add `--gamepad` to the shortcut's launch options in Steam to force this layout. In this mode, the launcher closes after starting the game so Steam switches to Elden Ring's own controller layout.

### Controller

Any controller Steam supports works in Big Picture mode. Outside Steam, Xbox-compatible (XInput) controllers work directly.

| Button | Action |
| --- | --- |
| D-pad or left stick up/down | Move between settings and buttons |
| D-pad or left stick left/right | Change a slider or dropdown; otherwise move |
| LB / RB | Change a slider in larger steps (RB elsewhere jumps to Launch) |
| RT | Jump to the Launch button |
| A | Press a button, flip a switch, open a dropdown, or edit a text field with the on-screen keyboard |
| B | Close a dropdown, dialog or the keyboard without changes; on the main screen, quit |
| X | Save settings |
| Y | Check for mod updates |
| Menu (☰) | Launch Seamless Co-Op |
| View (⧉) | Quit |
| Right stick | Scroll |

On the on-screen keyboard, X deletes, Y switches to capitals, and Menu confirms. **Choose folder** opens the standard Windows folder picker, which needs a mouse or keyboard; the game folder is normally found automatically.

## Unsigned builds

The installer and EXE are currently unsigned. Windows SmartScreen may show an unfamiliar-app warning on first download.

## Build

Install the .NET 10 SDK with Windows desktop support, then run:

```powershell
dotnet run --project tests/ERSC.Launcher.Tests/ERSC.Launcher.Tests.csproj
dotnet publish src/ERSC.Launcher/ERSC.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist
```

The output is `dist\ERSCLauncher.exe`; the release copy is named `ERSCLauncher-1.7.0-win-x64.exe`. It contains the .NET runtime and needs no separate runtime installation. The mod files are not bundled into the EXE, and the launcher does not download them.

To build both the portable EXE and the installer (uses [Inno Setup 6](https://jrsoftware.org/isinfo.php), installed through Chocolatey if missing):

```powershell
./installer/build.ps1 -Version 1.7.0
```

The Steam library artwork in `src/ERSC.Launcher.Core/SteamArt` is drawn by `tools/steam-art/art.html`. To change it, edit that page and run `npm install playwright` then `node render.mjs` in `tools/steam-art`, which writes the PNGs back into the project.

## Publish a launcher release

Update the project version and changelog, then push a matching `vX.Y.Z` tag from the repository's default branch. The Windows GitHub Actions workflow runs the tests, publishes the portable EXE and the installer, and creates the GitHub release. Pull requests run the same tests and upload both builds as workflow artifacts. A tag that does not match the project version fails without publishing.
