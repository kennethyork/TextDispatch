# Checks the bridge between TextDispatch and TextCallouts.
#
#   powershell -ExecutionPolicy Bypass -File tools\verify-bridge.ps1
#
# It reads both assemblies' metadata rather than loading them: both reference RagePluginHook, whose
# public types are compile-time stubs with no implementation, so a console process cannot load them at
# all. The check itself lives in tools\BridgeCheck, which carries its own Mono.Cecil dependency.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

Write-Host "Building the two plugins (Release)..." -ForegroundColor Cyan
dotnet build (Join-Path $repo 'src\TextCallouts\TextCallouts.csproj') -c Release -v quiet | Out-Null
dotnet build (Join-Path $repo 'src\TextDispatch\TextDispatch.csproj') -c Release -v quiet | Out-Null

Write-Host "Checking the bridge..." -ForegroundColor Cyan
dotnet run --project (Join-Path $PSScriptRoot 'BridgeCheck\BridgeCheck.csproj') -c Release -- $repo
exit $LASTEXITCODE
