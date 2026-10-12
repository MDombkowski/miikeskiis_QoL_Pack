namespace Outfitter.Equip
{
    /// <summary>
    /// Whether what he wears comes off when a loadout goes on (card B18, his feedback of 2026-10-11: "It needs to unequip
    /// equipped items when I do the switch to a new loadout ... especially pesky for arrows, which can be equipped even
    /// though I don't have a weapon in-hand"). The game's own EquipItem takes off only what sits in the slot the new piece
    /// goes to, and keeps arrows equipped with no bow in hand, so whatever the loadout doesn't wear stays on unless Outfitter
    /// takes it off. Until 0.6.0 Outfitter took such a piece off only when a chest took it or the loadout put something in its
    /// place (card A34, decisions 37 and 47); "What I wear that the loadout doesn't" (ComesOff, the default) takes it off
    /// either way, keeping it in his inventory. What never goes away (a favourite left alone, a quick-slot item in Fully
    /// replace, a stand's own piece, what a swap mode doesn't change) is not touched here and stays as it was. Plain
    /// booleans only, so the scratch harness reaches it.
    /// </summary>
    internal static class ComesOff
    {
        /// <summary>The chests' part (Exchange): whether a piece the plan takes off comes off now. It does when it doesn't go
        /// away (it stays with him, or the loadout carries it unworn), when a chest takes it, when the loadout puts something
        /// in its place, and, with ComesOff, always.</summary>
        internal static bool InExchange(bool goesAway, bool routed, bool replaced, bool offWithoutChest) =>
            !goesAway || routed || replaced || offWithoutChest;

        /// <summary>The chests' part: whether a piece that came off to go away, and is still with him afterwards (no chest
        /// took it, or took only part of a stack of arrows), goes back on where nothing else went (decision 47). With
        /// ComesOff only a piece the loadout replaces does (what was to replace it didn't come: as "Keep wearing mine"); 0.5's
        /// way, every one.</summary>
        internal static bool BackOnIfStillHere(bool goesAway, bool replaced, bool offWithoutChest) =>
            goesAway && (replaced || !offWithoutChest);
    }
}
