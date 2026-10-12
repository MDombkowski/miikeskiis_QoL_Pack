using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// The catch-up (card A93, 0.2.0): a base's stations make up, at once, the time players were on the server while
    /// nobody was near them. A station only runs while a player's game has it loaded, and the game itself gives one coming
    /// back at most an hour of world time with what's already in it; what never happened was the refilling. So when a
    /// station Topper-Upper feeds loads again on the game that looks after it, before the game's own first update of it,
    /// this works out the time with players on since it last ran (PlayTime: the server pack's clock, the world's own, or
    /// this game's own time online), at most "Most time made up", and does what the station and Topper-Upper's feeding
    /// would have done in that time (CatchUpSim, on plain values: the game's own smelter second by second, and the feed by
    /// his rules every few seconds), then never less than the game's own catch-up of what was in it. In one frame it takes
    /// what that used from the chests this game holds (Taking: ChestClaims.MayChange, never a claim, as every feed), writes
    /// the station's record (its queue, fuel, timers, held stack, and its StartTime now, so the game's own update doesn't
    /// count the time again) and sends it, puts what it made into chests as stacks (the linked output, else as
    /// Topper-Upper puts products away; what doesn't fit lies at the station's output as whole stacks, never single
    /// items), and says it in one line ("Smelter caught up 3 h 12 min: Copper x96").
    /// The smelter family's turn is a prefix on Smelter.UpdateSmelter (once a second, on every computer that has it
    /// loaded; it acts only on the owner's); the fermenter's (it runs on world time, so its last batch is ready when it
    /// loads) is the module's, once a second, before the loop taps or fills it. A station found ready joins a line, and at
    /// most one catch-up runs a frame (stations loaded together come at once: review F4), its game update skipped until its
    /// turn. Once per load per station. The game's own update may wait a few seconds at most (WaitAtMost) while the
    /// server pack's answer hasn't come or the chests near the station aren't yet this game's; and while he follows the
    /// server pack's settings and its word may still come (his character not yet in the world, or its answer not yet
    /// in, just after the first hello), a station his own settings would leave out waits for it rather than settles
    /// (review F2), within the same bounds. A station another player's game looked after here first was run by that game:
    /// nothing to catch up. Topper-Upper's own feeding and put-away leave a station alone while its catch-up may still come
    /// (Pending). An error loses only the catch-up, for the session, said once; lights, stations and loose items go on, and
    /// what was taken from the chests is never lost (measured as it leaves them, it goes back into them, or lies at the
    /// station).
    /// </summary>
    internal static class CatchUps
    {
        /// <summary>The longest the game's own update waits for the server pack's answer and the chests, in seconds.</summary>
        internal const float WaitAtMost = 5f;

        /// <summary>How long a station loaded before his character is in the world waits for it, in seconds.</summary>
        internal const float WaitForHimAtMost = 120f;

        /// <summary>A station that last ran less than this long ago (world time, seconds) has nothing to catch up.</summary>
        internal const double LeastGap = 60.0;

        /// <summary>How long after it loads a station's catch-up may still come, in seconds: Topper-Upper's own feeding never
        /// waits longer.</summary>
        internal const float PendingAtMost = WaitForHimAtMost + WaitAtMost + 10f;

        /// <summary>How long after this game's first hello on a connection its answer may still come, in seconds: a server
        /// without the server pack never answers, and stations loaded later don't wait for it.</summary>
        internal const float AnswerMayCome = 10f;

        /// <summary>How often a fermenter waiting for its catch-up is looked at, in seconds.</summary>
        internal const float FermenterEvery = 1f;

        /// <summary>The line over a station floats this high above it, in metres.</summary>
        internal const float SayHeight = 2f;

        private static readonly List<Fed> Fermenters = new List<Fed>();
        private static readonly Queue<Fed> Ready = new Queue<Fed>();
        private static readonly HashSet<Fed> InLine = new HashSet<Fed>();
        private static readonly OnePerFrame Turn = new OnePerFrame();
        private static bool lost;

        /// <summary>An error lost the catch-up for the session.</summary>
        internal static bool Lost => lost;

        /// <summary>The catch-up's rules for this game now: his own settings, or the server pack's for everyone
        /// (CatchUpChoice).</summary>
        internal static CatchUpRules Rules()
        {
            var own = new CatchUpRules
            {
                On = Settings.CatchUp.Value,
                CapSeconds = Math.Min(Settings.CatchUpMost.Value * 3600.0, CatchUpChoice.MostCap),
                Wind = Settings.CatchUpWind.Value,
                Smelters = Settings.CatchUpSmelters.Value,
                Fermenter = Settings.CatchUpFermenter.Value,
            };
            return CatchUpChoice.Decide(own, Settings.FollowServerPack.Value, ServerPack.Runs(PlayTime.ServerModule),
                                        key => ServerPack.Value(PlayTime.ServerModule, key));
        }

        /// <summary>Whether a station's catch-up may still come, so Topper-Upper's own feeding, tapping and put-away leave it
        /// alone until then (at most PendingAtMost after it loaded): the rules have it catch up, or the server pack's word
        /// on them may still come (Logic.Hearing).</summary>
        internal static bool Pending(Fed fed) => Pending(fed, out _);

        /// <summary>As Pending; <paramref name="forTheServer"/> is true when only the server pack's word may still bring
        /// the catch-up (the rules leave the station out for now), for the hover (round two).</summary>
        internal static bool Pending(Fed fed, out bool forTheServer)
        {
            forTheServer = false;
            if (fed == null || lost || !Module.IsOn || fed.IsLight || (fed.Type != FedType.Smelter && fed.Type != FedType.Fermenter))
            {
                return false;
            }
            if (fed.CatchUpDone)
            {
                return false;
            }
            CatchUpRules rules = Rules();
            bool rulesHaveIt = rules.On && (fed.Type == FedType.Smelter ? rules.Smelters : rules.Fermenter);
            bool pending = Hearing.Pending(fed.CatchUpDone, fed.Mine, Time.time - fed.LoadedAt, PendingAtMost, rulesHaveIt, MayStillHear());
            forTheServer = pending && !rulesHaveIt;
            return pending;
        }

        /// <summary>A fermenter just loaded (Registry): its catch-up is tried by the module's turn (Tick).</summary>
        internal static void Watch(Fed fed)
        {
            if (fed != null && !Fermenters.Contains(fed))
            {
                Fermenters.Add(fed);
            }
        }

        /// <summary>The undo step, and a new world: the fermenters waiting, and the line, forgotten.</summary>
        internal static void Forget()
        {
            Fermenters.Clear();
            Ready.Clear();
            InLine.Clear();
        }

        /// <summary>Every frame, from the module's Update: at most one catch-up from the line, then the fermenters' turns.</summary>
        internal static void Tick()
        {
            if (lost)
            {
                return;
            }
            try
            {
                if (Ready.Count > 0 && Module.IsOn && Turn.Try(Time.frameCount))
                {
                    Fed fed = Ready.Dequeue();
                    InLine.Remove(fed);
                    Run(fed);
                }
                for (int i = Fermenters.Count - 1; i >= 0; i--)
                {
                    Fed fed = Fermenters[i];
                    if (fed == null || !fed.Alive || fed.CatchUpDone)
                    {
                        Fermenters.RemoveAt(i);
                        continue;
                    }
                    if (!Module.IsOn)
                    {
                        fed.CatchUpDone = true;   // loaded while the mod was off: no catch-up for this load
                        continue;
                    }
                    if (InLine.Contains(fed) || Time.time - fed.CatchUpLookedAt < FermenterEvery)
                    {
                        continue;
                    }
                    fed.CatchUpLookedAt = Time.time;
                    FermenterTurn(fed);
                }
            }
            catch (Exception error)
            {
                Lose(error);
            }
        }

        /// <summary>
        /// The prefix on Smelter.UpdateSmelter: true lets the game's own update run now; false skips it this second, while
        /// the station's catch-up waits its turn, or a moment for the server pack's word, the chests or his character (see
        /// the class). Changes nothing while the mod is off, or once this station's catch-up is settled.
        /// </summary>
        internal static bool BeforeUpdate(Smelter smelter)
        {
            if (lost || !Module.IsOn || smelter == null)
            {
                return true;
            }
            Fed fed = Registry.Of(smelter);
            if (fed == null || fed.CatchUpDone)
            {
                return true;
            }
            try
            {
                return SmelterTurn(fed);
            }
            catch (Exception error)
            {
                Lose(error);
                return true;
            }
        }

        // ---- Turns: settled, waiting, or into the line ----

        private static bool SmelterTurn(Fed fed)
        {
            ZNetView view = fed.View;
            if (view == null || !view.IsValid())
            {
                return true;
            }
            ZDO zdo = view.GetZDO();
            if (!view.IsOwner())
            {
                NoteOwner(fed, zdo);
                return true;   // the game's update does nothing here either; the owner's game runs it
            }
            if (InLine.Contains(fed))
            {
                return false;   // its turn comes in a frame or so
            }
            CatchUpRules rules = Rules();
            Verdict verdict = Judge(fed, rules);
            if (verdict != Verdict.Go)
            {
                return verdict == Verdict.Settled;
            }
            long startTicks = zdo.GetLong(ZDOVars.s_startTime, 0L);
            if (startTicks <= 0L)
            {
                Settle(fed, rules, "it hasn't run before");
                return true;
            }
            double elapsed = ZNet.instance.GetTimeSeconds() - startTicks / (double)TimeSpan.TicksPerSecond;
            if (elapsed < LeastGap)
            {
                Settle(fed, rules, null);   // it ran a moment ago (here, or on the game that had it): nothing to catch up
                return true;
            }
            switch (WaitTurn(fed))
            {
                case Wait.Yes:
                    return false;
                case Wait.GiveUp:
                    Settle(fed, rules, "your character wasn't in the world yet");
                    return true;
            }
            Enter(fed);
            return false;
        }

        private static void FermenterTurn(Fed fed)
        {
            var fermenter = (Fermenter)fed.Component;
            ZNetView view = fed.View;
            if (view == null || !view.IsValid())
            {
                return;
            }
            ZDO zdo = view.GetZDO();
            if (!view.IsOwner())
            {
                NoteOwner(fed, zdo);
                if (Time.time - fed.LoadedAt > PendingAtMost)
                {
                    fed.CatchUpDone = true;   // never this game's while it might have caught up
                }
                return;
            }
            CatchUpRules rules = Rules();
            if (Judge(fed, rules) != Verdict.Go)
            {
                return;
            }
            int content = zdo.GetInt(ZDOVars.s_content);
            long startTicks = zdo.GetLong(ZDOVars.s_startTime, 0L);
            if (content == 0 || startTicks <= 0L)
            {
                Settle(fed, rules, null);   // empty: Topper-Upper fills it as usual
                return;
            }
            double start = startTicks / (double)TimeSpan.TicksPerSecond;
            if (!(ZNet.instance.GetTimeSeconds() - start > fermenter.m_fermentationDuration))
            {
                Settle(fed, rules, null);   // still fermenting: it finishes on world time by itself
                return;
            }
            switch (WaitTurn(fed))
            {
                case Wait.Yes:
                    return;
                case Wait.GiveUp:
                    Settle(fed, rules, "your character wasn't in the world yet");
                    return;
            }
            Enter(fed);
        }

        private enum Verdict
        {
            Go,
            Waiting,
            Settled,
        }

        // The rules and the station: settled for a reason of the station's own; for a reason of the rules, waiting while
        // the server pack's word may still change them (review F2); else on.
        private static Verdict Judge(Fed fed, CatchUpRules rules)
        {
            string why = StationsReason(fed);
            if (why != null)
            {
                Settle(fed, rules, why);
                return Verdict.Settled;
            }
            why = RulesReason(fed, rules);
            if (why == null)
            {
                return Verdict.Go;
            }
            if (MayStillHear() && Time.time - fed.LoadedAt < WaitForHimAtMost)
            {
                return Verdict.Waiting;
            }
            Settle(fed, rules, why);
            return Verdict.Settled;
        }

        // Into the line: its catch-up runs at the next frame's turn (one a frame).
        private static void Enter(Fed fed)
        {
            if (InLine.Add(fed))
            {
                Ready.Enqueue(fed);
            }
        }

        // A station's turn in the line: everything read again in this frame, the rules judged again (a "nobody" from the
        // server pack, or a setting changed, while it waited stops it: round two), then its catch-up.
        private static void Run(Fed fed)
        {
            if (fed == null || !fed.Alive || fed.CatchUpDone || !fed.Mine)
            {
                return;   // gone, or no longer this game's: its next update settles it
            }
            CatchUpRules rules = Rules();
            if (Judge(fed, rules) != Verdict.Go)
            {
                return;   // settled now, or waiting again for the server pack's word: its next turn judges it again
            }
            fed.CatchUpDone = true;
            if (fed.Component is Smelter smelter)
            {
                CatchUpSmelter(fed, smelter, fed.Zdo, rules);
            }
            else if (fed.Component is Fermenter fermenter)
            {
                CatchUpFermenter(fed, fermenter, fed.Zdo, rules);
            }
        }

        // ---- The smelter family ----

        private static void CatchUpSmelter(Fed fed, Smelter smelter, ZDO zdo, CatchUpRules rules)
        {
            double now = ZNet.instance.GetTimeSeconds();
            long startTicks = zdo.GetLong(ZDOVars.s_startTime, 0L);
            double start = startTicks / (double)TimeSpan.TicksPerSecond;
            double elapsed = now - start;
            if (startTicks <= 0L || elapsed < LeastGap)
            {
                return;
            }
            double played = PlayTime.Played(start, now, out string clock);
            CatchUpSim.Seconds(played, rules.CapSeconds, zdo.GetFloat(ZDOVars.s_accTime), elapsed, out int fedSeconds, out int gift);
            var products = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
            var inputs = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
            SmelterKind kind = KindOf(smelter, products, inputs);
            SmelterState state = StateOf(zdo);
            bool haveRoof = !smelter.m_requiresRoof || smelter.m_roofCheckPoint == null || Cover.IsUnderRoof(smelter.m_roofCheckPoint.position);
            bool blocked = smelter.m_smokeSpawner != null && smelter.m_smokeSpawner.IsBlocked();
            float giftPower = 1f;
            float fedPower = 1f;
            string wind = null;
            if (smelter.m_windmill != null)
            {
                giftPower = smelter.m_windmill.GetPowerOutput();
                fedPower = Winds.Power(fed, smelter, rules.Wind, out wind);
            }
            bool cheated = (zdo.GetBool(ZDOVars.s_cheatedQueued) || zdo.GetBool(ZDOVars.s_cheated)) && !PlayerProfile.s_bypassCheatChecks;
            var times = new CatchUpTimes
            {
                Fed = fedSeconds,
                Gift = gift,
                FedPower = fedPower,
                GiftPower = giftPower,
                LookEvery = Mathf.Max(1, Mathf.RoundToInt(Settings.StationsEvery.Value)),
            };
            var supply = new Supply(fed);
            Lane fuel = FuelLane(smelter);
            FeedBook book = Book(fed, supply, Stations.InputLanes(fed, smelter), fuel);
            SmelterOutcome plan = CatchUpSim.Smelter(kind, state, book, times, haveRoof, blocked);

            // In this frame: take, write, put away. Whatever happens, what left the chests is never lost: each take is
            // measured as it happens (Taking.Removed); once the record is written, what it made comes out and what it
            // didn't use goes back; before that, all that was taken goes back. What it made and took again in that time
            // (the windmill's Oat, with OutputsBack) never leaves the chests: only the difference is taken, or put away.
            CatchUpSim.Flows(plan.Book, plan.Made, out Dictionary<string, int> planTake, out Dictionary<string, int> _);
            SmelterOutcome final = null;
            Dictionary<string, int> finalTake = null;
            Dictionary<string, int> finalGive = null;
            Dictionary<string, int> took = null;
            bool written = false;
            Delivered delivered = null;
            try
            {
                foreach (KeyValuePair<string, int> each in planTake)
                {
                    if (fuel != null && each.Key == fuel.Input)
                    {
                        supply.TakeFuel(each.Key, each.Value);
                    }
                    else
                    {
                        supply.TakeInput(each.Key, each.Value);
                    }
                }
                took = supply.Totals();
                final = Short(planTake, took) ? CatchUpSim.Smelter(kind, state, Limited(plan.Book, took), times, haveRoof, blocked) : plan;
                CatchUpSim.Flows(final.Book, final.Made, out finalTake, out finalGive);
                WriteSmelter(zdo, final.State, QueuedAny(final.Book));
                written = true;
            }
            finally
            {
                took = supply.Totals();   // what really left the chests, whatever happened above
                try
                {
                    delivered = Deliver(fed, written ? finalGive : null, products, inputs, Unused(took, written ? finalTake : null), supply.Removed(), cheated);
                }
                finally
                {
                    supply.Finish();
                }
            }
            Ledger(finalTake, final.Book.Queued);
            string text = CatchUpChoice.Line(Names.Of(fed), final.FedSeconds, CatchUpChoice.Made(final.Made, Names.Item));
            Say(fed, text, final.Made.Count > 0 || final.Book.Taken.Count > 0);
            if (Settings.LogCatchUps.Value)
            {
                Module.Log.LogInfo($"Catch-up: {Describe(fed)} last ran {elapsed:0} s of world time ago; {played:0} s with players on by {clock}, " +
                                   $"{rules.CapSeconds:0} s at most{(rules.FromServer ? " (the server pack's settings)" : string.Empty)}: " +
                                   $"{final.FedSeconds} s fed, {final.GiftSeconds} s more as the game's own catch-up of what was in it, " +
                                   $"{final.ExtraSeconds} s more so it finished no less of that than the game alone would" +
                                   (final.GameAloneFinished >= 0 ? $" ({final.OriginalsFinished} of {final.GameAloneFinished} needed)" : string.Empty) +
                                   (wind != null ? $"; wind: {wind}, the game's own {giftPower:0.00} power" : string.Empty) +
                                   (!haveRoof ? "; no roof" : string.Empty) + (blocked ? "; smoke blocked" : string.Empty) +
                                   $". Took {Listed(finalTake)} from {supply.ChestsUsed} chest(s){Back(took, finalTake)}; made {Listed(final.Made)}; " +
                                   $"{delivered?.Describe() ?? "nothing put away"}. {text}.");
            }
        }

        // What the game's Smelter is, as plain values, its conversions read once (review F4); the products' and the inputs'
        // prefabs by key into products and inputs (the fuel's too), for what's made and what's given back.
        private static SmelterKind KindOf(Smelter smelter, Dictionary<string, ItemDrop> products, Dictionary<string, ItemDrop> inputs)
        {
            var conversions = new List<(string From, string To, int MaxStack)>();
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (conversion == null)
                {
                    continue;
                }
                string to = conversion.m_to != null ? conversion.m_to.m_itemData.m_shared.m_name : null;
                if (conversion.m_to != null && !products.ContainsKey(to))
                {
                    products[to] = conversion.m_to;
                }
                if (conversion.m_from != null && !inputs.ContainsKey(conversion.m_from.m_itemData.m_shared.m_name))
                {
                    inputs[conversion.m_from.m_itemData.m_shared.m_name] = conversion.m_from;
                }
                conversions.Add((conversion.m_from != null ? conversion.m_from.gameObject.name : null, to,
                                 conversion.m_to != null ? conversion.m_to.m_itemData.m_shared.m_maxStackSize : 1));
            }
            if (smelter.m_fuelItem != null && !inputs.ContainsKey(smelter.m_fuelItem.m_itemData.m_shared.m_name))
            {
                inputs[smelter.m_fuelItem.m_itemData.m_shared.m_name] = smelter.m_fuelItem;
            }
            return new SmelterKind
            {
                MaxOre = smelter.m_maxOre,
                MaxFuel = smelter.m_maxFuel,
                FuelPerProduct = smelter.m_fuelPerProduct,
                SecPerProduct = smelter.m_secPerProduct,
                SpawnStack = smelter.m_spawnStack,
                RequiresRoof = smelter.m_requiresRoof,
            }.WithConversions(conversions);
        }

        private static SmelterState StateOf(ZDO zdo)
        {
            var state = new SmelterState
            {
                Fuel = zdo.GetFloat(ZDOVars.s_fuel),
                BakeTimer = zdo.GetFloat(ZDOVars.s_bakeTimer),
                Accumulator = zdo.GetFloat(ZDOVars.s_accTime),
                SpawnOre = zdo.GetString(ZDOVars.s_spawnOre) ?? string.Empty,
                SpawnAmount = zdo.GetInt(ZDOVars.s_spawnAmount),
            };
            int queued = zdo.GetInt(ZDOVars.s_queued);
            for (int i = 0; i < queued && i < 1000; i++)
            {
                state.Queue.Add(zdo.GetString("item" + i) ?? string.Empty);
            }
            return state;
        }

        // The station's record as the catch-up leaves it, written as the game's own code writes each value (QueueOre,
        // SetFuel, SetBakeTimer, SetAccumulator, QueueProcessed, GetDeltaTime's StartTime), so its next update counts only
        // the time from now; then sent at once, as the chests are, so a crash a moment later never leaves the chests
        // changed and the station as it was (a second catch-up of the same time).
        private static void WriteSmelter(ZDO zdo, SmelterState state, bool queuedSome)
        {
            zdo.Set(ZDOVars.s_fuel, state.Fuel);
            for (int i = 0; i < state.Queue.Count; i++)
            {
                zdo.Set("item" + i, state.Queue[i]);
            }
            zdo.Set(ZDOVars.s_queued, state.Queue.Count);
            if (queuedSome)
            {
                zdo.Set(ZDOVars.s_cheatedQueued, false);   // as QueueOre writes it for what a feed puts in
            }
            zdo.Set(ZDOVars.s_bakeTimer, state.BakeTimer);
            zdo.Set(ZDOVars.s_accTime, state.Accumulator);
            zdo.Set(ZDOVars.s_spawnOre, state.SpawnOre ?? string.Empty);
            zdo.Set(ZDOVars.s_spawnAmount, state.SpawnAmount);
            zdo.Set(ZDOVars.s_startTime, ZNet.instance.GetTime().Ticks);
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        // Its fuel's lane, or null for a station that burns none (the Frigid Kiln's makes Liquid Frost).
        private static Lane FuelLane(Smelter smelter)
        {
            if (smelter.m_fuelItem == null || smelter.m_maxFuel <= 0)
            {
                return null;
            }
            Smelter.ItemConversion sourceless = Stations.Sourceless(smelter);
            return new Lane
            {
                Input = smelter.m_fuelItem.m_itemData.m_shared.m_name,
                Prefab = smelter.m_fuelItem.gameObject.name,
                Product = sourceless?.m_to != null ? sourceless.m_to.m_itemData.m_shared.m_name : null,
                UnitsPerProduct = Mathf.Max(1, smelter.m_fuelPerProduct),
            };
        }

        private static bool QueuedAny(FeedBook book)
        {
            foreach (Lane lane in book.OreLanes)
            {
                if (book.Taken.TryGetValue(lane.Input, out int taken) && taken > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- The fermenter ----

        private static void CatchUpFermenter(Fed fed, Fermenter fermenter, ZDO zdo, CatchUpRules rules)
        {
            int content = zdo.GetInt(ZDOVars.s_content);
            long startTicks = zdo.GetLong(ZDOVars.s_startTime, 0L);
            double now = ZNet.instance.GetTimeSeconds();
            double start = startTicks / (double)TimeSpan.TicksPerSecond;
            double duration = fermenter.m_fermentationDuration;
            Fermenter.ItemConversion ready = content != 0 && startTicks > 0L && now - start > duration ? Stations.ConversionOf(fermenter, content) : null;
            if (ready?.m_from == null || ready.m_to == null)
            {
                Settle(fed, rules, ready == null ? null : "what's in it isn't a mead base this fermenter knows");
                return;
            }
            Cover.GetCoverForPoint(fermenter.m_roofCheckPoint != null ? fermenter.m_roofCheckPoint.position : fermenter.transform.position,
                                   out float cover, out bool roof);
            if (!roof || cover < 0.7f || !PrivateArea.CheckAccess(fermenter.transform.position, 0f, flash: false))
            {
                Settle(fed, rules, "it can't be filled where it stands (no roof, out in the open, or behind a ward you aren't on)");
                return;
            }
            double readyAt = start + duration;
            double played = PlayTime.Played(readyAt, now, out string clock);
            double time = Math.Min(played, rules.CapSeconds);
            if (time < LeastGap)
            {
                Settle(fed, rules, null);   // ready a moment ago: tapped and filled as usual
                return;
            }

            var supply = new Supply(fed);
            List<Lane> lanes = Stations.FermenterLanes(fed, fermenter);
            var inputs = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
            foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
            {
                if (conversion?.m_from != null && !inputs.ContainsKey(conversion.m_from.m_itemData.m_shared.m_name))
                {
                    inputs[conversion.m_from.m_itemData.m_shared.m_name] = conversion.m_from;
                }
            }
            FeedBook book = Book(fed, supply, lanes, null);
            string readyPrefab = ready.m_from.gameObject.name;
            FermentOutcome plan = CatchUpSim.Fermenter(readyPrefab, time, duration, book);

            Dictionary<string, int> took = null;
            FermentOutcome final = null;
            FeedBook finalBook = null;
            bool written = false;
            var made = new Dictionary<string, int>(StringComparer.Ordinal);
            var prefabs = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
            bool cheated = (zdo.GetBool(ZDOVars.s_cheatedQueued) || zdo.GetBool(ZDOVars.s_cheated)) && !PlayerProfile.s_bypassCheatChecks;
            Delivered delivered = null;
            try
            {
                foreach (KeyValuePair<string, int> each in book.Taken)
                {
                    supply.TakeInput(each.Key, each.Value);
                }
                took = supply.Totals();
                if (Short(book.Taken, took))
                {
                    finalBook = Limited(book, took);
                    final = CatchUpSim.Fermenter(readyPrefab, time, duration, finalBook);
                }
                else
                {
                    finalBook = book;
                    final = plan;
                }
                foreach (string tapped in final.Tapped)
                {
                    Fermenter.ItemConversion conversion = ConversionByPrefab(fermenter, tapped);
                    if (conversion?.m_to == null)
                    {
                        continue;
                    }
                    string product = conversion.m_to.m_itemData.m_shared.m_name;
                    prefabs[product] = conversion.m_to;
                    made.TryGetValue(product, out int sum);
                    made[product] = sum + Mathf.Max(0, conversion.m_producedItems);
                }
                WriteFermenter(zdo, final);
                written = true;
            }
            finally
            {
                took = supply.Totals();   // what really left the chests, whatever happened above
                try
                {
                    // Written: what it made comes out, what it didn't use goes back; not written: all that was taken goes back.
                    delivered = Deliver(fed, written ? made : null, prefabs, inputs, Unused(took, written ? finalBook.Taken : null), supply.Removed(), cheated);
                }
                finally
                {
                    supply.Finish();
                }
            }
            Ledger(finalBook.Taken, finalBook.Queued);
            string text = CatchUpChoice.Line(Names.Of(fed), (int)time, CatchUpChoice.Made(made, Names.Item));
            Say(fed, text, made.Count > 0);
            if (Settings.LogCatchUps.Value)
            {
                Module.Log.LogInfo($"Catch-up: {Describe(fed)} was ready {now - readyAt:0} s of world time ago; {played:0} s with players on by {clock}, " +
                                   $"{rules.CapSeconds:0} s at most{(rules.FromServer ? " (the server pack's settings)" : string.Empty)}: tapped " +
                                   $"{final.Tapped.Count} time(s), filled {final.Fills.Count} time(s)" +
                                   (final.Content != null ? $", {final.Content.Prefab} in it for {final.Fermented:0} s" : ", empty") +
                                   $". Took {Listed(finalBook.Taken)} from {supply.ChestsUsed} chest(s){Back(took, finalBook.Taken)}; made {Listed(made)}; " +
                                   $"{delivered?.Describe() ?? "nothing put away"}. {text}.");
            }
        }

        // As its fill and its tap leave it (Fermenter.RPC_AddItem, RPC_Tap): what's in it and since when (its time of play
        // so far taken off now), or empty as a tap leaves it; then sent at once.
        private static void WriteFermenter(ZDO zdo, FermentOutcome outcome)
        {
            if (outcome.Content != null)
            {
                zdo.Set(ZDOVars.s_content, outcome.Content.Prefab.GetStableHashCode());
                zdo.Set(ZDOVars.s_startTime, ZNet.instance.GetTime().Ticks - (long)(outcome.Fermented * TimeSpan.TicksPerSecond));
                zdo.Set(ZDOVars.s_cheatedQueued, false);
            }
            else
            {
                zdo.Set(ZDOVars.s_content, 0);
                zdo.Set(ZDOVars.s_startTime, 0);   // as the game's own tap writes it
                zdo.Set(ZDOVars.s_cheatedQueued, false);
            }
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        private static Fermenter.ItemConversion ConversionByPrefab(Fermenter fermenter, string prefab)
        {
            foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
            {
                if (conversion?.m_from != null && conversion.m_from.gameObject.name == prefab)
                {
                    return conversion;
                }
            }
            return null;
        }

        // ---- Shared ----

        private enum Wait
        {
            No,
            Yes,
            GiveUp,
        }

        // Whether to wait a moment more (see the class): for his character, then (at most WaitAtMost) for the server pack's
        // answer and for the chests near the station to be this game's.
        private static Wait WaitTurn(Fed fed)
        {
            float now = Time.time;
            if (Player.m_localPlayer == null)
            {
                return now - fed.LoadedAt < WaitForHimAtMost ? Wait.Yes : Wait.GiveUp;
            }
            if (fed.CatchUpWaitSince < 0f)
            {
                fed.CatchUpWaitSince = now;
            }
            if (now - fed.CatchUpWaitSince >= WaitAtMost)
            {
                return Wait.No;
            }
            return AwaitingServerPack() || ChestsNotYetOurs(fed) ? Wait.Yes : Wait.No;
        }

        // Whether the server pack's word on the catch-up may still come (Logic.Hearing).
        private static bool MayStillHear()
        {
            ZNet net = ZNet.instance;
            float said = ServerPack.FirstSaidAt;
            bool helloSaid = !float.IsNegativeInfinity(said);
            return Hearing.MayStillCome(Settings.FollowServerPack.Value, net != null && !net.IsServer(), Player.m_localPlayer != null,
                                        ServerPack.Answered, helloSaid, helloSaid ? Time.realtimeSinceStartup - said : 0f, AnswerMayCome);
        }

        // On a server whose server pack (if it has one) hasn't answered yet, just after joining: its clock and settings may
        // be on their way.
        private static bool AwaitingServerPack()
        {
            ZNet net = ZNet.instance;
            if (net == null || net.IsServer() || ServerPack.Answered)
            {
                return false;
            }
            float said = ServerPack.FirstSaidAt;
            return float.IsNegativeInfinity(said) || Time.realtimeSinceStartup - said < AnswerMayCome;
        }

        // A chest the station would take from or put into that this game may not change yet (the server hands chests out
        // every two seconds): its linked chests, those within its input radius, and, within a farther output radius, only
        // those already holding one of its products (card B30: a 250 m output radius would otherwise wait on chests near a
        // friend that never become this game's, gaining nothing).
        private static bool ChestsNotYetOurs(Fed fed)
        {
            Links.Resolve(fed, out List<Container> inputs, out List<Container> outputs);
            IEnumerable<Container> near = ChestFinder.Find(fed.Position, Settings.Rules(Settings.StationReach));
            if (Settings.StationOutputReach > Settings.StationReach)
            {
                HashSet<string> products = Stations.ProductsOf(fed);
                near = near.Concat(ChestFinder.Find(fed.Position, Settings.Rules(Settings.StationOutputReach)).Where(chest => HoldsAny(chest, products)));
            }
            foreach (Container chest in inputs.Concat(outputs).Concat(near))
            {
                if (chest != null && !Obliterator.Is(chest) && !ChestClaims.MayChange(chest))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HoldsAny(Container chest, HashSet<string> names)
        {
            Inventory inventory = chest != null ? chest.GetInventory() : null;
            if (inventory == null || names == null)
            {
                return false;
            }
            foreach (string name in names)
            {
                if (inventory.ContainsItemByName(name))
                {
                    return true;
                }
            }
            return false;
        }

        // Another player's game looking after it here, before this game did: it ran it (the server itself doesn't).
        private static void NoteOwner(Fed fed, ZDO zdo)
        {
            long owner = zdo.GetOwner();
            if (owner != 0L && owner != ZDOMan.GetSessionID() && owner != ServerPack.ServerId())
            {
                fed.OwnedElsewhere = true;
            }
        }

        // Why a station doesn't catch up whatever the rules say (settled at once), or null.
        private static string StationsReason(Fed fed)
        {
            if (fed.IsLight || (fed.Type != FedType.Smelter && fed.Type != FedType.Fermenter))
            {
                return "not a station that catches up";
            }
            if (fed.OwnedElsewhere)
            {
                return "another player's game looked after it here first, so it has run";
            }
            if (!Settings.FeedStations.Value)
            {
                return "\"Feed stations\" is off";
            }
            if (!Stations.Feedable(fed))
            {
                return "not a station Topper-Upper feeds";
            }
            if (!Stations.IsFed(fed))
            {
                return "it's left alone, or its kind isn't fed";
            }
            if (Rivals.Any)
            {
                return $"{string.Join(" and ", Rivals.Found)} feeds stations";
            }
            if (fed.Type == FedType.Fermenter && !Settings.TapFermenter.Value)
            {
                return "\"Tap the fermenter when it's ready\" is off";
            }
            return null;
        }

        // Why the rules leave a station out (his own settings, or the server pack's for everyone), or null.
        private static string RulesReason(Fed fed, CatchUpRules rules)
        {
            if (!rules.On)
            {
                return rules.FromServer ? "the server pack's catch-up is set for nobody" : "the catch-up is off";
            }
            if (fed.Type == FedType.Smelter ? !rules.Smelters : !rules.Fermenter)
            {
                return "its kind doesn't catch up (a setting)";
            }
            return null;
        }

        // Settled without a catch-up: said in the log (with "Write catch-ups to the log", for a reason worth saying).
        private static void Settle(Fed fed, CatchUpRules rules, string why)
        {
            fed.CatchUpDone = true;
            if (why != null && rules.On && Settings.LogCatchUps.Value && !fed.IsLight)
            {
                Module.Log.LogInfo($"Catch-up: {Describe(fed)} doesn't catch up: {why}.");
            }
        }

        // His rules' starting values for one catch-up, as a feed of that station counts them (Stations.StationFeed).
        private static FeedBook Book(Fed fed, Supply supply, List<Lane> oreLanes, Lane fuel)
        {
            Tally tally = Counts.For(fed);
            var book = new FeedBook
            {
                OreLanes = oreLanes,
                FuelLane = fuel,
                TopUpAt = Settings.StationTopUpAt.Value,
                FuelOnlyWhileWorking = Settings.FuelOnlyWhileWorking.Value,
                Never = RuleLists.Never,
                Uncounted = !Counts.Covers(tally, fed),
                // What comes out lands where the feed takes from only without linked chests, put away near the station.
                OutputsBack = !supply.Linked && Settings.NoLinkedOutput.Value == NoOutputChoice.PutAwayLikeTheQuickStack,
            };
            foreach (Lane lane in oreLanes)
            {
                Start(book, tally, lane, supply.CanGiveInput(lane.Input));
            }
            if (fuel != null)
            {
                Start(book, tally, fuel, supply.CanGiveFuel(fuel.Input));
            }
            if (Settings.LinkedOutputFull.Value == FullOutputChoice.StopFeedingTheStation && supply.LinkedOutputs.Count > 0)
            {
                book.LinkedRoomAtStart = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string product in Stations.ProductsOf(fed))
                {
                    book.LinkedRoomAtStart[product] = Products.LinkedRoom(supply.LinkedOutputs, product);
                }
            }
            return book;
        }

        // One lane's starting counts: what's held near him and its reserve, what the station's chests can give, and for its
        // product what counts toward its maximum (held, queued, lying at outputs) and the maximum.
        private static void Start(FeedBook book, Tally tally, Lane lane, int canGive)
        {
            if (lane?.Input == null)
            {
                return;
            }
            Count(book, tally, lane.Input);
            book.CanGiveAtStart[lane.Input] = canGive;
            if (lane.Product != null)
            {
                Count(book, tally, lane.Product);
            }
        }

        private static void Count(FeedBook book, Tally tally, string key)
        {
            book.HeldAtStart[key] = tally.HeldOf(key);
            book.CountedAtStart[key] = tally.CountedOf(key);
            int? reserve = RuleLists.ReserveOf(key);
            if (reserve.HasValue)
            {
                book.Reserves[key] = reserve.Value;
            }
            int? most = RuleLists.MaximumOf(key);
            if (most.HasValue)
            {
                book.Maximums[key] = most.Value;
            }
        }

        // Whether the chests gave less of anything than the plan took (another mod changed one in the same frame).
        private static bool Short(Dictionary<string, int> planned, Dictionary<string, int> took)
        {
            foreach (KeyValuePair<string, int> each in planned)
            {
                if (!took.TryGetValue(each.Key, out int got) || got < each.Value)
                {
                    return true;
                }
            }
            return false;
        }

        // The same catch-up again, with only what the chests really gave: it can use no more of anything than that.
        private static FeedBook Limited(FeedBook book, Dictionary<string, int> took)
        {
            FeedBook fresh = book.Fresh();
            foreach (string key in fresh.CanGiveAtStart.Keys.ToList())
            {
                fresh.CanGiveAtStart[key] = took.TryGetValue(key, out int got) ? got : 0;
            }
            return fresh;
        }

        // What left the chests but went into no station (all of it when nothing was written), by key.
        private static Dictionary<string, int> Unused(Dictionary<string, int> took, Dictionary<string, int> used)
        {
            var back = new Dictionary<string, int>(StringComparer.Ordinal);
            if (took == null)
            {
                return back;
            }
            foreach (KeyValuePair<string, int> each in took)
            {
                int spent = used != null && used.TryGetValue(each.Key, out int value) ? value : 0;
                if (each.Value - spent > 0)
                {
                    back[each.Key] = each.Value - spent;
                }
            }
            return back;
        }

        // Every count reads what the catch-up took from the chests and queued (Counts.Ledger), as for a feed.
        private static void Ledger(Dictionary<string, int> taken, Dictionary<string, int> queued)
        {
            if (taken != null)
            {
                foreach (KeyValuePair<string, int> each in taken)
                {
                    Counts.Took(each.Key, each.Value, null, 0);
                }
            }
            if (queued != null)
            {
                foreach (KeyValuePair<string, int> each in queued)
                {
                    Counts.Took(null, 0, each.Key, each.Value);
                }
            }
        }

        /// <summary>
        /// Puts what a catch-up made (and anything taken that went into no station) away in this frame, from a scratch
        /// inventory, as Topper-Upper puts a station's products away (Products): each product into the linked output chests
        /// as far as they have room, the rest as "Products with no linked output" or "When the linked output is full" say
        /// (put away like the quick stack into the chests near the station, or left); what was taken but not used back into
        /// the chests it came from (those holding it, linked inputs first), remade from the station's own input prefab at
        /// the world level it was taken at (the last taken first). Whatever is left lies at the station's output as whole
        /// stacks, never single items, and never lost.
        /// </summary>
        private static Delivered Deliver(Fed fed, Dictionary<string, int> made, Dictionary<string, ItemDrop> products, Dictionary<string, ItemDrop> inputs,
                                         Dictionary<string, int> back, Dictionary<string, List<KeyValuePair<int, int>>> removed, bool cheated)
        {
            var delivered = new Delivered();
            var lots = new List<Lot>();
            if (made != null)
            {
                foreach (KeyValuePair<string, int> each in made)
                {
                    ItemDrop prefab = products.TryGetValue(each.Key, out ItemDrop known) ? known : RuleLists.ItemByShared(each.Key);
                    Add(lots, new Lot { Key = each.Key, Prefab = prefab, Amount = each.Value, Made = true, Level = Game.m_worldLevel });
                }
            }
            if (back != null)
            {
                foreach (KeyValuePair<string, int> each in back)
                {
                    ItemDrop prefab = inputs.TryGetValue(each.Key, out ItemDrop known) ? known : RuleLists.ItemByShared(each.Key);
                    int left = each.Value;
                    if (removed != null && removed.TryGetValue(each.Key, out List<KeyValuePair<int, int>> levels))
                    {
                        for (int i = levels.Count - 1; i >= 0 && left > 0; i--)
                        {
                            int part = Math.Min(left, levels[i].Value);
                            Add(lots, new Lot { Key = each.Key, Prefab = prefab, Amount = part, Made = false, Level = levels[i].Key });
                            left -= part;
                        }
                    }
                    Add(lots, new Lot { Key = each.Key, Prefab = prefab, Amount = left, Made = false, Level = Game.m_worldLevel });
                }
            }
            if (lots.Count == 0)
            {
                return delivered;
            }
            int stacks = 0;
            foreach (Lot lot in lots)
            {
                stacks += CatchUpChoice.Stacks(lot.Amount, lot.Prefab.m_itemData.m_shared.m_maxStackSize).Count;
            }
            var scratch = new Inventory("TopperUpper_CatchUp", null, 8, stacks / 8 + 2);
            try
            {
                foreach (Lot lot in lots)
                {
                    foreach (int size in CatchUpChoice.Stacks(lot.Amount, lot.Prefab.m_itemData.m_shared.m_maxStackSize))
                    {
                        ItemDrop.ItemData item = lot.Prefab.m_itemData.Clone();
                        item.m_stack = size;
                        item.m_dropPrefab = lot.Prefab.gameObject;
                        item.m_worldLevel = lot.Level;
                        item.m_cheated = lot.Made && cheated;
                        item.m_equipped = false;
                        scratch.AddItem(item);
                    }
                }
                Links.Resolve(fed, out List<Container> linkedIn, out List<Container> outputs);
                Vector3 at = fed.Position;
                ChestRules near = Settings.Rules(Settings.StationOutputReach);   // what it made: the output's radius (card B30)
                List<Container> nearby = ChestFinder.Find(at, near).Where(chest => !outputs.Contains(chest)).ToList();
                List<Container> sources = null;   // within the input radius, for what goes back (read once, when needed)
                var done = new HashSet<string>(StringComparer.Ordinal);
                foreach (Lot lot in lots)
                {
                    if (!done.Add((lot.Made ? "+" : "-") + lot.Key))
                    {
                        continue;   // each key's stacks are put away together, whatever their world levels
                    }
                    if (lot.Made)
                    {
                        List<ItemDrop.ItemData> mine = StacksOf(scratch, lot.Key);
                        int amount = mine.Sum(item => item.m_stack);
                        OutputPlan plan = OutputChoice.Decide(amount, outputs.Count > 0, Products.LinkedRoom(outputs, lot.Key),
                                                              Settings.NoLinkedOutput.Value, Settings.LinkedOutputFull.Value);
                        if (plan.IntoLinked > 0)
                        {
                            // Linked outputs take whatever comes, in any cell, reserved or not: he chose them for it.
                            List<Container> nearest = outputs.OrderBy(chest => (chest.transform.position - at).sqrMagnitude).ToList();
                            StackRules linked = Stow.Rules(Settings.Rules(ChestSettings.MostRange), onlyWhereHeld: false, followSetups: false, leaveOthersMarked: false);
                            linked.Chests.UseReserved = true;
                            delivered.Moved(Stow.FromInventory(scratch, mine, nearest, linked));
                        }
                        if (plan.PutAway > 0)
                        {
                            StackRules rules = Stow.Rules(near, Settings.OnlyWhereHeld.Value, Settings.FollowSetups.Value, Settings.LeaveOthersMarked.Value);
                            delivered.Moved(Stow.FromInventory(scratch, StacksOf(scratch, lot.Key), nearby, rules));
                        }
                    }
                    else
                    {
                        // Back where it came from: a chest holding it, the linked inputs first, then those within the input
                        // radius it was taken from (card B30: not the output's).
                        ChestRules taken = Settings.Rules(Settings.StationReach);
                        sources = sources ?? ChestFinder.Find(at, taken).Where(chest => !outputs.Contains(chest)).ToList();
                        List<Container> from = linkedIn.Concat(sources.Where(chest => !linkedIn.Contains(chest))).ToList();
                        StackRules rules = Stow.Rules(taken, onlyWhereHeld: true, followSetups: false, leaveOthersMarked: false);
                        delivered.Moved(Stow.FromInventory(scratch, StacksOf(scratch, lot.Key), from, rules));
                    }
                }
            }
            finally
            {
                LayDown(fed, scratch, delivered);
            }
            return delivered;
        }

        private sealed class Lot
        {
            internal string Key;
            internal ItemDrop Prefab;
            internal int Amount;
            internal bool Made;
            internal int Level;
        }

        private static void Add(List<Lot> lots, Lot lot)
        {
            if (lot.Amount <= 0)
            {
                return;
            }
            if (lot.Prefab?.m_itemData?.m_shared == null)
            {
                Module.Warn($"Topper-Upper's catch-up couldn't make {lot.Amount} of {lot.Key}: the game has no such item.");
                return;
            }
            lots.Add(lot);
        }

        private static List<ItemDrop.ItemData> StacksOf(Inventory scratch, string key) =>
            scratch.GetAllItems().Where(item => item.m_shared.m_name == key && item.m_stack > 0).ToList();

        // What's left in the scratch inventory lies at the station's output, a stack each (as Smelter.Spawn makes one).
        private static void LayDown(Fed fed, Inventory scratch, Delivered delivered)
        {
            Transform output = Stations.OutputOf(fed);
            Vector3 at = output != null ? output.position : fed.Position + Vector3.up;
            Quaternion turn = output != null ? output.rotation : Quaternion.identity;
            foreach (ItemDrop.ItemData item in scratch.GetAllItems().ToList())
            {
                if (item?.m_dropPrefab == null || item.m_stack <= 0)
                {
                    continue;
                }
                GameObject made = UnityEngine.Object.Instantiate(item.m_dropPrefab, at + UnityEngine.Random.insideUnitSphere * 0.2f, turn);
                ItemDrop drop = made.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    continue;
                }
                drop.m_itemData.m_stack = item.m_stack;
                ItemDrop.OnCreateNew(drop, item.m_cheated);
                drop.m_itemData.m_worldLevel = item.m_worldLevel;   // given back at the level it was taken at
                delivered.Left(item.m_shared.m_name, item.m_stack);
            }
        }

        // The one line, as "Say what was made" says; nothing when the catch-up did nothing worth saying.
        private static void Say(Fed fed, string text, bool worthIt)
        {
            if (!worthIt)
            {
                return;
            }
            switch (Settings.CatchUpSayMade.Value)
            {
                case CatchUpSay.OverTheStation:
                    if (!Loose.PutText.OverTheChest(fed.Position + Vector3.up * SayHeight, text))
                    {
                        QoLMods.Kit.Say.TopLeft(text, log: true);
                    }
                    break;
                case CatchUpSay.InTheLogOnly:
                    Module.Log.LogInfo(text);
                    break;
            }
        }

        private static string Describe(Fed fed)
        {
            Vector3 at = fed.Position;
            return $"{Names.Of(fed)} ({fed.Prefab} at {at.x:0},{at.y:0},{at.z:0})";
        }

        private static string Listed(Dictionary<string, int> amounts)
        {
            if (amounts == null || amounts.Count == 0)
            {
                return "nothing";
            }
            return string.Join(", ", amounts.Select(each => $"{each.Value} {Names.Item(each.Key)}"));
        }

        private static string Back(Dictionary<string, int> took, Dictionary<string, int> used)
        {
            Dictionary<string, int> back = Unused(took, used);
            return back.Count > 0 ? $" ({Listed(back)} of it unused, after the chests gave less than planned, put back)" : string.Empty;
        }

        private static void Lose(Exception error)
        {
            lost = true;
            Fermenters.Clear();
            Ready.Clear();
            InLine.Clear();
            Module.Warn("Topper-Upper's catch-up stopped after an error; it stays off until the game is started again. Lights, stations and " +
                        $"loose items go on as before.\n{error}");
        }

        /// <summary>The chests one catch-up takes from, as a feed of that station would (Stations.StationFeed): its linked
        /// inputs if it has any, else the chests near it; its fuel from the linked inputs, then (by the setting) the chests
        /// near it. What it took is measured as it leaves each chest (Taking.Removed).</summary>
        private sealed class Supply
        {
            internal readonly List<Container> LinkedOutputs;
            private readonly Taking inputs;
            private readonly Taking nearby;

            /// <summary>It has chests linked, input or output.</summary>
            internal readonly bool Linked;

            internal Supply(Fed fed)
            {
                Links.Resolve(fed, out List<Container> linkedInputs, out LinkedOutputs);
                Linked = linkedInputs.Count > 0 || LinkedOutputs.Count > 0;
                ChestRules rules = Settings.Rules(Settings.StationReach);
                if (linkedInputs.Count > 0)
                {
                    inputs = new Taking(linkedInputs, Settings.LinkedKeepOne.Value, Settings.Reserved.Value == ReservedChests.UsedLikeAnyOther);
                    if (Settings.FuelWhenLinkedEmpty.Value == LinkedFuel.FromChestsNearby)
                    {
                        nearby = new Taking(ChestFinder.Find(fed.Position, rules).Where(chest => !linkedInputs.Contains(chest)), rules.LeaveOne, rules.UseReserved);
                    }
                }
                else
                {
                    inputs = new Taking(ChestFinder.Find(fed.Position, rules), rules.LeaveOne, rules.UseReserved);
                }
            }

            internal int CanGiveInput(string key) => inputs.CanGive(key);

            internal int CanGiveFuel(string key) => inputs.CanGive(key) + (nearby?.CanGive(key) ?? 0);

            internal void TakeInput(string key, int amount) => inputs.Take(key, amount);

            internal void TakeFuel(string key, int amount)
            {
                int got = inputs.Take(key, amount);
                if (got < amount && nearby != null)
                {
                    nearby.Take(key, amount - got);
                }
            }

            /// <summary>What left the chests so far, by item: read in the caller's finally (review F3).</summary>
            internal Dictionary<string, int> Totals()
            {
                var totals = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<KeyValuePair<int, int>>> each in Removed())
                {
                    totals[each.Key] = each.Value.Sum(level => level.Value);
                }
                return totals;
            }

            /// <summary>What left the chests so far, by item, each take's world level and amount, in order.</summary>
            internal Dictionary<string, List<KeyValuePair<int, int>>> Removed()
            {
                var all = new Dictionary<string, List<KeyValuePair<int, int>>>(StringComparer.Ordinal);
                foreach (Taking taking in new[] { inputs, nearby })
                {
                    if (taking == null)
                    {
                        continue;
                    }
                    foreach (KeyValuePair<string, List<KeyValuePair<int, int>>> each in taking.Removed)
                    {
                        if (!all.TryGetValue(each.Key, out List<KeyValuePair<int, int>> list))
                        {
                            all[each.Key] = list = new List<KeyValuePair<int, int>>();
                        }
                        list.AddRange(each.Value);
                    }
                }
                return all;
            }

            internal int ChestsUsed => inputs.ChangedCount + (nearby?.ChangedCount ?? 0);

            internal void Finish()
            {
                try
                {
                    inputs.Finish();
                }
                finally
                {
                    nearby?.Finish();
                }
            }
        }

        /// <summary>Where a catch-up's products went: into chests, and left at the station.</summary>
        private sealed class Delivered
        {
            private readonly HashSet<Container> chests = new HashSet<Container>();
            private int into;
            private readonly Dictionary<string, int> left = new Dictionary<string, int>(StringComparer.Ordinal);

            internal void Moved(StowResult result)
            {
                foreach (KeyValuePair<Container, Dictionary<string, int>> chest in result.Into)
                {
                    chests.Add(chest.Key);
                }
                into += result.Total;
            }

            internal void Left(string key, int amount)
            {
                left.TryGetValue(key, out int sum);
                left[key] = sum + amount;
            }

            internal string Describe() =>
                $"{into} into {chests.Count} chest(s)" + (left.Count > 0 ? $", left at the station: {Listed(left)}" : string.Empty);
        }
    }

    /// <summary>The smelter family's catch-up comes before the game's own update of it (CatchUps.BeforeUpdate); changes
    /// nothing while the mod is off.</summary>
    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class CatchUpBeforeUpdate
    {
        private static bool Prefix(Smelter __instance) => CatchUps.BeforeUpdate(__instance);
    }
}
