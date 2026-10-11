using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// A personal stand (his yes, 2026-10-06; A46's decision 45): its record says whose it is, under the pack's keys as the
    /// mark's are (the player ID, and the name as written then), so friends with the pack see it and a game without it
    /// ignores it. A mark of whose, not a lock: for another player with the pack, every key and button of the stands leaves
    /// someone else's personal stand alone (save, put on and hang back, pose, rename, assign, unassign, personal, moving a
    /// loadout onto it, lending from it); the game's own E is untouched, the ward deciding who may touch a stand at all.
    /// "Someone else" is anyone but this character (his other characters too, as with marks). Written only by the stand's
    /// owner, in a claim's frame; taken off by writing 0 and "", never a removal (a removal reaches no other player). Text
    /// another game wrote is read through Outfitter's plain filter. Switched off, a personal stand is an ordinary stand.
    /// </summary>
    internal static class StandPersonal
    {
        internal const string Key = "modprojects.outfitter.standpersonal";
        internal const string NameKey = "modprojects.outfitter.standpersonalname";

        /// <summary>Whose personal stand it is (their player ID; 0: nobody's) and their name, plain (empty when not
        /// personal, or not written).</summary>
        internal static (long Whose, string Name) Read(IStand stand)
        {
            if (stand == null || !stand.Valid)
            {
                return (0L, string.Empty);
            }
            long whose = stand.GetLong(Key);
            if (whose == 0L)
            {
                return (0L, string.Empty);
            }
            return (whose, Text.Plain(stand.GetString(NameKey) ?? string.Empty, Loadout.MaxName));
        }

        /// <summary>Makes it whose personal stand (0 and "": nobody's), as its owner (the caller's claim); false when it was
        /// so already (nothing written).</summary>
        internal static bool Write(IStand stand, long whose, string name)
        {
            if (stand == null || !stand.Valid)
            {
                return false;
            }
            string plain = whose == 0L ? string.Empty : Text.Plain(name ?? string.Empty, Loadout.MaxName);
            (long was, string wasName) = Read(stand);
            if (was == whose && wasName == plain)
            {
                return false;
            }
            stand.SetLong(Key, whose);
            stand.SetString(NameKey, plain);
            return true;
        }

        /// <summary>An ordinary stand again (not personal).</summary>
        internal static bool Clear(IStand stand) => Write(stand, 0L, string.Empty);

        /// <summary>Someone else's personal stand, while the stands work: his keys and buttons leave it alone.</summary>
        internal static bool Blocks(IStand stand)
        {
            if (!Stands.Active)
            {
                return false;
            }
            long whose = Read(stand).Whose;
            return whose != 0L && whose != Stands.Me();
        }

        /// <summary>His own personal stand (this character's).</summary>
        internal static bool Mine(IStand stand)
        {
            long me = Stands.Me();
            return me != 0L && Read(stand).Whose == me;
        }

        /// <summary>The refusal at someone else's personal stand, naming whose it is.</summary>
        internal static string Refusal(IStand stand) => $"This is {Whose(Read(stand).Name)} personal stand: only they can change it.";

        /// <summary>Says the refusal (at the centre) and answers true when it's someone else's personal stand; false, saying
        /// nothing, otherwise.</summary>
        internal static bool Refused(IStand stand)
        {
            if (!Blocks(stand))
            {
                return false;
            }
            Stands.Say(Refusal(stand), true);
            return true;
        }

        /// <summary>
        /// Whether he may turn the stand's personal switch now (the window's Personal toggle and the key): the stands on, the
        /// stand his to use (the ward, "Which stands can be loadout stands"), and either his own personal stand (to take it
        /// off) or not personal and not holding someone else's loadout (theirs to make personal: the same rule as renaming
        /// it, decision 41).
        /// </summary>
        internal static bool MayToggle(IStand stand) => WhyNotToggle(stand) == null;

        /// <summary>Why he may not turn the switch now, in words; null when he may.</summary>
        internal static string WhyNotToggle(IStand stand)
        {
            if (!Stands.Active)
            {
                return "Loadout Stands is off.";
            }
            if (stand == null || !stand.Valid)
            {
                return "The stand is gone.";
            }
            long me = Stands.Me();
            if (me == 0L)
            {
                return "There's no character to make it personal for.";
            }
            if (!stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: false))
            {
                return "You may not use this stand.";
            }
            (long whose, string name) = Read(stand);
            if (whose != 0L)
            {
                return whose == me ? null : $"This is {Whose(name)} personal stand: only they can change it.";
            }
            StandMark mark = StandMark.Read(stand);
            if (mark.Marked && mark.By != me)
            {
                return $"This stand holds {(mark.ByName.Length > 0 ? mark.ByName + "'s" : "another player's")} loadout: only they can make it personal.";
            }
            return null;
        }

        /// <summary>
        /// "Stands I save to become personal": in the frame a save, an assign or a move writes his mark on the stand, it
        /// becomes his personal stand too, when it isn't someone else's (the caller has refused that already; asked again
        /// here). True when it was written.
        /// </summary>
        internal static bool Become(IStand stand)
        {
            if (StandSettings.BecomePersonal == null || !StandSettings.BecomePersonal.Value || !Stands.Active)
            {
                return false;
            }
            long me = Stands.Me();
            if (me == 0L || Read(stand).Whose != 0L)
            {
                return false;   // his already, or someone else's (never taken over)
            }
            return Write(stand, me, Stands.MyName());
        }

        // "Astrid's", or "another player's" for a name never written.
        private static string Whose(string name) => name.Length > 0 ? name + "'s" : "another player's";
    }
}
