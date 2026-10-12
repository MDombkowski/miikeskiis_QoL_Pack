using System;
using HarmonyLib;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// Crafting an amount (card B16, his word of 2026-10-11): a number beside the Craft button (BatchPanel), and one press
    /// of Craft makes that many, one craft after another, each pressed through the game's own Craft button handler so the
    /// game, and crafting from chests, do each craft as they always do. While a whole batch is left and the materials and
    /// the inventory room for it are there, a craft is the game's own x5 craft (m_multiCrafting, set right after the press,
    /// before crafting from chests asks the chests for it), so its growing bonus is rolled; else a single craft. 41 is
    /// eight x5 crafts and one single. A run stops when he cancels, closes the menu, picks another recipe or leaves the
    /// station, when a craft didn't happen, or when not even one more can be made. Upgrades are never run.
    /// After an x5 craft (his or the game's own Shift + Craft), the skill raise of five is made as five raises of one
    /// (the skill kept whole), since the game throws away what a raise has left over at a level-up.
    /// The amount is also what the recipe's materials show (card B17: Display, through LabelMultiplier).
    /// </summary>
    internal static class Batches
    {
        private const string OutOfMaterials = "out of materials";

        /// <summary>The number beside the Craft button while no run goes.</summary>
        private static int amount = 1;

        private static Run run;

        /// <summary>Inside the run's own press of the Craft button.</summary>
        private static bool pressing;

        private static int lastWheelFrame;

        // The recipe and variant the number was set for: another one puts it back to 1.
        private static Recipe lastTarget;
        private static int lastVariant = -1;

        // How many he can make of the recipe shown, worked out at most every quarter second.
        private static Recipe makeableRecipe;
        private static CraftingStation makeableStation;
        private static int makeableMost;
        private static int makeableCount = 1;
        private static float makeableUntil;

        /// <summary>Inside a press of the Craft button that makes a run's craft: the game's own x5 request (Shift, the left
        /// stick) is hidden from it (ShiftIsTheRunsOwn), so the run, not Shift, decides x5 or single.</summary>
        internal static bool HidingBatchButtons;

        // The skill raise to split, armed inside DoCrafting for an x5 craft (None: nothing armed).
        private static Skills.SkillType splitSkill = Skills.SkillType.None;
        private static int splitInto;
        private static bool splitting;

        private sealed class Run
        {
            internal RunTally Tally;
            internal Recipe Recipe;
            internal CraftingStation Station;
            internal int Variant;

            /// <summary>The size of the craft whose bar runs (0: none).</summary>
            internal int InBar;

            /// <summary>The last craft was made and more are left: press Craft for the next.</summary>
            internal bool PressNext;

            /// <summary>Inside DoCrafting for this run's craft, and how many of the item he had before it.</summary>
            internal bool Watching;

            internal int CountBefore;
        }

        /// <summary>Crafting an amount is on: the module and its setting are, the game's members were found, and it doesn't
        /// stand aside for MyLittleUI's Multicraft.</summary>
        internal static bool On => Module.IsOn && GameHooks.Batches && Settings.Batches.Value && !OtherMulticraft.OursStandsAside;

        /// <summary>The number the panel shows: while a run goes, what is still to make.</summary>
        internal static int Amount => run != null ? run.Tally.Remaining : amount;

        internal static bool Running => run != null;

        /// <summary>An x5 craft's skill raise is waiting to be split (and this isn't one of the split raises).</summary>
        internal static bool SplitArmed => splitSkill != Skills.SkillType.None && !splitting;

        /// <summary>The highest the number may go now: the largest amount, and no more than he can make of the recipe shown
        /// (at least 1).</summary>
        internal static int Ceiling
        {
            get
            {
                int most = Settings.MostAmount.Value;
                Player player = Player.m_localPlayer;
                Recipe recipe = Target(InventoryGui.instance, player);
                return recipe == null ? most : Mathf.Clamp(MakeableSoon(player, recipe, most), 1, most);
            }
        }

        /// <summary>Takes everything back: no run, the number at 1, nothing armed. An undo step.</summary>
        internal static void Reset()
        {
            run = null;
            amount = 1;
            pressing = false;
            HidingBatchButtons = false;
            splitSkill = Skills.SkillType.None;
            splitting = false;
            lastTarget = null;
            lastVariant = -1;
            makeableRecipe = null;
        }

        /// <summary>From the start of a press of the Craft button: true when it makes a run's craft (the run's own press, a
        /// press while a run goes, or his press with the number above 1 on a recipe it applies to).</summary>
        internal static bool PressMakesARun(InventoryGui gui)
        {
            if (pressing || run != null)
            {
                return true;
            }
            return On && amount > 1 && Target(gui, Player.m_localPlayer) != null;
        }

        // The recipe the number applies to: the one picked in the crafting tab, a new item (never an upgrade), at a station
        // that isn't an upgrader; null for anything else.
        internal static Recipe Target(InventoryGui gui, Player player)
        {
            if (gui == null || player == null || !InventoryGui.IsVisible() || !gui.InCraftTab())
            {
                return null;
            }
            object picked = GameHooks.SelectedRecipe.GetValue(gui);
            var recipe = (Recipe)GameHooks.SelectedRecipeRecipe.GetValue(picked, null);
            if (recipe == null || recipe.m_item == null || GameHooks.SelectedRecipeItem.GetValue(picked, null) != null)
            {
                return null;
            }
            CraftingStation station = player.GetCurrentCraftingStation();
            return station != null && station.m_upgrader ? null : recipe;
        }

        /// <summary>Whether the panel shows now: on, no run going, a recipe it applies to, and the Craft button showing
        /// (the game hides it while a bar runs, and the panel with it).</summary>
        internal static bool PanelWanted(InventoryGui gui)
        {
            if (!On || run != null || gui == null || gui.m_craftButton == null || !gui.m_craftButton.gameObject.activeInHierarchy)
            {
                return false;
            }
            return Target(gui, Player.m_localPlayer) != null;
        }

        /// <summary>The multiplier the recipe's materials show (card B17): the whole amount, while it is above 1 and applies
        /// to the recipe shown; else the game's own (5 with Shift held, else 1).</summary>
        internal static int LabelMultiplier(int gameMultiplier)
        {
            if (!On || !Settings.WholeAmount.Value)
            {
                return gameMultiplier;
            }
            int whole = Amount;
            if (whole <= 1)
            {
                return gameMultiplier;
            }
            Recipe target = Target(InventoryGui.instance, Player.m_localPlayer);
            if (target == null || (run != null && run.Recipe != target))
            {
                return gameMultiplier;
            }
            return whole;
        }

        /// <summary>One step of the + or - (or the wheel), or, with <paramref name="mostOrOne"/> or the most key held, the
        /// most he can make (up) or 1 (down).</summary>
        internal static void Change(int direction, bool mostOrOne)
        {
            if (!On || run != null)
            {
                return;
            }
            int most = Settings.MostAmount.Value;
            if (mostOrOne || Settings.Held(Settings.MostKey.Value))
            {
                if (direction < 0)
                {
                    amount = 1;
                    return;
                }
                InventoryGui gui = InventoryGui.instance;
                Player player = Player.m_localPlayer;
                Recipe recipe = Target(gui, player);
                amount = BatchMath.Most(recipe == null ? 1 : Makeable(player, recipe, most), most);
                return;
            }
            int stepped = BatchMath.Step(amount, direction, Settings.Held(Settings.StepFiveKey.Value), Settings.StepsOnFives.Value, most);
            amount = direction > 0 ? Mathf.Min(stepped, Mathf.Max(amount, Ceiling)) : stepped;
        }

        // While no run goes: another recipe or variant puts the number back to 1 (40 arrows never become 40 swords at one
        // click), and the number never stays above what he can make.
        private static void FollowSelection(InventoryGui gui, Player player)
        {
            Recipe target = Target(gui, player);
            int variant = target == null ? -1 : GameHooks.Read<int>(GameHooks.SelectedVariant, gui);
            if (target != lastTarget || variant != lastVariant)
            {
                lastTarget = target;
                lastVariant = variant;
                amount = 1;
                return;
            }
            if (amount > 1 && target != null)
            {
                amount = Mathf.Min(amount, Mathf.Clamp(MakeableSoon(player, target, Settings.MostAmount.Value), 1, Settings.MostAmount.Value));
            }
        }

        // Makeable, kept for a quarter second per recipe and station (the menu asks every frame).
        private static int MakeableSoon(Player player, Recipe recipe, int most)
        {
            CraftingStation station = player == null ? null : player.GetCurrentCraftingStation();
            if (recipe != makeableRecipe || station != makeableStation || most != makeableMost || Time.unscaledTime >= makeableUntil)
            {
                makeableRecipe = recipe;
                makeableStation = station;
                makeableMost = most;
                makeableCount = Makeable(player, recipe, most);
                makeableUntil = Time.unscaledTime + 0.25f;
            }
            return makeableCount;
        }

        /// <summary>Every frame: follows the run (the next craft, or its end) and the wheel over the number.</summary>
        internal static void Update()
        {
            if (run == null && !On)
            {
                return;
            }
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (run != null)
            {
                if (!On || gui == null || player == null || !InventoryGui.IsVisible())
                {
                    End(null);   // switched off, or the menu closed: the craft under way, if any, finishes as the game's own
                                 // craft it was started as (an x5 stays an x5), and no more follow
                }
                else
                {
                    FollowRun(gui, player);
                }
            }
            else if (gui != null && player != null)
            {
                FollowSelection(gui, player);
            }
            if (On && BatchPanel.Hovered && BatchPanel.Showing)
            {
                float wheel = ZInput.GetMouseScrollWheel();
                if (wheel != 0f && Time.frameCount - lastWheelFrame > 2)
                {
                    lastWheelFrame = Time.frameCount;
                    Change(wheel > 0f ? 1 : -1, false);
                }
            }
        }

        private static void FollowRun(InventoryGui gui, Player player)
        {
            float timer = GameHooks.Read<float>(GameHooks.CraftTimer, gui);
            if (run.InBar > 0)
            {
                if (timer < 0f && !run.Watching)
                {
                    End(null);   // the bar ended without crafting: cancelled, or the game stopped it
                }
                return;
            }
            if (!run.PressNext || timer >= 0f)
            {
                return;
            }
            // The next craft: the same recipe and variant, at the same station, and only while one more can be made.
            if (Target(gui, player) != run.Recipe || player.GetCurrentCraftingStation() != run.Station ||
                GameHooks.Read<int>(GameHooks.SelectedVariant, gui) != run.Variant)
            {
                End(null);
                return;
            }
            string cannot = CannotCraftOne(player, run.Recipe);
            if (cannot != null)
            {
                End(cannot);
                return;
            }
            run.PressNext = false;
            pressing = true;
            try
            {
                GameHooks.CraftPressed.Invoke(gui, null);
            }
            finally
            {
                pressing = false;
            }
        }

        /// <summary>
        /// From the Craft button, after the game handled the press (his, or the run's own), before crafting from chests asks
        /// the chests: starts a run when the number is above 1, and decides whether this craft is the game's x5 craft or a
        /// single one. <paramref name="started"/>: the press started the bar (the game refuses a craft whose product
        /// wouldn't fit, and says so).
        /// </summary>
        internal static void Pressed(InventoryGui gui, bool started)
        {
            if (!started)
            {
                if (pressing && run != null)
                {
                    End(null);   // the game said why
                }
                return;
            }
            Player player = Player.m_localPlayer;
            var recipe = GameHooks.Read<Recipe>(GameHooks.CraftRecipe, gui);
            var upgrade = GameHooks.Read<ItemDrop.ItemData>(GameHooks.CraftUpgradeItem, gui);
            if (run == null)
            {
                if (!On || pressing || amount <= 1 || upgrade != null || recipe == null || player == null || Target(gui, player) != recipe)
                {
                    return;   // the game's own craft, Shift + Craft's x5 included
                }
                run = new Run
                {
                    Tally = new RunTally(amount),
                    Recipe = recipe,
                    Station = player.GetCurrentCraftingStation(),
                    Variant = GameHooks.Read<int>(GameHooks.CraftVariant, gui),
                };
            }
            else if (recipe != run.Recipe || upgrade != null || player == null)
            {
                End(null);
                return;
            }
            int batch = gui.m_multiCraftAmount;
            bool tryBatch = Settings.BatchesOfFive.Value && batch > 1 && run.Tally.Remaining >= batch;
            int size = BatchMath.NextSize(run.Tally.Remaining, batch, Settings.BatchesOfFive.Value, tryBatch && CanBatch(gui, player, recipe, batch));
            GameHooks.MultiCrafting.SetValue(gui, size > 1);
            run.InBar = size;
            run.PressNext = false;
        }

        /// <summary>From the cancel button: the run ends with the craft under way.</summary>
        internal static void Cancelled() => End(null);

        /// <summary>Inside DoCrafting, before the game crafts (after crafting from chests' own check): notes how many of the
        /// item he has, to tell whether the craft happened, and arms the skill split for an x5 craft.</summary>
        internal static void BeforeCraft(InventoryGui gui, Player player, bool crafts)
        {
            var recipe = GameHooks.Read<Recipe>(GameHooks.CraftRecipe, gui);
            bool multi = GameHooks.Read<bool>(GameHooks.MultiCrafting, gui);
            splitSkill = Skills.SkillType.None;
            if (crafts && multi && Module.IsOn && Settings.SkillWhole.Value && recipe != null && recipe.m_craftingStation != null &&
                recipe.m_craftingStation.m_craftingSkill != Skills.SkillType.None && gui.m_multiCraftAmount > 1)
            {
                splitSkill = recipe.m_craftingStation.m_craftingSkill;
                splitInto = gui.m_multiCraftAmount;
            }
            if (run == null || run.InBar == 0 || recipe != run.Recipe || player == null)
            {
                return;
            }
            run.Watching = true;
            run.CountBefore = player.GetInventory().CountItems(recipe.m_item.m_itemData.m_shared.m_name, -1, false);
        }

        /// <summary>After DoCrafting, whatever happened: counts the craft, then presses for the next at the next frame, or
        /// ends the run (made them all, or the craft didn't happen).</summary>
        internal static void AfterCraft(Player player)
        {
            splitSkill = Skills.SkillType.None;
            if (run == null || !run.Watching)
            {
                return;
            }
            run.Watching = false;
            int size = run.InBar;
            run.InBar = 0;
            bool made = player != null && player.GetInventory().CountItems(run.Recipe.m_item.m_itemData.m_shared.m_name, -1, false) > run.CountBefore;
            if (!made)
            {
                End(player == null ? null : CannotCraftOne(player, run.Recipe) ?? (NoRoom(player, run.Recipe) ? "no room in your inventory" : "a craft didn't happen"));
                return;
            }
            if (run.Tally.Made(size))
            {
                run.PressNext = true;
            }
            else
            {
                End(null);   // all made
            }
        }

        /// <summary>
        /// From the skills' raise: true when this raise is the one to make in single steps (the first raise of the armed
        /// skill inside an x5 craft's DoCrafting, worth splitting), and disarms it; every other raise goes through as the
        /// game makes it. The caller then skips the game's raise before making the steps, so an error in a step can never
        /// add the game's whole raise on top.
        /// </summary>
        internal static bool ClaimRaise(Skills.SkillType skill, float factor)
        {
            if (splitting || splitSkill == Skills.SkillType.None || skill != splitSkill)
            {
                return false;
            }
            splitSkill = Skills.SkillType.None;   // once per craft
            return BatchMath.SplitRaise(factor, splitInto, out _) > 1;
        }

        /// <summary>The claimed raise, made as single steps through the game's own raise.</summary>
        internal static void RaiseInSteps(Skills skills, Skills.SkillType skill, float factor)
        {
            int times = BatchMath.SplitRaise(factor, splitInto, out float each);
            splitting = true;
            try
            {
                for (int i = 0; i < times; i++)
                {
                    skills.RaiseSkill(skill, each);
                }
            }
            finally
            {
                splitting = false;
            }
        }

        // Ends the run: the number goes back (to 1, or to the run's start with "Keep the amount"), and with a reason, a line
        // at the top left with how many it made.
        private static void End(string why)
        {
            if (run == null)
            {
                return;
            }
            RunTally tally = run.Tally;
            run = null;
            amount = Settings.KeepAmount != null && Settings.KeepAmount.Value ? Mathf.Max(1, tally.Start) : 1;
            if (why != null && Settings.StopMessage.Value)
            {
                Messages.TopLeft($"Crafting stopped after {tally.Done} of {tally.Start}: {why}.");
            }
        }

        // Why not even one more can be made now, or null: the materials (counting the chests, as the menu does), or the
        // station (out of reach, or not usable).
        private static string CannotCraftOne(Player player, Recipe recipe)
        {
            CraftingStation required = recipe.GetRequiredStation(1);
            CraftingStation current = player.GetCurrentCraftingStation();
            if (required != null && (current == null || !current.CheckUsable(player, false)))
            {
                return "the station can't be used";
            }
            if (Free(player))
            {
                return null;
            }
            return player.HaveRequirements(recipe, false, 1, 1) ? null : OutOfMaterials;
        }

        // Whether one craft's product wouldn't fit.
        private static bool NoRoom(Player player, Recipe recipe) => !player.GetInventory().CanAddItem(recipe.m_item.gameObject, Mathf.Max(1, recipe.m_amount));

        private static bool Free(Player player) =>
            player.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));

        // Whether this craft can be the game's batch: the materials for all of it (counting the chests, as the menu does),
        // and the room for all it makes with the largest bonus it could roll.
        private static bool CanBatch(InventoryGui gui, Player player, Recipe recipe, int batch)
        {
            if (!Free(player) && !player.HaveRequirements(recipe, false, 1, batch))
            {
                return false;
            }
            Inventory inventory = player.GetInventory();
            int makes;
            if (recipe.m_requireOnlyOneIngredient)
            {
                // The game finds the one ingredient it will use; with none, it couldn't craft at all (and GetAmount fails).
                if (player.GetFirstRequiredItem(inventory, recipe, 1, out _, out _, batch) == null)
                {
                    return false;
                }
                makes = recipe.GetAmount(1, out _, out _, batch);
            }
            else
            {
                makes = recipe.m_amount * batch;
            }
            CraftingStation station = player.GetCurrentCraftingStation();
            if (station != null && station.m_craftingSkill != Skills.SkillType.None && recipe.m_item.m_itemData.m_shared.m_maxStackSize > 1)
            {
                makes += BatchMath.MostBonus(batch, gui.m_craftBonusAmount);
            }
            return inventory.CanAddItem(recipe.m_item.gameObject, Mathf.Max(1, makes));
        }

        // How many he can make, counting the chests as the menu does (the game's own check, halved each time), up to most.
        private static int Makeable(Player player, Recipe recipe, int most)
        {
            if (player == null)
            {
                return 1;
            }
            if (Free(player))
            {
                return most;
            }
            if (!player.HaveRequirements(recipe, false, 1, 1))
            {
                return 1;
            }
            if (recipe.m_requireOnlyOneIngredient)
            {
                return OnlyOneMakeable(player, recipe, most);
            }
            int low = 1, high = Mathf.Max(1, most);
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (player.HaveRequirements(recipe, false, 1, middle))
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return low;
        }

        // A recipe that takes any one of its ingredients (the food table's fish): the game's check counts one ingredient at a
        // time, so every alternative's crafts are added up, as MyLittleUI's Multicraft does, each counted as the menu counts
        // it (what he carries, and the chests while crafting from them is on), with the game's own filter of the list.
        private static int OnlyOneMakeable(Player player, Recipe recipe, int most)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            bool chests = Crafting.On;
            int total = 0;
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null ||
                    (station != null && station.m_upgrader != requirement.m_upgraderResource) || (station == null && requirement.m_upgraderResource))
                {
                    continue;
                }
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int need = requirement.GetAmount(1);
                if (need <= 0 || !player.IsKnownMaterial(name))
                {
                    continue;
                }
                int have = inventory.CountItems(name) + (chests ? Stock.Of(name) : 0);
                total += have / need;
                if (total >= most)
                {
                    return most;
                }
            }
            return Mathf.Max(1, total);
        }
    }

    // The game's crafting, patched for crafting an amount. Each patch asks whether it's on first (or follows only a run
    // that was started while it was) and changes nothing otherwise. The press itself is followed by crafting from chests'
    // patch on OnCraftPressed (Crafting.cs), which hands it here first.

    // Shift (the game's AltPlace) steps the number by five, and the game's own press turns on its x5 craft while AltPlace,
    // the left stick or the touch toggle is held, refusing the press if five won't fit. So a press that makes a run's craft
    // hides those from the game for the length of the press: the game starts a single craft, and the run decides (Batches.
    // Pressed) whether it becomes an x5.
    [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
    internal static class ShiftIsTheRunsOwn
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(InventoryGui __instance)
        {
            if (!Module.IsOn && !Batches.Running)
            {
                return;
            }
            Module.Guard("starting a run of crafts", () =>
            {
                if (Batches.PressMakesARun(__instance))
                {
                    Batches.HidingBatchButtons = true;
                    GameHooks.TouchMultiCrafting?.SetValue(__instance, false);
                }
            });
        }

        private static void Finalizer() => Batches.HidingBatchButtons = false;
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton), new[] { typeof(string) })]
    internal static class HideTheBatchButtons
    {
        // Every button read in the game comes through here: only inside a run's press of Craft does it change anything.
        private static bool Prefix(string name, ref bool __result)
        {
            if (!Batches.HidingBatchButtons || (name != "AltPlace" && name != "JoyLStick"))
            {
                return true;
            }
            __result = false;
            return false;
        }

        // Keeps a failure to this one class (the kit takes every class off at the first failure): without it, a run's press
        // with Shift held is the game's x5 as before, and the rest of the mod runs.
        private static Exception Cleanup(System.Reflection.MethodBase original, Exception ex, Harmony harmony)
        {
            if (ex == null)
            {
                return null;
            }
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way; the press then sees Shift, as the game does.
            }
            try
            {
                Module.Log.LogWarning("Couldn't keep Shift from the game's own x5 at a run's press of Craft: with Shift held, a run's " +
                                      $"craft can be the game's x5 or refused when five won't fit.\n{ex}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnCraftCancelPressed")]
    internal static class EndTheRunAtCancel
    {
        private static void Prefix()
        {
            if (Batches.Running)
            {
                Module.Guard("ending a run of crafts", Batches.Cancelled);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class FollowEachCraft
    {
        // After crafting from chests' own prefix (which may skip the craft), so it knows whether the game crafts.
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(InventoryGui __instance, Player player, bool __runOriginal)
        {
            if (!Module.IsOn)
            {
                return;
            }
            Module.Guard("following a run of crafts", () => Batches.BeforeCraft(__instance, player, __runOriginal));
        }

        private static void Finalizer()
        {
            Module.Guard("following a run of crafts", () => Batches.AfterCraft(Player.m_localPlayer));
        }
    }

    [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill), new[] { typeof(Skills.SkillType), typeof(float) })]
    internal static class KeepTheSkillWhole
    {
        // First, so a mod that scales the factor in its own prefix scales only the single steps (each passes through it
        // again), never the whole raise as well.
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Skills __instance, Skills.SkillType skillType, float factor)
        {
            // Every raise in the game comes through here: only one inside an x5 craft's DoCrafting goes further.
            if (!Batches.SplitArmed || !Module.IsOn)
            {
                return true;
            }
            bool raise = true;
            Module.Guard("raising a crafting skill in single steps", () =>
            {
                if (Batches.ClaimRaise(skillType, factor))
                {
                    raise = false;   // before the steps: even if one throws, never the game's raise of five as well
                    Batches.RaiseInSteps(__instance, skillType, factor);
                }
            });
            return raise;
        }
    }
}
