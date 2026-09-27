// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;

namespace QoLMods.Chests
{
    /// <summary>
    /// Which chests a module may use, and how much of each it may take. The service has no settings of its own: each
    /// module makes these from its own settings, each time it asks, so a changed setting counts at once.
    /// </summary>
    internal sealed class ChestRules
    {
        /// <summary>How far from the player a chest may be, in metres. Only chests in his loaded area exist on his
        /// computer at all (about 128 to 190 m at the game's default settings), so a larger range finds no more.</summary>
        internal float Range = 20f;

        /// <summary>Only chests he built himself. Otherwise every chest he could open by hand.</summary>
        internal bool OnlyBuiltByMe;

        /// <summary>Leave one of each kind of item in every chest, so a quick stack still knows where it belongs.</summary>
        internal bool LeaveOne;

        /// <summary>Chests to leave alone besides those, such as reserved chests (card A12). May be null.</summary>
        internal Func<Container, bool> Skip = null;
    }
}
