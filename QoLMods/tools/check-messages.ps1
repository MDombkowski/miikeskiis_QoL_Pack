# Refuses a built DLL whose code shows a message on screen any way but through the kit's QoLMods.Kit.Say, which fits
# every message to the screen (card B14). The game's message texts never wrap, so a message longer than the screen is
# wide ran off both sides of it: StoreStash's messages were fitted on 2026-10-09 (card A82), the other modules' weren't,
# and Topper-Upper's linking message ran off his screen on 2026-10-10. His word: "This should be caught without me
# having to catch it." So the check reads the compiled code itself (every method, its lambdas too), not the source:
# anywhere outside QoLMods.Kit.Say, a call to MessageHud.ShowMessage, MessageAll or QueueUnlockMsg, or to a creature's
# or player's Message (Character, Humanoid or Player), a use of the HUD's own message texts (m_messageCenterText,
# m_messageText), or of the kit's unfitted Say.Send, is refused, named by its type and method.
#
#   powershell -ExecutionPolicy Bypass -File tools\check-messages.ps1 <dll> [<dll> ...] [-ModProfile <profile folder>]
#
# It reads the DLL with Mono.Cecil, which ships with BepInEx (<profile>\BepInEx\core\Mono.Cecil.dll; the bundle compiles
# against the same one for Spare Keys). The bundle's package.ps1 runs it over the built QoLMods.dll before it packs.
[CmdletBinding(PositionalBinding = $false)]
param([Parameter(Position = 0, ValueFromRemainingArguments = $true)][string[]]$Dlls, [string]$ModProfile)
$ErrorActionPreference = 'Stop'

if (-not $ModProfile) { $ModProfile = Join-Path $env:APPDATA 'Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default' }
$cecil = Join-Path $ModProfile 'BepInEx\core\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecil)) { throw "check-messages: Mono.Cecil isn't at $cecil; pass -ModProfile <the mod profile folder>." }
Add-Type -LiteralPath $cecil
if (-not $Dlls -or $Dlls.Count -eq 0) { throw 'check-messages: name the DLL to check.' }

# The kit's own type, the only one allowed to show a message.
$allowed = 'QoLMods.Kit.Say'

# A call that shows a message: MessageHud's ShowMessage, MessageAll or QueueUnlockMsg, or Message on the game's
# Character, Humanoid or Player.
function Shows-Message($method) {
    if ($null -eq $method -or $null -eq $method.DeclaringType) { return $false }
    $type = $method.DeclaringType.FullName
    if ($type -eq 'MessageHud' -and @('ShowMessage', 'MessageAll', 'QueueUnlockMsg') -contains $method.Name) { return $true }
    if ($method.Name -eq 'Message' -and @('Character', 'Humanoid', 'Player') -contains $type) { return $true }
    return $false
}

# A field that bypasses the fitting (review m6): the HUD's own message texts (writing one shows text unfitted), and the
# kit's Say.Send (it sends a message as it is: only Say itself fits first).
function Bypasses-Fitting($field) {
    if ($null -eq $field -or $null -eq $field.DeclaringType) { return $false }
    $type = $field.DeclaringType.FullName
    if ($type -eq 'MessageHud' -and @('m_messageCenterText', 'm_messageText') -contains $field.Name) { return $true }
    if ($type -eq $allowed -and $field.Name -eq 'Send') { return $true }
    return $false
}

# The outermost type a type sits in (a lambda's closure class sits in the class that wrote the lambda).
function Outermost($type) {
    while ($null -ne $type.DeclaringType) { $type = $type.DeclaringType }
    return $type
}

# Every type of the module, nested ones too.
function All-Types($types) {
    foreach ($type in $types) {
        $type
        if ($type.HasNestedTypes) { All-Types $type.NestedTypes }
    }
}

$found = @()
$calls = 0
foreach ($dll in $Dlls) {
    $path = (Resolve-Path -LiteralPath $dll).Path
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($path)
    try {
        foreach ($type in (All-Types $module.Types)) {
            $owner = (Outermost $type).FullName
            foreach ($method in $type.Methods) {
                if (-not $method.HasBody) { continue }
                foreach ($instruction in $method.Body.Instructions) {
                    $code = $instruction.OpCode.Code.ToString()
                    if (@('Call', 'Callvirt', 'Ldftn', 'Ldvirtftn') -contains $code) {
                        if (-not (Shows-Message $instruction.Operand)) { continue }
                        $calls++
                        if ($owner -eq $allowed) { continue }
                        $found += "  $([System.IO.Path]::GetFileName($path)): $($type.FullName).$($method.Name) calls $($instruction.Operand.DeclaringType.FullName).$($instruction.Operand.Name)"
                    } elseif (@('Ldfld', 'Ldflda', 'Stfld', 'Ldsfld', 'Ldsflda', 'Stsfld') -contains $code) {
                        if (-not (Bypasses-Fitting $instruction.Operand) -or $owner -eq $allowed) { continue }
                        $found += "  $([System.IO.Path]::GetFileName($path)): $($type.FullName).$($method.Name) uses $($instruction.Operand.DeclaringType.FullName).$($instruction.Operand.Name)"
                    }
                }
            }
        }
    } finally {
        $module.Dispose()
    }
}
if ($found.Count -gt 0) {
    throw ("A message would be shown without being fitted to the screen (card B14). Show it through QoLMods.Kit.Say " +
           "(Say.Center, Say.TopLeft or Say.To) instead:`n" + ($found -join "`n"))
}
"Every message is shown through the kit's Say, fitted to the screen ($calls call(s) to the game's message methods, all inside $allowed)."
