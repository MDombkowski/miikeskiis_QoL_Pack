using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.ListPicker;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// The player's settings (card A45's table, and what building it added; his rule 3). Alone, the mod keeps them in
    /// BepInEx\config\modprojects.usecase.cfg, General and one section per part; inside miikeskii's QoL Pack, in the
    /// bundle's file, "Use Case" and "Use Case - &lt;part&gt;". A configuration manager's window (F1) lists them all under
    /// the one heading, each part behind a title row that opens and closes it (Folds; his look at F1, 2026-09-28). Every
    /// one takes effect at once: the panel's words are made again whenever one changes.
    /// </summary>
    internal static class Settings
    {
        internal const string WherePart = "Where it shows";
        internal const string WhatPart = "What it lists";
        internal const string NumbersPart = "Numbers";
        internal const string OrderPart = "Order and headings";
        internal const string SpoilersPart = "Spoilers";
        internal const string IgnorePart = "Ignore lists";
        internal const string LookPart = "Look";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // General
        internal static ConfigEntry<bool> ShowPanel { get; private set; }
        internal static ConfigEntry<ShowWhen> When { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> PinKey { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> IgnoreKey { get; private set; }

        // Where it shows
        internal static ConfigEntry<bool> InInventory { get; private set; }
        internal static ConfigEntry<bool> InTrader { get; private set; }
        internal static ConfigEntry<bool> InCraftingCosts { get; private set; }
        internal static ConfigEntry<bool> InBuildCosts { get; private set; }

        // What it lists: one switch per kind, and the rules that cut across them
        internal static readonly Dictionary<UseKind, ConfigEntry<bool>> Kinds = new Dictionary<UseKind, ConfigEntry<bool>>();
        internal static ConfigEntry<bool> AnythingRules { get; private set; }
        internal static ConfigEntry<bool> SelfUses { get; private set; }
        internal static ConfigEntry<KeptLitPieces> KeptLit { get; private set; }
        internal static ConfigEntry<OtherMods> OtherMods { get; private set; }

        // Numbers
        internal static ConfigEntry<bool> Amounts { get; private set; }
        internal static ConfigEntry<UpgradeNumber> Upgrade { get; private set; }
        internal static ConfigEntry<ProcessingNumber> Processing { get; private set; }
        internal static ConfigEntry<bool> Makes { get; private set; }
        internal static ConfigEntry<bool> Station { get; private set; }

        // Order and headings
        internal static ConfigEntry<bool> SplitBuildingByTool { get; private set; }
        internal static ConfigEntry<bool> Headings { get; private set; }
        internal static ConfigEntry<string> SectionOrder { get; private set; }
        internal static ConfigEntry<InsideOrder> Inside { get; private set; }
        internal static ConfigEntry<string> Title { get; private set; }
        internal static ConfigEntry<string> Bullet { get; private set; }

        // Spoilers
        internal static ConfigEntry<bool> HideUnknown { get; private set; }
        internal static ConfigEntry<UnknownLine> Unknown { get; private set; }
        internal static ConfigEntry<PlacesUnknownUntil> Places { get; private set; }
        internal static ConfigEntry<bool> HideTamingAndBait { get; private set; }
        internal static ConfigEntry<bool> EverythingWhenTheGameShowsAll { get; private set; }
        internal static ConfigEntry<OutOfSeason> Seasonal { get; private set; }

        // Ignore lists
        internal static ConfigEntry<string> NeverList { get; private set; }
        internal static ConfigEntry<string> NeverShowFor { get; private set; }

        // Look
        internal static ConfigEntry<PanelSide> Side { get; private set; }
        internal static ConfigEntry<LongLists> Long { get; private set; }
        internal static ConfigEntry<int> Cap { get; private set; }
        internal static ConfigEntry<int> MostLinesPerColumn { get; private set; }
        internal static ConfigEntry<int> TextSize { get; private set; }
        internal static ConfigEntry<float> Opacity { get; private set; }

        /// <summary>Goes up whenever any setting changes, so the panel's words are made again.</summary>
        internal static int Version { get; private set; }

        private static ModuleSettings bound;
        private static int order;
        private static ConfigEntry<bool> foldUnfolded;
        private static List<ConfigEntryBase> foldSettings;

        internal static void Bind(ModuleSettings settings)
        {
            bound = settings;
            Enabled = settings.Enabled;
            order = 990;
            string general = ModuleSettings.General;

            // On out of the box since card A63 (2026-10-06, his own settings made the pack's defaults; off before, MODULES.md
            // rule 4), and Selling below too: a file still at the old default moves once (the kit's MovedDefault).
            ShowPanel = Add(general, "ShowPanel", true, "Show the panel",
                "On (the default): beside the game's item tooltip, a panel lists everything the item is used in: building, " +
                "crafting, upgrading, refueling, and the game's other uses, each with its \"· X\". Off: the game's " +
                "tooltips as they are.");
            settings.MovedDefault(ShowPanel, false, 1);
            When = Add(general, "When", ShowWhen.OnHover, "When the panel shows",
                "OnHover: with the game's tooltip, as it shows. WhileKeyHeld: only while the key below is held. " +
                "ToggledByKey: the key below switches it on and off. With no key set, both key choices show it on hover.");
            Key = Add(general, "Key", KeyboardShortcut.Empty, "The key",
                "The key for the two key choices above, and for unfolding sections when long lists fold (Look). Left Alt is " +
                "best avoided: StoreStash and other mods use it over items.");
            PinKey = Add(general, "PinKey", KeyboardShortcut.Empty, "Pin key",
                "Pressed while the panel shows, it stays where it is after the pointer moves on, to read a long list; " +
                "pressed again, or once the window it was pinned in closes (the inventory, the trader; for the build panel's " +
                "costs, once you stop building), it goes. None: no pinning.");
            IgnoreKey = Add(general, "IgnoreKey", KeyboardShortcut.Empty, "Key to stop listing an item's uses",
                "Pressed while the panel shows, it adds the item to \"Never show a list for these resources\" (Ignore " +
                "lists), and the panel goes at once. None: no key.");

            BeginFold(WherePart);
            InInventory = Add(WherePart, "Inventory", true, "In the inventory and chests",
                "Beside the tooltip of items in the inventory, in chests, carts and ships, and in other mods' slots that " +
                "are the game's own inventory cells.");
            InTrader = Add(WherePart, "Trader", true, "In the trader's list",
                "Beside the tooltip of what Haldor, Hildir and the Bog Witch sell.");
            InCraftingCosts = Add(WherePart, "CraftingCosts", true, "On the crafting panel's costs",
                "Beside the small tooltip of each cost icon of the recipe picked at a workbench or other station.");
            InBuildCosts = Add(WherePart, "BuildCosts", true, "On the build panel's costs",
                "Beside the small tooltip of each cost icon of the piece picked with the hammer, where the game shows one.");
            EndFold();

            BeginFold(WhatPart);
            BindKind(UseKind.Building, true, "Things built with the hammer, the cultivator and the hoe.");
            BindKind(UseKind.Crafting, true, "Things crafted at a station or by hand.");
            BindKind(UseKind.Upgrading, true, "Gear upgraded with it.");
            BindKind(UseKind.Refueling, true, "Pieces that burn it, with the most each holds (a sconce · 6).");
            BindKind(UseKind.Processing, true, "Stations that turn it into something else (smelting, cooking, fermenting, milling, spinning, refining, the Obliterator's own list), with how many the station holds.");
            BindKind(UseKind.Summoning, true, "Boss altars it summons a boss at, with how many a summon takes.");
            BindKind(UseKind.LockedDoors, true, "Dungeon doors it unlocks (the Swamp Key, the Sealbreaker, the Intricate Key).");
            BindKind(UseKind.Taming, true, "Creatures that eat it while being tamed, and the saddles.");
            BindKind(UseKind.FishingBait, true, "Fish that bite on it.");
            BindKind(UseKind.Ammo, true, "The ballista's missiles and the catapult's payloads, with the most each holds.");
            BindKind(UseKind.Fireworks, true, "Fires it can be launched from as a firework (campfire, iron fire pit, bonfire).");
            BindKind(UseKind.Buying, true, "For coins: what the traders sell for them, with the price.");
            ConfigEntry<bool> selling = BindKind(UseKind.Selling, true, "What Haldor pays for it. On out of the box, so nearly " +
                "everything gets this line.");
            settings.MovedDefault(selling, false, 1);
            BindKind(UseKind.Traders, true, "Hildir's chests, which she takes back.");
            BindKind(UseKind.GuardianStones, true, "The boss trophies hung at the guardian stones where the world begins.");
            BindKind(UseKind.BallistaTargets, false, "Trophies that set the ballista's target. Off out of the box: it would put a line on every trophy.");
            AnythingRules = Add(WhatPart, "AnythingRules", false, "The Obliterator's and the catapult's \"anything\"",
                "The Obliterator turns any item it has no rule for into coal, and the catapult can throw anything that fits an " +
                "item stand. Off out of the box: either would fill every list (and your call on coal of 2026-09-26 leaves the " +
                "Obliterator out of how coal is made).");
            SelfUses = Add(WhatPart, "SelfUses", false, "Things that use themselves",
                "The serving tray's food and feasts, which cost the food they set down, and the hoe's placeable stone.");
            KeptLit = Add(WhatPart, "KeptLit", KeptLitPieces.ShownAsTheGameHasThem, "Pieces TimedTorchesStayLit keeps lit",
                "ShownAsTheGameHasThem: the sconces, torches, braziers and Jack-o-turnip it keeps lit stay under Refueling, " +
                "as a friend without that mod needs them. LeftOutOfRefueling: while it keeps them lit on this computer, they " +
                "aren't listed there.");
            OtherMods = Add(WhatPart, "OtherMods", UseCase.OtherMods.Shown, "Things other mods add",
                "Shown: listed as the game's own. MarkedWithTheirMod: the mod's name after the line. Hidden: left out. Which " +
                "mod added a thing is known for mods built with Jötunn.");
            EndFold();

            BeginFold(NumbersPart);
            Amounts = Add(NumbersPart, "Amounts", true, "Show \"· X\"",
                "The amount after each line: for one build or craft, the most a piece holds, a summon's count, a price.");
            Upgrade = Add(NumbersPart, "Upgrade", UpgradeNumber.PerLevel, "Upgrading's number",
                "PerLevel: \"· 2 per level\", the recipe's amount per level. EachLevel: \"· 2 / 4\", what each level takes, " +
                "from level 2 up (the game raises it at higher levels).");
            Processing = Add(NumbersPart, "Processing", ProcessingNumber.StationHolds, "Processing's number",
                "StationHolds: how many the station holds at once (Smelter · 10), as the rule for fuel. PerProduct: how many " +
                "one product takes.");
            Makes = Add(NumbersPart, "Makes", false, "Say how many a craft makes",
                "\"(makes 20)\" after a recipe or station that makes more than one at a time.");
            Station = Add(NumbersPart, "Station", false, "Say the station",
                "The station a recipe or piece needs, after its line (\"Forge\").");
            EndFold();

            BeginFold(OrderPart);
            SplitBuildingByTool = Add(OrderPart, "BuildingByTool", false, "Building split by tool",
                "On: one section per tool (Hammer, Cultivator, Hoe) in place of one Building section.");
            Headings = Add(OrderPart, "Headings", true, "Section headings", "Each section's heading above its lines.");
            SectionOrder = Add(OrderPart, "SectionOrder", Lines.DefaultOrderLine, "Section order",
                "The sections' order, as names separated by commas. Names left out go last, in this default order; names it " +
                "doesn't know are ignored. Case and spaces don't matter.");
            Inside = Add(OrderPart, "Inside", InsideOrder.Progression, "Order inside a section",
                "Progression: Meadows things first, as Add to Cart lists them (the pack's order). GameMenus: as the game's " +
                "own menus list them. AToZ: by name.");
            Title = Add(OrderPart, "Title", "Used In", "Title", "The panel's first line. Empty: no title.");
            Bullet = Add(OrderPart, "Bullet", "•", "Bullet", "What starts each line. Empty: nothing.");
            EndFold();

            BeginFold(SpoilersPart);
            HideUnknown = Add(SpoilersPart, "HideUnknown", true, "Hide the names of unknown things",
                "On: a thing whose recipe you don't have yet reads \"???\" (a piece: until the game has taught it, which " +
                "needs its tool in your inventory; an altar or a locked door: below).");
            Unknown = Add(SpoilersPart, "Unknown", UnknownLine.HiddenWithAmount, "An unknown line reads",
                "HiddenWithAmount: \"??? · 6\". Hidden: \"???\". LeftOut: not listed. CountedAtTheEnd: one line, \"3 more you " +
                "haven't found\". Unknown lines always come last in their section.");
            Places = Add(SpoilersPart, "Places", PlacesUnknownUntil.Seen, "Altars and locked doors are unknown until",
                "Seen: until it has loaded near you, once (the mod remembers it for each character), or its boss is beaten. " +
                "BossBeaten: an altar until its boss is beaten, by you or in this world (a door, which has no boss, until " +
                "seen). NeverHidden: never hidden.");
            HideTamingAndBait = Add(SpoilersPart, "TamingAndBait", false, "Hide taming and bait until known",
                "The game keeps no \"known\" for creatures and fish. On: a creature reads \"???\" until you have held its " +
                "trophy (or else what it drops), a fish until you have held one.");
            EverythingWhenTheGameShowsAll = Add(SpoilersPart, "WhenTheGameShowsAll", true, "Show everything when the game does",
                "With the no-cost cheat, or in a world with every recipe or piece unlocked, the game lists everything, and so " +
                "does the panel.");
            Seasonal = Add(SpoilersPart, "Seasonal", OutOfSeason.ListedMarked, "Seasonal recipes and pieces out of season",
                "ListedMarked: listed with their season (\"Jack-o-turnip (Halloween)\"). Hidden: not listed to build or craft " +
                "while their season is off; a piece already built still shows under Refueling.");
            EndFold();

            BeginFold(IgnorePart);
            NeverList = Add(IgnorePart, "NeverList", string.Empty, "Never list these",
                "Pieces, recipes (by what they make), stations and creatures never to list, separated by commas. Each may be " +
                "its name as shown (\"Sconce\"), its prefab name (\"piece_walltorch\") or its $ token; case and spaces don't " +
                "matter.");
            NeverShowFor = Add(IgnorePart, "NeverShowFor", string.Empty, "Never show a list for these resources",
                "Items that never get a panel, written the same way (\"Wood\", \"Wood\" or \"$item_wood\"). The key to stop " +
                "listing an item's uses (General) adds to it.");
            EndFold();

            // The Pick buttons in F1 (card B03): both lists picked from pictures of the game's things, names read as Loose
            // reads them (the name shown, the prefab's name or the $ token) and new ones written by prefab name.
            PickerSettings.Bind(bound);
            Lists.Attach(NeverList, IgnoreList("Pieces, recipes (by what they make), stations and creatures never to list.",
                                               ThingKind.Items, ThingKind.Pieces, ThingKind.Creatures));
            Lists.Attach(NeverShowFor, IgnoreList("Items that never get a panel.", ThingKind.Items));

            BeginFold(LookPart);
            Side = Add(LookPart, "Side", PanelSide.Beside, "Side",
                "Beside: right of the tooltip, or left of it when there's no room. AlwaysRight, AlwaysLeft, Below: there, as " +
                "far as the screen allows.");
            Long = Add(LookPart, "LongLists", LongLists.Columns, "Long lists",
                "Columns: a list too long for the screen runs on in columns (Wood has 141 uses). CapPerSection: each section " +
                "shows its first lines, then \"and 57 more\". FoldedUntilKeyHeld: each section is its heading and a count " +
                "until the key (General) is held; with no key set, it runs in columns.");
            Cap = Add(LookPart, "Cap", 8, "Lines per section when capped",
                "For CapPerSection: how many lines each section shows.", new AcceptableValueRange<int>(1, 50));
            MostLinesPerColumn = Add(LookPart, "LinesPerColumn", 0, "Most lines per column",
                "0: as many as fit on the screen. Any other number: at most that many.", new AcceptableValueRange<int>(0, 100));
            TextSize = Add(LookPart, "TextSize", 0, "Text size",
                "0: the size of the game's tooltip text. Any other number: that size.", new AcceptableValueRange<int>(0, 40));
            Opacity = Add(LookPart, "Opacity", 1f, "Background opacity",
                "1: as opaque as the game's tooltip. Less: more see-through.", new AcceptableValueRange<float>(0f, 1f));
            EndFold();
        }

        /// <summary>The switch of one kind of use.</summary>
        internal static bool KindOn(UseKind kind) => Kinds.TryGetValue(kind, out ConfigEntry<bool> entry) && entry.Value;

        /// <summary>The settings that shape the words, as plain values.</summary>
        internal static View ReadView(Func<string, string> localize)
        {
            var view = new View
            {
                SelfUses = SelfUses.Value,
                AnythingRules = AnythingRules.Value,
                KeptLit = KeptLit.Value,
                OtherMods = OtherMods.Value,
                SplitBuildingByTool = SplitBuildingByTool.Value,
                Headings = Headings.Value,
                Order = Lines.OrderFrom(SectionOrder.Value),
                Inside = Inside.Value,
                Amounts = Amounts.Value,
                Upgrade = Upgrade.Value,
                Processing = Processing.Value,
                Makes = Makes.Value,
                Station = Station.Value,
                HideUnknown = HideUnknown.Value,
                Unknown = Unknown.Value,
                Places = Places.Value,
                HideTamingAndBait = HideTamingAndBait.Value,
                EverythingWhenTheGameShowsAll = EverythingWhenTheGameShowsAll.Value,
                OutOfSeason = Seasonal.Value,
                Bullet = (Bullet.Value ?? string.Empty).Trim(),
                Title = (Title.Value ?? string.Empty).Trim(),
                NeverList = Loose.From(NeverList.Value),
                Long = Long.Value == LongLists.FoldedUntilKeyHeld && Key.Value.MainKey == KeyCode.None ? LongLists.Columns : Long.Value,
                Cap = Cap.Value,
                Localize = localize,
            };
            foreach (UseKind kind in Enum.GetValues(typeof(UseKind)))
            {
                view.KindOn[(int)kind] = KindOn(kind);
            }
            return view;
        }

        /// <summary>The key is held down now (its main key and every modifier), read through the game's input.</summary>
        internal static bool Held(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || !ZInput.GetKey(key.MainKey, false))
            {
                return false;
            }
            foreach (KeyCode modifier in key.Modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The key's main key went down this frame while every modifier is held.</summary>
        internal static bool Pressed(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || !ZInput.GetKeyDown(key.MainKey, false))
            {
                return false;
            }
            foreach (KeyCode modifier in key.Modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return true;
        }

        private static ConfigEntry<bool> BindKind(UseKind kind, bool on, string what)
        {
            string heading = Lines.Heading(kind);
            return Kinds[kind] = Add(WhatPart, kind.ToString(), on, heading, "List " + heading + ": " + what);
        }

        private static ListSpec IgnoreList(string says, params ThingKind[] kinds) => new ListSpec
        {
            Module = Module.Name,
            Kinds = kinds,
            Format = new ListFormat { Separators = ",;\n\r", Joiner = ", ", Normal = Loose.Normal },
            ShownNames = true,
            Says = says,
            Warn = text => Module.Log?.LogWarning(text),
        };

        private static ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, order--, description, range);
            entry.SettingChanged += (sender, args) => Version++;
            foldSettings?.Add(entry);
            return entry;
        }

        // Each part after General is a fold-out: a title row, then its settings, all under the module's one heading. The
        // title row is a setting of the part's own ("Unfolded"), which remembers whether it is open.
        private static void BeginFold(string part)
        {
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, part, order--,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(part, settings));
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
