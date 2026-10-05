# Packages the TextCallouts release zip: the plugin, the install notes and the licence.
#
# Naming, and RELEASES.md is the authority on it:
#
#   git tag    callouts-v1.2.1
#   release    TextCallouts 1.2.1
#   asset      TextCallouts-1.2.1.zip
#
# The release name carries the product and its version, nothing else - what changed goes in the notes.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-textcallouts.ps1 -Version 1.0.0

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutputDirectory = "",
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = $repo }

$project = Join-Path $repo 'src\TextCallouts\TextCallouts.csproj'
Write-Host "Building TextCallouts (Release)..." -ForegroundColor Cyan
dotnet build $project -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll = Join-Path $repo 'src\TextCallouts\bin\Release\TextCallouts.dll'
if (-not (Test-Path $dll)) { throw "Build produced no DLL at $dll" }

$stamp = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($stamp -ne "$Version.0") {
    throw "The build says $stamp but this is release $Version. Bump <Version> in TextCallouts.csproj first."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("textcallouts-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item $dll $stage
    Copy-Item (Join-Path $repo 'src\TextCallouts\INSTALL.txt') $stage

    # The callout library: the pack's own recipes, in the same format a player's are. They go in the
    # player's data folder beside the DLL, in a folder of their own - Library, next to Custom - and the
    # pack reads both.
    $library = Join-Path $repo 'src\TextCallouts\Library'
    if (Test-Path $library) { Copy-Item $library (Join-Path $stage 'Library') -Recurse }

    Copy-Item (Join-Path $repo 'LICENSE') $stage

    $zip = Join-Path $OutputDirectory "TextCallouts-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    $recipeCount = 0
    if (Test-Path $library) { $recipeCount = (Get-ChildItem $library -Filter '*.xml').Count }
    Write-Host "Contents: TextCallouts.dll $stamp, INSTALL.txt, LICENSE, Library\ ($recipeCount recipe callouts)"
    Write-Host "No RagePluginHook.dll and no LSPD First Response.dll in it - neither may be redistributed." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
