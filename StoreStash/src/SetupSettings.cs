using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>What pasting does to a chest that already has a setup.</summary>
    internal enum PasteMode
    {
        /// <summary>The pasted setup replaces the chest's.</summary>
        Replace,

        /// <summary>The pasted setup's picks, excepts and biomes are added to the chest's.</summary>
        Add,
    }

    /// <summary>
    /// The settings of chest setups (card A41, runbook job 24), StoreStash's section "Chest setups": in the single release
    /// a section of its own in modprojects.storestash.cfg, in the bundle part of the one "StoreStash" section, listed in F1
    /// under StoreStash's heading behind its fold row (500), its switch open under it (499), then its groups, each row right
    /// above its settings (card A41's second round, decision 22): The setup window (490; 489 to 484), Copy and paste (480;
    /// 479 to 475), What set-up chests take (470; 469 to 465), What you're told (460; 459 to 457). Keys prefixed Setup. Which set-up chest comes
    /// first is the quick stack's "Which chest first" (Settings, since 0.5.0; 0.4.0's three switches were carried into it
    /// once). Out of the box a chest with no setup is quick stacked into exactly as before; a chest someone sets up follows
    /// its setup for every player on the pack. The button in chest windows and "Say where things went" are on out of the
    /// box since card A63 (2026-10-06, his own settings made the pack's defaults; off before, MODULES.md rule 4), and a file
    /// still at the old default moves once (the kit's MovedDefault). Every setting takes effect at once.
    /// </summary>
    internal static class SetupSettings
    {
        internal const string Part = "Chest setups";

        internal static ConfigEntry<bool> On { get; private set; }

        internal static ConfigEntry<bool> ShowButton { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<bool> CopyButtons { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> CopyKey { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PasteKey { get; private set; }

        internal static ConfigEntry<PasteMode> Paste { get; private set; }

        internal static ConfigEntry<bool> SystemClipboard { get; private set; }

        internal static ConfigEntry<bool> KeepHeld { get; private set; }

        internal static ConfigEntry<bool> Overflow { get; private set; }

        internal static ConfigEntry<bool> Others { get; private set; }

        internal static ConfigEntry<bool> ByHand { get; private set; }

        internal static ConfigEntry<bool> HoverLine { get; private set; }

        internal static ConfigEntry<bool> KeyHint { get; private set; }

        internal static ConfigEntry<bool> Vehicles { get; private set; }

        internal static ConfigEntry<bool> HideUnknown { get; private set; }

        internal static ConfigEntry<bool> SayWhere { get; private set; }

        internal static ConfigEntry<bool> Rename { get; private set; }

        internal static ConfigEntry<bool> QuickPicks { get; private set; }

        /// <summary>True while StoreStash runs, is switched on, and chest setups are on.</summary>
        internal static bool Active => Module.IsOn && On != null && On.Value;

        internal static void Bind(ModuleSettings settings)
        {
            On = settings.Bind(Part, "SetupOn", true,
                "Chest setups", 499,
                "On: a chest can be set up to take only the kinds of things you pick (families such as Hides and Pelts, single " +
                "items, biomes), and quick stack follows it. Off: quick stack ignores every setup, and the setup window, its " +
                "button and keys and the hover line are gone. Setups stay in the chests either way.");

            // The setup window.
            Key = settings.Bind(Part, "SetupKey", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Setup key", 489,
                "Pointing at a chest: opens it, with its setup window beside it. With a chest's window open: opens or closes " +
                "its setup window.");

            ShowButton = settings.Bind(Part, "SetupButton", true,
                "Setup button in the chest window", 488,
                "A button beside an open chest's window, under Reserve Stock's padlock, that opens its setup window; on by " +
                "default. The setup key opens it too.");
            settings.MovedDefault(ShowButton, false, 1);

            ButtonOffset = settings.Bind(Part, "SetupButtonOffset", Vector2.zero,
                "Setup button position (offset)", 487,
                "Moves the button from its place right of Place stacks, in the chest window's own units: x to the right, y up. " +
                "For when another mod puts something there.");

            Rename = settings.Bind(Part, "SetupRename", true,
                "Rename the chest in the setup window", 486,
                "On: the chest's name in the setup window's title can be clicked and typed over, to rename the chest (kept in " +
                "the chest, where MyLittleUI keeps chest names, up to 32 characters; an empty name, or the chest's kind, clears " +
                "it). Off: the name only shows.");

            HideUnknown = settings.Bind(Part, "SetupHideUnknown", true,
                "Hide what you haven't found yet", 485,
                "On: the setup window shows only items this character has found, as the game's build menu does. A family you " +
                "pick still takes the items in it you find later.");

            // Card A81 (44-B): the quick picks, last in "The setup window".
            QuickPicks = settings.Bind(Part, "SetupQuickPicks", true,
                "Quick picks on a family's page", 484,
                "On: some families' pages in the setup window start with buttons for their natural groups, each taken in one " +
                "click and named so in what the chest takes: Metals' Metal Ingots and Metal Ores and Scrap; Food's Prepared " +
                "Dishes, Cooked Meat and Fish, Raw Meat and Fish, and Health, Stamina, Eitr and Balanced Foods (as the game " +
                "colours their mark); Meads and Potions' Finished Meads. Off: they're hidden, but one a chest takes or leaves " +
                "out still shows (and in \"This chest's setup\"), to take it out again; chests set up with them keep working.");

            // Copy and paste.
            CopyButtons = settings.Bind(Part, "SetupCopyButtons", true,
                "Copy and Paste buttons", 479,
                "Copy and Paste buttons in the setup window, to give another chest the same setup.");

            CopyKey = settings.Bind(Part, "SetupCopyKey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl),
                "Copy key", 478,
                "With a chest's window open: copies its setup. Never while you type in a text box. Not while pointing at a " +
                "chest in the world, where Left Ctrl crouches and C walks.");

            PasteKey = settings.Bind(Part, "SetupPasteKey", new KeyboardShortcut(KeyCode.V, KeyCode.LeftControl),
                "Paste key", 477,
                "With a chest's window open: gives it the setup you copied. Never while you type in a text box. Not while " +
                "pointing at a chest in the world, where V picks up and uses a quick slot.");

            Paste = settings.Bind(Part, "SetupPaste", PasteMode.Replace,
                "Pasting over a setup", 476,
                "Replace: the chest's setup becomes the one you copied. Add: the copy's picks, left-out things and biomes join the " +
                "chest's own, so its biomes narrow the chest's families too, and what it leaves out is left out of the chest too " +
                "(unless the chest picked that very thing).");

            SystemClipboard = settings.Bind(Part, "SetupSystemClipboard", false,
                "Copy to the clipboard too", 475,
                "On: Copy also puts the setup on your computer's clipboard as a line of text, to send to a friend, and Paste " +
                "reads a setup from the clipboard when you haven't copied one in the game. Off: your clipboard is left alone.");

            // What set-up chests take.
            KeepHeld = settings.Bind(Part, "SetupKeepHeld", false,
                "A set-up chest also takes what it holds", 469,
                "Off: a set-up chest takes only what its setup takes, even if something else is already inside it. On: it also " +
                "takes more of what it already holds, as a chest without a setup does (after the set-up chests meant for it, " +
                "while \"Which chest first\" puts those first).");

            Overflow = settings.Bind(Part, "SetupOverflow", true,
                "What set-up chests can't fit goes on", 468,
                "On: what the set-up chests that take an item can't fit goes on to other chests, by the quick stack's own " +
                "rules (chests that already hold it). Off: an item a set-up chest nearby takes goes into set-up chests only, " +
                "and what they can't fit stays with you.");

            Others = settings.Bind(Part, "SetupOthers", true,
                "What no setup takes goes to other chests", 467,
                "On: things no set-up chest nearby takes go to chests without a setup, as quick stack always did. Off: quick " +
                "stack only puts things into set-up chests.");

            ByHand = settings.Bind(Part, "SetupByHand", true,
                "Place stacks and holding E follow a setup", 466,
                "On: Place stacks and holding E at a set-up chest put in what its setup takes (even if it holds none yet), and " +
                "nothing else. Off: they put in only what the chest already holds, as they do at any chest.");

            Vehicles = settings.Bind(Part, "SetupVehicles", true,
                "Carts' and ships' chests can be set up", 465,
                "On: a cart's or ship's chest can have a setup (it's kept with the cart or ship) and quick stack follows it. " +
                "Off: their setups are ignored and can't be changed.");

            // What you're told.
            HoverLine = settings.Bind(Part, "SetupHoverLine", true,
                "Say what a set-up chest takes", 459,
                "A line under a set-up chest's name when you point at it, saying what it takes.");

            KeyHint = settings.Bind(Part, "SetupKeyHint", false,
                "Setup key hint on every chest", 458,
                "A line with the setup key under every chest you could set up, as the game shows its own keys.");

            SayWhere = settings.Bind(Part, "SetupSayWhere", true,
                "Say where things went", 457,
                "On by default. After a quick stack, what each set-up chest took by its setup, in place of the usual " +
                "line: \"Into set-up " +
                "chests: 12 Wolf Pelt, 3 Deer Hide → Pelt chest\" (a chest by its name, else by what it takes; several, a line " +
                "each). What went into other chests is the first line, \"15 items into 2 other chests\". In one message, where " +
                "\"Message after a quick stack\" puts it (Quick stack); with that off, top left.");
            settings.MovedDefault(SayWhere, false, 1);

            Folds.Fold setups = Folds.Add(settings, Part, "SetupUnfolded", Part, 500, new ConfigEntryBase[]
            {
                On,
            });
            Folds.AddGroup(settings, Part, "SetupWindowUnfolded", "The setup window", 490, new ConfigEntryBase[]
            {
                Key, ShowButton, ButtonOffset, Rename, HideUnknown, QuickPicks,
            }, setups);
            Folds.AddGroup(settings, Part, "SetupCopyUnfolded", "Copy and paste", 480, new ConfigEntryBase[]
            {
                CopyButtons, CopyKey, PasteKey, Paste, SystemClipboard,
            }, setups);
            Folds.AddGroup(settings, Part, "SetupTakesUnfolded", "What set-up chests take", 470, new ConfigEntryBase[]
            {
                KeepHeld, Overflow, Others, ByHand, Vehicles,
            }, setups);
            Folds.AddGroup(settings, Part, "SetupToldUnfolded", "What you're told", 460, new ConfigEntryBase[]
            {
                HoverLine, KeyHint, SayWhere,
            }, setups);

            // Anything that shows a setup follows a change at once.
            On.SettingChanged += (sender, args) => SetupWindow.SettingsChanged();
            HideUnknown.SettingChanged += (sender, args) => SetupWindow.SettingsChanged();
            QuickPicks.SettingChanged += (sender, args) => SetupWindow.SettingsChanged();
            CopyButtons.SettingChanged += (sender, args) => SetupWindow.SettingsChanged();
            Vehicles.SettingChanged += (sender, args) => SetupWindow.SettingsChanged();
        }

        /// <summary>
        /// How a quick stack follows the chests' setups, as the settings say now; null while setups are off (or, for Place
        /// stacks and holding E, while they don't follow setups), so the quick stack routes as before them. By hand at one
        /// chest, what other chests would get doesn't apply. Which set-up chest comes first is the quick stack's own "Which
        /// chest first" (Settings.Rules: StackRules.Order).
        /// </summary>
        internal static SetupRules Routing(bool byHand)
        {
            if (!Active || (byHand && !ByHand.Value))
            {
                return null;
            }
            return new SetupRules
            {
                Of = Setups.Reading,
                KeepHeld = KeepHeld.Value,
                Overflow = byHand || Overflow.Value,
                Others = byHand || Others.Value,
            };
        }
    }
}
