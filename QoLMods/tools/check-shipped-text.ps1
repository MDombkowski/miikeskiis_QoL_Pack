# Refuses text that ships in a zip and says how a mod was tested, or that it wasn't. Whether and how a version was
# tested lives only in the project's docs (cards, status records, session pages), never in a pack: miikeskii's rule of
# 2026-10-10 (card B07), after Thunderstore rejected the pack as an "Untested AI generated mod" because its changelog
# said "Not yet tested in game" on version after version he had in fact tested.
#
#   powershell -ExecutionPolicy Bypass -File tools\check-shipped-text.ps1 <folder or file> [<folder or file> ...]
#
# A folder means its README.md, CHANGELOG.md and CREDITS.md. A .dll means the strings compiled into it (every setting's
# description and every message ships too: 0.45.0's DLL said "untested" in two settings, card B10), and the check also
# refuses a DLL holding this computer's user folder (the build's debug path, from a build under the user profile). The
# bundle's package.ps1 runs it over the bundle and every module before it builds, and over the built DLL before it
# packs; any other packager (a single release's, the server pack's) can call it the same way.
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Paths)
$ErrorActionPreference = 'Stop'

# Testing words, and the build notes that carried them ("By session ..., reviewed by an Opus agent, checked by a
# scratch harness", "found in review", a session's number such as "55-B"), and "still in the works". A line about the
# game's own behaviour ("fires, tested again a moment after the change") passes.
$pattern = 'untested|not (yet )?tested|tested in game|not been tested|without testing|scratch harness|planted bugs?|\bOpus (agent|round)s?\b|code review|last look|found in review|in the works|\bsessions? \d+-[A-Z]\b|^\s*- By sessions?\b'

$files = @()
foreach ($path in $Paths) {
    if (Test-Path -LiteralPath $path -PathType Container) {
        foreach ($doc in @('README.md', 'CHANGELOG.md', 'CREDITS.md')) {
            $file = Join-Path $path $doc
            if (Test-Path -LiteralPath $file) { $files += $file }
        }
    } elseif (Test-Path -LiteralPath $path) {
        $files += $path
    } else {
        throw "check-shipped-text: $path doesn't exist."
    }
}

# This computer's user folder, as plain text and as a DLL's UTF-16 strings hold it.
$latin1 = [System.Text.Encoding]::GetEncoding(28591)
$profileFolder = $env:USERPROFILE
$profileForms = @()
if ($profileFolder) { $profileForms = @($profileFolder, $latin1.GetString([System.Text.Encoding]::Unicode.GetBytes($profileFolder))) }

$found = @()
foreach ($file in $files) {
    if ($file -like '*.dll') {
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $file).Path)
        $raw = $latin1.GetString($bytes)
        foreach ($form in $profileForms) {
            if ($raw.IndexOf($form, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $found += "  $($file): holds this computer's user folder ($profileFolder), most likely the build's debug path: pack from a folder outside the user profile."
                break
            }
        }
        # Its strings: the UTF-16 ones (string literals: settings' descriptions, messages), and the plain ones (attributes).
        $strings = @()
        foreach ($match in [regex]::Matches($raw, '(?:[\x20-\x7e\xa0-\xff]\x00|[\x10-\x2f]\x20){6,}')) {
            $strings += [System.Text.Encoding]::Unicode.GetString($bytes, $match.Index, $match.Length)
        }
        foreach ($match in [regex]::Matches($raw, '[\x20-\x7e]{6,}')) { $strings += $match.Value }
        foreach ($text in ($strings | Select-Object -Unique)) {
            if ($text -match $pattern) {
                $line = $text.Trim()
                if ($line.Length -gt 140) { $line = $line.Substring(0, 140) + '...' }
                $found += "  $($file) (a string in it): $line"
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
    throw ("These would ship in a zip and say how a mod was tested, or that it wasn't, which belongs only in the project's docs (cards B07 and B10), or name this computer's user folder. Take them out:`n" + ($found -join "`n"))
}
