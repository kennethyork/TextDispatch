# Packages the TextDispatch release zip: the plugin, the install notes and the licence.
#
# Naming, and RELEASES.md is the authority on it:
#
#   git tag    v1.0.14
#   release    TextDispatch 1.0.14
#   asset      TextDispatch-1.0.14.zip
#
# The release name carries the product and its version, nothing else - what changed goes in the notes,
# which is where somebody who clicks the release is already looking.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 1.0.12
#
# The install notes live in tools\INSTALL.txt rather than inside this command, because the version of
# them that existed only as a string in a shell command was wrong in three consecutive releases and
# nobody noticed.

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = $repo }
# Compress-Archive will not create the folder for you, and its complaint does not say so.
if (-not (Test-Path $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null }

$project = Join-Path $repo 'src\TextDispatch\TextDispatch.csproj'
Write-Host "Building TextDispatch (Release)..." -ForegroundColor Cyan
dotnet build $project -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll = Join-Path $repo 'src\TextDispatch\bin\Release\TextDispatch.dll'
if (-not (Test-Path $dll)) { throw "Build produced no DLL at $dll" }

# The DLL is what people download, so it has to be the version the release claims to be.
$stamp = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($stamp -ne "$Version.0") {
    throw "The build says $stamp but this is release $Version. Bump <Version> in TextDispatch.csproj first."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("textdispatch-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item $dll $stage
    Copy-Item (Join-Path $PSScriptRoot 'INSTALL.txt') $stage
    # Beside the plugin's own manual, and shipped with it, so a download comes with its command list.
    Copy-Item (Join-Path $PSScriptRoot '..\src\TextDispatch\COMMANDS.md') (Join-Path $stage 'COMMANDS.md')
    Copy-Item (Join-Path $repo 'LICENSE') $stage

    $zip = Join-Path $OutputDirectory "TextDispatch-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: TextDispatch.dll $stamp, INSTALL.txt, COMMANDS.md, LICENSE"
    Write-Host "RagePluginHook.dll is not in it, and never will be - RPH's terms forbid redistributing it." -ForegroundColor DarkGray
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
