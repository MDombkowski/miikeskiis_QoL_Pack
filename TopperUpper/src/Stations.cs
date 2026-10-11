using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Stations (card A77; the research's 1.3 to 1.7 and section 3): the smelter family (smelter, blast furnace,
    /// charcoal kiln, windmill, spinning wheel, eitr refinery, Frigid Kiln), the fermenter (filled when empty), the shield
    /// generator's fuel, the Frost Foundry's and stone oven's fuel, and the ballista's missiles, fed from the chests near
    /// each (or only from its linked input chests) by his rules (FeedRule): what each takes first, the kiln's woods, the
    /// never-take list, reserves, maximums counting what's queued and lying at the outputs, leave-one, and fuel only while
    /// there's something to process. The room is worked out from the live station (its own capacity, which a server mod
    /// may have changed), since the owner's own add never checks it. Only player-built stations, only while "Feed
    /// stations" is on, only by the game that looks after it (or as a hand would, with that setting), never while the
    /// server fed it lately (stand aside). Food on cooking stations is never touched.
    /// </summary>
    internal static class Stations
    {
        /// <summary>Players within this distance of a station count when choosing who feeds a friend's as a hand would, in
        /// metres.</summary>
        internal const float HandRange = 64f;

        /// <summary>The charcoal kiln's prefab: its woods are its own list.</summary>
        internal const string Kiln = "charcoal_kiln";

        /// <summary>Whether it's a station this mod may feed at all: player-built, a kind it knows how to feed, not a
        /// siege engine. Its switches and mark come after.</summary>
        internal static bool Feedable(Fed fed) =>
            !fed.IsLight && !Kinds.Never.Contains(fed.Prefab) && !Kinds.Never.Contains(fed.RootPrefab) && fed.PlayerBuilt;

        /// <summary>A station that can't work without a roof and has none (the spinning wheel: Smelter.m_requiresRoof, its
        /// private m_haveRoof as the smelter itself refreshes it each second): not fed, its items would only sit in it
        /// (code review n13). The game's own add doesn't check, so without the member it's fed as the add would be.</summary>
        internal static bool NeedsRoof(Fed fed) =>
            fed.Component is Smelter smelter && smelter.m_requiresRoof && GameHooks.SmelterHaveRoof != null &&
            !GameHooks.Read<bool>(GameHooks.SmelterHaveRoof, smelter);

        /// <summary>An oven or a Frost Foundry whose record says its owner counted its time a moment ago, or never (code
        /// review n17): fed before its owner's first tick, its fuel would burn, and its food cook, for the whole time nobody
        /// was there. Every other station: always (a smelter's catch-up is capped at an hour and makes products of it).</summary>
        internal static bool CaughtUp(Fed fed) =>
            !(fed.Component is CookingStation) || ZNet.instance == null ||
            CatchUp.Done(fed.Zdo.GetLong(ZDOVars.s_startTime, 0L), ZNet.instance.GetTime().Ticks, Lights.CatchUpWithin, true);

        /// <summary>Whether its kind says it's fed: its own switch, or "Other mods' stations" for a kind not named.</summary>
        internal static bool KindSaysFed(Fed fed)
        {
            ConfigEntry<bool> kind = Settings.StationKind(fed.Prefab);
            return kind != null ? kind.Value : Settings.OtherStations.Value;
        }

        /// <summary>Whether it's fed, by its kind and its own mark (with "Feed stations" on).</summary>
        internal static bool IsFed(Fed fed) => Settings.FeedStations.Value && FeedMark.IsFed(fed.Mark, KindSaysFed(fed));

        /// <summary>
        /// The loop's look (cheap: its record): whether it wants a feed now. Notes why not on it, for its hover line.
        /// </summary>
        internal static bool Look(Fed fed, Vector3 him, float now, out float key)
        {
            key = 0f;
            fed.Why = null;
            if (!Settings.FeedStations.Value || !Feedable(fed))
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
            if (Owners.StandingAside(fed, now))
            {
                fed.Why = "The server fed it a moment ago: left alone for a minute";
                return false;
            }
            if (NeedsRoof(fed))
            {
                fed.Why = "It needs a roof";
                return false;
            }
            if (!Wants(fed))
            {
                return false;
            }
            if (fed.Mine && !CaughtUp(fed))
            {
                return false;   // its owner hasn't counted the time it was away yet (n17): looked at again next time
            }
            if (!fed.Mine && !Owners.MayFeedByHand(fed, HandRange, now))
            {
                return false;
            }
            key = (him - fed.Position).sqrMagnitude * 0.0001f;   // nearer first, all else being equal
            return true;
        }

        // Whether its queue or fuel is due a top-up, from its record alone.
        private static bool Wants(Fed fed)
        {
            ZDO zdo = fed.Zdo;
            StationTopUp at = Settings.StationTopUpAt.Value;
            switch (fed.Component)
            {
                case Smelter smelter:
                {
                    bool sourceless = Sourceless(smelter) != null;
                    int queued = zdo.GetInt(ZDOVars.s_queued);
                    if (!sourceless && smelter.m_maxOre > 0 && Rooms.StationDue(Rooms.Queue(queued, smelter.m_maxOre), smelter.m_maxOre, at))
                    {
                        return true;
                    }
                    if (smelter.m_fuelItem == null || smelter.m_maxFuel <= 0)
                    {
                        return false;
                    }
                    bool working = sourceless || queued > 0 || !Settings.FuelOnlyWhileWorking.Value;
                    return working && Rooms.StationDue(Rooms.Fuel(zdo.GetFloat(ZDOVars.s_fuel), smelter.m_maxFuel), smelter.m_maxFuel, at);
                }
                case Fermenter _:
                    return zdo.GetInt(ZDOVars.s_content) == 0;
                case ShieldGenerator shield:
                    return shield.m_fuelItems != null && shield.m_fuelItems.Count > 0 &&
                           Rooms.StationDue(Rooms.Fuel(zdo.GetFloat(ZDOVars.s_fuel, shield.m_defaultFuel), shield.m_maxFuel), shield.m_maxFuel, at);
                case CookingStation cooking:
                    return Rooms.StationDue(Rooms.Fuel(zdo.GetFloat(ZDOVars.s_fuel), cooking.m_maxFuel), cooking.m_maxFuel, at) &&
                           (!Settings.FuelOnlyWhileWorking.Value || HasUncooked(cooking));
                case Turret turret:
                    return turret.m_maxAmmo > 0 && Rooms.StationDue(turret.m_maxAmmo - turret.GetAmmo(), turret.m_maxAmmo, at);
                default:
                    return false;   // beehives and sap extractors are only harvested (Products)
            }
        }

        /// <summary>Feeds it now, in this frame, if it still wants it. Notes why not on it.</summary>
        internal static void Feed(Fed fed)
        {
            float now = Time.time;
            if (!fed.Alive || !Feedable(fed) || Rivals.Any || !IsFed(fed) || Owners.StandingAside(fed, now) || NeedsRoof(fed))
            {
                return;
            }
            bool mine = fed.Mine;
            if (!mine && !Owners.MayFeedByHand(fed, HandRange, now))
            {
                return;
            }
            if (mine && !CaughtUp(fed))
            {
                return;
            }
            var feed = new StationFeed(fed);
            try
            {
                switch (fed.Component)
                {
                    case Smelter smelter:
                        feed.Smelter(smelter);
                        break;
                    case Fermenter fermenter:
                        feed.Fermenter(fermenter);
                        break;
                    case ShieldGenerator shield:
                        feed.Fuel(shield.m_fuelItems, Rooms.Fuel(fed.Zdo.GetFloat(ZDOVars.s_fuel, shield.m_defaultFuel), shield.m_maxFuel), shield.m_maxFuel,
                                  (units, book) => Adds.ShieldFuel(fed, units, book));
                        break;
                    case CookingStation cooking:
                        if (Settings.FuelOnlyWhileWorking.Value && !HasUncooked(cooking))
                        {
                            fed.Why = "Nothing in it to bake or cast";
                            return;
                        }
                        feed.Fuel(new List<ItemDrop> { cooking.m_fuelItem }, Rooms.Fuel(fed.Zdo.GetFloat(ZDOVars.s_fuel), cooking.m_maxFuel), cooking.m_maxFuel,
                                  (units, book) => Adds.CookingFuel(fed, units, book));
                        break;
                    case Turret turret:
                        feed.Turret(turret);
                        break;
                }
            }
            finally
            {
                feed.Finish();
            }
            if (!mine && feed.Given.Count > 0)
            {
                fed.HandFedAt = now;
            }
            fed.Barren = feed.Given.Count > 0 ? 0 : fed.Barren + 1;
            Feeding.Tell(fed, feed.Given, feed.ChestsUsed, byHand: !mine);
        }

        /// <summary>A sourceless smelter's conversion (the Frigid Kiln: its fuel is its input), or null.</summary>
        internal static Smelter.ItemConversion Sourceless(Smelter smelter)
        {
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (conversion != null && conversion.m_from == null && conversion.m_to != null)
                {
                    return conversion;
                }
            }
            return null;
        }

        /// <summary>A smelter's conversion for an input prefab, or null.</summary>
        internal static Smelter.ItemConversion ConversionOf(Smelter smelter, string inputPrefab)
        {
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (conversion?.m_from != null && conversion.m_from.gameObject.name == inputPrefab)
                {
                    return conversion;
                }
            }
            return null;
        }

        /// <summary>A fermenter's conversion for an input's name hash (its record's content), or null.</summary>
        internal static Fermenter.ItemConversion ConversionOf(Fermenter fermenter, int contentHash)
        {
            foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
            {
                if (conversion?.m_from != null && conversion.m_from.gameObject.name.GetStableHashCode() == contentHash)
                {
                    return conversion;
                }
            }
            return null;
        }

        internal static bool HasUncooked(CookingStation cooking)
        {
            try
            {
                return GameHooks.Call<bool>(GameHooks.CookingHasUncooked, cooking);
            }
            catch (Exception)
            {
                return true;   // can't tell: as if something were in it
            }
        }

        /// <summary>What a station makes, by shared name: its products, for the products at its output and its counts.</summary>
        internal static HashSet<string> ProductsOf(Fed fed)
        {
            var products = new HashSet<string>(StringComparer.Ordinal);
            switch (fed.Component)
            {
                case Smelter smelter:
                    foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                    {
                        if (conversion?.m_to != null)
                        {
                            products.Add(conversion.m_to.m_itemData.m_shared.m_name);
                        }
                    }
                    break;
                case Fermenter fermenter:
                    foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                    {
                        if (conversion?.m_to != null)
                        {
                            products.Add(conversion.m_to.m_itemData.m_shared.m_name);
                        }
                    }
                    break;
                case Beehive hive:
                    if (hive.m_honeyItem != null)
                    {
                        products.Add(hive.m_honeyItem.m_itemData.m_shared.m_name);
                    }
                    break;
                case SapCollector sap:
                    if (sap.m_spawnItem != null)
                    {
                        products.Add(sap.m_spawnItem.m_itemData.m_shared.m_name);
                    }
                    break;
            }
            return products;
        }

        /// <summary>Where a station's products land: its output point, or its spawn point; null for one that makes none.</summary>
        internal static Transform OutputOf(Fed fed)
        {
            switch (fed.Component)
            {
                case Smelter smelter:
                    return smelter.m_outputPoint;
                case Fermenter fermenter:
                    return fermenter.m_outputPoint;
                case Beehive hive:
                    return hive.m_spawnPoint;
                case SapCollector sap:
                    return sap.m_spawnPoint;
                default:
                    return null;
            }
        }

        /// <summary>For the maximums (the research's 3.2): what this station will make from what's in it, and what lies at
        /// its output, by shared name, added to <paramref name="made"/>.</summary>
        internal static void CountMade(Fed fed, Dictionary<string, int> made)
        {
            ZDO zdo = fed.Zdo;
            switch (fed.Component)
            {
                case Smelter smelter:
                {
                    int queued = zdo.GetInt(ZDOVars.s_queued);
                    for (int i = 0; i < queued && i < 1000; i++)
                    {
                        Smelter.ItemConversion conversion = ConversionOf(smelter, zdo.GetString("item" + i));
                        if (conversion?.m_to != null)
                        {
                            Tally.Add(made, conversion.m_to.m_itemData.m_shared.m_name, 1);
                        }
                    }
                    int held = zdo.GetInt(ZDOVars.s_spawnAmount);
                    if (held > 0)
                    {
                        string ore = zdo.GetString(ZDOVars.s_spawnOre);
                        Smelter.ItemConversion conversion = ConversionOf(smelter, ore) ?? Sourceless(smelter);
                        if (conversion?.m_to != null)
                        {
                            Tally.Add(made, conversion.m_to.m_itemData.m_shared.m_name, held);
                        }
                    }
                    Smelter.ItemConversion sourceless = Sourceless(smelter);
                    if (sourceless != null && smelter.m_fuelPerProduct > 0)
                    {
                        // The product in progress too: it spends its Ice as it works (n6; FeedRule.Pending).
                        Tally.Add(made, sourceless.m_to.m_itemData.m_shared.m_name,
                                  FeedRule.Pending(zdo.GetFloat(ZDOVars.s_fuel), smelter.m_fuelPerProduct));
                    }
                    break;
                }
                case Fermenter fermenter:
                {
                    Fermenter.ItemConversion conversion = ConversionOf(fermenter, zdo.GetInt(ZDOVars.s_content));
                    if (conversion?.m_to != null)
                    {
                        Tally.Add(made, conversion.m_to.m_itemData.m_shared.m_name, conversion.m_producedItems);
                    }
                    break;
                }
            }
            Transform output = OutputOf(fed);
            if (output != null)
            {
                HashSet<string> products = ProductsOf(fed);
                foreach (ItemDrop drop in Lying.At(output.position, Products.Reach))
                {
                    if (drop.m_itemData?.m_shared != null && products.Contains(drop.m_itemData.m_shared.m_name))
                    {
                        Tally.Add(made, drop.m_itemData.m_shared.m_name, drop.m_itemData.m_stack);
                    }
                }
            }
        }

        /// <summary>
        /// One feed of one station, in one frame: its chests (its linked inputs, else those within its chest radius), the
        /// count, and what was given; Finish (in the caller's finally) holds and sends every chest it took from.
        /// </summary>
        private sealed class StationFeed
        {
            private readonly Fed fed;
            private readonly Tally tally;
            private readonly WriteBook book = new WriteBook();
            private readonly List<Container> linkedInputs;
            private readonly List<Container> linkedOutputs;
            private Taking inputs;
            private Taking nearby;

            internal readonly List<Given> Given = new List<Given>();

            internal StationFeed(Fed fed)
            {
                this.fed = fed;
                tally = Counts.For(fed);
                Links.Resolve(fed, out linkedInputs, out linkedOutputs);
            }

            internal int ChestsUsed => (inputs?.ChangedCount ?? 0) + (nearby?.ChangedCount ?? 0);

            /// <summary>The chests it takes its inputs from: its linked inputs if it has any, else those near it.</summary>
            private Taking Inputs()
            {
                if (inputs == null)
                {
                    inputs = linkedInputs.Count > 0
                        ? new Taking(linkedInputs, Settings.LinkedKeepOne.Value, Settings.Reserved.Value == ReservedChests.UsedLikeAnyOther)
                        : Nearby();
                }
                return inputs;
            }

            private Taking Nearby()
            {
                if (nearby == null)
                {
                    ChestRules rules = Settings.Rules(Settings.StationRadius.Value);
                    nearby = new Taking(ChestFinder.Find(fed.Position, rules), rules.LeaveOne, rules.UseReserved);
                }
                return nearby;
            }

            /// <summary>The chests its fuel comes from: its linked inputs while they have some, else (by the setting) those
            /// near it.</summary>
            private Taking FuelFrom(IList<Lane> lanes)
            {
                Taking first = Inputs();
                if (linkedInputs.Count == 0 || Settings.FuelWhenLinkedEmpty.Value == LinkedFuel.OnlyFromTheLinkedChests)
                {
                    return first;
                }
                foreach (Lane lane in lanes)
                {
                    if (first.CanGive(lane.Input) > 0)
                    {
                        return first;
                    }
                }
                return Nearby();
            }

            internal void Finish()
            {
                try
                {
                    inputs?.Finish();
                    if (nearby != null && nearby != inputs)
                    {
                        nearby.Finish();
                    }
                }
                finally
                {
                    Adds.Effects(fed, book);   // once per kind written, for the whole feed (M1)
                    Feeding.Count(Given);
                }
            }

            // The smelter family: its queue first, by his order, then its fuel while there's something to process. The
            // Frigid Kiln, whose fuel is its input, only its fuel, counted against Liquid Frost's maximum.
            internal void Smelter(Smelter smelter)
            {
                ZDO zdo = fed.Zdo;
                StationTopUp at = Settings.StationTopUpAt.Value;
                Smelter.ItemConversion sourceless = Sourceless(smelter);
                if (sourceless == null && smelter.m_maxOre > 0)
                {
                    int room = Rooms.Queue(zdo.GetInt(ZDOVars.s_queued), smelter.m_maxOre);
                    if (Rooms.StationDue(room, smelter.m_maxOre, at))
                    {
                        List<Lane> lanes = InputLanes(smelter);
                        Run(room, lanes, Inputs(), (grant, units) => Adds.Ore(fed, grant.Lane.Prefab, units, book));
                    }
                }
                if (smelter.m_fuelItem == null || smelter.m_maxFuel <= 0)
                {
                    return;
                }
                float fuel = zdo.GetFloat(ZDOVars.s_fuel);
                int fuelRoom = Rooms.Fuel(fuel, smelter.m_maxFuel);
                if (!Rooms.StationDue(fuelRoom, smelter.m_maxFuel, at))
                {
                    return;
                }
                var fuelLanes = new List<Lane>();
                if (sourceless != null)
                {
                    fuelLanes.Add(new Lane
                    {
                        Input = smelter.m_fuelItem.m_itemData.m_shared.m_name,
                        Prefab = smelter.m_fuelItem.gameObject.name,
                        Product = sourceless.m_to.m_itemData.m_shared.m_name,
                        UnitsPerProduct = Mathf.Max(1, smelter.m_fuelPerProduct),
                        PendingUnits = fuel,
                    });
                }
                else
                {
                    if (Settings.FuelOnlyWhileWorking.Value && zdo.GetInt(ZDOVars.s_queued) <= 0)
                    {
                        if (fed.Why == null && Given.Count == 0)
                        {
                            fed.Why = "Nothing in it to process: no fuel";
                        }
                        return;
                    }
                    fuelLanes.Add(new Lane { Input = smelter.m_fuelItem.m_itemData.m_shared.m_name, Prefab = smelter.m_fuelItem.gameObject.name });
                }
                Run(fuelRoom, fuelLanes, FuelFrom(fuelLanes), (grant, units) => Adds.SmelterFuel(fed, units, book));
            }

            // What a smelter takes, in his order: the kiln only its listed woods (all, with none listed); every other
            // station its "takes first" line, then the rest in the game's order. An input listed twice counts once.
            private List<Lane> InputLanes(Smelter smelter)
            {
                var conversions = new List<Smelter.ItemConversion>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                {
                    if (conversion?.m_from != null && conversion.m_to != null && seen.Add(conversion.m_from.gameObject.name))
                    {
                        conversions.Add(conversion);
                    }
                }
                List<Smelter.ItemConversion> ordered;
                if (fed.Prefab == Kiln && RuleLists.KilnWoods.Count > 0)
                {
                    ordered = RuleLists.KilnWoods.Order(conversions, each => each.m_from.gameObject.name, onlyListed: true);
                }
                else
                {
                    ordered = RuleLists.FirstsOf(fed.Prefab).Order(conversions, each => each.m_from.gameObject.name, onlyListed: false);
                }
                var lanes = new List<Lane>();
                foreach (Smelter.ItemConversion conversion in ordered)
                {
                    lanes.Add(new Lane
                    {
                        Input = conversion.m_from.m_itemData.m_shared.m_name,
                        Prefab = conversion.m_from.gameObject.name,
                        Product = conversion.m_to.m_itemData.m_shared.m_name,
                    });
                }
                return lanes;
            }

            // The fermenter: one mead base when it's empty, under a roof and not out in the open, and you may use it (a ward).
            internal void Fermenter(Fermenter fermenter)
            {
                if (fed.Zdo.GetInt(ZDOVars.s_content) != 0)
                {
                    return;
                }
                if (!GameHooks.Read<bool>(GameHooks.FermenterRoof, fermenter))
                {
                    fed.Why = "It needs a roof";
                    return;
                }
                if (GameHooks.Read<bool>(GameHooks.FermenterExposed, fermenter))
                {
                    fed.Why = "It's out in the open";
                    return;
                }
                if (!PrivateArea.CheckAccess(fermenter.transform.position, 0f, flash: false))
                {
                    fed.Why = "Behind a ward you aren't on";
                    return;
                }
                var conversions = new List<Fermenter.ItemConversion>();
                foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                {
                    if (conversion?.m_from != null && conversion.m_to != null)
                    {
                        conversions.Add(conversion);
                    }
                }
                var lanes = new List<Lane>();
                foreach (Fermenter.ItemConversion conversion in RuleLists.FirstsOf(fed.Prefab).Order(conversions, each => each.m_from.gameObject.name, onlyListed: false))
                {
                    lanes.Add(new Lane
                    {
                        Input = conversion.m_from.m_itemData.m_shared.m_name,
                        Prefab = conversion.m_from.gameObject.name,
                        Product = conversion.m_to.m_itemData.m_shared.m_name,
                        ProductsPerBatch = Mathf.Max(1, conversion.m_producedItems),
                    });
                }
                // One base per feed (room 1): its own message, which plays its effect once and writes no log line.
                Run(1, lanes, Inputs(), (grant, units) =>
                {
                    for (int i = 0; i < units; i++)
                    {
                        fed.View.InvokeRPC("RPC_AddItem", grant.Lane.Prefab.GetStableHashCode(), false);
                    }
                });
            }

            // A station whose only take is fuel (any of its fuel items: the shield generator's bones, an oven's wood).
            internal void Fuel(IList<ItemDrop> fuels, int room, int capacity, Action<int, WriteBook> add)
            {
                if (fuels == null || !Rooms.StationDue(room, capacity, Settings.StationTopUpAt.Value))
                {
                    return;
                }
                var items = new List<ItemDrop>();
                foreach (ItemDrop fuel in fuels)
                {
                    if (fuel != null)
                    {
                        items.Add(fuel);
                    }
                }
                var lanes = new List<Lane>();
                foreach (ItemDrop fuel in RuleLists.FirstsOf(fed.Prefab).Order(items, each => each.gameObject.name, onlyListed: false))
                {
                    lanes.Add(new Lane { Input = fuel.m_itemData.m_shared.m_name, Prefab = fuel.gameObject.name });
                }
                Run(room, lanes, FuelFrom(lanes), (grant, units) => add(units, book));
            }

            // The ballista: one kind of missile at a time (its own message sets the kind to the last one added, so two
            // kinds in one feed would turn the first into the second); loaded, only more of that kind.
            internal void Turret(Turret turret)
            {
                int ammo = turret.GetAmmo();
                int room = turret.m_maxAmmo - ammo;
                if (!Rooms.StationDue(room, turret.m_maxAmmo, Settings.StationTopUpAt.Value))
                {
                    return;
                }
                string loaded = ammo > 0 ? turret.GetAmmoType() : null;
                var kinds = new List<ItemDrop>();
                foreach (Turret.AmmoType each in turret.m_allowedAmmo)
                {
                    if (each.m_ammo != null && (loaded == null || each.m_ammo.gameObject.name == loaded))
                    {
                        kinds.Add(each.m_ammo);
                    }
                }
                Taking taking = Inputs();
                foreach (ItemDrop kind in RuleLists.FirstsOf(fed.Prefab).Order(kinds, each => each.gameObject.name, onlyListed: false))
                {
                    var lane = new List<Lane> { new Lane { Input = kind.m_itemData.m_shared.m_name, Prefab = kind.gameObject.name } };
                    if (Run(room, lane, taking, (grant, units) => Adds.Ammo(fed, grant.Lane.Prefab, units, book)) > 0)
                    {
                        return;   // one kind per feed
                    }
                }
            }

            // Plans by his rules, carries it out, and notes why when nothing was given. Returns how many went in.
            private int Run(int room, List<Lane> lanes, Taking taking, Action<Grant, int> add)
            {
                if (lanes.Count == 0 || room <= 0)
                {
                    return 0;
                }
                List<Grant> grants = FeedRule.Plan(room, lanes, AboveReserve, Wanted, taking.CanGive, RuleLists.Never);
                if (grants.Count == 0)
                {
                    if (fed.Why == null)
                    {
                        fed.Why = WhyNothing(lanes, taking);
                    }
                    return 0;
                }
                List<Given> given = Feeding.Carry(grants, taking, add);
                Given.AddRange(given);
                int total = 0;
                foreach (Given each in given)
                {
                    total += each.Amount;
                }
                if (total > 0)
                {
                    fed.Why = null;
                }
                return total;
            }

            // The count is told what this feed took only at Finish, so what its earlier plans (the queue, before the fuel)
            // took is taken off here, and what they queued added to what's made.
            private int AboveReserve(string input)
            {
                int took = 0;
                foreach (Given each in Given)
                {
                    if (each.Grant.Input == input)
                    {
                        took += each.Amount;
                    }
                }
                return FeedRule.AboveReserve(tally.HeldOf(input) - took, RuleLists.ReserveOf(input));
            }

            private int Wanted(string product)
            {
                int queued = 0;
                foreach (Given each in Given)
                {
                    if (each.Grant.Product == product)
                    {
                        queued += each.Products;
                    }
                }
                int? most = RuleLists.MaximumOf(product);
                if (most.HasValue && !Counts.Covers(tally, fed))
                {
                    return 0;   // its own queue and output aren't in the count, so its maximum can't stop it (m1)
                }
                int wanted = FeedRule.Wanted(tally.CountedOf(product) + queued, most);
                if (Settings.LinkedOutputFull.Value == FullOutputChoice.StopFeedingTheStation && linkedOutputs.Count > 0)
                {
                    int room = Products.LinkedRoom(linkedOutputs, product);
                    if (OutputChoice.Paused(true, room, FullOutputChoice.StopFeedingTheStation))
                    {
                        return 0;
                    }
                }
                return wanted;
            }

            // Why a plan gave nothing, from the first input that had something to give.
            private string WhyNothing(List<Lane> lanes, Taking taking)
            {
                bool linked = linkedInputs.Count > 0 && taking == inputs;
                string where = linked ? "its linked chests" : "the chests near it";
                if (taking.Chests.Count == 0)
                {
                    if (taking.ReservedLeftOut > 0)
                    {
                        return linked ? "Its linked input chests are reserved (Reserve Stock; see Reserved chests)"
                                      : "The chests near it are reserved (Reserve Stock; see Reserved chests)";
                    }
                    return linked ? "No linked input chest your game holds is loaded" : "No chest your game holds is near it";
                }
                foreach (Lane lane in lanes)
                {
                    if (RuleLists.Never(lane.Input) || taking.CanGive(lane.Input) <= 0)
                    {
                        continue;
                    }
                    string name = Names.Item(lane.Input);
                    int? reserve = RuleLists.ReserveOf(lane.Input);
                    if (reserve.HasValue && tally.HeldOf(lane.Input) <= reserve.Value)
                    {
                        return $"{name} kept: {tally.HeldOf(lane.Input)} near you, {reserve.Value} kept";
                    }
                    if (lane.Product != null)
                    {
                        int? most = RuleLists.MaximumOf(lane.Product);
                        if (most.HasValue && !Counts.Covers(tally, fed))
                        {
                            return $"Farther than the count range from you: its {Names.Item(lane.Product)} can't be counted against its most";
                        }
                        int wanted = FeedRule.Wanted(tally.CountedOf(lane.Product), most);
                        if (most.HasValue && FeedRule.MostUnits(wanted, lane.UnitsPerProduct, lane.ProductsPerBatch, lane.PendingUnits) <= 0)
                        {
                            string batch = lane.ProductsPerBatch > 1 ? $" (one makes {lane.ProductsPerBatch})" : string.Empty;
                            return $"{Names.Item(lane.Product)} at its most: {tally.CountedOf(lane.Product)}/{most.Value}{batch}";
                        }
                        if (Settings.LinkedOutputFull.Value == FullOutputChoice.StopFeedingTheStation && linkedOutputs.Count > 0 &&
                            Products.LinkedRoom(linkedOutputs, lane.Product) <= 0)
                        {
                            return "Its output chest is full";
                        }
                    }
                }
                return $"Nothing it takes in {where}";
            }
        }
    }
}
