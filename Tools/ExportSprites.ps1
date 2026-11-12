<#
.SYNOPSIS
  Export Unity Sprite .asset files (YAML, as produced by AssetRipper) to standalone PNGs.

.DESCRIPTION
  Each Sprite .asset only stores a rectangle inside a source texture (usually an atlas).
  This script resolves the texture GUID to its PNG via the .meta files, crops m_RD.textureRect
  (Unity's origin is bottom-left), applies packing rotation/flip if present, places the crop
  inside a canvas of m_Rect size at textureRectOffset (restores trimmed transparent margins),
  and saves it as <SpriteName>.png.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\ExportSprites.ps1
  powershell -ExecutionPolicy Bypass -File Tools\ExportSprites.ps1 -SpriteDir Assets\Sprite -OutDir SpriteExport
#>
param(
    [string]$ProjectDir = (Split-Path $PSScriptRoot -Parent),
    [string]$SpriteDir  = "Assets\Sprite",
    [string]$OutDir     = "SpriteExport",
    [switch]$Overwrite
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Resolve-InProject([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return $path }
    return Join-Path $ProjectDir $path
}

$spritePath = Resolve-InProject $SpriteDir
$outPath    = Resolve-InProject $OutDir
$assetsPath = Join-Path $ProjectDir "Assets"
New-Item -ItemType Directory -Force $outPath | Out-Null

# --- GUID -> image file map (all image .meta files under Assets) ---------------
Write-Host "Indexing texture GUIDs..."
$guidMap = @{}
Get-ChildItem $assetsPath -Recurse -File -Include *.png.meta, *.jpg.meta, *.jpeg.meta, *.tga.meta, *.psd.meta |
    ForEach-Object {
        $m = Select-String -Path $_.FullName -Pattern '^guid:\s*([0-9a-f]{32})' | Select-Object -First 1
        if ($m) { $guidMap[$m.Matches[0].Groups[1].Value] = $_.FullName.Substring(0, $_.FullName.Length - 5) }
    }

# --- YAML helpers ------------------------------------------------------------
function Get-Num($block, $key) {
    $m = [regex]::Match($block, "(?m)^\s*${key}:\s*(-?[\d.eE+-]+)")
    if ($m.Success) { return [double]$m.Groups[1].Value } else { return 0 }
}
function Get-Rect($text, $key) {
    # Reads a multi-line rect:  key:\n serializedVersion: 2\n x: ..\n y: ..\n width: ..\n height: ..
    $m = [regex]::Match($text, "(?ms)^(\s*)${key}:\s*\r?\n((?:\1\s+.*\r?\n){1,6})")
    if (-not $m.Success) { return $null }
    $b = $m.Groups[2].Value
    return @{ x = Get-Num $b 'x'; y = Get-Num $b 'y'; w = Get-Num $b 'width'; h = Get-Num $b 'height' }
}
function Get-Vec2($text, $key) {
    $m = [regex]::Match($text, "${key}:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}")
    if ($m.Success) { return @{ x = [double]$m.Groups[1].Value; y = [double]$m.Groups[2].Value } }
    return @{ x = 0; y = 0 }
}

$texCache = @{}
$ok = 0; $skipped = 0; $failed = @()
$files = Get-ChildItem $spritePath -Filter *.asset -File

foreach ($f in $files) {
    try {
        $text = [IO.File]::ReadAllText($f.FullName)
        if ($text -notmatch '(?m)^Sprite:') { $skipped++; continue }

        # File names are unique (AssetRipper suffixes duplicates), sprite m_Name may not be
        $dest = Join-Path $outPath "$($f.BaseName).png"
        if ((Test-Path $dest) -and -not $Overwrite) { $skipped++; continue }

        # m_RD section only (m_AtlasRD comes after and is usually empty)
        $rdStart = $text.IndexOf('  m_RD:')
        $rdEnd   = $text.IndexOf('  m_AtlasRD:')
        if ($rdStart -lt 0) { throw "no m_RD" }
        $rd = if ($rdEnd -gt $rdStart) { $text.Substring($rdStart, $rdEnd - $rdStart) } else { $text.Substring($rdStart) }

        $guid = [regex]::Match($rd, 'texture:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})').Groups[1].Value
        if (-not $guid) { throw "no texture reference" }
        $texFile = $guidMap[$guid]
        if (-not $texFile) { throw "texture guid $guid not found" }

        $fullRect = Get-Rect $text 'm_Rect'
        $texRect  = Get-Rect $rd 'textureRect'
        if (-not $texRect -or $texRect.w -le 0) { $texRect = $fullRect }
        $offset   = Get-Vec2 $rd 'textureRectOffset'
        $settings = [int](Get-Num $rd 'settingsRaw')
        $packed   = $settings -band 1
        $rotation = ($settings -shr 2) -band 0xF

        if (-not $texCache.ContainsKey($texFile)) {
            # Load via a copy so the file isn't locked
            $bytes = [IO.File]::ReadAllBytes($texFile)
            $ms = New-Object IO.MemoryStream(, $bytes)
            $texCache[$texFile] = [Drawing.Bitmap]::new($ms)
        }
        $tex = $texCache[$texFile]

        $cx = [int][Math]::Round($texRect.x)
        $cw = [int][Math]::Round($texRect.w)
        $ch = [int][Math]::Round($texRect.h)
        $cy = $tex.Height - [int][Math]::Round($texRect.y) - $ch   # bottom-left -> top-left
        $cx = [Math]::Max(0, $cx); $cy = [Math]::Max(0, $cy)
        $cw = [Math]::Min($cw, $tex.Width - $cx); $ch = [Math]::Min($ch, $tex.Height - $cy)
        if ($cw -le 0 -or $ch -le 0) { throw "empty rect" }

        $crop = $tex.Clone([Drawing.Rectangle]::new($cx, $cy, $cw, $ch), [Drawing.Imaging.PixelFormat]::Format32bppArgb)

        if ($packed -and $rotation -ne 0) {
            switch ($rotation) {
                1 { $crop.RotateFlip([Drawing.RotateFlipType]::RotateNoneFlipX) }
                2 { $crop.RotateFlip([Drawing.RotateFlipType]::RotateNoneFlipY) }
                3 { $crop.RotateFlip([Drawing.RotateFlipType]::Rotate180FlipNone) }
                4 { $crop.RotateFlip([Drawing.RotateFlipType]::Rotate270FlipNone) }
            }
        }

        $outW = [int][Math]::Round($fullRect.w); $outH = [int][Math]::Round($fullRect.h)
        if ($outW -gt $crop.Width -or $outH -gt $crop.Height) {
            # Trimmed sprite: restore original size, offset is from bottom-left
            $canvas = New-Object Drawing.Bitmap($outW, $outH, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [Drawing.Graphics]::FromImage($canvas)
            $g.Clear([Drawing.Color]::Transparent)
            $px = [int][Math]::Round($offset.x)
            $py = $outH - [int][Math]::Round($offset.y) - $crop.Height
            $g.DrawImageUnscaled($crop, $px, $py)
            $g.Dispose(); $crop.Dispose()
            $crop = $canvas
        }

        $crop.Save($dest, [Drawing.Imaging.ImageFormat]::Png)
        $crop.Dispose()
        $ok++
    } catch {
        $failed += "$($f.Name): $($_.Exception.Message)"
    }
}

foreach ($b in $texCache.Values) { $b.Dispose() }

Write-Host ""
Write-Host "Exported : $ok -> $outPath"
Write-Host "Skipped  : $skipped"
Write-Host "Failed   : $($failed.Count)"
$failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
