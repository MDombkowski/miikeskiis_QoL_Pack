# Refuses a mod's name that nothing the pack shows a player may hold: miikeskii's rule 8 (PROJECT.md, 2026-10-10), a
# setting's text or a message names another mod only when ours works with it, changes it, stands aside for it or
# deliberately makes room for it, and a mod he no longer uses is named nowhere (card B05, after Topper-Upper's settings
# still compared their numbers with ServersideQoL's once it was gone from the server).
#
#   powershell -ExecutionPolicy Bypass -File tools\check-mod-names.ps1 [-List <file>] <folder or file> [<folder or file> ...]
#
# The names are read from tools\mods-never-named.txt (or -List): one per line, # for a note. A folder means its
# README.md; a .dll means every string compiled into it, which is every setting's name and description, every message
# on screen and every log line, and its attributes (comments never reach a DLL); any other file is read as text. A name
# matches whole, in any case: not inside a longer word, so "ServersideQoL" never matches the pack's own
# "ServerSideQoLPack". CHANGELOGs are history and are left out, and CREDITS.md too: crediting where a module's ideas
# came from is his rule 5. The bundle's package.ps1 runs it over the bundle's and every module's README before it
# builds, and over the built QoLMods.dll before it packs (every single release it packs is built from the same code).
[CmdletBinding(PositionalBinding = $false)]   # -List only by name; every other argument is a path
param(
    [string]$List,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Paths
)
$ErrorActionPreference = 'Stop'

if (-not $List) { $List = Join-Path $PSScriptRoot 'mods-never-named.txt' }
if (-not (Test-Path -LiteralPath $List)) { throw "check-mod-names: the list $List doesn't exist." }
# A # starts a note, at the start of a line or after a name.
$names = @(Get-Content -LiteralPath $List -Encoding UTF8 | ForEach-Object { ($_ -replace '#.*$', '').Trim() } | Where-Object { $_ })
if ($names.Count -eq 0) { throw "check-mod-names: the list $List names no mod, so it would check nothing." }
# A list saved in another encoding (UTF-16 without its mark) reads as names that can never match, and would pass
# everything (review n9); a mod's name is plain letters, so anything else is refused.
$odd = @($names | Where-Object { $_ -notmatch '^[\x20-\x7e]+$' })
if ($odd.Count -gt 0) { throw "check-mod-names: the list $List has a name that isn't plain letters, digits and punctuation (save the list as UTF-8, and write the name as the mod spells it in plain letters): $($odd[0])" }
if (-not $Paths -or $Paths.Count -eq 0) { throw 'check-mod-names: name a folder, a README or a DLL to check.' }

# Whole names only: no letter or digit right before or after.
$pattern = '(?i)(?<![A-Za-z0-9])(' + (($names | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')(?![A-Za-z0-9])'

$files = @()
foreach ($path in $Paths) {
    if (Test-Path -LiteralPath $path -PathType Container) {
        $readme = Join-Path $path 'README.md'
        if (Test-Path -LiteralPath $readme) { $files += $readme }
    } elseif (Test-Path -LiteralPath $path) {
        $files += $path
    } else {
        throw "check-mod-names: $path doesn't exist."
    }
}

$latin1 = [System.Text.Encoding]::GetEncoding(28591)
$found = @()
foreach ($file in $files) {
    if ($file -like '*.dll') {
        # Its strings, as check-shipped-text.ps1 reads them: the UTF-16 ones (string literals: descriptions, messages, log
        # lines), and the plain ones (attributes, such as a plugin's name).
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $file).Path)
        $raw = $latin1.GetString($bytes)
        # A set, not an array grown in a loop: Windows PowerShell 5.1 copies an array at every +=, which took the
        # pack's DLL from 7 s to 51 s once the plain strings were read too (second look, R2).
        $strings = New-Object 'System.Collections.Generic.HashSet[string]'
        foreach ($match in [regex]::Matches($raw, '(?:[\x20-\x7e\xa0-\xff]\x00|[\x10-\x2f]\x20){6,}')) {
            [void]$strings.Add([System.Text.Encoding]::Unicode.GetString($bytes, $match.Index, $match.Length))
        }
        foreach ($match in [regex]::Matches($raw, '[\x20-\x7e]{6,}')) { [void]$strings.Add($match.Value) }
        foreach ($text in $strings) {
            $hit = [regex]::Match($text, $pattern)
            if ($hit.Success) {
                $line = $text.Trim()
                $start = [Math]::Max(0, $hit.Index - 60)
                if ($line.Length -gt 140) { $line = '...' + $text.Substring($start, [Math]::Min(140, $text.Length - $start)).Trim() + '...' }
                $found += "  $($file) (a string in it, naming $($hit.Groups[1].Value)): $line"
            }
        }
        continue
    }
    foreach ($hit in @(Select-String -LiteralPath $file -Pattern $pattern -Encoding UTF8)) {
        $line = $hit.Line.Trim()
        if ($line.Length -gt 140) { $line = $line.Substring(0, 140) + '...' }
        $found += "  $($file):$($hit.LineNumber): $line"
    }
}
if ($found.Count -gt 0) {
    throw ("These would show a player the name of a mod the pack never names (tools\mods-never-named.txt; his rule 8, card B05). Reword them without it:`n" + ($found -join "`n"))
}
"check-mod-names: none of the $($names.Count) name(s) in $(Split-Path -Leaf $List) in $($files.Count) file(s)."
