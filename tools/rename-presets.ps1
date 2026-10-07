# Names your characters.
#
# The characters you pick from at a police station are the preset characters in
# <GTA V folder>\lspdfr\data\cop_presets.xml, and the name the game shows for one is its <Name>
# element. (A custom character is named in the game's own character creator; these are the presets.)
#
#   powershell -ExecutionPolicy Bypass -File tools\rename-presets.ps1 -List
#   powershell -ExecutionPolicy Bypass -File tools\rename-presets.ps1 -Number 4 -Name "Alex Doyle"
#   powershell -ExecutionPolicy Bypass -File tools\rename-presets.ps1 -Character "Victor Reyes" -Name "Alex Doyle"
#   powershell -ExecutionPolicy Bypass -File tools\rename-presets.ps1 -Map names.txt
#
# -List is the default when nothing else is asked for. Run it first: it prints each character with
# the number to use, and the agency and ped model so you can tell them apart.
#
# -Map reads a file, one rename per line, either form - blank lines and lines starting with # are
# ignored:
#
#       4 = Alex Doyle
#       Victor Reyes = Alex Doyle
#
# -DryRun says what would change and writes nothing.
#
# The <ScriptName> is deliberately left alone: it is what the game and the save files know the
# character by, it is never shown to the player, and changing it would only break references.
# Names do not have to be unique; script names are. The file is backed up before it is written, and
# the result is parsed - if it no longer parses, the backup is put back and nothing is left changed.

param(
    [string] $GtaFolder = "D:\Grand Theft Auto V Legacy",
    [switch] $List,
    [int]    $Number = 0,
    [string] $Character = "",
    [string] $Name = "",
    [string] $Map = "",
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$file = Join-Path $GtaFolder 'lspdfr\data\cop_presets.xml'
if (-not (Test-Path $file)) { throw "Not found: $file" }

$text = [System.IO.File]::ReadAllText($file)
$blocks = [regex]::Matches($text, '(?s)<Preset>.*?</Preset>')
if ($blocks.Count -eq 0) { throw "No <Preset> entries in $file" }

function Get-Field([string] $block, [string] $field) {
    $m = [regex]::Match($block, "<$field>([^<]*)</$field>")
    if ($m.Success) { return $m.Groups[1].Value } else { return "" }
}

$presets = @()
for ($i = 0; $i -lt $blocks.Count; $i++) {
    $b = $blocks[$i].Value
    $presets += [pscustomobject]@{
        Number     = $i + 1
        Name       = Get-Field $b 'Name'
        ScriptName = Get-Field $b 'ScriptName'
        Agency     = Get-Field $b 'Agency'
        Model      = Get-Field $b 'Model'
        Block      = $b
    }
}

function Show-Presets {
    Write-Host ("The characters in " + $file) -ForegroundColor Cyan
    foreach ($p in $presets) {
        Write-Host ("  [{0,2}] {1,-22} {2,-14} {3}" -f $p.Number, $p.Name, $p.Agency, $p.Model)
    }
    Write-Host ""
    Write-Host "  Rename one:  -Number 4 -Name ""Alex Doyle""     or    -Character ""Victor Reyes"" -Name ""Alex Doyle"""
    Write-Host "  Rename many: -Map names.txt                      one per line,  4 = Alex Doyle"
    Write-Host "  Change nothing: add -DryRun"
}

if ($List -or ($Number -eq 0 -and -not $Character -and -not $Map)) {
    Show-Presets
    return
}

# Resolve a key - either the number -List printed, or the current name or script name.
function Resolve-Preset([string] $key) {
    $found = @()
    if ($key -match '^\d+$') {
        $found = @($presets | Where-Object { $_.Number -eq [int]$key })
    } else {
        $found = @($presets | Where-Object { $_.Name -eq $key -or $_.ScriptName -eq $key })
    }
    if ($found.Count -eq 0) { throw "No character called or numbered '$key'.  Run -List." }
    if ($found.Count -gt 1) {
        $numbers = ($found | ForEach-Object { $_.Number }) -join ', '
        throw "'$key' matches more than one character (numbers $numbers). Use the number instead."
    }
    return $found[0]
}

$wanted = @()
if ($Map) {
    if (-not (Test-Path $Map)) { throw "Not found: $Map" }
    foreach ($line in (Get-Content $Map)) {
        $l = $line.Trim()
        if ($l -eq '' -or $l.StartsWith('#')) { continue }
        $parts = $l -split '=', 2
        if ($parts.Count -ne 2) { throw "Not 'key = name': $line" }
        $wanted += [pscustomobject]@{ Preset = (Resolve-Preset $parts[0].Trim()); NewName = $parts[1].Trim() }
    }
} else {
    if (-not $Name) { throw "Nothing to do: give -Name, or -Map, or -List." }
    if ($Number -eq 0 -and -not $Character) { throw "Which character? Give -Number (see -List) or -Character." }
    $key = if ($Number -gt 0) { "$Number" } else { $Character }
    $wanted += [pscustomobject]@{ Preset = (Resolve-Preset $key); NewName = $Name }
}

$new = $text
foreach ($w in $wanted) {
    $old = $w.Preset
    if ($old.Name -eq $w.NewName) {
        Write-Host ("  [{0,2}] {1} is already called that - skipped" -f $old.Number, $old.Name) -ForegroundColor DarkGray
        continue
    }
    $escaped = [System.Security.SecurityElement]::Escape($w.NewName)
    $blockNew = [regex]::Replace($old.Block, '<Name>[^<]*</Name>', ('<Name>' + $escaped + '</Name>'), 1)
    if ($blockNew -eq $old.Block) { throw "Could not find <Name> inside the block for '$($old.Name)'." }
    $new = $new.Replace($old.Block, $blockNew)
    Write-Host ("  [{0,2}] {1}  ->  {2}" -f $old.Number, $old.Name, $w.NewName) -ForegroundColor Yellow
}

if ($DryRun) {
    Write-Host "Dry run - nothing was written." -ForegroundColor Cyan
    return
}

if ($new -eq $text) {
    Write-Host "Nothing to write." -ForegroundColor Cyan
    return
}

$backup = $file + '.bak-names-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
Copy-Item $file $backup
[System.IO.File]::WriteAllText($file, $new, (New-Object System.Text.UTF8Encoding($false)))

try {
    [xml] $null = Get-Content $file
} catch {
    Copy-Item $backup $file -Force
    throw "The edit left the XML unreadable, so the backup was put back: $backup"
}

Write-Host ""
Write-Host "Renamed $($wanted.Count) character(s) in $file" -ForegroundColor Green
Write-Host "Backup: $backup" -ForegroundColor DarkGray
Write-Host "Start the game (or go on duty) for the names to show - LSPDFR reads this file at startup." -ForegroundColor DarkGray
