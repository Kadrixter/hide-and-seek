# Builds Updater.exe (.NET Framework 4.8 - present on every Windows 10/11, no install needed).
# Usage: powershell -File Updater\build.ps1 [-Out <path to Updater.exe>]
param([string]$Out = "$PSScriptRoot\bin\Updater.exe")
$ErrorActionPreference = 'Stop'
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$icon = Join-Path $PSScriptRoot 'icon.ico'
$iconArg = if (Test-Path $icon) { "/win32icon:$icon" } else { $null }
& "$fw\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu "/out:$Out" $iconArg `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll `
    "$PSScriptRoot\Updater.cs" "$PSScriptRoot\UpdaterForm.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed ($LASTEXITCODE)" }
Write-Host "Updater built: $Out"
