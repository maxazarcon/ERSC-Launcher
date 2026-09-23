# Seamless Co-Op Launcher

A Windows launcher for the standard Steam installation of Elden Ring. It locates the game in Steam libraries, installs the newest published [Seamless Co-Op release](https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases), lets you edit every setting in `ersc_settings.ini`, and starts the mod's own launcher.

## Use

1. Download `ERSCLauncher-1.5.0-setup-win-x64.exe` from [GitHub Releases](https://github.com/maxazarcon/ERSC-Launcher/releases) and run it on Windows 10 or 11. It installs for your Windows account only (no administrator prompt) into `%LOCALAPPDATA%\Programs\ERSC Launcher`, adds a Start Menu entry, and can add the launcher to Steam. Uninstall it from **Settings > Apps**. If you prefer no installation, download the portable `ERSCLauncher-1.5.0-win-x64.exe` instead.
2. Confirm the detected `ELDEN RING\Game` folder, or choose one containing `eldenring.exe`.
3. Install the mod when prompted. Existing installations show an update button when a newer release is available; updates require confirmation.
4. Enter a co-op password, save settings, then select **Launch Seamless Co-Op**. Steam must be running and online. Everyone in the session needs the same game version, mod version, and password.

The launcher works offline with an installed mod, although it cannot confirm whether a newer release exists. Backups are kept under `%LOCALAPPDATA%\ERSC Launcher\backups`. The app remembers the selected game folder and version records under `%LOCALAPPDATA%\ERSC Launcher\state.json`.

Known settings appear as switches, dropdowns, or sliders. Scaling sliders also have an exact percentage field. Settings added by a future mod release remain editable as text, and the launcher keeps comments and unfamiliar entries when saving.

The launcher checks for its own stable releases at startup. A new version downloads in the background; select **Restart to update** to replace the EXE in place, whether installed or portable. The previous version is saved under `%LOCALAPPDATA%\ERSC Launcher\launcher-updates\<version>\previous.exe`. If the EXE is in a protected or read-only folder, move it to a folder you can write to before updating. Launcher updates and Seamless Co-Op mod updates are independent. The current release supports the normal Steam game folder layout and does not manage Mod Engine 2 setups.

## Steam and Big Picture mode

Tick **Add to Steam** in the installer, or select **Add to Steam** in the launcher, to add it to your Steam library as a non-Steam game called "Seamless Co-Op Launcher". Steam must close for a moment to do this; the launcher asks first, then reopens it. Launch options or other properties you later change in Steam are kept if you add it again. Uninstalling removes the shortcut. If the Steam shortcut is added from the portable EXE, it points at wherever that EXE is stored.

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

The output is `dist\ERSCLauncher.exe`; the release copy is named `ERSCLauncher-1.5.0-win-x64.exe`. It contains the .NET runtime and needs no separate runtime installation. The launcher downloads the mod directly from the release asset; the mod files are not bundled into the EXE.

To build both the portable EXE and the installer (uses [Inno Setup 6](https://jrsoftware.org/isinfo.php), installed through Chocolatey if missing):

```powershell
./installer/build.ps1 -Version 1.5.0
```

## Publish a launcher release

Update the project version and changelog, then push a matching `vX.Y.Z` tag from the repository's default branch. The Windows GitHub Actions workflow runs the tests, publishes the portable EXE and the installer, and creates the GitHub release. Pull requests run the same tests and upload both builds as workflow artifacts. A tag that does not match the project version fails without publishing.
