using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// A measured put from an item lying on the ground into a chest, the mirror of the chest service's ChestPut.Put (a
    /// candidate for the chest service: the research's 2.5, B). Only when this game owns both: the item (a station's
    /// product, or a drop this game made) and the chest (ChestClaims.MayChange); never an ownership request, never a claim;
    /// never into the Obliterator or a chest kept for a craft whose bar is running (ChestUse).
    /// The item's data is read again from its record, a copy of up to the asked amount goes into the chest, what arrived
    /// is measured, and exactly that leaves the ground: the item's stack lowered, or the item removed
    /// (ZNetScene.Destroy) when all of it went. What arrived but the ground no longer holds (another mod's handler of the
    /// chest's change took the item meanwhile) comes back out of the chest, the copy first. Every step that changes the
    /// world is inside the try whose finally settles it, so another mod's error mid-put never copies or loses an item.
    /// The caller holds and sends every chest it put into (Stow does), in a finally of its own.
    /// </summary>
    internal static class GroundPut
    {
        /// <summary>
        /// Puts up to <paramref name="amount"/> of the item lying on the ground into the chest. With
        /// <paramref name="topUpOnly"/>, only into the chest's part-stacks of the same item, never an empty cell. Nothing
        /// goes into a reserved chest (Reserve Stock's mark) unless <paramref name="useReserved"/>. Returns how many moved.
        /// </summary>
        internal static int Put(Container chest, ItemDrop drop, int amount, bool topUpOnly, bool useReserved)
        {
            if (amount <= 0 || chest == null || drop == null ||
                ChestUse.WhyNot(ChestClaims.MayChange(chest), Obliterator.Is(chest), ChestClaims.IsKeptForCraft(chest),
                                ChestReserve.IsReserved(chest), useReserved) != null)
            {
                return 0;   // never the Obliterator (M2) or a chest kept for a craft (m4); ChestUse
            }
            ZNetView view = drop.GetComponent<ZNetView>();
            if (!Ours(drop, view))
            {
                return 0;   // another game's: never asked for
            }
            drop.Load();   // its newest data, from its record
            ItemDrop.ItemData item = drop.m_itemData;
            if (item?.m_shared == null || item.m_stack <= 0 || item.m_dropPrefab == null)
            {
                return 0;
            }
            Inventory into = chest.GetInventory();
            int part = PutBook.Part(amount, item.m_stack, ChestPut.Room(into, item, topUpOnly));
            if (part <= 0)
            {
                return 0;
            }
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = part;
            copy.m_equipped = false;
            int before = ChestStock.CountSame(into, item);
            int moved = 0;
            try
            {
                into.AddItem(copy);
            }
            finally
            {
                // Also when another mod's handler of the chest's change throws inside AddItem: what arrived leaves the ground
                // (or comes back out of the chest) before the error goes on and switches the module off.
                int arrived = Mathf.Clamp(ChestStock.CountSame(into, copy) - before, 0, part);
                if (arrived > 0)
                {
                    bool ours = Ours(drop, view);
                    PutBook.Settled settled = PutBook.Settle(arrived, ours ? drop.m_itemData.m_stack : 0, ours);
                    try
                    {
                        if (settled.Remove)
                        {
                            ZNetScene.instance.Destroy(drop.gameObject);
                        }
                        else if (settled.Leaves > 0)
                        {
                            drop.SetStack(settled.Left);
                        }
                    }
                    finally
                    {
                        if (settled.TakeBack > 0)
                        {
                            ChestStock.TakeBack(into, copy, settled.TakeBack);
                        }
                    }
                    moved = settled.Leaves;
                }
            }
            return moved;
        }

        /// <summary>Still lying there, its record valid and this game's.</summary>
        internal static bool Ours(ItemDrop drop, ZNetView view) =>
            drop != null && view != null && view.IsValid() && view.IsOwner() && drop.m_itemData != null;
    }
}
