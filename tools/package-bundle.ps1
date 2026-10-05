# Packages the bundle release zip: all three plugins, the combined install notes and the licence.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-bundle.ps1 -Version 1.4.0
#
# The naming convention, so that the releases page reads as a list of things rather than sentences:
#
#   git tag    bundle-1.4.0
#   release    bundle 1.4.0
#   asset      bundle-1.4.0.zip
#   the notes  BUNDLE.txt, whose title line says "bundle 1.4.0"
#
# The bundle has its own version because it is its own thing: it names a *set* of plugin versions,
# which move independently of each other.
#
# TextJobs is the odd one of the three: it is a ScriptHookVDotNet script rather than an LSPDFR plugin,
# so it goes into scripts\ and its command list ships beside TextDispatch's rather than replacing it.

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

$dispatchProject = Join-Path $repo 'src\TextDispatch\TextDispatch.csproj'
$calloutsProject = Join-Path $repo 'src\TextCallouts\TextCallouts.csproj'
$jobsProject = Join-Path $repo 'src\TextJobs\TextJobs.csproj'

Write-Host "Building TextDispatch, TextCallouts and TextJobs (Release)..." -ForegroundColor Cyan
dotnet build $dispatchProject -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "TextDispatch failed to build." }
dotnet build $calloutsProject -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "TextCallouts failed to build." }
dotnet build $jobsProject -c Release -v minimal -p:GtaFolder="$GtaFolder"
if ($LASTEXITCODE -ne 0) { throw "TextJobs failed to build." }

$dispatch = Join-Path $repo 'src\TextDispatch\bin\Release\TextDispatch.dll'
$callouts = Join-Path $repo 'src\TextCallouts\bin\Release\TextCallouts.dll'
$jobs = Join-Path $repo 'src\TextJobs\bin\Release\TextJobs.dll'
foreach ($dll in @($dispatch, $callouts, $jobs)) {
    if (-not (Test-Path $dll)) { throw "Missing $dll" }
}

$dispatchVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dispatch).Version.ToString()
$calloutsVersion = [System.Reflection.AssemblyName]::GetAssemblyName($callouts).Version.ToString()
$jobsVersion = [System.Reflection.AssemblyName]::GetAssemblyName($jobs).Version.ToString()

# The install notes state the versions inside, so a download can be identified without opening the DLLs.
$notes = Join-Path $repo 'BUNDLE.txt'
$text = [System.IO.File]::ReadAllText($notes)
$text = $text.Replace('%BUNDLE_VERSION%', $Version)
$text = $text.Replace('TextDispatch %DISPATCH_VERSION%', "TextDispatch $dispatchVersion")
$text = $text.Replace('TextCallouts %CALLOUTS_VERSION%', "TextCallouts $calloutsVersion")
$text = $text.Replace('TextJobs %TEXTJOBS_VERSION%', "TextJobs $jobsVersion")

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("bundle-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item $dispatch $stage
    Copy-Item $callouts $stage
    Copy-Item $jobs $stage

    # Two command lists, two files: TextDispatch's keeps the plain name, TextJobs' names itself.
    Copy-Item (Join-Path $repo 'src\TextDispatch\COMMANDS.md') (Join-Path $stage 'COMMANDS.md')
    Copy-Item (Join-Path $repo 'src\TextJobs\COMMANDS.md') (Join-Path $stage 'COMMANDS-TextJobs.md')
    Copy-Item (Join-Path $repo 'LICENSE') $stage
    [System.IO.File]::WriteAllText((Join-Path $stage 'BUNDLE.txt'), $text)

    $zip = Join-Path $OutputDirectory "bundle-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: TextDispatch.dll $dispatchVersion, TextCallouts.dll $calloutsVersion, TextJobs.dll $jobsVersion,"
    Write-Host "          BUNDLE.txt, COMMANDS.md (TextDispatch), COMMANDS-TextJobs.md, LICENSE"
    Write-Host "No RagePluginHook.dll and no LSPD First Response.dll - neither may be redistributed." -ForegroundColor DarkGray
    Write-Host "TextDispatch.dll and TextCallouts.dll go in Plugins\LSPDFR; TextJobs.dll goes in scripts." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
