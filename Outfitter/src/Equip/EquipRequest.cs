using System.Collections.Generic;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Equip
{
    /// <summary>Where what he takes off goes.</summary>
    internal enum TakenOffGoes
    {
        /// <summary>Into the chests nearby, by Outfitter's routing (the chests set up for it first): the window's way.</summary>
        Away,

        /// <summary>It stays in his inventory, listed in <see cref="EquipResult.TakenOff"/>: for a caller that puts it
        /// somewhere itself (a stand hanging his old gear up).</summary>
        StaysWithHim,
    }

    /// <summary>
    /// What to put on, and how: the window's Equip and a Loadout Stand's key each make one and hand it to
    /// <see cref="Outfit.Begin"/>. Every field but <see cref="Loadout"/> may stay as it is for Outfitter's own settings.
    /// </summary>
    internal sealed class EquipRequest
    {
        /// <summary>The loadout to put on (read once, when the equip begins).</summary>
        internal Loadout Loadout;

        /// <summary>The mode; null: the setting "Equip mode".</summary>
        internal EquipMode? Mode;

        /// <summary>What happens when some of it isn't nearby; null: the setting "Missing items".</summary>
        internal MissingItems? Missing;

        /// <summary>The point the chests are found around ("Chest range"); null: where he stands.</summary>
        internal Vector3? Centre;

        /// <summary>Chests within <see cref="NearRange"/> metres of this point are taken from first (a stand's "chests near
        /// the stand first"); null: none first. Putting away follows the quick stack's routing either way (R1, finding 17).</summary>
        internal Vector3? NearFirst;

        internal float NearRange;

        /// <summary>Where what he takes off goes.</summary>
        internal TakenOffGoes TakenOff = TakenOffGoes.Away;

        /// <summary>What he takes off that stays with him whatever <see cref="TakenOff"/> says, piece by piece (a Loadout
        /// Stand taking the pieces of another stand's loadout home, job 30): true keeps it, listed in
        /// <see cref="EquipResult.TakenOff"/>; everything else he takes off goes as <see cref="TakenOff"/> says. Asked of
        /// each worn item at every plan, within one frame. It may turn false between frames, never back to true (the stands
        /// switched off mid-equip: nothing more is kept back for a stand); what it kept in an earlier round stays kept
        /// (PlanInput.KeptEarlier). Null: none.</summary>
        internal System.Func<ItemDrop.ItemData, bool> KeepsTakenOff;

        /// <summary>Items of his that never go away, in any mode (a Loadout Stand's own gear, which came to him for this
        /// equip: what he doesn't end up wearing goes back on the stand, job 30). Like <see cref="KeepsTakenOff"/>, it may
        /// turn false between frames, never back to true. Null: none beyond Outfitter's own rules (favourites, quest
        /// items).</summary>
        internal System.Func<ItemDrop.ItemData, bool> Stays;

        /// <summary>Whether a worn item he takes off stays with him (by <see cref="TakenOff"/>, or by
        /// <see cref="KeepsTakenOff"/>).</summary>
        internal bool Keeps(ItemDrop.ItemData item) => Keeping(TakenOff, KeepsTakenOff, item);

        /// <summary>The same test on its parts, for the planner's input.</summary>
        internal static bool Keeping(TakenOffGoes takenOff, System.Func<ItemDrop.ItemData, bool> keepsTakenOff, ItemDrop.ItemData item) =>
            takenOff == TakenOffGoes.StaysWithHim || (keepsTakenOff != null && keepsTakenOff(item));

        /// <summary>A few words for the messages and the log ("Outfitter", "the stand").</summary>
        internal string Source = "Outfitter";

        /// <summary>For the window's preview only (<see cref="Outfit.Preview"/>): items held for this equip elsewhere (the
        /// pieces a Loadout Stand would give the window's Equip through it, job 30's Session 3), counted as a chest that is
        /// taken from first and never put into (Exchange.LentView). <see cref="Outfit.Begin"/> and the exchange never read
        /// it: a real equip gets those pieces into his inventory before it begins. Null: none.</summary>
        internal List<ItemDrop.ItemData> Lent;
    }

    /// <summary>A loadout's item that couldn't be had, how many, and why.</summary>
    internal sealed class MissingItem
    {
        internal LoadoutItem Item;

        internal int Count;

        /// <summary>Why, in words: "none nearby", "not in your game", "no room in your inventory" …</summary>
        internal string Why;
    }

    /// <summary>What an equip came to, handed to the caller's done when it's over (in the frame it ends).</summary>
    internal sealed class EquipResult
    {
        /// <summary>Nothing changed: he cancelled at the pop-up, or "Missing items" is Cancel, or it couldn't begin.</summary>
        internal bool Cancelled;

        /// <summary>Why it was cancelled, in words, or null.</summary>
        internal string WhyCancelled;

        /// <summary>How many of the loadout's pieces were put on (each once, however many rounds it took).</summary>
        internal int Equipped;

        /// <summary>The pieces counted in <see cref="Equipped"/>.</summary>
        internal readonly HashSet<ItemDrop.ItemData> PiecesPutOn = new HashSet<ItemDrop.ItemData>();

        /// <summary>How many items came from chests (counting each item of a stack).</summary>
        internal int Fetched;

        /// <summary>How many items went into chests.</summary>
        internal int PutAway;

        /// <summary>What he took off and kept, with <see cref="TakenOffGoes.StaysWithHim"/> or by
        /// <see cref="EquipRequest.KeepsTakenOff"/>: in his inventory now. Never what the loadout itself carries unworn.</summary>
        internal readonly List<ItemDrop.ItemData> TakenOff = new List<ItemDrop.ItemData>();

        /// <summary>The loadout's items not had.</summary>
        internal readonly List<MissingItem> Missing = new List<MissingItem>();

        /// <summary>What was to go away but no chest could take: still with him.</summary>
        internal readonly List<ItemDrop.ItemData> StayedWithHim = new List<ItemDrop.ItemData>();

        /// <summary>Chests left out because someone had them open (or opened them meanwhile).</summary>
        internal int ChestsInUse;

        /// <summary>Chests whose owner's game didn't answer in time.</summary>
        internal int ChestsNoAnswer;

        /// <summary>Chests that turned out reserved (Reserve Stock's mark) once they were his.</summary>
        internal int ChestsReserved;

        /// <summary>Items a friend's open chest gave that didn't fit, dropped at his feet by the chest service.</summary>
        internal int Dropped;

        /// <summary>Pieces that couldn't be put on, with why ("broken", "needs a DLC", "below the world's level" …).</summary>
        internal readonly List<string> NotPutOn = new List<string>();

        /// <summary>The one-line message, as shown.</summary>
        internal string Line;
    }
}
