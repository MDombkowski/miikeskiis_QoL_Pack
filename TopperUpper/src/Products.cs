using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// What stations make (the research's 4.3, and 1.3 to 1.7): bars, coal, flour, thread, eitr, Liquid Frost, meads, and
    /// (with their switches) honey, feathers and sap, lying at the station's output. The station's owner's game made them,
    /// so that game owns them and puts them away without asking anyone: those lying within 1.5 m of the output, a moment
    /// old (a setting), go into its linked output chests, or, with none linked (or those full, by the settings), into
    /// chests near the station like the quick stack, or stay where they fall. Here too: a ready fermenter is tapped, the
    /// windmill emptied early (a setting, off), and full beehives, birds' nests and sap extractors harvested (their
    /// switches, off). Each station is looked at about once a second; nothing is ever asked of another game.
    /// </summary>
    internal static class Products
    {
        /// <summary>How near a station's output a product must lie, in metres.</summary>
        internal const float Reach = 1.5f;

        /// <summary>A station this game looks after, about once a second: tapped, emptied or harvested as its settings say,
        /// and what lies at its output a moment old put away. The windmill holds its flour as one stack until it's full
        /// (Smelter.m_spawnStack); "Empty the windmill early" lets it out sooner.</summary>
        internal static void Look(Fed fed, float now)
        {
            if (!Settings.FeedStations.Value || Rivals.Any || !fed.Mine || !Stations.Feedable(fed) || !Stations.IsFed(fed) || CatchUps.Pending(fed))
            {
                return;   // a station about to catch up (card A93) is tapped and emptied by the catch-up first
            }
            ZDO zdo = fed.Zdo;
            switch (fed.Component)
            {
                case Fermenter fermenter:
                    if (Settings.TapFermenter.Value && FermenterReady(fermenter, zdo))
                    {
                        // Its content clears now and its meads land 1.5 s later: counted meanwhile (code review m2).
                        Fermenter.ItemConversion conversion = Stations.ConversionOf(fermenter, zdo.GetInt(ZDOVars.s_content));
                        if (conversion?.m_to != null)
                        {
                            Counts.NoteTap(conversion.m_to.m_itemData.m_shared.m_name, conversion.m_producedItems);
                        }
                        fed.View.InvokeRPC("RPC_Tap");   // to itself: its meads come out 1.5 s later, at its output
                    }
                    break;
                case Smelter smelter:
                    if (smelter.m_spawnStack && Settings.EmptyWindmillEarly.Value && zdo.GetInt(ZDOVars.s_spawnAmount) > 0)
                    {
                        fed.View.InvokeRPC("RPC_EmptyProcessed");
                    }
                    break;
                case Beehive hive:
                    if (zdo.GetInt(ZDOVars.s_level) >= hive.m_maxHoney && hive.m_maxHoney > 0)
                    {
                        fed.View.InvokeRPC("RPC_Extract");
                    }
                    break;
                case SapCollector sap:
                    if (zdo.GetInt(ZDOVars.s_level) >= sap.m_maxLevel && sap.m_maxLevel > 0)
                    {
                        fed.View.InvokeRPC("RPC_Extract");
                    }
                    break;
            }
            Transform output = Stations.OutputOf(fed);
            if (output == null)
            {
                return;
            }
            HashSet<string> products = Stations.ProductsOf(fed);
            var ready = new List<ItemDrop>();
            DateTime worldNow = ZNet.instance.GetTime();
            foreach (ItemDrop drop in Lying.At(output.position, Reach))
            {
                ZNetView view = drop.GetComponent<ZNetView>();
                // Never one he dropped on purpose (the game marks those not to be picked up by itself).
                if (!GroundPut.Ours(drop, view) || !drop.m_autoPickup || drop.m_itemData.m_shared == null ||
                    !products.Contains(drop.m_itemData.m_shared.m_name))
                {
                    continue;
                }
                long born = view.GetZDO().GetLong(ZDOVars.s_spawnTime, 0L);
                if (born > 0L && (worldNow - new DateTime(born)).TotalSeconds < Settings.WaitBeforePutting.Value)
                {
                    continue;   // let it land
                }
                ready.Add(drop);
            }
            if (ready.Count > 0)
            {
                PutAway(fed, ready);
            }
        }

        // Into its linked output chests as far as they have room; the rest (or all, with none linked) as the settings say:
        // decided for each product apart (a full output's room for iron says nothing of copper: code review n5).
        private static void PutAway(Fed fed, List<ItemDrop> ready)
        {
            Links.Resolve(fed, out List<Container> _, out List<Container> outputs);
            var moved = new StowResult();
            List<Container> nearby = null;
            foreach (IGrouping<string, ItemDrop> product in ready.GroupBy(drop => drop.m_itemData.m_shared.m_name))
            {
                List<ItemDrop> drops = product.ToList();
                int amount = drops.Sum(drop => drop.m_itemData.m_stack);
                OutputPlan plan = OutputChoice.Decide(amount, outputs.Count > 0, LinkedRoom(outputs, product.Key),
                                                      Settings.NoLinkedOutput.Value, Settings.LinkedOutputFull.Value);
                if (plan.IntoLinked > 0)
                {
                    // Linked outputs take whatever comes, in any cell: he chose them for it. Nearest the station first.
                    Vector3 at = fed.Position;
                    List<Container> nearest = outputs.OrderBy(chest => (chest.transform.position - at).sqrMagnitude).ToList();
                    StackRules rules = Stow.Rules(Settings.Rules(ChestSettings.MostRange), onlyWhereHeld: false, followSetups: false, leaveOthersMarked: false);
                    rules.Chests.UseReserved = true;   // a chest he linked is used though it carries a reserve mark
                    Merge(moved, Stow.Items(drops, nearest, rules));
                }
                if (plan.PutAway > 0)
                {
                    List<ItemDrop> rest = drops.Where(drop => drop != null && GroundPut.Ours(drop, drop.GetComponent<ZNetView>()) && drop.m_itemData.m_stack > 0).ToList();
                    if (rest.Count > 0)
                    {
                        ChestRules near = Settings.Rules(Settings.StationOutputReach);   // card B30: the output's own radius
                        nearby = nearby ?? ChestFinder.Find(fed.Position, near).Where(chest => !outputs.Contains(chest)).ToList();
                        StackRules rules = Stow.Rules(near, Settings.OnlyWhereHeld.Value, Settings.FollowSetups.Value, Settings.LeaveOthersMarked.Value);
                        Merge(moved, Stow.Items(rest, nearby, rules));
                    }
                }
            }
            Tell(fed, moved);
        }

        /// <summary>How many of a product (by shared name) the linked output chests have room for, from this computer's
        /// copies: room in their part-stacks of it and a full stack in each empty cell.</summary>
        internal static int LinkedRoom(List<Container> outputs, string product)
        {
            ItemDrop sample = RuleLists.ItemByShared(product);
            if (sample == null || outputs == null)
            {
                return 0;
            }
            int most = Mathf.Max(1, sample.m_itemData.m_shared.m_maxStackSize);
            int worldLevel = Game.m_worldLevel;
            int room = 0;
            foreach (Container chest in outputs)
            {
                Inventory inventory = chest != null ? chest.GetInventory() : null;
                if (inventory == null)
                {
                    continue;
                }
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item.m_shared.m_name == product && item.m_quality == 1 && item.m_worldLevel == worldLevel && item.m_stack < most)
                    {
                        room += most - item.m_stack;
                    }
                }
                room += inventory.GetEmptySlots() * most;
            }
            return room;
        }

        private static bool FermenterReady(Fermenter fermenter, ZDO zdo)
        {
            if (zdo.GetInt(ZDOVars.s_content) == 0)
            {
                return false;
            }
            long start = zdo.GetLong(ZDOVars.s_startTime, 0L);
            return start != 0L && (ZNet.instance.GetTime() - new DateTime(start)).TotalSeconds > fermenter.m_fermentationDuration;
        }

        private static void Merge(StowResult into, StowResult from)
        {
            foreach (KeyValuePair<Container, Dictionary<string, int>> chest in from.Into)
            {
                foreach (KeyValuePair<string, int> item in chest.Value)
                {
                    into.Add(chest.Key, item.Key, item.Value);
                }
            }
        }

        // "Put away 10 Iron": as the settings say, and a log line with "Write what's fed to the log".
        private static void Tell(Fed fed, StowResult moved)
        {
            if (moved.Total <= 0)
            {
                return;
            }
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Dictionary<string, int> items in moved.Into.Values)
            {
                foreach (KeyValuePair<string, int> item in items)
                {
                    totals.TryGetValue(item.Key, out int sum);
                    totals[item.Key] = sum + item.Value;
                }
            }
            string what = string.Join(", ", totals.Select(each => $"{each.Value} {Names.Item(each.Key)}"));
            string text = $"Put away {what} from the {Names.Of(fed)}";
            switch (Settings.Say.Value)
            {
                case SayAdded.TopLeft:
                    QoLMods.Kit.Say.TopLeft(text, log: true);
                    break;
                case SayAdded.Centre:
                    QoLMods.Kit.Say.Center(text);
                    break;
            }
            if (Settings.LogFeeds.Value)
            {
                Module.Log.LogInfo($"{text} into {moved.Into.Count} chest(s).");
            }
        }
    }
}
