# Builds the bundle in Release and packs it for Thunderstore: the zip he uploads there (thunderstore.io/package/create),
# which a mod manager can also import from a file (Settings > "Import local mod from file"):
#   dist\miikeskii-miikeskiis_QoL_Pack-<version>.zip
# holding manifest.json, README.md, CHANGELOG.md, CREDITS.md (every module's credits, gathered), LICENSE (GPL-3.0),
# icon.png and QoLMods.dll, all at the zip's root; never the source (Thunderstore's moderators refuse it). With
# -SourceTo, it mirrors the source into the public source repository's folder. Then, unless -BundleOnly, it runs every
# module's own tools\package.ps1, so each single release's zip is built from the same sources at the same time.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-BundleOnly] [-SourceTo <source repository folder>] [-ValheimManaged <valheim_Data\Managed>] [-ModProfile <profile folder>]
# Nothing is downloaded, and nothing is pushed. It writes only the build output (bin\, obj\), dist\, icon.png if that is
# missing, the same in each module's folder for the single releases, and with -SourceTo that folder.
param([string]$ValheimManaged, [string]$ModProfile, [switch]$BundleOnly, [string]$SourceTo)
$ErrorActionPreference = 'Stop'
$bundleFolder = Split-Path -Parent $PSScriptRoot
$valheimFolder = Split-Path -Parent $bundleFolder

# Keep the .NET command line offline: no telemetry, no update checks.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'

# The package. Author and name are what his friends' mod manager keys the import on: a later zip with the same two
# replaces the installed one, and a Thunderstore upload by the team of that name is offered as its update. At his word
# (card A30), drumcowski-QoL_Mods became miikeskii-QoL_Pack for 0.6.3, whose Thunderstore listing was rejected; then,
# so that his name is in the title Thunderstore shows (the package name with its underscores as spaces; no
# apostrophe allowed, so "miikeskii's QoL Pack" can't be it), miikeskii-miikeskiis_QoL_Pack from 0.6.4. A mod
# manager takes a new name for a new mod, so the old one is removed first. From here on they stay.
$author = 'miikeskii'
$name = 'miikeskiis_QoL_Pack'
# Only what the bundle itself needs. Other authors' mods a module works with are optional: without one, only that
# module switches off, so they are named in the README instead.
$dependencies = @('denikson-BepInExPack_Valheim-5.4.2350', 'ValheimModding-Jotunn-2.30.2')

# The kit must be the same everywhere, or a single release would behave differently from its module in the bundle.
& (Join-Path $PSScriptRoot 'sync-kit.ps1') -Check

# One version number in three places: the project file, the plugin and the changelog must agree.
$projectText = Get-Content -Raw (Join-Path $bundleFolder 'QoLMods.csproj')
$project = [xml]$projectText
$version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
$pluginSource = Get-Content -Raw (Join-Path $bundleFolder 'src\Plugin.cs')
if ($pluginSource -notmatch 'const string Version = "([^"]+)"') { throw 'src\Plugin.cs has no Version constant.' }
if ($Matches[1] -ne $version) { throw "Version mismatch: QoLMods.csproj says $version, src\Plugin.cs says $($Matches[1])." }
if (-not (Select-String -Path (Join-Path $bundleFolder 'CHANGELOG.md') -Pattern "^## $([regex]::Escape($version))\b" -Quiet)) {
    throw "CHANGELOG.md has no entry for $version."
}

# The modules, from the project's Compile lines (sync-kit.ps1 has just checked their shape), with each one's name and
# version from its src\Module.cs, which must agree with its own project file and changelog.
$modules = @()
foreach ($folder in @([regex]::Matches($projectText, '<Compile\s+Include="\.\.\\([^\\"]+)\\src\\') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)) {
    $moduleFolder = Join-Path $valheimFolder $folder
    $moduleSource = Get-Content -Raw (Join-Path $moduleFolder 'src\Module.cs')
    if ($moduleSource -notmatch 'const string Name = "([^"]+)"') { throw "$folder\src\Module.cs has no Name constant." }
    $moduleName = $Matches[1]
    if ($moduleSource -notmatch 'const string Version = "([^"]+)"') { throw "$folder\src\Module.cs has no Version constant." }
    $moduleVersion = $Matches[1]
    $moduleProject = [xml](Get-Content -Raw (Join-Path $moduleFolder "$folder.csproj"))
    $projectVersion = @($moduleProject.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    if ($projectVersion -ne $moduleVersion) { throw "Version mismatch: $folder.csproj says $projectVersion, $folder\src\Module.cs says $moduleVersion." }
    if (-not (Select-String -Path (Join-Path $moduleFolder 'CHANGELOG.md') -Pattern "^## $([regex]::Escape($moduleVersion))\b" -Quiet)) {
        throw "$folder\CHANGELOG.md has no entry for $moduleVersion."
    }
    $modules += [pscustomobject]@{ Folder = $folder; Name = $moduleName; Version = $moduleVersion }
}
if ($modules.Count -eq 0) { throw 'QoLMods.csproj names no module folders.' }
$inside = ($modules | ForEach-Object { "$($_.Name) $($_.Version)" }) -join ', '
# The plugin's module list, without // comments, so a commented-out line doesn't count.
$pluginCode = (($pluginSource -split "`n") | ForEach-Object { $_ -replace '//.*$', '' }) -join "`n"
foreach ($module in $modules) {
    if ($pluginCode -notmatch [regex]::Escape("new $($module.Folder).Module()")) { throw "src\Plugin.cs doesn't list the module $($module.Folder), which QoLMods.csproj compiles." }
    if (-not (Select-String -Path (Join-Path $bundleFolder 'CHANGELOG.md') -Pattern ([regex]::Escape("$($module.Name) $($module.Version)")) -Quiet)) {
        throw "The bundle's CHANGELOG.md doesn't name $($module.Name) $($module.Version); say in its entry which versions are inside."
    }
}

# Thunderstore refuses a README.md or CHANGELOG.md that isn't UTF-8, starts with a byte-order mark or runs past 100,000 characters
# (its upload checks, read in its code on 2026-09-27). PowerShell's Out-File and some editors add the mark.
foreach ($doc in @('README.md', 'CHANGELOG.md')) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $bundleFolder $doc))
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$doc starts with a byte-order mark, which Thunderstore refuses: save it as UTF-8 without one." }
    try { $text = (New-Object System.Text.UTF8Encoding($false, $true)).GetString($bytes) } catch { throw "$doc isn't valid UTF-8, which Thunderstore refuses: save it as UTF-8 without a byte-order mark." }
    if ($text.Length -gt 100000) { throw "$doc is longer than Thunderstore's 100,000 characters." }
}

# Thunderstore asks an AI-made mod to say so in its README and in its project's AssemblyMetadata (its wiki page "LLMs
# and AI-generated files", read 2026-09-27; his order: the package follows the policies). Refuse to pack without both.
$readmePath = Join-Path $bundleFolder 'README.md'
if (-not (Select-String -Path $readmePath -Pattern 'Made with AI' -SimpleMatch -Quiet) -or -not (Select-String -Path $readmePath -Pattern 'Claude' -SimpleMatch -Quiet)) {
    throw "README.md doesn't say it's made with AI, and by which AI (Claude), which Thunderstore asks of AI-made mods."
}
if ($projectText -notmatch 'AssemblyMetadata\s+Include="AI_Assisted_Creation"') { throw "QoLMods.csproj has no AI_Assisted_Creation AssemblyMetadata, which Thunderstore asks of AI-made mods." }

# What a mod manager lists under the title: the pack's own name (the title can't hold its apostrophe), then his own
# words from the README's opening (2026-09-27), then the mods inside, by name only, so there is room for more; their
# versions are in the README and the changelog.
$names = ($modules | ForEach-Object { $_.Name }) -join ', '
$description = "miikeskii's QoL Pack. Made with AI. A collection of Valheim quality-of-life mods, client-side, with an on/off switch for each. Inside: $names."
if ($description.Length -gt 250) { throw "The description is $($description.Length) characters long; Thunderstore allows 250. Shorten its wording." }

$buildArguments = @('build', (Join-Path $bundleFolder 'QoLMods.csproj'), '-c', 'Release', '-nologo')
if ($ValheimManaged) { $buildArguments += "-p:ValheimManaged=$ValheimManaged" }
if ($ModProfile) { $buildArguments += "-p:ModProfile=$ModProfile" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'The build failed, so nothing was packed.' }

$icon = Join-Path $bundleFolder 'icon.png'
if (-not (Test-Path $icon)) { & (Join-Path $PSScriptRoot 'make-icon.ps1') -OutFile $icon }

# Every module's CREDITS.md, gathered under one heading each (their own headings one level down).
$credits = New-Object System.Text.StringBuilder
[void]$credits.AppendLine("# Credits: miikeskii's QoL Pack $version")
[void]$credits.AppendLine()
[void]$credits.AppendLine("What each mod in this bundle borrows, from whom, and under which licence: each mod's own CREDITS.md, gathered here when the bundle was packed. Inside: $inside.")
foreach ($module in $modules) {
    $file = Join-Path $valheimFolder "$($module.Folder)\CREDITS.md"
    if (-not (Test-Path $file)) { throw "$($module.Folder) has no CREDITS.md." }
    [void]$credits.AppendLine()
    foreach ($line in (Get-Content -Encoding UTF8 $file)) {
        if ($line -match '^#') { $line = '#' + $line }
        [void]$credits.AppendLine($line)
    }
}

# The pack is GPL-3.0 (his pick, 2026-09-27). Thunderstore's moderators rejected 0.6.3 for carrying its source
# ("Please remove the source files"), so the package carries the licence only, and the source lives in a public
# repository that the README and the manifest's website_url point to (the GPL allows the source on another server, with
# directions beside the download). -SourceTo <that repository's folder> mirrors the source there: this folder and every
# module's, side by side as here, without build output, with LICENSE and a README at its root. A file naming this
# computer's user, user folder or git email stops it: the repository is public.
$license = Join-Path $bundleFolder 'LICENSE'
if (-not (Test-Path $license)) { throw 'LICENSE is missing: the pack is GPL-3.0 (the text from gnu.org/licenses/gpl-3.0.txt).' }
$sourceUrl = 'https://github.com/MDombkowski/miikeskiis_QoL_Pack'
if (-not (Select-String -Path (Join-Path $bundleFolder 'README.md') -Pattern $sourceUrl -SimpleMatch -Quiet)) { throw "README.md doesn't say where the source is ($sourceUrl), which the GPL asks for." }
if ($SourceTo) {
    if (-not (Test-Path -LiteralPath (Join-Path $SourceTo '.qol-pack-source')) -or -not (Test-Path -LiteralPath (Join-Path $SourceTo '.git'))) {
        throw "$SourceTo isn't the source repository's folder: it needs its .git folder and the marker file .qol-pack-source."
    }
    $private = @([regex]::Escape($env:USERPROFILE), "\b$([regex]::Escape($env:USERNAME))\b")
    $email = ''
    try { $email = (& git config user.email) } catch { }
    if ($email) { $private += [regex]::Escape($email.Trim()) }
    # Every file is checked before anything in the repository's folder changes.
    $copies = @()
    foreach ($folder in @((Split-Path -Leaf $bundleFolder)) + @($modules | ForEach-Object { $_.Folder })) {
        $root = Join-Path $valheimFolder $folder
        $files = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName.Substring($root.Length + 1) -notmatch '^(bin|obj|dist)\\' } | Sort-Object FullName
        foreach ($file in $files) {
            $relative = Join-Path $folder $file.FullName.Substring($root.Length + 1)
            $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($file.FullName))
            foreach ($pattern in $private) {
                if ($text -match $pattern) { throw "$relative names this computer's user, user folder or git email; take it out before the source goes public." }
            }
            $copies += ,@($file.FullName, (Join-Path $SourceTo $relative))
        }
    }
    # Then everything there but .git and the marker goes, and the source comes back fresh, so a file deleted here goes
    # there too.
    Get-ChildItem -LiteralPath $SourceTo -Force | Where-Object { $_.Name -notin @('.git', '.qol-pack-source') } | Remove-Item -Recurse -Force
    foreach ($copy in $copies) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $copy[1]) | Out-Null
        Copy-Item -LiteralPath $copy[0] -Destination $copy[1]
    }
    Copy-Item -LiteralPath $license -Destination (Join-Path $SourceTo 'LICENSE')
    $sourceReadme = @'
# miikeskii's QoL Pack: the source

The source of **miikeskii's QoL Pack**, Valheim quality-of-life mods for BepInEx 5 and Jotunn, installed from Thunderstore as [miikeskiis QoL Pack](https://thunderstore.io/c/valheim/p/miikeskii/miikeskiis_QoL_Pack/). This is the source of its version VERSION; to play it, install it from Thunderstore.

- `QoLMods/`: the bundle (its plugin, the kit that runs each mod as a module, and the parts several mods share). `QoLMods/README.md` describes the pack, and `QoLMods/MODULES.md` says how to build it ("Building and packing the bundle").
- `AddToCart/`, `MapZoom/`, `TraderCircles/` and `XPortalMapPicker/`: each mod's own code, compiled into the bundle from there.

**Made with AI.** The code was written by Claude, Anthropic's AI (the Claude Opus 5 and Opus 5.5 models, working as Claude Code agents), for miikeskii.

**Licence:** GPL-3.0, in `LICENSE`.
'@
    [System.IO.File]::WriteAllText((Join-Path $SourceTo 'README.md'), ($sourceReadme -replace 'VERSION', $version), (New-Object System.Text.UTF8Encoding($false)))
    "Mirrored the source of $version into $SourceTo ($($copies.Count) files, with LICENSE and README.md)"
}

# Thunderstore reads only its own keys (name, version_number, website_url, description, dependencies) and takes the
# author from the team that uploads; "author" is for a mod manager's import from a file. website_url is the source.
$manifest = [ordered]@{
    name           = $name
    author         = $author
    version_number = $version
    website_url    = $sourceUrl
    description    = $description
    dependencies   = $dependencies
}
$utf8 = New-Object System.Text.UTF8Encoding($false)   # no byte-order mark: the mod manager reads the JSON as it is
$contents = [ordered]@{
    'manifest.json' = $utf8.GetBytes(($manifest | ConvertTo-Json))
    'README.md'     = [System.IO.File]::ReadAllBytes((Join-Path $bundleFolder 'README.md'))
    'CHANGELOG.md'  = [System.IO.File]::ReadAllBytes((Join-Path $bundleFolder 'CHANGELOG.md'))
    'CREDITS.md'    = $utf8.GetBytes($credits.ToString())
    'LICENSE'       = [System.IO.File]::ReadAllBytes($license)
    'icon.png'      = [System.IO.File]::ReadAllBytes($icon)
    'QoLMods.dll'   = [System.IO.File]::ReadAllBytes((Join-Path $bundleFolder 'bin\Release\net472\QoLMods.dll'))
}

Add-Type -AssemblyName System.IO.Compression
$dist = Join-Path $bundleFolder 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zipPath = Join-Path $dist "$author-$name-$version.zip"
$stream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)   # replaces an older zip of the same name
try {
    $zip = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entryName in $contents.Keys) {
            $bytes = $contents[$entryName]
            $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $writer = $entry.Open()
            try { $writer.Write($bytes, 0, $bytes.Length) } finally { $writer.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}
finally { $stream.Dispose() }
"Packed $zipPath ($inside)"
"Thunderstore will list it as: $($name -replace '_', ' '), by $author"

if (-not $BundleOnly) {
    foreach ($module in $modules) {
        $single = Join-Path $valheimFolder "$($module.Folder)\tools\package.ps1"
        $singleArguments = @{}
        if ($ValheimManaged) { $singleArguments.ValheimManaged = $ValheimManaged }
        if ($ModProfile) { $singleArguments.ModProfile = $ModProfile }
        & $single @singleArguments
    }
}
