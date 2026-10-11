using System;
using System.Collections.Generic;
using System.Text;

namespace Outfitter.Loadouts
{
    /// <summary>Where in his inventory a loadout's item sits.</summary>
    internal enum CellKind
    {
        /// <summary>No fixed cell: the first free one.</summary>
        None,

        /// <summary>The top row, the hotbar (keys 1 to 8), at <see cref="LoadoutItem.Column"/>.</summary>
        Hotbar,

        /// <summary><see cref="LoadoutItem.RowsUp"/> rows up from the bottom row of the visible grid (0 is the bottom
        /// row), at <see cref="LoadoutItem.Column"/>: so a bigger inventory (Haldor's Pockets add a row at the bottom)
        /// keeps it in place (his words, 2026-09-29).</summary>
        FromBottom,

        /// <summary>One of EquipmentAndQuickSlots' slots, by its name (<see cref="LoadoutItem.SlotId"/>: Quick1 … Quick6,
        /// Helmet, Chest, Legs, Shoulder, Utility, Trinket, Utility2, Utility3).</summary>
        Slot,
    }

    /// <summary>What a loadout's item is worn as, if it was worn when saved.</summary>
    internal enum WornAs
    {
        None,
        RightHand,
        LeftHand,
        Helmet,
        Chest,
        Legs,
        Cape,
        Utility,
        Trinket,
        Ammo,
    }

    /// <summary>One item of a loadout: what it is, how many, and where it sits.</summary>
    internal sealed class LoadoutItem
    {
        /// <summary>The item's prefab name (unique, unlike the $ name a few items share).</summary>
        internal string Prefab = string.Empty;

        internal int Quality = 1;

        /// <summary>Its style (shields and capes have several).</summary>
        internal int Variant;

        /// <summary>How many: 1 for a piece of gear, the stack for a stackable item.</summary>
        internal int Count = 1;

        internal CellKind Cell;

        internal int Column;

        internal int RowsUp;

        internal string SlotId = string.Empty;

        internal WornAs Worn;

        /// <summary>Whether a loadout stand shows it (his words, 2026-10-07: two toggles, "Displayed" and "Worn"): null
        /// follows <see cref="Worn"/> (what it wears shows, what it carries doesn't, as before 0.5.0); true shows it even
        /// carried (a build hammer shown on the stand, never put in his hands); false never, even worn. Never in a share
        /// code.</summary>
        internal bool? Shown;

        /// <summary>Whether a loadout stand shows it (<see cref="Shown"/>, else whether it's worn); never a food slot.</summary>
        internal bool Displayed => Food == null && (Shown ?? Worn != WornAs.None);

        /// <summary>Who made it, for "This exact item"; never in a share code.</summary>
        internal long CrafterId;

        internal string CrafterName = string.Empty;

        /// <summary>Other mods' data on the item (its m_customData: a magic item's enchantments, a gem's sockets …) as a
        /// fingerprint (<see cref="Capture.DataOf"/>), for "This exact item": empty when it had none, null when unknown (a
        /// share code, which never carries it, or a loadout kept before it was saved).</summary>
        internal string Data;

        /// <summary>A food slot's rule (card A34, decision 72), or null for an ordinary item. A food slot has a cell and a
        /// count (how many to bring, 1 to <see cref="FoodRule.MaxCount"/>) like any item; it is never worn, its
        /// <see cref="Prefab"/> stays empty (its food is chosen at each equip: FoodPick), its quality 1 and its style 0.</summary>
        internal FoodRule Food;

        /// <summary>A copy of its own, its food rule too: changing one never changes the other.</summary>
        internal LoadoutItem Clone()
        {
            var copy = (LoadoutItem)MemberwiseClone();
            copy.Food = Food?.Clone();
            return copy;
        }

        /// <summary>Its place as words for the log ("hotbar 3", "2 rows up, column 5", "slot Helmet").</summary>
        internal string PlaceText()
        {
            string worn = Worn == WornAs.None ? string.Empty : $", worn ({Worn})";
            switch (Cell)
            {
                case CellKind.Hotbar:
                    return $"hotbar {Column + 1}{worn}";
                case CellKind.FromBottom:
                    return (RowsUp == 0 ? "the bottom row" : $"{RowsUp} row{(RowsUp == 1 ? string.Empty : "s")} up") + $", column {Column + 1}{worn}";
                case CellKind.Slot:
                    return $"slot {SlotId}{worn}";
                default:
                    return "any cell" + worn;
            }
        }
    }

    /// <summary>A saved loadout: a name, and its items in their places.</summary>
    internal sealed class Loadout
    {
        /// <summary>Made once, never changed, even by a rename: what a stand's mark points at.</summary>
        internal string Id = string.Empty;

        internal string Name = string.Empty;

        /// <summary>The character that saved it (its player ID), or 0 for one read from a code.</summary>
        internal long SavedBy;

        internal string SavedByName = string.Empty;

        internal readonly List<LoadoutItem> Items = new List<LoadoutItem>();

        /// <summary>Whether putting it on puts his favourites away like any other item, in every mode (his words,
        /// 2026-10-07: "overrides all favorited items 'stickiness'", a toggle "for each loadout beside the equip mode
        /// options", the setting its default): null follows the setting "Equip modes put away my favourites"; true or false
        /// once he sets it in the window. Never in a share code.</summary>
        internal bool? FavouritesGo;

        /// <summary>The most items a loadout may hold: every cell of the biggest inventory with EquipmentAndQuickSlots'
        /// rows (8 × 9 + 24), with room to spare.</summary>
        internal const int MaxItems = 128;

        /// <summary>The longest name.</summary>
        internal const int MaxName = 40;

        /// <summary>The most food slots a loadout may hold (he eats three foods at a time; it also bounds the work of
        /// choosing their foods at each equip: FoodPick). Its items count them among <see cref="MaxItems"/>.</summary>
        internal const int MaxFoodSlots = 16;

        /// <summary>How many of its items are food slots.</summary>
        internal int FoodSlots
        {
            get
            {
                int slots = 0;
                foreach (LoadoutItem item in Items)
                {
                    if (item.Food != null)
                    {
                        slots++;
                    }
                }
                return slots;
            }
        }

        internal Loadout Clone()
        {
            var copy = new Loadout { Id = Id, Name = Name, SavedBy = SavedBy, SavedByName = SavedByName, FavouritesGo = FavouritesGo };
            foreach (LoadoutItem item in Items)
            {
                copy.Items.Add(item.Clone());
            }
            return copy;
        }

        private const string IdLetters = "abcdefghijkmnpqrstuvwxyz23456789";

        /// <summary>A new loadout ID: eight letters and digits, none of them easy to misread.</summary>
        internal static string NewId(Random random)
        {
            var id = new StringBuilder(8);
            for (int i = 0; i < 8; i++)
            {
                id.Append(IdLetters[random.Next(IdLetters.Length)]);
            }
            return id.ToString();
        }

        /// <summary>Whether a text is a loadout ID as <see cref="NewId"/> makes them.</summary>
        internal static bool IsId(string text)
        {
            if (text == null || text.Length != 8)
            {
                return false;
            }
            foreach (char c in text)
            {
                if (IdLetters.IndexOf(c) < 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
