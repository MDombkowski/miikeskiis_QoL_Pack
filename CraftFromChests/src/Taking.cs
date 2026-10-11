using System.Collections.Generic;
using HarmonyLib;

namespace CraftFromChests
{
    /// <summary>
    /// Taking a craft's or a piece's materials. The game's own Player.ConsumeResources takes each material from his
    /// inventory, as much as he carries; whatever it's short of comes out of the chests, in the same frame, just before
    /// (the patch below): the chests held for the craft under way, or the chests he owns for a piece. Only when the craft
    /// or the placing was approved in this frame (Crafting.Consuming, Building.PlacingThisFrame), so no other use of
    /// ConsumeResources ever reaches a chest.
    /// </summary>
    internal static class Taking
    {
        /// <summary>
        /// What these requirements need beyond what he carries, added to <paramref name="into"/> by key (the item's name
        /// for any quality, or name@quality when <paramref name="itemQuality"/> names one), with the same requirements the
        /// game's ConsumeResources takes (a station's upgrade items only at that station, and only there).
        /// </summary>
        internal static void Shortfall(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier,
                                       Dictionary<string, int> into)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (!Counts(requirement, station))
                {
                    continue;
                }
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int need = requirement.GetAmount(qualityLevel) * multiplier;
                // What the game's own inventory.RemoveItem(name, need, itemQuality) finds: every stack of that name (and
                // quality) at this world's level.
                int carried = inventory.CountItems(name, itemQuality);
                if (need > carried)
                {
                    string key = itemQuality >= 0 ? $"{name}@{itemQuality}" : name;
                    into.TryGetValue(key, out int sum);
                    into[key] = sum + need - carried;
                }
            }
        }

        /// <summary>Whether ConsumeResources takes this requirement here (Player.ConsumeResources' own test).</summary>
        internal static bool Counts(Piece.Requirement requirement, CraftingStation station) =>
            (station == null || station.m_upgrader == requirement.m_upgraderResource) && (station != null || !requirement.m_upgraderResource) &&
            requirement.m_resItem != null;

        /// <summary>Takes out of the chests what ConsumeResources, about to run, won't find on him: from the chests held for
        /// the craft under way, or, for a piece placed in this frame, from the chests he owns right now (a fresh plan for
        /// each payment, since another mod may place several pieces and pay for each: PlantEasily's extra plants).</summary>
        internal static void BeforeConsuming(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
        {
            var shortOf = new Dictionary<string, int>();
            Shortfall(player, requirements, qualityLevel, itemQuality, multiplier, shortOf);
            if (shortOf.Count == 0)
            {
                return;
            }
            Request from = Crafting.InDoCrafting ? Crafting.Consuming : Supply.FromOwned(shortOf, player.transform.position);
            if (from == null)
            {
                return;
            }
            try
            {
                foreach (KeyValuePair<string, int> each in shortOf)
                {
                    int removed = from.Remove(each.Key, each.Value);
                    if (removed < each.Value)
                    {
                        // A craft: the game's check counted these very chests in this frame, so this is a fault. A piece:
                        // another mod placed more pieces than the chests he owns held (it asked before a chest was handed on).
                        Module.Log.LogWarning($"The chests gave {removed} of the {each.Value} {each.Key} it was short of; the game took " +
                                              "what it could from the inventory. " +
                                              (Crafting.InDoCrafting ? "This is a fault in the mod." : "Another mod placed more than the chests held."));
                    }
                }
            }
            finally
            {
                from.Send();   // even if another mod's handler threw mid-take: a changed chest is sent and held
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class TakeWhatHeIsShortOf
    {
        // Last, so it knows whether another mod's prefix has stopped the game's own taking: then nothing comes out of the
        // chests either, or he'd pay only the chests' part.
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier, bool __runOriginal)
        {
            if (Crafting.InDoCrafting && __instance == Player.m_localPlayer)
            {
                Crafting.Consumed = true;   // the craft happened, even if another mod takes its materials instead
            }
            if (!__runOriginal || !Module.IsOn || __instance != Player.m_localPlayer || requirements == null)
            {
                return;
            }
            if (!Crafting.InDoCrafting && !Building.PlacingThisFrame)
            {
                return;   // not a craft or a piece this mod approved: never a chest
            }
            if (Crafting.InDoCrafting && Crafting.Consuming == null)
            {
                return;
            }
            Module.Guard("taking materials from chests", () => Taking.BeforeConsuming(__instance, requirements, qualityLevel, itemQuality, multiplier));
        }
    }
}
