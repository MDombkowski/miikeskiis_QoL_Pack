// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
namespace QoLMods.Chests
{
    /// <summary>
    /// The reserve mark of Reserve Stock (card A12): a chest carrying it is left alone by every mod of the bundle that
    /// takes from or counts chests. The mark lives in the chest's own network record, so the unmodded server keeps it
    /// with the world and every player nearby receives it. The chest service honours it from its first version: it never
    /// takes from a marked chest (checked again once the chest is handed over, and at the take itself, ChestStock.Take and
    /// Remove), unless a module's rules use reserved chests like any other (ChestRules.UseReserved), and it counts marked
    /// chests apart. Reserve Stock writes it, always as the chest's owner (ReserveStock\src\Mark.cs).
    /// </summary>
    internal static class ChestReserve
    {
        /// <summary>The mark's name in the chest's record: true while reserved. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.qolmods.reserved";

        internal static bool IsReserved(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() && view.GetZDO().GetBool(Key);
        }
    }
}
