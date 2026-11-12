<#
.SYNOPSIS
  Cut every region of Spine atlas files (*.atlas.txt / *.atlas) into standalone PNGs.

.DESCRIPTION
  A Spine atlas lists pages (PNG files) and the regions packed inside them. For each region
  this script crops it from its page (looked up anywhere under Assets/), un-rotates it,
  restores the trimmed whitespace (orig/offset) and writes <OutDir>/<atlas>/<region>.png.
  Supports both the Spine 3.x (xy/size/orig/offset) and 4.x (bounds/offsets) atlas formats.

  Note: the .skel.bytes files are skeleton/animation data, not images; they are not converted.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\ExportSpineAtlas.ps1
  powershell -ExecutionPolicy Bypass -File Tools\ExportSpineAtlas.ps1 -AtlasDir Assets\TextAsset -OutDir SpineExport -Overwrite
#>
param(
    [string]$ProjectDir = (Split-Path $PSScriptRoot -Parent),
    [string]$AtlasDir   = "Assets\TextAsset",
    [string]$OutDir     = "SpineExport",
    [switch]$Overwrite
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Resolve-InProject([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return $path }
    return Join-Path $ProjectDir $path
}
$atlasPath  = Resolve-InProject $AtlasDir
$outRoot    = Resolve-InProject $OutDir
$assetsPath = Join-Path $ProjectDir "Assets"

function Find-Page([string]$name, [string]$nearDir) {
    $local = Join-Path $nearDir $name
    if (Test-Path -LiteralPath $local) { return $local }
    $hit = Get-ChildItem $assetsPath -Recurse -File -ErrorAction SilentlyContinue |
           Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}
function Ints([string]$v) { return @($v.Split(',') | ForEach-Object { [int]$_.Trim() }) }

# Parse an atlas into pages, each with a list of regions
function Read-Atlas([string]$file) {
    $pages = @(); $page = $null; $region = $null; $prevBlank = $true
    foreach ($raw in [IO.File]::ReadAllLines($file)) {
        $line = $raw.TrimEnd()
        if ($line.Trim() -eq '') { $prevBlank = $true; $region = $null; continue }
        $t = $line.Trim()
        $kv = [regex]::Match($t, '^([A-Za-z]+)\s*:\s*(.*)$')
        if (-not $kv.Success) {
            if ($prevBlank -or -not $page) {
                $page = @{ name = $t; regions = New-Object System.Collections.ArrayList }
                $pages += $page; $region = $null
            } else {
                $region = @{ name = $t; rotate = 0; index = -1; offX = 0; offY = 0 }
                [void]$page.regions.Add($region)
            }
        } else {
            $k = $kv.Groups[1].Value; $v = $kv.Groups[2].Value
            if (-not $region -and $page -and $k -eq 'size') {
                $a = Ints $v; $page.w = $a[0]; $page.h = $a[1]
            }
            if ($region) {
                switch ($k) {
                    'rotate'  { $region.rotate = if ($v -eq 'true') { 90 } elseif ($v -eq 'false') { 0 } else { [int]$v } }
                    'xy'      { $a = Ints $v; $region.x = $a[0]; $region.y = $a[1] }
                    'size'    { $a = Ints $v; $region.w = $a[0]; $region.h = $a[1] }
                    'orig'    { $a = Ints $v; $region.origW = $a[0]; $region.origH = $a[1] }
                    'offset'  { $a = Ints $v; $region.offX = $a[0]; $region.offY = $a[1] }
                    'bounds'  { $a = Ints $v; $region.x = $a[0]; $region.y = $a[1]; $region.w = $a[2]; $region.h = $a[3] }
                    'offsets' { $a = Ints $v; $region.offX = $a[0]; $region.offY = $a[1]; $region.origW = $a[2]; $region.origH = $a[3] }
                    'index'   { $region.index = [int]$v }
                }
            }
        }
        $prevBlank = $false
    }
    return $pages
}

$ok = 0; $skipped = 0; $failed = @()
$atlases = Get-ChildItem $atlasPath -File | Where-Object { $_.Name -match '\.atlas(\.txt)?$' }

foreach ($atlas in $atlases) {
    $atlasName = $atlas.Name -replace '\.atlas(\.txt)?$', ''
    $outDir = Join-Path $outRoot $atlasName
    New-Item -ItemType Directory -Force $outDir | Out-Null

    foreach ($page in (Read-Atlas $atlas.FullName)) {
        $pageFile = Find-Page $page.name $atlas.DirectoryName
        if (-not $pageFile) { $failed += "$($atlas.Name): page '$($page.name)' not found"; continue }
        $bytes = [IO.File]::ReadAllBytes($pageFile)
        $tex = [Drawing.Bitmap]::new((New-Object IO.MemoryStream(, $bytes)))
        if ($page.w -and ($tex.Width -ne $page.w -or $tex.Height -ne $page.h)) {
            # Unity may have resized the page on import (e.g. NPOT -> POT); scale back to atlas coordinates
            Write-Host "  $($page.name): resizing $($tex.Width)x$($tex.Height) -> $($page.w)x$($page.h)" -ForegroundColor DarkYellow
            $fixed = New-Object Drawing.Bitmap($page.w, $page.h, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [Drawing.Graphics]::FromImage($fixed)
            $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.DrawImage($tex, 0, 0, $page.w, $page.h)
            $g.Dispose(); $tex.Dispose(); $tex = $fixed
        }

        foreach ($r in $page.regions) {
            try {
                $file = $r.name
                if ($r.index -ge 0) { $file = "${file}_$($r.index)" }
                $file = ($file -split '/' | ForEach-Object { $_ -replace '[\\:*?"<>|]', '_' }) -join '\'
                $dest = Join-Path $outDir "$file.png"
                if ((Test-Path -LiteralPath $dest) -and -not $Overwrite) { $skipped++; continue }
                New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null

                # w/h are the unrotated size; a 90/270 region occupies h x w in the page
                $sideways = ($r.rotate -eq 90 -or $r.rotate -eq 270)
                $pw = if ($sideways) { $r.h } else { $r.w }
                $ph = if ($sideways) { $r.w } else { $r.h }
                $crop = $tex.Clone([Drawing.Rectangle]::new($r.x, $r.y, $pw, $ph), [Drawing.Imaging.PixelFormat]::Format32bppArgb)

                switch ($r.rotate) {
                    90  { $crop.RotateFlip([Drawing.RotateFlipType]::Rotate90FlipNone) }
                    180 { $crop.RotateFlip([Drawing.RotateFlipType]::Rotate180FlipNone) }
                    270 { $crop.RotateFlip([Drawing.RotateFlipType]::Rotate270FlipNone) }
                }

                $ow = if ($r.origW) { $r.origW } else { $r.w }
                $oh = if ($r.origH) { $r.origH } else { $r.h }
                if ($ow -ne $crop.Width -or $oh -ne $crop.Height) {
                    # Restore whitespace stripped by the packer; offset is from bottom-left
                    $canvas = New-Object Drawing.Bitmap($ow, $oh, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    $g = [Drawing.Graphics]::FromImage($canvas)
                    $g.Clear([Drawing.Color]::Transparent)
                    $g.DrawImageUnscaled($crop, $r.offX, $oh - $r.offY - $crop.Height)
                    $g.Dispose(); $crop.Dispose(); $crop = $canvas
                }
                $crop.Save($dest, [Drawing.Imaging.ImageFormat]::Png)
                $crop.Dispose()
                $ok++
            } catch {
                $failed += "$($atlas.Name) / $($r.name): $($_.Exception.Message)"
            }
        }
        $tex.Dispose()
    }
}

Write-Host "Exported : $ok -> $outRoot"
Write-Host "Skipped  : $skipped"
Write-Host "Failed   : $($failed.Count)"
$failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
