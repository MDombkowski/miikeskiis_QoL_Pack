# Builds the mod in Release and packs it for Thunderstore Mod Manager (Settings > "Import local mod from file"):
#   dist\drumcowski-XPortalMapPicker-<version>.zip
# holding manifest.json, README.md, CHANGELOG.md, CREDITS.md, icon.png and XPortalMapPicker.dll, all at the zip's root.
# This is the single release. The bundle, miikeskii's QoL Pack, is packed by ..\QoLMods\tools\package.ps1, which
# runs this script too.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-ValheimManaged <valheim_Data\Managed>] [-ModProfile <profile folder>]
# Nothing is downloaded. It writes only the build output (bin\, obj\), dist\, and icon.png if that is missing.
param([string]$ValheimManaged, [string]$ModProfile)
$ErrorActionPreference = 'Stop'
$modFolder = Split-Path -Parent $PSScriptRoot

# Keep the .NET command line offline: no telemetry, no update checks.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'

# The package. Thunderstore Mod Manager's local import needs an "author" field and reads Author-Name-Version from the
# file name. "drumcowski" is the author he gave 0.2.0 when he imported it, so a newer zip replaces that install
# instead of landing beside it (was "ModProjects" until 0.3.0). A real Thunderstore upload would drop the author
# field, because Thunderstore takes the author from the team that uploads the package.
$author = 'drumcowski'
$name = 'XPortalMapPicker'
$description = 'Adds a Map button beside the XPortal destination list: choose where a portal leads by clicking it on the big map. Client-side only; the server and other players do not need it.'
$dependencies = @('denikson-BepInExPack_Valheim-5.4.2350', 'ValheimModding-Jotunn-2.30.2', 'SpikeHimself-XPortal-1.2.25')

# One version number in three places: the project file, the module and the changelog must agree.
$project = [xml](Get-Content -Raw (Join-Path $modFolder 'XPortalMapPicker.csproj'))
$version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
$moduleSource = Get-Content -Raw (Join-Path $modFolder 'src\Module.cs')
if ($moduleSource -notmatch 'const string Version = "([^"]+)"') { throw 'src\Module.cs has no Version constant.' }
if ($Matches[1] -ne $version) { throw "Version mismatch: XPortalMapPicker.csproj says $version, src\Module.cs says $($Matches[1])." }
if (-not (Select-String -Path (Join-Path $modFolder 'CHANGELOG.md') -Pattern "^## $([regex]::Escape($version))\b" -Quiet)) {
    throw "CHANGELOG.md has no entry for $version."
}
if ($description.Length -gt 250) { throw "The description is $($description.Length) characters long; Thunderstore allows 250." }

$buildArguments = @('build', (Join-Path $modFolder 'XPortalMapPicker.csproj'), '-c', 'Release', '-nologo')
if ($ValheimManaged) { $buildArguments += "-p:ValheimManaged=$ValheimManaged" }
if ($ModProfile) { $buildArguments += "-p:ModProfile=$ModProfile" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'The build failed, so nothing was packed.' }

$icon = Join-Path $modFolder 'icon.png'
if (-not (Test-Path $icon)) { & (Join-Path $PSScriptRoot 'make-icon.ps1') -OutFile $icon }

$manifest = [ordered]@{
    name           = $name
    author         = $author
    version_number = $version
    website_url    = ''
    description    = $description
    dependencies   = $dependencies
}
$utf8 = New-Object System.Text.UTF8Encoding($false)   # no byte-order mark: the mod manager reads the JSON as it is
$contents = [ordered]@{
    'manifest.json'        = $utf8.GetBytes(($manifest | ConvertTo-Json))
    'README.md'            = [System.IO.File]::ReadAllBytes((Join-Path $modFolder 'README.md'))
    'CHANGELOG.md'         = [System.IO.File]::ReadAllBytes((Join-Path $modFolder 'CHANGELOG.md'))
    'CREDITS.md'           = [System.IO.File]::ReadAllBytes((Join-Path $modFolder 'CREDITS.md'))
    'icon.png'             = [System.IO.File]::ReadAllBytes($icon)
    'XPortalMapPicker.dll' = [System.IO.File]::ReadAllBytes((Join-Path $modFolder 'bin\Release\net472\XPortalMapPicker.dll'))
}

Add-Type -AssemblyName System.IO.Compression
$dist = Join-Path $modFolder 'dist'
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
"Packed $zipPath"
