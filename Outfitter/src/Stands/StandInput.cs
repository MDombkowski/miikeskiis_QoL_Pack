using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// The stands' keys, read through the game's own input where an error can't reach the game (a key missing from the
    /// game's key map throws there: LESSONS_LEARNED, Item Stand Icons' lesson), a key that failed said once and not tried
    /// again until its setting changes. When two keys go down together (Left Alt + Left Shift + E is also Left Alt + E with
    /// one more key held), the one with the most modifiers wins, so the save key never also puts a loadout on.
    /// </summary>
    internal static class StandInput
    {
        /// <summary>Whether a key went down this frame (ZInput.GetKeyDown in play); the harness sets another.</summary>
        internal static Func<KeyCode, bool> KeyDown = key => ZInput.GetKeyDown(key, false);

        /// <summary>Whether a key is held (ZInput.GetKey in play); the harness sets another.</summary>
        internal static Func<KeyCode, bool> KeyHeld = key => ZInput.GetKey(key, false);

        private static readonly Dictionary<ConfigEntry<KeyboardShortcut>, KeyboardShortcut> Failed =
            new Dictionary<ConfigEntry<KeyboardShortcut>, KeyboardShortcut>();

        /// <summary>Every frame while the stands are on: the key pressed at the stand he points at, if any, starts its
        /// work.</summary>
        internal static void Update()
        {
            if (!Stands.CanUseKeys())
            {
                return;
            }
            ConfigEntry<KeyboardShortcut> pressed = Pressed();
            if (pressed == null)
            {
                return;
            }
            IStand stand = Stands.Hovered();
            if (stand == null)
            {
                return;
            }
            if (pressed == StandSettings.SaveKey)
            {
                StandJob.Save(stand);
            }
            else if (pressed == StandSettings.EquipKey)
            {
                StandJob.Equip(stand);
            }
            else if (pressed == StandSettings.PersonalKey)
            {
                StandEdits.TogglePersonal(stand);
            }
            else if (pressed == StandSettings.PoseKey)
            {
                StandJob.Pose(stand);
            }
        }

        /// <summary>The stands' key that went down this frame, the one with the most modifiers when several did; null for
        /// none.</summary>
        internal static ConfigEntry<KeyboardShortcut> Pressed()
        {
            ConfigEntry<KeyboardShortcut> best = null;
            foreach (ConfigEntry<KeyboardShortcut> entry in Keys())
            {
                if (Down(entry) && (best == null || Modifiers(entry.Value) > Modifiers(best.Value)))
                {
                    best = entry;
                }
            }
            return best;
        }

        // Save first, so a tie (the same key set twice) goes to saving, which asks before changing anything; then putting on,
        // then the personal switch (job 30, Session 3), which changes only a mark.
        private static IEnumerable<ConfigEntry<KeyboardShortcut>> Keys()
        {
            yield return StandSettings.SaveKey;
            yield return StandSettings.EquipKey;
            yield return StandSettings.PersonalKey;
            if (StandSettings.ChangePose.Value)
            {
                yield return StandSettings.PoseKey;
            }
        }

        private static int Modifiers(KeyboardShortcut key) => key.Modifiers.Count();

        /// <summary>Whether any of the stands' keys is held now (its main key and every modifier): the game's own E then
        /// doesn't also take everything off the stand (StandPatches). Never throws.</summary>
        internal static bool AnyHeld()
        {
            foreach (ConfigEntry<KeyboardShortcut> entry in Keys())
            {
                if (Held(entry))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Down(ConfigEntry<KeyboardShortcut> entry) => Read(entry, down: true);

        private static bool Held(ConfigEntry<KeyboardShortcut> entry) => Read(entry, down: false);

        private static bool Read(ConfigEntry<KeyboardShortcut> entry, bool down)
        {
            if (entry == null)
            {
                return false;
            }
            KeyboardShortcut key = entry.Value;
            if (key.MainKey == KeyCode.None || (Failed.TryGetValue(entry, out KeyboardShortcut failed) && failed.Equals(key)))
            {
                return false;
            }
            try
            {
                if (!(down ? KeyDown(key.MainKey) : KeyHeld(key.MainKey)))
                {
                    return false;
                }
                foreach (KeyCode modifier in key.Modifiers)
                {
                    if (!KeyHeld(modifier))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception error)
            {
                Failed[entry] = key;
                Module.Warn($"The key {key} ({entry.Definition.Key}) can't be read ({error.Message}); choose another in the settings.");
                return false;
            }
        }

        /// <summary>A key as the game's hints write one: "Left Alt + E", "Left Alt + Left Shift + E" (Alt, then Ctrl, then
        /// Shift, whatever order BepInEx keeps them in: it sorts them by key code).</summary>
        internal static string Text(KeyboardShortcut key) =>
            key.MainKey == KeyCode.None
                ? "(no key)"
                : string.Join(" + ", key.Modifiers.OrderBy(ModifierRank).ThenBy(code => (int)code).Concat(new[] { key.MainKey }).Select(Spaced));

        private static int ModifierRank(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.AltGr:
                    return 0;
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return 1;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return 2;
                default:
                    return 3;
            }
        }

        // "LeftAlt" as "Left Alt", "Alpha1" as "1".
        private static string Spaced(KeyCode code)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal) && name.Length == 6)
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

        /// <summary>Forgets the keys that failed (the undo step: a key fixed while off is tried again).</summary>
        internal static void Forget() => Failed.Clear();
    }
}
