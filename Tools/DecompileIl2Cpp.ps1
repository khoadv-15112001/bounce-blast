<#
.SYNOPSIS
  Convert an IL2CPP build (native binary + global-metadata.dat) into readable C# source.

.DESCRIPTION
  Pipeline:
    1. Validate global-metadata.dat (standard IL2CPP magic 0xFAB11BAF + version).
    2. Run Il2CppDumper  -> dump.cs, script.json, il2cpp.h, DummyDll\*.dll
    3. Run ilspycmd on every DummyDll -> one C# project folder per assembly.

  IL2CPP compiles C# to native code, so method BODIES cannot be restored as C#.
  The output contains namespaces, classes, fields, properties, method signatures,
  attributes and the native address (RVA/VA) of each method. Use script.json /
  il2cpp.h with IDA / Ghidra to read the method bodies as pseudo-code.

  Only standard (non-obfuscated) metadata is supported. If the metadata was processed
  by an obfuscator, use the original metadata from the build pipeline instead.

  Tools are downloaded on first run into Tools\.cache (Il2CppDumper from GitHub
  releases, ilspycmd via `dotnet tool install`). Requires the .NET SDK for ilspycmd.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\DecompileIl2Cpp.ps1 `
      -Binary   "C:\builds\BounceBusters.app\Frameworks\UnityFramework.framework\UnityFramework" `
      -Metadata "C:\builds\BounceBusters.app\Data\Managed\Metadata\global-metadata.dat"

.EXAMPLE
  # Only game assemblies, skip Unity/system DLLs
  powershell -ExecutionPolicy Bypass -File Tools\DecompileIl2Cpp.ps1 -Binary ... -Metadata ... -Include "Assembly-CSharp*"
#>
param(
    [Parameter(Mandatory)] [string]$Binary,
    [Parameter(Mandatory)] [string]$Metadata,
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "DecompiledCode"),
    [string[]]$Include = @("*"),            # wildcard filter on DummyDll names
    [string[]]$Exclude = @("mscorlib*", "System*", "Mono.*", "Unity.*", "UnityEngine*", "netstandard*", "Microsoft.*"),
    [string]$Il2CppDumperExe,               # use an existing Il2CppDumper.exe instead of downloading
    [switch]$SkipDecompile,                 # stop after Il2CppDumper
    [switch]$Overwrite
)

$ErrorActionPreference = 'Stop'
$CacheDir = Join-Path $PSScriptRoot ".cache"

function Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. validate input
function Test-Metadata([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $hdr = New-Object byte[] 8
        if ($fs.Read($hdr, 0, 8) -ne 8) { throw "Metadata file too small: $path" }
    } finally { $fs.Dispose() }
    $magic   = [BitConverter]::ToUInt32($hdr, 0)
    $version = [BitConverter]::ToInt32($hdr, 4)
    if ($magic -ne 0xFAB11BAF) {
        throw ("'{0}' is not standard IL2CPP metadata (magic 0x{1:X8}, expected 0xFAB11BAF). " +
               "It is likely obfuscated/encrypted - use the original global-metadata.dat from the build pipeline.") -f $path, $magic
    }
    if ($version -lt 16 -or $version -gt 35) { throw "Unexpected metadata version $version" }
    return $version
}

function Test-Binary([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try { $hdr = New-Object byte[] 4; [void]$fs.Read($hdr, 0, 4) } finally { $fs.Dispose() }
    $m = [BitConverter]::ToUInt32($hdr, 0)
    switch ($m) {
        0xFEEDFACF { return "Mach-O 64" }
        0xFEEDFACE { return "Mach-O 32" }
        0xBEBAFECA { return "Mach-O FAT" }
        0x464C457F { return "ELF" }
        default {
            if (($m -band 0xFFFF) -eq 0x5A4D) { return "PE" }
            throw ("Unknown binary format (magic 0x{0:X8}): {1}" -f $m, $path)
        }
    }
}

foreach ($p in @($Binary, $Metadata)) { if (-not (Test-Path $p -PathType Leaf)) { throw "File not found: $p" } }
$Binary   = (Resolve-Path $Binary).Path
$Metadata = (Resolve-Path $Metadata).Path

Step "Checking input"
$ver = Test-Metadata $Metadata
$fmt = Test-Binary $Binary
Write-Host "    metadata v$ver, binary $fmt"

if (Test-Path $OutDir) {
    if (-not $Overwrite) { throw "Output folder exists: $OutDir (use -Overwrite)" }
    Remove-Item $OutDir -Recurse -Force
}
$DumpDir = Join-Path $OutDir "il2cppdumper"
$SrcDir  = Join-Path $OutDir "src"
New-Item -ItemType Directory -Force $DumpDir, $SrcDir | Out-Null

# ---------------------------------------------------------------- 2. Il2CppDumper
function Get-Il2CppDumper {
    if ($Il2CppDumperExe) {
        if (-not (Test-Path $Il2CppDumperExe)) { throw "Il2CppDumper not found: $Il2CppDumperExe" }
        return (Resolve-Path $Il2CppDumperExe).Path
    }
    $dir = Join-Path $CacheDir "Il2CppDumper"
    $exe = Get-ChildItem $dir -Recurse -Filter "Il2CppDumper.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exe) { return $exe.FullName }

    Step "Downloading Il2CppDumper (GitHub: Perfare/Il2CppDumper, latest release)"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $rel = Invoke-RestMethod "https://api.github.com/repos/Perfare/Il2CppDumper/releases/latest" -Headers @{ "User-Agent" = "DecompileIl2Cpp" }
    $asset = $rel.assets | Where-Object { $_.name -match 'net6.*win|win.*net6' } | Select-Object -First 1
    if (-not $asset) { $asset = $rel.assets | Where-Object { $_.name -match 'win' -and $_.name -like '*.zip' } | Select-Object -First 1 }
    if (-not $asset) { throw "No Windows asset found in release $($rel.tag_name)" }
    Write-Host "    $($asset.name) ($([math]::Round($asset.size/1MB,1)) MB)"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $zip = Join-Path $dir $asset.name
    Invoke-WebRequest $asset.browser_download_url -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $dir -Force
    Remove-Item $zip
    $exe = Get-ChildItem $dir -Recurse -Filter "Il2CppDumper.exe" | Select-Object -First 1
    if (-not $exe) { throw "Il2CppDumper.exe not found after extracting $($asset.name)" }
    return $exe.FullName
}

$dumper = Get-Il2CppDumper
# Make it non-interactive: no "press any key", generate dummy DLLs.
$cfgPath = Join-Path (Split-Path $dumper) "config.json"
if (Test-Path $cfgPath) {
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    $cfg.RequireAnyKey = $false
    $cfg.GenerateDummyDll = $true
    $cfg | ConvertTo-Json -Depth 5 | Set-Content $cfgPath -Encoding UTF8
}

Step "Running Il2CppDumper"
& $dumper $Binary $Metadata $DumpDir
if ($LASTEXITCODE -ne 0) { throw "Il2CppDumper failed (exit $LASTEXITCODE)" }
$dummyDir = Join-Path $DumpDir "DummyDll"
if (-not (Test-Path $dummyDir)) { throw "Il2CppDumper produced no DummyDll folder - check its output above" }

if ($SkipDecompile) { Step "Done (dump only): $DumpDir"; return }

# ---------------------------------------------------------------- 3. ilspycmd
function Get-IlSpyCmd {
    $toolDir = Join-Path $CacheDir "ilspycmd"
    $exe = Join-Path $toolDir "ilspycmd.exe"
    if (Test-Path $exe) { return $exe }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET SDK not found - install it from https://dotnet.microsoft.com/download" }
    $sdks = & dotnet --list-sdks
    if (-not $sdks) { throw ".NET runtime found but no SDK - install the .NET SDK (8.0+) to use ilspycmd" }
    Step "Installing ilspycmd (dotnet tool)"
    # 9.1.x runs on the .NET 8 SDK; newer ilspycmd builds require a newer SDK.
    & dotnet tool install ilspycmd --version 9.1.0.7988 --tool-path $toolDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool install ilspycmd failed" }
    return $exe
}

$ilspy = Get-IlSpyCmd
$dlls = Get-ChildItem $dummyDir -Filter *.dll | Where-Object {
    $n = $_.Name
    ($Include | Where-Object { $n -like $_ }) -and -not ($Exclude | Where-Object { $n -like $_ })
}
if (-not $dlls) { throw "No DLLs matched -Include/-Exclude in $dummyDir" }

Step "Decompiling $($dlls.Count) assemblies to C#"
$failed = @()
foreach ($dll in $dlls) {
    $name = [IO.Path]::GetFileNameWithoutExtension($dll.Name)
    $dst = Join-Path $SrcDir $name
    Write-Host "    $name"
    & $ilspy -p -o $dst -r $dummyDir $dll.FullName | Out-Null
    if ($LASTEXITCODE -ne 0) { $failed += $name }
}

$csCount = (Get-ChildItem $SrcDir -Recurse -Filter *.cs | Measure-Object).Count
Step "Done: $csCount .cs files in $SrcDir"
Write-Host "    Il2CppDumper output (dump.cs, script.json, il2cpp.h): $DumpDir"
if ($failed) { Write-Warning "Failed to decompile: $($failed -join ', ')" }
