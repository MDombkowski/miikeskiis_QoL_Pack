using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// What pointing at a chest shows of the "mine" mark (card A43): a line, Yours (in its colour, the favourites' gold at
    /// first) or whose it is, on a marked chest; and, with its setting (off out of the box), the mark key and what it would
    /// do on every chest he could mark. Added after every other mod's hover text (MyLittleUI replaces the game's with its
    /// own), so it is never lost.
    /// </summary>
    internal static class MineHover
    {
        /// <summary>The hover text with the mark's lines added, as the settings say.</summary>
        internal static string Add(Container chest, string text)
        {
            if (chest.GetType() != typeof(Container))
            {
                return text;
            }
            long me = ChestClaims.PlayerId();
            long mark = ChestMine.MarkOf(chest);
            if (mark != 0L && Settings.MineHoverLine.Value)
            {
                text += mark == me ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(Settings.MineColor.Value)}>Yours</color>" : $"\n{Mine.Whose(chest)}{Kept(chest)}";
            }
            // No hint while the key is cleared (it would read "[None]"), nor on a chest someone else marked: the key doesn't
            // act there.
            if (Settings.MineKeyHint.Value && Settings.MineKey.Value.MainKey != KeyCode.None && (mark == 0L || mark == me) &&
                Mine.Markable(chest) && Mine.WhyNot(chest) == null)
            {
                text += $"\n[<color=yellow><b>{Settings.KeyText(Settings.MineKey.Value)}</b></color>] {(mark == me ? "Unmark" : "Mark as yours")}";
            }
            return text;
        }

        // On another player's marked chest, what they let him do there (card A80): " (look only)", " (closed to you)", or
        // nothing where it opens as usual.
        private static string Kept(Container chest)
        {
            string words = ChestAccess.HoverWords(ChestAccess.HisHands(chest));
            return words.Length > 0 ? $" ({words})" : string.Empty;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class SayWhoseWhenPointing
    {
        // Last, after MyLittleUI's, which replaces the whole text with its own.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!Module.IsOn || __instance == null || __result == null || (!Settings.MineHoverLine.Value && !Settings.MineKeyHint.Value))
            {
                return;
            }
            string text = __result;
            Module.Guard("showing whose a chest is in its hover text", () => text = MineHover.Add(__instance, text));
            __result = text;
        }
    }
}
