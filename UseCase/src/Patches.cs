using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches, all postfixes: the game's own code always runs as it would without the mod. Each target was looked
// up and checked in GameHooks first; a patch whose target wasn't found is skipped (Prepare). Every one asks first whether
// the mod is on and the panel wanted, and does nothing else otherwise (MODULES.md rule 5). What they note is only read
// by the panel (Sources); the one that shows the panel runs its work inside Module.Guard.
namespace UseCase
{
    /// <summary>The inventory's and chests' item tooltips: the slot under the pointer, noted every frame the game fills it.</summary>
    [HarmonyPatch]
    internal static class InventoryTooltipPatch
    {
        private static MethodBase TargetMethod() => GameHooks.CreateItemTooltip;

        private static void Postfix(ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (Module.IsOn && Settings.ShowPanel.Value && item?.m_shared != null)
            {
                Sources.Note(tooltip, item.m_shared.m_name, Where.Inventory);
            }
        }
    }

    /// <summary>The cost icons of the crafting panel and of the build panel (Hud.SetupPieceInfo calls the same method),
    /// told apart by whose icon it is.</summary>
    [HarmonyPatch]
    internal static class CostTooltipPatch
    {
        private static MethodBase TargetMethod() => GameHooks.SetupRequirement;

        private static void Postfix(Transform elementRoot, Piece.Requirement req, bool __result)
        {
            if (!__result || !Module.IsOn || !Settings.ShowPanel.Value || req?.m_resItem == null || elementRoot == null)
            {
                return;
            }
            UITooltip tooltip = elementRoot.GetComponent<UITooltip>();
            if (tooltip != null)
            {
                Sources.Note(tooltip, req.m_resItem.m_itemData.m_shared.m_name, IsBuildPanel(elementRoot) ? Where.BuildCost : Where.CraftingCost);
            }
        }

        private static bool IsBuildPanel(Transform elementRoot)
        {
            GameObject[] slots = Hud.instance != null ? Hud.instance.m_requirementItems : null;
            if (slots == null)
            {
                return false;
            }
            foreach (GameObject slot in slots)
            {
                if (slot != null && slot.transform == elementRoot)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The trader's list: its elements stand in the same order as the trader's available wares (the game's
    /// own SelectItem relies on it).</summary>
    [HarmonyPatch]
    internal static class TraderTooltipPatch
    {
        private static bool Prepare() => GameHooks.FillList != null;

        private static MethodBase TargetMethod() => GameHooks.FillList;

        private static void Postfix(StoreGui __instance)
        {
            if (!Module.IsOn || !Settings.ShowPanel.Value)
            {
                return;
            }
            Module.Guard("noting the trader's wares", () =>
            {
                Trader trader = GameHooks.StoreTrader(__instance);
                List<GameObject> elements = GameHooks.StoreItems(__instance);
                if (trader == null || elements == null)
                {
                    return;
                }
                List<Trader.TradeItem> wares = trader.GetAvailableItems();
                for (int i = 0; i < elements.Count && i < wares.Count; i++)
                {
                    UITooltip tooltip = elements[i] != null ? elements[i].GetComponent<UITooltip>() : null;
                    if (wares[i]?.m_prefab != null)
                    {
                        Sources.Note(tooltip, wares[i].m_prefab.m_itemData.m_shared.m_name, Where.Trader);
                    }
                }
            });
        }
    }

    /// <summary>Right after the game has shown its tooltip and moved it to the pointer (every UITooltip runs this each
    /// frame; only the one whose tooltip shows does anything): the panel beside it.</summary>
    [HarmonyPatch]
    internal static class TooltipPlacedPatch
    {
        private static MethodBase TargetMethod() => GameHooks.TooltipLateUpdate;

        private static void Postfix(UITooltip __instance)
        {
            if (!Module.IsOn || __instance != GameHooks.Current())
            {
                return;
            }
            Module.Guard("showing the panel beside a tooltip", () => Panel.Follow(__instance));
        }
    }

    /// <summary>An altar loaded near him: seen, for the spoiler rule.</summary>
    [HarmonyPatch]
    internal static class AltarLoadedPatch
    {
        private static bool Prepare() => GameHooks.BowlStart != null;

        private static MethodBase TargetMethod() => GameHooks.BowlStart;

        private static void Postfix(OfferingBowl __instance)
        {
            if (Module.IsOn && Settings.ShowPanel.Value)
            {
                Module.Guard("noting an altar seen", () => Places.Loaded(__instance));
            }
        }
    }

    /// <summary>A locked door loaded near him: seen, for the spoiler rule.</summary>
    [HarmonyPatch]
    internal static class DoorLoadedPatch
    {
        private static bool Prepare() => GameHooks.DoorAwake != null;

        private static MethodBase TargetMethod() => GameHooks.DoorAwake;

        private static void Postfix(Door __instance)
        {
            if (Module.IsOn && Settings.ShowPanel.Value && __instance != null && __instance.m_keyItem != null)
            {
                Module.Guard("noting a locked door seen", () => Places.Loaded(__instance));
            }
        }
    }
}
