using System.Collections.Generic;

namespace TopperUpper.Loose
{
    /// <summary>One chest as a put-away frame sees it, from this computer's copy, for one kind of item.</summary>
    internal struct ChestView
    {
        internal Spot At;

        /// <summary>This game may change it now (ChestClaims.MayChange), and the rules allow it (a reserved chest only when
        /// reserved chests are used).</summary>
        internal bool Usable;

        /// <summary>It destroys what it holds: the Obliterator's container (code review M1). Never used, whatever else it
        /// says.</summary>
        internal bool Destroys;

        /// <summary>It holds some of the item already (by name, any quality).</summary>
        internal bool Holds;

        /// <summary>It is set up with StoreStash (and setups are followed).</summary>
        internal bool HasSetup;

        /// <summary>Its setup takes the item.</summary>
        internal bool SetupTakes;

        /// <summary>Another player marked it as theirs (StoreStash's Left Alt + H).</summary>
        internal bool OthersMark;

        /// <summary>How many of the item it can still take (its part-stacks and empty cells).</summary>
        internal int Room;
    }

    /// <summary>
    /// Which chests one frame of a put-away uses (at most "Chests put into per frame"), chosen the way the quick stack's
    /// routing would rank them (StackRouting, with Stow.Rules: a set-up chest takes only what its setup takes, and comes
    /// first; then chests holding the item; then, with "Only into chests already holding it" off, any chest), each within
    /// the range of at least one of the frame's items, with room, one this game may change now, and never the Obliterator
    /// (code review M1; Settings.Rules leaves it out before this, and Stow and GroundPut refuse it after). Those chosen go to Stow
    /// nearest first, which routes and plans among them; a chest that fills drops out next frame and the next one is used.
    /// </summary>
    internal static class ChestPick
    {
        /// <summary>A chest's place in the choice: 0 a set-up chest whose setup takes the item, 1 one holding it, 2 any
        /// other (only where-held off); -1 not used.</summary>
        internal static int Class(in ChestView chest, bool onlyWhereHeld, bool followSetups, bool leaveOthersMarked)
        {
            if (!chest.Usable || chest.Room <= 0)
            {
                return -1;
            }
            if (chest.Destroys)
            {
                return -1;   // the Obliterator: what goes in is destroyed at its next pull
            }
            if (leaveOthersMarked && chest.OthersMark)
            {
                return -1;
            }
            if (followSetups && chest.HasSetup)
            {
                return chest.SetupTakes ? 0 : -1;
            }
            if (chest.Holds)
            {
                return 1;
            }
            return onlyWhereHeld ? -1 : 2;
        }

        /// <summary>The chests (indices into <paramref name="chests"/>) this frame puts into: those that may take the item
        /// and lie within <paramref name="range"/> of at least one of <paramref name="items"/>, the best
        /// <paramref name="cap"/> by class and then nearness to <paramref name="centre"/>, returned nearest first.</summary>
        internal static List<int> Candidates(IList<ChestView> chests, IList<Spot> items, Spot centre, float range,
                                             bool onlyWhereHeld, bool followSetups, bool leaveOthersMarked, int cap)
        {
            var picked = new List<(int Index, int Class, float Away)>();
            if (chests == null || items == null || items.Count == 0 || cap <= 0)
            {
                return new List<int>();
            }
            for (int c = 0; c < chests.Count; c++)
            {
                int kind = Class(chests[c], onlyWhereHeld, followSetups, leaveOthersMarked);
                if (kind < 0 || !InRangeOfAny(chests[c].At, items, range))
                {
                    continue;
                }
                picked.Add((c, kind, chests[c].At.DistanceSquared(centre)));
            }
            picked.Sort((a, b) => a.Class != b.Class ? a.Class.CompareTo(b.Class) : a.Away != b.Away ? a.Away.CompareTo(b.Away) : a.Index.CompareTo(b.Index));
            if (picked.Count > cap)
            {
                picked.RemoveRange(cap, picked.Count - cap);
            }
            picked.Sort((a, b) => a.Away != b.Away ? a.Away.CompareTo(b.Away) : a.Index.CompareTo(b.Index));
            var chosen = new List<int>(picked.Count);
            foreach (var each in picked)
            {
                chosen.Add(each.Index);
            }
            return chosen;
        }

        /// <summary>Whether a chest at <paramref name="at"/> lies within <paramref name="range"/> of any of the items.</summary>
        internal static bool InRangeOfAny(Spot at, IList<Spot> items, float range)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (at.Within(items[i], range))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
