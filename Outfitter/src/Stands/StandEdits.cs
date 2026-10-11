using System;
using System.Collections.Generic;
using Outfitter.Equip;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// The stand window's edits (job 30, Session 2; his words, 2026-10-06: "free control over what loadouts are on what
    /// armor stands"): renaming a stand, assigning a loadout to a stand, moving a loadout to another stand with its gear,
    /// swapping two loadouts' stands, and unassigning. Each claims the stands it changes first and changes them in their
    /// frame (StandJob.Claim, one job at a time), checks there that he may still use them and that their marks are still
    /// the ones he answered about, moves every piece through StandMoves (measured, settled in a finally), and says what
    /// happened. Only stands near him (loaded) can be changed; only his own loadouts move. Someone else's personal stand
    /// (StandPersonal, job 30's Session 3) is left alone by every edit, at the click and again in the frame; with "Stands I
    /// save to become personal", a stand a loadout of his goes onto becomes his personal stand in the same frame.
    /// </summary>
    internal static class StandEdits
    {
        /// <summary>One of the stands near him, as the window lists them.</summary>
        internal sealed class Listed
        {
            internal IStand Stand;

            /// <summary>Its own name, or the game's.</summary>
            internal string Name;

            internal float Distance;

            internal StandMark Mark;

            /// <summary>The loadout its mark points at (his own, or read from the mark's code), or null.</summary>
            internal Loadout Loadout;

            /// <summary>The mark is his.</summary>
            internal bool Mine;

            /// <summary>He may use it (a ward, and "Which stands can be loadout stands").</summary>
            internal bool Usable;

            /// <summary>Whose personal stand it is (their player ID; 0: nobody's) and their name (job 30, Session 3).</summary>
            internal long Personal;

            internal string PersonalName = string.Empty;

            /// <summary>It's someone else's personal stand: his buttons leave it alone (not offered).</summary>
            internal bool Blocked;
        }

        /// <summary>The stands within "Stands listed within" of <paramref name="at"/>, nearest first.</summary>
        internal static List<Listed> Near(Vector3 at)
        {
            var listed = new List<Listed>();
            long me = Stands.Me();
            bool built = StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt;
            foreach (IStand stand in Stands.Nearby(at, StandSettings.ListRange.Value))
            {
                if (stand == null || !stand.Valid)
                {
                    continue;
                }
                StandMark mark = StandMark.Read(stand);
                bool mine = false;
                Loadout loadout = mark.Marked ? StandLoadouts.Of(mark, me, out mine) : null;
                (long whose, string whoseName) = StandPersonal.Read(stand);
                listed.Add(new Listed
                {
                    Stand = stand,
                    Name = StandTitle.Shown(stand, Stands.GameName(stand)),
                    Distance = Vector3.Distance(at, stand.Position),
                    Mark = mark,
                    Loadout = loadout,
                    Mine = mine && mark.By == me,
                    Usable = stand.MayUse(built, flash: false),
                    Personal = whose,
                    PersonalName = whoseName,
                    Blocked = whose != 0L && whose != me,
                });
            }
            return listed;
        }

        /// <summary>The stand near him that holds this loadout of his (marked with its Id by him): the nearest he may use
        /// (and that isn't someone else's personal stand), else the nearest he may not (none with
        /// <paramref name="usableOnly"/>: the window's Equip through a stand); null when none near him holds it. A loadout
        /// can be on two stands (one he may no longer use, or one farther than the list reaches): it moves from the one he
        /// may use (review round 2 of Session 2, m1).</summary>
        internal static IStand StandOf(Loadout loadout, Vector3 at, IStand except = null, bool usableOnly = false)
        {
            if (loadout == null)
            {
                return null;
            }
            long me = Stands.Me();
            IStand unusable = null;
            foreach (IStand stand in Stands.Nearby(at, StandSettings.ListRange.Value))
            {
                if (stand == null || !stand.Valid || (except != null && ReferenceEquals(stand.Key, except.Key)))
                {
                    continue;
                }
                StandMark mark = StandMark.Read(stand);
                if (mark.Marked && mark.Id == loadout.Id && mark.By == me)
                {
                    if (StandJob.StillMayUse(stand) && !StandPersonal.Blocks(stand))
                    {
                        return stand;
                    }
                    if (!usableOnly)
                    {
                        unusable = unusable ?? stand;
                    }
                }
            }
            return unusable;
        }

        /// <summary>The stand bar's Rename stand: asks for the stand's own name (filled in; empty for a stand without one,
        /// the game's name showing in the empty box), then names it, an empty name giving it the game's name back. The
        /// stands' own name box, taken down with them (review round 1 of Session 2, m4 and n6).</summary>
        internal static void AskRename(IStand stand)
        {
            if (!Ready(stand, out IWearer _) || !MayRename(stand))
            {
                return;
            }
            StandJob.AskName(stand, "Name this armour stand", StandTitle.Read(stand), Stands.GameName(stand), name => Rename(stand, name));
        }

        /// <summary>Names a stand (empty: the game's name again). Not a stand holding another player's loadout: only they
        /// rename that one (review round 1 of Session 2, j1).</summary>
        internal static void Rename(IStand stand, string name)
        {
            if (!Ready(stand, out IWearer _) || !MayRename(stand))
            {
                return;
            }
            StandMark seen = StandMark.Read(stand);
            string plain = Text.Plain(name ?? string.Empty, StandTitle.MaxName);
            if (StandTitle.Read(stand) == plain)
            {
                Stands.Say(Already(plain), false);
                return;
            }
            StandJob.Claim(new[] { stand }, held =>
            {
                if (!Held(held, stand) || !Usable(stand) || !StandJob.StillSeen(stand, seen))
                {
                    return;
                }
                if (!StandTitle.Write(stand, plain))
                {
                    Stands.Say(Already(plain), false);   // named so meanwhile
                    return;
                }
                Finish(stand);
                StandHover.Forget();   // its hover shows the new name at once
                Stands.Say(plain.Length > 0 ? $"This stand is {plain} now." : "This stand has the game's name again.", false);
            });
        }

        // Whether he may rename the stand: not someone else's personal stand, nor one with another player's loadout on it
        // (said).
        private static bool MayRename(IStand stand)
        {
            if (StandPersonal.Refused(stand))
            {
                return false;
            }
            StandMark mark = StandMark.Read(stand);
            if (mark.Marked && mark.By != Stands.Me())
            {
                Stands.Say($"This stand holds {Whose(mark)}: only they can rename it.", true);
                return false;
            }
            return true;
        }

        private static string Already(string plain) =>
            plain.Length > 0 ? $"This stand is {plain} already." : "This stand has the game's name already.";

        /// <summary>Takes the loadout off a stand (an ordinary stand again; its gear stays on it).</summary>
        internal static void Unassign(IStand stand)
        {
            if (!Ready(stand, out IWearer _) || StandPersonal.Refused(stand))
            {
                return;
            }
            StandMark seen = StandMark.Read(stand);
            if (!seen.Marked)
            {
                Stands.Say("This stand holds no loadout.", true);
                return;
            }
            if (seen.By != Stands.Me())
            {
                Stands.Say($"This stand holds {Whose(seen)}: only they can take it off.", true);
                return;
            }
            StandJob.Claim(new[] { stand }, held =>
            {
                if (!Held(held, stand) || !Usable(stand) || !StandJob.StillSeen(stand, seen))
                {
                    return;
                }
                StandMark.Clear(stand);
                Finish(stand);
                Stands.Say($"An ordinary stand again ({Named(seen)} stays in your list).", false);
            });
        }

        /// <summary>
        /// Puts a loadout of his on a stand (the stand bar's "Assign to armor stand", and the list's dropdown):
        /// - already there: nothing;
        /// - the stand holds someone else's loadout: refused (only they change it, or the save key saves over it);
        /// - the stand holds another loadout of his: swapped when this one is on a stand near him and "Moving onto a stand
        ///   that holds another loadout" swaps; else asked, then that loadout leaves the stand (its gear to him);
        /// - the stand is free: moved there with its gear (by "Moving a loadout moves its gear") when it's on another stand
        ///   near him; else the stand is marked (he hangs it back there with the stands' key).
        /// A stand of the loadout's that he may no longer use (a ward went up, or he didn't build it) is left as it is: the
        /// loadout is marked here too, and he's told (review round 1 of Session 2, m5).
        /// </summary>
        internal static void Assign(IStand target, Loadout loadout)
        {
            if (loadout == null || !Ready(target, out IWearer wearer) || StandPersonal.Refused(target))
            {
                return;
            }
            long me = Stands.Me();
            StandMark there = StandMark.Read(target);
            if (there.Marked && there.Id == loadout.Id && there.By == me)
            {
                Stands.Say($"This stand holds {loadout.Name} already.", true);
                return;
            }
            if (there.Marked && there.By != me)
            {
                Stands.Say($"This stand holds {Whose(there)}: only they can change it (or save over it with {StandInput.Text(StandSettings.SaveKey.Value)}).", true);
                return;
            }
            IStand from = StandOf(loadout, wearer.Position, except: target);
            IStand left = null;
            if (from != null && (!StandJob.StillMayUse(from) || StandPersonal.Blocks(from)))
            {
                left = from;
                from = null;
            }
            if (there.Marked)
            {
                Loadout other = StandLoadouts.Of(there, me, out _);
                if (from != null && StandSettings.Onto.Value == OntoAnother.SwapThem)
                {
                    Swap(from, target, loadout, other, there);
                    return;
                }
                string named = other != null ? other.Name : Named(there);
                string after = other != null
                    ? $"{named}'s gear comes to you; {named} stays in your list."
                    : $"{named} isn't in your list: whatever of it is on the stand stays there.";
                // Both stands' marks as he's asked about them, so a change to either while the question shows refuses
                // (the harness's S2-replace-source-seen-late: the old stand's was read only at the answer).
                StandMark seenFrom = from != null ? StandMark.Read(from) : null;
                Ask($"{Capital(Called(target, "this stand"))} holds {named}. Put {loadout.Name} on it instead?\n\n{after}",
                    "Put it on the stand", "Cancel", target, () => Replace(target, there, other, loadout, from, seenFrom, left));
                return;
            }
            if (from != null)
            {
                Move(from, target, loadout);
                return;
            }
            Mark(target, there, loadout, left);
        }

        /// <summary>Moves a loadout of his to another stand near him (the list's dropdown); null: off its stand.</summary>
        internal static void MoveTo(Loadout loadout, IStand target)
        {
            if (loadout == null)
            {
                return;
            }
            if (target != null)
            {
                Assign(target, loadout);
                return;
            }
            IWearer wearer = Outfit.WearerOf();
            IStand from = wearer != null ? StandOf(loadout, wearer.Position) : null;
            if (from == null)
            {
                Stands.Say($"{loadout.Name} isn't on a stand near you.", true);
                return;
            }
            if (!StandJob.StillMayUse(from) || StandPersonal.Blocks(from))
            {
                Stands.Say($"{loadout.Name} is only on stands you may not use near you ({Called(from, "its stand")}: {WhyNot(from)}).", true);
                return;
            }
            Unassign(from);
        }

        /// <summary>
        /// The personal stand key, and the stand bar's Personal (job 30, Session 3: his yes of 2026-10-06; A46's decision
        /// 45): makes the stand his personal stand, or an ordinary stand again. Refused at someone else's personal stand ("This
        /// is Astrid's personal stand: only they can change it."), and at a stand holding someone else's loadout (theirs to
        /// make personal, as renaming it is: decision 41). In the claim's frame, checked again: he may still use it, it
        /// isn't someone else's personal stand now, and its mark and its personal switch are as he saw them at the press.
        /// </summary>
        internal static void TogglePersonal(IStand stand)
        {
            if (!Ready(stand, out IWearer _))
            {
                return;
            }
            string why = StandPersonal.WhyNotToggle(stand);
            if (why != null)
            {
                Stands.Say(why, true);
                return;
            }
            StandMark seen = StandMark.Read(stand);
            long was = StandPersonal.Read(stand).Whose;
            bool on = was == 0L;
            StandJob.Claim(new[] { stand }, held =>
            {
                if (!Held(held, stand) || !Usable(stand) || !StandJob.StillSeen(stand, seen))
                {
                    return;
                }
                if (StandPersonal.Read(stand).Whose != was)
                {
                    Stands.Say("This stand changed meanwhile: nothing changed. Point at it to see it now.", true);
                    return;
                }
                if (on)
                {
                    StandPersonal.Write(stand, Stands.Me(), Stands.MyName());
                }
                else
                {
                    StandPersonal.Clear(stand);
                }
                Finish(stand);
                StandHover.Forget();   // its hover says so at once
                Stands.Say(on
                    ? $"{Capital(Called(stand, "this stand"))} is your personal stand now: other players' Outfitter keys and buttons leave it alone."
                    : $"{Capital(Called(stand, "this stand"))} isn't personal any more.", false);
            });
        }

        // ---- The edits, each in the frame its stands are his ----

        private static void Mark(IStand target, StandMark seen, Loadout loadout, IStand left)
        {
            StandJob.Claim(new[] { target }, held =>
            {
                if (!Held(held, target) || !Usable(target) || !StandJob.StillSeen(target, seen))
                {
                    return;
                }
                StandMark.Write(target, StandMark.For(loadout, Stands.Me(), Stands.MyName()));
                StandPersonal.Become(target);
                Finish(target);
                Stands.Say($"{loadout.Name} is on {Called(target, "this stand")} now: hang it back there with {StandInput.Text(StandSettings.EquipKey.Value)}.", false);
                SayLeft(loadout, left);
            });
        }

        private static void Move(IStand from, IStand to, Loadout loadout)
        {
            StandMark seenFrom = StandMark.Read(from);
            StandMark seenTo = StandMark.Read(to);
            StandJob.Claim(new[] { from, to }, held =>
            {
                if (!Held(held, from) || !Held(held, to) || !Usable(from) || !Usable(to) ||
                    !StandJob.StillSeen(from, seenFrom) || !StandJob.StillSeen(to, seenTo))
                {
                    return;
                }
                int moved = 0;
                int stayed = 0;
                if (StandSettings.MoveGear.Value)
                {
                    GearAcross(from, to, loadout, out moved, out stayed);
                }
                StandMark.Clear(from);
                StandMark.Write(to, StandMark.For(loadout, Stands.Me(), Stands.MyName()));
                StandPersonal.Become(to);
                Finish(from, to);
                string gear = moved > 0 ? $", {StandJob.Pieces(moved)} along" : string.Empty;
                Stands.Say($"{loadout.Name} moved to {Called(to, "the new stand")}{gear}.", false);
                if (stayed > 0)
                {
                    Stands.Say($"{Capital(StandJob.Pieces(stayed))} of it stayed on {Called(from, "the old stand")}: {(stayed == 1 ? "its place" : "their places")} on {Called(to, "the new stand")} {(stayed == 1 ? "is" : "are")} taken.", false);
                }
            });
        }

        // Two loadouts of his trade stands: the other's gear comes to him first (all or nothing), this one's moves across,
        // then the other's goes from him onto the stand this one left.
        private static void Swap(IStand from, IStand to, Loadout loadout, Loadout other, StandMark otherMark)
        {
            StandMark seenFrom = StandMark.Read(from);
            StandJob.Claim(new[] { from, to }, held =>
            {
                if (!Held(held, from) || !Held(held, to) || !Usable(from) || !Usable(to) ||
                    !StandJob.StillSeen(from, seenFrom) || !StandJob.StillSeen(to, otherMark))
                {
                    return;
                }
                IWearer wearer = Outfit.WearerOf();
                if (wearer == null || wearer.Unavailable)
                {
                    return;
                }
                string otherName = other != null ? other.Name : Named(otherMark);
                List<Moved> came = null;
                int across = 0;
                int stayed = 0;
                int back = 0;
                if (StandSettings.MoveGear.Value)
                {
                    var theirs = new List<int>(StandPlan.OnStand(to, StandPlan.Pieces(other, to), StandJob.Rules()).Keys);
                    came = StandMoves.TakeAll(to, wearer.Inventory, theirs, out int needed, out int _);
                    if (came == null)
                    {
                        to.Send();
                        Stands.Say($"No room in your inventory for {otherName}'s {StandJob.Pieces(needed)}, to swap the stands: make room, then try again.", true);
                        return;
                    }
                    GearAcross(from, to, loadout, out across, out stayed);
                    foreach (Moved one in came)
                    {
                        if (StandMoves.PutBack(from, one, wearer))
                        {
                            back++;
                        }
                    }
                }
                StandMark.Write(from, StandMark.For(other ?? Clone(otherMark), Stands.Me(), Stands.MyName()));
                StandMark.Write(to, StandMark.For(loadout, Stands.Me(), Stands.MyName()));
                StandPersonal.Become(from);   // each takes a loadout of his
                StandPersonal.Become(to);
                Finish(from, to);
                string picked = Called(to, "the stand you picked");
                string leftBehind = Called(from, $"the stand {loadout.Name} left");
                Stands.Say($"Swapped: {loadout.Name} on {picked}, {otherName} on {leftBehind}.", false);
                if (stayed > 0)
                {
                    Stands.Say($"{Capital(StandJob.Pieces(stayed))} of {loadout.Name} stayed on {leftBehind}: {(stayed == 1 ? "its place" : "their places")} on {picked} {(stayed == 1 ? "is" : "are")} taken.", false);
                }
                int withHim = came != null ? came.Count - back : 0;
                if (withHim > 0)
                {
                    Stands.Say($"{Capital(StandJob.Pieces(withHim))} of {otherName} stayed with you: {(withHim == 1 ? "its place" : "their places")} on {leftBehind} {(withHim == 1 ? "is" : "are")} taken.", false);
                }
                if (other == null && StandSettings.MoveGear.Value)
                {
                    Stands.Say($"{otherName} isn't in your list: whatever of it was on {picked} stayed there.", false);
                }
            });
        }

        // Another loadout of his leaves the stand (its gear to him, all or nothing), and this one takes it (with its gear
        // from the stand it was on, if any). Both marks are as they were when he was asked.
        private static void Replace(IStand target, StandMark seen, Loadout other, Loadout loadout, IStand from, StandMark seenFrom, IStand left)
        {
            var stands = new List<IStand> { target };
            if (from != null)
            {
                stands.Add(from);
            }
            StandJob.Claim(stands, held =>
            {
                if (!Held(held, target) || !Usable(target) || !StandJob.StillSeen(target, seen) ||
                    (from != null && (!Held(held, from) || !Usable(from) || !StandJob.StillSeen(from, seenFrom))))
                {
                    return;
                }
                IWearer wearer = Outfit.WearerOf();
                if (wearer == null || wearer.Unavailable)
                {
                    return;
                }
                string otherName = other != null ? other.Name : Named(seen);
                int came = 0;
                if (other != null)
                {
                    var theirs = new List<int>(StandPlan.OnStand(target, StandPlan.Pieces(other, target), StandJob.Rules()).Keys);
                    List<Moved> taken = StandMoves.TakeAll(target, wearer.Inventory, theirs, out int needed, out int _);
                    if (taken == null)
                    {
                        target.Send();
                        Stands.Say($"No room in your inventory for {otherName}'s {StandJob.Pieces(needed)}: make room, then try again.", true);
                        return;
                    }
                    came = taken.Count;
                }
                int across = 0;
                int stayed = 0;
                if (from != null)
                {
                    if (StandSettings.MoveGear.Value)
                    {
                        GearAcross(from, target, loadout, out across, out stayed);
                    }
                    StandMark.Clear(from);
                }
                StandMark.Write(target, StandMark.For(loadout, Stands.Me(), Stands.MyName()));
                StandPersonal.Become(target);
                if (from != null)
                {
                    Finish(target, from);
                }
                else
                {
                    Finish(target);
                }
                Stands.Say($"{loadout.Name} is on {Called(target, "this stand")} now" + (came > 0 ? $"; {otherName}'s {StandJob.Pieces(came)} came to you." : "."), false);
                if (other == null)
                {
                    Stands.Say($"{otherName} isn't in your list: whatever of it was on the stand stayed there.", false);
                }
                if (stayed > 0)
                {
                    Stands.Say($"{Capital(StandJob.Pieces(stayed))} of {loadout.Name} stayed on {Called(from, "its old stand")}: {(stayed == 1 ? "its place" : "their places")} here {(stayed == 1 ? "is" : "are")} taken.", false);
                }
                SayLeft(loadout, left);
            });
        }

        // The loadout's pieces on one stand straight onto the other's same places, where free.
        private static void GearAcross(IStand from, IStand to, Loadout loadout, out int moved, out int stayed)
        {
            moved = 0;
            stayed = 0;
            var places = new List<int>(StandPlan.OnStand(from, StandPlan.Pieces(loadout, from), StandJob.Rules()).Keys);
            places.Sort();
            foreach (int place in places)
            {
                if (StandMoves.Transfer(from, place, to, place))
                {
                    moved++;
                }
                else
                {
                    stayed++;
                }
            }
        }

        // ---- Small things ----

        // The checks every edit makes at the click: the stands on, the stand there, him there, the stand his to use; each
        // refusal said (review round 1 of Session 2, n7), but for him unable to act (dead, teleporting).
        private static bool Ready(IStand stand, out IWearer wearer)
        {
            wearer = Outfit.WearerOf();
            if (!Stands.Active)
            {
                Stands.Say("Loadout Stands is off.", true);
                return false;
            }
            if (stand == null || !stand.Valid)
            {
                Stands.Say("The stand is gone.", true);
                return false;
            }
            if (wearer == null || wearer.Unavailable)
            {
                return false;
            }
            bool built = StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt;
            if (!stand.MayUse(built, flash: true))
            {
                Stands.Say(built && stand.MayUse(false, flash: false)
                    ? $"You didn't build {Called(stand, "this stand")} (your settings: only stands you built)."
                    : $"You may not use {Called(stand, "this stand")} (a ward).", true);
                return false;
            }
            return true;
        }

        private static bool Held(StandClaimResult held, IStand stand)
        {
            if (held.Held.Contains(stand))
            {
                return true;
            }
            Stands.Say(StandJob.NotHeldWords(held), true);
            return false;
        }

        // In the claim's frame: he may still use the stand (a ward may have gone up meanwhile), and it isn't someone else's
        // personal stand (a friend may have made it theirs while his question showed or the claim waited); each refusal said.
        private static bool Usable(IStand stand)
        {
            if (StandJob.StillMayUse(stand))
            {
                return !StandPersonal.Refused(stand);
            }
            Stands.Say($"You may not use {Called(stand, "this stand")} any more ({WhyNot(stand)}).", true);
            return false;
        }

        // Why he may not use a stand: someone else's personal stand, his setting (only stands he built), or a ward.
        private static string WhyNot(IStand stand)
        {
            if (StandPersonal.Blocks(stand))
            {
                string name = StandPersonal.Read(stand).Name;
                return $"it's {(name.Length > 0 ? name + "'s" : "another player's")} personal stand";
            }
            return StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt && stand.MayUse(false, flash: false)
                ? "you didn't build it, and your settings take only stands you built"
                : "a ward";
        }

        // The loadout's stand he may no longer use, left as it was (m5): said.
        private static void SayLeft(Loadout loadout, IStand left)
        {
            if (left != null && left.Valid)
            {
                Stands.Say($"{loadout.Name} stays on {Called(left, "its old stand")} too, with its gear there: you may not use that stand ({WhyNot(left)}).", false);
            }
        }

        // Sends the changed stands to everyone and holds them, so other games have the change first.
        private static void Finish(params IStand[] stands)
        {
            foreach (IStand stand in stands)
            {
                stand.Send();
                StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
            }
        }

        // Asks with the stands' own question (StandJob's: taken down at a switch-off, its answer acting only while the
        // stands work, the stand is there and so is he).
        private static void Ask(string question, string yes, string no, IStand stand, Action then) =>
            StandJob.Ask(stand, question, yes, no, answer =>
            {
                if (answer)
                {
                    then();
                }
            });

        // A stand's own name, or these words for one without (review round 1 of Session 2, n8: not every stand "the stand").
        private static string Called(IStand stand, string otherwise)
        {
            string own = stand != null && stand.Valid ? StandTitle.Read(stand) : string.Empty;
            return own.Length > 0 ? own : otherwise;
        }

        private static string Named(StandMark mark) => mark.Name.Length > 0 ? mark.Name : "its loadout";

        private static string Whose(StandMark mark) =>
            (mark.ByName.Length > 0 ? $"{mark.ByName}'s loadout" : "another player's loadout") + (mark.Name.Length > 0 ? " " + mark.Name : string.Empty);

        // A loadout standing in for a mark whose loadout couldn't be read (its Id and name kept, so the mark stays the same).
        private static Loadout Clone(StandMark mark) => new Loadout { Id = mark.Id, Name = mark.Name };

        private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
