using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace QuickFix
{
    /// <summary>
    /// The player's settings (card B22's table, his defaults, and what the research's decisions added; his rule 3). Alone,
    /// the mod keeps them in BepInEx\config\modprojects.quickfix.cfg, General and one section per part; inside miikeskii's
    /// QoL Pack, in the bundle's file, "Quick Fix" and "Quick Fix - &lt;part&gt;". A configuration manager's window (F1)
    /// lists them all under the one heading, each part behind a title row that opens and closes it (Folds; his look at
    /// F1, 2026-09-28). Every one is read when it is used, so a change works at once.
    /// </summary>
    internal static class Settings
    {
        // Part names become section names in the settings file, where BepInEx refuses an apostrophe, quotes, brackets,
        // equals signs, backslashes and tabs (MODULES.md rule 2): so "Wheel and Anvil", shown in the window under its
        // title, "Grinding Wheel and Smith's Anvil".
        internal const string PiecesPart = "Wheel and Anvil";
        internal const string PiecesTitle = "Grinding Wheel and Smith's Anvil";
        internal const string NearPart = "Near stations";
        internal const string StonePart = "Sharpening Stone";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // General
        internal static ConfigEntry<string> MetalBars { get; private set; }
        internal static ConfigEntry<bool> RaiseCrafting { get; private set; }

        // The Grinding Wheel and the Smith's Anvil
        internal static ConfigEntry<bool> PressE { get; private set; }
        internal static ConfigEntry<WhatItRepairs> Wheel { get; private set; }
        internal static ConfigEntry<WhatItRepairs> Anvil { get; private set; }
        internal static ConfigEntry<bool> PlaceAnywhere { get; private set; }

        // Near stations
        internal static ConfigEntry<bool> OnUse { get; private set; }
        internal static ConfigEntry<bool> NearStations { get; private set; }
        internal static ConfigEntry<bool> NearPieces { get; private set; }
        internal static ConfigEntry<float> NearDistance { get; private set; }

        // The Sharpening Stone
        internal static ConfigEntry<bool> Stone { get; private set; }
        internal static ConfigEntry<StoneShare> Share { get; private set; }
        internal static ConfigEntry<bool> Tools { get; private set; }
        internal static ConfigEntry<bool> Weapons { get; private set; }
        internal static ConfigEntry<bool> Armor { get; private set; }
        internal static ConfigEntry<bool> Shields { get; private set; }
        internal static ConfigEntry<bool> Other { get; private set; }
        internal static ConfigEntry<bool> MetallicToo { get; private set; }
        internal static ConfigEntry<string> Always { get; private set; }
        internal static ConfigEntry<string> Never { get; private set; }
        internal static ConfigEntry<StoneCost> Cost { get; private set; }
        internal static ConfigEntry<int> PerUse { get; private set; }
        internal static ConfigEntry<int> PerItem { get; private set; }
        internal static ConfigEntry<StoneTime> Time { get; private set; }
        internal static ConfigEntry<float> SecondsPerItem { get; private set; }
        internal static ConfigEntry<float> FlatSeconds { get; private set; }
        internal static ConfigEntry<OnCancel> Cancel { get; private set; }
        internal static ConfigEntry<bool> CancelOnMove { get; private set; }
        internal static ConfigEntry<bool> CancelOnStagger { get; private set; }
        internal static ConfigEntry<RepairLook> Look { get; private set; }

        private static ModuleSettings bound;
        private static int order;
        private static ConfigEntry<bool> foldUnfolded;
        private static List<ConfigEntryBase> foldSettings;

        internal static void Bind(ModuleSettings settings)
        {
            bound = settings;
            Enabled = settings.Enabled;

            order = 990;
            MetalBars = Add(ModuleSettings.General, "MetalBars", Rules.DefaultBars, "Metal bars",
                "Gear counts as metallic when the recipe it's repaired by takes any of these, by the game's prefab names, " +
                "separated by commas (the idols of the Forge of Potential never count). FlametalNew is today's Flametal, " +
                "Gold is Bloodgold, Flametal is the old Ancient Metal. Add AxeHead1 and AxeHead2 to count Early Axes as " +
                "metallic, Hook for the Grappling Hook.");
            RaiseCrafting = Add(ModuleSettings.General, "RaiseCrafting", true, "Repairs raise Crafting",
                "On (the default): every repair of this mod raises your Crafting skill as the game's own repair does, by the " +
                "share of full durability each item got back. Off: no skill from these repairs.");

            BeginFold(PiecesPart, PiecesTitle, 900);
            PressE = Add(PiecesPart, "PressE", true, "Repair with the Use key",
                "On (the default): pressing the Use key (E) at a Grinding Wheel or Smith's Anvil repairs, in one go and to " +
                "full, all the gear in your inventory, worn or not, that its setting below covers. Off: they do nothing, as " +
                "in the game.");
            Wheel = Add(PiecesPart, "Wheel", WhatItRepairs.NonMetallicGear, "What the Grinding Wheel repairs",
                "Non-Metallic Gear (the default): gear whose recipe takes no metal bar (\"Metal bars\" in General). Metallic " +
                "Gear: gear whose recipe takes one. All: both. No station level is needed, and a Forge needn't be near.");
            Anvil = Add(PiecesPart, "Anvil", WhatItRepairs.MetallicGear, "What the Smith's Anvil repairs",
                "Metallic Gear (the default), Non-Metallic Gear or All, as for the Grinding Wheel.");
            PlaceAnywhere = Add(PiecesPart, "PlaceAnywhere", true, "Place them with no Forge near",
                "On (the default): the Grinding Wheel and the Smith's Anvil can be built anywhere, not only within 5 m of a " +
                "Forge. Building them still needs a Workbench near, as in the game, and they still need 2 m of room from any " +
                "other station extension. Placed away from a Forge, they don't raise its level.");
            EndFold();

            BeginFold(NearPart, NearPart, 800);
            OnUse = Add(NearPart, "OnUse", false, "Repair on using a crafting station",
                "On: opening a crafting station repairs at once, to full, everything in your inventory the station itself " +
                "can repair by the game's own rules (a Workbench its own gear, at its level). Off (the default): repair " +
                "one item at a time with its Repair button, as in the game.");
            NearStations = Add(NearPart, "NearStations", false, "Repair near crafting stations",
                "On: coming within \"Distance\" of a crafting station repairs, to full, everything it can repair by the " +
                "game's own rules (its level, a roof and fire where it needs them). Once each time you come near. Off (the " +
                "default): no repair from walking by.");
            NearPieces = Add(NearPart, "NearPieces", false, "Repair near a Grinding Wheel or Smith's Anvil",
                "On: coming within \"Distance\" of a Grinding Wheel or Smith's Anvil repairs what its setting covers, once " +
                "each time you come near. Off (the default): only the Use key does.");
            NearDistance = Add(NearPart, "Distance", 4f, "Distance (metres)",
                "How near a station, Grinding Wheel or Smith's Anvil you must come for the two settings above. 4 by default.",
                new AcceptableValueRange<float>(1f, 15f));
            EndFold();

            BeginFold(StonePart, StonePart, 700);
            Stone = Add(StonePart, "Stone", true, "Repair with a Sharpening Stone",
                "On (the default): using a Sharpening Stone (its hotbar key, or a right-click in your inventory) repairs your " +
                "gear anywhere: one bar runs, and when it fills the repair lands, by the settings below. Pressing it again " +
                "cancels. Off: it is only a material, as in the game.");
            Share = Add(StonePart, "Share", StoneShare.Half, "How much it repairs",
                "How much of each item's full durability one repair gives back, never past full: 25%, 50% (the default), 75% " +
                "or 100%.");
            Tools = Add(StonePart, "Tools", true, "Repairs tools",
                "The hammer, hoe, cultivator, pickaxes, scythe, snow shovel, serving tray and grappling hook; axes count as " +
                "tools and as weapons.");
            Weapons = Add(StonePart, "Weapons", true, "Repairs weapons",
                "Swords, maces, knives, spears, atgeirs, fists, bows, crossbows, staffs; axes count as weapons and as tools.");
            Armor = Add(StonePart, "Armor", true, "Repairs armour",
                "Helmets, chest pieces, legs and capes, the clothes too.");
            Shields = Add(StonePart, "Shields", true, "Repairs shields", "Every shield and buckler.");
            Other = Add(StonePart, "Other", true, "Repairs other gear",
                "Anything else that wears and can be repaired, such as another mod's gear of a kind not above.");
            MetallicToo = Add(StonePart, "MetallicToo", true, "Metallic gear too",
                "On (the default): metallic gear is repaired too. Off: only gear whose recipe takes no metal bar (\"Metal bars\" " +
                "in General).");
            Always = Add(StonePart, "Always", "", "Always repairs",
                "Items it always repairs, whatever their kind or metal, by the game's prefab names, separated by commas.");
            Never = Add(StonePart, "Never", "", "Never repairs",
                "Items it never repairs, by the game's prefab names, separated by commas. This wins over everything above.");
            Cost = Add(StonePart, "Cost", StoneCost.OneStone, "What a repair costs",
                "One stone used (the default). Or a share of a stone's durability, per use or per item repaired: the stone in " +
                "use then wears like a tool, with its own durability bar, comes off its stack and keeps a cell of its own (a " +
                "worn stone never stacks), and is used up at 0, the next stone carrying on. A share per item repairs the most worn gear your stones can pay for; a share per use " +
                "needs the whole share.");
            PerUse = Add(StonePart, "PerUse", 25, "Share per use (%)",
                "With \"A share of a stone's durability per use\": how much of one stone's durability each repair uses.",
                new AcceptableValueRange<int>(1, 100));
            PerItem = Add(StonePart, "PerItem", 10, "Share per item (%)",
                "With \"A share of a stone's durability per item repaired\": how much of one stone's durability each item " +
                "repaired uses.",
                new AcceptableValueRange<int>(1, 100));
            Time = Add(StonePart, "Time", StoneTime.PerItem, "How long it takes",
                "Seconds per item (the default): the bar runs \"Seconds per item\" for each item it repairs. A flat time: " +
                "\"Flat time\" whatever the number. The length is fixed when the bar starts.");
            SecondsPerItem = Add(StonePart, "SecondsPerItem", 1f, "Seconds per item",
                "With \"Seconds per item\": the bar's length for each item. 1 by default.",
                new AcceptableValueRange<float>(0.1f, 10f));
            FlatSeconds = Add(StonePart, "FlatSeconds", 5f, "Flat time (seconds)",
                "With \"A flat time\": the bar's length, whatever the number of items.",
                new AcceptableValueRange<float>(0.5f, 60f));
            Cancel = Add(StonePart, "OnCancel", OnCancel.StoneKept, "On cancel",
                "What a repair stopped before its bar fills costs: Stone kept, nothing repaired (the default), or Stone used, " +
                "nothing repaired. An attack, a sprint, a jump, a dodge, pressing the stone again, losing the stone, a " +
                "teleport, taking a ship's helm or losing your footing in water stop it; dying stops it at no cost.");
            CancelOnMove = Add(StonePart, "CancelOnMove", false, "Moving cancels",
                "On: moving (autorun too) stops the repair. Off (the default): you can walk while it runs, as while equipping " +
                "gear.");
            CancelOnStagger = Add(StonePart, "CancelOnStagger", false, "Being staggered cancels",
                "On: being staggered by a hit stops the repair. Off (the default): it carries on, as equipping gear does.");
            Look = Add(StonePart, "Look", RepairLook.EquipMotion, "While repairing",
                "What your character does while the bar runs: the equip motion (the default, as while equipping gear), the " +
                "crafting pose of a workbench, or nothing. Other players see it too.");
            EndFold();

            // The Pick buttons in F1 (card B03): the three lists picked from pictures of the game's items, their text
            // written as Rules.Names reads it (split at commas, semicolons or spaces, any case).
            PickerSettings.Bind(settings);
            Lists.Attach(MetalBars, ItemList("Gear counts as metallic when the recipe it's repaired by takes any of these.",
                                             null, new QoLMods.Catalogue.Selection(QoLMods.Catalogue.Pick.Family, "Metals")));
            Lists.Attach(Always, ItemList("Items the Sharpening Stone always repairs, whatever their kind or metal.", Repairable, null));
            Lists.Attach(Never, ItemList("Items the Sharpening Stone never repairs: this wins over everything.", Repairable, null));
        }

        private static bool Repairable(Thing thing) => thing.Item != null && Gear.CanBeRepaired(thing.Item, out _);

        private static ListSpec ItemList(string says, System.Func<Thing, bool> holds, QoLMods.Catalogue.Selection? opensOn) => new ListSpec
        {
            Module = Module.Name,
            Kinds = new[] { ThingKind.Items },
            Format = new ListFormat { Separators = ",;", WhitespaceSeparates = true, Joiner = ", " },
            Holds = holds,
            Says = says,
            OpensOn = opensOn,
            Warn = text => Module.Log?.LogWarning(text),
        };

        /// <summary>What the Sharpening Stone may repair, from the settings now.</summary>
        internal static StoneChoice StoneChoice()
        {
            GearKinds kinds = GearKinds.None;
            if (Tools.Value) kinds |= GearKinds.Tools;
            if (Weapons.Value) kinds |= GearKinds.Weapons;
            if (Armor.Value) kinds |= GearKinds.Armor;
            if (Shields.Value) kinds |= GearKinds.Shields;
            if (Other.Value) kinds |= GearKinds.Other;
            return new StoneChoice
            {
                Kinds = kinds,
                MetallicToo = MetallicToo.Value,
                Always = Rules.Names(Always.Value),
                Never = Rules.Names(Never.Value),
            };
        }

        /// <summary>Whether the stone wears like a tool now (a share of its durability is the cost).</summary>
        internal static bool StoneWears => Stone.Value && Cost.Value != StoneCost.OneStone;

        private static ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, order--, description, range);
            foldSettings?.Add(entry);
            return entry;
        }

        /// <summary>Opens a part's fold-out at <paramref name="top"/> (its title row's order; its settings follow, one order
        /// down each): a title row showing <paramref name="title"/>, then its settings, all under the module's one heading.
        /// The title row is a setting of the part's own ("Unfolded"), which remembers whether it is open.</summary>
        private static void BeginFold(string part, string title, int top)
        {
            order = top;
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, title, order--,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(title, settings));
            foldSettings = settings;
        }

        private static void EndFold()
        {
            Folds.Add(foldUnfolded, foldSettings, bound.SectionOf(ModuleSettings.General));
            foldUnfolded = null;
            foldSettings = null;
        }
    }
}
