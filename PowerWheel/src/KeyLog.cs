using System;
using System.Collections.Generic;
using UnityEngine;

namespace PowerWheel
{
    /// <summary>
    /// "Write what it sees of the key to the log" (off out of the box; card A75, his test of 0.1.0): a line in
    /// BepInEx\LogOutput.log, each with its frame number, for each press of the key the mod watches, each tap, hold, drop
    /// and opening of the wheel, and why it left a press to the game or the wheel didn't open; and the settings it
    /// watches by, again each time they change. Nothing is written while the switch is off. Every piece of the log runs
    /// through Safe: an error in it switches only the log off, with one warning, never the mod.
    /// </summary>
    internal static class KeyLog
    {
        private static bool wasOn;
        private static bool broken;
        private static string lastWatching;

        // Set when the switch goes on (or a character comes): the next run of the patch on Player.Update says it runs.
        private static bool sayPatchRuns;

        internal static bool On => !broken && Settings.LogKeys != null && Settings.LogKeys.Value;

        internal static void Line(string text)
        {
            if (On)
            {
                Module.Log.LogInfo($"[frame {Time.frameCount}] {text}");
            }
        }

        /// <summary>Runs a piece of the log; its error switches only the log off, for the session.</summary>
        internal static void Safe(Action work)
        {
            if (!On)
            {
                return;
            }
            try
            {
                work();
            }
            catch (Exception error)
            {
                broken = true;
                try
                {
                    Module.Log.LogWarning($"The key log stopped after an error of its own; the mod works on without it.\n{error}");
                }
                catch (Exception)
                {
                    // A log listener's own error: the log is off either way.
                }
            }
        }

        /// <summary>Every frame, from Module.Update (also while the mod is off): the first lines once the switch goes on,
        /// and the settings again whenever they change.</summary>
        internal static void Follow()
        {
            bool on = On;
            if (!on)
            {
                wasOn = false;
                return;
            }
            Safe(() =>
            {
                string watching = Watching();
                if (!wasOn)
                {
                    Line("The key log is on. " + watching + " " + PatchCheck.Listed() + (Module.IsOn
                        ? " The next line says that the mod sees your character's frames; if it never comes, its patch on Player.Update isn't running."
                        : " The mod is switched off (Enabled), so it watches nothing."));
                    sayPatchRuns = true;
                }
                else if (watching != lastWatching)
                {
                    Line("Its settings changed. " + watching);
                }
                lastWatching = watching;
            });
            wasOn = on;
        }

        /// <summary>From the patch on Player.Update, for the local player, while the mod is on.</summary>
        internal static void PatchRuns(bool newCharacter)
        {
            if (!sayPatchRuns && !newCharacter)
            {
                return;
            }
            sayPatchRuns = false;
            Line($"It sees your character's frames (its patch on Player.Update runs{(newCharacter ? ", for a character new to it" : string.Empty)}). " + Watching());
        }

        /// <summary>What the mod watches now, by its settings.</summary>
        internal static string Watching()
        {
            string key = Settings.Key.Value == KeyChoice.GamePowerKey
                ? $"Key: the game's power key ({Keys.GamePowerKeyPath()})"
                : $"Key: its own key ({(Settings.OwnKey.Value.MainKey == KeyCode.None ? "none set" : Settings.OwnKey.Value.ToString())})";
            return $"{key}; a hold is {Settings.Hold:0.00} s; a tap {(Settings.Tap.Value == TapChoice.UsesPower ? "uses the power" : "does nothing")}; " +
                   $"the gamepad's power button {(Settings.Gamepad.Value ? "is watched" : "is the game's")}.";
        }

        /// <summary>The source's name, to start a sentence.</summary>
        internal static string NameOf(KeySource source)
        {
            string name = NameInSentence(source);
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>The source's name, in the middle of a sentence.</summary>
        internal static string NameInSentence(KeySource source)
        {
            switch (source)
            {
                case KeySource.GameKey:
                    return $"the game's power key ({Keys.GamePowerKeyPath()})";
                case KeySource.OwnKey:
                    return $"its own key ({Settings.OwnKey.Value})";
                case KeySource.Gamepad:
                    return "the gamepad's power button";
                default:
                    return "the key";
            }
        }

        internal static string Seconds(float seconds) => seconds.ToString("0.00") + " s";

        /// <summary>The reasons joined, or a note that none was found.</summary>
        internal static string Because(List<string> reasons) =>
            reasons.Count > 0 ? string.Join("; ", reasons) : "no reason found (the frame numbers may tell more)";
    }
}
