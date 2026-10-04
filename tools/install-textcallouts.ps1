# Builds TextCallouts and installs it where LSPDFR will load it.
#
#   powershell -ExecutionPolicy Bypass -File tools\install-textcallouts.ps1
#
# Override the game folder if yours is somewhere else:
#   ... -File tools\install-textcallouts.ps1 -GtaFolder "E:\Games\GTAV"

param(
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$sdk = Join-Path $repo 'tools\rph-sdk\RagePluginHook.dll'
if (-not (Test-Path $sdk)) {
    Write-Error @"
RAGE Plugin Hook SDK not found at tools\rph-sdk\RagePluginHook.dll

Get RAGE Plugin Hook from https://ragepluginhook.net/Downloads.aspx and copy
SDK\RagePluginHook.dll into tools\rph-sdk\.
"@
}

$lspdfr = Join-Path $GtaFolder 'plugins\LSPD First Response.dll'
if (-not (Test-Path $lspdfr)) {
    Write-Error "LSPDFR not found at $lspdfr - this pack is built against its callout API."
}

$project = Join-Path $repo 'src\TextCallouts\TextCallouts.csproj'
Write-Host "Building TextCallouts ($Configuration)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$source = Join-Path $repo "src\TextCallouts\bin\$Configuration\TextCallouts.dll"
if (-not (Test-Path $source)) { throw "Build produced no DLL at $source" }

# ---------------------------------------------------------------------------------------------
# Plugins\LSPDFR again: that is the folder LSPDFR scans, into its own AppDomain. In Plugins\ the
# callouts would load as an RPH plugin, find no LSPDFR, and quietly do nothing at all.
# ---------------------------------------------------------------------------------------------
$target = Join-Path $GtaFolder 'Plugins\LSPDFR'
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

Copy-Item $source (Join-Path $target 'TextCallouts.dll') -Force
Write-Host "Installed TextCallouts.dll -> $target" -ForegroundColor Green

# RagePluginHook.dll and LSPD First Response.dll are never copied: RPH's terms forbid shipping the
# SDK, and LSPDFR is already in the game, loaded by RPH. Both are referenced Private=false.

Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then GO ON DUTY (press E at a police station)." -ForegroundColor Cyan
Write-Host "The callouts are registered at that moment and dispatch will start offering them." -ForegroundColor Cyan
Write-Host "Log: $target\textcallouts.log"
