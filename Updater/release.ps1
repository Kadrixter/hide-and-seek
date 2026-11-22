# Publishes a GitHub release that Updater.exe / the game picks up.
#   powershell -File Updater\release.ps1 -Notes "Co nowego..."          # game only (players keep their Tools)
#   powershell -File Updater\release.ps1 -Notes "..." -Full             # + HideAndSeek-tools.zip, "[full]" in the notes
# The first release automatically includes the tools (the updater needs one release that has them).
param(
    [string]$Build = "$PSScriptRoot\..\Builds\Windows",
    [string]$Notes = "",
    [switch]$Full,
    [string]$Repo = "Kadrixter/hide-and-seek"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$Build = (Resolve-Path $Build).Path
$version = (Get-Content "$Build\version.txt" -Raw).Trim()
if (-not (Test-Path "$Build\Updater.exe")) { throw "Brak Updater.exe w $Build - zbuduj gre od nowa." }
$out = Join-Path (Split-Path $Build) "Release"
New-Item -ItemType Directory -Force $out | Out-Null

function New-Zip([string]$zip, [string]$root, [scriptblock]$include) {
    if (Test-Path $zip) { Remove-Item $zip }
    $fs = [IO.File]::Open($zip, 'Create')
    $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $root -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($root.Length + 1)
            if (& $include $rel) {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $_.FullName, $rel.Replace('\', '/'), 'Optimal') | Out-Null
            }
        }
    } finally { $za.Dispose(); $fs.Dispose() }
    "{0}: {1:0.0} MB" -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB)
}

$anyRelease = (gh release list -R $Repo -L 50 --json tagName | ConvertFrom-Json).Count -gt 0
if (-not $anyRelease) { $Full = $true; Write-Host "Pierwsze wydanie - dolaczam narzedzia." }

$assets = @()
$gameZip = "$out\HideAndSeek-game.zip"
New-Zip $gameZip $Build { param($r) -not ($r -like 'Tools\*' -or $r -like '*_DoNotShip\*' -or $r -like '_update\*') }
$assets += $gameZip
if ($Full) {
    $toolsZip = "$out\HideAndSeek-tools.zip"
    New-Zip $toolsZip "$Build\Tools" { param($r) $true }
    $assets += $toolsZip
    $Notes = ($Notes + "`n`n[full]").Trim()
}

$notesFile = "$out\notes.md"
[IO.File]::WriteAllText($notesFile, $Notes, (New-Object Text.UTF8Encoding($false)))
gh release create "v$version" @assets -R $Repo --title "Hide and Seek $version" --notes-file $notesFile --latest
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
Write-Host "Wydanie v$version opublikowane."
