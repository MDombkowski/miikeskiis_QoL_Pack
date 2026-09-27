// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
namespace QoLMods.Kit
{
    /// <summary>
    /// The bundle's identity, known to every module's single release so it can stand aside when the bundle is there.
    /// The ID is permanent: it names the bundle's settings file.
    /// </summary>
    internal static class Bundle
    {
        internal const string Guid = "modprojects.qolmods";

        internal const string Name = "miikeskii's QoL Pack";
    }
}
