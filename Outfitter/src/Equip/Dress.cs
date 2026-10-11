using System.Collections.Generic;
using Outfitter.Loadouts;

namespace Outfitter.Equip
{
    /// <summary>
    /// Dressing him once the chests' part is done (the Outfitter plan, "Equipping", step 5), from a plan made on his
    /// inventory alone: what the loadout doesn't wear comes off (not what the chests' part left on: no chest took it and
    /// nothing took its place); the hands, when they change, are emptied and the right-hand item put on before the left (a
    /// torch beside a one-handed weapon goes left only that way: 37-B's fresh read of Humanoid.EquipItem), a hand nothing
    /// fills getting back what it held, and nothing of his ever put beside the loadout's where the game would knock the
    /// loadout's off (Beside); then the rest of the gear, and what the chests' part took off for nothing back on; then
    /// every item goes to its saved cell. Items move
    /// inside his inventory only by their cell (m_gridPos): to an empty cell, or swapping with what's there, never by the
    /// game's moves, which make a copy; never into a cell outside his grid, and never two items in one cell (the game
    /// loses one at its next load). The inventory's change callback runs once at the end.
    /// </summary>
    internal static class Dress
    {
        /// <summary>
        /// Puts on and takes off what the plan says, then arranges. False when the game won't put gear on now (he attacks,
        /// dodges, swims or eats) and nothing was done: ask again next frame. Pieces the game refuses for good (broken, a
        /// DLC's, below the world's level) are said in <paramref name="result"/>. <paramref name="stayOn"/> is what the
        /// chests' part left on, or took off and wants back on (Exchange.StayOn): never taken off here.
        /// <paramref name="offHands"/>: the hand items the chests' part took off, and from which hand (Exchange.OffHands),
        /// so one of them in <paramref name="stayOn"/> goes back into its hand.
        /// </summary>
        internal static bool Run(EquipPlan plan, PlanInput input, IWearer wearer, EquipRules rules, EquipResult result,
                                 ICollection<ItemDrop.ItemData> stayOn = null, IDictionary<ItemDrop.ItemData, WornAs> offHands = null)
        {
            if (rules.Instant && !wearer.CanEquipNow)
            {
                return false;
            }
            foreach (ItemDrop.ItemData item in plan.TakeOff)
            {
                if (stayOn != null && stayOn.Contains(item))
                {
                    continue;
                }
                wearer.ClearQueued(item);
                if (wearer.WornAs(item) != WornAs.None || ReferenceEquals(item, wearer.HiddenRight) || ReferenceEquals(item, wearer.HiddenLeft))
                {
                    wearer.Unequip(item, rules.Effects);
                }
            }

            // What each hand holds (a hand item the game put away counted in it), or else what the chests' part took off
            // that hand and wants back on (R3, finding 2: it read the hands after they were emptied).
            ItemDrop.ItemData wornRight = wearer.RightItem ?? wearer.HiddenRight;
            ItemDrop.ItemData wornLeft = wearer.LeftItem ?? wearer.HiddenLeft;
            ItemDrop.ItemData heldRight = wornRight ?? Back(offHands, stayOn, wearer, WornAs.RightHand);
            ItemDrop.ItemData heldLeft = wornLeft ?? Back(offHands, stayOn, wearer, WornAs.LeftHand);
            ItemDrop.ItemData right = HandItem(plan, input, WornAs.RightHand, out bool rightIsLoadouts);
            ItemDrop.ItemData left = HandItem(plan, input, WornAs.LeftHand, out bool leftIsLoadouts);
            // Something of his kept ("Keep wearing mine") never knocks the loadout's off: the game empties the right hand
            // for a bow, a staff or a second torch (R2, finding 1).
            if (!Beside(right, left))
            {
                if (!leftIsLoadouts)
                {
                    left = null;
                }
                else if (!rightIsLoadouts)
                {
                    right = null;
                }
            }
            // A hand nothing fills keeps what it held, unless that comes off or the game won't hold it beside the other
            // hand's item (R1, finding 37: a held torch while torches aren't gear stays in hand); a held torch the loadout's
            // one-handed weapon takes the right hand from goes into an empty left hand, as the game itself moves it.
            if (right == null && heldRight != null && !ReferenceEquals(heldRight, left) && Keeps(plan, heldRight, stayOn) && Beside(heldRight, left))
            {
                right = heldRight;
            }
            if (left == null && heldLeft != null && !ReferenceEquals(heldLeft, right) && Keeps(plan, heldLeft, stayOn) && Beside(right, heldLeft))
            {
                left = heldLeft;
            }
            if (left == null && heldRight != null && !ReferenceEquals(heldRight, right) && Keeps(plan, heldRight, stayOn) &&
                heldRight.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && Beside(right, heldRight))
            {
                left = heldRight;
            }
            // Nothing to do when the hands already hold these (a weapon he put away stays put away: R2, finding 7).
            bool backToHand = !ReferenceEquals(heldRight, wornRight) || !ReferenceEquals(heldLeft, wornLeft);
            if ((plan.HandsChange || backToHand) && !(ReferenceEquals(right, wornRight) && ReferenceEquals(left, wornLeft)))
            {
                foreach (ItemDrop.ItemData held in new[] { wearer.RightItem, wearer.LeftItem, wearer.HiddenRight, wearer.HiddenLeft })
                {
                    if (held != null)
                    {
                        wearer.ClearQueued(held);
                        wearer.Unequip(held, rules.Effects);
                    }
                }
                PutOn(right, wearer, rules, result, count: rightIsLoadouts);
                PutOn(left, wearer, rules, result, count: leftIsLoadouts);
            }

            foreach (Want want in plan.Wants)
            {
                if (want.Worn == WornAs.None || want.Worn == WornAs.RightHand || want.Worn == WornAs.LeftHand || want.Have.Count == 0)
                {
                    continue;
                }
                ItemDrop.ItemData item = WornStack(want, input);
                if (item != null && wearer.WornAs(item) == WornAs.None)
                {
                    PutOn(item, wearer, rules, result);
                }
            }

            // What the chests' part took off and is still with him (no chest took it, or what was to replace it didn't
            // come) goes back on where nothing else went (R1, finding 14; R3, finding 2); a hand item went back above.
            if (stayOn != null)
            {
                foreach (ItemDrop.ItemData item in stayOn)
                {
                    WornAs place = Matching.BodyPlace(item.m_shared.m_itemType);
                    if (place != WornAs.None && wearer.WornAs(item) == WornAs.None && !wearer.Queued(item) && wearer.Inventory.ContainsItem(item) &&
                        !Filled(wearer, place))
                    {
                        PutOn(item, wearer, rules, result, count: false);
                    }
                }
                // Once dressed, what is neither worn nor on its way on isn't "left on" any more, so a later round may put
                // it away (pruned after dressing, not before: a piece still waiting to go back on stays: R4).
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(stayOn))
                {
                    if (wearer.WornAs(item) == WornAs.None && !wearer.Queued(item) && !ReferenceEquals(item, wearer.HiddenRight) &&
                        !ReferenceEquals(item, wearer.HiddenLeft))
                    {
                        stayOn.Remove(item);
                    }
                }
            }

            Arrange(plan, input, wearer.Inventory, rules);
            return true;
        }

        // A hand item the chests' part took off that hand and wants back on (in stayOn, still his, not worn now), or null.
        private static ItemDrop.ItemData Back(IDictionary<ItemDrop.ItemData, WornAs> offHands, ICollection<ItemDrop.ItemData> stayOn,
                                              IWearer wearer, WornAs hand)
        {
            if (offHands == null || stayOn == null)
            {
                return null;
            }
            foreach (KeyValuePair<ItemDrop.ItemData, WornAs> off in offHands)
            {
                if (off.Value == hand && stayOn.Contains(off.Key) && wearer.Inventory.ContainsItem(off.Key) && wearer.WornAs(off.Key) == WornAs.None)
                {
                    return off.Key;
                }
            }
            return null;
        }

        // Whether he wears something in a body place, or something is on its way on there (the game's own way queues it:
        // the loadout's piece mustn't be followed by the old one, which the game would then put on over it: R4).
        private static bool Filled(IWearer wearer, WornAs place)
        {
            foreach (ItemDrop.ItemData item in wearer.Inventory.GetAllItems())
            {
                if (wearer.WornAs(item) == place || (wearer.Queued(item) && Matching.BodyPlace(item.m_shared.m_itemType) == place))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether the game holds these two at once when the right-hand one is put on first, then the left (its
        /// EquipItem): a shield beside a one-handed weapon or a torch, a torch beside a one-handed weapon; a bow, a staff or a
        /// second torch empties the right hand, and a shield or a torch drops a two-handed weapon or a tool from it. A
        /// missing one always fits.</summary>
        internal static bool Beside(ItemDrop.ItemData right, ItemDrop.ItemData left) =>
            right == null || left == null || Beside(right.m_shared.m_itemType, left.m_shared.m_itemType);

        internal static bool Beside(ItemDrop.ItemData.ItemType right, ItemDrop.ItemData.ItemType left)
        {
            switch (left)
            {
                case ItemDrop.ItemData.ItemType.Shield:
                    return right == ItemDrop.ItemData.ItemType.OneHandedWeapon || right == ItemDrop.ItemData.ItemType.Torch;
                case ItemDrop.ItemData.ItemType.Torch:
                    return right == ItemDrop.ItemData.ItemType.OneHandedWeapon;
                default:
                    return false;
            }
        }

        // Whether a held item stays held when nothing else fills its hand: it doesn't come off (or the chests' part left
        // it on), and it's still his.
        private static bool Keeps(EquipPlan plan, ItemDrop.ItemData held, ICollection<ItemDrop.ItemData> stayOn) =>
            !plan.TakeOff.Contains(held) || (stayOn != null && stayOn.Contains(held));

        // The item a hand should hold: the loadout's for that hand (loadouts: true), else what "Keep wearing mine" kept
        // there.
        private static ItemDrop.ItemData HandItem(EquipPlan plan, PlanInput input, WornAs hand, out bool loadouts)
        {
            loadouts = false;
            foreach (Want want in plan.Wants)
            {
                if (want.Worn == hand && want.Have.Count > 0)
                {
                    loadouts = true;
                    return want.Have[0].Item;
                }
            }
            foreach (ItemDrop.ItemData item in plan.KeptOn)
            {
                if (EquipPlanner.WornNow(input, item) == hand)
                {
                    return item;
                }
            }
            return null;
        }

        // The stack to wear for a worn stackable item (arrows): the one in its saved cell, else the one worn now, else the
        // biggest; for gear, its one piece.
        private static ItemDrop.ItemData WornStack(Want want, PlanInput input)
        {
            ItemDrop.ItemData best = null;
            foreach (Use use in want.Have)
            {
                ItemDrop.ItemData item = use.Item;
                if (EquipPlanner.WornNow(input, item) == want.Worn)
                {
                    return item;
                }
                foreach (LoadoutItem li in want.Items)
                {
                    if (li.Worn != WornAs.None && Places.CellFor(li, input.Grid, out int x, out int y) && item.m_gridPos.x == x && item.m_gridPos.y == y)
                    {
                        return item;
                    }
                }
                if (best == null || item.m_stack > best.m_stack)
                {
                    best = item;
                }
            }
            return best;
        }

        // Puts one item on; count: one of the loadout's pieces (not his own put back), counted once however many rounds
        // put it on again (the world harness, 37-B).
        private static void PutOn(ItemDrop.ItemData item, IWearer wearer, EquipRules rules, EquipResult result, bool count = true)
        {
            if (item == null || wearer.WornAs(item) != WornAs.None)
            {
                return;
            }
            wearer.ClearQueued(item);
            bool on = rules.Instant ? wearer.Equip(item, rules.Effects) : wearer.QueueEquip(item);
            if (on)
            {
                if (count && result.PiecesPutOn.Add(item))
                {
                    result.Equipped++;
                }
                return;
            }
            string name = Messages.Localize(item.m_shared.m_name);
            string why = Matching.Broken(item) ? "broken"
                : item.m_shared.m_dlc != null && item.m_shared.m_dlc.Length > 0 ? "needs its DLC"
                : item.m_worldLevel < rules.WorldLevel ? "made below this world's level"
                : "the game wouldn't put it on";
            string line = $"{name}: {why}";
            if (!result.NotPutOn.Contains(line))
            {
                result.NotPutOn.Add(line);
            }
        }

        /// <summary>Moves each of the loadout's items to its saved cell (Places): gear first, then stackable items; what's
        /// in the way moves to the item's old cell (or the next free cell), never displacing an item already placed; stackable
        /// items of one kind are gathered into their saved cells' stacks, never from or into a stack he wears. With
        /// EquipmentAndQuickSlots an unworn piece is never left in one of its equipment cells.</summary>
        internal static void Arrange(EquipPlan plan, PlanInput input, Inventory inventory, EquipRules rules)
        {
            GridShape grid = input.Grid;
            bool moved = false;
            var placed = new HashSet<ItemDrop.ItemData>();
            var keeps = new Dictionary<ItemDrop.ItemData, int>();   // a placed stack's own saved count
            // The cells the loadout's items are saved in: an item without a cell of its own never takes one (R2, finding 5).
            var saved = new HashSet<Vector2i>();
            foreach (Want want in plan.Wants)
            {
                if (want.Have.Count == 0)
                {
                    continue;
                }
                foreach (LoadoutItem li in want.Items)
                {
                    if (Places.CellFor(li, grid, out int sx, out int sy))
                    {
                        saved.Add(new Vector2i(sx, sy));
                    }
                }
            }
            Dictionary<Vector2i, List<(Want Want, LoadoutItem Item)>> kinds = SavedKinds(plan, grid);
            if (rules.Places == PlacesKept.AsSaved)
            {
                foreach (bool gear in new[] { true, false })
                {
                    foreach (Want want in plan.Wants)
                    {
                        if (want.Gear != gear || want.Have.Count == 0)
                        {
                            continue;
                        }
                        foreach (LoadoutItem li in want.Items)
                        {
                            ItemDrop.ItemData item = ItemFor(want, li, input, placed, kinds, saved);
                            if (item == null)
                            {
                                continue;
                            }
                            if (!Target(li, item, grid, inventory, input, rules, out int x, out int y))
                            {
                                moved |= Loose(li, item, grid, inventory, input, placed, saved);
                                continue;
                            }
                            moved |= Place(item, x, y, inventory, grid, input, rules, placed, saved);
                            if (!gear && placed.Contains(item))
                            {
                                moved |= Gather(item, li.Count, want, inventory, input, placed, keeps);
                                keeps[item] = System.Math.Min(li.Count, item.m_shared.m_maxStackSize);
                            }
                        }
                    }
                }
            }
            moved |= EmptyEquipmentCells(inventory, grid, input);
            if (moved)
            {
                inventory.m_onChanged?.Invoke();
            }
        }

        // Which of his items goes to this loadout item's cell: gear's one piece; for a stackable item, of the stacks that
        // answer its want, the one already there, else the biggest not placed yet that isn't in another saved cell of its kind (another
        // saved stack stays in its own: the world harness, 37-B; only its surplus tops this one up, by Gather).
        private static ItemDrop.ItemData ItemFor(Want want, LoadoutItem li, PlanInput input, HashSet<ItemDrop.ItemData> placed,
                                                 Dictionary<Vector2i, List<(Want Want, LoadoutItem Item)>> kinds, HashSet<Vector2i> saved)
        {
            if (want.Gear)
            {
                return want.Have[0].Item;
            }
            bool fixedCell = Places.CellFor(li, input.Grid, out int x, out int y);
            ItemDrop.ItemData best = null;
            foreach (Use use in want.Have)
            {
                ItemDrop.ItemData item = use.Item;
                if (placed.Contains(item) || item.m_stack <= 0 || !input.Inventory.ContainsItem(item) || !Matching.Answers(item, li, input.Rules))
                {
                    continue;
                }
                if (fixedCell && item.m_gridPos.x == x && item.m_gridPos.y == y)
                {
                    return item;
                }
                // A stack in one of EquipmentAndQuickSlots' quick slots that this want counts only part of stays there for
                // another cell (review round 2 of job 30's Session 3, m4): moved out whole, its surplus lost the quick slot's
                // keep and went away at the next press. A stack of his rows takes the cell and Gather tops it up from the quick
                // slot's counted share; with none, the quick slot's stack answers where it lies. A stack counted whole goes to
                // its cell as before (the last look's j1), and so does one lying in a cell another of the loadout's items is
                // saved in, which must leave it anyway (the last look's m2). Into another quick slot it may always go.
                if (EquipPlanner.InQuickSlot(input, item) && !(fixedCell && QuickCell(input.Grid, x, y)) && use.Amount < item.m_stack &&
                    !saved.Contains(item.m_gridPos))
                {
                    continue;
                }
                if (fixedCell && SavedFor(want, li, input.Grid, item.m_gridPos, kinds))
                {
                    continue;   // another saved stack of this kind is saved there, and stays in its own (an item without a
                                // cell of its own takes its stack anyway, and Loose moves it out: the harness,
                                // itemfor-skips-cellless-stack). A stack in a cell another kind is saved in is taken, and
                                // moved to its own cell (job 30, Session 3: the food agent's arrange-landed-in-a-saved-cell)
                }
                if (best == null || item.m_stack > best.m_stack)
                {
                    best = item;
                }
            }
            return best;
        }

        // Whether a cell is one of EquipmentAndQuickSlots' quick slots.
        private static bool QuickCell(GridShape grid, int x, int y)
        {
            string slot = grid?.SlotAt(x, y);
            return slot != null && grid.Slots[slot].Quick;
        }

        // Whether a saved stack of the same kind as this want's (its prefab and quality: in this want, or in another, with
        // "Topping up counts" per saved stack) other than this item is saved in this cell.
        private static bool SavedFor(Want want, LoadoutItem li, GridShape grid, Vector2i cell, Dictionary<Vector2i, List<(Want Want, LoadoutItem Item)>> kinds)
        {
            if (!kinds.TryGetValue(cell, out List<(Want Want, LoadoutItem Item)> there))
            {
                return false;
            }
            foreach ((Want other, LoadoutItem item) in there)
            {
                if (item != li && other.Prefab == want.Prefab && item.Quality == li.Quality)
                {
                    return true;
                }
            }
            return false;
        }

        // Every saved cell of the plan's wants, with the wants and items saved there.
        private static Dictionary<Vector2i, List<(Want Want, LoadoutItem Item)>> SavedKinds(EquipPlan plan, GridShape grid)
        {
            var kinds = new Dictionary<Vector2i, List<(Want, LoadoutItem)>>();
            foreach (Want want in plan.Wants)
            {
                foreach (LoadoutItem li in want.Items)
                {
                    if (Places.CellFor(li, grid, out int x, out int y))
                    {
                        var cell = new Vector2i(x, y);
                        if (!kinds.TryGetValue(cell, out List<(Want, LoadoutItem)> list))
                        {
                            kinds[cell] = list = new List<(Want, LoadoutItem)>();
                        }
                        list.Add((want, li));
                    }
                }
            }
            return kinds;
        }

        // The cell an item goes to: for a piece he wears that one of EquipmentAndQuickSlots' equipment slots takes, that
        // slot's cell (it would move the piece back there anyway); else its saved one; else none.
        private static bool Target(LoadoutItem li, ItemDrop.ItemData item, GridShape grid, Inventory inventory, PlanInput input,
                                   EquipRules rules, out int x, out int y)
        {
            if (input.WornAs(item) != WornAs.None)
            {
                foreach (KeyValuePair<string, SlotCell> slot in grid.Slots)
                {
                    if (slot.Value.Equipment && SlotTakes(slot.Key, item.m_shared.m_itemType))
                    {
                        x = slot.Value.X;
                        y = slot.Value.Y;
                        return true;
                    }
                }
            }
            return Places.CellFor(li, grid, out x, out y);
        }

        // An item with no saved cell in this grid (a quick-slot item while EquipmentAndQuickSlots isn't running, or one
        // saved with more rows than he has now) goes where the setting says (R1, finding 18): a quick-slot item to a free
        // hotbar cell ("HotbarIfFree") or to the first free main cell; anything else stays where it is. Never into a cell
        // another of the loadout's items has. True when it moved.
        private static bool Loose(LoadoutItem li, ItemDrop.ItemData item, GridShape grid, Inventory inventory, PlanInput input,
                                  HashSet<ItemDrop.ItemData> placed, HashSet<Vector2i> saved)
        {
            if (!Places.IsQuickSlot(li) || grid.Slots.Count > 0)
            {
                return false;
            }
            if (input.Rules.QuickSlotsWithout == QuickSlotItemsWithout.HotbarIfFree)
            {
                if (item.m_gridPos.y == 0 && !saved.Contains(item.m_gridPos))
                {
                    placed.Add(item);
                    return false;   // already in the hotbar, in no other item's cell (R3, finding 6)
                }
                for (int column = 0; column < grid.Width; column++)
                {
                    if (inventory.GetItemAt(column, 0) == null && !saved.Contains(new Vector2i(column, 0)))
                    {
                        item.m_gridPos = new Vector2i(column, 0);
                        placed.Add(item);
                        return true;
                    }
                }
            }
            if (item.m_gridPos.y >= 1 && item.m_gridPos.y < grid.Rows && !saved.Contains(item.m_gridPos))
            {
                placed.Add(item);
                return false;   // already in a main cell, in no other item's cell
            }
            for (int row = 1; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    if (inventory.GetItemAt(column, row) == null && !saved.Contains(new Vector2i(column, row)))
                    {
                        item.m_gridPos = new Vector2i(column, row);
                        placed.Add(item);
                        return true;
                    }
                }
            }
            return false;
        }

        // Moves one item to (x, y), dealing with what's there. True when anything moved.
        private static bool Place(ItemDrop.ItemData item, int x, int y, Inventory inventory, GridShape grid, PlanInput input,
                                  EquipRules rules, HashSet<ItemDrop.ItemData> placed, HashSet<Vector2i> saved)
        {
            if (!grid.Usable(x, y))
            {
                return false;
            }
            if (item.m_gridPos.x == x && item.m_gridPos.y == y)
            {
                placed.Add(item);
                return false;
            }
            if (!MaySit(item, x, y, grid, input))
            {
                return false;
            }
            ItemDrop.ItemData there = inventory.GetItemAt(x, y);
            if (there == null)
            {
                item.m_gridPos = new Vector2i(x, y);
                placed.Add(item);
                return true;
            }
            if (placed.Contains(there))
            {
                return false;   // another of the loadout's items holds it already
            }
            // A worn piece's EquipmentAndQuickSlots slot is always its own: what's there moves (it would move the piece
            // back there anyway: R2, finding 8).
            bool wornSlot = input.WornAs(item) != WornAs.None && grid.SlotAt(x, y) is string slotName && grid.Slots[slotName].Equipment;
            if (rules.Taken == CellTaken.UseTheNextFreeCell && !wornSlot)
            {
                // The item goes to the next free cell after its own, in reading order, rather than moving what's there
                // (R1, finding 18), never into another item's saved cell (R2, finding 5).
                if (NextFree(inventory, grid, x, y, item, saved, out int nx, out int ny) && MaySit(item, nx, ny, grid, input))
                {
                    bool moves = item.m_gridPos.x != nx || item.m_gridPos.y != ny;
                    item.m_gridPos = new Vector2i(nx, ny);
                    placed.Add(item);
                    return moves;
                }
                return false;
            }
            Vector2i from = item.m_gridPos;
            if (grid.Usable(from.x, from.y) && MaySit(there, from.x, from.y, grid, input))
            {
                there.m_gridPos = from;   // a swap: what was there takes the item's old cell
                item.m_gridPos = new Vector2i(x, y);
                placed.Add(item);
                return true;
            }
            if (FreeCell(inventory, grid, input, there, out int fx, out int fy))
            {
                there.m_gridPos = new Vector2i(fx, fy);
                item.m_gridPos = new Vector2i(x, y);
                placed.Add(item);
                return true;
            }
            return false;
        }

        // The first free cell of the visible rows after (x, y) in reading order (the hotbar only after the rest), the
        // item's own cell counting as free for it (so an item already there stays), never another item's saved cell; or
        // none.
        private static bool NextFree(Inventory inventory, GridShape grid, int x, int y, ItemDrop.ItemData item, HashSet<Vector2i> saved,
                                     out int nx, out int ny)
        {
            int cells = grid.Width * grid.Rows;
            int start = y < grid.Rows ? y * grid.Width + x : 0;
            for (int step = 1; step <= cells; step++)
            {
                int index = (start + step) % cells;
                int cx = index % grid.Width;
                int cy = index / grid.Width;
                if (cy == 0 && y != 0)
                {
                    continue;   // the hotbar isn't a "next cell" for an item saved below it
                }
                ItemDrop.ItemData there = inventory.GetItemAt(cx, cy);
                if ((there == null || ReferenceEquals(there, item)) && !saved.Contains(new Vector2i(cx, cy)))
                {
                    nx = cx;
                    ny = cy;
                    return true;
                }
            }
            nx = -1;
            ny = -1;
            return false;
        }

        // Whether an item may sit in a cell: any cell of the visible grid or a quick slot; one of EquipmentAndQuickSlots'
        // equipment cells only for a piece he wears of that slot's kind (it moves anything else out).
        private static bool MaySit(ItemDrop.ItemData item, int x, int y, GridShape grid, PlanInput input)
        {
            if (y < grid.Rows)
            {
                return x >= 0 && x < grid.Width && y >= 0;
            }
            string slot = grid.SlotAt(x, y);
            if (slot == null)
            {
                return false;
            }
            SlotCell cell = grid.Slots[slot];
            if (cell.Quick)
            {
                return true;
            }
            if (!cell.Equipment || input.WornAs(item) == WornAs.None)
            {
                return false;
            }
            return SlotTakes(slot, item.m_shared.m_itemType);
        }

        private static bool SlotTakes(string slot, ItemDrop.ItemData.ItemType type)
        {
            switch (slot)
            {
                case "Helmet":
                    return type == ItemDrop.ItemData.ItemType.Helmet;
                case "Chest":
                    return type == ItemDrop.ItemData.ItemType.Chest;
                case "Legs":
                    return type == ItemDrop.ItemData.ItemType.Legs;
                case "Shoulder":
                    return type == ItemDrop.ItemData.ItemType.Shoulder;
                case "Utility":
                case "Utility2":
                case "Utility3":
                    return type == ItemDrop.ItemData.ItemType.Utility;
                case "Trinket":
                    return type == ItemDrop.ItemData.ItemType.Trinket;
                default:
                    return false;
            }
        }

        // A free cell for an item moved out of the way: the visible grid from the bottom row up (the game's own order for
        // most items), the hotbar last.
        private static bool FreeCell(Inventory inventory, GridShape grid, PlanInput input, ItemDrop.ItemData item, out int x, out int y)
        {
            for (int row = grid.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    if (inventory.GetItemAt(column, row) == null)
                    {
                        x = column;
                        y = row;
                        return true;
                    }
                }
            }
            x = -1;
            y = -1;
            return false;
        }

        // Tops a placed stack up to its saved count from the other stacks answering its want, as much of each as answers it
        // (another saved stack's own count stays its own: the world harness, 37-B; R3, finding 3; a stack already placed for
        // another item gives only what's beyond its count: R4), of exactly its kind (name, quality, world level), never a
        // stack he wears. True when anything moved.
        private static bool Gather(ItemDrop.ItemData target, int count, Want want, Inventory inventory, PlanInput input, HashSet<ItemDrop.ItemData> placed,
                                   Dictionary<ItemDrop.ItemData, int> keeps)
        {
            bool moved = false;
            int max = target.m_shared.m_maxStackSize;
            int wanted = System.Math.Min(count, max);
            if (EquipPlanner.WornNow(input, target) != WornAs.None)
            {
                return false;
            }
            foreach (Use use in new List<Use>(want.Have))
            {
                ItemDrop.ItemData other = use.Item;
                if (target.m_stack >= wanted)
                {
                    break;
                }
                // A stack placed for another of the loadout's items gives only what's beyond its own saved count (R4).
                int spare = placed.Contains(other) ? (keeps.TryGetValue(other, out int keep) ? other.m_stack - keep : 0) : other.m_stack;
                if (ReferenceEquals(other, target) || spare <= 0 || other.m_stack <= 0 || use.Amount <= 0 || !inventory.ContainsItem(other) ||
                    other.m_shared.m_name != target.m_shared.m_name || other.m_quality != target.m_quality ||
                    other.m_worldLevel != target.m_worldLevel || EquipPlanner.WornNow(input, other) != WornAs.None)
                {
                    continue;
                }
                // At most the part of that stack that answers this want: another saved stack keeps its own count (R3,
                // finding 3).
                int amount = System.Math.Min(System.Math.Min(wanted - target.m_stack, spare), use.Amount);
                target.m_stack += amount;
                other.m_stack -= amount;
                use.Amount -= amount;
                if (other.m_stack <= 0)
                {
                    inventory.RemoveItem(other);
                }
                moved = true;
            }
            return moved;
        }

        // With EquipmentAndQuickSlots: an unworn item left in one of its equipment cells (what came off and stayed with
        // him) goes to a free cell of the visible grid now, so its own tidying never moves it to the hotbar.
        private static bool EmptyEquipmentCells(Inventory inventory, GridShape grid, PlanInput input)
        {
            bool moved = false;
            foreach (KeyValuePair<string, SlotCell> slot in grid.Slots)
            {
                if (!slot.Value.Equipment)
                {
                    continue;
                }
                ItemDrop.ItemData there = inventory.GetItemAt(slot.Value.X, slot.Value.Y);
                if (there == null || input.WornAs(there) != WornAs.None)
                {
                    continue;
                }
                if (FreeCell(inventory, grid, input, there, out int x, out int y))
                {
                    there.m_gridPos = new Vector2i(x, y);
                    moved = true;
                }
            }
            return moved;
        }
    }
}
