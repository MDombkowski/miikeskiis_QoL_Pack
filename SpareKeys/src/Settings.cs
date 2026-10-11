using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>How big the window is drawn, over the game's own GUI scale (Add to Cart's and Outfitter's sizes).</summary>
    internal enum WindowSize
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>What the window shows.</summary>
    internal enum ShowWhat
    {
        PictureAndList,
        Picture,
        List,
    }

    /// <summary>What the list on the left has picked when the window opens.</summary>
    internal enum OpensOn
    {
        Everything,
        FreeKeys,
        WhatIPickedLast,
    }

    /// <summary>What shows beside a "press a key" (in F1, in the game's Controls menu).</summary>
    internal enum PanelKind
    {
        Off,
        List,
        KeyboardPicture,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.sparekeys.cfg, a section per part;
    /// inside miikeskii's QoL Pack, in the bundle's file, all in the section "Spare Keys" (one heading in F1). Every one
    /// takes effect at once: the window and the panels read them each time they draw, and the map is worked out again
    /// when one that changes it changes. Keys are permanent from 0.1.0.
    /// </summary>
    internal static class Settings
    {
        internal const string WindowPart = "Window";
        internal const string CountsPart = "What counts";
        internal const string PickingPart = "While picking a key";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> FOneButton { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> OpenKey { get; private set; }

        internal static ConfigEntry<WindowSize> Size { get; private set; }

        internal static ConfigEntry<float> Opacity { get; private set; }

        internal static ConfigEntry<KeyboardKind> Keyboard { get; private set; }

        internal static ConfigEntry<bool> ShowMouse { get; private set; }

        internal static ConfigEntry<ShowWhat> Show { get; private set; }

        internal static ConfigEntry<OpensOn> Opens { get; private set; }

        internal static ConfigEntry<bool> Letters { get; private set; }

        internal static ConfigEntry<bool> Suggest { get; private set; }

        internal static ConfigEntry<Color> FreeColour { get; private set; }

        internal static ConfigEntry<Color> ModifierColour { get; private set; }

        internal static ConfigEntry<Color> UsedColour { get; private set; }

        internal static ConfigEntry<Color> SharedColour { get; private set; }

        internal static ConfigEntry<Color> ClashColour { get; private set; }

        internal static ConfigEntry<bool> ShowWhen { get; private set; }

        internal static ConfigEntry<SharedRule> SharedKeys { get; private set; }

        internal static ConfigEntry<UnknownRule> UnknownWhen { get; private set; }

        internal static ConfigEntry<string> WhensList { get; private set; }

        internal static ConfigEntry<Color> MaybeColour { get; private set; }

        internal static ConfigEntry<PlainRule> Plain { get; private set; }

        internal static ConfigEntry<CodeRule> Code { get; private set; }

        internal static ConfigEntry<bool> MenusTypingWindows { get; private set; }

        internal static ConfigEntry<DebugRule> Debug { get; private set; }

        internal static ConfigEntry<bool> ConsoleBinds { get; private set; }

        internal static ConfigEntry<bool> SwitchedOffModules { get; private set; }

        internal static ConfigEntry<bool> Gamepad { get; private set; }

        internal static ConfigEntry<bool> LogClashes { get; private set; }

        internal static ConfigEntry<PanelKind> F1Panel { get; private set; }

        internal static ConfigEntry<PanelKind> ControlsPanel { get; private set; }

        /// <summary>Goes up when the keys' colours or letters, or the suggestions, change: the pictures and the line under
        /// them are made again.</summary>
        internal static int LookVersion { get; private set; }

        internal static readonly Color DefaultFree = new Color(0.33f, 0.68f, 0.31f, 1f);
        internal static readonly Color DefaultModifier = new Color(0.3f, 0.52f, 0.85f, 1f);
        internal static readonly Color DefaultUsed = new Color(0.42f, 0.42f, 0.42f, 1f);
        internal static readonly Color DefaultShared = new Color(0.58f, 0.36f, 0.78f, 1f);
        internal static readonly Color DefaultClash = new Color(0.82f, 0.22f, 0.18f, 1f);
        internal static readonly Color DefaultMaybe = new Color(0.93f, 0.66f, 0.15f, 1f);

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first; orders are unique across the module, since in the bundle its parts share one
            // section.
            Enabled = settings.Enabled;

            FOneButton = settings.Bind(ModuleSettings.General, "FOneButton", true,
                "A button in F1", 99,
                "On: a \"Show the key map\" button here, at the top of Spare Keys' settings, opens the key map in a world (and " +
                "closes this window so you can see it). Off: no button; the key below still opens it.",
                null, F1Button.Draw);

            OpenKey = settings.Bind(ModuleSettings.General, "OpenKey", new KeyboardShortcut(KeyCode.Insert),
                "Key to open the key map", 98,
                "Opens and closes the key map, in a world: a picture of your keyboard and mouse with every key coloured by who uses it. " +
                "Insert is free in the game. Not set: only the button in F1 opens it. With Bindrune installed (it uses Insert " +
                "too), Insert is left to Bindrune and the log says so; pick another key here to use one of your own.");

            Size = settings.Bind(WindowPart, "WindowSize", WindowSize.Large,
                "Window size", 90,
                "How big the window is drawn, over the game's own GUI scale. Small: the game's scale. Medium: a quarter " +
                "bigger. Large (the default): half as big again. Kept to what fits your screen; the text stays sharp.");

            Opacity = settings.Bind(WindowPart, "WindowOpacity", 1f,
                "Window opacity", 89,
                "How solid the window's background is: 1 as solid as the game's own windows, less to see the game through " +
                "it. The keys and the text stay solid.",
                new AcceptableValueRange<float>(0.2f, 1f));

            Keyboard = settings.Bind(WindowPart, "Keyboard", KeyboardKind.Full,
                "Keyboard", 88,
                "Which keyboard the picture draws. Full: with the number pad. WithoutNumberPad: a keyboard without one. " +
                "Laptop: a compact one, Home, Page Up, Page Down and End in a column at the right, no Print Screen, Scroll " +
                "Lock or Pause (they are still listed when something uses them).");

            ShowMouse = settings.Bind(WindowPart, "ShowMouse", true,
                "Show the mouse", 87,
                "On: a mouse beside the keyboard, its buttons coloured the same way (Mouse3 and Mouse4 are the side " +
                "buttons).");

            Show = settings.Bind(WindowPart, "Show", ShowWhat.PictureAndList,
                "Show", 86,
                "PictureAndList: the keyboard picture, and under it a list of keys with the list to pick from on the left. " +
                "Picture: only the picture. List: only the list.");

            Opens = settings.Bind(WindowPart, "OpensOn", OpensOn.WhatIPickedLast,
                "Opens on", 85,
                "What the list on the left has picked when the window opens. WhatIPickedLast: as you left it (this game " +
                "session; Everything the first time).");

            Letters = settings.Bind(WindowPart, "LettersOnKeys", false,
                "Letters on keys (colour-blind)", 84,
                "On: every key carries a small letter as well as its colour: F free, M used only with a modifier, U used, " +
                "2 shared (two that never work at once), C a clash, ? maybe used or maybe a clash.");

            Suggest = settings.Bind(WindowPart, "SuggestFreeKeys", true,
                "Suggest nearby free keys", 83,
                "On: a key in use, under the pointer or in the list of clashes, names up to three free keys to use " +
                "instead: the same key with a modifier where that is free, then the free keys nearest it.");

            ShowWhen = settings.Bind(WindowPart, "ShowWhenOnHover", true,
                "Show the when on hover", 82,
                "On: the line about a key under the pointer says when each of its users works (\"pointing at a chest\", " +
                "\"the hammer in hand\", \"the inventory open\"), and for a clash or a shared key why. Off: only who uses it.");

            FreeColour = settings.Bind(WindowPart, "FreeColour", DefaultFree,
                "Colour: free", 81, "The colour of a key nothing uses.");
            ModifierColour = settings.Bind(WindowPart, "ModifierColour", DefaultModifier,
                "Colour: used only with a modifier", 80,
                "The colour of a key that is free with the modifiers shown, but used with others (its corner says which: " +
                "C Ctrl, A Alt, S Shift).");
            UsedColour = settings.Bind(WindowPart, "UsedColour", DefaultUsed,
                "Colour: used", 79, "The colour of a key in use.");
            SharedColour = settings.Bind(WindowPart, "SharedColour", DefaultShared,
                "Colour: shared", 78,
                "The colour of a key two or more things use, but never at the same moment: each works only somewhere the " +
                "others never do (pointing at a chest, pointing at an armour stand), so one press never does both. Beside F1, " +
                "while you pick a key for a setting: a key used, but never where that setting works, so free for it.");
            ClashColour = settings.Bind(WindowPart, "ClashColour", DefaultClash,
                "Colour: clash", 77, "The colour of a key two or more things use at once.");
            MaybeColour = settings.Bind(WindowPart, "MaybeColour", DefaultMaybe,
                "Colour: maybe used", 76,
                "The colour of a key that may be in use: found only in a mod's code (see \"Look inside mods' code\"), or a " +
                "mod's button with no setting behind it (often a key of its own window).");

            // The keys' look: a change repaints the pictures at once (the map itself is unchanged by it).
            Letters.SettingChanged += (sender, args) => LookVersion++;
            Suggest.SettingChanged += (sender, args) => LookVersion++;   // the line under the picture is written again
            ShowWhen.SettingChanged += (sender, args) => LookVersion++;
            foreach (ConfigEntry<Color> colour in new[] { FreeColour, ModifierColour, UsedColour, SharedColour, ClashColour, MaybeColour })
            {
                colour.SettingChanged += (sender, args) => LookVersion++;
            }

            Plain = settings.Bind(CountsPart, "PlainKeyUnderModifiers", PlainRule.Always,
                "A plain key counts as used under any modifier", 75,
                "Whether a key (or a combination) still counts as used when more modifiers are held. Always (the default, " +
                "safe): the game's own keys and many mods' fire whatever else is held, so Alt + Q still auto-runs. " +
                "GameAndLooseReadersOnly: only the game's keys, KeyCode settings, console binds and the pack's own keys; " +
                "other mods' shortcuts are taken to refuse extra keys, as BepInEx's own check does. Never: every key counts " +
                "only with exactly its own modifiers.");

            Code = settings.Bind(CountsPart, "LookInsideModsCode", CodeRule.MaybeUsed,
                "Look inside mods' code", 74,
                "Some mods read a key in their code with no setting for it. Spare Keys reads every mod's code once, when the " +
                "game starts, off the game's main thread (about half a second), and finds most of them. MaybeUsed (the " +
                "default): such keys show in amber, as maybe used, since many are only Escape or Enter inside the mod's own " +
                "window. CountAsUsed: they count as used. Off: not read. A mod's buttons with no setting behind them (often " +
                "keys of its own window) stay amber whatever this says.");

            MenusTypingWindows = settings.Bind(CountsPart, "MenusTypingWindows", false,
                "Keys used only in menus, while typing or with a window open", 73,
                "On: keys the game or a mod reads only in the main menu, the pause menu or the server list, while you type " +
                "in chat or the console, or with a window open (the inventory or a chest, the map, the split window, the " +
                "build menu's search, a mod's own window: StoreStash's sort key, Shift-click in the inventory) count as " +
                "used. Off (the default): they don't, since a key for walking about never meets them; turn it on when " +
                "you pick a key for a window.");

            Debug = settings.Bind(CountsPart, "DebugModeKeys", DebugRule.OnlyWhileDebugModeIsOn,
                "Debug-mode keys", 72,
                "The game's keys for debug mode with cheats (Z fly, B no building cost, K kill, L remove drops, and a few " +
                "more). OnlyWhileDebugModeIsOn (the default): they count while it is on. Never, or Always.");

            ConsoleBinds = settings.Bind(CountsPart, "ConsoleBinds", true,
                "The game's console binds", 71,
                "On: keys you bound to console commands with the game's \"bind\" command count as used.");

            SwitchedOffModules = settings.Bind(CountsPart, "SwitchedOffModules", false,
                "Keys of the pack's switched-off modules", 70,
                "On: keys of miikeskii's QoL Pack's mods that are switched off (Enabled off) count as used too. Off (the " +
                "default): a switched-off mod's keys are free.");

            Gamepad = settings.Bind(CountsPart, "GamepadButtons", false,
                "Gamepad buttons", 69,
                "On: gamepad buttons the game and mods use are listed too (in the list; the picture is a keyboard and " +
                "mouse).");

            SharedKeys = settings.Bind(CountsPart, "SharedKeys", SharedRule.ShownAsShared,
                "Keys shared by places never true at once", 68,
                "A key two things use, each only where the other never works (Left Alt + E pointing at a chest for one, " +
                "pointing at an armour stand for another): one press can't do both. ShownAsShared (the default): in its own " +
                "colour, listed under Shared Keys, not a clash. ShownAsUsed: simply used. CountedAsClashes: red, as a clash. " +
                "Two things that can both work at the same moment are a clash whatever this says: both anywhere, one anywhere " +
                "and one pointing at something, both pointing at the same kind of thing, or a tool in hand and a thing pointed " +
                "at (but not a build tool: the game points at nothing while one is in hand).");

            UnknownWhen = settings.Bind(CountsPart, "UnknownWhen", UnknownRule.Anywhere,
                "A key whose when isn't known counts as", 67,
                "Spare Keys knows when the game's keys and the pack's work, and the keys of some other mods (their when is " +
                "on hover). Another mod's key it knows no when for is taken to work anywhere. Anywhere (the default, safe): " +
                "it clashes with anything else on its key. Maybe: a clash only it could make shows in amber, as maybe a " +
                "clash. Tell it the when in the list below.");

            WhensList = settings.Bind(CountsPart, "WhensForOtherModsKeys", string.Empty,
                "Whens for other mods' keys", 66,
                "When other mods' keys work, for those Spare Keys doesn't know or knows wrongly: lines split by \";\", each " +
                "\"mod: setting = when\". The mod as F1 names it (or its id), the setting as F1 names it (or its key in the " +
                "settings file). The when, in parts split by commas: where (world, walking about (no build or radial menu " +
                "open), build menu, radial menu, inventory, chest open, split window, map, menu, typing, window <its name>, " +
                "anywhere); what's in hand (hand the hammer, hand the hoe, hand the cultivator, hand a terrain tool, hand a " +
                "build tool, hand another mod's build tool, hand anything but a build tool); what it points at (at a chest, at " +
                "a container (a chest, a " +
                "cart's or a ship's), at an armour stand, at an item stand, at a crafting station, at a smelter (kilns, the " +
                "windmill and the spinning wheel too), at a fire (torches too), at a cooking station, at a piece, at anything " +
                "you can use; in the inventory, over an item). Several of one kind split by \"/\"; another way it works after " +
                "\"|\"; \"unused\" for a setting its mod never reads. A name it doesn't know is taken to meet every other. For " +
                "example: \"Some Mod: Toggle key = world, hand the " +
                "hammer; Some Mod: Copy key = world, at a chest/an armour stand | chest open\". A line here wins over what " +
                "Spare Keys knows; one it can't read is named in the log.");

            LogClashes = settings.Bind(CountsPart, "LogClashesAtStart", false,
                "Log clashes at start", 65,
                "On: once the game has started, every clash goes into the log (BepInEx\\LogOutput.log).");

            F1Panel = settings.Bind(PickingPart, "PanelInFOne", PanelKind.KeyboardPicture,
                "Beside F1 while picking a key", 60,
                "While a key setting in the configuration manager's window (F1) waits for a key, a panel beside the window " +
                "shows which keys are free. KeyboardPicture (the default): the keyboard picture. List: the free keys in " +
                "words. Off: no panel. It only shows: a click on it would be taken as the new key, so it takes none. Works " +
                "with shudnal's configuration manager.");

            ControlsPanel = settings.Bind(PickingPart, "PanelInControls", PanelKind.KeyboardPicture,
                "Beside the game's Controls while rebinding", 59,
                "While the game's Settings, Controls tab waits for a key, a panel shows which keys are free of the game and " +
                "every mod (the game's keys fire whatever else is held, so only keys free with every modifier are green). " +
                "KeyboardPicture (the default), List, or Off. It only shows: a click there would be bound, so it takes none.");
        }

        /// <summary>The settings that decide what counts, as plain values (the map is worked out again when they change).</summary>
        internal static Options ReadOptions(bool debugModeOn) => new Options
        {
            Plain = Plain.Value,
            Code = Code.Value,
            MenusTypingWindows = MenusTypingWindows.Value,
            Debug = Debug.Value,
            DebugModeOn = debugModeOn,
            ConsoleBinds = ConsoleBinds.Value,
            SwitchedOffModules = SwitchedOffModules.Value,
            Gamepad = Gamepad.Value,
            Shared = SharedKeys.Value,
            Unknown = UnknownWhen.Value,
        };

        /// <summary>A state's colour, from the colour settings.</summary>
        internal static Color ColourOf(Status status)
        {
            switch (status)
            {
                case Status.Free:
                    return FreeColour.Value;
                case Status.WithModifier:
                    return ModifierColour.Value;
                case Status.Used:
                    return UsedColour.Value;
                case Status.Shared:
                    return SharedColour.Value;
                case Status.Clash:
                    return ClashColour.Value;
                case Status.Maybe:
                case Status.MaybeClash:
                    return MaybeColour.Value;
                case Status.Held:
                    return new Color(0.92f, 0.86f, 0.62f, 1f);
                default:
                    return new Color(0.13f, 0.12f, 0.11f, 1f);
            }
        }
    }
}
