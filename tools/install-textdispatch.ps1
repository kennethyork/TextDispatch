# Builds TextDispatch and installs it into the game's Plugins folder.
#
#   powershell -ExecutionPolicy Bypass -File tools\install-textdispatch.ps1
#
# Override the game folder if yours is somewhere else:
#   ... -File tools\install-textdispatch.ps1 -GtaFolder "E:\Games\GTAV"

param(
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy",
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$sdk = Join-Path $repo 'tools\rph-sdk\RagePluginHook.dll'
if (-not (Test-Path $sdk)) {
    Write-Error @"
RAGE Plugin Hook SDK not found at tools\rph-sdk\RagePluginHook.dll

Get RAGE Plugin Hook from https://ragepluginhook.net/Downloads.aspx and copy
SDK\RagePluginHook.dll into tools\rph-sdk\.

It is deliberately kept out of source control: RPH's terms say it must not be
redistributed, and the plugin never ships it either.
"@
}

$project = Join-Path $repo 'src\TextDispatch\TextDispatch.csproj'
Write-Host "Building TextDispatch ($Configuration)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$source = Join-Path $repo "src\TextDispatch\bin\$Configuration\TextDispatch.dll"
if (-not (Test-Path $source)) { throw "Build produced no DLL at $source" }

$plugins = Join-Path $GtaFolder 'Plugins'
if (-not (Test-Path $plugins)) {
    Write-Host "Creating $plugins" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $plugins | Out-Null
}

Copy-Item $source (Join-Path $plugins 'TextDispatch.dll') -Force
Write-Host "Installed TextDispatch.dll -> $plugins" -ForegroundColor Green
Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then press T in game." -ForegroundColor Cyan
Write-Host "Log: $env:APPDATA\TextDispatch\textdispatch.log"
