using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.Kit;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>Which chests the mod may use.</summary>
    internal enum WhichChests
    {
        AnyChestICanOpen,
        OnlyChestsIBuilt,
    }

    /// <summary>How chests reserved with Reserve Stock count (card A12, his answer 5): left out with a padlock showing how
    /// many are reserved, left out quietly, or used like any other chest.</summary>
    internal enum ReservedChests
    {
        LeftOutWithLock,
        LeftOut,
        UsedAsUsual,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.craftfromchests.cfg, in the sections
    /// General, Crafting and Stations; inside miikeskii's QoL Pack, in the bundle's file, in the section "Craft from
    /// Chests". Every one takes effect at once: the mod reads them each time it counts or takes. The defaults are his
    /// AzuCraftyBoxes settings of 2026-09-27 (150 m, leave one, Left Shift to fill, Left Alt + O to switch) and its default
    /// rules file (personal chests left out, the kiln taking only Wood).
    /// </summary>
    internal static class Settings
    {
        private const string Crafting = "Crafting";

        private const string Stations = "Stations";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> ToggleKey { get; private set; }

        internal static ConfigEntry<float> Range { get; private set; }

        internal static ConfigEntry<WhichChests> Chests { get; private set; }

        internal static ConfigEntry<bool> LeaveOne { get; private set; }

        internal static ConfigEntry<bool> PersonalChests { get; private set; }

        internal static ConfigEntry<ReservedChests> Reserved { get; private set; }

        internal static ConfigEntry<bool> PersonalFirst { get; private set; }

        internal static ConfigEntry<bool> CraftFromChests { get; private set; }

        internal static ConfigEntry<bool> BuildFromChests { get; private set; }

        internal static ConfigEntry<bool> ShowAmounts { get; private set; }

        internal static ConfigEntry<string> AmountFormat { get; private set; }

        internal static ConfigEntry<Color> FromChestsColor { get; private set; }

        internal static ConfigEntry<bool> ShowBuildCount { get; private set; }

        internal static ConfigEntry<Color> CanBuildColor { get; private set; }

        internal static ConfigEntry<Color> CannotBuildColor { get; private set; }

        internal static ConfigEntry<bool> FillStations { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> FillKey { get; private set; }

        internal static ConfigEntry<string> KilnTakes { get; private set; }

        internal static ConfigEntry<bool> StationHint { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first. Keys never change once released; new ones get new keys.
            Enabled = settings.Enabled;

            // The chest settings every mod of the pack shares (card A69): the range, "Chests beyond the server's reach"
            // (which was this mod's own setting since card A64) and the log lines.
            ChestSettings.Bind(settings);

            ToggleKey = settings.Bind(ModuleSettings.General, "ToggleKey", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt),
                "Key to switch it on and off", 40,
                "Switches the mod on and off (the Enabled setting above) and says which, as AzuCraftyBoxes' Left Alt + O " +
                "did. Off, everything uses only what you carry.");

            // Since card A69 (2026-10-08): 0 follows the shared range ("Chests (every mod)"), the default now; a file still
            // at the old default, 150, moves to it once (ChestSettings).
            Range = settings.Bind(ModuleSettings.General, "Range", 0f,
                "Chest range (metres)", 39,
                "How far from you a chest may be for crafting, building and stations to count and take from it. " +
                ChestSettings.OwnRangeNote,
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            settings.MovedDefault(Range, ChestSettings.DefaultRange, 2);

            Chests = settings.Bind(ModuleSettings.General, "Chests", WhichChests.AnyChestICanOpen,
                "Which chests", 38,
                "AnyChestICanOpen: every chest you could open by hand (a friend's public chest too, never a private one " +
                "or one behind a ward that isn't yours). OnlyChestsIBuilt: only chests, carts and ships you built. " +
                "Chests the world made itself, and gravestones, are never used.");

            LeaveOne = settings.Bind(ModuleSettings.General, "LeaveOne", true,
                "Leave one of each item in chests", 37,
                "Leaves one of each item in every chest, so quick stack still knows which chest it belongs in, as " +
                "AzuCraftyBoxes' \"Leave One Item\" did. Only for items that stack: a lone sword or piece of armour can " +
                "still be used.");

            // On out of the box since card A63 (2026-10-06, his own settings made the pack's defaults); a file still at the
            // old default moves once (the kit's MovedDefault).
            PersonalChests = settings.Bind(ModuleSettings.General, "PersonalChests", true,
                "Use my personal chests", 36,
                "On (the default): your own personal chests are used like any other chest. Off: they're left alone, as " +
                "AzuCraftyBoxes' default rules did, so they can keep what you don't want used. Other players' personal " +
                "chests are never used.");
            settings.MovedDefault(PersonalChests, false, 1);

            Reserved = settings.Bind(ModuleSettings.General, "ReservedChests", ReservedChests.LeftOutWithLock,
                "Reserved chests", 35,
                "Chests reserved with Reserve Stock (in miikeskii's QoL Pack; a friend's reserve counts too). " +
                "LeftOutWithLock: never used, and an amount in the menus shows a small padlock with how many more the " +
                "reserved chests nearby hold. LeftOut: never used, and nothing shown. UsedAsUsual: crafting, building and " +
                "stations use them like any other chest (quick stack and Add to Cart still leave them alone).");

            // On out of the box (card A80, his word: "prioritizing resources from player's personal chests"): only which chest
            // gives a material changes, never what can be made or what the menus show.
            PersonalFirst = settings.Bind(ModuleSettings.General, "PersonalFirst", true,
                "My personal chests first", 34,
                "On: crafting, building and filling stations take what they need from your personal chests first, and from " +
                "the other chests nearby only for the rest, nearest first. Your personal chests are the game's Personal " +
                "Chests you built and the chests you marked as yours (with StoreStash in miikeskii's QoL Pack, as its \"Which " +
                "chests are yours\" says). Off: nearest first, whoever's chest it is. Only which chest gives it changes: what " +
                "you can make, and the amounts the menus show, stay the same. A chest someone else has open still comes last.");

            CraftFromChests = settings.Bind(Crafting, "Crafting", true,
                "Craft from chests", 30,
                "Crafting and upgrading, at a station or by hand, uses what the chests nearby hold. What you carry goes " +
                "first; the rest comes from the nearest chests as the crafting bar ends.");

            BuildFromChests = settings.Bind(Crafting, "Building", true,
                "Build from chests", 29,
                "Placing a piece with the hammer, hoe or cultivator uses what the chests nearby hold. What you carry goes " +
                "first. The chests holding the rest are asked for as soon as you pick the piece, so placing isn't " +
                "delayed; if one of them wasn't yours yet, the click says so and the next one places it.");

            ShowAmounts = settings.Bind(Crafting, "ShowAmounts", true,
                "Show how much you have", 28,
                "The requirement lists of the crafting menu and the build menu show how much you have, counting the " +
                "chests, beside what's needed, such as 57/10. Off: only what's needed, as without the mod.");

            AmountFormat = settings.Bind(Crafting, "AmountFormat", "{0}/{1}",
                "Amount format", 27,
                "{0} is how much you have, counting the chests, and {1} what's needed. Big numbers show as 1.2K and 3.4M.");

            FromChestsColor = settings.Bind(Crafting, "FromChestsColor", new Color(1f, 0.922f, 0.016f, 1f),
                "Colour when chests make up the rest", 26,
                "An amount you'd be short of without the chests flashes between this colour and white, as " +
                "AzuCraftyBoxes' amounts did. Short even with the chests, it flashes red, as the game's own does.");

            ShowBuildCount = settings.Bind(Crafting, "ShowBuildCount", true,
                "Show how many you can build", 25,
                "The name of the piece you're about to place shows how many of it you could place, counting the chests, " +
                "such as Wood wall (38); free pieces show ∞.");

            CanBuildColor = settings.Bind(Crafting, "CanBuildColor", Color.green,
                "Colour: can build", 24,
                "The colour of that number while you can place at least one.");

            CannotBuildColor = settings.Bind(Crafting, "CannotBuildColor", Color.red,
                "Colour: can't build", 23,
                "The colour of that number when you can't place any.");

            FillStations = settings.Bind(Stations, "Stations", true,
                "Fill stations from chests", 20,
                "Smelters, the charcoal kiln, blast furnace, spinning wheel, windmill and eitr refinery, fires and " +
                "torches, the cooking stations and oven, the fermenter, the shield generator and the ballista take from " +
                "the chests nearby when you press E with none of what they need on you: one at a time, or as many as fit " +
                "with the fill key held.");

            FillKey = settings.Bind(Stations, "FillKey", new KeyboardShortcut(KeyCode.LeftShift),
                "Hold to fill", 19,
                "Hold it while pressing E on a station to fill it as far as it goes: first from what you carry, then from " +
                "the chests.");

            KilnTakes = settings.Bind(Stations, "KilnTakes", "Wood",
                "What the charcoal kiln takes from chests", 18,
                "The woods the charcoal kiln may take from chests, and be filled with, by their prefab names, separated by " +
                "commas. Wood alone by default, as in AzuCraftyBoxes' default rules, so Fine wood, Core wood and the rest " +
                "are kept for building. Empty: every wood it burns. Pressing E with a wood in hand still adds it, as in " +
                "the game.");

            StationHint = settings.Bind(Stations, "StationHint", true,
                "Show the fill hint", 17,
                "Adds a line to a station's hover text naming the fill key.");
        }

        /// <summary>The chest rules as the settings say right now.</summary>
        internal static ChestRules Rules() => new ChestRules
        {
            Range = ChestSettings.RangeOf(Range),
            OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
            LeaveOne = LeaveOne.Value,
            Skip = PersonalChests.Value ? null : SkipPersonal,
            UseReserved = Reserved.Value == ReservedChests.UsedAsUsual,
        };

        // His own personal chests (the game's CheckAccess already keeps everyone else's out).
        private static readonly System.Func<Container, bool> SkipPersonal = chest => chest.m_privacy == Container.PrivacySetting.Private;

        /// <summary>The chests in the order a take uses them, as "My personal chests first" says now (card A80): his personal
        /// chests first (ChestMine.IsPersonal, the kinds StoreStash's "Which chests are yours" counts, ChestMine.Kinds), each
        /// half in the order given; with the setting off, the order given. One rule for planning and for taking, so both
        /// pick the same chests.</summary>
        internal static List<Container> InTakeOrder(IEnumerable<Container> chests)
        {
            if (PersonalFirst == null || !PersonalFirst.Value)
            {
                return new List<Container>(chests);   // a list of its own: the caller may add to it
            }
            long me = ChestClaims.PlayerId();
            PersonalKinds kinds = ChestMine.Kinds;
            return ChestMine.PersonalFirst(chests, chest => ChestMine.IsPersonal(chest, me, kinds));
        }

        /// <summary>The woods the charcoal kiln may take from chests, or null for every wood it burns.</summary>
        internal static string[] KilnWoods()
        {
            string[] names = KilnTakes.Value.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
            return names.Length > 0 ? names : null;
        }

        /// <summary>True in the frame the key goes down with its modifiers held, read through the game's own input (as Add to
        /// Cart reads its key). The caller checks that the game takes input: no chat, console or text box.</summary>
        internal static bool Pressed(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));

        /// <summary>True while the key and its modifiers are held.</summary>
        internal static bool Held(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKey(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
    }
}
