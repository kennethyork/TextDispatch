# Builds all three plugins and installs each one where it is loaded from.
#
#   powershell -ExecutionPolicy Bypass -File tools\install-bundle.ps1
#
# TextDispatch is the police side, TextCallouts the callout pack, TextJobs the civilian side. Three
# separate DLLs - that is deliberate, each works without the others - but they are installed together
# and, when more than one is present, they talk: every line a callout says is mirrored into
# TextDispatch's chat box, TextDispatch lists the callouts and can start any by name, and TextJobs
# reads DriverJobs V's own job file.
#
# Two of them are LSPDFR plugins and go in Plugins\LSPDFR. TextJobs is a ScriptHookVDotNet script and
# goes in scripts\, beside DriverJobs - a plugin in the wrong folder is never loaded at all.

param(
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$sdk = Join-Path $repo 'tools\rph-sdk\RagePluginHook.dll'
if (-not (Test-Path $sdk)) {
    Write-Error "RAGE Plugin Hook SDK not found at tools\rph-sdk\RagePluginHook.dll (see src\TextDispatch\README.md)."
}

$lspdfr = Join-Path $GtaFolder 'plugins\LSPD First Response.dll'
if (-not (Test-Path $lspdfr)) { Write-Error "LSPDFR not found at $lspdfr" }

$shvdn = Join-Path $GtaFolder 'ScriptHookVDotNet3.dll'
if (-not (Test-Path $shvdn)) {
    Write-Host "ScriptHookVDotNet3.dll is not in the game folder: TextJobs will be installed, and will not run until it is." -ForegroundColor Yellow
}

$target = Join-Path $GtaFolder 'Plugins\LSPDFR'
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

$scripts = Join-Path $GtaFolder 'scripts'
if (-not (Test-Path $scripts)) { New-Item -ItemType Directory -Path $scripts -Force | Out-Null }

$plugins = @(
    @{ Name = 'TextDispatch'; Project = 'src\TextDispatch\TextDispatch.csproj'; Folder = $target },
    @{ Name = 'TextCallouts'; Project = 'src\TextCallouts\TextCallouts.csproj'; Folder = $target },
    @{ Name = 'TextJobs';     Project = 'src\TextJobs\TextJobs.csproj';         Folder = $scripts }
)

foreach ($plugin in $plugins)
{
    Write-Host ("Building " + $plugin.Name + " (" + $Configuration + ")...") -ForegroundColor Cyan
    dotnet build (Join-Path $repo $plugin.Project) -c $Configuration -v minimal -p:GtaFolder="$GtaFolder"
    if ($LASTEXITCODE -ne 0) { throw ($plugin.Name + " failed to build.") }

    $source = Join-Path $repo ("src\" + $plugin.Name + "\bin\" + $Configuration + "\" + $plugin.Name + ".dll")
    if (-not (Test-Path $source)) { throw ("Build produced no DLL at " + $source) }

    Copy-Item $source (Join-Path $plugin.Folder ($plugin.Name + ".dll")) -Force
    $version = [System.Reflection.AssemblyName]::GetAssemblyName($source).Version
    Write-Host ("  installed " + $plugin.Name + ".dll " + $version + " -> " + $plugin.Folder) -ForegroundColor Green
}

# The two LSPDFR plugins go to Plugins\LSPDFR: only there does LSPDFR load a plugin into its own
# AppDomain, which is what lets a callout pack see the callout API - and what lets TextCallouts find
# TextDispatch by reflection. TextJobs goes to scripts\, where ScriptHookVDotNet looks, and where the
# jobs mod it reads is already installed.

Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then GO ON DUTY (press E at a police station)." -ForegroundColor Cyan
Write-Host "The two LSPDFR plugins load at that moment, and the callouts register then too." -ForegroundColor Cyan
Write-Host "TextJobs loads with the game instead - left arrow for the police box, F9 for the jobs box." -ForegroundColor Cyan
Write-Host ""
Write-Host "Logs: $target\textdispatch.log, $target\textcallouts.log, $scripts\TextJobs.log"
Write-Host "In the police box: /help, /calls, /callout <name>.  In the jobs box: /jobs, /job <name>.  F4 console: tdstatus, tcstatus."
