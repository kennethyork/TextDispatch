# Packages the TextJobs release zip: the script, the install notes, the command list and the licence.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-textjobs.ps1 -Version 1.0.0
#
# The naming convention, so that the releases page reads as a list of things rather than sentences:
#
#   git tag    textjobs-v1.0.0
#   release    TextJobs 1.0.0
#   asset      TextJobs-1.0.0.zip
#
# TextJobs is not TextDispatch: it is a ScriptHookVDotNet script, so it goes in scripts\ and it has no
# LSPDFR or RAGE Plugin Hook in its requirements.

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutputDirectory = "",
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = $repo }
# Compress-Archive will not create the folder for you, and its complaint does not say so.
if (-not (Test-Path $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null }

$project = Join-Path $repo 'src\TextJobs\TextJobs.csproj'
Write-Host "Building TextJobs (Release)..." -ForegroundColor Cyan
dotnet build $project -c Release -v minimal -p:GtaFolder="$GtaFolder" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "the build failed" }

$dll = Join-Path $repo 'src\TextJobs\bin\Release\TextJobs.dll'
if (-not (Test-Path $dll)) { throw "no TextJobs.dll was produced" }

# The version asked for and the version built have to agree, or a download lies about itself.
$built = (Get-Item $dll).VersionInfo.FileVersion
$stamp = ($built -split '\.')[0..2] -join '.'
if ($stamp -ne $Version) { throw "the build produced $built but $Version was asked for" }

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("textjobs-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null

try {
    Copy-Item $dll $stage
    Copy-Item (Join-Path $PSScriptRoot 'INSTALL-textjobs.txt') $stage
    Copy-Item (Join-Path $repo 'src\TextJobs\COMMANDS.md') (Join-Path $stage 'COMMANDS.md')
    Copy-Item (Join-Path $repo 'LICENSE') $stage

    $zip = Join-Path $OutputDirectory "TextJobs-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: TextJobs.dll $built, INSTALL.txt, COMMANDS.md, LICENSE"
    Write-Host "ScriptHookVDotNet3.dll is not in it - the game already has one, and a copy here would be loaded as a script." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
