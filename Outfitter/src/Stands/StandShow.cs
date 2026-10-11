using System.Collections.Generic;
using Outfitter.Equip;
using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// A stand shows its loadout (his suggestion, 2026-10-06: "allows all three stands ... to "display" the cape, even if I
    /// only have one copy"; A46's decision 48, "Show what a stand's loadout lacks on it"): a marked stand is drawn, in this
    /// game only, wearing each piece of its loadout that isn't on it now (he wears it, another stand has it, it's in a
    /// chest), in the place the game would hang it, where the stand's record holds nothing. Nothing is on the stand in truth:
    /// the record is never written, so the game's E, its "take" line, a hand attach and a destroyed stand's drop all go by
    /// what is really there, and a friend without the pack (or with this off) sees the stand as it is. This class decides
    /// what is drawn where, from the record and his list (pure, so the scratch harness reaches it); GameShow draws it.
    /// </summary>
    internal static class StandShow
    {
        private static readonly Dictionary<int, LoadoutItem> None = new Dictionary<int, LoadoutItem>();

        /// <summary>Whether the setting shows this mark's stand: OnMyStands, only a stand he (this character) saved;
        /// OnEveryMarkedStand, any marked stand; Off, none.</summary>
        internal static bool ShowsOn(StandMark mark, long me)
        {
            if (mark == null || !mark.Marked || StandSettings.Show == null)
            {
                return false;
            }
            switch (StandSettings.Show.Value)
            {
                case ShowLacks.OnMyStands:
                    return me != 0L && mark.By == me;
                case ShowLacks.OnEveryMarkedStand:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// What a stand shows that it doesn't hold, by place: while the stands work and the setting shows this stand, each of
        /// its loadout's pieces the stand can show (StandPlan.Pieces: in the place the game would hang it, the first piece for
        /// each place) that no item on the stand answers (StandPlan.OnStand, by the stand's rules), where the record holds
        /// nothing (a place holding anything, a stray included, shows what it holds). Empty for none.
        /// </summary>
        internal static Dictionary<int, LoadoutItem> Missing(IStand stand)
        {
            var shown = new Dictionary<int, LoadoutItem>();
            if (!Stands.Active || Stands.ShowLost || stand == null || !stand.Valid)
            {
                return shown;
            }
            StandMark mark = StandMark.Read(stand);
            long me = Stands.Me();
            if (!ShowsOn(mark, me))
            {
                return shown;
            }
            Loadout loadout = StandLoadouts.Of(mark, me, out _);
            if (loadout == null)
            {
                return shown;
            }
            List<StandPiece> pieces = StandPlan.Pieces(loadout, stand);
            var onStand = new HashSet<StandPiece>(StandPlan.OnStand(stand, pieces, StandJob.Rules()).Values);
            foreach (StandPiece piece in pieces)
            {
                if (!onStand.Contains(piece) && !stand.Has(piece.Place))
                {
                    shown[piece.Place] = piece.Item;
                }
            }
            return shown;
        }

        // What was worked out for a stand, and the keys it was worked out for.
        private sealed class Entry
        {
            internal long Revision;
            internal int Store;
            internal int Settings;
            internal long Me;
            internal bool On;
            internal Dictionary<int, LoadoutItem> Shown;
        }

        private static readonly Dictionary<object, Entry> Made = new Dictionary<object, Entry>();

        /// <summary>
        /// <see cref="Missing"/>, worked out again only when one of its keys changed since the last time for this stand: its
        /// record (<paramref name="revision"/>, the record's data revision, which every write to a place or the mark moves),
        /// his loadouts (Store.Version), any setting (Settings.Version: the show setting, the stands' mode, Outfitter's
        /// matching rules), who he is, and whether the stands work (Outfitter's switch, theirs, an error). So the game's
        /// redraw of a stand, every frame, costs a few comparisons; <paramref name="changed"/> says when it was worked out
        /// anew.
        /// </summary>
        internal static Dictionary<int, LoadoutItem> Cached(IStand stand, long revision, out bool changed)
        {
            changed = false;
            if (stand == null)
            {
                return None;
            }
            bool on = Stands.Active && !Stands.ShowLost;
            int store = Loadouts.Store.Version;
            int settings = Outfitter.Settings.Version;
            long me = Stands.Me();
            if (Made.TryGetValue(stand.Key, out Entry entry) && entry.Revision == revision && entry.Store == store &&
                entry.Settings == settings && entry.Me == me && entry.On == on)
            {
                return entry.Shown;
            }
            if (Made.Count > 256)
            {
                Made.Clear();   // destroyed and unloaded stands linger as keys: start again now and then
            }
            Made[stand.Key] = new Entry
            {
                Revision = revision,
                Store = store,
                Settings = settings,
                Me = me,
                On = on,
                Shown = on ? Missing(stand) : None,
            };
            changed = true;
            return Made[stand.Key].Shown;
        }

        /// <summary>Forgets what was worked out (the undo step; switched off, every stand is worked out again anyway).</summary>
        internal static void Forget() => Made.Clear();
    }
}
