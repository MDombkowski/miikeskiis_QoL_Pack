// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
namespace QoLMods.Kit
{
    /// <summary>
    /// Display hints for configuration managers, including shudnal's ConfigurationManager (F1). They recognise a
    /// setting's tag object by this exact class name and copy its public fields by name, so the class must keep this
    /// name and these field names. Only the fields the kit uses are declared: the name shown for a setting, and its
    /// place within its section (a higher Order is listed first).
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;

        public int? Order;
    }
}
