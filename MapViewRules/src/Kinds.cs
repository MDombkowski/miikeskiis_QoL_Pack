using System;
using System.Collections.Generic;

namespace MapViewRules
{
    /// <summary>
    /// What Cartur's Map Pins calls things, and where each goes in this mod's settings.
    ///
    /// Cartur's keeps a record line for every pin it places, keyed "Category" or "Category:Kind" (PinPlacer.TryPin,
    /// read in its 1.6.0). The categories are its PinCategory names. A pickable's kind is its name for the thing
    /// (Cloudberry), or, when it has none, the name of the group it sorted the thing into (Pickable:HighValue). Its six
    /// pickable groups are sorted by words in the thing's name (PinCatalog.ClassifyPickable), never stored, so the table
    /// below gives each kind of its 1.6.0 kind list (Subtypes.Pickables) the group those words put it in. A kind added by a
    /// later version isn't in it, and its pins follow the Pickables section until it is.
    /// </summary>
    internal static class Kinds
    {
        internal const string Pickable = "Pickable";

        /// <summary>Cartur's categories, in its own order, with this mod's settings part for each and how a sentence names
        /// it. The part names are permanent: they name sections of the settings file.</summary>
        internal static readonly Category[] Categories =
        {
            new Category("Ore", "Ore", "ore deposits"),
            new Category("Dungeon", "Dungeons", "dungeons"),
            new Category("Camp", "Camps", "camps and villages"),
            new Category("BossAltar", "Boss altars", "boss altars"),
            new Category("Beehive", "Beehives", "beehives"),
            new Category(Pickable, "Pickables", "pickables"),
            new Category("Runestone", "Runestones", "runestones"),
            new Category("LoreStone", "Lore stones", "lore stones"),
            new Category("Chest", "Chests", "chests"),
            new Category("Spawner", "Spawners", "spawners"),
            new Category("Leviathan", "Leviathans", "leviathans"),
            new Category("Trader", "Traders", "traders"),
            new Category("Wisp", "Wisps", "wisp spawners"),
            new Category("Landmark", "Landmarks", "landmarks"),
            new Category("Prop", "Props", "props"),
            new Category("Miniboss", "Minibosses", "minibosses"),
            new Category("Home", "Home", "your home"),
        };

        /// <summary>Cartur's pickable groups, in its own order, with this mod's settings part for each.</summary>
        internal static readonly Category[] Groups =
        {
            new Category("HighValue", "Pickables - High value", "high-value pickables"),
            new Category("Berries", "Pickables - Berries", "berries"),
            new Category("Mushrooms", "Pickables - Mushrooms", "mushrooms"),
            new Category("Crops", "Pickables - Crops", "crops and herbs"),
            new Category("Junk", "Pickables - Junk", "branches, stones and flint"),
            new Category("Other", "Pickables - Other", "other pickables"),
        };

        // Each pickable kind of Cartur's 1.6.0 and its group, by the words its PinCatalog.ClassifyPickable sorts by, read
        // in that order: "surtlingcore", "yggdrasil" or "egg" is high value; "mushroom", "jotunpuff", "magecap" or
        // "smokepuff" a mushroom; "raspberr", "blueberr", "cloudberr", "berry" or "berries" a berry; "thistle",
        // "dandelion", "seed", "barley", "flax", "carrot", "turnip" or "onion" a crop; "branch", "wood", "stone", "flint",
        // "feather" or "resin" junk; anything else other. The words are looked for in the thing's prefab name, its item's
        // prefab name and its item's name ($item_…).
        private static readonly Dictionary<string, string> GroupOfKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Raspberry", "Berries" },          // RaspberryBush
            { "Blueberry", "Berries" },          // BlueberryBush
            { "Cloudberry", "Berries" },         // CloudberryBush
            { "Lingonberry", "Berries" },        // LingonberryBush
            { "Vineberry", "Berries" },          // VineGreen, VineAsh: the item Vineberry
            { "Jotun Puffs", "Mushrooms" },      // Pickable_Mushroom_JotunPuffs
            { "Magecap", "Mushrooms" },          // Pickable_Mushroom_Magecap
            { "Yellow Mushroom", "Mushrooms" },  // Pickable_Mushroom_yellow
            { "Smoke Puff", "Mushrooms" },       // Pickable_SmokePuff
            { "Mushroom", "Mushrooms" },         // Pickable_Mushroom
            { "Dandelion", "Crops" },
            { "Thistle", "Crops" },
            { "Fiddlehead", "Other" },           // Pickable_Fiddlehead: none of the words
            { "Carrot", "Crops" },
            { "Turnip", "Crops" },
            { "Onion", "Crops" },
            { "Barley", "Crops" },
            { "Flax", "Crops" },
            { "Flint", "Junk" },                 // Pickable_Flint
            { "Branch", "Junk" },                // Pickable_Branch
            { "Surtling Core", "HighValue" },    // Pickable_SurtlingCoreStand
            { "Molten Core", "Other" },
            { "Black Core", "Other" },
            { "Cave Crystal", "Other" },
            { "Royal Jelly", "Other" },
            { "Obsidian", "Other" },             // Pickable_Obsidian
            { "Tin Nugget", "Other" },           // Pickable_Tin
            { "Bog Iron", "Other" },             // Pickable_BogIronOre
            { "Tar", "Other" },                  // Pickable_Tar
            { "Meteorite", "Other" },
            { "Dragon Egg", "HighValue" },
            { "Volture Egg", "HighValue" },
        };

        /// <summary>Splits a record key, "Category" or "Category:Kind", into its category and its kind (null if none).</summary>
        internal static void Split(string key, out string category, out string kind)
        {
            int colon = key.IndexOf(':');
            if (colon < 0)
            {
                category = key;
                kind = null;
                return;
            }
            category = key.Substring(0, colon);
            kind = colon + 1 < key.Length ? key.Substring(colon + 1) : null;
        }

        /// <summary>The Cartur's group name of a pickable's kind: the kind itself when it is a group's name (Cartur's
        /// fallback, Pickable:HighValue), else the table's group, else null.</summary>
        internal static string GroupOf(string kind)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return null;
            }
            foreach (Category group in Groups)
            {
                if (string.Equals(group.Name, kind, StringComparison.OrdinalIgnoreCase))
                {
                    return group.Name;
                }
            }
            return GroupOfKind.TryGetValue(kind, out string found) ? found : null;
        }

        /// <summary>One of Cartur's categories or groups: its own name, this mod's settings part, and how a sentence
        /// names its pins.</summary>
        internal sealed class Category
        {
            internal readonly string Name;
            internal readonly string Part;
            internal readonly string Plural;

            internal Category(string name, string part, string plural)
            {
                Name = name;
                Part = part;
                Plural = plural;
            }
        }
    }
}
