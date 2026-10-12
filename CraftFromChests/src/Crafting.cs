using System;
using System.Collections.Generic;
using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// Crafting from chests. The menu counts what the chests near him hold as if he carried it (Player.HaveRequirements,
    /// through the survey). When he presses Craft, what the craft is short of is asked of the chests' owners at once (a
    /// Request), and the crafting bar runs meanwhile; if the chests aren't in when the bar would end, it waits just short
    /// of the end. As the bar ends (InventoryGui.DoCrafting), the chests of this craft are reloaded, the game's own check
    /// for this recipe counts only what he carries and those chests still his (whether or not anything was asked for),
    /// and the game's own ConsumeResources takes what he carries; what it's short of comes out of those chests in the same
    /// frame (Taking). The one recipe that takes any one of several ingredients (Raw Fish, at the food preparation table)
    /// gets its fish moved into his inventory first, since the game takes that one straight from there.
    /// </summary>
    internal static class Crafting
    {
        /// <summary>A little before the bar's end, where it waits for the chests.</summary>
        private const float WaitShortOfTheEnd = 0.05f;

        private const string NoLongerCarried = "Couldn't craft it: you no longer carry what it needs. Press Craft again to use the chests.";

        private static Craft craft;

        /// <summary>Inside InventoryGui.DoCrafting: the game's checks for the recipe being crafted count only what he
        /// carries and Consuming's chests.</summary>
        internal static bool InDoCrafting { get; private set; }

        /// <summary>Inside DoCrafting: the recipe being crafted, which the game's check counts strictly.</summary>
        internal static Recipe CraftingRecipe { get; private set; }

        /// <summary>Inside DoCrafting: the chests asked for this craft, or null (only what he carries).</summary>
        internal static Request Consuming { get; private set; }

        /// <summary>Set when the craft's materials were taken (ConsumeResources ran inside DoCrafting).</summary>
        internal static bool Consumed;

        /// <summary>Inside DoCrafting: set when the game's check for the craft under way said no because the chests held for
        /// it fell short (a friend opened one meanwhile, say), so the craft won't happen and AfterCrafting says why.</summary>
        internal static bool Refused { get; private set; }

        internal static bool On => Module.IsOn && Settings.CraftFromChests.Value;

        private sealed class Craft
        {
            internal Recipe Recipe;
            internal Request Request;

            /// <summary>For a recipe that takes any one of its ingredients: the fish (by key) to move into his inventory
            /// before the game looks for it, and how many.</summary>
            internal string OnlyOneKey;

            internal int OnlyOneAmount;

            /// <summary>Since when the bar has waited for a chest claimed far away (-1: not waiting).</summary>
            internal float FarWaitSince = -1f;
        }

        internal static void Reset()
        {
            Drop();
            Consuming = null;
            CraftingRecipe = null;
            InDoCrafting = false;
            Refused = false;
        }

        // Forgets the craft, and stops its request asking any more chests.
        private static void Drop()
        {
            if (craft != null)
            {
                craft.Request.Cancelled = true;
                craft = null;
            }
        }

        /// <summary>Every frame: forgets a craft that ended without its bar reaching the end (cancelled, or the window
        /// closed); while its bar runs, keeps the chests planned for it from the server (ChestClaims.KeepForCraft: a server that
        /// re-sorts chests took one mid-bar, and the craft came up short; a player's hand still gets one at once).</summary>
        internal static void Update()
        {
            if (craft == null)
            {
                return;
            }
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || GameHooks.Read<float>(GameHooks.CraftTimer, gui) < 0f)
            {
                Drop();
                return;
            }
            if (!On)
            {
                return;   // switched off mid-bar: the server is let in again
            }
            foreach (Container chest in craft.Request.Ready)
            {
                ChestClaims.KeepForCraft(chest);
            }
        }

        /// <summary>From the Craft button, once the game has started the bar: asks the chests for what the craft is short of.</summary>
        internal static void Started(InventoryGui gui)
        {
            Drop();
            Player player = Player.m_localPlayer;
            var recipe = GameHooks.Read<Recipe>(GameHooks.CraftRecipe, gui);
            if (player == null || recipe == null || player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
            {
                return;
            }
            var upgrade = GameHooks.Read<ItemDrop.ItemData>(GameHooks.CraftUpgradeItem, gui);
            int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
            int multiplier = GameHooks.Read<bool>(GameHooks.MultiCrafting, gui) ? gui.m_multiCraftAmount : 1;
            var wanted = new Dictionary<string, int>();
            string onlyOneKey = null;
            int onlyOneAmount = 0;
            if (recipe.m_requireOnlyOneIngredient)
            {
                if (!OnlyOneShort(player, recipe, quality, multiplier, out onlyOneKey, out onlyOneAmount))
                {
                    return;   // he carries one already, or the chests hold none either
                }
                wanted[onlyOneKey] = onlyOneAmount;
            }
            else
            {
                Wants(player, recipe, quality, multiplier, wanted);
            }
            if (wanted.Count == 0)
            {
                return;
            }
            craft = new Craft
            {
                Recipe = recipe,
                OnlyOneKey = onlyOneKey,
                OnlyOneAmount = onlyOneAmount,
                Request = Supply.Start(wanted, null),   // nothing to do when it's in: the craft takes as its bar ends
            };
        }

        /// <summary>From the bar's update: while this craft's chests aren't in, the bar waits just short of its end; so it
        /// does while a chest claimed beyond the server's hand-out is claimed again after the server let it go, until that
        /// claim may be used (ChestClaims.Confirming, card A64).</summary>
        internal static void HoldTheBar(InventoryGui gui, Player player, ref float dt)
        {
            if (craft == null)
            {
                return;
            }
            if (craft.Request.Finished)
            {
                // Waiting for a chest claimed far away (card A64) never lasts longer than a claim may wait (review round 4):
                // then the bar ends, and the craft says what it lacks.
                if (!ChestClaims.Confirming(craft.Request.Ready))
                {
                    craft.FarWaitSince = -1f;
                    return;
                }
                if (craft.FarWaitSince < 0f)
                {
                    craft.FarWaitSince = Time.time;
                }
                else if (Time.time - craft.FarWaitSince > ChestClaims.ConfirmTimeout)
                {
                    return;
                }
            }
            float timer = GameHooks.Read<float>(GameHooks.CraftTimer, gui);
            if (timer < 0f)
            {
                return;
            }
            float end = BarLength(gui, player) - WaitShortOfTheEnd;
            if (timer + dt >= end)
            {
                dt = Mathf.Max(0f, end - timer);
            }
        }

        // How long the game's bar runs for the craft under way (InventoryGui.UpdateRecipe).
        private static float BarLength(InventoryGui gui, Player player)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            var upgrade = GameHooks.Read<ItemDrop.ItemData>(GameHooks.CraftUpgradeItem, gui);
            int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
            float length = station != null && station.m_upgrader
                ? gui.m_upgraderDuration + quality * gui.m_upgraderDurationPerLevel
                : GameHooks.Read<bool>(GameHooks.MultiCrafting, gui) ? gui.m_multiCraftDuration : gui.m_craftDuration;
            if (station != null && station.m_craftingSkill != Skills.SkillType.None)
            {
                length *= 1f - player.GetSkillFactor(station.m_craftingSkill) * gui.m_craftDurationSkillMaxDecrease;
            }
            return length;
        }

        /// <summary>
        /// As the bar ends, before the game crafts: the checks from here to the end of DoCrafting count only what he
        /// carries and the chests held for this craft. Returns false to skip the game's crafting: a recipe that takes any
        /// one of its ingredients, with none on him that the game could find (it would fail looking for it).
        /// </summary>
        internal static bool BeforeCrafting(InventoryGui gui, Player player)
        {
            InDoCrafting = true;
            Consuming = null;
            Consumed = false;
            Refused = false;
            var recipe = GameHooks.Read<Recipe>(GameHooks.CraftRecipe, gui);
            // Strict for this recipe from here, whether or not anything was asked of the chests: he may have dropped or
            // stored what he carried at the press, or switched the mod on during the bar.
            CraftingRecipe = recipe;
            if (recipe == null || player != Player.m_localPlayer)
            {
                return true;
            }
            if (craft != null && craft.Recipe == recipe && craft.Request.Finished && !craft.Request.Cancelled)
            {
                Consuming = craft.Request;
                Consuming.Refresh();   // what the chests really hold now: one may have been a friend's in between
            }
            if (!recipe.m_requireOnlyOneIngredient || player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
            {
                return true;
            }
            var upgrade = GameHooks.Read<ItemDrop.ItemData>(GameHooks.CraftUpgradeItem, gui);
            int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
            int multiplier = GameHooks.Read<bool>(GameHooks.MultiCrafting, gui) ? gui.m_multiCraftAmount : 1;
            if (CarriesOnlyOne(player, recipe, quality, multiplier))
            {
                return true;
            }
            if (Consuming != null && craft.OnlyOneKey != null)
            {
                int moved = 0;
                try
                {
                    moved = Consuming.MoveTo(player.GetInventory(), craft.OnlyOneKey, craft.OnlyOneAmount);
                }
                finally
                {
                    Consuming.Send();   // even if another mod's handler threw mid-move: a changed chest is sent and held
                }
                if (moved > 0 && CarriesOnlyOne(player, recipe, quality, multiplier))
                {
                    return true;
                }
            }
            // Nothing on him the game could find: without this it would fail looking for it (Recipe.GetAmount).
            if (craft == null)
            {
                Messages.Center(NoLongerCarried);   // he carried it at the press, so no chest was asked
            }
            else
            {
                Messages.Refused(Consuming ?? craft.Request, "the ingredient");
            }
            InDoCrafting = false;
            Consuming = null;
            CraftingRecipe = null;
            return false;
        }

        /// <summary>After DoCrafting, whatever happened: back to counting the chests for the menu, and word if the chests
        /// were why the craft didn't happen (not when the game stopped it for a reason of its own, such as a full
        /// inventory).</summary>
        internal static void AfterCrafting()
        {
            // Under the no-cost cheat the game crafts whatever its check said, and takes nothing.
            Player player = Player.m_localPlayer;
            if (Refused && !Consumed && player != null && !player.NoCostCheat() && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
            {
                if (craft != null)
                {
                    Messages.Refused(craft.Request, "the materials");
                }
                else
                {
                    Messages.Center(NoLongerCarried);   // he carried it all at the press, so no chest was asked
                }
            }
            InDoCrafting = false;
            Consuming = null;
            CraftingRecipe = null;
            Refused = false;
            Drop();
        }

        /// <summary>
        /// Player.HaveRequirements(Recipe …) said no: yes if the chests make up what he's short of, as the game counts it
        /// (per requirement, one quality at a time), and the game's other conditions hold. Inside DoCrafting, for the craft
        /// under way and until its materials are taken, only the chests held for it count.
        /// </summary>
        internal static bool HaveWithChests(Player player, Recipe recipe, int qualityLevel, int amount)
        {
            if (!player.RequiredCraftingStation(recipe, qualityLevel, checkLevel: true))
            {
                return false;
            }
            string dlc = recipe.m_item.m_itemData.m_shared.m_dlc;
            if (dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(dlc))
            {
                return false;
            }
            // Strict only for the recipe being crafted, until its materials are taken: the game refreshes the whole recipe
            // list inside DoCrafting after a craft (UpdateCraftingPanel), and that list counts the chests as the menu does.
            // Never for a recipe that takes any one ingredient: the game takes that one straight from his inventory, not
            // through ConsumeResources (so Consumed never comes), and refuses by itself when he has none of it; the fish is
            // moved onto him first (BeforeCrafting), or the craft is stopped there with word why.
            bool strict = InDoCrafting && !Consumed && recipe == CraftingRecipe && !recipe.m_requireOnlyOneIngredient;
            if (!strict)
            {
                return Enough(player, recipe, qualityLevel, amount, null);
            }
            // No chests held for this craft: only what he carries, which the game has counted.
            bool enough = Consuming != null && Enough(player, recipe, qualityLevel, amount, Consuming);
            if (!enough)
            {
                Refused = true;   // the craft won't happen: AfterCrafting says why
            }
            return enough;
        }

        // Whether what he carries and the chests (those held for the craft, or else every chest near him, through the
        // survey) make up the recipe's materials as the game counts them: per requirement, one quality at a time; any one
        // of them for a recipe that takes any one.
        private static bool Enough(Player player, Recipe recipe, int qualityLevel, int amount, Request held)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if ((station != null && station.m_upgrader != requirement.m_upgraderResource) || (station == null && requirement.m_upgraderResource) || !requirement.m_resItem)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
                int need = requirement.GetAmount(qualityLevel) * amount;
                int best = 0;
                for (int quality = 1; quality < shared.m_maxQuality + 1; quality++)
                {
                    string key = Stock.KeyAt(shared.m_name, quality, shared.m_maxQuality);
                    int have = inventory.CountItems(shared.m_name, quality) + (held != null ? held.Count(key) : Stock.At(shared.m_name, quality, shared.m_maxQuality));
                    best = Mathf.Max(best, have);
                }
                if (recipe.m_requireOnlyOneIngredient)
                {
                    if (best >= need)
                    {
                        return true;
                    }
                }
                else if (best < need)
                {
                    return false;
                }
            }
            return !recipe.m_requireOnlyOneIngredient;
        }

        /// <summary>
        /// Player.GetFirstRequiredItem found no ingredient on him, for a recipe that takes any one: one from the chests, for
        /// the menu (the game reads its quality and name; nothing is taken from it). Never inside DoCrafting, where the
        /// fish must really be on him.
        /// </summary>
        internal static ItemDrop.ItemData FirstInChests(Player player, Recipe recipe, int qualityLevel, int multiplier, out int amount, out int extraAmount)
        {
            amount = 0;
            extraAmount = 0;
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if ((station != null && station.m_upgrader != requirement.m_upgraderResource) || (station == null && requirement.m_upgraderResource) || !requirement.m_resItem)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
                int need = requirement.GetAmount(qualityLevel) * multiplier;
                for (int quality = 1; quality <= shared.m_maxQuality; quality++)
                {
                    if (inventory.CountItems(shared.m_name, quality) + Stock.At(shared.m_name, quality, shared.m_maxQuality) >= need)
                    {
                        ItemDrop.ItemData sample = Stock.Sample(Stock.KeyAt(shared.m_name, quality, shared.m_maxQuality));
                        if (sample != null)
                        {
                            amount = need;
                            extraAmount = requirement.m_extraAmountOnlyOneIngredient;
                            return sample;
                        }
                    }
                }
            }
            return null;
        }

        // What a recipe (all of whose ingredients it takes) is short of, by key, so that the chests held for it pass the
        // game's own check as the bar ends. The game wants each ingredient's amount at one quality (HaveRequirementItems),
        // so an item with levels (fish, a weapon an imbued one is made from) is asked for at the lowest level that he and
        // the chests together have enough of; any other item by its name. The game then takes what he carries of it, at
        // any level, and ConsumeResources' shortfall (never more than this) comes out of those chests (Taking).
        private static void Wants(Player player, Recipe recipe, int qualityLevel, int multiplier, Dictionary<string, int> wanted)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (!Taking.Counts(requirement, station))
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
                int need = requirement.GetAmount(qualityLevel) * multiplier;
                if (need <= 0)
                {
                    continue;
                }
                string key = null;
                int amount = 0;
                bool carriesEnough = false;
                for (int quality = 1; quality <= shared.m_maxQuality && key == null; quality++)
                {
                    int carried = inventory.CountItems(shared.m_name, quality);
                    if (carried >= need)
                    {
                        carriesEnough = true;
                        break;
                    }
                    if (carried + Stock.At(shared.m_name, quality, shared.m_maxQuality) >= need)
                    {
                        key = Stock.KeyAt(shared.m_name, quality, shared.m_maxQuality);
                        amount = need - carried;
                    }
                }
                if (carriesEnough)
                {
                    continue;
                }
                if (key == null)
                {
                    // Neither he nor the chests have enough at any one level: the game's check will say no; asked by name
                    // all the same, so the message says why.
                    key = shared.m_name;
                    amount = need - inventory.CountItems(shared.m_name);
                }
                if (amount > 0)
                {
                    wanted.TryGetValue(key, out int sum);
                    wanted[key] = sum + amount;
                }
            }
        }

        // A recipe that takes any one ingredient: true if what he carries has one, as the game looks (GetFirstRequiredItem).
        private static bool CarriesOnlyOne(Player player, Recipe recipe, int qualityLevel, int multiplier)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if ((station != null && station.m_upgrader != requirement.m_upgraderResource) || (station == null && requirement.m_upgraderResource) || !requirement.m_resItem)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
                int need = requirement.GetAmount(qualityLevel) * multiplier;
                for (int quality = 0; quality <= shared.m_maxQuality; quality++)
                {
                    if (inventory.CountItems(shared.m_name, quality) >= need)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // A recipe that takes any one ingredient, and he carries none the game would find: the first the chests make up,
        // in the game's own order (the recipe's order, then the lowest quality), by key, and how many more of it he needs.
        private static bool OnlyOneShort(Player player, Recipe recipe, int qualityLevel, int multiplier, out string key, out int amount)
        {
            key = null;
            amount = 0;
            if (CarriesOnlyOne(player, recipe, qualityLevel, multiplier))
            {
                return false;
            }
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if ((station != null && station.m_upgrader != requirement.m_upgraderResource) || (station == null && requirement.m_upgraderResource) || !requirement.m_resItem)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
                int need = requirement.GetAmount(qualityLevel) * multiplier;
                for (int quality = 1; quality <= shared.m_maxQuality; quality++)
                {
                    int carried = inventory.CountItems(shared.m_name, quality);
                    if (carried + Stock.At(shared.m_name, quality, shared.m_maxQuality) >= need)
                    {
                        key = Stock.KeyAt(shared.m_name, quality, shared.m_maxQuality);
                        amount = need - carried;
                        return amount > 0;
                    }
                }
            }
            return false;
        }
    }

    // The game's crafting, patched. Each patch asks whether the mod is on first and changes nothing while it's off.

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) })]
    internal static class CountRecipeWithChests
    {
        private static void Postfix(Player __instance, Recipe recipe, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (__result || discover || !Crafting.On || __instance != Player.m_localPlayer || recipe == null || recipe.m_item == null)
            {
                return;
            }
            bool result = false;
            Module.Guard("counting a recipe's materials in the chests", () => result = Crafting.HaveWithChests(__instance, recipe, qualityLevel, amount));
            __result = result;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
    internal static class FirstIngredientInChests
    {
        private static void Postfix(Player __instance, Inventory inventory, Recipe recipe, int qualityLevel, ref int amount, ref int extraAmount, int craftMultiplier,
                                    ref ItemDrop.ItemData __result)
        {
            if (__result != null || !Crafting.On || Crafting.InDoCrafting || __instance != Player.m_localPlayer || recipe == null ||
                inventory != __instance.GetInventory())
            {
                return;
            }
            // Under the no-cost cheat, or in a world whose crafting costs nothing, nothing is asked of the chests, so the
            // fish would never be on him as the bar ends and the game would fail there every frame: the game's own way,
            // instead, which fails once at the press.
            if (__instance.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)))
            {
                return;
            }
            ItemDrop.ItemData found = null;
            int foundAmount = 0, foundExtra = 0;
            Module.Guard("looking in the chests for a recipe's ingredient", () => found = Crafting.FirstInChests(__instance, recipe, qualityLevel, craftMultiplier, out foundAmount, out foundExtra));
            if (found != null)
            {
                __result = found;
                amount = foundAmount;
                extraAmount = foundExtra;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
    internal static class AskChestsAtTheCraftButton
    {
        private static void Prefix(InventoryGui __instance, out bool __state)
        {
            __state = GameHooks.CraftTimer != null && GameHooks.Read<float>(GameHooks.CraftTimer, __instance) >= 0f;
        }

        private static void Postfix(InventoryGui __instance, bool __state)
        {
            // Only a press while the bar was idle. Crafting an amount first (card B16): it decides whether this craft is the
            // game's x5 craft, which the chests are then asked for.
            if (__state || GameHooks.CraftTimer == null)
            {
                return;
            }
            bool started = GameHooks.Read<float>(GameHooks.CraftTimer, __instance) >= 0f;
            if (Batches.On || Batches.Running)
            {
                Module.Guard("starting a run of crafts", () => Batches.Pressed(__instance, started));
            }
            // Only when this press started a craft (the bar was idle and now runs).
            if (!started || !Crafting.On)
            {
                return;
            }
            Module.Guard("asking the chests for a craft's materials", () => Crafting.Started(__instance));
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    internal static class WaitForTheChests
    {
        private static void Prefix(InventoryGui __instance, Player player, ref float dt)
        {
            if (!Crafting.On || player == null)
            {
                return;
            }
            float step = dt;
            Module.Guard("holding the crafting bar for the chests", () => Crafting.HoldTheBar(__instance, player, ref step));
            dt = step;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class CraftFromHeldChests
    {
        private static bool Prefix(InventoryGui __instance, Player player, bool __runOriginal)
        {
            if (!__runOriginal || !Crafting.On)
            {
                return true;
            }
            bool craft = true;
            Module.Guard("crafting from chests", () => craft = Crafting.BeforeCrafting(__instance, player));
            return craft;
        }

        private static void Finalizer()
        {
            if (Crafting.InDoCrafting || Crafting.Consuming != null)
            {
                Module.Guard("finishing a craft from chests", Crafting.AfterCrafting);
            }
            Crafting.Reset();
        }
    }
}
