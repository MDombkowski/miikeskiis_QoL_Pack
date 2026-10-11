using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// A key and the modifiers held with it, as a key setting holds them: the main key's id and the other keys' ids,
    /// sorted and without the main key, so two combinations held the same way compare equal. Ids are KeyCatalog's.
    /// </summary>
    internal readonly struct Combo
    {
        internal static readonly int[] NoMods = new int[0];

        internal readonly int Main;
        internal readonly int[] Mods;

        internal Combo(int main, IEnumerable<int> mods)
        {
            Main = main;
            Mods = Sorted(mods?.Where(id => id != main));
        }

        internal bool IsNone => Main < 0;

        internal static readonly Combo None = new Combo(-1, null);

        /// <summary>A set of modifier ids, sorted and without repeats: the form layers and combinations compare in.</summary>
        internal static int[] Sorted(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                return NoMods;
            }
            int[] sorted = ids.Distinct().OrderBy(id => id).ToArray();
            return sorted.Length == 0 ? NoMods : sorted;
        }

        internal static bool Same(int[] a, int[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Every id in <paramref name="part"/> is in <paramref name="whole"/> (both sorted).</summary>
        internal static bool Within(int[] part, int[] whole)
        {
            int j = 0;
            foreach (int id in part)
            {
                while (j < whole.Length && whole[j] < id)
                {
                    j++;
                }
                if (j == whole.Length || whole[j] != id)
                {
                    return false;
                }
                j++;
            }
            return true;
        }

        /// <summary>How a combination reads: "Left Ctrl + F1", modifiers first (Ctrl, Alt, Shift, left before right).</summary>
        internal string Text => IsNone ? "Not set" : Describe(Main, Mods);

        internal static string Describe(int main, int[] mods)
        {
            IEnumerable<string> held = mods.OrderBy(KeyCatalog.ModifierRank).Select(KeyCatalog.NameOf);
            return string.Join(" + ", held.Concat(new[] { KeyCatalog.NameOf(main) }));
        }

        /// <summary>A layer (the modifiers held) as words: "nothing held", "Left Alt held".</summary>
        internal static string LayerText(int[] layer) =>
            layer.Length == 0 ? "nothing held" : string.Join(" + ", layer.OrderBy(KeyCatalog.ModifierRank).Select(KeyCatalog.NameOf)) + " held";
    }

    /// <summary>
    /// Key text as BepInEx writes settings: a KeyCode's name ("F8"), or a KeyboardShortcut, main key first ("F1 +
    /// LeftControl"), read the way BepInEx reads them (KeyboardShortcut.Deserialize: split on spaces, '+', ',', ';' and
    /// '|', each part a KeyCode's exact name). Shared by the mod (which reads the values BepInEx serialises) and the
    /// scratch harness (which reads settings files), so both read keys the same way.
    /// </summary>
    internal static class KeyText
    {
        private static readonly char[] Separators = { ' ', '+', ',', ';', '|' };

        /// <summary>A KeyboardShortcut's text as its combination; None for "" or "None" (an empty shortcut), and also for
        /// text BepInEx couldn't read either (it then holds an empty shortcut). Keys the game can't read come back with id -1
        /// in <paramref name="unreadable"/>, so the caller can name them.</summary>
        internal static Combo Shortcut(string text, out string unreadable)
        {
            unreadable = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return Combo.None;
            }
            string[] parts = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var codes = new List<KeyCode>();
            foreach (string part in parts)
            {
                if (!TryCode(part, out KeyCode code))
                {
                    return Combo.None;   // BepInEx's own reading fails the whole shortcut then
                }
                codes.Add(code);
            }
            if (codes.Count == 0 || codes[0] == KeyCode.None)
            {
                return Combo.None;
            }
            var ids = new List<int>();
            foreach (KeyCode code in codes)
            {
                int id = KeyCatalog.IdOf(code);
                if (id < 0)
                {
                    unreadable = code.ToString();
                    return Combo.None;
                }
                ids.Add(id);
            }
            return new Combo(ids[0], ids.Skip(1));
        }

        /// <summary>A KeyCode setting's text as a combination with no modifiers; None for "None" or unreadable text.</summary>
        internal static Combo Single(string text, out string unreadable)
        {
            unreadable = null;
            if (string.IsNullOrWhiteSpace(text) || !TryCode(text.Trim(), out KeyCode code) || code == KeyCode.None)
            {
                return Combo.None;
            }
            int id = KeyCatalog.IdOf(code);
            if (id < 0)
            {
                unreadable = code.ToString();
                return Combo.None;
            }
            return new Combo(id, null);
        }

        /// <summary>Text that reads as a key or a combination of keys ("F7", "LeftAlt + H"), for a text setting that may be a
        /// key in disguise; None for anything else.</summary>
        internal static Combo Loose(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 60)
            {
                return Combo.None;
            }
            return Shortcut(text.Trim(), out _);
        }

        // A KeyCode's exact name, as Enum.Parse reads it (no numbers: BepInEx would read "5" as KeyCode 5, which no key is).
        private static bool TryCode(string name, out KeyCode code)
        {
            code = KeyCode.None;
            if (name.Length == 0 || char.IsDigit(name[0]) || name[0] == '-')
            {
                return false;
            }
            return Enum.TryParse(name, false, out code) && Enum.IsDefined(typeof(KeyCode), code);
        }
    }
}
