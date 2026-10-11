using System;
using System.Collections.Generic;
using System.Linq;
using Outfitter.Equip;
using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// What the stands' keys start (the plan's "Put it on", "Hang it back", "Save to a stand"), one at a time: each claims the
    /// stand it changes (StandClaims) and changes it inside the frame it's this computer's (StandMoves), holding it while
    /// the job runs and for "Hold the stand after a swap" seconds after. Putting a loadout on goes through Outfitter's own
    /// equip (Outfit.Begin), the stand's gear counted as carried; what he wore goes home piece by piece (the request's
    /// KeepsTakenOff). Every step ends with each item on a stand or with him (or where Outfitter's equip put it), never on
    /// the ground and never twice; every answer to a question first checks that the stands still work, so an error or a
    /// switch-off while a question shows chains nothing (review round 1's M4). Since 0.4.0 (job 30, Session 3): his other
    /// stands lend a put-on what it lacks (Loans, claimed with the stand, taken in its frame, back onto their own stands when
    /// not worn or cancelled); the window's Equip can go through a stand (EquipFromWindow, WindowStand, PreviewRequest); and
    /// someone else's personal stand is refused by every key, at the press and again in the claim's frame (StandPersonal).
    /// </summary>
    internal static class StandJob
    {
        /// <summary>How long a job may wait for Outfitter's equip to end after it stopped running (an error switched it off
        /// mid-equip, so its done never comes): then the job is dropped, everything already with him or on a stand.</summary>
        private const float LostEquip = 2f;

        private static Job job;

        internal static bool Busy => job != null;

        /// <summary>Every frame while the stands are on: the job's stands kept held while it runs, and a job whose equip
        /// ended without a word dropped.</summary>
        internal static void Tick()
        {
            if (job == null)
            {
                return;
            }
            float now = Stands.Clock();
            foreach (IStand stand in job.Stands)
            {
                StandClaims.Hold(stand, 1f);
            }
            if (job.Equipping)
            {
                if (Outfit.Busy)
                {
                    job.EquipSeen = now;
                }
                else if (now - job.EquipSeen > LostEquip)
                {
                    End();   // the equip ended without calling back (an error switched Outfitter off meanwhile)
                }
            }
        }

        /// <summary>The stands switched off (or Outfitter): the job ends where it stands, and he's told what is with him; a
        /// question of the stands still showing is taken down (its answer then does nothing: review round 2, n1). An equip
        /// of Outfitter's under way goes on as Outfitter's own: from now on it keeps nothing back for a stand (the request's
        /// KeepsTakenOff and Stays ask whether the job is still this one: harness area B).</summary>
        internal static void SwitchedOff()
        {
            if (job != null && job.Equipping)
            {
                int moved = job.Moved != null ? job.Moved.Count : 0;
                string stand = moved > 0 ? $"the stand's {Pieces(moved)} came to your inventory; " : string.Empty;
                if (job.Lent.Count > 0)
                {
                    stand += $"{Pieces(job.Lent.Count)} your other stands lent came too; ";
                }
                Stands.Say($"Loadout Stands is off: Outfitter finishes the equip on its own, and nothing more goes onto a stand ({stand}what Outfitter doesn't put away stays in your inventory).", false);
            }
            else if (job != null && job.Homing + job.Leftover > 0)
            {
                Stands.Say($"Loadout Stands is off: {Pieces(job.Homing + job.Leftover)} waiting to go onto a stand stayed in your inventory.", false);
            }
            job = null;
            if (asking != 0)
            {
                int number = asking;
                asking = 0;
                Stands.Dismiss(number);   // only if it still shows: a box he cancelled never told us it went
            }
        }

        // The number of the stands' own question (or name box) last shown, until it's answered (0: none). A name box he
        // cancelled is never answered, so the number can outlive it: the take-down asks the pop-up by number.
        private static int asking;

        // Whether an answer to one of the stands' questions may still act: the stands on, the stand there, him there (a
        // question taken down with its scene is answered "no" while all three can be gone: review round 2, m2).
        private static bool StillThere(IStand stand) => Stands.Active && stand != null && stand.Valid && Outfit.WearerOf() != null;

        // Asks with the stands' pop-up, noting the number of theirs that shows.
        internal static void Ask(IStand stand, string question, string yes, string no, Action<bool> then)
        {
            int number = 0;
            number = Stands.Ask(question, yes, no, answer =>
            {
                if (asking == number)
                {
                    asking = 0;
                }
                if (StillThere(stand))
                {
                    then(answer);
                }
                else if (answer)
                {
                    SayGone(stand);
                }
            });
            asking = number;
        }

        // A "yes" (or a name) about a stand that went meanwhile, the stands on and him there: said, as a claim would say it
        // (the last look's n4); a "no", or a question taken down with its scene, stays silent.
        private static void SayGone(IStand stand)
        {
            if (Stands.Active && Outfit.WearerOf() != null && (stand == null || !stand.Valid))
            {
                Stands.Say("The stand is gone.", true);
            }
        }

        // Asks for a name with the stands' pop-up (not called back when he cancels).
        private static void AskName(IStand stand, string heading, string filled, Action<string> then) => AskName(stand, heading, filled, null, then);

        /// <summary>Asks for a name with the stands' pop-up, taken down with the stands as their questions are (not called
        /// back when he cancels). With <paramref name="emptyShows"/>, an empty box is an answer too, and shows those words
        /// (a stand's own name: empty gives it the game's name back; review round 1 of Session 2, m4 and n6).</summary>
        internal static void AskName(IStand stand, string heading, string filled, string emptyShows, Action<string> then)
        {
            int number = 0;
            Action<string> answered = name =>
            {
                if (asking == number)
                {
                    asking = 0;
                }
                if (StillThere(stand))
                {
                    then(name);
                }
                else
                {
                    SayGone(stand);
                }
            };
            number = emptyShows == null ? Stands.Name(heading, filled, answered) : Stands.NameOrEmpty(heading, filled, answered, emptyShows);
            asking = number;
        }

        // Whether he may still use the stand after a claim's wait (a ward may have gone up: review round 2, n5).
        internal static bool StillMayUse(IStand stand) => stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: false);

        /// <summary>
        /// Claims these stands as one job (one at a time, as every key's job is) and, in the frame they're his, runs
        /// <paramref name="inFrame"/> with the claim's result, the job already over so it may start another (the stand
        /// window's edits, job 30, Session 2). False, with him told, when something else is under way.
        /// </summary>
        internal static bool Claim(IReadOnlyList<IStand> stands, Action<StandClaimResult> inFrame)
        {
            if (!Stands.Active || stands == null || stands.Count == 0)
            {
                return false;
            }
            if (Busy || Outfit.Busy || StandClaims.Busy)
            {
                Stands.Say("Outfitter is busy: try again in a moment.", true);
                return false;
            }
            var current = new Job();
            current.Stands.AddRange(stands);
            job = current;
            if (!StandClaims.Start(stands, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    End();
                    inFrame(held);
                }))
            {
                End();
                Stands.Say("Outfitter is busy: try again in a moment.", true);
                return false;
            }
            return true;
        }

        /// <summary>The claim's "didn't get it" in words, for the stand window's edits.</summary>
        internal static string NotHeldWords(StandClaimResult held) => NotHeld(held);

        /// <summary>The undo step: the job dropped, nothing more moved (each move settles inside its own frame, also when
        /// another mod's code throws: StandMoves). A question still showing is Outfitter's own undo step's (Popup.Close),
        /// whose "no" then does nothing here (StillThere).</summary>
        internal static void Forget()
        {
            job = null;
            asking = 0;
        }

        private static void End() => job = null;

        private sealed class Job
        {
            internal readonly List<IStand> Stands = new List<IStand>();
            internal bool Equipping;
            internal float EquipSeen;
            internal List<Moved> Moved;

            // Pieces of what he wore waiting for their stand's claim, and the stand's own pieces waiting to go back on it.
            internal int Homing;
            internal int Leftover;

            /// <summary>What his other stands lent this put-on, each with the stand it came from (decision 46).</summary>
            internal readonly List<Lent> Lent = new List<Lent>();

            /// <summary>What came off the stands for a piece the loadout shows but doesn't wear (0.5.0: "Displayed", not
            /// "Worn"), with that piece: carried by the loadout, so it stays with him after the equip rather than going back
            /// (StaysCarried).</summary>
            internal readonly Dictionary<ItemDrop.ItemData, LoadoutItem> Carries = new Dictionary<ItemDrop.ItemData, LoadoutItem>();

            /// <summary>The loadout put on and its rules (StaysCarried).</summary>
            internal Loadout Loadout;
            internal EquipRules Rules;

            /// <summary>What he carried, not wore, going home onto another of his stands: a piece it shows but doesn't wear
            /// (review 1's m3). Kept from the chests by the request's Stays, hung there after the equip.</summary>
            internal readonly HashSet<ItemDrop.ItemData> HomeCarried = new HashSet<ItemDrop.ItemData>();

            /// <summary>The window's status line, for a put-on the window started (its refusals and the equip's line); null
            /// for the key's.</summary>
            internal Action<string> Told;
        }

        /// <summary>A piece another of his stands lends a put-on (decision 46): the stand, the place, the piece of the
        /// loadout it answers, the item and the stand's mark as read before the claim, and the stand's name for his
        /// messages.</summary>
        internal sealed class Loan
        {
            internal IStand From;
            internal int Place;
            internal StandPiece Piece;
            internal ItemDrop.ItemData Seen;
            internal StandMark Mark;
            internal string Where;
        }

        /// <summary>A lent piece that came to him: its loan, and the move that brought it.</summary>
        internal sealed class Lent
        {
            internal Loan Loan;
            internal Moved Moved;
        }

        // ---------- the equip key ----------

        /// <summary>The equip key at a stand: put its loadout on, or hang it back.</summary>
        internal static void Equip(IStand stand)
        {
            IWearer wearer = Ready(stand);
            if (wearer == null || StandPersonal.Refused(stand))
            {
                return;   // someone else's personal stand: put on and hang back alike, whatever "Stands someone else saved" says
            }
            StandMark mark = StandMark.Read(stand);
            if (!mark.Marked)
            {
                Stands.Say($"This stand holds no loadout: {StandInput.Text(StandSettings.SaveKey.Value)} saves what you carry and wear to it.", true);
                return;
            }
            Loadout loadout = StandLoadouts.Of(mark, Stands.Me(), out bool mine);
            if (!mine && StandSettings.Others.Value == OthersStands.LeaveThemAlone)
            {
                Stands.Say($"This stand holds {Whose(mark)}{Named(mark)}: your settings leave others' stands alone.", true);
                return;
            }
            if (loadout == null)
            {
                Stands.Say($"This stand's loadout{Named(mark)} isn't in your list, and the stand carries no copy of it.", true);
                return;
            }
            if (loadout.Items.Count == 0)
            {
                // Nothing to put on: a put-on would only take things off him (review round 1 of Session 2, m2).
                Stands.Say($"{loadout.Name} is empty: put items into it in Outfitter's window ({StandInput.Text(Settings.WindowKey.Value)}) first.", true);
                return;
            }
            if (!wearer.CanEquipNow)
            {
                Stands.Say("Not now: you're busy (fighting, dodging, swimming or eating).", true);
                return;
            }
            EquipRules rules = Rules();
            List<StandPiece> pieces = StandPlan.Pieces(loadout, stand);
            if (StandPlan.Decide(stand, wearer, pieces, rules) == StandAction.HangBack)
            {
                HangBack(stand, loadout, pieces, rules, mine);
            }
            else
            {
                PutOn(stand, wearer, loadout, rules, mine, Mode(), null, null);
            }
        }

        // ---------- the window's Equip through a stand (decision 47) ----------

        /// <summary>
        /// The window's Equip on a loadout of his that's on one of his stands nearby ("Equip in the window puts a stand's
        /// loadout on at its stand"): what the put-on key does at that stand (WindowStand), without walking there, but only
        /// ever putting on, never hanging back, whatever the stand holds; in <paramref name="mode"/> (the window's mode when
        /// it shows its mode buttons, else the stand's "Equip mode at a stand": review round 1 of Session 3, j1); the chests found around the stand (its own chests first, by "Chests near the
        /// stand first"); his other stands lending as at the key; what he wore going home. True when a stand took it: the
        /// put-on started, or was refused with a message (said at the centre and to <paramref name="said"/>, the window's
        /// status line, which also gets "Putting ... on at ..." and, once it's over, the equip's own line). False when no
        /// stand applies (the setting off, the stands off, no stand of his near him holding it that he may use and that
        /// isn't someone else's personal stand): the window then runs its own equip.
        /// </summary>
        internal static bool EquipFromWindow(Loadout loadout, EquipMode mode, Action<string> said)
        {
            IWearer wearer = Outfit.WearerOf();
            IStand stand = wearer != null ? WindowStand(loadout, wearer.Position) : null;
            if (stand == null)
            {
                return false;
            }
            void Refused(string text)
            {
                Stands.Say(text, true);
                said?.Invoke(text);
            }
            if (Busy || Outfit.Busy || StandClaims.Busy)
            {
                Refused("Outfitter is busy: try again in a moment.");
                return true;
            }
            if (wearer.Unavailable)
            {
                Refused("Not now: you can't change just then.");
                return true;
            }
            if (loadout.Items.Count == 0)
            {
                Refused($"{loadout.Name} is empty: put items into it first.");
                return true;
            }
            if (!wearer.CanEquipNow)
            {
                Refused("Not now: you're busy (fighting, dodging, swimming or eating).");
                return true;
            }
            // The stand named as the window's button names it: its own name, else by its loadout (the last look's n4).
            string own = StandTitle.Read(stand);
            string at = own.Length > 0 ? own
                : loadout.Name.IndexOf("stand", StringComparison.OrdinalIgnoreCase) >= 0 ? loadout.Name : loadout.Name + "'s stand";
            said?.Invoke($"Putting {loadout.Name} on at {at}...");
            Loadout copy = loadout.Clone();   // read once, as the window's own equip reads its loadout
            PutOn(stand, wearer, copy, EquipRules.FromSettings(mode), mine: true, mode, stand.Position, said);
            return true;
        }

        /// <summary>The stand the window's Equip would put this loadout on at (decision 47), for the window to name it on
        /// its button: the nearest of his stands within "Stands listed within" of <paramref name="at"/> marked with it by
        /// him, that he may use and that isn't someone else's personal stand; null when there's none, or the setting or the
        /// stands are off.</summary>
        internal static IStand WindowStand(Loadout loadout, UnityEngine.Vector3 at)
        {
            if (loadout == null || StandSettings.WindowEquip == null || !StandSettings.WindowEquip.Value || !Stands.Active)
            {
                return null;
            }
            return StandEdits.StandOf(loadout, at, usableOnly: true);
        }

        /// <summary>
        /// Readies the window's preview request (Outfit.Preview) for an equip through a stand: when the window's Equip would
        /// go through one (WindowStand, for him where he stands now), the chests are found around that stand, its own chests
        /// first, and the pieces his stands would give this equip (the stand's own, and what his other stands would lend)
        /// are counted as there (EquipRequest.Lent: a "chest" the preview takes from first and never puts into). Returns the
        /// stand, or null with the request untouched.
        /// </summary>
        internal static IStand PreviewRequest(EquipRequest request)
        {
            IWearer wearer = Outfit.WearerOf();
            IStand stand = wearer != null && request?.Loadout != null ? WindowStand(request.Loadout, wearer.Position) : null;
            if (stand == null)
            {
                return null;
            }
            request.Centre = stand.Position;
            request.NearFirst = StandSettings.NearFirst.Value ? stand.Position : (UnityEngine.Vector3?)null;
            request.NearRange = StandSettings.NearRange.Value;
            List<ItemDrop.ItemData> lent = OnIt(stand);
            foreach (Loan loan in Loans(stand, wearer, request))
            {
                lent.Add(loan.Seen);
            }
            request.Lent = lent;
            return stand;
        }

        // The checks every key makes: the stands on, him there and able, nothing else under way, the stand his to use.
        private static IWearer Ready(IStand stand)
        {
            IWearer wearer = Outfit.WearerOf();
            if (!Stands.Active || stand == null || !stand.Valid || wearer == null || wearer.Unavailable)
            {
                return null;
            }
            if (Busy || Outfit.Busy || StandClaims.Busy)
            {
                Stands.Say("Outfitter is busy: try again in a moment.", true);
                return null;
            }
            bool built = StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt;
            if (!stand.MayUse(built, flash: true))
            {
                Stands.Say(built && stand.MayUse(false, flash: false) ? "You didn't build this stand (your settings: only stands you built)." : "You may not use this stand (a ward).", true);
                return null;
            }
            return wearer;
        }

        // The equip rules for a stand, by its mode setting.
        internal static EquipRules Rules() => EquipRules.FromSettings(Mode());

        internal static EquipMode Mode()
        {
            switch (StandSettings.Mode.Value)
            {
                case StandMode.FullyReplace:
                    return EquipMode.FullyReplace;
                case StandMode.SwapGearOnly:
                    return EquipMode.SwapGearOnly;
                case StandMode.SwapGearAndAddTheRest:
                    return EquipMode.SwapGearAndAddTheRest;
                default:
                    return Settings.Mode.Value;
            }
        }

        // The claim's "didn't get it" in words.
        private static string NotHeld(StandClaimResult held) =>
            held.Gone > 0 ? "The stand is gone." : "The stand didn't answer: someone else is using it. Try again in a moment.";

        // The same for one stand of a claim that asked for several (the stand he points at, with the stands lending to it).
        private static string NotHeld(IStand stand) =>
            !stand.Valid ? "The stand is gone." : "The stand didn't answer: someone else is using it. Try again in a moment.";

        // ---------- put it on ----------

        // The put-on, from the key (the stand's mode; the chests around him: centre null) or from the window (its mode; the
        // chests around the stand; told: its status line). His other stands' loans are worked out now and claimed with the
        // stand (decision 46).
        private static void PutOn(IStand stand, IWearer wearer, Loadout loadout, EquipRules rules, bool mine, EquipMode mode,
                                  UnityEngine.Vector3? centre, Action<string> told)
        {
            // Where what he wears goes, decided now, piece by piece (the request asks it of each piece at every plan).
            Dictionary<ItemDrop.ItemData, Home> home = Homes(stand, wearer, loadout, rules);
            List<Loan> loans = Loans(stand, wearer, Request(stand, loadout, mode, centre));
            var current = new Job { Told = told };
            current.Stands.Add(stand);
            var claiming = new List<IStand> { stand };
            foreach (Loan loan in loans)
            {
                if (!claiming.Contains(loan.From))
                {
                    claiming.Add(loan.From);
                    current.Stands.Add(loan.From);
                }
            }
            job = current;
            if (!StandClaims.Start(claiming, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    if (!held.Held.Contains(stand))
                    {
                        End();
                        Refuse(current, claiming.Count == 1 ? NotHeld(held) : NotHeld(stand));
                        return;
                    }
                    TakeAndBegin(current, stand, wearer, loadout, home, mine, mode, centre, rules, loans, held);
                }))
            {
                End();
                Refuse(current, "Outfitter is busy: try again in a moment.");
            }
        }

        // A refusal at the centre, and on the window's status line when the window started the job.
        private static void Refuse(Job current, string text)
        {
            Stands.Say(text, true);
            current?.Told?.Invoke(text);
        }

        // The equip request a stand's put-on makes (before the stand's own fields: what stays, what goes home): the loadout,
        // the mode, the chests found around the centre (null: around him), the stand's own chests first by the setting.
        private static EquipRequest Request(IStand stand, Loadout loadout, EquipMode mode, UnityEngine.Vector3? centre) => new EquipRequest
        {
            Loadout = loadout,
            Mode = mode,
            Centre = centre,
            NearFirst = StandSettings.NearFirst.Value ? stand.Position : (UnityEngine.Vector3?)null,
            NearRange = StandSettings.NearRange.Value,
            TakenOff = TakenOffGoes.Away,
            Source = "the stand",
        };

        // In the frame the stand is his: its gear to him (all or nothing), then what his other stands lend, then the equip.
        private static void TakeAndBegin(Job current, IStand stand, IWearer wearer, Loadout loadout, Dictionary<ItemDrop.ItemData, Home> home, bool mine,
                                         EquipMode mode, UnityEngine.Vector3? centre, EquipRules rules, List<Loan> loans, StandClaimResult held)
        {
            if (wearer.Gone || wearer.Unavailable)
            {
                End();
                return;
            }
            if (!StillMayUse(stand))
            {
                End();
                Refuse(current, "You may not use this stand any more (a ward).");
                return;
            }
            if (StandPersonal.Blocks(stand))
            {
                End();
                Refuse(current, StandPersonal.Refusal(stand));   // a friend made it theirs while the claim waited
                return;
            }
            Refresh(stand, loadout, mine);
            List<Moved> moved = StandMoves.TakeAll(stand, wearer.Inventory, out int needed, out int unreadable);
            if (moved == null)
            {
                End();
                stand.Send();
                Refuse(current, $"No room in your inventory for the stand's {Pieces(needed)}: make room, then try again.");
                return;
            }
            current.Moved = moved;
            stand.Send();
            StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
            if (unreadable > 0)
            {
                Stands.Say($"{Capital(Items(unreadable))} on the stand {(unreadable == 1 ? "isn't one" : "aren't ones")} the stands move (from a mod your game doesn't have, or another mod's stack): {(unreadable == 1 ? "it stays" : "they stay")} on it.", false);
            }
            Borrow(current, loans, held, wearer, rules);
            Carried(current, loadout, rules);
            // The stand's own gear never goes away in this equip: what he doesn't end up wearing goes back on the stand. So
            // does what his other stands lent, each onto its own stand. A piece the loadout carries stays with him.
            var standGear = new HashSet<ItemDrop.ItemData>();
            foreach (Moved one in moved)
            {
                if (Matching.Single(one.Item))
                {
                    standGear.Add(one.Item);
                }
            }
            foreach (Lent lent in current.Lent)
            {
                standGear.Add(lent.Moved.Item);
            }
            foreach (ItemDrop.ItemData item in home.Keys)
            {
                if (!Worn(wearer, item))
                {
                    current.HomeCarried.Add(item);   // a carried piece going home: never into a chest meanwhile
                    standGear.Add(item);
                }
            }
            EquipRequest request = Request(stand, loadout, mode, centre);
            // Both ask whether the stands are on and this is still their job: switched off meanwhile (even in the frame of
            // the switch, before SwitchedOff has run: the last look's n1), the equip goes on as Outfitter's own and keeps
            // nothing back for a stand that nothing will hang it on (harness area B).
            request.KeepsTakenOff = home.Count > 0 ? item => Stands.Active && job == current && home.ContainsKey(item) : (Func<ItemDrop.ItemData, bool>)null;
            request.Stays = standGear.Count > 0 ? item => Stands.Active && job == current && standGear.Contains(item) : (Func<ItemDrop.ItemData, bool>)null;
            current.Equipping = true;
            current.EquipSeen = Stands.Clock();
            if (!Outfit.Begin(request, result => Equipped(current, stand, wearer, home, result)))
            {
                current.Equipping = false;
                int back = PutBackNow(stand, moved, wearer);
                GiveBack(current.Lent, held, wearer, "went back onto", "stayed with you");
                End();
                Refuse(current, back == moved.Count
                    ? "Outfitter couldn't begin: the stand's gear is back on it."
                    : $"Outfitter couldn't begin: {Pieces(moved.Count - back)} of the stand's stayed with you.");
            }
        }

        // Which of what came off the stands answers a piece the loadout shows but doesn't wear (each piece once): the
        // loadout carries it, so it stays with him (0.5.0, his "Displayed" without "Worn": a build hammer on the stand). In
        // Swap gear only, only gear: the mode leaves the loadout's other items alone (review 1's m6).
        private static void Carried(Job current, Loadout loadout, EquipRules rules)
        {
            current.Loadout = loadout;
            current.Rules = rules;
            var pieces = loadout.Items.FindAll(item => item.Displayed && item.Worn == WornAs.None);
            if (pieces.Count == 0)
            {
                return;
            }
            bool Fetched(ItemDrop.ItemData item) => rules.Mode != EquipMode.SwapGearOnly || Matching.IsGear(item.m_shared.m_itemType, rules);
            var answered = new HashSet<LoadoutItem>();
            foreach (Lent lent in current.Lent)
            {
                LoadoutItem piece = lent.Loan.Piece?.Item;
                if (piece != null && pieces.Contains(piece) && Fetched(lent.Moved.Item) && answered.Add(piece))
                {
                    current.Carries[lent.Moved.Item] = piece;
                }
            }
            foreach (Moved one in current.Moved)
            {
                LoadoutItem piece = Fetched(one.Item) ? pieces.Find(each => !answered.Contains(each) && StandPlan.Answers(one.Item, each, rules)) : null;
                if (piece != null)
                {
                    answered.Add(piece);
                    current.Carries[one.Item] = piece;
                }
            }
        }

        // Whether a piece that came off a stand for one the loadout carries stays with him: unless he holds more copies of
        // it than the loadout holds of its prefab (his own hammer filled the loadout's cell: the stand's goes back, review 1's
        // m2).
        private static bool StaysCarried(Job current, IWearer wearer, ItemDrop.ItemData item)
        {
            if (!current.Carries.TryGetValue(item, out LoadoutItem piece) || current.Loadout == null)
            {
                return false;
            }
            int wanted = current.Loadout.Items.Count(each => each.Food == null && each.Prefab == piece.Prefab);
            int held = wearer.Inventory.GetAllItems().Count(each => each != null && each.m_stack > 0 && StandPlan.Answers(each, piece, current.Rules));
            return held <= wanted;
        }

        // ---------- what his other stands lend (decision 46) ----------

        /// <summary>
        /// What his other stands lend a put-on at this stand ("Take missing pieces from my other stands"): each of the
        /// loadout's pieces this stand can show that is neither on it, nor with him (carried or worn, as the equip would
        /// take it: StandPlan.WithHim), nor to be had from the chests (Outfitter's own preview of <paramref name="request"/>,
        /// the stand's own items counted as there: a piece whose want is still missing), from the nearest of his other
        /// stands within "Other stands lend within" of this one: marked by him (his own loadout's mark), his to use, not
        /// someone else's personal stand, and holding an item that answers the piece by this equip's rules and that the
        /// equip could put on him. Each item lends once. Worked out before the claim, from this computer's copies; checked
        /// again in the claim's frame (Borrow).
        /// </summary>
        internal static List<Loan> Loans(IStand stand, IWearer wearer, EquipRequest request)
        {
            var loans = new List<Loan>();
            Loadout loadout = request?.Loadout;
            if (!Stands.Active || StandSettings.Lend == null || !StandSettings.Lend.Value || stand == null || !stand.Valid || wearer == null ||
                loadout == null)
            {
                return loans;
            }
            EquipRules rules = EquipRules.FromSettings(request.Mode ?? Settings.Mode.Value);
            List<StandPiece> pieces = StandPlan.Pieces(loadout, stand);
            var onStand = new HashSet<StandPiece>(StandPlan.OnStand(stand, pieces, rules).Values);
            var withHim = new HashSet<StandPiece>(StandPlan.WithHim(wearer, pieces, rules, onStand).Values);
            var wanted = new List<StandPiece>();
            foreach (StandPiece piece in pieces)
            {
                if (!onStand.Contains(piece) && !withHim.Contains(piece))
                {
                    wanted.Add(piece);
                }
            }
            if (wanted.Count == 0)
            {
                return loans;
            }
            // The chests: what Outfitter's own preview of this equip still misses, the stand's own items counted as there
            // (they come to him first).
            EquipPlan plan = Outfit.Preview(Previewing(request, OnIt(stand)));
            if (plan == null)
            {
                return loans;
            }
            var missing = new List<StandPiece>();
            foreach (StandPiece piece in wanted)
            {
                foreach (Want want in plan.Wants)
                {
                    if (want.Items.Contains(piece.Item))
                    {
                        if (want.Missing > 0)
                        {
                            missing.Add(piece);
                        }
                        break;
                    }
                }
            }
            long me = Stands.Me();
            bool built = StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt;
            foreach (IStand other in missing.Count > 0 && me != 0L ? Stands.Nearby(stand.Position, StandSettings.LendRange.Value) : new List<IStand>())
            {
                if (missing.Count == 0)
                {
                    break;
                }
                if (other == null || !other.Valid || ReferenceEquals(other.Key, stand.Key) || !other.MayUse(built, flash: false) || StandPersonal.Blocks(other))
                {
                    continue;   // never a stand he may not use, nor someone else's personal stand
                }
                StandMark mark = StandMark.Read(other);
                if (!mark.Marked || mark.By != me)
                {
                    continue;   // only his own stands lend (a match can't tell his gear from a friend's: decision 30)
                }
                string where = null;
                for (int place = 0; place < other.Places && missing.Count > 0; place++)
                {
                    if (!other.Has(place))
                    {
                        continue;
                    }
                    ItemDrop.ItemData item = other.Read(place);
                    if (!StandMoves.OneOf(item) || !StandPlan.Wearable(item, rules))
                    {
                        continue;
                    }
                    foreach (StandPiece piece in missing)
                    {
                        if (StandPlan.Answers(item, piece.Item, rules))
                        {
                            loans.Add(new Loan { From = other, Place = place, Piece = piece, Seen = item, Mark = mark, Where = where ??= Called(other, mark) });
                            missing.Remove(piece);
                            break;
                        }
                    }
                }
            }
            return loans;
        }

        // The items on a stand the stands move (one of each, this game's), as read now.
        internal static List<ItemDrop.ItemData> OnIt(IStand stand)
        {
            var items = new List<ItemDrop.ItemData>();
            for (int place = 0; stand != null && stand.Valid && place < stand.Places; place++)
            {
                ItemDrop.ItemData item = stand.Has(place) ? stand.Read(place) : null;
                if (StandMoves.OneOf(item))
                {
                    items.Add(item);
                }
            }
            return items;
        }

        // The same equip, for Outfitter's preview only, with these items held for it elsewhere (EquipRequest.Lent).
        private static EquipRequest Previewing(EquipRequest request, List<ItemDrop.ItemData> lent) => new EquipRequest
        {
            Loadout = request.Loadout,
            Mode = request.Mode,
            Missing = request.Missing,
            Centre = request.Centre,
            NearFirst = request.NearFirst,
            NearRange = request.NearRange,
            TakenOff = request.TakenOff,
            Source = request.Source,
            Lent = lent,
        };

        // A stand as his messages name it: its own name; else the loadout it holds ("Stand 2", or "Iron set's stand").
        private static string Called(IStand stand, StandMark mark)
        {
            string own = StandTitle.Read(stand);
            if (own.Length > 0)
            {
                return own;
            }
            if (mark.Name.Length == 0)
            {
                return "another of your stands";
            }
            return mark.Name.IndexOf("stand", StringComparison.OrdinalIgnoreCase) >= 0 ? mark.Name : mark.Name + "'s stand";
        }

        // In the claim's frame, after this stand's own gear came to him: each loan off its stand into his inventory, if that
        // stand is held, he may still use it, it isn't someone else's personal stand now, its mark is the one read before
        // the claim, and the place still holds an item answering the piece; measured (StandMoves.TakeOne). A piece without
        // room stays on its stand, as does one whose stand refused; each said. Then one line per stand that lent.
        private static void Borrow(Job current, List<Loan> loans, StandClaimResult held, IWearer wearer, EquipRules rules)
        {
            var came = new List<Lent>();
            foreach (Loan loan in loans)
            {
                IStand from = loan.From;
                string name = Messages.NameOf(loan.Piece.Item);
                string why = null;
                if (!held.Held.Contains(from))
                {
                    why = from.Valid ? "the stand didn't answer" : "the stand is gone";
                }
                else if (!StillMayUse(from))
                {
                    why = "you may not use that stand any more";
                }
                else if (StandPersonal.Blocks(from))
                {
                    why = "it's someone else's personal stand now";
                }
                else if (!StandMark.Read(from).SameAs(loan.Mark))
                {
                    why = "that stand changed meanwhile";
                }
                else
                {
                    ItemDrop.ItemData there = from.Has(loan.Place) ? from.Read(loan.Place) : null;
                    if (!StandMoves.OneOf(there) || !StandPlan.Answers(there, loan.Piece.Item, rules))
                    {
                        why = "it isn't there any more";
                    }
                }
                if (why != null)
                {
                    Stands.Say($"{Capital(name)} wasn't lent by {loan.Where} ({why}).", false);
                    continue;
                }
                Moved one = StandMoves.TakeOne(from, loan.Place, wearer.Inventory);
                if (one == null)
                {
                    Stands.Say($"No room in your inventory for {name} from {loan.Where}: it stays there.", false);
                    continue;
                }
                from.Send();
                StandClaims.Hold(from, StandSettings.HoldSeconds.Value);
                var lent = new Lent { Loan = loan, Moved = one };
                current.Lent.Add(lent);
                came.Add(lent);
            }
            foreach (List<Lent> fromOne in ByStand(came))
            {
                Stands.Say($"{Capital(Names(fromOne))} came off {fromOne[0].Loan.Where}.", false);
            }
        }

        // Lent pieces back onto the stands they came from, each into its own place (or where the game would hang it), in the
        // frame those stands are his: each stand held, his to use, not someone else's personal stand now. What can't go back
        // stays with him (never lost); each stand's line says which ("Lox cape went back onto Stand 2.").
        private static void GiveBack(List<Lent> pieces, StandClaimResult held, IWearer wearer, string wentWords, string stayedWords)
        {
            var back = new List<Lent>();
            var changed = new List<IStand>();
            foreach (Lent lent in pieces)
            {
                IStand to = lent.Loan.From;
                string name = Messages.NameOf(lent.Loan.Piece.Item);
                string why = !held.Held.Contains(to) ? (to.Valid ? "its stand didn't answer" : "its stand is gone") :
                             !StillMayUse(to) ? "you may not use its stand any more" :
                             StandPersonal.Blocks(to) ? "its stand is someone else's personal stand now" : null;
                if (why == null && !wearer.Gone && StandMoves.PutBack(to, lent.Moved, wearer))
                {
                    back.Add(lent);
                    if (!changed.Contains(to))
                    {
                        changed.Add(to);
                    }
                    continue;
                }
                if (!wearer.Gone)
                {
                    Stands.Say($"{Capital(name)} {stayedWords} ({why ?? "its place on its stand was taken"}).", false);
                }
            }
            foreach (IStand to in changed)
            {
                to.Send();
                StandClaims.Hold(to, StandSettings.HoldSeconds.Value);
            }
            foreach (List<Lent> toOne in ByStand(back))
            {
                Stands.Say($"{Capital(Names(toOne))} {wentWords} {toOne[0].Loan.Where}.", false);
            }
        }

        // Lent pieces grouped by the stand they came from, in the order they came.
        private static List<List<Lent>> ByStand(List<Lent> pieces)
        {
            var groups = new List<List<Lent>>();
            foreach (Lent lent in pieces)
            {
                List<Lent> group = groups.Find(each => ReferenceEquals(each[0].Loan.From.Key, lent.Loan.From.Key));
                if (group == null)
                {
                    groups.Add(group = new List<Lent>());
                }
                group.Add(lent);
            }
            return groups;
        }

        // "Lox cape", "Lox cape and Bronze helmet", "Lox cape, Bronze helmet and Iron sword".
        private static string Names(List<Lent> pieces)
        {
            var names = pieces.ConvertAll(each => Messages.NameOf(each.Loan.Piece.Item));
            return names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        // Outfitter's equip is over (in its own frame, inside its guard).
        private static void Equipped(Job current, IStand stand, IWearer wearer, Dictionary<ItemDrop.ItemData, Home> home, EquipResult result)
        {
            if (job != current)
            {
                return;
            }
            current.Equipping = false;
            current.Told?.Invoke(result.Line);   // the window's status line: the equip's own line
            if (!Stands.Active)
            {
                End();
                Stands.Say("Loadout Stands was switched off before the stand's part was done: nothing more went onto a stand.", false);
                return;
            }
            if (wearer.Gone)
            {
                End();   // died or left meanwhile: what he carried is where the game put it
                return;
            }
            if (result.Cancelled)
            {
                // Nothing of the equip moved: the stand's gear goes back on it, and each lent piece onto its own stand.
                var backTo = new List<IStand> { stand };
                foreach (Lent lent in current.Lent)
                {
                    if (!backTo.Contains(lent.Loan.From))
                    {
                        backTo.Add(lent.Loan.From);
                    }
                }
                if (!StandClaims.Start(backTo, held =>
                    {
                        bool his = held.Held.Contains(stand);
                        bool may = his && StillMayUse(stand);
                        bool open = may && !StandPersonal.Blocks(stand);
                        int back = open && !wearer.Gone ? PutBackNow(stand, current.Moved, wearer) : 0;
                        GiveBack(current.Lent, held, wearer, "went back onto", "stayed with you");
                        End();
                        int stayed = current.Moved.Count - back;
                        if (stayed > 0 && !wearer.Gone)
                        {
                            string why = !his ? (backTo.Count == 1 ? (held.Gone > 0 ? "the stand is gone" : "the stand didn't answer") : !stand.Valid ? "the stand is gone" : "the stand didn't answer") :
                                         !may ? "you may not use the stand any more" : !open ? "it's someone else's personal stand now" : "its place on the stand was taken";
                            Stands.Say($"The stand's {Pieces(stayed)} stayed with you ({why}).", false);
                        }
                    }))
                {
                    End();
                    Stands.Say($"The stand's {Pieces(current.Moved.Count + current.Lent.Count)} stayed with you (Outfitter was busy).", false);
                }
                return;
            }
            // What he took off and kept goes onto its stand, each into its place; then what came off this stand and he
            // doesn't wear (a piece the game won't put on him: broken, say) goes back on it.
            var going = new Dictionary<IStand, List<KeyValuePair<ItemDrop.ItemData, Home>>>();
            int kept = 0;
            // What he carried that another of his stands shows goes home too, while it's still with him and not worn (m3).
            IEnumerable<ItemDrop.ItemData> homing = result.TakenOff.Concat(current.HomeCarried.Where(item =>
                !result.TakenOff.Contains(item) && wearer.Inventory.ContainsItem(item) && !Worn(wearer, item)));
            foreach (ItemDrop.ItemData item in homing)
            {
                if (home.TryGetValue(item, out Home to))
                {
                    if (!going.TryGetValue(to.Stand, out List<KeyValuePair<ItemDrop.ItemData, Home>> list))
                    {
                        going[to.Stand] = list = new List<KeyValuePair<ItemDrop.ItemData, Home>>();
                    }
                    list.Add(new KeyValuePair<ItemDrop.ItemData, Home>(item, to));
                    kept++;
                }
            }
            var leftover = new List<Moved>();
            foreach (Moved one in current.Moved)
            {
                if (Matching.Single(one.Item) && wearer.Inventory.ContainsItem(one.Item) && !Worn(wearer, one.Item) && !StaysCarried(current, wearer, one.Item))
                {
                    leftover.Add(one);
                }
            }
            // What his other stands lent and he doesn't wear goes back onto the stand it came from (Stays kept it from the
            // chests).
            var lentBack = current.Lent.FindAll(lent =>
                Matching.Single(lent.Moved.Item) && wearer.Inventory.ContainsItem(lent.Moved.Item) && !Worn(wearer, lent.Moved.Item) &&
                !StaysCarried(current, wearer, lent.Moved.Item));
            var claiming = new List<IStand>(going.Keys);
            if (leftover.Count > 0 && !going.ContainsKey(stand))
            {
                claiming.Add(stand);
            }
            foreach (Lent lent in lentBack)
            {
                if (!claiming.Contains(lent.Loan.From))
                {
                    claiming.Add(lent.Loan.From);
                }
            }
            if (claiming.Count == 0)
            {
                End();
                return;
            }
            current.Stands.AddRange(claiming);
            current.Homing = kept;
            current.Leftover = leftover.Count + lentBack.Count;
            if (!StandClaims.Start(claiming, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    var tally = new Tally();
                    foreach (KeyValuePair<IStand, List<KeyValuePair<ItemDrop.ItemData, Home>>> each in going)
                    {
                        IStand to = each.Key;
                        bool his = held.Held.Contains(to) && !wearer.Gone;
                        foreach (KeyValuePair<ItemDrop.ItemData, Home> piece in each.Value)
                        {
                            if (!his)
                            {
                                tally.NoAnswer++;
                                continue;
                            }
                            HangOne(to, piece.Key, piece.Value.Place, wearer, tally);
                        }
                    }
                    int back = 0;
                    if (held.Held.Contains(stand) && !wearer.Gone && StillMayUse(stand) && !StandPersonal.Blocks(stand))
                    {
                        foreach (Moved one in leftover)
                        {
                            if (!Worn(wearer, one.Item) && StandMoves.PutBack(stand, one, wearer))
                            {
                                back++;
                            }
                        }
                    }
                    GiveBack(lentBack.FindAll(lent => !Worn(wearer, lent.Moved.Item)), held, wearer, "you couldn't put on went back onto",
                             "you couldn't put on stayed with you");
                    foreach (IStand to in held.Held)
                    {
                        to.Send();
                        StandClaims.Hold(to, StandSettings.HoldSeconds.Value);
                    }
                    End();
                    bool onThis = going.Count == 1 && going.ContainsKey(stand);
                    if (tally.Hung > 0)
                    {
                        Stands.Say(onThis ? $"What you wore went onto the stand ({Pieces(tally.Hung)})." : $"What you wore went back onto its stand ({Pieces(tally.Hung)}).", false);
                    }
                    tally.Tell("of what you wore");
                    if (back > 0)
                    {
                        Stands.Say($"{Capital(Pieces(back))} you couldn't put on went back on the stand.", false);
                    }
                    if (leftover.Count > back)
                    {
                        Stands.Say($"{Capital(Pieces(leftover.Count - back))} of the stand's that you couldn't put on stayed with you.", false);
                    }
                }))
            {
                End();
                Stands.Say("What you wore stayed with you (Outfitter was busy).", false);
            }
        }

        // Whether he wears an item now (a hand item the game has put away for a moment counts), or has it queued to go on.
        private static bool Worn(IWearer wearer, ItemDrop.ItemData item) =>
            wearer.WornAs(item) != WornAs.None || ReferenceEquals(item, wearer.HiddenRight) || ReferenceEquals(item, wearer.HiddenLeft) ||
            wearer.Queued(item);

        /// <summary>What became of the pieces meant for a stand, for his messages.</summary>
        private sealed class Tally
        {
            internal int Hung;
            internal int NoAnswer;
            internal int NoPlace;
            internal int NotWithHim;
            internal int Refused;
            internal int Blocked;
            internal int NoRoom;
            internal int Strays;

            // One line per outcome that happened (centre false: what happened).
            internal void Tell(string what)
            {
                if (NoAnswer > 0)
                {
                    Stands.Say($"{Capital(Pieces(NoAnswer))} {what} stayed with you (its stand didn't answer, or is gone).", false);
                }
                if (NoPlace > 0)
                {
                    Stands.Say($"{Capital(Pieces(NoPlace))} {what} stayed with you (no place for {(NoPlace == 1 ? "it" : "them")} on the stand).", false);
                }
                if (NotWithHim > 0)
                {
                    Stands.Say($"{Capital(Pieces(NotWithHim))} {what} {(NotWithHim == 1 ? "wasn't" : "weren't")} with you any more to hang up.", false);
                }
                if (Refused > 0)
                {
                    Stands.Say($"{Capital(Pieces(Refused))} {what} stayed with you (you may not use {(Refused == 1 ? "its" : "their")} stand now).", false);
                }
                if (Blocked > 0)
                {
                    Stands.Say($"{Capital(Pieces(Blocked))} {what} stayed with you (what's on the stand in {(Blocked == 1 ? "its place" : "their places")} can't come off: no room in your inventory, or another mod's item).", false);
                }
                if (NoRoom > 0)
                {
                    Stands.Say($"{Capital(Items(NoRoom))} stayed on the stand (no room in your inventory).", false);
                }
                if (Strays > 0)
                {
                    Stands.Say($"{Capital(Items(Strays))} that {(Strays == 1 ? "was" : "were")} on the stand came to you.", false);
                }
            }
        }

        // One piece onto a stand, into its place if still open, else where the game would hang it if open.
        private static void HangOne(IStand stand, ItemDrop.ItemData item, int place, IWearer wearer, Tally tally)
        {
            if (!wearer.Inventory.ContainsItem(item))
            {
                tally.NotWithHim++;
                return;
            }
            if (!stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: false) || StandPersonal.Blocks(stand))
            {
                tally.Refused++;   // a ward went up meanwhile, or a friend made it theirs
                return;
            }
            if (!StandPlan.Open(stand, place, Kept(stand)))
            {
                place = stand.FitFor(item);
                if (place < 0 || !StandPlan.Open(stand, place, Kept(stand)))
                {
                    tally.NoPlace++;
                    return;
                }
            }
            if (StandMoves.Hang(stand, place, item, wearer, Settings.Effects.Value, out Moved stray, out bool blocked))
            {
                tally.Hung++;
            }
            else if (blocked)
            {
                tally.Blocked++;
            }
            else
            {
                tally.NoPlace++;
            }
            if (stray != null)
            {
                tally.Strays++;
            }
        }

        // The places of a stand that hold a piece of its own loadout: never emptied for something else.
        private static HashSet<int> Kept(IStand stand)
        {
            var kept = new HashSet<int>();
            StandMark mark = StandMark.Read(stand);
            Loadout loadout = mark.Marked ? StandLoadouts.Of(mark, Stands.Me(), out _) : null;
            if (loadout != null)
            {
                foreach (int place in StandPlan.OnStand(stand, StandPlan.Pieces(loadout, stand), Rules()).Keys)
                {
                    kept.Add(place);
                }
            }
            return kept;
        }

        // Puts the stand's own gear back on it, in this frame: how many went back.
        private static int PutBackNow(IStand stand, List<Moved> moved, IWearer wearer)
        {
            int back = 0;
            foreach (Moved one in moved)
            {
                if (StandMoves.PutBack(stand, one, wearer))
                {
                    back++;
                }
            }
            stand.Send();
            StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
            return back;
        }

        /// <summary>Where one of his worn pieces goes when it comes off: a stand, and the place there.</summary>
        internal sealed class Home
        {
            internal IStand Stand;
            internal int Place;
        }

        /// <summary>
        /// Where each piece he wears goes when the loadout takes it off (decision 11): onto this stand ("onto this stand",
        /// each into the place the game would hang it in, the first piece for each place), or home: a piece of another
        /// marked stand's loadout, onto that stand when it's within "Its own stand counts within" (the nearest first), into
        /// its place there while open. Pieces the loadout itself wears are left out (they stay on him). What isn't listed
        /// goes where Outfitter sends gear. Going home, not onto this stand: a piece he carries that another of his stands
        /// shows but doesn't wear (0.5.0's Displayed, not Worn), unless the loadout holds it (review 1's m3).
        /// </summary>
        internal static Dictionary<ItemDrop.ItemData, Home> Homes(IStand stand, IWearer wearer, Loadout loadout, EquipRules rules)
        {
            var homes = new Dictionary<ItemDrop.ItemData, Home>();
            var leaving = new List<ItemDrop.ItemData>();
            var answered = new HashSet<LoadoutItem>();
            foreach (ItemDrop.ItemData item in StandPlan.Worn(wearer))
            {
                bool stays = false;
                foreach (LoadoutItem piece in loadout.Items)
                {
                    if (piece.Worn != WornAs.None && !answered.Contains(piece) && StandPlan.Answers(item, piece, rules))
                    {
                        answered.Add(piece);
                        stays = true;
                        break;
                    }
                }
                if (!stays)
                {
                    leaving.Add(item);
                }
            }
            // What he carries goes home only where it would go away (Fully replace; the swap modes leave it with him), and never
            // a favourite the loadout keeps with him.
            var carried = new List<ItemDrop.ItemData>();
            var worn = new HashSet<ItemDrop.ItemData>(StandPlan.Worn(wearer));
            Func<ItemDrop.ItemData, bool> favourite = Exchange.FavouriteOf();
            foreach (ItemDrop.ItemData item in rules.Mode == EquipMode.FullyReplace ? wearer.Inventory.GetAllItemsInGridOrder() : new List<ItemDrop.ItemData>())
            {
                if (item == null || item.m_stack <= 0 || worn.Contains(item) || !Matching.Single(item) || item.m_shared.m_questItem)
                {
                    continue;
                }
                if (!rules.FavouritesGoFor(loadout) && rules.LeaveFavourites && favourite != null && favourite(item) &&
                    !(rules.FavouriteGearSwaps && Matching.IsGear(item.m_shared.m_itemType, rules)))
                {
                    continue;   // a favourite that stays with him (EquipPlanner.Protected's rule)
                }
                // Never a kind the loadout holds (the planner picks which copy it keeps, by its own rules: the last look's L1),
                // nor what sits in one of EquipmentAndQuickSlots' quick slots while Fully replace leaves those alone (L2).
                string prefab = Capture.PrefabOf(item);
                if (loadout.Items.Exists(li => li.Food == null && li.Prefab == prefab))
                {
                    continue;
                }
                string slot = wearer.Grid?.SlotAt(item.m_gridPos.x, item.m_gridPos.y);
                if (!rules.QuickSlotsInFullyReplace && slot != null && wearer.Grid.Slots[slot].Quick)
                {
                    continue;
                }
                carried.Add(item);
            }
            if (leaving.Count == 0 && carried.Count == 0)
            {
                return homes;
            }
            if (StandSettings.WhatIWore.Value == WhatIWoreGoes.OntoThisStand)
            {
                foreach (KeyValuePair<ItemDrop.ItemData, int> each in StandPlan.Assign(stand, leaving, keep: null))
                {
                    homes[each.Key] = new Home { Stand = stand, Place = each.Value };
                }
                return homes;
            }
            long me = Stands.Me();
            bool built = StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt;
            foreach (IStand other in Stands.Nearby(wearer.Position, StandSettings.HomeRange.Value))
            {
                if (other == null || other.Key == stand.Key || !other.Valid || !other.MayUse(built, flash: false) || StandPersonal.Blocks(other))
                {
                    continue;   // never a stand he may not use (a ward, or by "Which stands"): the harness, area A; nor someone else's personal stand
                }
                StandMark mark = StandMark.Read(other);
                bool own = false;
                Loadout theirs = mark.Marked ? StandLoadouts.Of(mark, me, out own) : null;
                if (theirs == null || !own)
                {
                    continue;   // only his own stands are homes: a match can't tell his gear from a friend's (review round 2, j2)
                }
                List<StandPiece> pieces = StandPlan.Pieces(theirs, other);
                Dictionary<int, StandPiece> onIt = StandPlan.OnStand(other, pieces, rules);
                var free = new List<StandPiece>();
                foreach (StandPiece piece in pieces)
                {
                    if (!onIt.ContainsValue(piece))
                    {
                        free.Add(piece);
                    }
                }
                var taken = new HashSet<int>(onIt.Keys);
                foreach (ItemDrop.ItemData item in leaving.Concat(carried))
                {
                    if (homes.ContainsKey(item))
                    {
                        continue;
                    }
                    bool wears = worn.Contains(item);
                    foreach (StandPiece piece in free)
                    {
                        int place = other.FitFor(item);
                        if (place >= 0 && !taken.Contains(place) && (wears || piece.Item.Worn == WornAs.None) && StandPlan.Answers(item, piece.Item, rules) &&
                            StandPlan.Open(other, place, onIt.Keys))
                        {
                            homes[item] = new Home { Stand = other, Place = place };
                            taken.Add(place);
                            free.Remove(piece);
                            break;
                        }
                    }
                }
            }
            return homes;
        }

        // ---------- hang it back ----------

        private static void HangBack(IStand stand, Loadout loadout, List<StandPiece> pieces, EquipRules rules, bool mine)
        {
            var current = new Job();
            current.Stands.Add(stand);
            job = current;
            if (!StandClaims.Start(new[] { stand }, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    End();
                    if (!held.Held.Contains(stand))
                    {
                        Stands.Say(NotHeld(held), true);
                        return;
                    }
                    IWearer wearer = Outfit.WearerOf();
                    if (wearer == null || wearer.Unavailable)
                    {
                        return;
                    }
                    if (!StillMayUse(stand))
                    {
                        Stands.Say("You may not use this stand any more (a ward).", true);
                        return;
                    }
                    if (StandPersonal.Refused(stand))
                    {
                        return;   // a friend made it theirs while the claim waited
                    }
                    Refresh(stand, loadout, mine);
                    var tally = new Tally();
                    HangNow(stand, wearer, pieces, rules, tally);
                    stand.Send();
                    StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
                    if (tally.Hung > 0)
                    {
                        Stands.Say($"{loadout.Name}: back on its stand ({Pieces(tally.Hung)}).", false);
                    }
                    else if (tally.NoPlace + tally.Blocked == 0)
                    {
                        Stands.Say($"You wear nothing of {loadout.Name} to hang back.", true);
                    }
                    tally.Tell("of it");
                }))
            {
                End();
                Stands.Say("Outfitter is busy: try again in a moment.", true);
            }
        }

        // In a frame the stand is his: every piece of its loadout he wears onto it (decision 12), each into its place; an
        // item in a place it needs comes to him first. With "onto this stand" (a plain swap) the swap goes both ways: what
        // else the stand holds comes to him too, and what came off it is put on him (his outfit, swapped there last time).
        private static void HangNow(IStand stand, IWearer wearer, List<StandPiece> pieces, EquipRules rules, Tally tally)
        {
            bool swap = StandSettings.WhatIWore.Value == WhatIWoreGoes.OntoThisStand;
            var came = new List<ItemDrop.ItemData>();
            Dictionary<int, StandPiece> onStand = StandPlan.OnStand(stand, pieces, rules);
            Dictionary<ItemDrop.ItemData, StandPiece> onHim = StandPlan.OnHim(wearer, pieces, rules, onStand.Values);
            Dictionary<ItemDrop.ItemData, int> places = StandPlan.Assign(stand, onHim.Keys, onStand.Keys);
            var hungNow = new HashSet<int>();
            foreach (ItemDrop.ItemData item in onHim.Keys)
            {
                Moved stray = null;
                bool blocked = false;
                if (places.TryGetValue(item, out int place) && StandMoves.Hang(stand, place, item, wearer, Settings.Effects.Value, out stray, out blocked))
                {
                    tally.Hung++;
                    hungNow.Add(place);
                }
                else if (blocked)
                {
                    tally.Blocked++;
                }
                else
                {
                    tally.NoPlace++;
                }
                if (stray != null)
                {
                    came.Add(stray.Item);
                    if (!swap)
                    {
                        tally.Strays++;
                    }
                }
            }
            if (!swap)
            {
                return;
            }
            for (int place = 0; place < stand.Places; place++)
            {
                if (hungNow.Contains(place) || onStand.ContainsKey(place) || !stand.Has(place))
                {
                    continue;
                }
                Moved one = StandMoves.TakeOne(stand, place, wearer.Inventory);
                if (one != null)
                {
                    came.Add(one.Item);
                }
                else if (StandMoves.OneOf(stand.Read(place)))
                {
                    tally.NoRoom++;   // no room for it: it stays on the stand, said (review round 2, n3)
                }
            }
            int notOn = PutOnHim(wearer, came);
            if (notOn > 0)
            {
                Stands.Say($"{Capital(Pieces(notOn))} that came off the stand {(notOn == 1 ? "is" : "are")} in your inventory: you were busy, put {(notOn == 1 ? "it" : "them")} on by hand.", false);
            }
        }

        // What came off the stand in a plain swap, put on him where nothing else is worn: right-hand pieces first, then the
        // left hand (so a torch goes beside a one-handed weapon, as the game's hands go), then the rest. While he can't change
        // (fighting, dodging), nothing is put on: how many weren't.
        private static int PutOnHim(IWearer wearer, List<ItemDrop.ItemData> items)
        {
            if (items.Count == 0)
            {
                return 0;
            }
            if (!wearer.CanEquipNow)
            {
                return items.Count;
            }
            int Rank(ItemDrop.ItemData item)
            {
                switch (item.m_shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    case ItemDrop.ItemData.ItemType.Tool:
                        return 0;
                    case ItemDrop.ItemData.ItemType.Shield:
                    case ItemDrop.ItemData.ItemType.Bow:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    case ItemDrop.ItemData.ItemType.Torch:
                        return 1;
                    default:
                        return 2;
                }
            }
            var ordered = new List<ItemDrop.ItemData>(items);
            ordered.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            foreach (ItemDrop.ItemData item in ordered)
            {
                if (!wearer.Inventory.ContainsItem(item) || !item.IsEquipable())
                {
                    continue;
                }
                WornAs body = Matching.BodyPlace(item.m_shared.m_itemType);
                if (body != WornAs.None && body != WornAs.Ammo && Wears(wearer, body))
                {
                    continue;
                }
                if (body == WornAs.None && TakesAHandOff(wearer, item))
                {
                    continue;
                }
                wearer.Equip(item, Settings.Effects.Value);
            }
            return 0;
        }

        // Whether putting a hand item on would take off something in his hands, by the game's own hand rules
        // (Humanoid.EquipItem, 1.0.17): a torch goes beside a one-handed weapon, or into the right hand beside a shield; a
        // shield beside a one-handed weapon or a torch; a one-handed weapon beside a shield or a torch (a torch in the right
        // hand moving left for it); anything else needs both hands free. So a torch he wore beside his shield goes back on
        // beside it (harness area B). Any hand item put on makes the game forget what he has put away (at a workbench, by
        // the hide key, swimming, sitting), so while anything is put away nothing goes into his hands (the last look's m1).
        private static bool TakesAHandOff(IWearer wearer, ItemDrop.ItemData item)
        {
            if (wearer.HiddenRight != null || wearer.HiddenLeft != null)
            {
                return true;
            }
            ItemDrop.ItemData.ItemType? right = wearer.RightItem?.m_shared.m_itemType;
            ItemDrop.ItemData.ItemType? left = wearer.LeftItem?.m_shared.m_itemType;
            const ItemDrop.ItemData.ItemType oneHanded = ItemDrop.ItemData.ItemType.OneHandedWeapon;
            const ItemDrop.ItemData.ItemType torch = ItemDrop.ItemData.ItemType.Torch;
            const ItemDrop.ItemData.ItemType shield = ItemDrop.ItemData.ItemType.Shield;
            switch (item.m_shared.m_itemType)
            {
                case torch:
                    return !((right == oneHanded && left == null) || (right == null && (left == null || left == shield)));
                case shield:
                    return !(left == null && (right == null || right == oneHanded || right == torch));
                case oneHanded:
                    return !((right == null || (right == torch && left == null)) && (left == null || left == shield || left == torch));
                default:
                    return right != null || left != null;
            }
        }

        private static bool Wears(IWearer wearer, WornAs place)
        {
            foreach (ItemDrop.ItemData item in wearer.Inventory.GetAllItems())
            {
                if (wearer.WornAs(item) == place)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------- the save key ----------

        /// <summary>The save key at a stand: save what he carries and wears to it (asking first over a loadout already
        /// there, and offering to unmark his own). Every answer first checks the stands still work (M4).</summary>
        internal static void Save(IStand stand)
        {
            IWearer wearer = Ready(stand);
            if (wearer == null || StandPersonal.Refused(stand))
            {
                return;
            }
            StandMark mark = StandMark.Read(stand);
            if (!mark.Marked)
            {
                Name(stand, mark);
                return;
            }
            Loadout loadout = StandLoadouts.Of(mark, Stands.Me(), out bool mine);
            bool hangsNow = StandSettings.Saving.Value == StandSaving.RecordsItAndHangsMyGearNow;
            if (mine)
            {
                string name = loadout != null ? loadout.Name : mark.Name;
                int slots = loadout != null ? loadout.FoodSlots : 0;
                string keeps = slots > 0 ? $" Its {slots} food slot{(slots == 1 ? "" : "s")} stay{(slots == 1 ? "s" : "")}." : string.Empty;
                Ask(stand, $"This stand holds your loadout {name}.\n\nSave what you carry and wear now over it?{keeps}", "Save over it", "No", yes =>
                {
                    if (yes)
                    {
                        Record(stand, mark, mark.Id, name, saveOver: true, was: loadout);
                        return;
                    }
                    Ask(stand, $"Make it an ordinary stand again instead?\n\nIts gear stays on it, and {name} stays in your list.", "Unmark it", "Cancel", unmark =>
                    {
                        if (unmark)
                        {
                            Unmark(stand, mark, name);
                        }
                    });
                });
                return;
            }
            string theirs = hangsNow ? " Where your gear goes on, theirs comes to you." : string.Empty;
            Ask(stand, $"This stand holds {Whose(mark)}{Named(mark)}.\n\nSave yours over it? It becomes your stand.{theirs}", "Save over it", "Cancel", yes =>
            {
                if (yes)
                {
                    Name(stand, mark);
                }
            });
        }

        // A new stand loadout's name: asked (a name filled in), or that name. The mark he saw goes along, so a change to it
        // meanwhile is caught (Record).
        private static void Name(IStand stand, StandMark seen)
        {
            string filled = NextName();
            if (StandSettings.Naming.Value == StandNaming.UseTheNameFilledIn)
            {
                Record(stand, seen, null, filled, saveOver: false);
                return;
            }
            AskName(stand, "Name this stand's loadout", filled, name => Record(stand, seen, null, name, saveOver: false));
        }

        // Whether the stand's mark is still the one he saw at the key press; if not, he's told and nothing changes (another
        // player saved to it, or unmarked it, while his question or name box showed: harness area B).
        internal static bool StillSeen(IStand stand, StandMark seen)
        {
            if (StandMark.Read(stand).SameAs(seen))
            {
                return true;
            }
            Stands.Say("This stand's loadout changed meanwhile (someone else saved to it, or unmarked it): nothing changed. Point at it to see it now.", true);
            return false;
        }

        /// <summary>"Stand 1", "Stand 2" …: the first not among his loadouts' names.</summary>
        internal static string NextName()
        {
            var names = new HashSet<string>();
            foreach (Loadout each in Store.All)
            {
                names.Add(each.Name);
            }
            int n = 1;
            while (names.Contains("Stand " + n))
            {
                n++;
            }
            return "Stand " + n;
        }

        // Claims the stand; in that frame, takes what he carries and wears (by the setting), keeps it in his list (as a new
        // loadout, or over the stand's own, same Id and name), and marks the stand; by the setting, hangs his pieces on it.
        private static void Record(IStand stand, StandMark seen, string id, string name, bool saveOver, Loadout was = null)
        {
            if (!Stands.Active)
            {
                return;
            }
            if (Busy || Outfit.Busy || StandClaims.Busy)
            {
                Stands.Say("Outfitter is busy: try again in a moment.", true);
                return;
            }
            var current = new Job();
            current.Stands.Add(stand);
            job = current;
            if (!StandClaims.Start(new[] { stand }, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    End();
                    if (!held.Held.Contains(stand))
                    {
                        Stands.Say(NotHeld(held), true);
                        return;
                    }
                    if (!StillMayUse(stand))
                    {
                        Stands.Say("You may not use this stand any more (a ward).", true);
                        return;
                    }
                    if (StandPersonal.Refused(stand) || !StillSeen(stand, seen))
                    {
                        return;
                    }
                    IWearer wearer = Outfit.WearerOf();
                    Loadout loadout = wearer != null && !wearer.Unavailable ? Stands.CaptureFor(StandSettings.Saves.Value, name) : null;
                    string slotsSaid = null;
                    if (loadout == null)
                    {
                        return;
                    }
                    if (loadout.Items.Count == 0)
                    {
                        Stands.Say("Nothing to save: you carry and wear nothing that a loadout keeps.", true);
                        return;
                    }
                    if (saveOver && id != null)
                    {
                        loadout.Id = id;
                        // Its food slots stay, with their rules (review round 1 of job 30's Session 3, M1): the loadout as kept
                        // in his list, else as the question counted it (the stand's own code: deleted from his list, or in
                        // the other list; round 2's m3).
                        Loadout before = Store.Find(id) ?? was;
                        loadout = LoadoutEdit.KeepFoodSlots(before, loadout, wearer.Grid, out string tooMany, out slotsSaid, Settings.FoodStatAtLeast.Value);
                        if (loadout == null)
                        {
                            Stands.Say($"Not saved: {tooMany}", true);
                            return;
                        }
                        loadout = LoadoutEdit.KeepOwnSettings(before, loadout);   // its favourites switch, what the stand shows (0.5.0)
                    }
                    if (!Store.Save(loadout))
                    {
                        Stands.Say($"Not saved: {Store.WhyNotSaved}", true);
                        return;
                    }
                    Loadout kept = Store.Find(loadout.Id) ?? loadout;
                    StandMark.Write(stand, StandMark.For(kept, Stands.Me(), Stands.MyName()));
                    StandPersonal.Become(stand);   // "Stands I save to become personal"
                    var tally = new Tally();
                    if (StandSettings.Saving.Value == StandSaving.RecordsItAndHangsMyGearNow)
                    {
                        HangNow(stand, wearer, StandPlan.Pieces(kept, stand), Rules(), tally);
                    }
                    stand.Send();
                    StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
                    Stands.Say((saveOver ? $"Saved over {kept.Name}{(slotsSaid != null ? " (" + slotsSaid + ")" : string.Empty)}" : $"Saved {kept.Name} to this stand") +
                               (tally.Hung > 0 ? $"; {Pieces(tally.Hung)} went onto it." : "."), false);
                    tally.Tell("of what you wear");
                }))
            {
                End();
                Stands.Say("Outfitter is busy: try again in a moment.", true);
            }
        }

        private static void Unmark(IStand stand, StandMark seen, string name)
        {
            if (!Stands.Active)
            {
                return;
            }
            if (Busy || StandClaims.Busy)
            {
                Stands.Say("Outfitter is busy: try again in a moment.", true);
                return;
            }
            var current = new Job();
            current.Stands.Add(stand);
            job = current;
            if (!StandClaims.Start(new[] { stand }, held =>
                {
                    if (job != current)
                    {
                        return;
                    }
                    End();
                    if (!held.Held.Contains(stand))
                    {
                        Stands.Say(NotHeld(held), true);
                        return;
                    }
                    if (!StillMayUse(stand))
                    {
                        Stands.Say("You may not use this stand any more (a ward).", true);
                        return;
                    }
                    if (StandPersonal.Refused(stand) || !StillSeen(stand, seen))
                    {
                        return;
                    }
                    StandMark.Clear(stand);
                    stand.Send();
                    StandClaims.Hold(stand, StandSettings.HoldSeconds.Value);
                    Stands.Say($"An ordinary stand again ({name} stays in your list).", false);
                }))
            {
                End();
                Stands.Say("Outfitter is busy: try again in a moment.", true);
            }
        }

        // ---------- the pose key ----------

        /// <summary>The pose key at a marked stand ("Change a marked stand's pose"): its next pose, for everyone, through the
        /// game's own message; never at a stand someone else saved while "Stands someone else saved" leaves them alone.</summary>
        internal static void Pose(IStand stand)
        {
            if (!Stands.Active || !StandSettings.ChangePose.Value || stand == null || !stand.Valid)
            {
                return;
            }
            StandMark mark = StandMark.Read(stand);
            if (!mark.Marked || StandPersonal.Refused(stand) || (mark.By != Stands.Me() && StandSettings.Others.Value == OthersStands.LeaveThemAlone))
            {
                return;
            }
            if (!stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: true))
            {
                Stands.Say("You may not use this stand.", true);
                return;
            }
            int count = Math.Max(1, stand.PoseCount);
            stand.SetPose((stand.Pose + 1) % count);
        }

        // ---------- small things ----------

        // Brings the stand's mark up to date with his own loadout (its name and code), when he saved it.
        private static void Refresh(IStand stand, Loadout loadout, bool mine)
        {
            if (mine && loadout != null && stand.IsOwner)
            {
                StandMark.Write(stand, StandMark.For(loadout, Stands.Me(), Stands.MyName()));
            }
        }

        private static string Whose(StandMark mark) => mark.ByName.Length > 0 ? $"{mark.ByName}'s loadout" : "another player's loadout";

        private static string Named(StandMark mark) => mark.Name.Length > 0 ? " " + mark.Name : string.Empty;

        internal static string Pieces(int n) => n == 1 ? "1 piece" : $"{n} pieces";

        internal static string Items(int n) => n == 1 ? "1 item" : $"{n} items";

        private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
