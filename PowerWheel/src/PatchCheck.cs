using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PowerWheel
{
    /// <summary>
    /// Says in the log when the mod's patch on Player.Update stops running, or never runs, while his character is in the
    /// world (card A75, his test of 0.1.0: F used the power at the press and his own key did nothing, so the mod never
    /// acted on a press). Watched all session, once a frame after every Update (so after the game's Player.Update): a
    /// warning once the patch hasn't run for 5 s and 30 frames with his character there, listing the Harmony patches on
    /// that method, and a line when it runs again. A warning whatever the key log says; nothing is written while the
    /// patch runs.
    /// </summary>
    internal static class PatchCheck
    {
        /// <summary>How long without a run of the patch, his character in the world, before the warning.</summary>
        private const float Patience = 5f;

        private const int PatienceFrames = 30;

        private static Player seen;
        private static float lastRun;
        private static int lastRunFrame;
        private static bool warned;

        /// <summary>From the patch on Player.Update, for the local player.</summary>
        internal static void Ran()
        {
            lastRun = Time.unscaledTime;
            lastRunFrame = Time.frameCount;
            if (warned)
            {
                warned = false;
                Module.Log.LogInfo("Its patch on Player.Update runs again.");
            }
        }

        /// <summary>Once a frame, from Module.LateUpdate while the mod is on.</summary>
        internal static void LateTick()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !ReferenceEquals(player, seen))
            {
                // No character, or a new one: the wait starts now.
                seen = player;
                lastRun = Time.unscaledTime;
                lastRunFrame = Time.frameCount;
                return;
            }
            if (warned || Time.unscaledTime - lastRun < Patience || Time.frameCount - lastRunFrame < PatienceFrames)
            {
                return;
            }
            warned = true;
            Module.Log.LogWarning($"Its patch on Player.Update hasn't run for {Patience:0} s with your character in the world, so the key " +
                                  "never reaches it: F works as in the game and its own key does nothing. " + Listed());
        }

        /// <summary>The Harmony patches on Player.Update now, in words (hooks made another way, and patches made with
        /// another copy of Harmony, aren't listed).</summary>
        internal static string Listed()
        {
            Patches info = GameHooks.PlayerUpdate != null ? Harmony.GetPatchInfo(GameHooks.PlayerUpdate) : null;
            var lines = new List<string>();
            if (info != null)
            {
                lines.AddRange(info.Prefixes.Select(p => Of("prefix", p)));
                lines.AddRange(info.Postfixes.Select(p => Of("postfix", p)));
                lines.AddRange(info.Transpilers.Select(p => Of("transpiler", p)));
                lines.AddRange(info.Finalizers.Select(p => Of("finalizer", p)));
            }
            return "The Harmony patches on Player.Update (hooks made another way aren't listed): " +
                   (lines.Count > 0 ? string.Join("; ", lines) : "none") + ".";
        }

        private static string Of(string kind, Patch patch) =>
            $"{kind} {patch.PatchMethod?.DeclaringType?.FullName}.{patch.PatchMethod?.Name} by {patch.owner} (priority {patch.priority})";
    }
}
