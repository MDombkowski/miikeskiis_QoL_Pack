using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// What the menus show. Each amount in the game's requirement lists (the crafting menu, and the build menu's panel
    /// for the piece in hand) becomes "have/need", counting the chests near him; an amount he'd be short of without the
    /// chests flashes between his colour for it (yellow) and white, as AzuCraftyBoxes' did, and one short even with the
    /// chests keeps the game's own red flash. The name of the piece in hand gains how many of it he could place. The
    /// game writes both every frame, so switching the mod off puts its own text back at once.
    /// </summary>
    internal static class Display
    {
        // The requirement amounts made to shrink to fit "have/need", with how they were before, to put back.
        private static readonly Dictionary<TMP_Text, (bool Auto, float Min, float Max)> Fitted = new Dictionary<TMP_Text, (bool, float, float)>();

        /// <summary>After the game drew one requirement: "have/need" and its colour, or the game's own look when the mod
        /// doesn't count chests there. <paramref name="ownMultiplier"/>: the multiplier is the whole amount being crafted
        /// (card B17), not the game's, so even the game's own look is written again with it.</summary>
        internal static void Requirement(Transform element, Piece.Requirement requirement, Player player, bool craft, int quality, int multiplier,
                                         bool ownMultiplier)
        {
            Transform child = element.Find("res_amount");
            TMP_Text amount = child != null ? child.GetComponent<TMP_Text>() : null;
            if (amount == null)
            {
                return;
            }
            // The crafting menu, or the build menu's panel for the piece in hand (Crafting.On and Building.On include the
            // mod's own switch).
            bool counts = Settings.ShowAmounts.Value && (InventoryGui.IsVisible() ? Crafting.On : Building.On) &&
                          !ZoneSystem.instance.GetGlobalKey(craft ? GlobalKeys.NoCraftCost : GlobalKeys.NoBuildCost) && !player.NoCostCheat();
            int need = requirement.GetAmount(quality) * multiplier;
            if (!counts || need <= 0)
            {
                PutBack(amount);
                LockBadge.Hide(amount);
                if (ownMultiplier && need > 0)
                {
                    // The game's look, for the whole amount: what's needed, flashing red while he carries less.
                    amount.text = need.ToString();
                    bool free = player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(craft ? GlobalKeys.NoCraftCost : GlobalKeys.NoBuildCost);
                    amount.color = !free && player.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name) < need && Mathf.Sin(Time.time * 10f) > 0f
                        ? Color.red
                        : Color.white;
                }
                return;
            }
            string name = requirement.m_resItem.m_itemData.m_shared.m_name;
            int carried = player.GetInventory().CountItems(name);
            int have = carried + Stock.Of(name);
            Fit(amount);
            amount.text = Format(have, need);
            // How many more the reserved chests nearby hold, with a padlock (card A12); with UsedAsUsual they're in "have".
            LockBadge.Show(amount, Settings.Reserved.Value == ReservedChests.LeftOutWithLock ? Stock.ReservedOf(name) : 0);
            if (carried < need && have >= need)
            {
                amount.color = Mathf.Sin(Time.time * 10f) > 0f ? Settings.FromChestsColor.Value : Color.white;
            }
            else if (ownMultiplier)
            {
                // The game judged its own amount: judged again on the whole one, in the game's colours.
                amount.color = have < need && Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.white;
            }
            // Short even with the chests: the game's red flash stays. Enough on him: the game's white stays.
        }

        /// <summary>After the game named the piece in hand: how many of it he could place, counting the chests.</summary>
        internal static void PieceName(Hud hud, Piece piece)
        {
            Player player = Player.m_localPlayer;
            if (player == null || hud.m_buildSelection == null)
            {
                return;
            }
            int count = Building.HowMany(player, piece);
            Color color = count > 0 ? Settings.CanBuildColor.Value : Settings.CannotBuildColor.Value;
            string label = count == int.MaxValue ? "∞" : count.ToString();
            hud.m_buildSelection.text += $" (<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{label}</color>)";
        }

        /// <summary>"57/10" in his format, with thousands as 1.2K and millions as 3.4M (so it fits).</summary>
        internal static string Format(int have, int need)
        {
            string format = Settings.AmountFormat.Value;
            if (string.IsNullOrEmpty(format) || !format.Contains("{0}"))
            {
                format = "{0}/{1}";
            }
            try
            {
                return string.Format(format, Short(have), need);
            }
            catch (System.FormatException)
            {
                return $"{Short(have)}/{need}";   // a format he typed that string.Format can't read
            }
        }

        internal static string Short(int number)
        {
            if (number >= 1000000)
            {
                return (number / 1000000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
            }
            if (number >= 1000)
            {
                return (number / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K";
            }
            return number.ToString();
        }

        // Lets the amount shrink to fit "have/need" (as AzuCraftyBoxes did), remembering how it was.
        private static void Fit(TMP_Text amount)
        {
            if (!Fitted.ContainsKey(amount))
            {
                if (Fitted.Count >= 64)
                {
                    Tidy();
                }
                Fitted[amount] = (amount.enableAutoSizing, amount.fontSizeMin, amount.fontSizeMax);
                amount.fontSizeMax = amount.fontSize;
                amount.fontSizeMin = Mathf.Min(10f, amount.fontSize);
                amount.enableAutoSizing = true;
            }
        }

        // Back to the game's own sizing, when the mod doesn't count chests there (switched off, or a free build).
        private static void PutBack(TMP_Text amount)
        {
            if (Fitted.TryGetValue(amount, out (bool Auto, float Min, float Max) was))
            {
                amount.enableAutoSizing = was.Auto;
                amount.fontSizeMin = was.Min;
                amount.fontSizeMax = was.Max;
                Fitted.Remove(amount);
            }
        }

        /// <summary>Puts every amount back to the game's own sizing, and takes the padlocks away: the undo step after an
        /// error.</summary>
        internal static void PutBackAll()
        {
            foreach (TMP_Text amount in new List<TMP_Text>(Fitted.Keys))
            {
                if (amount != null)
                {
                    PutBack(amount);
                }
            }
            Fitted.Clear();
            LockBadge.RemoveAll();
        }

        /// <summary>Forgets requirement texts that no longer exist (a menu rebuilt after a world change).</summary>
        internal static void Tidy()
        {
            if (Fitted.Count > 0)
            {
                var gone = new List<TMP_Text>();
                foreach (TMP_Text text in Fitted.Keys)
                {
                    if (text == null)
                    {
                        gone.Add(text);
                    }
                }
                foreach (TMP_Text text in gone)
                {
                    Fitted.Remove(text);
                }
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class ShowWhatTheChestsHold
    {
        private static void Postfix(bool __result, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier)
        {
            // Also while the mod is switched off in its settings: Requirement then puts the game's own sizing back.
            if (!__result || elementRoot == null || req == null || req.m_resItem == null || player == null || player != Player.m_localPlayer)
            {
                return;
            }
            Module.Guard("showing what the chests hold", () =>
            {
                // The whole amount being crafted (card B17), else the game's own multiplier.
                int multiplier = craft && Module.IsOn ? Batches.LabelMultiplier(craftMultiplier) : craftMultiplier;
                Display.Requirement(elementRoot, req, player, craft, quality, multiplier, multiplier != craftMultiplier);
            });
        }
    }

    [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
    internal static class ShowHowManyCanBePlaced
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Hud __instance, Piece piece)
        {
            if (piece == null || piece.m_repairPiece || piece.m_removePiece || !Building.On || !Settings.ShowBuildCount.Value)
            {
                return;
            }
            Module.Guard("showing how many can be placed", () => Display.PieceName(__instance, piece));
        }
    }
}
