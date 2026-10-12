using System;
using System.Collections.Generic;

namespace QuickFix
{
    /// <summary>One resource of a recipe, as the metal rule reads it.</summary>
    internal struct Resource
    {
        internal string Prefab;
        internal int Amount;
        internal int PerLevel;
        internal bool Upgrader;

        internal Resource(string prefab, int amount, int perLevel, bool upgrader)
        {
            Prefab = prefab;
            Amount = amount;
            PerLevel = perLevel;
            Upgrader = upgrader;
        }
    }

    /// <summary>One stack of Sharpening Stones as the wear rule reads it: how many stones, and how much durability its
    /// top stone has left (the others are whole).</summary>
    internal struct StoneStack
    {
        internal int Count;
        internal float Top;

        internal StoneStack(int count, float top)
        {
            Count = count;
            Top = top;
        }
    }

    /// <summary>What the Sharpening Stone may repair, from its settings.</summary>
    internal sealed class StoneChoice
    {
        internal GearKinds Kinds;
        internal bool MetallicToo;
        internal ISet<string> Always = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal ISet<string> Never = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Quick Fix's rules on plain facts, with nothing of Unity in them, so a harness can check each one (card B22; the
    /// research set's decisions, Mod Projects Vault\research\2026-10-11_quick-fix-repair-stations-and-the-sharpening-stone).
    /// </summary>
    internal static class Rules
    {
        /// <summary>Durability below this counts as none left (floats).</summary>
        internal const float Tiny = 0.001f;

        /// <summary>The metal bars out of the box, by prefab name: today's Flametal is FlametalNew, Bloodgold is Gold, and
        /// Flametal is the old Ancient Metal, kept so the name never seems to mean today's (research part B).</summary>
        internal const string DefaultBars = "Copper, Tin, Bronze, Iron, Silver, BlackMetal, FlametalNew, Gold, Flametal";

        /// <summary>The names in a list setting: separated by commas, semicolons or spaces; any case.</summary>
        internal static HashSet<string> Names(string text)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text))
            {
                return names;
            }
            foreach (string part in text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
            return names;
        }

        /// <summary>An item is metallic when its repair recipe holds a listed metal, at any quality: a resource that is
        /// not an upgrader idol (the Forge of Potential's; "Bronze Protection Idol" is no bar), whose prefab is in the
        /// list, in its base amount or its amount per level (decision 2).</summary>
        internal static bool IsMetallic(IEnumerable<Resource> resources, ISet<string> bars)
        {
            if (resources == null || bars == null)
            {
                return false;
            }
            foreach (Resource resource in resources)
            {
                if (resource.Upgrader || string.IsNullOrEmpty(resource.Prefab))
                {
                    continue;
                }
                if ((resource.Amount > 0 || resource.PerLevel > 0) && bars.Contains(resource.Prefab))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a Grinding Wheel or Smith's Anvil set to <paramref name="what"/> repairs an item.</summary>
        internal static bool Covers(WhatItRepairs what, bool metallic)
        {
            switch (what)
            {
                case WhatItRepairs.NonMetallicGear:
                    return !metallic;
                case WhatItRepairs.MetallicGear:
                    return metallic;
                case WhatItRepairs.All:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The kinds an item counts as for the Sharpening Stone (decision 9), the first match by item type, then skill:
        /// armour (the armour slots, and the cosmetic clothes, which are Chest and Helmet), shields, the build tools,
        /// axes (a tool and a weapon), the pickaxes, farming tools and anything with no skill (the Grappling Hook), then
        /// weapons; anything else is Other.
        /// </summary>
        internal static GearKinds KindsOf(ItemDrop.ItemData.ItemType type, Skills.SkillType skill)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return GearKinds.Armor;
                case ItemDrop.ItemData.ItemType.Shield:
                    return GearKinds.Shields;
                case ItemDrop.ItemData.ItemType.Tool:
                    return GearKinds.Tools;
            }
            bool weaponType = type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                              type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow ||
                              type == ItemDrop.ItemData.ItemType.Torch;
            if (!weaponType)
            {
                return GearKinds.Other;
            }
            switch (skill)
            {
                case Skills.SkillType.Axes:
                    return GearKinds.Tools | GearKinds.Weapons;
                case Skills.SkillType.Pickaxes:
                case Skills.SkillType.Farming:
                case Skills.SkillType.None:
                    return GearKinds.Tools;
                default:
                    return GearKinds.Weapons;
            }
        }

        /// <summary>Whether the Sharpening Stone repairs an item (decision 8): "Never allowed" wins over everything,
        /// "Always allowed" over the kinds and the metal; otherwise a kind ticked, and metal only with "Metallic gear
        /// too".</summary>
        internal static bool StoneTakes(string prefab, GearKinds kinds, bool metallic, StoneChoice choice)
        {
            if (choice == null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(prefab))
            {
                if (choice.Never.Contains(prefab))
                {
                    return false;
                }
                if (choice.Always.Contains(prefab))
                {
                    return true;
                }
            }
            if ((kinds & choice.Kinds) == GearKinds.None)
            {
                return false;
            }
            return !metallic || choice.MetallicToo;
        }

        /// <summary>The game's own repair rule at a station, once the item has a recipe (InventoryGui.CanRepair, read in
        /// 1.0.17): the no-cost cheat repairs anything; otherwise the recipe's repair or crafting station must be this one
        /// (or the item comes from a lower world level), and the station's level, at most 4, must reach the recipe's
        /// minimum. Station names are the game's tokens ("$piece_workbench").</summary>
        internal static bool StationRepairs(string repairStation, string craftingStation, string station, int itemWorldLevel,
                                            int worldLevel, int stationLevel, int minStationLevel, bool noCost)
        {
            if (noCost)
            {
                return true;
            }
            if (repairStation == null && craftingStation == null)
            {
                return false;
            }
            bool match = (repairStation != null && repairStation == station) ||
                         (craftingStation != null && craftingStation == station) ||
                         itemWorldLevel < worldLevel;
            return match && Math.Min(stationLevel, 4) >= minStationLevel;
        }

        /// <summary>How much durability one stone repair gives an item: its share of full, never past full, never
        /// negative.</summary>
        internal static float Gained(float durability, float max, int sharePercent)
        {
            if (max <= 0f || sharePercent <= 0)
            {
                return 0f;
            }
            float target = Math.Min(max, Math.Max(0f, durability) + max * sharePercent / 100f);
            return Math.Max(0f, target - durability);
        }

        /// <summary>The bar's length, fixed at the press: seconds per item times the items, or the flat time.</summary>
        internal static float Duration(StoneTime mode, float secondsPerItem, float flatSeconds, int items)
        {
            float seconds = mode == StoneTime.Flat ? flatSeconds : secondsPerItem * Math.Max(0, items);
            return Math.Max(0f, seconds);
        }

        /// <summary>How much durability all the stones hold: each stack's top stone as it is, the rest whole.</summary>
        internal static float Available(IList<StoneStack> stacks, float max)
        {
            float total = 0f;
            if (stacks == null)
            {
                return total;
            }
            foreach (StoneStack stack in stacks)
            {
                if (stack.Count <= 0)
                {
                    continue;
                }
                total += Math.Min(max, Math.Max(0f, stack.Top)) + (stack.Count - 1) * max;
            }
            return total;
        }

        /// <summary>How many of <paramref name="items"/> worn items a repair can pay for (decision 12): one stone pays for
        /// them all; a share per use pays for them all or none; a share per item pays for as many as the stones hold.
        /// Shares are percents of one stone's full durability.</summary>
        internal static int Affordable(StoneCost cost, int stones, float available, float max, int perUsePercent,
                                       int perItemPercent, int items)
        {
            if (items <= 0 || stones <= 0)
            {
                return 0;
            }
            switch (cost)
            {
                case StoneCost.OneStone:
                    return items;
                case StoneCost.PercentPerUse:
                    return available + Tiny >= max * perUsePercent / 100f ? items : 0;
                case StoneCost.PercentPerItem:
                    float each = max * perItemPercent / 100f;
                    if (each <= 0f)
                    {
                        return items;
                    }
                    return Math.Min(items, (int)Math.Floor((available + Tiny) / each));
                default:
                    return 0;
            }
        }

        /// <summary>The durability a repair of <paramref name="items"/> items takes from the stones (0 for one stone).</summary>
        internal static float WearCost(StoneCost cost, float max, int perUsePercent, int perItemPercent, int items)
        {
            switch (cost)
            {
                case StoneCost.PercentPerUse:
                    return items > 0 ? max * perUsePercent / 100f : 0f;
                case StoneCost.PercentPerItem:
                    return max * perItemPercent / 100f * Math.Max(0, items);
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Wears the stones by <paramref name="cost"/>, in the order given (the caller puts the worn stone first): each
        /// stack's top stone loses what it can; one at 0 is used up and the next stone of the stack, whole, carries on.
        /// Returns each stack's stones used and its new top, and whether all of the cost was paid. Nothing is changed when
        /// it can't be paid in full (the caller checks <see cref="Available"/> first).
        /// </summary>
        internal static bool PlanWear(IList<StoneStack> stacks, float max, float cost, out int[] used, out float[] tops)
        {
            int n = stacks?.Count ?? 0;
            used = new int[n];
            tops = new float[n];
            for (int i = 0; i < n; i++)
            {
                tops[i] = stacks[i].Top;
            }
            if (cost <= Tiny)
            {
                return true;
            }
            if (max <= 0f || Available(stacks, max) + Tiny < cost)
            {
                return false;
            }
            float remaining = cost;
            for (int i = 0; i < n && remaining > Tiny; i++)
            {
                int left = stacks[i].Count;
                float top = Math.Min(max, Math.Max(0f, stacks[i].Top));
                while (left > 0 && remaining > Tiny)
                {
                    float take = Math.Min(remaining, top);
                    top -= take;
                    remaining -= take;
                    if (top <= Tiny)
                    {
                        left--;
                        used[i]++;
                        top = left > 0 ? max : 0f;
                    }
                }
                tops[i] = top;
            }
            return remaining <= Tiny;
        }

        /// <summary>The order the stone repairs in, and keeps when it can't pay for all: the most worn first (the lowest
        /// share of full left), then as listed.</summary>
        internal static List<int> MostWornFirst(IList<float> durability, IList<float> max)
        {
            var order = new List<int>();
            for (int i = 0; i < durability.Count; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                float fa = max[a] > 0f ? durability[a] / max[a] : 1f;
                float fb = max[b] > 0f ? durability[b] / max[b] : 1f;
                int byShare = fa.CompareTo(fb);
                return byShare != 0 ? byShare : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>The things within reach now that weren't at the last look (each repairs once as it comes near).</summary>
        internal static List<int> Newly(ICollection<int> before, ICollection<int> now)
        {
            var newly = new List<int>();
            foreach (int id in now)
            {
                if (before == null || !before.Contains(id))
                {
                    newly.Add(id);
                }
            }
            return newly;
        }
    }
}
