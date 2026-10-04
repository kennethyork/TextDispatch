# Packages the TextCallouts release zip: the plugin, the install notes and the licence.
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
    Copy-Item (Join-Path $repo 'LICENSE') $stage

    $zip = Join-Path $OutputDirectory "TextCallouts-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: TextCallouts.dll $stamp, INSTALL.txt, LICENSE"
    Write-Host "No RagePluginHook.dll and no LSPD First Response.dll in it - neither may be redistributed." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
