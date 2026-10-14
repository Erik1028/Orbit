# Builds the shareable installer: release\OrbitOverlaySetup.exe (+ the same file as OrbitSetup.exe, the name the
# in-app updater of 1.6 and older looks for - upload both with a release).
# (self-contained single file - the .NET runtime is inside, nothing to install first).
# Running OrbitOverlaySetup.exe shows the installer; the installed copy is the same exe renamed OrbitOverlay.exe.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'release\build'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish (Join-Path $root 'Orbit.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
$setup = Join-Path $root 'release\OrbitOverlaySetup.exe'
Copy-Item -Force (Join-Path $out 'OrbitOverlay.exe') $setup
Copy-Item -Force $setup (Join-Path $root 'release\OrbitSetup.exe')
Remove-Item -Recurse -Force $out
$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Output "release\OrbitOverlaySetup.exe (+ OrbitSetup.exe)  ($size MB)"
