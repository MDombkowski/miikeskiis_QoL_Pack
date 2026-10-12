using System;
using System.Collections.Generic;
using QoLMods.Chests;
using TopperUpper.Logic;

namespace TopperUpper
{
    /// <summary>What a put-away moved: by chest, how many of each item (by shared name).</summary>
    internal sealed class StowResult
    {
        internal readonly Dictionary<Container, Dictionary<string, int>> Into = new Dictionary<Container, Dictionary<string, int>>();

        internal int Total;

        internal void Add(Container chest, string item, int amount)
        {
            if (!Into.TryGetValue(chest, out Dictionary<string, int> items))
            {
                Into[chest] = items = new Dictionary<string, int>(StringComparer.Ordinal);
            }
            items.TryGetValue(item, out int sum);
            items[item] = sum + amount;
            Total += amount;
        }
    }

    /// <summary>
    /// Putting items lying on the ground away into chests, the way the quick stack puts his items away (the research's
    /// 4.3): the chest service's own routing (StackRouting: a chest holding the item by name first, set-up chests by
    /// their setups, others' marked chests left alone, as the rules say) and planning (StackPlanner, from counts, adding as
    /// the game adds), then each put measured (GroundPut), all in one frame, into chests this game holds and may change
    /// now, never one it would have to claim, never the Obliterator or a chest kept for a craft whose bar is running
    /// (ChestUse). Every chest put into is held and sent in a finally. For a station's
    /// products here; the loose-item pickup uses it too (one call per batch: one put and one line per chest).
    /// </summary>
    internal static class Stow
    {
        /// <summary>The quick stack's rules as Topper-Upper's settings say: which chests, only into chests already holding
        /// it (or set up for it), setups followed, others' marked chests left alone.</summary>
        internal static StackRules Rules(ChestRules chests, bool onlyWhereHeld, bool followSetups, bool leaveOthersMarked) => new StackRules
        {
            Chests = chests,
            Trophies = TrophyRule.LikeAnyItem,
            OnlyWhereHeld = onlyWhereHeld,
            TopUpFirst = true,
            LeaveOthersMarked = leaveOthersMarked,
            Setups = followSetups ? new SetupRules { Of = Setups.Reading, KeepHeld = false, Overflow = true, Others = true } : null,
        };

        /// <summary>
        /// Puts the items lying on the ground (only those this game owns) into the chests (nearest first; only those this game
        /// holds and may change now, and no reserved one unless the rules use them), as the rules route them. Returns what
        /// went where. Nothing is claimed or asked for.
        /// </summary>
        internal static StowResult Items(IList<ItemDrop> drops, IList<Container> chests, StackRules rules)
        {
            var result = new StowResult();
            var usable = new List<ItemDrop>();
            var items = new List<ItemDrop.ItemData>();
            var models = new List<StackPlanner.Item>();
            foreach (ItemDrop drop in drops)
            {
                ZNetView view = drop != null ? drop.GetComponent<ZNetView>() : null;
                if (!GroundPut.Ours(drop, view) || usable.Contains(drop))
                {
                    continue;
                }
                drop.Load();
                ItemDrop.ItemData item = drop.m_itemData;
                if (item?.m_shared == null || item.m_stack <= 0 || item.m_dropPrefab == null)
                {
                    continue;
                }
                usable.Add(drop);
                items.Add(item);
                models.Add(new StackPlanner.Item
                {
                    Name = item.m_shared.m_name,
                    Quality = item.m_quality,
                    WorldLevel = item.m_worldLevel,
                    Amount = item.m_stack,
                    MaxStack = item.m_shared.m_maxStackSize,
                });
            }
            if (usable.Count == 0)
            {
                return result;
            }
            var owned = new List<Container>();
            var contents = new List<Inventory>();
            var chestModels = new List<StackPlanner.Chest>();
            if (!Chests(chests, rules, owned, contents, chestModels))
            {
                return result;
            }
            List<IList<IList<int>>> tiers = StackRouting.Tiers(items, owned, contents, rules)
                .ConvertAll(each => (IList<IList<int>>)each.ConvertAll(tier => (IList<int>)tier));
            List<StackPlanner.Put> plan = StackPlanner.Plan(models, chestModels, i => tiers[i], rules.TopUpFirst);
            var changed = new HashSet<Container>();
            try
            {
                foreach (StackPlanner.Put put in plan)
                {
                    Container chest = owned[put.Chest];
                    ItemDrop drop = usable[put.Item];
                    string name = items[put.Item].m_shared.m_name;
                    bool marked = changed.Add(chest);   // first: a throw inside the put still sends and holds it
                    int moved = GroundPut.Put(chest, drop, put.Amount, put.TopUpOnly, rules.Chests.UseReserved);
                    if (moved > 0)
                    {
                        result.Add(chest, name, moved);
                    }
                    else if (marked)
                    {
                        changed.Remove(chest);
                    }
                }
            }
            finally
            {
                SendAndHold(changed);
            }
            return result;
        }

        /// <summary>
        /// Puts stacks lying in a scratch inventory (the catch-up's products, made at once: card A93) into the chests the
        /// same way as Items: the same routing and planning, the same chests (this game's, changeable now, never the
        /// Obliterator, a chest kept for a craft or a reserved one unless the rules use them), each put measured (ChestPut,
        /// the mirror of a take: what arrived leaves the scratch inventory, exactly). What doesn't fit stays in the scratch
        /// inventory, for the caller. Every chest put into is held and sent in a finally.
        /// </summary>
        internal static StowResult FromInventory(Inventory from, IList<ItemDrop.ItemData> stacks, IList<Container> chests, StackRules rules)
        {
            var result = new StowResult();
            var items = new List<ItemDrop.ItemData>();
            var models = new List<StackPlanner.Item>();
            foreach (ItemDrop.ItemData item in stacks)
            {
                if (item?.m_shared == null || item.m_stack <= 0 || item.m_dropPrefab == null || !from.ContainsItem(item) || items.Contains(item))
                {
                    continue;
                }
                items.Add(item);
                models.Add(new StackPlanner.Item
                {
                    Name = item.m_shared.m_name,
                    Quality = item.m_quality,
                    WorldLevel = item.m_worldLevel,
                    Amount = item.m_stack,
                    MaxStack = item.m_shared.m_maxStackSize,
                });
            }
            if (items.Count == 0)
            {
                return result;
            }
            var owned = new List<Container>();
            var contents = new List<Inventory>();
            var chestModels = new List<StackPlanner.Chest>();
            if (!Chests(chests, rules, owned, contents, chestModels))
            {
                return result;
            }
            List<IList<IList<int>>> tiers = StackRouting.Tiers(items, owned, contents, rules)
                .ConvertAll(each => (IList<IList<int>>)each.ConvertAll(tier => (IList<int>)tier));
            List<StackPlanner.Put> plan = StackPlanner.Plan(models, chestModels, i => tiers[i], rules.TopUpFirst);
            var changed = new HashSet<Container>();
            try
            {
                foreach (StackPlanner.Put put in plan)
                {
                    Container chest = owned[put.Chest];
                    ItemDrop.ItemData item = items[put.Item];
                    string name = item.m_shared.m_name;
                    bool marked = changed.Add(chest);   // first: a throw inside the put still sends and holds it
                    int moved = ChestPut.Put(chest, from, item, put.Amount, put.TopUpOnly, rules.Chests.UseReserved);
                    if (moved > 0)
                    {
                        result.Add(chest, name, moved);
                    }
                    else if (marked)
                    {
                        changed.Remove(chest);
                    }
                }
            }
            finally
            {
                SendAndHold(changed);
            }
            return result;
        }

        // The chests a put may use, each reloaded (the newest copy, in the frame of the put), with what they hold as the
        // planner sees it. False when none may be used.
        private static bool Chests(IList<Container> chests, StackRules rules, List<Container> owned, List<Inventory> contents, List<StackPlanner.Chest> chestModels)
        {
            foreach (Container chest in chests)
            {
                if (chest == null || owned.Contains(chest) ||
                    ChestUse.WhyNot(ChestClaims.MayChange(chest), Obliterator.Is(chest), ChestClaims.IsKeptForCraft(chest),
                                    ChestReserve.IsReserved(chest), rules.Chests.UseReserved) != null)
                {
                    continue;   // never the Obliterator (M2) or a chest kept for a craft (m4)
                }
                ChestHooks.Reload(chest);   // the newest copy, in the frame of the put
                Inventory inventory = chest.GetInventory();
                owned.Add(chest);
                contents.Add(inventory);
                var model = new StackPlanner.Chest { Cells = inventory.GetWidth() * inventory.GetHeight() };
                foreach (ItemDrop.ItemData held in inventory.GetAllItems())
                {
                    model.Stacks.Add(new StackPlanner.Item
                    {
                        Name = held.m_shared.m_name,
                        Quality = held.m_quality,
                        WorldLevel = held.m_worldLevel,
                        Amount = held.m_stack,
                        MaxStack = held.m_shared.m_maxStackSize,
                    });
                }
                chestModels.Add(model);
            }
            return owned.Count > 0;
        }

        // Every chest changed is held (other players' requests for it dropped until their copies have reloaded) and its
        // record sent at once. Never throws: it runs in a finally.
        private static void SendAndHold(IEnumerable<Container> changed)
        {
            foreach (Container chest in changed)
            {
                try
                {
                    ZNetView view = ChestHooks.NetView(chest);
                    if (view != null && view.IsValid())
                    {
                        ChestClaims.Hold(chest);
                        ZDOMan.instance?.ForceSendZDO(view.GetZDO().m_uid);
                    }
                }
                catch (Exception error)
                {
                    Module.Warn($"A chest put into couldn't be sent or held: {error}");
                }
            }
        }
    }
}
