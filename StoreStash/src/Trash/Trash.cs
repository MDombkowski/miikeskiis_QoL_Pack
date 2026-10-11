using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using UnityEngine;

namespace StoreStash
{
    /// <summary>What the trash can would delete from his inventory, and the marked stacks it keeps.</summary>
    internal sealed class TrashPlan
    {
        /// <summary>The stacks that go, whole as they are when it's made (Add), no more than that later.</summary>
        internal readonly List<ItemDrop.ItemData> Stacks = new List<ItemDrop.ItemData>();

        // Each stack's amount when the plan was made: what the question names, and the most its yes deletes.
        private readonly Dictionary<ItemDrop.ItemData, int> amounts = new Dictionary<ItemDrop.ItemData, int>();

        /// <summary>Marked stacks it keeps: on the hotbar, worn or held, favourites, quest items.</summary>
        internal int KeptHotbar;

        internal int KeptWorn;

        internal int KeptFavorites;

        internal int KeptQuest;

        internal void Add(ItemDrop.ItemData stack)
        {
            Stacks.Add(stack);
            amounts[stack] = stack.m_stack;
        }

        /// <summary>A stack's amount when the plan was made (0 for one it doesn't name).</summary>
        internal int AmountOf(ItemDrop.ItemData stack) => amounts.TryGetValue(stack, out int amount) ? amount : 0;

        internal int Items => Stacks.Sum(AmountOf);

        internal int Kinds => Stacks.Select(stack => stack.m_shared.m_name).Distinct().Count();

        internal bool AnyKept => KeptHotbar + KeptWorn + KeptFavorites + KeptQuest > 0;
    }

    /// <summary>Why the can will or won't delete what he's carrying.</summary>
    internal enum CarriedVerdict
    {
        Deletable,
        NotCarrying,

        /// <summary>It's no longer where it was picked up from (another mod moved it).</summary>
        Gone,

        Worn,
        Quest,
        Favorite,

        /// <summary>From his hotbar, or from a row below the game's own (EquipmentAndQuickSlots' quick slots).</summary>
        KeptRow,

        /// <summary>From a chest this computer doesn't own this moment, or from a window that isn't his inventory or the
        /// open chest.</summary>
        NotHis,
    }

    /// <summary>
    /// The trash (card A53). The can deletes every stack in his inventory whose kind he marked as trash, whole, but only in
    /// the game's own rows and never on the hotbar, never what he wears or holds (a weapon the game has put away for a
    /// moment too), never a favourite, never a quest item: those are kept, whatever is marked, and the can says so.
    /// Carrying an item (picked up with a click, or split off) and clicking the can deletes just what he carries, marked
    /// or not, from his inventory or the chest open on his screen, unless it's worn, a quest item, or (unless a setting
    /// says otherwise) a favourite or from the hotbar. It asks first, as the settings say; while either marks file can't
    /// be read it deletes nothing (an unread favourite would count as trash). Every deletion is re-checked in the frame it
    /// happens.
    /// </summary>
    internal static class Trash
    {
        /// <summary>Asks before deleting (TrashDialog.Open: its heading, its text, what a Delete does). Only the scratch test
        /// harness sets another.</summary>
        internal static Action<string, string, Action> AskFirst = TrashDialog.Open;

        /// <summary>Refreshes the crafting list after a deletion. Only the scratch test harness sets another.</summary>
        internal static Action<InventoryGui> Refresh = SortHooks.RefreshCrafting;

        /// <summary>Lets go of what he's carrying. Only the scratch test harness sets another.</summary>
        internal static Action<InventoryGui> LetGo = GameHooks.LetGoOfDrag;

        /// <summary>The stacks the can would delete from his inventory now, and the marked ones it keeps.</summary>
        internal static TrashPlan Marked(Player player) =>
            Marked(player.GetInventory(), StackItems.GameRows(player), item => ChestHooks.Worn(player, item), Favorites.IsFavorite,
                   TrashMarks.List.Has, ChestHooks.InHisHand);

        /// <summary>The same, from plain tests, for the scratch harness: stacks in rows 1 to <paramref name="gameRows"/>
        /// minus one, whose kind is marked, that aren't worn, a favourite, a quest item or in his hand.</summary>
        internal static TrashPlan Marked(Inventory inventory, int gameRows, Func<ItemDrop.ItemData, bool> worn, Func<ItemDrop.ItemData, bool> favorite,
                                         Func<ItemDrop.ItemData, bool> marked, Func<ItemDrop.ItemData, bool> inHand)
        {
            var plan = new TrashPlan();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                switch (Classify(item, gameRows, worn, favorite, marked, inHand))
                {
                    case Fate.Goes:
                        plan.Add(item);
                        break;
                    case Fate.KeptHotbar:
                        plan.KeptHotbar++;
                        break;
                    case Fate.KeptWorn:
                        plan.KeptWorn++;
                        break;
                    case Fate.KeptFavorite:
                        plan.KeptFavorites++;
                        break;
                    case Fate.KeptQuest:
                        plan.KeptQuest++;
                        break;
                }
            }
            plan.Stacks.Sort((a, b) => a.m_gridPos.y != b.m_gridPos.y ? a.m_gridPos.y.CompareTo(b.m_gridPos.y) : a.m_gridPos.x.CompareTo(b.m_gridPos.x));
            return plan;
        }

        /// <summary>How many stacks the can would delete now, by the same rules, without making a list: every frame, for its
        /// picture and tooltip.</summary>
        internal static int CountMarked(Player player)
        {
            int rows = StackItems.GameRows(player);
            int count = 0;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (Classify(item, rows, each => ChestHooks.Worn(player, each), Favorites.IsFavorite, TrashMarks.List.Has, ChestHooks.InHisHand) == Fate.Goes)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>What becomes of one stack when the can is clicked.</summary>
        internal enum Fate
        {
            /// <summary>Not marked, or not in the game's own rows: not the can's business.</summary>
            Untouched,
            Goes,
            KeptHotbar,
            KeptWorn,
            KeptFavorite,
            KeptQuest,

            /// <summary>In his hand (carried, or in the split window): left for now.</summary>
            InHand,
        }

        /// <summary>The one place the can's rules are written: a marked stack in the game's own rows goes unless it's on
        /// the hotbar, worn or held, a favourite, a quest item, or in his hand, in that order.</summary>
        internal static Fate Classify(ItemDrop.ItemData item, int gameRows, Func<ItemDrop.ItemData, bool> worn, Func<ItemDrop.ItemData, bool> favorite,
                                      Func<ItemDrop.ItemData, bool> marked, Func<ItemDrop.ItemData, bool> inHand)
        {
            if (item?.m_shared == null || item.m_stack <= 0 || item.m_gridPos.y < 0 || item.m_gridPos.y >= gameRows || !marked(item))
            {
                return Fate.Untouched;
            }
            if (item.m_gridPos.y == 0)
            {
                return Fate.KeptHotbar;
            }
            if (item.m_equipped || worn(item))
            {
                return Fate.KeptWorn;
            }
            if (favorite(item))
            {
                return Fate.KeptFavorite;
            }
            if (item.m_shared.m_questItem)
            {
                return Fate.KeptQuest;
            }
            return inHand(item) ? Fate.InHand : Fate.Goes;
        }

        /// <summary>
        /// Whether the can may delete what he carries: from his inventory, anything in the game's own rows below the hotbar
        /// that isn't worn, a quest item or a favourite (the hotbar, other mods' rows and favourites too if
        /// <paramref name="keptToo"/>); from the open chest, only while this computer owns it, and never a favourite
        /// unless <paramref name="keptToo"/>.
        /// </summary>
        internal static CarriedVerdict Judge(ItemDrop.ItemData item, Inventory from, Inventory mine, Inventory chest, bool chestHis, int gameRows,
                                             bool worn, bool favorite, bool keptToo)
        {
            if (item?.m_shared == null || from == null)
            {
                return CarriedVerdict.NotCarrying;
            }
            if (!from.ContainsItem(item) || item.m_stack <= 0)
            {
                return CarriedVerdict.Gone;
            }
            if (worn || item.m_equipped)
            {
                return CarriedVerdict.Worn;
            }
            if (item.m_shared.m_questItem)
            {
                return CarriedVerdict.Quest;
            }
            if (from == mine)
            {
                if (!keptToo && (item.m_gridPos.y <= 0 || item.m_gridPos.y >= gameRows))
                {
                    return CarriedVerdict.KeptRow;
                }
            }
            else if (from != chest || !chestHis)
            {
                return CarriedVerdict.NotHis;
            }
            return favorite && !keptToo ? CarriedVerdict.Favorite : CarriedVerdict.Deletable;
        }

        /// <summary>The verdict on what he's carrying right now.</summary>
        internal static CarriedVerdict JudgeCarried(Player player, InventoryGui gui, out ItemDrop.ItemData item, out Inventory from, out int amount)
        {
            if (!SortHooks.Carried(gui, out item, out from, out amount))
            {
                return CarriedVerdict.NotCarrying;
            }
            Container open = GameHooks.OpenChest(gui);
            Inventory chest = open != null ? open.GetInventory() : null;
            // Not his to delete from: another player's chest he may only look at (card A80).
            bool his = open != null && open.IsOwner() && open.IsInUse() && ChestAccess.MayTouch(open);
            return Judge(item, from, player.GetInventory(), chest, his, StackItems.GameRows(player), ChestHooks.Worn(player, item),
                         Favorites.IsFavorite(item), TrashSettings.CarriedKept.Value);
        }

        /// <summary>The can's click.</summary>
        internal static void Clicked()
        {
            Player player = Player.m_localPlayer;
            InventoryGui gui = InventoryGui.instance;
            if (!Module.IsOn || !TrashSettings.On.Value || player == null || gui == null || player.IsDead() || player.IsTeleporting() || TrashDialog.IsOpen)
            {
                return;
            }
            if (SortHooks.Carried(gui, out _, out _, out _))
            {
                ClickedCarrying(player, gui);
                return;
            }
            if (!Favorites.List.Readable || !TrashMarks.List.Readable)
            {
                Messages.TopLeft("Trash: your favorites or trash marks file can't be read, so nothing was deleted (see the log)");
                return;
            }
            TrashPlan plan = Marked(player);
            if (plan.Stacks.Count == 0)
            {
                Messages.TopLeft(NothingText(plan));
                return;
            }
            if (Asks(plan.Stacks.Count))
            {
                AskFirst("Delete marked items?", PlanText(plan), () => DeleteMarked(plan));
                return;
            }
            DeleteMarked(plan);
        }

        private static void ClickedCarrying(Player player, InventoryGui gui)
        {
            if (!TrashSettings.Carried.Value)
            {
                Messages.TopLeft("Trash: put down what you're carrying first (StoreStash: the can doesn't take carried items)");
                return;
            }
            if (!TrashSettings.CarriedKept.Value && !Favorites.List.Readable)
            {
                Messages.TopLeft("Trash: your favorites file can't be read, so nothing was deleted (see the log)");
                return;
            }
            CarriedVerdict verdict = JudgeCarried(player, gui, out ItemDrop.ItemData item, out Inventory from, out int amount);
            if (verdict != CarriedVerdict.Deletable)
            {
                Messages.TopLeft(RefusedText(verdict, item));
                return;
            }
            amount = Mathf.Clamp(amount, 1, item.m_stack);
            string what = $"{Name(item)} ×{amount}";
            if (Asks(1))
            {
                AskFirst("Delete this?", what, () => DeleteCarried(item, from, amount));
                return;
            }
            DeleteCarried(item, from, amount);
        }

        /// <summary>Whether to ask first, by the setting, for this many stacks.</summary>
        internal static bool Asks(int stacks)
        {
            switch (TrashSettings.Ask.Value)
            {
                case TrashAsk.Never:
                    return false;
                case TrashAsk.MoreThanOneStack:
                    return stacks > 1;
                default:
                    return true;
            }
        }

        // The marked stacks, after his yes: each only if it's still in his inventory and the can would still delete it now
        // (another mod, a pickup or a mark may have changed things while he read the question), and no more of it than the
        // question named (a pickup can top a stack up meanwhile); what left is measured, not assumed.
        private static void DeleteMarked(TrashPlan asked)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !Module.IsOn || !TrashSettings.On.Value)
            {
                return;
            }
            if (!Favorites.List.Readable || !TrashMarks.List.Readable)
            {
                Messages.TopLeft("Trash: your favorites or trash marks file can't be read, so nothing was deleted (see the log)");
                return;
            }
            var now = new HashSet<ItemDrop.ItemData>(Marked(player).Stacks);
            Inventory inventory = player.GetInventory();
            int items = 0;
            var kinds = new HashSet<string>();
            foreach (ItemDrop.ItemData stack in asked.Stacks)
            {
                if (!now.Contains(stack))
                {
                    continue;
                }
                int before = stack.m_stack;
                int amount = Math.Min(asked.AmountOf(stack), before);
                if (amount <= 0 || !inventory.RemoveItem(stack, amount))
                {
                    continue;
                }
                int gone = before - (inventory.ContainsItem(stack) ? stack.m_stack : 0);
                if (gone > 0)
                {
                    items += gone;
                    kinds.Add(stack.m_shared.m_name);
                }
            }
            After(items > 0);
            if (TrashSettings.Message.Value)
            {
                Messages.TopLeft(items > 0 ? DeletedText(kinds.Count, items, asked) : "Trash: nothing deleted, the marked items had moved");
            }
        }

        // What he carries, after his yes (or at once): only if he's still carrying that same stack, the same amount, and
        // the can would still take it; then that much leaves where it was picked up from, and he lets go.
        private static void DeleteCarried(ItemDrop.ItemData item, Inventory from, int amount)
        {
            Player player = Player.m_localPlayer;
            InventoryGui gui = InventoryGui.instance;
            if (player == null || gui == null || !Module.IsOn || !TrashSettings.On.Value)
            {
                return;
            }
            CarriedVerdict verdict = JudgeCarried(player, gui, out ItemDrop.ItemData carried, out Inventory carriedFrom, out int carriedAmount);
            if (verdict != CarriedVerdict.Deletable || !ReferenceEquals(carried, item) || carriedFrom != from || Mathf.Clamp(carriedAmount, 1, item.m_stack) != amount)
            {
                Messages.TopLeft("Trash: nothing deleted, what you carried had changed");
                return;
            }
            string what = $"{Name(item)} ×{amount}";
            bool removed = from.RemoveItem(item, amount);
            LetGo(gui);
            After(removed);
            if (TrashSettings.Message.Value)
            {
                Messages.TopLeft(removed ? $"Deleted {what}" : "Trash: nothing deleted, it had moved");
            }
        }

        private static void After(bool deleted)
        {
            if (deleted)
            {
                Refresh(InventoryGui.instance);
            }
        }

        private static string Name(ItemDrop.ItemData item) => Sorter.Localize(item.m_shared.m_name) ?? item.m_shared.m_name;

        /// <summary>How many kinds the question names before "and N more kinds": with the count line, what's kept (up to
        /// two lines) and that line, ten lines, what the question has room for (TrashDialog).</summary>
        internal const int KindsNamed = 6;

        /// <summary>The question's text: how much, then each kind with its amount (the first six), then what's kept.</summary>
        internal static string PlanText(TrashPlan plan)
        {
            var lines = new List<string> { $"{Count(plan.Kinds, "kind", "kinds")}, {Count(plan.Items, "item", "items")}:" };
            List<(string Name, int Amount)> kinds = plan.Stacks.GroupBy(stack => stack.m_shared.m_name)
                .Select(group => (Name(group.First()), group.Sum(plan.AmountOf))).ToList();
            foreach ((string name, int amount) in kinds.Take(KindsNamed))
            {
                lines.Add($"{name} ×{amount}");
            }
            if (kinds.Count > KindsNamed)
            {
                lines.Add($"and {Count(kinds.Count - KindsNamed, "more kind", "more kinds")}");
            }
            string kept = KeptText(plan);
            if (kept != null)
            {
                lines.Add($"Kept: {kept}.");
            }
            return string.Join("\n", lines);
        }

        /// <summary>"Deleted 3 kinds, 57 items", and what was kept.</summary>
        internal static string DeletedText(int kinds, int items, TrashPlan plan)
        {
            string kept = KeptText(plan);
            string text = $"Deleted {Count(kinds, "kind", "kinds")}, {Count(items, "item", "items")}";
            return kept != null ? $"{text} (kept: {kept})" : text;
        }

        /// <summary>Why the can deleted nothing, when nothing marked may go.</summary>
        internal static string NothingText(TrashPlan plan)
        {
            string kept = KeptText(plan);
            if (kept != null)
            {
                return $"Trash: nothing deleted, marked items there are kept ({kept})";
            }
            KeyCode modifier = TrashSettings.Modifier.Value;
            return modifier == KeyCode.None
                ? "Trash: nothing marked as trash in your inventory"
                : $"Trash: nothing marked as trash in your inventory ({Settings.Spaced(modifier)} + right-click an item to mark its kind)";
        }

        /// <summary>Why the can won't take what he carries.</summary>
        internal static string RefusedText(CarriedVerdict verdict, ItemDrop.ItemData item)
        {
            string name = item?.m_shared != null ? Name(item) : "It";
            switch (verdict)
            {
                case CarriedVerdict.Worn:
                    return $"Trash: {name} is something you wear or hold, so it's never deleted";
                case CarriedVerdict.Quest:
                    return $"Trash: {name} is a quest item, so it's never deleted";
                case CarriedVerdict.Favorite:
                    return $"Trash: {name} is a favorite, so it's kept (StoreStash's settings can allow it)";
                case CarriedVerdict.KeptRow:
                    return $"Trash: {name} is from your hotbar or quick slots, so it's kept (StoreStash's settings can allow it)";
                case CarriedVerdict.NotHis:
                    return "Trash: only items from your inventory or the chest you have open can be deleted";
                default:
                    return "Trash: nothing deleted, what you carried had moved";
            }
        }

        // "2 on the hotbar, 1 favorite", or null when nothing marked was kept.
        private static string KeptText(TrashPlan plan)
        {
            var parts = new List<string>();
            if (plan.KeptHotbar > 0)
            {
                parts.Add($"{plan.KeptHotbar} on the hotbar");
            }
            if (plan.KeptWorn > 0)
            {
                parts.Add($"{plan.KeptWorn} worn");
            }
            if (plan.KeptFavorites > 0)
            {
                parts.Add(Count(plan.KeptFavorites, "favorite", "favorites"));
            }
            if (plan.KeptQuest > 0)
            {
                parts.Add(Count(plan.KeptQuest, "quest item", "quest items"));
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static string Count(int number, string one, string many) => $"{number} {(number == 1 ? one : many)}";
    }
}
