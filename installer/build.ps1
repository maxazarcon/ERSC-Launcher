# Publishes the self-contained EXE and wraps it in the Inno Setup installer.
# Output: dist/ERSCLauncher-<version>-win-x64.exe and dist/ERSCLauncher-<version>-setup-win-x64.exe
param([Parameter(Mandatory)] [string] $Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'

dotnet restore "$root/src/ERSC.Launcher/ERSC.Launcher.csproj" -r win-x64 --configfile "$root/NuGet.Config"
dotnet publish "$root/src/ERSC.Launcher/ERSC.Launcher.csproj" --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $dist
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) {
    choco install innosetup -y --no-progress
    if ($LASTEXITCODE -ne 0) { throw "Could not install Inno Setup" }
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}
& $iscc "/DAppVersion=$Version" "/DSourceExe=$dist\ERSCLauncher.exe" "/O$dist" "$root\installer\ERSCLauncher.iss"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

Move-Item "$dist/ERSCLauncher.exe" "$dist/ERSCLauncher-$Version-win-x64.exe" -Force
