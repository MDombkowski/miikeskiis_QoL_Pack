using System.Collections.Generic;
using Outfitter.Equip;

namespace Outfitter.Stands
{
    /// <summary>An item that came off a stand into his inventory: where it was, the item as read, how many, and how much of
    /// each of his stacks it made (the item itself, or the stacks of its kind it merged into).</summary>
    internal sealed class Moved
    {
        internal int Place;
        internal ItemDrop.ItemData Item;
        internal int Amount;
        internal readonly Dictionary<ItemDrop.ItemData, int> Grew = new Dictionary<ItemDrop.ItemData, int>();

        /// <summary>How much of it is in his inventory now, by what grew (each stack's growth since the add, never more
        /// than it grew then, never below 0).</summary>
        internal int Arrived;

        // His stacks of its kind before the add, for measuring.
        internal Dictionary<ItemDrop.ItemData, int> Before;
    }

    /// <summary>
    /// The stands' moves, each inside one frame on a stand this computer owns (the plan's "The moves"), so nothing else of
    /// the game runs between their steps. Into his inventory, added first and measured (gear by the very item; a stackable
    /// by how much each of his stacks of its kind grew), and only what arrived leaves the stand; onto the stand, written
    /// first, and only once the very item has left him does it stay there (taken off first, any queued equip of it
    /// cleared). What doesn't arrive or leave is put back as it was. **Also when another mod's code throws in the middle**
    /// (a handler of his inventory's change, a patch on taking an item off): every move settles in a finally, by measuring
    /// what really happened, before the error goes on, so an item is never on the stand and with him at once (review round
    /// 1's M3; the chest service's ChestStock.Take does the same). Pure apart from the game's plain item and inventory
    /// classes, so the scratch harness reaches it.
    /// </summary>
    internal static class StandMoves
    {
        /// <summary>
        /// Everything on the stand into his inventory, all or nothing: null when anything wouldn't arrive (then nothing has
        /// changed; <paramref name="needed"/> says how many items the stand holds that this game can read), else what came,
        /// its places emptied and shown so. Items this game can't read (a removed mod's) stay on the stand
        /// (<paramref name="unreadable"/>).
        /// </summary>
        internal static List<Moved> TakeAll(IStand stand, Inventory inventory, out int needed, out int unreadable) =>
            TakeAll(stand, inventory, null, out needed, out unreadable);

        /// <summary>The same for only these places (<paramref name="only"/>; null: every place): a swap takes only the
        /// other loadout's pieces off a stand (job 30, Session 2).</summary>
        internal static List<Moved> TakeAll(IStand stand, Inventory inventory, ICollection<int> only, out int needed, out int unreadable)
        {
            needed = 0;
            unreadable = 0;
            var items = new List<KeyValuePair<int, ItemDrop.ItemData>>();
            for (int place = 0; place < stand.Places; place++)
            {
                if (!stand.Has(place) || (only != null && !only.Contains(place)))
                {
                    continue;
                }
                ItemDrop.ItemData item = stand.Read(place);
                if (!OneOf(item))
                {
                    unreadable++;
                    continue;
                }
                items.Add(new KeyValuePair<int, ItemDrop.ItemData>(place, item));
            }
            needed = items.Count;
            var moved = new List<Moved>();
            bool settled = false;
            try
            {
                foreach (KeyValuePair<int, ItemDrop.ItemData> each in items)
                {
                    var one = new Moved { Place = each.Key, Item = each.Value, Amount = each.Value.m_stack };
                    moved.Add(one);
                    if (!Add(inventory, one))
                    {
                        // Not all of it would arrive (no room): everything taken back, nothing changed.
                        for (int i = moved.Count - 1; i >= 0; i--)
                        {
                            TakeBack(inventory, moved[i]);
                        }
                        settled = true;
                        return null;
                    }
                }
                foreach (Moved one in moved)
                {
                    stand.Clear(one.Place);
                    stand.Show(one.Place);
                }
                settled = true;
                return moved;
            }
            finally
            {
                if (!settled)
                {
                    // Another mod's code threw in the middle: what is with him now leaves the stand, before the error goes on.
                    foreach (Moved one in moved)
                    {
                        Settle(stand, inventory, one);
                    }
                }
            }
        }

        /// <summary>A place's item the stands move: one this game can read, and one of it, as the game's attach writes a
        /// place (anything else, another mod's doing, stays on the stand as it is).</summary>
        internal static bool OneOf(ItemDrop.ItemData item) => item != null && item.m_stack == 1;

        /// <summary>One item into his inventory, measured (in a finally, so also when the add throws): true when all of it
        /// arrived; false, with what did arrive taken back out, when not.</summary>
        internal static bool Add(Inventory inventory, Moved moved)
        {
            ItemDrop.ItemData item = moved.Item;
            moved.Before = Kind(inventory, item);
            try
            {
                inventory.AddItem(item);
            }
            finally
            {
                Measure(inventory, moved);
            }
            if (moved.Arrived >= moved.Amount)
            {
                return true;
            }
            TakeBack(inventory, moved);   // part of it, or none: none, as it was
            item.m_stack = moved.Amount;  // the game's AddItem keeps only the rest in the item it couldn't place
            return false;
        }

        /// <summary>One item into his inventory, measured; null, with nothing changed, when not all of it arrives.</summary>
        internal static Moved Add(Inventory inventory, ItemDrop.ItemData item, int place)
        {
            var moved = new Moved { Place = place, Item = item, Amount = item.m_stack };
            return Add(inventory, moved) ? moved : null;
        }

        // What each of his stacks of the item's kind grew by since the add (gear: the very item there or not).
        private static void Measure(Inventory inventory, Moved moved)
        {
            moved.Grew.Clear();
            moved.Arrived = 0;
            ItemDrop.ItemData item = moved.Item;
            if (Matching.Single(item))
            {
                if (inventory.ContainsItem(item))
                {
                    moved.Grew[item] = 1;
                    moved.Arrived = 1;
                }
                return;
            }
            foreach (KeyValuePair<ItemDrop.ItemData, int> stack in Kind(inventory, item))
            {
                moved.Before.TryGetValue(stack.Key, out int was);
                if (stack.Value > was)
                {
                    int grew = System.Math.Min(stack.Value - was, moved.Amount - moved.Arrived);
                    if (grew > 0)
                    {
                        moved.Grew[stack.Key] = grew;
                        moved.Arrived += grew;
                    }
                }
            }
        }

        /// <summary>Takes back out of his inventory exactly what an add put in (what is still there of it).</summary>
        internal static void TakeBack(Inventory inventory, Moved moved)
        {
            foreach (KeyValuePair<ItemDrop.ItemData, int> stack in new List<KeyValuePair<ItemDrop.ItemData, int>>(moved.Grew))
            {
                int amount = System.Math.Min(stack.Value, stack.Key.m_stack);
                if (inventory.ContainsItem(stack.Key) && amount > 0)
                {
                    inventory.RemoveItem(stack.Key, amount);
                }
                moved.Grew.Remove(stack.Key);
                moved.Arrived -= stack.Value;
            }
            moved.Arrived = 0;
        }

        // After an error: an item that arrived leaves the stand (one of it: OneOf); none, the stand keeps it. Measured again
        // now, since a take-back may have run part way.
        private static void Settle(IStand stand, Inventory inventory, Moved moved)
        {
            try
            {
                if (moved.Before != null)
                {
                    Measure(inventory, moved);
                }
                if (moved.Arrived <= 0)
                {
                    return;
                }
                stand.Clear(moved.Place);   // one of it (OneOf): it's with him now
                stand.Show(moved.Place);
            }
            catch (System.Exception)
            {
                // Settling runs while another error is on its way; it must never hide that error.
            }
        }

        /// <summary>His stacks of an item's kind (as the game merges: the same name, quality and world level), and the item
        /// itself, each with how many it holds.</summary>
        internal static Dictionary<ItemDrop.ItemData, int> Kind(Inventory inventory, ItemDrop.ItemData item)
        {
            var stacks = new Dictionary<ItemDrop.ItemData, int>();
            foreach (ItemDrop.ItemData mine in inventory.GetAllItems())
            {
                if (ReferenceEquals(mine, item) ||
                    (mine.m_shared.m_name == item.m_shared.m_name && mine.m_quality == item.m_quality && mine.m_worldLevel == item.m_worldLevel))
                {
                    stacks[mine] = mine.m_stack;
                }
            }
            return stacks;
        }

        // How much of an item he carries: by the very object (1, or 0 once it's gone), or a stackable by everything of its kind.
        private static int Carried(Inventory inventory, ItemDrop.ItemData item, bool byObject)
        {
            if (item == null)
            {
                return 0;
            }
            if (byObject)
            {
                return inventory.ContainsItem(item) ? 1 : 0;
            }
            int count = 0;
            foreach (int each in Kind(inventory, item).Values)
            {
                count += each;
            }
            return count;
        }

        /// <summary>
        /// One of his items onto a place of the stand (the game's attach): a stray there comes to him first, measured (no
        /// room: nothing moves); then the item written there, taken off him (any queued equip cleared, then unequipped, as
        /// the game's attach does), and one of it taken out of his inventory; unless that very item really lost one (gear:
        /// gone from his inventory; a stack: one fewer), the place is emptied again, in a finally (another mod's code
        /// throwing). True when it's on the stand and gone from him. Shown to everyone.
        /// </summary>
        internal static bool Hang(IStand stand, int place, ItemDrop.ItemData item, IWearer wearer, bool effects, out Moved stray) =>
            Hang(stand, place, item, wearer, effects, out stray, out _);

        /// <summary>The same, and <paramref name="blocked"/> says when the place held something that couldn't come off (no
        /// room for it in his inventory, or an item the stands don't move): his piece then stays with him.</summary>
        internal static bool Hang(IStand stand, int place, ItemDrop.ItemData item, IWearer wearer, bool effects, out Moved stray, out bool blocked)
        {
            stray = null;
            blocked = false;
            Inventory inventory = wearer.Inventory;
            if (item == null || !inventory.ContainsItem(item) || place < 0 || place >= stand.Places)
            {
                return false;
            }
            if (stand.Has(place))
            {
                stray = TakeOne(stand, place, inventory);
                if (stray == null)
                {
                    blocked = true;   // no room for it, or a removed mod's item: his piece stays with him
                    return false;
                }
            }
            return WriteThenRemove(stand, place, item, item, wearer, unequip: true, effects);
        }

        /// <summary>One place's item into his inventory, measured, and the place emptied and shown so (in a finally: also
        /// when another mod's code throws); null, nothing moved, when there's none, it isn't one this game can read
        /// (OneOf), or it wouldn't arrive.</summary>
        internal static Moved TakeOne(IStand stand, int place, Inventory inventory)
        {
            if (!stand.Has(place))
            {
                return null;
            }
            ItemDrop.ItemData there = stand.Read(place);
            if (!OneOf(there))
            {
                return null;
            }
            var came = new Moved { Place = place, Item = there, Amount = there.m_stack };
            bool cleared = false;
            try
            {
                if (!Add(inventory, came))
                {
                    return null;
                }
                stand.Clear(place);
                stand.Show(place);
                cleared = true;
                return came;
            }
            finally
            {
                if (!cleared && came.Before != null)
                {
                    Settle(stand, inventory, came);   // another mod's code threw: what came leaves the stand
                }
            }
        }

        /// <summary>
        /// An item that came off the stand back onto it (a cancelled put-on, a piece he couldn't wear), into its own place if
        /// that's still empty, else the place the game would hang it in if empty: written from the item as it was read, then
        /// one of its kind taken out of his inventory, from the stacks it went into first, never from one he wears or holds;
        /// unless that stack really lost one, the place is emptied again (in a finally). False, nothing moved, when it can't
        /// go back (it's gone from him, or its place is taken).
        /// </summary>
        internal static bool PutBack(IStand stand, Moved moved, IWearer wearer)
        {
            int place = !stand.Has(moved.Place) ? moved.Place : stand.FitFor(moved.Item);
            if (place < 0 || stand.Has(place))
            {
                return false;
            }
            ItemDrop.ItemData from = Source(moved, wearer);
            if (from == null)
            {
                return false;
            }
            return WriteThenRemove(stand, place, moved.Item, from, wearer, unequip: false, effects: false);
        }

        // Writes `written` into the (empty) place, then takes one of `from` out of his inventory (taking it off first, when
        // asked). The place stays written only if one really left him: RemoveOneItem said it removed one (or, when it threw
        // part way, it was reached), AND he holds one fewer now. When the one leaving is the very object (gear, or a stack of
        // one), by that object: gone from his inventory, whatever else of its kind arrives or merges meanwhile (the last
        // look's j1). A bigger stack by everything of its kind, so another mod merging stacks meanwhile can't make a real
        // move look failed, and one putting the item straight back can't make a failed one look done (one of its kind
        // arriving from elsewhere in that moment then counts as the one put back: the move is undone rather than risk a
        // copy). The write itself is inside the try: one that throws part way (another mod's patch on the game's item
        // saving) is cleared again too (review round 2's M1 and m1; the harness's twist). Shown to everyone either way.
        private static bool WriteThenRemove(IStand stand, int place, ItemDrop.ItemData written, ItemDrop.ItemData from, IWearer wearer,
                                            bool unequip, bool effects)
        {
            Inventory inventory = wearer.Inventory;
            bool byObject = Matching.Single(from) || from.m_stack == 1;
            int had = Carried(inventory, from, byObject);
            bool reached = false;
            bool returned = false;
            bool removed = false;
            bool left = false;
            try
            {
                stand.Write(place, written);
                if (!stand.Has(place))
                {
                    return false;
                }
                wearer.ClearQueued(from);
                if (unequip && (wearer.WornAs(from) != Loadouts.WornAs.None || ReferenceEquals(from, wearer.HiddenRight) ||
                                ReferenceEquals(from, wearer.HiddenLeft)))
                {
                    wearer.Unequip(from, effects);
                }
                reached = true;
                removed = inventory.RemoveOneItem(from);
                returned = true;
            }
            finally
            {
                left = (returned ? removed : reached) && Carried(inventory, from, byObject) <= had - 1;
                if (!left)
                {
                    try
                    {
                        stand.Clear(place);   // it didn't leave him: it isn't on the stand either (the place was empty)
                    }
                    catch (System.Exception)
                    {
                        // Never hides an error on its way.
                    }
                }
                try
                {
                    stand.Show(place);
                }
                catch (System.Exception)
                {
                    // Never hides an error on its way.
                }
            }
            return left;
        }

        /// <summary>
        /// One place's item straight onto another stand's place (job 30, Session 2: a loadout moved to another stand takes
        /// its gear along), both stands this computer's in this frame: written to the new place first, and only once it's
        /// there taken off the old; in a finally, if the old place still holds it (its clear threw, or didn't take), the new
        /// place is cleared again, so the item is on one stand only, whatever another mod's code does. False, nothing moved,
        /// when the old place holds nothing the stands move (StandMoves.OneOf) or the new one is taken.
        /// </summary>
        internal static bool Transfer(IStand from, int fromPlace, IStand to, int toPlace)
        {
            if (from == null || to == null || ReferenceEquals(from.Key, to.Key) || fromPlace < 0 || fromPlace >= from.Places ||
                toPlace < 0 || toPlace >= to.Places || !from.Has(fromPlace) || to.Has(toPlace))
            {
                return false;
            }
            ItemDrop.ItemData item = from.Read(fromPlace);
            if (!OneOf(item))
            {
                return false;
            }
            bool written = false;
            bool moved = false;
            try
            {
                to.Write(toPlace, item);
                written = to.Has(toPlace);
                if (!written)
                {
                    return false;
                }
                from.Clear(fromPlace);
            }
            finally
            {
                moved = written && !Holds(from, fromPlace);
                if (!moved)
                {
                    try
                    {
                        to.Clear(toPlace);   // it never left the old place: it isn't on the new one either
                    }
                    catch (System.Exception)
                    {
                        // Never hides an error on its way.
                    }
                }
                try
                {
                    from.Show(fromPlace);
                    to.Show(toPlace);
                }
                catch (System.Exception)
                {
                    // Never hides an error on its way.
                }
            }
            return moved;
        }

        // Whether a place still holds an item, asked where an error can't escape (a stand gone counts as holding nothing).
        private static bool Holds(IStand stand, int place)
        {
            try
            {
                return stand.Valid && stand.Has(place);
            }
            catch (System.Exception)
            {
                return true;   // can't tell: treated as still there, so the copy on the new place is cleared
            }
        }

        // The stack of his to take a put-back from: the item itself (gear), else a stack it grew, else any of its kind; never
        // one he wears or holds (or has queued), never empty.
        private static ItemDrop.ItemData Source(Moved moved, IWearer wearer)
        {
            Inventory inventory = wearer.Inventory;
            bool Free(ItemDrop.ItemData stack) =>
                stack != null && inventory.ContainsItem(stack) && stack.m_stack >= 1 && wearer.WornAs(stack) == Loadouts.WornAs.None &&
                !ReferenceEquals(stack, wearer.HiddenRight) && !ReferenceEquals(stack, wearer.HiddenLeft) && !wearer.Queued(stack);
            if (Free(moved.Item))
            {
                return moved.Item;
            }
            if (Matching.Single(moved.Item))
            {
                return null;   // gear: only the very item goes back
            }
            foreach (ItemDrop.ItemData stack in moved.Grew.Keys)
            {
                if (Free(stack))
                {
                    return stack;
                }
            }
            foreach (ItemDrop.ItemData stack in Kind(inventory, moved.Item).Keys)
            {
                if (Free(stack))
                {
                    return stack;
                }
            }
            return null;
        }
    }
}
