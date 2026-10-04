# Packages the CursorFree release zip: the executable, its notes and the licence.
#
#   powershell -ExecutionPolicy Bypass -File tools\package-cursorfree.ps1 -Version 1.0.0
#
# Naming, same as the bundle so the releases page reads as a list:
#
#   git tag    cursorfree-1.0.0
#   release    cursorfree 1.0.0
#   asset      cursorfree-1.0.0.zip

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = $repo }

$project = Join-Path $repo 'src\CursorFree\CursorFree.csproj'
Write-Host "Building CursorFree (Release)..." -ForegroundColor Cyan
dotnet build $project -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$exe = Join-Path $repo 'src\CursorFree\bin\Release\CursorFree.exe'
if (-not (Test-Path $exe)) { throw "Build produced no exe at $exe" }

# The exe is the thing people run, so it has to be the version the release claims to be.
$stamp = [System.Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString()
if ($stamp -ne "$Version.0") {
    throw "The build says $stamp but this is release $Version. Bump <Version> in CursorFree.csproj first."
}

# It is not a console program, and a WinExe's output does not come back through a normal capture - so
# the status text is redirected to a file and read from there. The point of the check is that the exe
# loads, runs and reports, which is the failure nobody forgives.
$statusFile = Join-Path ([System.IO.Path]::GetTempPath()) ("cursorfree-" + [guid]::NewGuid().ToString('N') + ".txt")
$run = Start-Process -FilePath $exe -ArgumentList '--status' -Wait -NoNewWindow -PassThru -RedirectStandardOutput $statusFile
$status = @()
if (Test-Path $statusFile) { $status = Get-Content $statusFile; Remove-Item $statusFile -Force }

if ($run.ExitCode -ne 0) { throw "CursorFree.exe --status exited with code $($run.ExitCode)" }
if (-not ($status -match 'clip')) { throw "CursorFree.exe --status reported nothing back" }
Write-Host ("  --status says: " + (($status | Where-Object { $_ }) -join ' | ')) -ForegroundColor DarkGray

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("cursorfree-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item $exe $stage
    Copy-Item (Join-Path $repo 'src\CursorFree\README.md') (Join-Path $stage 'README.md')
    Copy-Item (Join-Path $repo 'LICENSE') $stage

    $zip = Join-Path $OutputDirectory "cursorfree-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host ("Packaged " + $zip + "  (" + [Math]::Round((Get-Item $zip).Length / 1kb) + " KB)") -ForegroundColor Green
    Write-Host "Contents: CursorFree.exe $stamp, README.md, LICENSE"
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
