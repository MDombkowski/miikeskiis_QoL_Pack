using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using QoLMods.Kit;
using QoLMods.Naps;
using UnityEngine;

namespace NapTime
{
    /// <summary>
    /// The player's own bed by day, on a server whose Nap Time (Server) is on (Server.Now): the morning opens inside the
    /// server's window (the game's EnvMan.CalculateCanSleep, which the bed asks); each lie-down on the player's own bed
    /// writes into the character's record whether it is a nap (NapRules.MarkKey, beside the game's own s_inBed, so the
    /// server reads both together); the bed's hover text names what each key does at the time; and a nap's end says
    /// "Good evening".
    /// Everything else about the bed (the roof, the fire, enemies near, wet) stays the game's own.
    /// </summary>
    internal static class Beds
    {
        internal const string NapLine = "Nap until dusk";

        private static int markHash;
        private static Func<EnvMan, float, float> rescaleDayFraction;

        /// <summary>The bed's mark's hash in a character's record.</summary>
        private static int MarkHash => markHash != 0 ? markHash : (markHash = NapRules.MarkKey.GetStableHashCode());

        /// <summary>The game's members the patches reach for: without them, the module stays off.</summary>
        internal static void Resolve(List<string> missing)
        {
            if (AccessTools.Method(typeof(EnvMan), "CalculateCanSleep", Type.EmptyTypes)?.ReturnType != typeof(bool))
            {
                missing.Add("EnvMan.CalculateCanSleep");
            }
            var rescale = AccessTools.Method(typeof(EnvMan), "RescaleDayFraction", new[] { typeof(float) });
            if (rescale == null || rescale.ReturnType != typeof(float))
            {
                missing.Add("EnvMan.RescaleDayFraction");
            }
            else
            {
                rescaleDayFraction = AccessTools.MethodDelegate<Func<EnvMan, float, float>>(rescale);
            }
            if (AccessTools.Field(typeof(Player), nameof(Player.m_wakeupTime))?.FieldType != typeof(double))
            {
                missing.Add("Player.m_wakeupTime");
            }
            if (AccessTools.Field(typeof(EnvMan), nameof(EnvMan.m_sleepCooldownSeconds))?.FieldType != typeof(double))
            {
                missing.Add("EnvMan.m_sleepCooldownSeconds");
            }
        }

        /// <summary>The sky's hour now by the world's clock (SkyFromClock), as the server decides a nap by it: the bed's mark
        /// and its hover lines agree with the server's morning and afternoon. False when the game can't say.</summary>
        private static bool HourNow(out double hour)
        {
            float sky = SkyFromClock();
            hour = float.IsNaN(sky) ? 0 : NapRules.Hour(sky);
            return !float.IsNaN(sky);
        }

        /// <summary>
        /// The sky's fraction straight from the world's clock, as the game's EnvMan works it out each frame before it
        /// smooths it (its raw fraction through its own RescaleDayFraction, with whatever patches it, World Settings'
        /// included): at a wake the smoothed sky still trails a skip, the more so the lower the frame rate, while the clock
        /// is at most the server's last 2 s behind. NaN when the game can't say.
        /// </summary>
        internal static float SkyFromClock()
        {
            EnvMan env = EnvMan.instance;
            ZNet net = ZNet.instance;
            if (env == null || net == null || rescaleDayFraction == null || env.m_dayLengthSec <= 0)
            {
                return float.NaN;
            }
            return rescaleDayFraction(env, NapRules.RawFraction(net.GetTimeSeconds(), env.m_dayLengthSec));
        }

        /// <summary>Whether the bed opens where the game's own says no: the morning, inside the server's window, after the
        /// game's own wait since the player woke. By the smoothed sky, as the game's own afternoon and night are, so the
        /// bed never shuts for a moment at noon between the two.</summary>
        internal static bool Opens(EnvMan env, Served served)
        {
            Player player = Player.m_localPlayer;
            ZNet net = ZNet.instance;
            if (player == null || net == null || env == null)
            {
                return false;
            }
            double hour = NapRules.Hour(env.GetDayFraction());
            return NapRules.BedOpens(hour, served, net.GetTimeSeconds(), player.m_wakeupTime, env.m_sleepCooldownSeconds);
        }

        /// <summary>
        /// Before the game's Bed.Interact: on the player's own current bed, writes the mark for this lie-down (before the
        /// game's own checks, so it is in the record with s_inBed whenever the player gets into bed): whether it is a nap
        /// on a server serving naps, and not a nap anywhere else, so an earlier lie-down's mark never counts for this one.
        /// Returns what to say once in bed: 0 nothing, 1 a nap, 2 a nap that waits on everyone in bed choosing it.
        /// </summary>
        internal static int BeforeLieDown(Bed bed, Player player)
        {
            if (bed == null || player == null || player != Player.m_localPlayer || player.InBed() || !bed.IsCurrent())
            {
                return 0;
            }
            Served served = Server.Now;
            if (served == null || !HourNow(out double hour))
            {
                WriteMark(player, false);   // writes only where an earlier mark says yes
                return 0;
            }
            bool nap = NapRules.BedNaps(hour, served, Keys.Held(Settings.NapKey.Value), Settings.UseKeyAfternoon.Value,
                                        Settings.NapKeyAfternoon.Value);
            WriteMark(player, nap);
            if (!nap)
            {
                return 0;
            }
            return NapRules.IsAfternoon(hour) && served.Afternoon == AfternoonRule.EachPlayerPicks ? 2 : 1;
        }

        /// <summary>After the game's Bed.Interact: once the player is in bed for a nap, a short line.</summary>
        internal static void AfterLieDown(Player player, int say)
        {
            if (say == 0 || player == null || !player.InBed() || !Settings.Messages.Value)
            {
                return;
            }
            Say.Center(say == 2
                ? "Napping until dusk if everyone in bed naps, else sleeping to morning."
                : "Napping: once everyone is in bed, all wake at dusk.");
        }

        private static void WriteMark(Player player, bool nap)
        {
            ZNetView view = player.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (zdo == null || !view.IsOwner())
            {
                return;
            }
            if (zdo.GetBool(MarkHash) != nap)
            {
                zdo.Set(MarkHash, nap);
            }
        }

        /// <summary>Takes the mark off the player's record (switched off, or after an error), so the server never counts a
        /// nap chosen before.</summary>
        internal static void ClearMark()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            WriteMark(player, false);
        }

        /// <summary>The bed's hover text with its keys' nap lines: E's line changed where it naps, and the second key's line
        /// where it does something else than E.</summary>
        internal static string Hover(Bed bed, string text)
        {
            Served served = Server.Now;
            if (served == null || !bed.IsCurrent() || !HourNow(out double hour))
            {
                return text;
            }
            KeyboardShortcut second = Settings.NapKey.Value;
            BedHint hint = NapRules.Hint(hour, served, Settings.UseKeyAfternoon.Value, Settings.NapKeyAfternoon.Value,
                                         second.MainKey != KeyCode.None);
            if (!hint.Changed)
            {
                return text;
            }
            string sleep = Localization.instance.Localize("$piece_bed_sleep");
            string useKey = Localization.instance.Localize("$KEY_Use");
            string lines = KeyLine(useKey, hint.UseKeyNaps ? NapLine : sleep);
            if (hint.ShowSecond)
            {
                lines += "\n" + KeyLine(Keys.Text(second), hint.SecondKeyNaps ? NapLine : sleep);
            }
            // The game's own sleep line is its last; another mod may have added lines after it, so it is found by its words.
            string[] all = text.Split('\n');
            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (all[i].EndsWith(sleep, StringComparison.Ordinal))
                {
                    all[i] = lines;
                    return string.Join("\n", all);
                }
            }
            return text + "\n" + lines;
        }

        // As the game writes a key's line: "[<color=yellow><b>E</b></color>] Sleep".
        private static string KeyLine(string key, string what) => $"[<color=yellow><b>{key}</b></color>] {what}";
    }

    /// <summary>The bed opens in the morning (EnvMan.CalculateCanSleep, which Bed.Interact asks through EnvMan.CanSleep).</summary>
    [HarmonyPatch(typeof(EnvMan), "CalculateCanSleep")]
    internal static class CanSleepPatch
    {
        private static void Postfix(EnvMan __instance, ref bool __result)
        {
            Served served = Server.Now;
            if (__result || served == null || !Module.IsOn)
            {
                return;
            }
            bool opens = false;
            Module.Guard("letting the bed open by day", () => opens = Beds.Opens(__instance, served));
            if (opens)
            {
                __result = true;
            }
        }
    }

    /// <summary>The bed's mark, written as the player lies down, and a line once in bed.</summary>
    [HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
    internal static class BedInteractPatch
    {
        private static void Prefix(Bed __instance, Humanoid human, bool repeat, out int __state)
        {
            __state = 0;
            if (repeat || !Module.IsOn)
            {
                return;
            }
            int say = 0;
            Module.Guard("marking a nap", () => say = Beds.BeforeLieDown(__instance, human as Player));
            __state = say;
        }

        private static void Postfix(Humanoid human, int __state)
        {
            if (__state == 0 || !Module.IsOn)
            {
                return;
            }
            Module.Guard("saying a nap's line", () => Beds.AfterLieDown(human as Player, __state));
        }
    }

    /// <summary>What each key does by day, on the bed's hover text.</summary>
    [HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText))]
    internal static class BedHoverPatch
    {
        private static void Postfix(Bed __instance, ref string __result)
        {
            if (!Module.IsOn || Server.Now == null || string.IsNullOrEmpty(__result))
            {
                return;
            }
            string text = __result;
            Module.Guard("showing the bed's nap keys", () => text = Beds.Hover(__instance, text));
            __result = text;
        }
    }

    /// <summary>"Good evening" in place of the game's "Good morning" when a nap ends at dusk: the server said the latest
    /// sleep is a nap (its answer's NapTo), and this game's clock is at dusk.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetSleeping))]
    internal static class WakePatch
    {
        private static void Prefix(Player __instance, bool sleep, out bool __state)
        {
            __state = !sleep && __instance.IsSleeping();
        }

        private static void Postfix(Player __instance, bool __state)
        {
            if (!__state || !Module.IsOn || !Settings.Messages.Value || __instance != Player.m_localPlayer)
            {
                return;
            }
            Module.Guard("saying good evening", () =>
            {
                if (NapRules.WokeFromNap(Server.Now, Beds.SkyFromClock()))
                {
                    Say.Center("Good evening");
                }
            });
        }
    }
}
