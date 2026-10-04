# Builds TextDispatch and installs it where LSPDFR will load it.
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

# ---------------------------------------------------------------------------------------------
# Plugins\LSPDFR, NOT Plugins\.
#
# LSPDFR loads the DLLs in Plugins\LSPDFR into its own AppDomain, which is what lets a plugin
# see LSPDFR's types and call the API. RPH gives every plugin it loads its own AppDomain, so a
# copy in Plugins\ runs perfectly and then cannot see LSPDFR at all - no callouts, no dispatch,
# no ped state. It looks like the plugin works and silently does nothing.
# ---------------------------------------------------------------------------------------------
$target = Join-Path $GtaFolder 'Plugins\LSPDFR'
if (-not (Test-Path $target)) {
    Write-Host "Creating $target" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

Copy-Item $source (Join-Path $target 'TextDispatch.dll') -Force
Write-Host "Installed TextDispatch.dll -> $target" -ForegroundColor Green

# ---------------------------------------------------------------------------------------------
# Undo the earlier, wrong install: a copy in Plugins\ and its startup.rphs entry.
# Left in place, RPH loads that copy into the wrong AppDomain and it does nothing useful.
# ---------------------------------------------------------------------------------------------
$stale = Join-Path $GtaFolder 'Plugins\TextDispatch.dll'
if (Test-Path $stale) {
    try {
        Remove-Item $stale -Force
        Write-Host "Removed the old copy in Plugins\ (RPH loaded that one into the wrong AppDomain)" -ForegroundColor Yellow
    } catch {
        Write-Host "Could not remove $stale - close the game first, then delete it by hand." -ForegroundColor Red
    }
}

$startup = Join-Path $GtaFolder 'startup.rphs'
if (Test-Path $startup) {
    $before = @(Get-Content $startup)
    $after  = @($before | Where-Object { $_ -notmatch 'TextDispatch' })

    if ($after.Count -ne $before.Count) {
        [System.IO.File]::WriteAllLines($startup, $after, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "Removed the TextDispatch line from startup.rphs - LSPDFR loads it now." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then press T in game." -ForegroundColor Cyan
Write-Host "Log: $target\textdispatch.log"   # beside the DLL and the ini
Write-Host ""
Write-Host "The log's first lines should say 'detected, LSPDFR 0.4.9'. If they say 'not installed'," -ForegroundColor DarkGray
Write-Host "this plugin is being loaded by RPH instead of LSPDFR and is in the wrong folder." -ForegroundColor DarkGray
