using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// House Trained's keys (cards B25 and B26), read through the game's own input (ZInput, as the pack's other mods read
    /// theirs: Topper-Upper's Keys). ZInput throws for a key missing from the game's own key map (archive/LESSONS_LEARNED
    /// 2026-09, "ZInput.GetKey throws"), and a friend may pick one in F1: such a key reads as not pressed, and is named in the
    /// log once. Never throws, so it's safe in a patch's body too.
    /// </summary>
    internal static class Keys
    {
        private static readonly HashSet<string> Unreadable = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True in the frame the key goes down with its modifiers held.</summary>
        internal static bool Pressed(KeyboardShortcut key) => Read(key, down: true);

        /// <summary>True while the key and its modifiers are held.</summary>
        internal static bool Held(KeyboardShortcut key) => Read(key, down: false);

        private static bool Read(KeyboardShortcut key, bool down)
        {
            if (key.MainKey == KeyCode.None)
            {
                return false;
            }
            string name = key.ToString();
            if (Unreadable.Contains(name))
            {
                return false;
            }
            try
            {
                bool main = down ? ZInput.GetKeyDown(key.MainKey, false) : ZInput.GetKey(key.MainKey, false);
                return main && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
            }
            catch (Exception)
            {
                Unreadable.Add(name);
                Module.Warn($"The key \"{name}\" in House Trained's settings can't be read by the game (it isn't in the game's own key " +
                            "map), so it does nothing. Pick another one.");
                return false;
            }
        }

        /// <summary>The key as the game's hints write one: "Left Alt + E"; empty for none.</summary>
        internal static string Text(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None)
            {
                return string.Empty;
            }
            var parts = new List<string>();
            foreach (KeyCode code in key.Modifiers.Concat(new[] { key.MainKey }))
            {
                parts.Add(Spaced(code));
            }
            return string.Join(" + ", parts);
        }

        // "LeftAlt" as "Left Alt", "Alpha1" as "1".
        private static string Spaced(KeyCode code)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal) && name.Length == 6)
            {
                return name.Substring(5);
            }
            var spaced = new StringBuilder();
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

        /// <summary>As the game decides whether a key is meant for it: a player, alive, and no chat, console, text box, menu,
        /// map, inventory, trader or radial menu in the way.</summary>
        internal static bool TakesKeys()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !Menu.IsVisible() && (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() &&
                   !InventoryGui.IsVisible() && !StoreGui.IsVisible() && !Hud.InRadial();
        }
    }
}
