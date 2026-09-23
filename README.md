# Seamless Co-Op Launcher

A portable Windows launcher for the standard Steam installation of Elden Ring. It locates the game in Steam libraries, installs the newest published [Seamless Co-Op release](https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases), lets you edit every setting in `ersc_settings.ini`, and starts the mod's own launcher.

## Use

1. Download `ERSCLauncher-1.3.0-win-x64.exe` from [GitHub Releases](https://github.com/maxazarcon/ERSC-Launcher/releases) and run it on Windows 10 or 11.
2. Confirm the detected `ELDEN RING\Game` folder, or choose one containing `eldenring.exe`.
3. Install the mod when prompted. Existing installations show an update button when a newer release is available; updates require confirmation.
4. Enter a co-op password, save settings, then select **Launch Seamless Co-Op**. Steam must be running and online. Everyone in the session needs the same game version, mod version, and password.

The launcher works offline with an installed mod, although it cannot confirm whether a newer release exists. Backups are kept under `%LOCALAPPDATA%\ERSC Launcher\backups`. The app remembers the selected game folder and version records under `%LOCALAPPDATA%\ERSC Launcher\state.json`.

Known settings appear as switches, dropdowns, or sliders. Scaling sliders also have an exact percentage field. Settings added by a future mod release remain editable as text, and the launcher keeps comments and unfamiliar entries when saving.

The launcher checks for its own stable releases at startup. A new version downloads in the background; select **Restart to update** to replace the EXE wherever you keep it. The previous version is saved under `%LOCALAPPDATA%\ERSC Launcher\launcher-updates\<version>\previous.exe`. If the EXE is in a protected or read-only folder, move it to a folder you can write to before updating. Launcher updates and Seamless Co-Op mod updates are independent. The current release supports the normal Steam game folder layout and does not manage Mod Engine 2 setups.

The portable EXE is currently unsigned. Windows SmartScreen may show an unfamiliar-app warning on first download.

## Build

Install the .NET 10 SDK with Windows desktop support, then run:

```powershell
dotnet run --project tests/ERSC.Launcher.Tests/ERSC.Launcher.Tests.csproj
dotnet publish src/ERSC.Launcher/ERSC.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist
```

The output is `dist\ERSCLauncher.exe`; the release copy is named `ERSCLauncher-1.3.0-win-x64.exe`. It contains the .NET runtime and needs no separate runtime installation. The launcher downloads the mod directly from the release asset; the mod files are not bundled into the EXE.

## Publish a launcher release

Update the project version and changelog, then push a matching `vX.Y.Z` tag from the repository's default branch. The Windows GitHub Actions workflow runs the tests, publishes the portable EXE, and creates the GitHub release. A tag that does not match the project version fails without publishing.
