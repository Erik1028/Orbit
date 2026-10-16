# Makes the Windows 95 screenshots of the website's 1990s mode, from the app itself.
#
#   powershell -ExecutionPolicy Bypass -File docs\make-95-shots.ps1
#
# It runs OrbitOverlay --snapshot (no VR needed: made-up batteries, frame times and song, its own config, the real
# one is not touched), then copies the Windows 95 pictures into docs\img next to the modern ones:
#   keyboard-95.png        <- keyboard_95.png
#   settings-look-95.png   <- settings5_95.png (the Look tab)
#   watch-faces-95.png     <- wrist_full / analog / minimal / time _95.png, two by two
# The website shows NAME-95.png instead of NAME.png in its 1990s mode as soon as it is there.
# Pass -Exe to use another build, e.g. -Exe bin\Release\net8.0-windows\win-x64\OrbitOverlay.exe
param(
    [string]$Exe = (Join-Path $env:LOCALAPPDATA 'Programs\OrbitOverlay\OrbitOverlay.exe')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$img = Join-Path $root 'docs\img'
if (-not (Test-Path $Exe)) { throw "OrbitOverlay.exe not found at $Exe - pass -Exe <path>" }

$snap = Join-Path $env:TEMP ('orbit-snap-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
Write-Output "snapshot -> $snap"
$p = Start-Process -FilePath $Exe -ArgumentList @('--snapshot', "`"$snap`"") -Wait -PassThru
if ($p.ExitCode -ne 0) { Write-Warning "snapshot exited with $($p.ExitCode); see orbit-tool.log in $env:LOCALAPPDATA\Orbit" }

function Need($name) {
    $f = Join-Path $snap $name
    if (-not (Test-Path $f)) { throw "$name was not made - see orbit-tool.log in $env:LOCALAPPDATA\Orbit" }
    return $f
}
Copy-Item (Need 'keyboard_95.png') (Join-Path $img 'keyboard-95.png') -Force
Copy-Item (Need 'settings5_95.png') (Join-Path $img 'settings-look-95.png') -Force

# the four built-in faces, two by two on a transparent background, like watch-faces.png
Add-Type -AssemblyName System.Drawing
$faces = 'full', 'analog', 'minimal', 'time' | ForEach-Object { [System.Drawing.Image]::FromFile((Need "wrist_$($_)_95.png")) }
$gap = 24
$cw = ($faces | Measure-Object -Property Width -Maximum).Maximum
$ch = ($faces | Measure-Object -Property Height -Maximum).Maximum
$sheet = New-Object System.Drawing.Bitmap ([int](2 * $cw + $gap)), ([int](2 * $ch + $gap)), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
for ($i = 0; $i -lt 4; $i++) {
    $f = $faces[$i]
    $x = ($i % 2) * ($cw + $gap) + [int](($cw - $f.Width) / 2)
    $y = [math]::Floor($i / 2) * ($ch + $gap) + [int](($ch - $f.Height) / 2)
    $g.DrawImage($f, [int]$x, [int]$y, $f.Width, $f.Height)
}
$g.Dispose()
$sheet.Save((Join-Path $img 'watch-faces-95.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
$faces | ForEach-Object { $_.Dispose() }

Remove-Item -Recurse -Force $snap
Write-Output 'done: docs\img\keyboard-95.png, settings-look-95.png, watch-faces-95.png - commit and push them'
