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

# ---------------------------------------------------------------------------------------------
# The library as well, and this is the part that is easy to forget: the DLL and the recipes are
# shipped together and are meant to move together. Installing the plugin without its recipes leaves
# the previous library in place, which looks exactly like a pack that has stopped working - the new
# callouts are simply not in the folder it reads. The player's own Custom folder is beside it and is
# never touched: the library belongs to the pack, Custom belongs to them.
$library = Join-Path $repo 'src\TextCallouts\Library'
if (Test-Path $library) {
    $installed = Join-Path $target 'TextCallouts\Library'
    if (Test-Path $installed) { Remove-Item $installed -Recurse -Force }
    New-Item -ItemType Directory -Path (Split-Path -Parent $installed) -Force | Out-Null
    Copy-Item $library $installed -Recurse -Force
    $recipes = (Get-ChildItem $installed -Filter '*.xml').Count
    $spoken = (Select-String -Path (Join-Path $installed '*.xml') -Pattern '<Line>').Count
    Write-Host "Installed the library   -> $installed  ($recipes recipes, $spoken script lines)" -ForegroundColor Green

    $scriptless = @(Get-ChildItem $installed -Filter '*.xml' | Where-Object { (Get-Content $_.FullName -Raw) -notmatch '<Script>' })
    if ($scriptless.Count -gt 0) {
        Write-Host ("" + $scriptless.Count + " recipe(s) went in without a script - that is a library built by hand, not by tools\make-callout-library.ps1:") -ForegroundColor Red
        foreach ($file in $scriptless) { Write-Host ("   " + $file.Name) -ForegroundColor Red }
    }
}
else {
    Write-Host "No Library folder in the repo - only the plugin was installed." -ForegroundColor Yellow
}

# RagePluginHook.dll and LSPD First Response.dll are never copied: RPH's terms forbid shipping the
# SDK, and LSPDFR is already in the game, loaded by RPH. Both are referenced Private=false.

Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then GO ON DUTY (press E at a police station)." -ForegroundColor Cyan
Write-Host "The callouts are registered at that moment and dispatch will start offering them." -ForegroundColor Cyan
Write-Host "Log: $target\textcallouts.log"
