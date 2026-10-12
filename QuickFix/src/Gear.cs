using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickFix
{
    /// <summary>
    /// The player's gear as Quick Fix reads it: what the game can repair (decision 1), what is metallic (decision 2),
    /// what kind it is (decision 9), and the one place an item's durability is raised, never past full.
    /// </summary>
    internal static class Gear
    {
        /// <summary>The Sharpening Stone's name in the game's data.</summary>
        internal const string StoneName = "$item_sharpeningstone";

        /// <summary>Its prefab, for the stone's shared data.</summary>
        internal const string StonePrefab = "SharpeningStone";

        private static string barsText;
        private static HashSet<string> bars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<Resource> resources = new List<Resource>();

        /// <summary>A Sharpening Stone: the game's name for it, and its prefab where the item knows it (so another mod's
        /// item that borrowed the name isn't taken for one).</summary>
        internal static bool IsStone(ItemDrop.ItemData item) =>
            item?.m_shared != null && item.m_shared.m_name == StoneName &&
            (item.m_dropPrefab == null || item.m_dropPrefab.name == StonePrefab);

        /// <summary>An item the game can repair (decision 1): it wears, the game lets it be repaired, and the recipe the game
        /// repairs it by has a crafting or repair station. Never a Sharpening Stone, which wears only by this mod.</summary>
        internal static bool CanBeRepaired(ItemDrop.ItemData item, out Recipe recipe)
        {
            recipe = null;
            if (item?.m_shared == null || !item.m_shared.m_useDurability || !item.m_shared.m_canBeReparied || IsStone(item))
            {
                return false;
            }
            recipe = ObjectDB.instance != null ? ObjectDB.instance.GetRecipe(item) : null;
            return recipe != null && (recipe.m_craftingStation != null || recipe.m_repairStation != null);
        }

        /// <summary>Below full durability.</summary>
        internal static bool Worn(ItemDrop.ItemData item) => item.m_durability < item.GetMaxDurability();

        /// <summary>Whether the recipe the item is repaired by takes a metal bar from the setting (decision 2).</summary>
        internal static bool IsMetallic(Recipe recipe)
        {
            if (recipe?.m_resources == null)
            {
                return false;
            }
            string text = Settings.MetalBars.Value ?? string.Empty;
            if (!ReferenceEquals(text, barsText))
            {
                bars = Rules.Names(text);
                barsText = text;
            }
            resources.Clear();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement?.m_resItem == null)
                {
                    continue;
                }
                resources.Add(new Resource(requirement.m_resItem.name, requirement.m_amount, requirement.m_amountPerLevel,
                                           requirement.m_upgraderResource));
            }
            return Rules.IsMetallic(resources, bars);
        }

        /// <summary>The item's prefab name, for the allow and deny lists: the game sets it on every item it makes, loads,
        /// crafts or picks up (Inventory.AddItem, ItemDrop.Awake); "" in the rare case it hasn't.</summary>
        internal static string PrefabOf(ItemDrop.ItemData item) => item.m_dropPrefab != null ? item.m_dropPrefab.name : string.Empty;

        internal static GearKinds KindsOf(ItemDrop.ItemData item) => Rules.KindsOf(item.m_shared.m_itemType, item.m_shared.m_skillType);

        /// <summary>
        /// Raises an item's durability by <paramref name="gain"/>, never past full, and the player's Crafting skill as the
        /// game's own repair does (InventoryGui.RepairOneItem: the share of full durability given back), with "Repairs
        /// raise Crafting" on. Returns whether anything changed.
        /// </summary>
        internal static bool Mend(Player player, ItemDrop.ItemData item, float gain)
        {
            float max = item.GetMaxDurability();
            float before = item.m_durability;
            float after = Mathf.Min(max, before + Mathf.Max(0f, gain));
            if (after <= before)
            {
                return false;
            }
            item.m_durability = after;
            if (Settings.RaiseCrafting.Value && max > 0f)
            {
                player.RaiseSkill(Skills.SkillType.Crafting, (after - before) / max);
            }
            return true;
        }

        /// <summary>Repairs to full every item in the player's inventory that the game can repair, is worn and
        /// <paramref name="covers"/> takes. Returns how many were repaired.</summary>
        internal static int MendAll(Player player, Func<ItemDrop.ItemData, Recipe, bool> covers)
        {
            Inventory inventory = player?.GetInventory();
            if (inventory == null)
            {
                return 0;
            }
            int mended = 0;
            // A copy: raising a skill can run other mods' code, which may change the inventory.
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (!CanBeRepaired(item, out Recipe recipe) || !Worn(item) || !covers(item, recipe))
                {
                    continue;
                }
                if (Mend(player, item, item.GetMaxDurability()))
                {
                    mended++;
                }
            }
            return mended;
        }

        /// <summary>"1 item", "3 items".</summary>
        internal static string Items(int count) => count == 1 ? "1 item" : $"{count} items";
    }
}
