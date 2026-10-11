using HarmonyLib;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// What pointing at a chest shows (card A41): a line saying what a set-up chest takes, in Setups.Describe's few words
    /// ("Takes: Trophies from Swamp, Plains; Wolf Fang; except Leech Trophy"), and, with its setting, the setup key on every
    /// chest that can have a setup. Added after every other mod's hover text (MyLittleUI replaces the game's with its own),
    /// as Reserve Stock's Reserved line is.
    /// </summary>
    internal static class SetupHover
    {
        /// <summary>The Takes line's colour: a soft green, apart from the padlock's orange and the keys' yellow.</summary>
        private const string TakesColor = "#b5d98c";

        /// <summary>How many of a setup's picks the line names before "and 3 more".</summary>
        private const int Most = 4;

        /// <summary>The hover text with the setup's lines added, as the settings say.</summary>
        internal static string Add(Container chest, string text)
        {
            if (!Setups.Settable(chest))
            {
                return text;
            }
            if (SetupSettings.HoverLine.Value)
            {
                ItemPicks picks = Setups.Of(ChestSetup.Read(chest));
                if (picks != null)
                {
                    text += $"\n<color={TakesColor}>Takes: {Setups.Describe(picks, Most)}</color>";
                }
            }
            // No hint while the key is cleared (it would read "[None]", and the key does nothing then), nor on a chest he
            // couldn't open (someone else's private chest, behind a ward he isn't on): the chest service's own rules, as
            // Reserve Stock's hint asks them.
            if (SetupSettings.KeyHint.Value && SetupSettings.Key.Value.MainKey != KeyCode.None &&
                ChestFinder.Judge(chest, new ChestRules(), ChestClaims.PlayerId()) == null)
            {
                text += $"\n[<color=yellow><b>{Settings.KeyText(SetupSettings.Key.Value)}</b></color>] Set up";
            }
            // Stack Overflow's tidy key (card A42), with its setting: on a chest with a setup a tidy follows, that he could
            // open, and while the key isn't cleared.
            if (WantsTidyHint() && Tidy.AlikeKey(chest) != null &&
                ChestFinder.Judge(chest, new ChestRules(), ChestClaims.PlayerId()) == null)
            {
                text += $"\n[<color=yellow><b>{Settings.KeyText(OverflowSettings.Key.Value)}</b></color>] Tidy chests set up alike";
            }
            return text;
        }

        /// <summary>Whether Stack Overflow's key hint is asked for: Stack Overflow on, its hint on, its key not cleared.</summary>
        internal static bool WantsTidyHint() =>
            OverflowSettings.Active && OverflowSettings.KeyHint.Value && OverflowSettings.Key.Value.MainKey != KeyCode.None;
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class SayWhatASetUpChestTakes
    {
        // Last, after MyLittleUI's, which replaces the whole text with its own.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!SetupSettings.Active || __instance == null || __result == null ||
                (!SetupSettings.HoverLine.Value && !SetupSettings.KeyHint.Value && !SetupHover.WantsTidyHint()))
            {
                return;
            }
            string text = __result;
            Module.Guard("showing a chest's setup in its hover text", () => text = SetupHover.Add(__instance, text));
            __result = text;
        }
    }
}
