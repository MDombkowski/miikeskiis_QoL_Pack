using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Lights and fires (card A77; the research's 1.2): every player-built fire with fuel the game burns, the hot tub too,
    /// topped up with its own fuel from the chests near him (or near the light), by his rules: its kind's switch, the
    /// world-fire and candle rules, its own mark, the top-up threshold, the reserve (one list for everything the mod
    /// takes, counted near him), leave-one; when fuel is short, the emptiest (or nearest) first. Only the game that looks
    /// after a light feeds it, from chests that game already holds; one a friend's game without the mod looks after is fed
    /// the way a hand would only with that setting.
    /// </summary>
    internal static class Lights
    {
        /// <summary>A light as the loop reads it: its fuel item, its fuel now, its capacity and its room (as a hand adds).</summary>
        internal struct State
        {
            internal ItemDrop FuelItem;
            internal float Level;
            internal float Max;
            internal int Room;
        }

        /// <summary>Reads a light's fuel from its record. False for one with no fuel to give (infinite, none, or one no
        /// hand can refill that isn't a resin candle).</summary>
        internal static bool Read(Fed fed, out State state)
        {
            state = default;
            if (fed.Component is Fireplace fire)
            {
                if (fire.m_infiniteFuel || fire.m_fuelItem == null || fire.m_maxFuel <= 0f || (!fire.m_canRefill && fed.Prefab != Kinds.Candle))
                {
                    return false;
                }
                state.FuelItem = fire.m_fuelItem;
                state.Level = fed.Zdo.GetFloat(ZDOVars.s_fuel);
                state.Max = fire.m_maxFuel;
                state.Room = Rooms.Fire(state.Level, state.Max);
                return true;
            }
            if (fed.Component is Smelter tub)
            {
                if (tub.m_fuelItem == null || tub.m_maxFuel <= 0)
                {
                    return false;
                }
                state.FuelItem = tub.m_fuelItem;
                state.Level = fed.Zdo.GetFloat(ZDOVars.s_fuel);
                state.Max = tub.m_maxFuel;
                state.Room = Rooms.Fuel(state.Level, tub.m_maxFuel);
                return true;
            }
            return false;
        }

        /// <summary>Whether its kind says it's fed, before its own mark: its kind's switch (or "Resin candles", or "Other
        /// kinds of light"), and a fire the world made only with "Fires the world made".</summary>
        internal static bool KindSaysFed(Fed fed)
        {
            ConfigEntry<bool> kind = Settings.LightKind(fed.Prefab);
            bool on = fed.Prefab == Kinds.Candle ? Settings.Candles.Value : kind != null ? kind.Value : Settings.OtherLights.Value;
            return on && (Settings.WorldFires.Value || fed.PlayerBuilt);
        }

        /// <summary>Whether it's fed, by its kind and its own mark.</summary>
        internal static bool IsFed(Fed fed) => FeedMark.IsFed(fed.Mark, KindSaysFed(fed));

        /// <summary>
        /// The loop's look (cheap: its record and his distance): whether it wants a feed now, and its place in line when fuel
        /// is short (lower first). Notes why not on it, for its hover line.
        /// </summary>
        internal static bool Look(Fed fed, Vector3 him, float now, out float key)
        {
            key = 0f;
            fed.Why = null;
            if (!Settings.FeedLights.Value || !Read(fed, out State state))
            {
                return false;
            }
            if (Rivals.Any)
            {
                fed.Why = $"{string.Join(" and ", Rivals.Found)} feeds it";
                return false;
            }
            int mark = fed.Mark;
            if (!FeedMark.IsFed(mark, KindSaysFed(fed)))
            {
                fed.Why = mark == FeedMark.LeftAlone ? "Left alone" : "Its kind isn't fed (a setting)";
                return false;
            }
            float distance = Vector3.Distance(him, fed.Position);
            if (distance > Settings.LightRange.Value)
            {
                fed.Why = "Farther from you than the light range";
                return false;
            }
            if (!Rooms.LightDue(state.Level, state.Max, state.Room, Settings.LightTopUpAt.Value))
            {
                return false;
            }
            if (fed.Mine && !CaughtUp(fed))
            {
                return false;   // its owner hasn't burned the time it was away yet (n1): looked at again next time
            }
            if (!fed.Mine && !Owners.MayFeedByHand(fed, Settings.LightRange.Value, now))
            {
                return false;
            }
            key = Settings.WhenShort.Value == ShortFirst.TheNearest ? distance : state.Level / Mathf.Max(0.01f, state.Max);
            return true;
        }

        /// <summary>Feeds it now, in this frame, if it still wants it: its fuel from the chests the settings name, as much as
        /// the room, the reserve, leave-one and the chests allow. Notes why not on it.</summary>
        internal static void Feed(Fed fed)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !fed.Alive || !Settings.FeedLights.Value || Rivals.Any || !Read(fed, out State state) || !IsFed(fed))
            {
                return;
            }
            bool mine = fed.Mine;
            float now = Time.time;
            if (!mine && !Owners.MayFeedByHand(fed, Settings.LightRange.Value, now))
            {
                return;
            }
            if (state.Room <= 0 || (mine && !CaughtUp(fed)))
            {
                return;
            }
            // Chests near him: found with the count and kept as long (m5); near the light: its own small search.
            bool nearLight = Settings.LightFuelFrom.Value == FuelFrom.ChestsNearTheLight;
            ChestRules rules = Settings.Rules(nearLight ? Settings.LightChestRange.Value : Settings.ChestRange);
            List<Container> candidates = nearLight ? ChestFinder.Find(fed.Position, rules) : Counts.ChestsNearHim(rules.Range);
            var taking = new Taking(candidates, rules.LeaveOne, rules.UseReserved);
            Tally tally = Counts.For(fed);
            string fuel = state.FuelItem.m_itemData.m_shared.m_name;
            var lanes = new List<Lane> { new Lane { Input = fuel } };
            List<Grant> grants = FeedRule.Plan(state.Room, lanes,
                name => FeedRule.AboveReserve(tally.HeldOf(name), RuleLists.ReserveOf(name)),
                name => FeedRule.Unlimited,
                taking.CanGive,
                RuleLists.Never);
            if (grants.Count == 0)
            {
                fed.Why = Why(fuel, tally, taking);
                fed.Barren++;
                return;
            }
            List<Given> given;
            var book = new WriteBook();
            try
            {
                given = Feeding.Carry(grants, taking, (grant, units) => Add(fed, units, book));
            }
            finally
            {
                taking.Finish();
                Adds.Effects(fed, book);
            }
            Feeding.Count(given);
            if (!mine)
            {
                fed.HandFedAt = now;
            }
            fed.Why = null;
            fed.Barren = given.Count > 0 ? 0 : fed.Barren + 1;
            Feeding.Tell(fed, given, taking.ChangedCount, byHand: !mine);
        }

        // The fuel into the light (Adds): a fire one message of the amount to itself; the hot tub (a smelter) one write of
        // its fuel and its effect once; for a friend's game, a message per unit.
        private static void Add(Fed fed, int units, WriteBook book)
        {
            if (fed.Component is Smelter)
            {
                Adds.SmelterFuel(fed, units, book);
            }
            else
            {
                Adds.FireFuel(fed, units);
            }
        }

        /// <summary>How long a fire's (or an oven's, a Frost Foundry's) owner may have gone without counting its time, in
        /// seconds, before it's fed: loaded again, it burns the whole time it was away at its owner's first tick (every 2 s,
        /// a cooking station every second), so fuel added before that would burn for the time nobody was there (code review
        /// n1, n17; Logic.CatchUp).</summary>
        internal const float CatchUpWithin = 6f;

        // A fire whose record says its owner burned its time a moment ago (the hot tub keeps its time apart: always).
        private static bool CaughtUp(Fed fed)
        {
            if (!(fed.Component is Fireplace) || ZNet.instance == null)
            {
                return true;
            }
            return CatchUp.Done(fed.Zdo.GetLong(ZDOVars.s_lastTime, 0L), ZNet.instance.GetTime().Ticks, CatchUpWithin, false);
        }

        /// <summary>Why a feed found nothing to give, in words for the hover line.</summary>
        internal static string Why(string fuel, Tally tally, Taking taking)
        {
            string name = Names.Item(fuel);
            if (RuleLists.Never(fuel))
            {
                return $"{name} is never taken (a setting)";
            }
            int? reserve = RuleLists.ReserveOf(fuel);
            if (reserve.HasValue && tally.HeldOf(fuel) <= reserve.Value)
            {
                return $"{name} kept: {tally.HeldOf(fuel)} near you, {reserve.Value} kept";
            }
            return taking.Chests.Count == 0 ? "No chest your game holds is near" : $"No {name} to spare in the chests near";
        }
    }
}
