# Seamless Co-Op Launcher

A portable Windows launcher for the standard Steam installation of Elden Ring. It locates the game in Steam libraries, installs the newest published [Seamless Co-Op release](https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases), lets you edit every setting in `ersc_settings.ini`, and starts the mod's own launcher.

## Use

1. Run `ERSCLauncher-1.1.0.exe` on Windows 10 or 11.
2. Confirm the detected `ELDEN RING\Game` folder, or choose one containing `eldenring.exe`.
3. Install the mod when prompted. Existing installations show an update button when a newer release is available; updates require confirmation.
4. Enter a co-op password, save settings, then select **Launch Seamless Co-Op**. Steam must be running and online. Everyone in the session needs the same game version, mod version, and password.

The launcher works offline with an installed mod, although it cannot confirm whether a newer release exists. Backups are kept under `%LOCALAPPDATA%\ERSC Launcher\backups`. The app remembers the selected game folder and version records under `%LOCALAPPDATA%\ERSC Launcher\state.json`.

Known settings appear as switches, dropdowns, or sliders. Scaling sliders also have an exact percentage field. Settings added by a future mod release remain editable as text, and the launcher keeps comments and unfamiliar entries when saving.

The current release supports the normal Steam game folder layout. It does not manage Mod Engine 2 setups or update the launcher itself.

## Build

Install the .NET 10 SDK with Windows desktop support, then run:

```powershell
dotnet run --project tests/ERSC.Launcher.Tests/ERSC.Launcher.Tests.csproj
dotnet publish src/ERSC.Launcher/ERSC.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist
```

The output is `dist\ERSCLauncher.exe`; the release copy is named `dist\ERSCLauncher-1.1.0.exe`. It contains the .NET runtime and needs no separate runtime installation. The launcher downloads the mod directly from the release asset; the mod files are not bundled into the EXE.
