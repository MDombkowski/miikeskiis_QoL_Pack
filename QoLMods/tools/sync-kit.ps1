# Copies the kit (this folder's kit\*.cs, the one copy to edit) into every module's own kit\ folder, so each mod's
# single release builds from exactly the same kit as the bundle. The modules are the folders named in QoLMods.csproj's
# Compile lines. Run it after changing the kit, then build the bundle and every single release.
# The parts (parts\<Part>\*.cs, such as the chest service, parts\Chests) are kept the same way, but only in the modules
# that use them: a module uses a part when it has a folder parts\<Part>\ of its own (MODULES.md, "Parts").
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\sync-kit.ps1           copy the kit and the parts to every module
#   powershell -ExecutionPolicy Bypass -File tools\sync-kit.ps1 -Check    only check; fails if any copy differs
# Line endings don't count as a difference (git may store and check out either).
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$bundleFolder = Split-Path -Parent $PSScriptRoot
$valheimFolder = Split-Path -Parent $bundleFolder
$kit = Join-Path $bundleFolder 'kit'
$parts = Join-Path $bundleFolder 'parts'

function Text([string]$path) { ([System.IO.File]::ReadAllText($path)) -replace "`r`n", "`n" }

$project = Get-Content -Raw (Join-Path $bundleFolder 'QoLMods.csproj')
# Every Compile line must name a module's src\ folder in exactly this shape, or a module would be skipped silently.
$includes = @([regex]::Matches($project, '<Compile\s+Include="([^"]*)"') | ForEach-Object { $_.Groups[1].Value })
$odd = @($includes | Where-Object { $_ -notmatch '^\.\.\\[A-Za-z0-9_]+\\src\\' })
if ($odd.Count -gt 0) { throw "QoLMods.csproj has Compile lines not in the shape ..\<Mod>\src\...: $($odd -join '; ')" }
$modules = @($includes | ForEach-Object { ($_ -split '\\')[1] } | Select-Object -Unique)
if ($modules.Count -eq 0) { throw 'QoLMods.csproj names no module folders.' }

$problems = @()

# Makes $target hold exactly the .cs files of $source (copying, or with -Check only reporting); $label names it.
function Sync-Folder([string]$source, [string]$target, [string]$label) {
    $sourceFiles = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File)
    if (-not $Check) { New-Item -ItemType Directory -Force $target | Out-Null }
    foreach ($file in $sourceFiles) {
        $copy = Join-Path $target $file.Name
        $same = (Test-Path -LiteralPath $copy) -and ((Text $copy) -ceq (Text $file.FullName))   # -ceq: C# is case-sensitive
        if ($same) { continue }
        if ($Check) { $script:problems += "$label\$($file.Name) differs from its source or is missing" }
        else { Copy-Item -LiteralPath $file.FullName -Destination $copy; "Copied $($file.Name) to $label" }
    }
    # A file in the copy that the source no longer has.
    if (Test-Path -LiteralPath $target) {
        foreach ($extra in @(Get-ChildItem -LiteralPath $target -Filter '*.cs' -File | Where-Object { -not (Test-Path -LiteralPath (Join-Path $source $_.Name)) })) {
            if ($Check) { $script:problems += "$label\$($extra.Name) is not in its source" }
            else { Remove-Item -LiteralPath $extra.FullName; "Removed $($extra.Name) from $label (no longer in its source)" }
        }
    }
}

$usesParts = @()
foreach ($module in $modules) {
    $moduleFolder = Join-Path $valheimFolder $module
    Sync-Folder $kit (Join-Path $moduleFolder 'kit') "$module\kit"
    $own = Join-Path $moduleFolder 'parts'
    if (Test-Path -LiteralPath $own) {
        foreach ($part in @(Get-ChildItem -LiteralPath $own -Directory)) {
            $source = Join-Path $parts $part.Name
            if (-not (Test-Path -LiteralPath $source)) { $problems += "$module\parts\$($part.Name) names no part: there is no QoLMods\parts\$($part.Name)"; continue }
            Sync-Folder $source $part.FullName "$module\parts\$($part.Name)"
            $usesParts += "$module ($($part.Name))"
        }
    }
}
if ($problems.Count -gt 0) { throw ("The kit or part copies are out of step; run tools\sync-kit.ps1:`n  " + ($problems -join "`n  ")) }
"The kit is the same in the bundle and in: $($modules -join ', ')"
if ($usesParts.Count -gt 0) { "The parts are the same in the bundle and in: $($usesParts -join ', ')" }
