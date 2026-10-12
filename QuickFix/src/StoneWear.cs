using System.Collections.Generic;
using UnityEngine;

namespace QuickFix
{
    /// <summary>
    /// The Sharpening Stone wearing like a tool (his pick, 2026-10-11; decision 12). While a share of a stone's
    /// durability is the cost and the mod is on, the stone prefab's shared data says it has durability, so the game draws
    /// its own bar on a worn stone (only below full: InventoryGrid, HotkeyBar) and its tooltip shows it, and says it can't
    /// be repaired, so no station mends it. A worn stone never stacks: it gets a cell of its own when there's room, and
    /// nothing joins it (Chosen, RefusesDrag); at 0 it is used up and the next stone carries on. Otherwise the stone is put
    /// back as the game has it, and a worn stone keeps its wear, unseen, until a share is the cost again. Research part C,
    /// section 4; the merge rules from 65-B's two reviews (reports\2026-10-11_quick-fix-code-review.md).
    /// <para>
    /// Outside the editor every stone made, crafted or loaded holds its own copy of the shared data (Unity copies it when
    /// the prefab is instantiated; the game points items back at the prefab's only in the editor: Humanoid.EquipItem,
    /// ItemDrop.Awake), so the stones in his inventory are pointed at the prefab's once a second, and at the put-back.
    /// </para>
    /// </summary>
    internal static class StoneWear
    {
        /// <summary>The name of the character's row count among his unique keys (Player.InventoryRowsKey): rows below it
        /// belong to other mods (EquipmentAndQuickSlots' equipment and quick slots), so a split stone never goes there.</summary>
        private const string RowsKey = "invrows";

        private static ObjectDB db;
        private static ItemDrop.ItemData.SharedData prefab;
        private static ItemDrop.ItemData.SharedData applied;
        private static bool hadDurability;
        private static bool couldBeRepaired;
        private static float nextSweep;

        /// <summary>Whether the stone wears now.</summary>
        internal static bool On => applied != null;

        /// <summary>Every frame: the stone prefab's shared data follows the settings; once a second, the stones he carries
        /// are pointed at it.</summary>
        internal static void Follow(bool moduleOn)
        {
            ItemDrop.ItemData.SharedData shared = Shared();
            ItemDrop.ItemData.SharedData wanted = moduleOn && Settings.StoneWears ? shared : null;
            if (applied != null && !ReferenceEquals(applied, wanted))
            {
                PutBack();
            }
            if (wanted != null && applied == null)
            {
                hadDurability = wanted.m_useDurability;
                couldBeRepaired = wanted.m_canBeReparied;
                wanted.m_useDurability = true;
                wanted.m_canBeReparied = false;
                applied = wanted;
                nextSweep = 0f;
            }
            if (moduleOn && applied != null && Time.time >= nextSweep)
            {
                nextSweep = Time.time + 1f;
                Sweep(shared);
            }
        }

        /// <summary>Puts the stone back as the game has it (an undo step too), and the stones he carries with it.</summary>
        internal static void PutBack()
        {
            if (applied == null)
            {
                return;
            }
            applied.m_useDurability = hadDurability;
            applied.m_canBeReparied = couldBeRepaired;
            ItemDrop.ItemData.SharedData shared = applied;
            applied = null;
            Sweep(shared);
        }

        /// <summary>The stone prefab's shared data, looked up again only when the game's item list is a new one.</summary>
        internal static ItemDrop.ItemData.SharedData Shared()
        {
            ObjectDB now = ObjectDB.instance;
            if (!ReferenceEquals(now, db))
            {
                db = now;
                GameObject stone = now != null ? now.GetItemPrefab(Gear.StonePrefab) : null;
                ItemDrop drop = stone != null ? stone.GetComponent<ItemDrop>() : null;
                prefab = drop?.m_itemData?.m_shared;
            }
            return prefab;
        }

        /// <summary>Points the stones in the local player's inventory at <paramref name="shared"/>.</summary>
        private static void Sweep(ItemDrop.ItemData.SharedData shared)
        {
            Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            if (inventory == null || shared == null)
            {
                return;
            }
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (Gear.IsStone(item) && !ReferenceEquals(item.m_shared, shared))
                {
                    item.m_shared = shared;
                }
            }
        }

        /// <summary>One stone's full durability.</summary>
        internal static float Max(ItemDrop.ItemData stone) => Mathf.Max(1f, stone.GetMaxDurability());

        /// <summary>His stone stacks, the worn ones first (the least left first), then <paramref name="first"/>, then the
        /// rest as they lie.</summary>
        internal static List<ItemDrop.ItemData> Stacks(Inventory inventory, ItemDrop.ItemData first)
        {
            var stacks = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (Gear.IsStone(item) && item.m_stack > 0)
                {
                    stacks.Add(item);
                }
            }
            // A stable sort: List.Sort isn't, so ties keep their place by index.
            var place = new Dictionary<ItemDrop.ItemData, int>();
            for (int i = 0; i < stacks.Count; i++)
            {
                place[stacks[i]] = i;
            }
            stacks.Sort((a, b) =>
            {
                bool wornA = a.m_durability < Max(a) - Rules.Tiny;
                bool wornB = b.m_durability < Max(b) - Rules.Tiny;
                if (wornA != wornB)
                {
                    return wornA ? -1 : 1;
                }
                if (wornA && a.m_durability != b.m_durability)
                {
                    return a.m_durability.CompareTo(b.m_durability);
                }
                bool firstA = ReferenceEquals(a, first);
                bool firstB = ReferenceEquals(b, first);
                if (firstA != firstB)
                {
                    return firstA ? -1 : 1;
                }
                return place[a].CompareTo(place[b]);
            });
            return stacks;
        }

        /// <summary>How many stones he carries, at any world level.</summary>
        internal static int Count(Inventory inventory)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (Gear.IsStone(item))
                {
                    count += Mathf.Max(0, item.m_stack);
                }
            }
            return count;
        }

        /// <summary>His stones as the wear rule reads them, in <see cref="Stacks"/>' order.</summary>
        internal static List<StoneStack> Read(List<ItemDrop.ItemData> stacks)
        {
            var read = new List<StoneStack>(stacks.Count);
            foreach (ItemDrop.ItemData stack in stacks)
            {
                read.Add(new StoneStack(stack.m_stack, Mathf.Clamp(stack.m_durability, 0f, Max(stack))));
            }
            return read;
        }

        /// <summary>
        /// Wears his stones by <paramref name="cost"/> durability (Rules.PlanWear), the worn one first; a stone used up is
        /// taken the careful way (TakeOne). Then a worn stone left in a stack of several gets a cell of its own in the
        /// game's own rows (the first <paramref name="gameRows"/>), below the hotbar first, when there's room. Returns
        /// false, having changed nothing, when his stones can't pay (planned first); a take that fails part way (another
        /// mod moving a stone meanwhile) leaves the stones taken so far taken, and the repair isn't made.
        /// </summary>
        internal static bool Wear(Inventory inventory, int gameRows, ItemDrop.ItemData first, float cost)
        {
            List<ItemDrop.ItemData> stacks = Stacks(inventory, first);
            if (stacks.Count == 0)
            {
                return false;
            }
            float max = Max(stacks[0]);
            if (!Rules.PlanWear(Read(stacks), max, cost, out int[] used, out float[] tops))
            {
                return false;
            }
            ItemDrop.ItemData.SharedData shared = Shared();
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemDrop.ItemData stack = stacks[i];
                for (int n = 0; n < used[i]; n++)
                {
                    if (!TakeOne(inventory, stack))
                    {
                        return false;
                    }
                }
                if (inventory.ContainsItem(stack) && stack.m_stack > 0)
                {
                    if (shared != null)
                    {
                        stack.m_shared = shared;
                    }
                    stack.m_durability = tops[i];
                }
            }
            foreach (ItemDrop.ItemData stack in Stacks(inventory, null))
            {
                if (stack.m_stack > 1 && stack.m_durability < Max(stack) - Rules.Tiny)
                {
                    SplitWorn(inventory, gameRows, stack);
                }
            }
            return true;
        }

        /// <summary>
        /// Takes one stone out of <paramref name="stack"/> (or, if it's gone, out of any of his stones) the careful way
        /// (research part C, section 5; LESSONS_LEARNED): counted before and after, and taken only if the game said so
        /// and the count fell by exactly one.
        /// </summary>
        internal static bool TakeOne(Inventory inventory, ItemDrop.ItemData stack)
        {
            int before = Count(inventory);
            if (before <= 0)
            {
                return false;
            }
            ItemDrop.ItemData from = stack != null && inventory.ContainsItem(stack) && stack.m_stack > 0 ? stack : null;
            if (from == null)
            {
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (Gear.IsStone(item) && item.m_stack > 0)
                    {
                        from = item;
                        break;
                    }
                }
            }
            if (from == null)
            {
                return false;
            }
            bool said = inventory.RemoveOneItem(from);
            return said && Count(inventory) == before - 1;
        }

        // A worn stone never stacks (decision 12, as 65-B's second review settled it): a stack's durability is one number,
        // and the game merges stacks by name, quality and world level alone, so any merge with a worn stone on either side
        // would lose or invent wear. A worn stone keeps a cell of its own, and nothing joins it. A stack of several is worn
        // only when there was no room to split its worn top stone off; its stones but the top are whole.

        /// <summary>Whether a stone (a stack's top stone) has lost any durability.</summary>
        internal static bool Worn(ItemDrop.ItemData stone) => stone.m_durability < Max(stone) - Rules.Tiny;

        /// <summary>What a stone being added carried in: the stone, if it is worn, how many stones it holds, and how many of
        /// them the game has placed so far (Inventory.AddItem places one at a time, asking FindFreeStackItem for each).</summary>
        internal struct Adding
        {
            internal ItemDrop.ItemData Stone;
            internal int Units;
            internal int Asked;
        }

        private static Adding adding;

        /// <summary>A stone being added to an inventory (a prefix on Inventory.AddItem): a worn one is noted, so its worn top
        /// stone, the last one placed, never joins a stack. Returns what was noted before, for <see cref="EndAdd"/>.</summary>
        internal static Adding BeginAdd(ItemDrop.ItemData item)
        {
            Adding before = adding;
            adding = Gear.IsStone(item) && Worn(item) ? new Adding { Stone = item, Units = item.m_stack } : default;
            return before;
        }

        internal static void EndAdd(Adding before) => adding = before;

        /// <summary>
        /// The stack an added stone goes onto (a postfix on Inventory.FindFreeStackItem, which only knows the name): never
        /// a worn stack, and a worn stone onto nothing. The game then puts what's left of the stone being added (its worn
        /// top stone with it) in a cell of its own, or, with no empty cell, leaves it where it came from.
        /// </summary>
        internal static ItemDrop.ItemData Chosen(Inventory inventory, string name, int quality, float worldLevel, ItemDrop.ItemData found)
        {
            if (found == null || name != Gear.StoneName)
            {
                return found;
            }
            if (adding.Stone != null)
            {
                bool wornOne = adding.Asked == adding.Units - 1;
                adding.Asked++;
                if (wornOne)
                {
                    return null;
                }
            }
            return Worn(found) ? WholeStackWithRoom(inventory, found, name, quality, worldLevel) : found;
        }

        /// <summary>Another whole stone stack with room, of the same name, quality and world level; null when there's none.</summary>
        private static ItemDrop.ItemData WholeStackWithRoom(Inventory inventory, ItemDrop.ItemData not, string name, int quality, float worldLevel)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item != not && item.m_shared.m_name == name && item.m_quality == quality &&
                    item.m_stack < item.m_shared.m_maxStackSize && (float)item.m_worldLevel == worldLevel && !Worn(item))
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Whether <paramref name="stack"/> stones of <paramref name="item"/> fit in <paramref name="inventory"/> by the rule
        /// that a worn stone never stacks, exactly as Inventory.AddItem will place them with <see cref="Chosen"/>: whole
        /// stones join whole stacks with room, and what's left (the worn top stone with it) takes one empty cell. A
        /// postfix on Inventory.CanAddItem, which counts a worn stack's room too: without it, auto-pickup (which asks only
        /// CanAddItem) would add part of a drop, fail, and leave the drop's saved record whole, so the stones would come
        /// back (65-B's last look, R3-1); and a craft would fall at his feet.
        /// </summary>
        internal static bool CanAdd(Inventory inventory, ItemDrop.ItemData item, int stack)
        {
            if (stack <= 0)
            {
                stack = item.m_stack;
            }
            int empty = inventory.GetWidth() * inventory.GetHeight() - inventory.GetAllItems().Count;
            if (Worn(item))
            {
                return empty >= 1;
            }
            int room = 0;
            foreach (ItemDrop.ItemData other in inventory.GetAllItems())
            {
                if (other.m_shared.m_name == item.m_shared.m_name && other.m_quality == item.m_quality &&
                    other.m_worldLevel == item.m_worldLevel && other.m_stack < other.m_shared.m_maxStackSize && !Worn(other))
                {
                    room += other.m_shared.m_maxStackSize - other.m_stack;
                }
            }
            return room + Mathf.Max(0, empty) * item.m_shared.m_maxStackSize >= stack;
        }

        /// <summary>A drag of <paramref name="moving"/> stones of <paramref name="item"/> onto the stone stack
        /// <paramref name="onto"/> (the game's private positional Inventory.AddItem): refused when the stack is worn, or when
        /// the drag would bring the worn top stone along (all of a worn stack). Part of a worn stack moves whole stones only:
        /// the stack it lands on keeps its durability, and the worn stone stays behind.</summary>
        internal static bool RefusesDrag(ItemDrop.ItemData onto, ItemDrop.ItemData item, int moving)
        {
            return Worn(onto) || (Worn(item) && moving >= item.m_stack);
        }

        /// <summary>Moves the worn top stone of a stack of several to the first empty cell of the game's own rows below the
        /// hotbar (the hotbar's last), as the game's own split does (Inventory.MoveItemToThis into an empty cell clones the
        /// stack with one in it, its durability too); the stack left behind is whole. Nothing when there's no room.</summary>
        private static void SplitWorn(Inventory inventory, int gameRows, ItemDrop.ItemData stack)
        {
            float worn = stack.m_durability;
            int rows = Mathf.Min(gameRows, inventory.GetHeight());
            int width = inventory.GetWidth();
            for (int pass = 0; pass < 2; pass++)
            {
                int top = pass == 0 ? 1 : 0;
                int bottom = pass == 0 ? rows : Mathf.Min(1, rows);
                for (int y = top; y < bottom; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (inventory.GetItemAt(x, y) != null)
                        {
                            continue;
                        }
                        int before = stack.m_stack;
                        inventory.MoveItemToThis(inventory, stack, 1, x, y);
                        ItemDrop.ItemData single = inventory.GetItemAt(x, y);
                        if (single != null && Gear.IsStone(single) && single.m_stack == 1 && stack.m_stack == before - 1)
                        {
                            single.m_durability = worn;
                            stack.m_durability = Max(stack);
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>The rows of his inventory that are the game's own (the character's row count, 4 or more once bought:
        /// Player.SetInventorySize), as the chest service reads them (StackItems.GameRows).</summary>
        internal static int GameRows(Player player)
        {
            int rows = 4;
            if (player.TryGetUniqueKeyValue(RowsKey, out string value) && int.TryParse(value, out int kept))
            {
                rows = Mathf.Clamp(kept, 1, 9);
            }
            return rows;
        }
    }
}
