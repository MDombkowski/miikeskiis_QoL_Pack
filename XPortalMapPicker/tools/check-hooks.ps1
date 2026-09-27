# Checks, without running the game, that every member XPortal Map Picker looks up at startup exists
# in the installed DLLs with the expected shape. Run it after every XPortal or Valheim update:
#   powershell -ExecutionPolicy Bypass -File tools\check-hooks.ps1 [-Managed <valheim_Data\Managed>] [-ModProfile <profile folder>]
# It loads the DLLs for inspection only (ReflectionOnly), so no mod or game code runs, and it writes nothing.
# It covers the private and internal members, which the mod finds by name. The public ones it uses directly are
# checked by the compiler: rebuild the mod against the updated game (dotnet build -c Release).
param(
    [string]$Managed    = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed',
    [string]$ModProfile = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default"
)
$ErrorActionPreference = 'Stop'
$managed = $Managed
$prof    = $ModProfile
$dirs = @($managed, "$prof\BepInEx\core", "$prof\BepInEx\plugins\ValheimModding-Jotunn", "$prof\BepInEx\plugins\SpikeHimself-XPortal\XPortal")

[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({
    param($sender, $e)
    $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    foreach ($d in $dirs) {
        $p = Join-Path $d "$name.dll"
        if (Test-Path $p) { return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($p) }
    }
    return [System.Reflection.Assembly]::ReflectionOnlyLoad($e.Name)
})

$xp   = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom("$prof\BepInEx\plugins\SpikeHimself-XPortal\XPortal\XPortal.dll")
$game = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom("$managed\assembly_valheim.dll")
$core = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom("$managed\UnityEngine.CoreModule.dll")
$ui   = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom("$managed\UnityEngine.UI.dll")
$vec2 = $core.GetType('UnityEngine.Vector2')
$vec3 = $core.GetType('UnityEngine.Vector3')
$rawImage = $ui.GetType('UnityEngine.UI.RawImage')
$inst = [System.Reflection.BindingFlags]'Instance, Public, NonPublic'
$stat = [System.Reflection.BindingFlags]'Static, Public, NonPublic'
$fails = 0
function Check($label, $ok, $detail) {
    if ($ok) { "OK    $label  $detail" } else { "FAIL  $label  $detail"; $script:fails++ }
}

$panel = $xp.GetType('XPortal.UI.PortalConfigurationPanel')
Check 'PortalConfigurationPanel type' ($panel -ne $null) ''
$p = $panel.GetProperty('Instance', $stat);                       Check 'panel.Instance (static)' ($p -ne $null -and $p.PropertyType -eq $panel) $p.PropertyType.FullName
$f = $panel.GetField('mainPanel', $inst);                         Check 'panel.mainPanel' ($f.FieldType.FullName -eq 'UnityEngine.GameObject') $f.FieldType.FullName
$f = $panel.GetField('targetPortalDropdown', $inst);              Check 'panel.targetPortalDropdown' ($f.FieldType.FullName -eq 'UnityEngine.UI.Dropdown') $f.FieldType.FullName
$f = $panel.GetField('pingMapButtonObject', $inst);               Check 'panel.pingMapButtonObject' ($f.FieldType.FullName -eq 'UnityEngine.GameObject') $f.FieldType.FullName
$f = $panel.GetField('dropdownIndexToZDOIDMapping', $inst)
$args2 = $f.FieldType.GetGenericArguments()
Check 'panel.dropdownIndexToZDOIDMapping' ($f.FieldType.GetGenericTypeDefinition().FullName -eq 'System.Collections.Generic.Dictionary`2' -and $args2[0] -eq [int] -and $args2[1].FullName -eq 'ZDOID') $f.FieldType.ToString()
$m = $panel.GetMethod('SetActive', $inst, $null, [type[]]@([bool]), $null);            Check 'panel.SetActive(bool)' ($m -ne $null) $m
$m = $panel.GetMethod('InitialiseUI', $inst, $null, [type[]]@(), $null);               Check 'panel.InitialiseUI()' ($m -ne $null) $m
$m = $panel.GetMethod('SetPingMapButtonActive', $inst, $null, [type[]]@([bool]), $null); Check 'panel.SetPingMapButtonActive(bool)' ($m -ne $null) $m
$m = $panel.GetMethod('OnDropdownValueChanged', $inst);           Check 'panel.OnDropdownValueChanged (reached through the list''s listener)' ($m -ne $null) $m

$mgr = $xp.GetType('XPortal.KnownPortalsManager')
$p = $mgr.GetProperty('Instance', $stat);                         Check 'KnownPortalsManager.Instance (static)' ($p -ne $null -and $p.PropertyType -eq $mgr) $p.PropertyType.FullName
$m = $mgr.GetMethod('GetList', $inst, $null, [type[]]@(), $null); Check 'KnownPortalsManager.GetList()' ($m -ne $null -and $m.ReturnType.GetInterface('System.Collections.IEnumerable') -ne $null) $m.ReturnType.ToString()

$kp = $xp.GetType('XPortal.KnownPortal')
foreach ($pair in @(@('Id','ZDOID'), @('Name','System.String'), @('Location','UnityEngine.Vector3'))) {
    $p = $kp.GetProperty($pair[0], $inst); Check "KnownPortal.$($pair[0])" ($p -ne $null -and $p.PropertyType.FullName -eq $pair[1] -and $p.GetGetMethod($true) -ne $null) $p.PropertyType.FullName
}

$cfg = $xp.GetType('XPortal.XPortalConfig')
$p = $cfg.GetProperty('Instance', $stat);                         Check 'XPortalConfig.Instance (static)' ($p -ne $null) ''
$s = $cfg.GetProperty('Server', $inst);                           Check 'XPortalConfig.Server' ($s -ne $null) $s.PropertyType.FullName
$f = $s.PropertyType.GetField('PingMapDisabled', $inst);          Check 'ConfigSettings.PingMapDisabled' ($f -ne $null -and $f.FieldType -eq [bool]) $f.FieldType

$mm = $game.GetType('Minimap')
$m = $mm.GetMethod('OnMapLeftClick', $inst, $null, [type[]]@(), $null);        Check 'Minimap.OnMapLeftClick()' ($m -ne $null) $m
$m = $mm.GetMethod('RemovePinUnderPointer', $inst, $null, [type[]]@(), $null); Check 'Minimap.RemovePinUnderPointer()' ($m -ne $null) $m
$m = $mm.GetMethod('OnMapDblClick', $inst, $null, [type[]]@(), $null);         Check 'Minimap.OnMapDblClick()' ($m -ne $null) $m
$m = $mm.GetMethod('OnMapRightClick', $inst);                                  Check 'Minimap.OnMapRightClick is absent (right click = RemovePinUnderPointer)' ($m -eq $null) ''

# The portal markers: the game's own world-to-map conversion and its explored check.
$m = $mm.GetMethod('WorldToMapPoint', $inst, $null, [type[]]@($vec3, [float].MakeByRefType(), [float].MakeByRefType()), $null)
Check 'Minimap.WorldToMapPoint(Vector3, out float, out float)' ($m -ne $null -and $m.ReturnType -eq [void]) $m
$m = $mm.GetMethod('MapPointToLocalGuiPos', $inst, $null, [type[]]@([float], [float], $rawImage), $null)
Check 'Minimap.MapPointToLocalGuiPos(float, float, RawImage) -> Vector2' ($m -ne $null -and $m.ReturnType -eq $vec2) $m
$m = $mm.GetMethod('IsExplored', $inst, $null, [type[]]@($vec3), $null)
Check 'Minimap.IsExplored(Vector3) -> bool' ($m -ne $null -and $m.ReturnType -eq [bool]) $m
# The other players on the clean map (optional: without it they stay hidden while picking).
$f = $mm.GetField('m_playerPins', $inst)
Check 'Minimap.m_playerPins (List<Minimap.PinData>)' ($f -ne $null -and $f.FieldType.IsGenericType -and $f.FieldType.GetGenericTypeDefinition().FullName -eq 'System.Collections.Generic.List`1' -and $f.FieldType.GetGenericArguments()[0].FullName -eq 'Minimap+PinData') $(if ($f -ne $null) { $f.FieldType.ToString() })
$m = $game.GetType('Game').GetMethod('Shutdown', $inst, $null, [type[]]@([bool]), $null); Check 'Game.Shutdown(bool)' ($m -ne $null) $m

"failures: $fails"
