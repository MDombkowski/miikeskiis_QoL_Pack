// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
using System;
using BepInEx.Configuration;

namespace QoLMods.Kit
{
    /// <summary>
    /// Display hints for configuration managers, including shudnal's ConfigurationManager (F1). They recognise a
    /// setting's tag object by this exact class name and copy its public fields by name, so the class must keep this
    /// name and these field names. Only the fields the kit uses are declared: the name shown for a setting, its place
    /// within its section (a higher Order is listed first), a drawer of the module's own for its value, and (since kit
    /// 2026-09-28, for Map View Rules' fold-outs) the heading it is listed under, whether it is listed, and whether its
    /// name and reset button show. A field left null leaves the manager's own choice.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;

        public int? Order;

        /// <summary>Draws the setting's value in the manager's window instead of its own field, with Unity's
        /// immediate-mode GUI (GUILayout), given the setting. Null for the manager's own field. Since kit 2026-09-27, for
        /// Map View Rules' "use current map zoom" buttons.</summary>
        public Action<ConfigEntryBase> CustomDrawer;

        // The fields below are set only by the modules that use them (so a mod that doesn't would warn "never
        // assigned"), and read by configuration managers through reflection.
#pragma warning disable CS0649
        /// <summary>The heading the manager lists the setting under, instead of its section's name; the settings file
        /// keeps its section. Null: its section.</summary>
        public string Category;

        /// <summary>False: the manager doesn't list the setting. shudnal's ConfigurationManager reads it again while its
        /// window is open (so a module can change it then); BepInEx's own reads it as its window opens. Once set, set it
        /// back to true, not null, to list the setting again: a manager keeps the last true or false it read.</summary>
        public bool? Browsable;

        /// <summary>True: the setting's name isn't shown, so its CustomDrawer has the whole row.</summary>
        public bool? HideSettingName;

        /// <summary>True: no button to put the setting back to its default beside it.</summary>
        public bool? HideDefaultButton;
#pragma warning restore CS0649
    }
}
