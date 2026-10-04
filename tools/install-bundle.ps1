# Builds both plugins and installs them where LSPDFR loads them.
#
#   powershell -ExecutionPolicy Bypass -File tools\install-bundle.ps1
#
# TextDispatch is the text interface; TextCallouts is the callout pack. They are separate DLLs - that
# is deliberate, each works without the other - but they are installed together and, when both are
# present, they talk: every line a callout says is mirrored into TextDispatch's chat box, and
# TextDispatch lists the callouts and can start any of them by name.

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

$target = Join-Path $GtaFolder 'Plugins\LSPDFR'
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

$plugins = @(
    @{ Name = 'TextDispatch'; Project = 'src\TextDispatch\TextDispatch.csproj' },
    @{ Name = 'TextCallouts'; Project = 'src\TextCallouts\TextCallouts.csproj' }
)

foreach ($plugin in $plugins)
{
    Write-Host ("Building " + $plugin.Name + " (" + $Configuration + ")...") -ForegroundColor Cyan
    dotnet build (Join-Path $repo $plugin.Project) -c $Configuration -v minimal -p:GtaFolder="$GtaFolder"
    if ($LASTEXITCODE -ne 0) { throw ($plugin.Name + " failed to build.") }

    $source = Join-Path $repo ("src\" + $plugin.Name + "\bin\" + $Configuration + "\" + $plugin.Name + ".dll")
    if (-not (Test-Path $source)) { throw ("Build produced no DLL at " + $source) }

    Copy-Item $source (Join-Path $target ($plugin.Name + ".dll")) -Force
    $version = [System.Reflection.AssemblyName]::GetAssemblyName($source).Version
    Write-Host ("  installed " + $plugin.Name + ".dll " + $version + " -> " + $target) -ForegroundColor Green
}

# Both go to Plugins\LSPDFR: only there does LSPDFR load a plugin into its own AppDomain, which is what
# lets a callout pack see the callout API - and what lets TextCallouts find TextDispatch by reflection.

Write-Host ""
Write-Host "Launch RagePluginHook.exe, load story mode, then GO ON DUTY (press E at a police station)." -ForegroundColor Cyan
Write-Host "Both plugins load at that moment; the callouts register then too." -ForegroundColor Cyan
Write-Host ""
Write-Host "Logs: $target\textdispatch.log and $target\textcallouts.log"
Write-Host "In the box: /help, /calls, /callout <name>.  In the F4 console: tdstatus, tcstatus."
