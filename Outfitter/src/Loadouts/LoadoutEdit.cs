using System.Collections.Generic;
using ItemType = ItemDrop.ItemData.ItemType;

namespace Outfitter.Loadouts
{
    /// <summary>An item kind put into a loadout by hand (dragged from the drawer, or clicked in): what it is, and how it
    /// counts. Never one exact copy: its kind, quality and style.</summary>
    internal sealed class Putting
    {
        internal string Prefab = string.Empty;
        internal int Quality = 1;
        internal int Variant;

        /// <summary>How many it asks for (a stackable's; gear is always one).</summary>
        internal int Count = 1;

        /// <summary>The game's item for its prefab (its type, its stack size); null when this game lacks it.</summary>
        internal ItemDrop.ItemData Template;
    }

    /// <summary>A food among the game's items, for the window's picker: its prefab, the game's item for it (its icon and
    /// name), and its Health, Stamina and Eitr.</summary>
    internal sealed class FoodInfo
    {
        internal string Prefab = string.Empty;
        internal ItemDrop.ItemData Template;
        internal float Health;
        internal float Stamina;
        internal float Eitr;
    }

    /// <summary>
    /// Editing a loadout cell by cell in Outfitter's window (his words, 2026-10-06: "freely edit and configure all of my
    /// loadouts from the L screen using everything that's near me"; job 30, Session 2). Each edit returns a changed copy of
    /// the loadout (the caller saves it), or null with the reason it can't be made, and never touches a real item. The
    /// cells are his inventory's (GridShape: the hotbar, his rows, EquipmentAndQuickSlots' active slots), and an item's
    /// place is kept as Places keeps it. Worn follows the game: a body place is worn by one item (EquipmentAndQuickSlots'
    /// belt slots excepted), and the hands by the game's own rules (Humanoid.EquipItem, 1.0.17). Pure, so the scratch
    /// harness reaches it.
    /// </summary>
    internal static class LoadoutEdit
    {
        /// <summary>The loadout's item in a cell, or null.</summary>
        internal static LoadoutItem At(Loadout loadout, GridShape grid, int x, int y)
        {
            if (loadout == null)
            {
                return null;
            }
            foreach (LoadoutItem item in loadout.Items)
            {
                if (Places.CellFor(item, grid, out int ix, out int iy) && ix == x && iy == y)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>Whether a cell may hold an item of this type: a cell of his rows takes anything; one of
        /// EquipmentAndQuickSlots' slots only what it takes (a quick slot anything, a gear slot its own kind).</summary>
        internal static bool Takes(GridShape grid, int x, int y, ItemType type)
        {
            if (!grid.Usable(x, y))
            {
                return false;
            }
            string slot = grid.SlotAt(x, y);
            if (slot == null || slot.StartsWith("Quick", System.StringComparison.Ordinal))
            {
                return true;
            }
            switch (slot)
            {
                case "Helmet":
                    return type == ItemType.Helmet;
                case "Chest":
                    return type == ItemType.Chest;
                case "Legs":
                    return type == ItemType.Legs;
                case "Shoulder":
                    return type == ItemType.Shoulder;
                case "Utility":
                case "Utility2":
                case "Utility3":
                    return type == ItemType.Utility;
                case "Trinket":
                    return type == ItemType.Trinket;
                default:
                    return false;
            }
        }

        /// <summary>One of EquipmentAndQuickSlots' gear slots: what it wears (its item is always worn), else None.</summary>
        internal static WornAs SlotWears(GridShape grid, int x, int y)
        {
            switch (grid.SlotAt(x, y))
            {
                case "Helmet":
                    return WornAs.Helmet;
                case "Chest":
                    return WornAs.Chest;
                case "Legs":
                    return WornAs.Legs;
                case "Shoulder":
                    return WornAs.Cape;
                case "Utility":
                case "Utility2":
                case "Utility3":
                    return WornAs.Utility;
                case "Trinket":
                    return WornAs.Trinket;
                default:
                    return WornAs.None;
            }
        }

        /// <summary>
        /// Puts an item kind into a cell (what was there goes). Gear asks for one; a stackable for its count, at most one
        /// stack. Into one of EquipmentAndQuickSlots' gear slots it's worn; elsewhere, with <paramref name="wearIfFree"/>,
        /// it's worn when nothing of the loadout is worn in its place (the hands by the game's rules), else carried.
        /// </summary>
        internal static Loadout Put(Loadout loadout, GridShape grid, int x, int y, Putting putting, bool wearIfFree, out string why)
        {
            why = null;
            if (loadout == null || putting?.Template == null || string.IsNullOrEmpty(putting.Prefab))
            {
                why = "That item isn't in this game.";
                return null;
            }
            ItemType type = putting.Template.m_shared.m_itemType;
            if (!Takes(grid, x, y, type))
            {
                why = grid.SlotAt(x, y) != null ? $"The {grid.SlotAt(x, y)} slot doesn't take that." : "There's no cell there.";
                return null;
            }
            Loadout changed = loadout.Clone();
            LoadoutItem there = At(changed, grid, x, y);
            if (there != null)
            {
                changed.Items.Remove(there);
            }
            if (changed.Items.Count >= Loadout.MaxItems)
            {
                why = $"A loadout holds at most {Loadout.MaxItems} items.";
                return null;
            }
            var item = new LoadoutItem
            {
                Prefab = putting.Prefab,
                Quality = System.Math.Max(1, putting.Quality),
                Variant = System.Math.Max(0, putting.Variant),
                Count = Stackable(putting.Template) ? Clamp(putting.Count, putting.Template) : 1,
            };
            Places.Of(x, y, grid, item);
            WornAs slotWears = SlotWears(grid, x, y);
            if (slotWears != WornAs.None)
            {
                Unwear(changed, grid, slotWears, keepSlot: true);
                item.Worn = slotWears;
            }
            else if (wearIfFree)
            {
                item.Worn = FreeWear(changed, type, out LoadoutItem torchMovesLeft);
                if (torchMovesLeft != null)
                {
                    torchMovesLeft.Worn = WornAs.LeftHand;   // the game moves a torch in the right hand to the left for a weapon
                }
            }
            changed.Items.Add(item);
            return changed;
        }

        /// <summary>Moves a cell's item to another cell; an item there takes its place (a swap). Into one of
        /// EquipmentAndQuickSlots' gear slots an item is worn.</summary>
        internal static Loadout Move(Loadout loadout, GridShape grid, int fromX, int fromY, int toX, int toY,
                                     System.Func<string, ItemDrop.ItemData> template, out string why)
        {
            why = null;
            if (fromX == toX && fromY == toY)
            {
                return null;
            }
            Loadout changed = loadout?.Clone();
            LoadoutItem moving = At(changed, grid, fromX, fromY);
            if (moving == null)
            {
                return null;
            }
            LoadoutItem other = At(changed, grid, toX, toY);
            ItemType movingType = TypeOf(moving, template);
            if (!Takes(grid, toX, toY, movingType))
            {
                why = grid.SlotAt(toX, toY) != null ? $"The {grid.SlotAt(toX, toY)} slot doesn't take that." : "There's no cell there.";
                return null;
            }
            if (other != null && !Takes(grid, fromX, fromY, TypeOf(other, template)))
            {
                why = $"The {grid.SlotAt(fromX, fromY)} slot doesn't take what's there.";
                return null;
            }
            Places.Of(toX, toY, grid, moving);
            if (other != null)
            {
                Places.Of(fromX, fromY, grid, other);
            }
            // Into a gear slot an item is worn, and what else of the loadout was worn there (now in his rows) is carried.
            WornAs wears = SlotWears(grid, toX, toY);
            if (wears != WornAs.None)
            {
                Unwear(changed, grid, wears, keepSlot: true, except: moving);
                moving.Worn = wears;
            }
            WornAs otherWears = other != null ? SlotWears(grid, fromX, fromY) : WornAs.None;
            if (otherWears != WornAs.None)
            {
                Unwear(changed, grid, otherWears, keepSlot: true, except: other);
                other.Worn = otherWears;
            }
            return changed;
        }

        /// <summary>Takes a cell's item out of the loadout.</summary>
        internal static Loadout Remove(Loadout loadout, GridShape grid, int x, int y)
        {
            Loadout changed = loadout?.Clone();
            LoadoutItem item = At(changed, grid, x, y);
            if (item == null)
            {
                return null;
            }
            changed.Items.Remove(item);
            return changed;
        }

        /// <summary>Sets a stackable's count, from one to one stack; gear stays at one; a food slot's from one to
        /// <see cref="FoodRule.MaxCount"/>, whatever <paramref name="template"/> is (null: nothing to change).</summary>
        internal static Loadout SetCount(Loadout loadout, GridShape grid, int x, int y, int count, ItemDrop.ItemData template)
        {
            Loadout changed = loadout?.Clone();
            LoadoutItem item = At(changed, grid, x, y);
            if (item?.Food != null)
            {
                int slotCount = System.Math.Max(1, System.Math.Min(count, FoodRule.MaxCount));
                if (slotCount == item.Count)
                {
                    return null;
                }
                item.Count = slotCount;
                return changed;
            }
            if (item == null || template == null || !Stackable(template))
            {
                return null;
            }
            int set = Clamp(count, template);
            if (set == item.Count)
            {
                return null;
            }
            item.Count = set;
            return changed;
        }

        /// <summary>
        /// Marks a cell's item worn or carried. Worn: in its body place, or the hand the game would put it in; whatever of
        /// the loadout is worn there (or would be taken off for it, by the game's rules for hands) becomes carried. An item
        /// in one of EquipmentAndQuickSlots' gear slots is always worn. Whether a stand shows the item stays as it was (his
        /// two toggles, 2026-10-07: "Displayed" and "Worn", each its own). Null when it can't be worn (not gear) or nothing
        /// changes.
        /// </summary>
        internal static Loadout SetWorn(Loadout loadout, GridShape grid, int x, int y, bool worn, ItemDrop.ItemData template, out string why)
        {
            why = null;
            Loadout changed = loadout?.Clone();
            LoadoutItem item = At(changed, grid, x, y);
            if (item == null || template == null)
            {
                return null;
            }
            bool displayed = item.Displayed;
            if (SlotWears(grid, x, y) != WornAs.None)
            {
                why = "An item in a gear slot is always worn.";
                return null;
            }
            if (!worn)
            {
                if (item.Worn == WornAs.None)
                {
                    return null;
                }
                item.Worn = WornAs.None;
                KeepDisplayed(item, displayed);
                return changed;
            }
            if (item.Worn != WornAs.None)
            {
                return null;
            }
            ItemType type = template.m_shared.m_itemType;
            WornAs body = Equip.Matching.BodyPlace(type);
            if (body != WornAs.None)
            {
                if (body != WornAs.Utility && InGearSlot(changed, grid, body, item))
                {
                    why = "The piece in the gear slot is the one worn there: move this one into the slot to wear it.";
                    return null;
                }
                Unwear(changed, grid, body, keepSlot: true, except: item);
                item.Worn = body;
                KeepDisplayed(item, displayed);
                return changed;
            }
            if (!Held(type))
            {
                why = "That isn't something to wear or hold.";
                return null;
            }
            WearInHands(changed, item, type);
            KeepDisplayed(item, displayed);
            return changed;
        }

        /// <summary>Marks whether a loadout stand shows a cell's item (his "Displayed", 2026-10-07), whatever it wears: a
        /// build hammer shown on the stand and never put in his hands. Null for a food slot (never on a stand), an empty
        /// cell, or nothing changed.</summary>
        internal static Loadout SetShown(Loadout loadout, GridShape grid, int x, int y, bool shown)
        {
            Loadout changed = loadout?.Clone();
            LoadoutItem item = At(changed, grid, x, y);
            if (item == null || item.Food != null || item.Displayed == shown)
            {
                return null;
            }
            KeepDisplayed(item, shown);
            return changed;
        }

        // Whether a stand shows an item, kept so whatever it wears: unset when it's what its worn says (as before 0.5.0).
        internal static void KeepDisplayed(LoadoutItem item, bool displayed) =>
            item.Shown = displayed == (item.Worn != WornAs.None) ? (bool?)null : displayed;

        /// <summary>
        /// Saving over a loadout (the window's Save over, a stand's "Save over it") keeps what a capture can't know (0.5.0):
        /// its "Favourites go too", and whether a stand shows each item, onto the captured item of the same prefab (each
        /// once, in the loadout's order; one worn alike first, worn to worn and carried to carried: review 1's m7; the
        /// capture's own worn as it is). A copy; <paramref name="into"/> as it is when there's nothing to keep from.
        /// </summary>
        internal static Loadout KeepOwnSettings(Loadout from, Loadout into)
        {
            if (from == null || into == null)
            {
                return into;
            }
            Loadout kept = into.Clone();
            kept.FavouritesGo = from.FavouritesGo;
            var used = new HashSet<LoadoutItem>();
            foreach (LoadoutItem was in from.Items)
            {
                if (was.Food != null || !was.Shown.HasValue)
                {
                    continue;
                }
                bool wasWorn = was.Worn != WornAs.None;
                LoadoutItem now = kept.Items.Find(item => item.Food == null && !item.Shown.HasValue && !used.Contains(item) && item.Prefab == was.Prefab &&
                                                          (item.Worn != WornAs.None) == wasWorn)
                                  ?? kept.Items.Find(item => item.Food == null && !item.Shown.HasValue && !used.Contains(item) && item.Prefab == was.Prefab);
                if (now != null)
                {
                    used.Add(now);
                    KeepDisplayed(now, was.Displayed);
                }
            }
            return kept;
        }

        /// <summary>
        /// Wears a hand item as the game equips it (Humanoid.EquipItem, 1.0.17), taking off of the loadout what the game
        /// would take off: a torch goes left beside a one-handed weapon alone, else right (a shield stays); a one-handed
        /// weapon goes right (a torch alone in the right hand moving left; a shield or torch on the left stays); a shield
        /// goes left (a one-handed weapon or torch on the right stays); a bow, a left-handed two-hander, a two-hander and a
        /// tool take both hands.
        /// </summary>
        private static void WearInHands(Loadout loadout, LoadoutItem item, ItemType type)
        {
            LoadoutItem right = null;
            LoadoutItem left = null;
            foreach (LoadoutItem each in loadout.Items)
            {
                if (each == item)
                {
                    continue;
                }
                if (each.Worn == WornAs.RightHand && right == null)
                {
                    right = each;
                }
                else if (each.Worn == WornAs.LeftHand && left == null)
                {
                    left = each;
                }
            }
            ItemType? rightType = right != null ? HandType(right) : (ItemType?)null;
            ItemType? leftType = left != null ? HandType(left) : (ItemType?)null;
            void Off(LoadoutItem held)
            {
                if (held != null)
                {
                    held.Worn = WornAs.None;
                }
            }
            switch (type)
            {
                case ItemType.Torch:
                    if (rightType == ItemType.OneHandedWeapon && left == null)
                    {
                        item.Worn = WornAs.LeftHand;
                        return;
                    }
                    Off(right);
                    if (leftType != ItemType.Shield)
                    {
                        Off(left);
                    }
                    item.Worn = WornAs.RightHand;
                    return;
                case ItemType.OneHandedWeapon:
                    if (rightType == ItemType.Torch && left == null)
                    {
                        right.Worn = WornAs.LeftHand;
                        item.Worn = WornAs.RightHand;
                        return;
                    }
                    Off(right);
                    if (leftType != ItemType.Shield && leftType != ItemType.Torch)
                    {
                        Off(left);
                    }
                    item.Worn = WornAs.RightHand;
                    return;
                case ItemType.Shield:
                    Off(left);
                    if (rightType != ItemType.OneHandedWeapon && rightType != ItemType.Torch)
                    {
                        Off(right);
                    }
                    item.Worn = WornAs.LeftHand;
                    return;
                case ItemType.Bow:
                case ItemType.TwoHandedWeaponLeft:
                    Off(right);
                    Off(left);
                    item.Worn = WornAs.LeftHand;
                    return;
                default:
                    Off(right);
                    Off(left);
                    item.Worn = WornAs.RightHand;
                    return;
            }
        }

        // Whether an item of the loadout other than this one sits in one of EquipmentAndQuickSlots' gear slots worn as this.
        private static bool InGearSlot(Loadout loadout, GridShape grid, WornAs body, LoadoutItem except)
        {
            foreach (LoadoutItem each in loadout.Items)
            {
                if (each != except && each.Worn == body && InGearSlotHere(each, grid))
                {
                    return true;
                }
            }
            return false;
        }

        // Whether an item sits in one of EquipmentAndQuickSlots' gear slots that this grid has. A slot's place kept from a
        // grid with that mod's slots, edited where they aren't (the mod uninstalled, or its gear slots off), is no slot
        // here: the item has no cell, and it's worn or carried as any other (job 30, Session 2, the harness's
        // S2-setworn-inactive-slot).
        internal static bool InGearSlotHere(LoadoutItem item, GridShape grid) =>
            item.Cell == CellKind.Slot && !Places.IsQuickSlot(item) && item.SlotId != null && grid.Slots.ContainsKey(item.SlotId);

        // Whether the loadout wears something in this body place outside this grid's gear slots (in his rows, a quick slot,
        // or a slot this grid hasn't): a free gear slot would then wear a second one there.
        internal static bool WornOutsideSlots(Loadout loadout, GridShape grid, WornAs body)
        {
            if (loadout == null || body == WornAs.None)
            {
                return false;
            }
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item.Worn == body && !InGearSlotHere(item, grid))
                {
                    return true;
                }
            }
            return false;
        }

        // What the game holds in a hand.
        private static bool Held(ItemType type) =>
            type == ItemType.OneHandedWeapon || type == ItemType.TwoHandedWeapon || type == ItemType.TwoHandedWeaponLeft || type == ItemType.Bow ||
            type == ItemType.Torch || type == ItemType.Tool || type == ItemType.Shield;

        /// <summary>The first free cell for an item kind, as the game would fill it: weapons, tools, shields, belts,
        /// trinkets and odds from the top row down, the rest from the bottom row up; or, for gear with a free gear slot
        /// of EquipmentAndQuickSlots' for it and <paramref name="toSlot"/>, that slot, unless the loadout already wears
        /// something in its place outside the gear slots (a slot's piece is worn, and "Gear dragged in goes on" wears it
        /// only where nothing else is worn: the harness's S2-click-into-gear-slot; a belt in the Utility slot leaves
        /// Utility2 free, as that mod wears a belt in each). False when every cell is taken.</summary>
        internal static bool FirstFree(Loadout loadout, GridShape grid, ItemDrop.ItemData template, bool toSlot, out int x, out int y)
        {
            x = y = -1;
            if (template == null)
            {
                return false;
            }
            ItemType type = template.m_shared.m_itemType;
            if (toSlot && !WornOutsideSlots(loadout, grid, Equip.Matching.BodyPlace(type)))
            {
                foreach (KeyValuePair<string, SlotCell> slot in grid.Slots)
                {
                    if (!slot.Key.StartsWith("Quick", System.StringComparison.Ordinal) && Takes(grid, slot.Value.X, slot.Value.Y, type) &&
                        At(loadout, grid, slot.Value.X, slot.Value.Y) == null)
                    {
                        x = slot.Value.X;
                        y = slot.Value.Y;
                        return true;
                    }
                }
            }
            bool topFirst = TopFirst(type);
            for (int i = 0; i < grid.Rows; i++)
            {
                int row = topFirst ? i : grid.Rows - 1 - i;
                for (int column = 0; column < grid.Width; column++)
                {
                    if (At(loadout, grid, column, row) == null)
                    {
                        x = column;
                        y = row;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>A new, empty loadout, ready to fill.</summary>
        internal static Loadout Empty(string id, string name, long savedBy, string savedByName) =>
            new Loadout { Id = id, Name = name, SavedBy = savedBy, SavedByName = savedByName ?? string.Empty };

        /// <summary>
        /// Saving over a loadout (the window's Save over, a stand's "Save over it") keeps its food slots (review round 1 of
        /// job 30's Session 3, M1): every slot of <paramref name="from"/> (its rule, count and place) goes into
        /// <paramref name="into"/> (what was just captured). Of the foods the capture took that <paramref name="from"/> didn't
        /// name as items of their own, the slots take one each, as they would choose from them (FoodPick.Assign, on what was
        /// captured alone), and those foods are left out, every stack of each, wherever it lies (round 2's m1: saved as an
        /// item it would bar every slot from it, as you eat one of each; the last look's m1: only the food each slot takes,
        /// so a new food beside an "any food" slot is saved). Anything else captured stays, and a slot whose cell it lies in
        /// moves to the first free cell of <paramref name="grid"/> from the bottom, after every slot whose own cell is free
        /// (round 2's m2, the last look's n1).
        /// <paramref name="said"/> says what was left to the slots and what moved (null: nothing). Null, with why, when no
        /// cell is free for such a slot, or the loadout would hold more than it may; <paramref name="into"/> as it is when
        /// <paramref name="from"/> has no slot.
        /// </summary>
        internal static Loadout KeepFoodSlots(Loadout from, Loadout into, GridShape grid, out string why, out string said, int statAtLeast = 1)
        {
            why = null;
            said = null;
            if (from == null || into == null || from.FoodSlots == 0)
            {
                return into;
            }
            var slots = new List<LoadoutItem>();
            var named = new HashSet<string>();
            foreach (LoadoutItem item in from.Items)
            {
                if (item.Food != null)
                {
                    slots.Add(item);
                    continue;
                }
                ItemDrop.ItemData template = TemplateOf(item.Prefab);
                if (template != null)
                {
                    named.Add(template.m_shared.m_name);
                }
            }
            Loadout kept = into.Clone();
            // The captured foods no item names, one kind per $ name (as the game counts "one of each"), as the slots would
            // choose among them.
            var kinds = new List<Equip.FoodKind>();
            var kindOf = new Dictionary<string, Equip.FoodKind>(System.StringComparer.Ordinal);
            foreach (LoadoutItem item in kept.Items)
            {
                ItemDrop.ItemData template = item.Food == null ? TemplateOf(item.Prefab) : null;
                if (!Equip.FoodPick.IsFood(template) || named.Contains(template.m_shared.m_name))
                {
                    continue;
                }
                string name = template.m_shared.m_name;
                if (!kindOf.TryGetValue(name, out Equip.FoodKind kind))
                {
                    kindOf[name] = kind = new Equip.FoodKind { Name = name, Prefab = item.Prefab, Template = template };
                    kinds.Add(kind);
                }
                else if (string.CompareOrdinal(item.Prefab, kind.Prefab) < 0)
                {
                    kind.Prefab = item.Prefab;
                    kind.Template = template;
                }
                kind.Carried += System.Math.Max(1, item.Count);
            }
            var taken = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (Equip.FoodKind kind in Equip.FoodPick.Assign(slots, kinds, statAtLeast))
            {
                if (kind != null)
                {
                    taken.Add(kind.Name);
                }
            }
            var leftOut = new List<string>();
            kept.Items.RemoveAll(item =>
            {
                ItemDrop.ItemData template = item.Food == null && taken.Count > 0 ? TemplateOf(item.Prefab) : null;
                if (template == null || !taken.Contains(template.m_shared.m_name))
                {
                    return false;
                }
                leftOut.Add(Equip.Messages.NameOf(item) + (item.Count > 1 ? " x" + item.Count : string.Empty));
                return true;
            });
            // Every slot whose own place is free first, in the loadout's order; then each whose place the capture holds,
            // to the first free cell from the bottom (so a moved slot never takes a later slot's own cell).
            var copies = new List<LoadoutItem>();
            var blocked = new List<(LoadoutItem Copy, LoadoutItem There)>();
            foreach (LoadoutItem slot in slots)
            {
                LoadoutItem copy = slot.Clone();
                LoadoutItem there = copy.Cell == CellKind.None ? null : kept.Items.Find(item => SamePlace(item, copy));
                if (there == null)
                {
                    kept.Items.Add(copy);
                }
                else
                {
                    blocked.Add((copy, there));
                }
                copies.Add(copy);
            }
            var moved = new List<string>();
            foreach ((LoadoutItem copy, LoadoutItem there) in blocked)
            {
                if (grid == null || !FreeFromBottom(kept, grid, out Vector2i free))
                {
                    why = $"{into.Name}'s food slot's cell holds your {Equip.Messages.NameOf(there)}, and no cell is free to move the slot to: free one first.";
                    return null;
                }
                Places.Of(free.x, free.y, grid, copy);
                kept.Items.Add(copy);
                moved.Add(Equip.Messages.NameOf(there));
            }
            // The slots in the loadout's order, after the captured items (as before).
            kept.Items.RemoveAll(item => item.Food != null && copies.Contains(item));
            kept.Items.AddRange(copies);
            if (kept.Items.Count > Loadout.MaxItems)
            {
                why = $"With its {slots.Count} food slot{(slots.Count == 1 ? "" : "s")} kept, {into.Name} would hold more than {Loadout.MaxItems} items: pick fewer cells.";
                return null;
            }
            var parts = new List<string> { $"its {slots.Count} food slot{(slots.Count == 1 ? "" : "s")} kept" };
            if (leftOut.Count > 0)
            {
                parts.Add($"{string.Join(", ", leftOut)} left to {(slots.Count == 1 ? "it" : "them")}, not saved as items");
            }
            if (moved.Count > 0)
            {
                parts.Add($"{(moved.Count == 1 ? "a slot" : moved.Count + " slots")} moved to a free cell, as your {string.Join(", ", moved)} lay in {(moved.Count == 1 ? "its" : "theirs")}");
            }
            said = string.Join("; ", parts);
            return kept;
        }

        /// <summary>The first free cell of his rows, from the bottom row up and each row from the left (where the game puts
        /// food first): "Add a food slot"'s cell, and where a kept slot moves.</summary>
        internal static bool FreeFromBottom(Loadout loadout, GridShape grid, out Vector2i cell)
        {
            for (int row = grid.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    if (At(loadout, grid, column, row) == null)
                    {
                        cell = new Vector2i(column, row);
                        return true;
                    }
                }
            }
            cell = new Vector2i(-1, -1);
            return false;
        }

        // Whether two items are kept at one place (its kind of cell, column, rows up and slot), whatever the grid.
        private static bool SamePlace(LoadoutItem a, LoadoutItem b) =>
            a.Cell == b.Cell && a.Column == b.Column && a.RowsUp == b.RowsUp && string.Equals(a.SlotId ?? string.Empty, b.SlotId ?? string.Empty, System.StringComparison.Ordinal);

        /// <summary>
        /// Makes a cell a food slot (card A34, decision 72; the window's "Make a food slot"): an empty cell (its rule: any
        /// food, the most of all three; its count <paramref name="count"/>, the setting "Food slots: count at first"), or a
        /// cell holding a food, which it replaces in the loadout's order (its rule: from a list, that food ticked; its count
        /// kept). Null, with why, for anything else: no cell there, one of EquipmentAndQuickSlots' gear slots, a cell
        /// holding something that isn't a food (<paramref name="template"/>: the game's item for a prefab), a food slot
        /// already, or a loadout full of items or of food slots (<see cref="Loadout.MaxFoodSlots"/>).
        /// </summary>
        internal static Loadout MakeFoodSlot(Loadout loadout, GridShape grid, int x, int y, int count, System.Func<string, ItemDrop.ItemData> template,
                                             out string why)
        {
            why = null;
            if (loadout == null)
            {
                return null;
            }
            if (!Takes(grid, x, y, ItemType.Consumable))
            {
                why = grid.SlotAt(x, y) != null ? $"The {grid.SlotAt(x, y)} slot doesn't take a food slot." : "There's no cell there.";
                return null;
            }
            Loadout changed = loadout.Clone();
            LoadoutItem there = At(changed, grid, x, y);
            var slot = new LoadoutItem();
            if (there != null)
            {
                if (there.Food != null)
                {
                    why = "That cell is a food slot already.";
                    return null;
                }
                ItemDrop.ItemData food = template?.Invoke(there.Prefab);
                if (!Equip.FoodPick.IsFood(food))
                {
                    why = "Only an empty cell or a food can become a food slot.";
                    return null;
                }
                // Another cell naming the same food would keep the slot from ever taking it (one of each is eaten: review
                // round 1 of job 30's Session 3, m4).
                foreach (LoadoutItem other in changed.Items)
                {
                    ItemDrop.ItemData its = other != there && other.Food == null ? template(other.Prefab) : null;
                    if (its != null && its.m_shared.m_name == food.m_shared.m_name)
                    {
                        why = "Another cell of this loadout holds this food too, which would keep the slot from ever taking it: take that one out first.";
                        return null;
                    }
                }
                slot.Food = FoodRule.OnlyThis(there.Prefab);
                slot.Count = there.Count;
            }
            else
            {
                if (loadout.Items.Count >= Loadout.MaxItems)
                {
                    why = $"A loadout holds at most {Loadout.MaxItems} items.";
                    return null;
                }
                slot.Food = FoodRule.AnyFood();
                slot.Count = count;
            }
            if (changed.FoodSlots >= Loadout.MaxFoodSlots)
            {
                why = $"A loadout holds at most {Loadout.MaxFoodSlots} food slots.";
                return null;
            }
            slot.Count = System.Math.Max(1, System.Math.Min(slot.Count, FoodRule.MaxCount));
            Places.Of(x, y, grid, slot);
            if (there != null)
            {
                changed.Items[changed.Items.IndexOf(there)] = slot;   // in the food's place in the loadout's order
            }
            else
            {
                changed.Items.Add(slot);
            }
            return changed;
        }

        /// <summary>Changes a food slot's rule to a copy of <paramref name="rule"/> (the caller's own stays its own). Null,
        /// with why, when the cell holds no food slot or the rule can't be kept (FoodRule.WhyNotValid); null alone when
        /// nothing changes.</summary>
        internal static Loadout SetFoodRule(Loadout loadout, GridShape grid, int x, int y, FoodRule rule, out string why)
        {
            why = null;
            Loadout changed = loadout?.Clone();
            LoadoutItem slot = At(changed, grid, x, y);
            if (slot?.Food == null)
            {
                why = "That cell isn't a food slot.";
                return null;
            }
            if (rule == null)
            {
                return null;
            }
            why = rule.WhyNotValid();
            if (why != null)
            {
                return null;
            }
            if (slot.Food.SameAs(rule))
            {
                return null;
            }
            slot.Food = rule.Clone();
            return changed;
        }

        /// <summary>The foods among the game's items (FoodPick.IsFood), each prefab once, in the order given, with their
        /// three values: for the window's picker ("Choose foods…", which sorts them by biome). <paramref name="templates"/>:
        /// each prefab's name and the game's item for it.</summary>
        internal static List<FoodInfo> FoodsAmong(IEnumerable<KeyValuePair<string, ItemDrop.ItemData>> templates)
        {
            var foods = new List<FoodInfo>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            if (templates == null)
            {
                return foods;
            }
            foreach (KeyValuePair<string, ItemDrop.ItemData> each in templates)
            {
                if (string.IsNullOrEmpty(each.Key) || !Equip.FoodPick.IsFood(each.Value) || !seen.Add(each.Key))
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = each.Value.m_shared;
                foods.Add(new FoodInfo
                {
                    Prefab = each.Key,
                    Template = each.Value,
                    Health = shared.m_food,
                    Stamina = shared.m_foodStamina,
                    Eitr = shared.m_foodEitr,
                });
            }
            return foods;
        }

        /// <summary>Whether the game stacks this item.</summary>
        internal static bool Stackable(ItemDrop.ItemData template) => template != null && template.m_shared.m_maxStackSize > 1;

        // A count kept within one to one stack.
        private static int Clamp(int count, ItemDrop.ItemData template) =>
            System.Math.Max(1, System.Math.Min(count, System.Math.Max(1, template.m_shared.m_maxStackSize)));

        // The game's own first-free-cell order (Inventory.TopFirst, 1.0.17).
        private static bool TopFirst(ItemType type) =>
            type == ItemType.OneHandedWeapon || type == ItemType.Bow || type == ItemType.TwoHandedWeapon || type == ItemType.TwoHandedWeaponLeft ||
            type == ItemType.Torch || type == ItemType.Tool || type == ItemType.Shield || type == ItemType.Utility || type == ItemType.Misc ||
            type == ItemType.Trinket;

        private static ItemType TypeOf(LoadoutItem item, System.Func<string, ItemDrop.ItemData> template)
        {
            if (item.Food != null)
            {
                return ItemType.Consumable;   // a food slot goes where food may (never into a gear slot)
            }
            ItemDrop.ItemData found = template?.Invoke(item.Prefab);
            return found != null ? found.m_shared.m_itemType : ItemType.Misc;
        }

        /// <summary>
        /// Where an item of this type would be worn without taking anything of the loadout off: its body place when no
        /// item of the loadout is worn there (EquipmentAndQuickSlots' belt slots take more than one belt, but a belt in his
        /// rows counts as the one), or the hand the game would put it in beside what the loadout holds. A one-handed weapon
        /// beside a torch alone in the right hand moves the torch to the left, as the game does
        /// (<paramref name="torchMovesLeft"/>). None: nothing free.
        /// </summary>
        internal static WornAs FreeWear(Loadout loadout, ItemType type, out LoadoutItem torchMovesLeft)
        {
            torchMovesLeft = null;
            WornAs body = Equip.Matching.BodyPlace(type);
            if (body != WornAs.None)
            {
                foreach (LoadoutItem item in loadout.Items)
                {
                    if (item.Worn == body)
                    {
                        return WornAs.None;
                    }
                }
                return body;
            }
            LoadoutItem rightItem = null;
            LoadoutItem leftItem = null;
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item.Worn == WornAs.RightHand && rightItem == null)
                {
                    rightItem = item;
                }
                else if (item.Worn == WornAs.LeftHand && leftItem == null)
                {
                    leftItem = item;
                }
            }
            ItemType? right = rightItem != null ? HandType(rightItem) : (ItemType?)null;
            ItemType? left = leftItem != null ? HandType(leftItem) : (ItemType?)null;
            switch (type)
            {
                case ItemType.Torch:
                    if (right == ItemType.OneHandedWeapon && left == null)
                    {
                        return WornAs.LeftHand;
                    }
                    return right == null && (left == null || left == ItemType.Shield) ? WornAs.RightHand : WornAs.None;
                case ItemType.Shield:
                    return left == null && (right == null || right == ItemType.OneHandedWeapon || right == ItemType.Torch) ? WornAs.LeftHand : WornAs.None;
                case ItemType.OneHandedWeapon:
                    if (right == ItemType.Torch && left == null)
                    {
                        torchMovesLeft = rightItem;
                        return WornAs.RightHand;
                    }
                    return right == null && (left == null || left == ItemType.Shield || left == ItemType.Torch) ? WornAs.RightHand : WornAs.None;
                case ItemType.TwoHandedWeapon:
                case ItemType.Tool:
                    return right == null && left == null ? WornAs.RightHand : WornAs.None;
                case ItemType.Bow:
                case ItemType.TwoHandedWeaponLeft:
                    return right == null && left == null ? WornAs.LeftHand : WornAs.None;
                default:
                    return WornAs.None;
            }
        }

        /// <summary>The template seam for a loadout item's type (Codes.Template in game; the harness sets another).</summary>
        internal static System.Func<string, ItemDrop.ItemData> TemplateOf = prefab => Codes.Template(prefab);

        private static ItemType HandType(LoadoutItem item)
        {
            ItemDrop.ItemData found = TemplateOf(item.Prefab);
            return found != null ? found.m_shared.m_itemType : ItemType.OneHandedWeapon;
        }

        // Makes whatever else of the loadout is worn in this body place carried; with keepSlot, a piece in one of
        // EquipmentAndQuickSlots' gear slots this grid has stays worn (its belt slots wear a belt each).
        private static void Unwear(Loadout loadout, GridShape grid, WornAs body, bool keepSlot, LoadoutItem except = null)
        {
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item == except || item.Worn != body)
                {
                    continue;
                }
                if (keepSlot && InGearSlotHere(item, grid))
                {
                    continue;
                }
                item.Worn = WornAs.None;
            }
        }
    }
}
