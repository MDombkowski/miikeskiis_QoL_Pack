// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// Putting into a chest, the mirror of a take (ChestStock.Take), in the frame a chest is this computer's (card A52): a
    /// copy of the stack goes into the chest first, what arrived is measured, and exactly that leaves the stack it came
    /// from; whatever can't leave it (another mod moved the stack meanwhile) goes back out of the chest. So a full chest, or
    /// another mod's handler of a change, can't lose or copy anything. The caller claims the chests first (ChestClaims), and
    /// sends and holds every chest it changed afterwards, in a finally (ChestStacker, ChestMove).
    /// </summary>
    internal static class ChestPut
    {
        /// <summary>
        /// Puts up to <paramref name="amount"/> of <paramref name="stack"/> (an item in <paramref name="from"/>, his
        /// inventory) into <paramref name="chest"/>. With <paramref name="topUpOnly"/>, only into the chest's part-stacks of
        /// the same item (name, quality and world level: what the game stacks together), never into an empty cell. Returns
        /// how many moved. Nothing moves into a chest this computer doesn't own, into a reserved one (Reserve Stock's mark)
        /// unless <paramref name="useReserved"/>, from an equipped item, or from the stack in his hand.
        /// </summary>
        internal static int Put(Container chest, Inventory from, ItemDrop.ItemData stack, int amount, bool topUpOnly, bool useReserved)
        {
            if (!Mine(chest) || (!useReserved && ChestReserve.IsReserved(chest)) || !Movable(from, stack))
            {
                return 0;
            }
            return Transfer(chest.GetInventory(), from, stack, amount, topUpOnly);
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> of <paramref name="stack"/> (an item in <paramref name="source"/>) into
        /// <paramref name="target"/>, both chests this computer owns, the same way: into the target first, measured, then out
        /// of the source. Any world level (the caller picks the stacks). Neither chest may be reserved, unless
        /// <paramref name="useReserved"/>. Returns how many moved.
        /// </summary>
        internal static int Move(Container source, Container target, ItemDrop.ItemData stack, int amount, bool topUpOnly, bool useReserved)
        {
            if (source == null || target == null || source == target || !Mine(source) || !Mine(target))
            {
                return 0;
            }
            if (!useReserved && (ChestReserve.IsReserved(source) || ChestReserve.IsReserved(target)))
            {
                return 0;
            }
            Inventory from = source.GetInventory();
            if (!Movable(from, stack))
            {
                return 0;
            }
            return Transfer(target.GetInventory(), from, stack, amount, topUpOnly);
        }

        /// <summary>How much of <paramref name="like"/> (its name, quality and world level) <paramref name="into"/> can take:
        /// the room left in its part-stacks of that item, and with <paramref name="topUpOnly"/> off, a full stack in each empty
        /// cell too.</summary>
        internal static int Room(Inventory into, ItemDrop.ItemData like, bool topUpOnly)
        {
            int room = FreeStackSpace(into, like);
            if (!topUpOnly)
            {
                room += into.GetEmptySlots() * Mathf.Max(1, like.m_shared.m_maxStackSize);
            }
            return room;
        }

        /// <summary>The room left in <paramref name="into"/>'s part-stacks of the item the game would stack with
        /// <paramref name="like"/> (Inventory.FindFreeStackItem: the same name, quality and world level). None for an item
        /// that doesn't stack.</summary>
        internal static int FreeStackSpace(Inventory into, ItemDrop.ItemData like)
        {
            if (like.m_shared.m_maxStackSize <= 1)
            {
                return 0;
            }
            int room = 0;
            foreach (ItemDrop.ItemData item in into.GetAllItems())
            {
                if (item.m_shared.m_name == like.m_shared.m_name && item.m_quality == like.m_quality && item.m_worldLevel == like.m_worldLevel &&
                    item.m_stack < item.m_shared.m_maxStackSize)
                {
                    room += item.m_shared.m_maxStackSize - item.m_stack;
                }
            }
            return room;
        }

        // Adds a copy of up to that much of the stack to "into", measures what arrived, and takes exactly that out of the
        // stack in "from". The game's AddItem grows the part-stacks of the same item first, then puts the rest into one
        // empty cell as the copy itself. No more is offered than fits (Room): with topUpOnly, or with no empty cell, only
        // the part-stacks' room, so no cell is needed; a chest with no room isn't touched at all (the game's AddItem would
        // warn of an occupied slot and save the chest unchanged). If the stack lost some of it meanwhile (another mod's
        // handler of the change moved it), the rest goes back out of "into", the copy first, by reference.
        private static int Transfer(Inventory into, Inventory from, ItemDrop.ItemData stack, int amount, bool topUpOnly)
        {
            int part = Mathf.Min(amount, stack.m_stack);
            part = Mathf.Min(part, Room(into, stack, topUpOnly));
            if (part <= 0)
            {
                return 0;
            }
            ItemDrop.ItemData copy = stack.Clone();
            copy.m_stack = part;
            copy.m_equipped = false;
            int before = ChestStock.CountSame(into, stack);
            int added = 0;
            try
            {
                into.AddItem(copy);
            }
            finally
            {
                // Also when another mod's handler of the change throws inside AddItem: what arrived leaves his stack
                // before the error goes on (and switches the module off).
                added = Mathf.Clamp(ChestStock.CountSame(into, stack) - before, 0, part);
                if (added > 0)
                {
                    int leaves = from.ContainsItem(stack) ? Mathf.Min(stack.m_stack, added) : 0;
                    if (leaves > 0 && !from.RemoveItem(stack, leaves))
                    {
                        leaves = 0;
                    }
                    if (leaves < added)
                    {
                        ChestStock.TakeBack(into, copy, added - leaves);
                        added = leaves;
                    }
                }
            }
            return added;
        }

        // A chest this computer owns now: only the owner's change to a chest is saved (Container.OnContainerChanged).
        private static bool Mine(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() && view.IsOwner();
        }

        // A stack that may leave where it lies: still there, not worn or held (ChestHooks.Worn: a hand item the game has put
        // away counts), and not in his hand (dragged or being split).
        private static bool Movable(Inventory from, ItemDrop.ItemData stack)
        {
            if (from == null || stack == null || stack.m_stack <= 0 || !from.ContainsItem(stack) || stack.m_equipped || ChestHooks.InHisHand(stack))
            {
                return false;
            }
            Player player = Player.m_localPlayer;
            return player == null || from != player.GetInventory() || !ChestHooks.Worn(player, stack);
        }
    }
}
