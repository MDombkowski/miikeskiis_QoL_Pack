using System;
using System.Collections.Generic;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Equip
{
    /// <summary>
    /// Putting a loadout on (card A34; the plan in the vault, working\2026-10-05_outfitter-plan.md, "Equipping"): wait until
    /// he can, plan from the chests' copies, ask about what's missing, then the chests' part in one claim (Exchange: take
    /// off, put away, take, in the frame the chests are his), then dress him (Dress: put on, instantly, and every item to
    /// its saved cell), up to three rounds, and say what happened. One equip at a time. The window's Equip and a Loadout
    /// Stand's key both come here.
    /// </summary>
    internal static class Outfit
    {
        private static Job current;

        /// <summary>The player an equip works on; the scratch harness sets another (the engine isn't there).</summary>
        internal static Func<IWearer> WearerOf = () => Player.m_localPlayer != null ? new PlayerWearer(Player.m_localPlayer) : null;

        /// <summary>The clock (unscaled seconds); the scratch harness sets another.</summary>
        internal static Func<float> Clock = () => Time.unscaledTime;

        /// <summary>
        /// The pop-up an equip asks with: the question, the words of the yes and the no, and what to do with the answer.
        /// Outfitter's window sets it (src\Window); while it's null no question can be asked, and "Ask" acts as "Cancel",
        /// saying what's missing.
        /// </summary>
        internal static Func<string, string, string, Action<bool>, int> Ask;

        /// <summary>The pop-up's way to ask a question with a list of items, each drawn with its icon (the missing-items
        /// question, card B21): the question, the words of the yes and the no, and what to do with the answer. Null: the
        /// question is asked as plain text through <see cref="Ask"/>.</summary>
        internal static Func<ListQuestion, string, string, Action<bool>, int> AskList;

        /// <summary>Takes down question n (Ask's number) if it's still showing, unanswered (an equip that ended meanwhile);
        /// set with Ask.</summary>
        internal static Action<int> Dismiss;

        /// <summary>Says a result's line to him (the game's message at the top left); the harness sets another.</summary>
        internal static Action<EquipResult> Say = Messages.Show;

        /// <summary>True while an equip is under way (waiting for him, for an answer at the pop-up, or for chests).</summary>
        internal static bool Busy => current != null;

        /// <summary>
        /// Begins putting <paramref name="request"/>'s loadout on; <paramref name="done"/> gets the result once it's over: in
        /// a later frame inside Outfitter's guard, or in this one inside the caller's own call (guard it as any of
        /// Outfitter's work). False, doing nothing and calling nothing, while another equip is under way, Outfitter is off,
        /// or there's no player.
        /// </summary>
        internal static bool Begin(EquipRequest request, Action<EquipResult> done)
        {
            if (Busy || !Module.IsOn || request?.Loadout == null || done == null)
            {
                return false;
            }
            IWearer wearer = WearerOf();
            if (wearer == null)
            {
                return false;
            }
            var job = new Job(request, done, wearer, Clock());
            current = job;
            job.Step(Clock());
            return true;
        }

        /// <summary>The plan an equip of this request would make now, from the chests' copies, without moving anything:
        /// for the window's preview (what's missing, what it will weigh). What the request's Lent holds (a stand's pieces,
        /// for the window's Equip through it) counts as a chest taken from first (Exchange.LentView). Null without a
        /// player.</summary>
        internal static EquipPlan Preview(EquipRequest request)
        {
            IWearer wearer = WearerOf();
            if (wearer == null || request?.Loadout == null)
            {
                return null;
            }
            EquipRules rules = EquipRules.FromSettings(request.Mode ?? Settings.Mode.Value);
            var exchange = new Exchange(request, rules, wearer, _ => { });
            List<ChestView> views = Exchange.Found(request, wearer);
            ChestView lent = Exchange.LentView(request.Lent);
            if (lent != null)
            {
                views.Insert(0, lent);
            }
            return EquipPlanner.Plan(exchange.Input(views));
        }

        /// <summary>Whether he wears every piece the loadout has him wear: an item that answers it as an equip would take it
        /// (the same item by prefab, its quality, style, maker and other mods' data by the settings), worn now (or a weapon
        /// the game has put away for a moment).</summary>
        internal static bool Wears(Player player, Loadout loadout)
        {
            if (player == null || loadout == null)
            {
                return false;
            }
            EquipRules rules = EquipRules.FromSettings(Settings.Mode.Value);
            bool any = false;
            ItemDrop.ItemData hiddenRight = GameHooks.HiddenRight(player);
            ItemDrop.ItemData hiddenLeft = GameHooks.HiddenLeft(player);
            foreach (LoadoutItem want in loadout.Items)
            {
                if (want.Worn == WornAs.None || want.Food != null)
                {
                    continue;   // carried, or a food slot (never worn)
                }
                any = true;
                bool found = false;
                // The $ name first, which costs nothing: a prefab's name asks the engine (the window asks this for every
                // loadout once a second: R2, finding 13).
                string name = Codes.Template(want.Prefab)?.m_shared.m_name;
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                {
                    bool worn = player.IsItemEquiped(item) || ReferenceEquals(item, hiddenRight) || ReferenceEquals(item, hiddenLeft);
                    if (worn && (name == null || item.m_shared.m_name == name) && Capture.PrefabOf(item) == want.Prefab &&
                        Matching.Answers(item, want, rules))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            return any;
        }

        /// <summary>Every frame: the equip under way, if any.</summary>
        internal static void Tick()
        {
            current?.Step(Clock());
        }

        /// <summary>After an error switched Outfitter off: the equip under way is given up (its done isn't called; nothing
        /// is half moved, since every move finishes inside one frame).</summary>
        internal static void Abandon()
        {
            current = null;
        }

        // Why the game won't put gear on, in words (PlayerWearer.CanEquipNow).
        private const string BusyWords = "you were busy (fighting, dodging, swimming, eating, or the game was still changing your gear)";

        private enum Stage
        {
            Waiting,
            Asking,
            Starting,
            Exchanging,
            Dressing,
            Over,
        }

        private sealed class Job
        {
            private readonly EquipRequest request;
            private readonly Action<EquipResult> done;
            private readonly IWearer wearer;
            private readonly float started;
            private readonly EquipRules rules;
            private readonly Exchange exchange;
            private Stage stage;
            private float stageSince;
            private bool undressed;   // the chests' part is done but he isn't dressed yet
            private int question;     // the number of its question showing, while Asking

            internal Job(EquipRequest request, Action<EquipResult> done, IWearer wearer, float now)
            {
                this.request = request;
                this.done = done;
                this.wearer = wearer;
                started = now;
                stageSince = now;
                rules = EquipRules.FromSettings(request.Mode ?? Settings.Mode.Value);
                exchange = new Exchange(request, rules, wearer, Settled);
            }

            internal void Step(float now)
            {
                if (stage == Stage.Over)
                {
                    return;
                }
                if (current != this)
                {
                    // Abandoned: an error switched Outfitter off inside this job's own call (Abandon), so its done is
                    // never called and nothing more moves (the world harness, 37-B).
                    stage = Stage.Over;
                    return;
                }
                if (!Module.IsOn)
                {
                    Cancel("Outfitter was switched off");
                    return;
                }
                if (wearer.Gone)
                {
                    Cancel("you weren't there any more");   // a respawn or a logout (R1, finding 1)
                    return;
                }
                switch (stage)
                {
                    case Stage.Waiting:
                        if (wearer.Unavailable || (rules.Instant && !wearer.CanEquipNow))
                        {
                            if (now - started > Settings.WaitSeconds.Value)
                            {
                                Cancel(wearer.Unavailable ? "you couldn't change just then" : BusyWords);
                            }
                            return;
                        }
                        Ready(now);
                        return;
                    case Stage.Asking:
                        return;   // waiting for his answer at the pop-up
                    case Stage.Starting:
                        Start(now);
                        return;
                    case Stage.Exchanging:
                        if (exchange.Over)
                        {
                            Enter(undressed ? Stage.Dressing : Stage.Over, now);
                            if (stage == Stage.Over)
                            {
                                Finish();
                            }
                            else
                            {
                                Step(now);
                            }
                        }
                        return;
                    case Stage.Dressing:
                        if (!wearer.Unavailable && DressNow())
                        {
                            Finish();
                        }
                        else if (now - stageSince > Settings.WaitSeconds.Value)
                        {
                            exchange.Result.NotPutOn.Add((wearer.Unavailable ? "you couldn't change just then" : BusyWords) +
                                                         ", so some pieces weren't put on");
                            Finish();
                        }
                        return;
                }
            }

            // He can change: look at what the chests hold, ask what must be asked, then begin.
            private void Ready(float now)
            {
                EquipPlan preview = exchange.PlanFound(Exchange.Found(request, wearer));
                int missing = preview.MissingCount;
                MissingItems policy = request.Missing ?? Settings.Missing.Value;
                if (missing > 0 && policy != MissingItems.EquipWhatsHere)
                {
                    if (policy == MissingItems.Cancel || Ask == null)
                    {
                        AddMissing(preview);
                        Cancel(Messages.MissingWords(preview, 3) + (policy == MissingItems.Cancel ? string.Empty : " (no pop-up to ask with)"));
                        return;
                    }
                    Enter(Stage.Asking, now);
                    Action<bool> then = yes => Answered(yes, preview, true);
                    question = AskList != null
                        ? AskList(Messages.MissingList(request.Loadout, preview, rules.Mode), "Equip what's here", "Cancel", then)
                        : Ask(Messages.MissingQuestion(request.Loadout, preview, rules.Mode), "Equip what's here", "Cancel", then);
                    return;
                }
                if (Settings.Confirm.Value && Ask != null)
                {
                    Enter(Stage.Asking, now);
                    question = Ask(Messages.ConfirmQuestion(request.Loadout, preview), "Equip", "Cancel", yes => Answered(yes, preview, false));
                    return;
                }
                Enter(Stage.Starting, now);
                Start(now);
            }

            private void Answered(bool yes, EquipPlan preview, bool aboutMissing)
            {
                Module.Guard("answering Outfitter's question", () =>
                {
                    if (current != this || stage != Stage.Asking)
                    {
                        return;
                    }
                    float now = Clock();
                    if (!yes)
                    {
                        if (aboutMissing)
                        {
                            AddMissing(preview);
                        }
                        Cancel(!Module.IsOn ? "Outfitter was switched off" : wearer.Gone ? "you weren't there any more" : "you cancelled it");
                        return;
                    }
                    if (aboutMissing && Settings.Confirm.Value && Ask != null)
                    {
                        question = Ask(Messages.ConfirmQuestion(request.Loadout, preview), "Equip", "Cancel", answer => Answered(answer, preview, false));
                        return;
                    }
                    Enter(Stage.Starting, now);
                    Start(now);
                });
            }

            // The chests' part, asked again a moment later while another module's claim is under way, or while he can't
            // change (dragging an item, teleporting), up to 5 s.
            private void Start(float now)
            {
                if (wearer.Unavailable)
                {
                    if (now - stageSince > 5f)
                    {
                        Cancel("you couldn't change just then");
                    }
                    return;
                }
                if (!exchange.NextRound())
                {
                    if (now - stageSince > 5f)
                    {
                        Cancel("the chests nearby are busy");
                    }
                    return;
                }
                Enter(Stage.Exchanging, now);
                Step(now);
            }

            // Each round's chests are done (in that frame): dress him at once if the game lets him change, else next frames.
            private void Settled(Exchange settledExchange)
            {
                undressed = !DressNow();
            }

            // Plans on his inventory alone and dresses him by it; false when the game won't put gear on now.
            private bool DressNow()
            {
                if (wearer.Gone)
                {
                    return false;
                }
                PlanInput input = exchange.Input(new List<ChestView>());
                EquipPlan plan = EquipPlanner.Plan(input);
                return Dress.Run(plan, input, wearer, rules, exchange.Result, exchange.StayOn, exchange.OffHands);
            }

            private void AddMissing(EquipPlan plan)
            {
                foreach (Want want in plan.Wants)
                {
                    if (want.Missing > 0)
                    {
                        exchange.Result.Missing.Add(new MissingItem { Item = want.First, Count = want.Missing, Why = want.Why });
                    }
                }
            }

            private void Enter(Stage next, float now)
            {
                stage = next;
                stageSince = now;
            }

            private void Cancel(string why)
            {
                EquipResult result = exchange.Result;
                result.Cancelled = true;
                result.WhyCancelled = why;
                End(result);
            }

            private void Finish()
            {
                EquipResult result = exchange.Result;
                EquipPlan last = exchange.LastPlan;
                if (last != null)
                {
                    // What's still missing after every round, by a plan on his inventory alone.
                    EquipPlan now = EquipPlanner.Plan(exchange.Input(new List<ChestView>()));
                    foreach (Want want in now.Wants)
                    {
                        if (want.Missing > 0)
                        {
                            string why = want.Why;
                            foreach (Want before in last.Wants)
                            {
                                if (before.First == want.First && before.Why != null)
                                {
                                    why = before.Why;
                                }
                            }
                            // The plan once the chests were his saw only those: a plain "none nearby" gives way to what the
                            // plan that saw every chest said (no room, only broken ones, none in a set-up chest).
                            if ((why == null || why == "none nearby" || why == "not enough nearby" || why == "none in the chests set up for it" ||
                                 why == "not enough in the chests set up for it") && exchange.Reasons.TryGetValue(want.First, out string seen))
                            {
                                why = seen;
                            }
                            result.Missing.Add(new MissingItem { Item = want.First, Count = want.Missing, Why = why ?? "none nearby" });
                        }
                    }
                }
                End(result);
            }

            private void End(EquipResult result)
            {
                bool wasAsking = stage == Stage.Asking;
                stage = Stage.Over;
                exchange.Cancel();   // a claim still on its way moves nothing now (R1, finding 16)
                if (current == this)
                {
                    current = null;
                }
                if (wasAsking)
                {
                    Dismiss?.Invoke(question);   // its own question only (answering it now returns at once)
                }
                result.Line = Messages.Line(request, result);
                Say?.Invoke(result);
                done(result);
            }
        }
    }
}
