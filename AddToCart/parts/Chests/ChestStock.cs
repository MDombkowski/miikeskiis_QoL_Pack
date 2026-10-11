// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>Some of an item in the chests loaded near the player, and why a count leaves it out (ChestStock.Explain).</summary>
    internal sealed class LeftOutItem
    {
        /// <summary>The item's shared name ("$item_wood"), as the game stacks it.</summary>
        internal string Name;

        internal int Amount;

        /// <summary>How far away the nearest chest holding it for this reason is, in metres.</summary>
        internal float Distance;

        /// <summary>Why it is left out, as a clause for him to read ("the chest is out of range …"); null when a count
        /// by these rules includes it.</summary>
        internal string Why;

        /// <summary>The item and the chest, for the log: prefab, quality, world level, the chest's prefab.</summary>
        internal string Detail;
    }

    /// <summary>What the chests near the player hold, counted in one pass (ChestStock.Survey).</summary>
    internal sealed class ChestSurvey
    {
        /// <summary>What may be taken, by item key: every item by its name (all qualities together), and each quality
        /// of an item that has several (gear, fish) by its own key too. The two overlap: a name's count includes its
        /// levels' (CartMath.Settled sorts that out). Leave-one is already taken off.</summary>
        internal readonly Dictionary<string, int> Stock = new Dictionary<string, int>();

        /// <summary>The same as Stock, by the same keys and chests, but whole: nothing kept back by leave-one (Add to Cart's
        /// "True counts", card A83). Equal to Stock when the rules don't leave one.</summary>
        internal readonly Dictionary<string, int> Whole = new Dictionary<string, int>();

        /// <summary>What reserved chests hold, by the same keys, whole (nothing is kept back from a chest never taken
        /// from): never taken, counted apart for display.</summary>
        internal readonly Dictionary<string, int> Reserved = new Dictionary<string, int>();

        /// <summary>One item of each kind found (by ChestStock.KeyOf), for its name, icon and type.</summary>
        internal readonly Dictionary<string, ItemDrop.ItemData> Samples = new Dictionary<string, ItemDrop.ItemData>();

        /// <summary>Chests counted (another player's open chest is left out, since a fetch would skip it, unless his game
        /// shares it: ChestShare).</summary>
        internal int Chests;

        /// <summary>Reserved chests seen within range.</summary>
        internal int ReservedChests;

        internal int StockOf(string key) => Stock.TryGetValue(key, out int amount) ? amount : 0;

        internal int WholeOf(string key) => Whole.TryGetValue(key, out int amount) ? amount : 0;

        internal int ReservedOf(string key) => Reserved.TryGetValue(key, out int amount) ? amount : 0;
    }

    /// <summary>
    /// What may be taken from chests, and the taking itself.
    ///
    /// Items are named by a key. A plain key is the item's shared name ("$item_wood") and means any quality, the way the
    /// game counts a recipe's or a piece's materials. A key "name@quality" means that one quality of an item that has
    /// several (gear), the way the Take tab lists them. Only items the game counts for crafting at this world's level
    /// are counted (Inventory.CountItems). "Leave one" leaves one of each stackable item in every chest; it never keeps
    /// back a lone sword or a single piece of armour, unless the caller asks for gear too (ChestRules.LeaveOneOfGear, the
    /// gearToo parameters: KeepsOne).
    /// </summary>
    internal static class ChestStock
    {
        /// <summary>The key the Take tab lists an item under: its name, or name@quality for gear.</summary>
        internal static string KeyOf(ItemDrop.ItemData item) =>
            item.m_shared.m_maxQuality > 1 ? $"{item.m_shared.m_name}@{item.m_quality}" : item.m_shared.m_name;

        /// <summary>Whether leave-one keeps one of this item back: anything that stacks, and with <paramref name="gearToo"/>
        /// (ChestRules.LeaveOneOfGear) gear as well (anything that doesn't). The one place the rule is written.</summary>
        internal static bool KeepsOne(ItemDrop.ItemData item, bool gearToo) => gearToo || item.m_shared.m_maxStackSize > 1;

        /// <summary>Splits a key into the item's name and its quality (-1: any).</summary>
        internal static void Parse(string key, out string name, out int quality)
        {
            int at = key.LastIndexOf('@');
            if (at > 0 && int.TryParse(key.Substring(at + 1), out quality))
            {
                name = key.Substring(0, at);
                return;
            }
            name = key;
            quality = -1;
        }

        /// <summary>How much of one item (by key) may be taken from one chest: with <paramref name="leaveOne"/>, all but one
        /// of an item leave-one keeps back (KeepsOne: stackable, or gear too with <paramref name="gearToo"/>).</summary>
        internal static int Count(Container chest, string key, bool leaveOne, bool gearToo = false)
        {
            Parse(key, out string name, out int quality);
            int total = 0;
            bool keeps = false;
            foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
            {
                if (item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) && Counts(item))
                {
                    total += item.m_stack;
                    keeps |= KeepsOne(item, gearToo);
                }
            }
            return leaveOne && keeps && total > 0 ? total - 1 : total;
        }

        /// <summary>
        /// Counts the usable chests near <paramref name="centre"/> from this computer's copies: what may be taken, by key,
        /// and apart from it what reserved chests hold (unless the rules use them like any other: then they count as
        /// stock). A chest another player has open is left out, unless his game shares it (ChestShare.CanShare: a friend on
        /// the pack, at the chest), which counts like any other.
        /// </summary>
        internal static ChestSurvey Survey(Vector3 centre, ChestRules rules)
        {
            var survey = new ChestSurvey();
            var byName = new Dictionary<string, int>();
            var keeps = new HashSet<string>();   // the keys leave-one keeps one of back (KeepsOne)
            var byQuality = new Dictionary<string, int>();
            foreach (Container chest in ChestFinder.Find(centre, rules, withReserved: true))
            {
                if (ChestFinder.OpenElsewhere(chest) && !ChestShare.CanShare(chest))
                {
                    continue;
                }
                bool reserved = !rules.UseReserved && ChestReserve.IsReserved(chest);
                if (reserved)
                {
                    survey.ReservedChests++;
                }
                else
                {
                    survey.Chests++;
                }
                byName.Clear();
                keeps.Clear();
                byQuality.Clear();
                foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
                {
                    if (!Counts(item))
                    {
                        continue;
                    }
                    string name = item.m_shared.m_name;
                    byName.TryGetValue(name, out int sum);
                    byName[name] = sum + item.m_stack;
                    bool keepsOne = KeepsOne(item, rules.LeaveOneOfGear);
                    if (keepsOne)
                    {
                        keeps.Add(name);
                    }
                    string key = KeyOf(item);
                    if (key != name)
                    {
                        byQuality.TryGetValue(key, out int each);
                        byQuality[key] = each + item.m_stack;
                        if (keepsOne)
                        {
                            keeps.Add(key);
                        }
                    }
                    if (!survey.Samples.ContainsKey(key))
                    {
                        survey.Samples[key] = item;
                    }
                }
                // A reserved chest's contents are counted whole: nothing is taken from it, so none is kept back.
                if (reserved)
                {
                    foreach (KeyValuePair<string, int> each in byName.Concat(byQuality))
                    {
                        Add(survey.Reserved, each.Key, each.Value);
                    }
                    continue;
                }
                bool keepOne = rules.LeaveOne;
                foreach (KeyValuePair<string, int> each in byName.Concat(byQuality))
                {
                    // As Count does: one of each name, and of each level of an item with levels, that leave-one keeps back
                    // (stackable: fish by level; gear only with LeaveOneOfGear) stays behind. Whole counts it all.
                    int amount = keepOne && keeps.Contains(each.Key) ? each.Value - 1 : each.Value;
                    Add(survey.Stock, each.Key, amount);
                    Add(survey.Whole, each.Key, each.Value);
                }
            }
            return survey;
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> of an item (by key) from a chest this computer owns into the player's
        /// inventory, never the last one of an item leave-one keeps back with <paramref name="leaveOne"/> (KeepsOne: a stackable
        /// item, or gear too with <paramref name="gearToo"/>). Each part goes into his
        /// inventory first and comes out of the chest only as far as it arrived, so a full inventory can't lose or copy
        /// anything. Returns how many moved. A chest carrying Reserve Stock's mark gives nothing, unless
        /// <paramref name="useReserved"/> (ChestRules.UseReserved) says reserved chests give like any other: the mark can
        /// be set on a chest after it was planned for (card A12). A stack <paramref name="spare"/> names is left where it is
        /// (the one in the hand of a player who has the chest open: ChestShare).
        /// </summary>
        internal static int Take(Container chest, Inventory into, string key, int amount, bool leaveOne, bool useReserved = false,
                                 Func<ItemDrop.ItemData, bool> spare = null, bool gearToo = false)
        {
            if (!useReserved && ChestReserve.IsReserved(chest))
            {
                return 0;
            }
            Parse(key, out string name, out int quality);
            Inventory from = chest.GetInventory();
            var stacks = new List<ItemDrop.ItemData>();
            int total = 0;
            bool keeps = false;
            foreach (ItemDrop.ItemData item in from.GetAllItems())
            {
                if (item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) && Counts(item))
                {
                    stacks.Add(item);
                    total += item.m_stack;
                    keeps |= KeepsOne(item, gearToo);
                }
            }
            int allowed = Mathf.Min(amount, total - (leaveOne && keeps ? 1 : 0));
            // Lowest levels first, for a take by name (a recipe's ingredient): a spare, not his upgraded weapon. Then the
            // smallest stacks: they empty whole cells, and what's left behind stays in one stack.
            stacks.Sort((a, b) => TakeOrder(a, b, a.m_stack, b.m_stack));
            int moved = 0;
            foreach (ItemDrop.ItemData stack in stacks)
            {
                if (moved >= allowed)
                {
                    break;
                }
                if (spare != null && spare(stack))
                {
                    continue;
                }
                int part = Mathf.Min(stack.m_stack, allowed - moved);
                ItemDrop.ItemData copy = stack.Clone();
                copy.m_stack = part;
                int before = CountSame(into, stack);
                int added = 0;
                try
                {
                    into.AddItem(copy);
                }
                finally
                {
                    // Also when another mod's handler of his inventory's change throws inside AddItem: what arrived
                    // leaves the chest before the error goes on (and switches the module off).
                    added = Mathf.Clamp(CountSame(into, stack) - before, 0, part);
                    if (added > 0)
                    {
                        // What really leaves the chest: another mod may have moved that stack out of it, or part of it
                        // elsewhere in it, while the copy arrived (re-sorting the chest when his inventory changed). The chest
                        // still holds the rest, so that much goes back out of his inventory.
                        int leaves = from.ContainsItem(stack) ? Mathf.Min(stack.m_stack, added) : 0;
                        if (leaves > 0 && !from.RemoveItem(stack, leaves))
                        {
                            leaves = 0;
                        }
                        moved += leaves;
                        if (leaves < added)
                        {
                            TakeBack(into, copy, added - leaves);
                            added = leaves;
                        }
                    }
                }
                if (added < part)
                {
                    break;   // his inventory has no room for more of this item
                }
            }
            return moved;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> of an item (by key) from a chest this computer owns, for something the
        /// game uses up where it stands (a craft's or a piece's materials, a station's fuel), never the last one of an item
        /// leave-one keeps back with <paramref name="leaveOne"/> (KeepsOne, as Take). Lowest levels first, then the smallest
        /// stacks, as Take does.
        /// Changes nothing, and returns 0, on a chest this computer doesn't own, or on one carrying Reserve Stock's mark
        /// unless <paramref name="useReserved"/> says reserved chests give like any other (as Take). Returns how many went.
        /// The caller sends the chest (ZDOMan.ForceSendZDO) and holds it (ChestClaims.Hold), as ChestFetch does after a take.
        /// </summary>
        internal static int Remove(Container chest, string key, int amount, bool leaveOne, bool useReserved = false, bool gearToo = false)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (amount <= 0 || view == null || !view.IsValid() || !view.IsOwner() || (!useReserved && ChestReserve.IsReserved(chest)))
            {
                return 0;
            }
            Parse(key, out string name, out int quality);
            Inventory from = chest.GetInventory();
            var stacks = new List<ItemDrop.ItemData>();
            int total = 0;
            bool keeps = false;
            foreach (ItemDrop.ItemData item in from.GetAllItems())
            {
                if (item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) && Counts(item))
                {
                    stacks.Add(item);
                    total += item.m_stack;
                    keeps |= KeepsOne(item, gearToo);
                }
            }
            int allowed = Mathf.Min(amount, total - (leaveOne && keeps ? 1 : 0));
            stacks.Sort((a, b) => TakeOrder(a, b, a.m_stack, b.m_stack));
            int removed = 0;
            foreach (ItemDrop.ItemData stack in stacks)
            {
                if (removed >= allowed)
                {
                    break;
                }
                int part = Mathf.Min(stack.m_stack, allowed - removed);
                if (from.RemoveItem(stack, part))
                {
                    removed += part;
                }
            }
            return removed;
        }

        /// <summary>
        /// Where the items <paramref name="matches"/> picks lie, in every chest loaded on this computer, and why a count by
        /// these rules leaves each out (Why is null where it counts): one line per item and reason, nearest first. For
        /// telling him why something he can see in a chest isn't offered; never for taking.
        /// </summary>
        internal static List<LeftOutItem> Explain(Vector3 centre, ChestRules rules, Func<ItemDrop.ItemData, bool> matches)
        {
            var found = new Dictionary<string, LeftOutItem>();
            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            // The chests the service knows of, and every chest the scene holds: one it never heard of would be a fault.
            var known = new HashSet<Container>(ChestFinder.Loaded());
            var chests = new HashSet<Container>(known);
            chests.UnionWith(UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None));
            foreach (Container chest in chests)
            {
                Inventory inventory = chest != null ? chest.GetInventory() : null;
                if (inventory == null)
                {
                    continue;
                }
                List<ItemDrop.ItemData> matching = inventory.GetAllItems().Where(item => item?.m_shared != null && matches(item)).ToList();
                if (matching.Count == 0)
                {
                    continue;
                }
                float distance = Vector3.Distance(chest.transform.position, centre);
                string chestWhy = WhyChestLeftOut(chest, distance, rules, playerId)
                                  ?? (known.Contains(chest) ? null : "the mod never saw the chest come into being: a fault in the mod");
                // As Survey counts for the Take tab: by the Take key (name, or name@quality for items with levels), at this
                // world's level, and with "leave one", one of each key it keeps back left behind (review 1, finding 3: fish).
                var counted = new Dictionary<string, int>();
                foreach (ItemDrop.ItemData item in matching.Where(Counts))
                {
                    string key = KeyOf(item);
                    counted.TryGetValue(key, out int sum);
                    counted[key] = sum + item.m_stack;
                }
                foreach (ItemDrop.ItemData item in matching)
                {
                    string name = item.m_shared.m_name;
                    string why = chestWhy ?? WhyItemLeftOut(item.m_worldLevel, Game.m_worldLevel, rules.LeaveOne, KeepsOne(item, rules.LeaveOneOfGear),
                                                            counted.TryGetValue(KeyOf(item), out int inChest) ? inChest : 0,
                                                            gear: !KeepsOne(item, false));
                    string key = name + "\n" + why;
                    if (!found.TryGetValue(key, out LeftOutItem each))
                    {
                        found[key] = each = new LeftOutItem
                        {
                            Name = name,
                            Distance = distance,
                            Why = why,
                            Detail = $"{(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?")}, quality {item.m_quality} of " +
                                     $"{item.m_shared.m_maxQuality}, world level {item.m_worldLevel}, in {chest.gameObject.name} {distance:0} m away",
                        };
                    }
                    each.Amount += item.m_stack;
                    each.Distance = Mathf.Min(each.Distance, distance);
                }
            }
            return found.Values.OrderBy(each => each.Distance).ToList();
        }

        /// <summary>
        /// Why a count leaves out an item lying in a chest it counts, as Survey decides it (null: it counts, and some of it
        /// may be taken): below the world's level, or the last of an item leave-one keeps back (<paramref name="keepsOne"/>:
        /// KeepsOne) when one of each stays behind. <paramref name="countedInChest"/> is how many of it that chest holds at
        /// the world's level, by its Take key (KeyOf: one level of an item with levels, as the Take tab lists it).
        /// <paramref name="gear"/>: it doesn't stack, so only "Leave one of gear too" (Add to Cart's, the one module that asks
        /// for gear) keeps it back, and the reason names that setting too.
        /// </summary>
        internal static string WhyItemLeftOut(int itemWorldLevel, int worldLevel, bool leaveOne, bool keepsOne, int countedInChest, bool gear = false)
        {
            if (itemWorldLevel < worldLevel)
            {
                return $"it is from a lower world level ({itemWorldLevel}; the world's is {worldLevel}), which the game won't count either";
            }
            if (leaveOne && keepsOne && countedInChest <= 1)
            {
                return gear
                    ? "the last piece of gear stays in its chest (the settings \"Leave one of each item in chests\" and \"Leave one of gear too\")"
                    : "the last one of an item stays in its chest (the setting \"Leave one of each item in chests\")";
            }
            return null;
        }

        // Why a count leaves this whole chest out, as Survey decides it; null if it counts the chest.
        private static string WhyChestLeftOut(Container chest, float distance, ChestRules rules, long playerId)
        {
            if (distance > rules.Range)
            {
                return $"the chest is out of range ({distance:0} m; the range is {rules.Range:0} m)";
            }
            string refused = ChestFinder.WhyNot(chest, rules, playerId);
            if (refused != null)
            {
                return "the chest is " + refused;
            }
            if (!rules.UseReserved && ChestReserve.IsReserved(chest))
            {
                return "the chest is reserved";
            }
            return ChestFinder.OpenElsewhere(chest) && !ChestShare.CanShare(chest) ? "someone else has the chest open, and their game doesn't share it" : null;
        }

        /// <summary>
        /// The order a take picks stacks of one item in (Take, Remove, and ChestShare.Room, which must pick as Take does):
        /// lowest level first, then the smallest stack (<paramref name="stackA"/> and <paramref name="stackB"/>: what is left
        /// of each), then the lower world level, then the cell, top left first. A total order, so the same stacks always
        /// come in the same order (List.Sort isn't stable).
        /// </summary>
        internal static int TakeOrder(ItemDrop.ItemData a, ItemDrop.ItemData b, int stackA, int stackB)
        {
            if (a.m_quality != b.m_quality)
            {
                return a.m_quality.CompareTo(b.m_quality);
            }
            if (stackA != stackB)
            {
                return stackA.CompareTo(stackB);
            }
            if (a.m_worldLevel != b.m_worldLevel)
            {
                return a.m_worldLevel.CompareTo(b.m_worldLevel);
            }
            return a.m_gridPos.y != b.m_gridPos.y ? a.m_gridPos.y.CompareTo(b.m_gridPos.y) : a.m_gridPos.x.CompareTo(b.m_gridPos.x);
        }

        /// <summary>Counted as the game counts a material for crafting: at least this world's level.</summary>
        private static bool Counts(ItemDrop.ItemData item) => item.m_worldLevel >= Game.m_worldLevel;

        private static void Add(Dictionary<string, int> into, string key, int amount)
        {
            if (amount > 0)
            {
                into.TryGetValue(key, out int sum);
                into[key] = sum + amount;
            }
        }

        /// <summary>Takes back out of an inventory that many of what a take (or a put, ChestPut) had just added there: the
        /// added copy itself first (gear is added as that very object; so is a new stack), then, for what merged into stacks
        /// already there, as many from stacks of the same name, quality and world level (the game stacks only those together,
        /// so they are alike), the last first, never an equipped one.</summary>
        internal static void TakeBack(Inventory inventory, ItemDrop.ItemData copy, int amount)
        {
            if (inventory.ContainsItem(copy))
            {
                int part = Mathf.Min(copy.m_stack, amount);
                if (inventory.RemoveItem(copy, part))
                {
                    amount -= part;
                }
            }
            if (copy.m_shared.m_maxStackSize <= 1)
            {
                return;   // gear never merges: the copy was the one item added
            }
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            for (int i = items.Count - 1; i >= 0 && amount > 0; i--)
            {
                ItemDrop.ItemData item = items[i];
                if (!item.m_equipped && item.m_shared.m_name == copy.m_shared.m_name && item.m_quality == copy.m_quality &&
                    item.m_worldLevel == copy.m_worldLevel && inventory.ContainsItem(item))
                {
                    int part = Mathf.Min(item.m_stack, amount);
                    if (inventory.RemoveItem(item, part))
                    {
                        amount -= part;
                    }
                }
            }
        }

        /// <summary>The items the game would stack together with this one (Inventory.FindFreeStackItem): same name, quality
        /// and world level. Adding a copy only ever grows these.</summary>
        internal static int CountSame(Inventory inventory, ItemDrop.ItemData like)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == like.m_shared.m_name && item.m_quality == like.m_quality && item.m_worldLevel == like.m_worldLevel)
                {
                    count += item.m_stack;
                }
            }
            return count;
        }
    }
}
