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

        // Each set only by some of the mods carrying the part, so another's single release never sets it: the compiler's
        // warning is silenced, as the kit's hints' are.
#pragma warning disable CS0649
        /// <summary>Only chests he built himself. Otherwise every chest he could open by hand.</summary>
        internal bool OnlyBuiltByMe;

        /// <summary>Leave one of each kind of item that stacks in every chest, so a quick stack still knows where it belongs.
        /// A lone piece of gear (anything that doesn't stack) is never kept back, unless LeaveOneOfGear.</summary>
        internal bool LeaveOne;

        /// <summary>With LeaveOne, keep back the last piece of gear too (anything that doesn't stack: armour, weapons,
        /// tools, shields), one of each in every chest, as for things that stack. Off by default, the service's rule since
        /// its start; only Add to Cart sets it, from its setting "Leave one of gear too" (card A83).</summary>
        internal bool LeaveOneOfGear;

        /// <summary>Chests to leave alone besides those. May be null.</summary>
        internal Func<Container, bool> Skip = null;

        /// <summary>Chests carrying Reserve Stock's mark (ChestReserve) count, give and take like any other. Off by default,
        /// and then a reserved chest is never taken from or put into and is counted apart. Craft from Chests turns it on
        /// when its setting "Reserved chests" says crafting may use them (card A12, his answer 5's third choice), and
        /// StoreStash for Place stacks and holding E, a choice made by hand at that one chest (card A52).</summary>
        internal bool UseReserved;
#pragma warning restore CS0649
    }
}
