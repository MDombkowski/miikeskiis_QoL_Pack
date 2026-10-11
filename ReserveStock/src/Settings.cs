using System.Linq;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace ReserveStock
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.reservestock.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Reserve Stock". Every one takes effect at
    /// once. How Craft from Chests' counts show reserved chests is Craft from Chests' own setting, "Reserved chests".
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<bool> ShowButton { get; private set; }

        internal static ConfigEntry<bool> ShowHoverLine { get; private set; }

        internal static ConfigEntry<bool> ShowKeyHint { get; private set; }

        internal static ConfigEntry<bool> BlockPlaceStacks { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first. Keys never change once released; new ones get new keys.
            Enabled = settings.Enabled;

            // The chest service's section for other mods made for speed (it runs the switch, so it must offer the setting;
            // card A69, 0.36.1's review). Its chest settings ("Chests (every mod)") aren't Reserve Stock's to offer: it
            // never counts or takes from chests.
            QoLMods.Chests.PerformanceMods.Bind(settings);

            Key = settings.Bind(ModuleSettings.General, "Key", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Key to reserve the chest you point at", 40,
                "Reserves the chest you're pointing at, or frees it if it's reserved, without opening it. Left Alt + E by " +
                "default, since Left Shift + E renames a chest (MyLittleUI). Keep a key such as Left Alt with E: with plain " +
                "E, E would reserve chests instead of opening them. Anyone who could open the chest can reserve or free it; " +
                "not while someone has it open.");

            // On by default, his call after his test (2026-09-28), though it adds to every chest window (MODULES.md, rule 4).
            ShowButton = settings.Bind(ModuleSettings.General, "ShowButton", true,
                "Button in the chest window", 39,
                "A padlock button beside Place stacks in an open chest's window: click it to reserve the chest or free it. " +
                "Closed and orange while the chest is reserved. Off: no button; the key works either way.");

            ShowHoverLine = settings.Bind(ModuleSettings.General, "ShowHoverLine", true,
                "Say \"Reserved\" when pointing at one", 38,
                "Adds a line, Reserved, to a reserved chest's hover text.");

            ShowKeyHint = settings.Bind(ModuleSettings.General, "ShowKeyHint", false,
                "Show the key when pointing at a chest", 37,
                "Adds the key and what it does (Reserve, or Unreserve) to every chest's hover text, as the game shows its own keys.");

            BlockPlaceStacks = settings.Bind(ModuleSettings.General, "BlockPlaceStacks", false,
                "Place stacks leaves reserved chests alone too", 36,
                "Off: by hand, nothing is ever blocked at a reserved chest: taking, adding and Place stacks in its window, " +
                "and holding E on it, all work as usual, since they're a choice made at that one chest. On: Place stacks and " +
                "holding E do nothing at a reserved chest and say why. StoreStash (in the pack) has the same setting for its " +
                "own Place stacks and holding E: either one on leaves reserved chests alone. Quick stack to nearby chests " +
                "(StoreStash's, or HexQuickStackStorage's P and Q) leaves reserved chests alone either way.");
        }

        /// <summary>True in the frame the key goes down with its modifiers held, read through the game's own input.</summary>
        internal static bool Pressed(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));

        /// <summary>True while the key and its modifiers are held.</summary>
        internal static bool Held(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKey(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));

        /// <summary>The key as the game's hints write one: "Left Alt + E".</summary>
        internal static string KeyText(KeyboardShortcut key) =>
            string.Join(" + ", key.Modifiers.Concat(new[] { key.MainKey }).Select(Spaced));

        // "LeftAlt" as "Left Alt", "Alpha1" as "1".
        private static string Spaced(KeyCode code)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha") && name.Length == 6)
            {
                return name.Substring(5);
            }
            var spaced = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    spaced.Append(' ');
                }
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }
    }
}
