# Packages the bundle release zip: both plugins, the combined install notes and the licence.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-bundle.ps1 -Version 1.0.0

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutputDirectory = "",
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = $repo }

$dispatchProject = Join-Path $repo 'src\TextDispatch\TextDispatch.csproj'
$calloutsProject = Join-Path $repo 'src\TextCallouts\TextCallouts.csproj'

Write-Host "Building TextDispatch and TextCallouts (Release)..." -ForegroundColor Cyan
dotnet build $dispatchProject -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "TextDispatch failed to build." }
dotnet build $calloutsProject -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "TextCallouts failed to build." }

$dispatch = Join-Path $repo 'src\TextDispatch\bin\Release\TextDispatch.dll'
$callouts = Join-Path $repo 'src\TextCallouts\bin\Release\TextCallouts.dll'
foreach ($dll in @($dispatch, $callouts)) {
    if (-not (Test-Path $dll)) { throw "Missing $dll" }
}

$dispatchVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dispatch).Version.ToString()
$calloutsVersion = [System.Reflection.AssemblyName]::GetAssemblyName($callouts).Version.ToString()

# The install notes state the versions inside, so a download can be identified without opening the DLLs.
$notes = Join-Path $repo 'BUNDLE.txt'
$text = [System.IO.File]::ReadAllText($notes)
$text = $text.Replace('TextDispatch %DISPATCH_VERSION%', "TextDispatch $dispatchVersion")
$text = $text.Replace('TextCallouts %CALLOUTS_VERSION%', "TextCallouts $calloutsVersion")

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("bundle-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item $dispatch $stage
    Copy-Item $callouts $stage
    Copy-Item (Join-Path $repo 'LICENSE') $stage
    [System.IO.File]::WriteAllText((Join-Path $stage 'BUNDLE.txt'), $text)

    $zip = Join-Path $OutputDirectory "TextDispatch+TextCallouts-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: TextDispatch.dll $dispatchVersion, TextCallouts.dll $calloutsVersion, BUNDLE.txt, LICENSE"
    Write-Host "No RagePluginHook.dll and no LSPD First Response.dll - neither may be redistributed." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
