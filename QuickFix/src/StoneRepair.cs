using System;
using System.Collections.Generic;
using QoLMods.Kit;
using UnityEngine;

namespace QuickFix
{
    /// <summary>
    /// The Sharpening Stone in the field (decisions 7 to 13; research part C). A press of the stone (its hotbar key, the
    /// gamepad's hotbar, a right-click in his own inventory) fixes the items and the bar's length, and the bar runs in the
    /// game's own action bar, on the game's own tick, while nothing of the game's runs first; when it fills, the cost is
    /// paid and then the items are repaired by their share. A second press, the game's own cancels (an attack, a sprint, a
    /// jump, a dodge), an attack press, losing the stone, a teleport, a ship's helm or losing his footing in water stop
    /// it; moving and being staggered too, by setting; dying, the mod switched off and the stone's setting turned off stop
    /// it at no cost. One repair at a time, the local player's only.
    /// </summary>
    internal static class StoneRepair
    {
        /// <summary>An item of the repair, with what finds it again if the game replaced it with a copy (a move to another
        /// cell clones it: Inventory.AddItem into an empty cell).</summary>
        private struct Entry
        {
            internal ItemDrop.ItemData Item;
            internal string Name;
            internal int Quality;
            internal int WorldLevel;
            internal int Variant;
            internal long Crafter;
            internal float Durability;

            internal Entry(ItemDrop.ItemData item)
            {
                Durability = item.m_durability;
                Item = item;
                Name = item.m_shared.m_name;
                Quality = item.m_quality;
                WorldLevel = item.m_worldLevel;
                Variant = item.m_variant;
                Crafter = item.m_crafterID;
            }

            /// <summary>The same item moved to another cell: a copy keeps every one of these, its durability exactly, so
            /// an identical item the repair left out (for its cost) is never taken for it.</summary>
            internal bool Same(ItemDrop.ItemData other) =>
                other != null && other.m_shared != null && other.m_shared.m_name == Name && other.m_quality == Quality &&
                other.m_worldLevel == WorldLevel && other.m_variant == Variant && other.m_crafterID == Crafter &&
                other.m_durability == Durability;
        }

        private static bool running;
        private static Player who;
        private static ItemDrop.ItemData firstStone;
        private static float elapsed;
        private static float duration;
        private static RepairLook look;
        private static string label;
        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly Player.MinorActionData bar = new Player.MinorActionData();

        // What the patches pass to the guarded work, so no closure is made every physics step.
        private static Player stepPlayer;
        private static float stepTime;
        private static Vector3 stepMove;
        private static bool stepAttack;
        private static bool stepAutoRun;
        internal static readonly Action TickStep = () => Tick(stepPlayer, stepTime);
        internal static readonly Action ControlsStep = () => Controls(stepPlayer, stepMove, stepAttack, stepAutoRun);

        /// <summary>Whether <paramref name="player"/>'s repair runs now (and the mod is on: every patch asks this first).</summary>
        internal static bool IsHis(Player player) => running && Module.IsOn && player != null && ReferenceEquals(player, who);

        internal static void SetStep(Player player, float dt)
        {
            stepPlayer = player;
            stepTime = dt;
        }

        internal static void SetControls(Player player, Vector3 movedir, bool attack, bool autoRun)
        {
            stepPlayer = player;
            stepMove = movedir;
            stepAttack = attack;
            stepAutoRun = autoRun;
        }

        /// <summary>A press of the stone: starts a repair, or cancels the one running.</summary>
        internal static void Press(Player player, ItemDrop.ItemData stone)
        {
            if (running)
            {
                Cancel(player, "Repair cancelled");
                return;
            }
            if (Refused(player, out string why))
            {
                if (why != null)
                {
                    Say.Center(why);
                }
                return;
            }

            Inventory inventory = player.GetInventory();
            int share = (int)Settings.Share.Value;
            StoneChoice choice = Settings.StoneChoice();
            var worn = new List<ItemDrop.ItemData>();
            var durability = new List<float>();
            var max = new List<float>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!Gear.CanBeRepaired(item, out Recipe recipe) || !Gear.Worn(item))
                {
                    continue;
                }
                if (!Rules.StoneTakes(Gear.PrefabOf(item), Gear.KindsOf(item), Gear.IsMetallic(recipe), choice))
                {
                    continue;
                }
                if (Rules.Gained(item.m_durability, item.GetMaxDurability(), share) <= Rules.Tiny)
                {
                    continue;
                }
                worn.Add(item);
                durability.Add(item.m_durability);
                max.Add(item.GetMaxDurability());
            }
            if (worn.Count == 0)
            {
                Say.Center("Nothing for the Sharpening Stone to repair");
                return;
            }

            StoneCost cost = Settings.Cost.Value;
            float stoneMax = StoneWear.Max(stone);
            int stones = StoneWear.Count(inventory);
            float available = Rules.Available(StoneWear.Read(StoneWear.Stacks(inventory, stone)), stoneMax);
            int affordable = Rules.Affordable(cost, stones, available, stoneMax, Settings.PerUse.Value, Settings.PerItem.Value, worn.Count);
            if (affordable <= 0)
            {
                Say.Center("Your Sharpening Stones are too worn for this repair");
                return;
            }

            entries.Clear();
            List<int> order = Rules.MostWornFirst(durability, max);
            for (int i = 0; i < affordable && i < order.Count; i++)
            {
                entries.Add(new Entry(worn[order[i]]));
            }
            running = true;
            who = player;
            firstStone = stone;
            elapsed = 0f;
            duration = Rules.Duration(Settings.Time.Value, Settings.SecondsPerItem.Value, Settings.FlatSeconds.Value, entries.Count);
            look = Settings.Look.Value;
            label = $"Repairing {Gear.Items(entries.Count)}";
            bar.m_progressText = label;
            bar.m_duration = Mathf.Max(duration, 0.51f);   // the game hides its bar for 0.5 s or less (Hud.UpdateActionProgress)
            bar.m_item = null;
            bar.m_type = Player.MinorActionData.ActionType.Reload;   // the radial menu fills an item only for Equip and Unequip
            if (player.m_equipStartEffects != null)
            {
                player.m_equipStartEffects.Create(player.transform.position, Quaternion.identity, player.transform);
            }
            Look(true);
            if (affordable < worn.Count)
            {
                Say.Center($"Your Sharpening Stones pay for {Gear.Items(affordable)} of {worn.Count}: the most worn first");
            }
        }

        /// <summary>The game's own tick (a postfix on Player.UpdateActionQueue: the owner's, while alive, stopped by the
        /// game's pause). The bar holds while the game's own equip, reload or its short pause after one runs, and during an
        /// attack, as the game's own queue does; meanwhile the repair's motion gives way to the game's.</summary>
        internal static void Tick(Player player, float dt)
        {
            if (!IsHis(player))
            {
                return;
            }
            string stop = StopsIt(player);
            if (stop != null)
            {
                Cancel(player, stop);
                return;
            }
            if (player.GetActionQueueCount() > 0 || player.InAttack() || GameHooks.ActionQueuePause(player) > 0f)
            {
                GiveWay(player);
                return;
            }
            Look(true);
            elapsed += dt;
            if (elapsed >= duration)
            {
                Complete(player);
            }
        }

        /// <summary>Fills the game's action bar while nothing of the game's is in it (a postfix on
        /// Player.GetActionProgress).</summary>
        internal static void Bar(Player player, ref string name, ref float progress, ref Player.MinorActionData data)
        {
            if (!IsHis(player) || name != null)
            {
                return;
            }
            name = label;
            progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            data = bar;
        }

        /// <summary>The player's controls, every physics step (a postfix on Player.SetControls): an attack press stops the
        /// repair (the motion may keep the game from starting the attack, so its own cancel never comes), but not placing
        /// a piece in build mode, which never cancels the game's own equip; moving with "Moving cancels", autorun too (its key,
        /// and Player.m_autoRun once it's on: then the controls' direction is zero while he moves).</summary>
        internal static void Controls(Player player, Vector3 movedir, bool attack, bool autoRun)
        {
            if (!IsHis(player))
            {
                return;
            }
            if (attack && !player.InPlaceMode())
            {
                Cancel(player, "Repair cancelled");
            }
            else if (Settings.CancelOnMove.Value && (movedir.sqrMagnitude > 0.01f || autoRun || player.m_autoRun))
            {
                Cancel(player, "Repair cancelled: you moved");
            }
        }

        /// <summary>Every frame while the module runs: a repair whose player is gone, dead or no longer this game's
        /// stops, costing nothing; and so does one when the mod or the stone's setting is switched off.</summary>
        internal static void Watch(bool on)
        {
            if (!running)
            {
                return;
            }
            if (!on || !Settings.Stone.Value || who == null || who != Player.m_localPlayer || who.IsDead())
            {
                Stop();
            }
        }

        /// <summary>Stops a repair, costing nothing and saying nothing (an undo step too).</summary>
        internal static void Stop()
        {
            End();
        }

        /// <summary>Stops a repair before its bar filled: nothing repaired, the stone kept or used by "On cancel". The
        /// repair is ended before anything is paid, so nothing the payment sets off can end it twice.</summary>
        internal static void Cancel(Player player, string message)
        {
            if (!IsHis(player))
            {
                return;
            }
            List<ItemDrop.ItemData> still = Still(player);
            ItemDrop.ItemData stone = firstStone;
            End();
            bool used = Settings.Cancel.Value == OnCancel.StoneUsed && Pay(player, stone, still.Count);
            string cost = Settings.Cost.Value == StoneCost.OneStone ? ": the stone is used" : ": the stone is worn";
            Say.Center(used ? message + cost : message);
        }

        private static void Complete(Player player)
        {
            List<ItemDrop.ItemData> still = Still(player);
            ItemDrop.ItemData stone = firstStone;
            End();
            if (still.Count == 0)
            {
                Say.Center("Nothing left to repair");
                return;
            }
            if (!Pay(player, stone, still.Count))
            {
                Say.Center("No Sharpening Stone left to pay for the repair");
                return;
            }
            int share = (int)Settings.Share.Value;
            int mended = 0;
            foreach (ItemDrop.ItemData item in still)
            {
                if (Gear.Mend(player, item, Rules.Gained(item.m_durability, item.GetMaxDurability(), share)))
                {
                    mended++;
                }
            }
            GameObject workbench = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("piece_workbench") : null;
            CraftingStation station = workbench != null ? workbench.GetComponent<CraftingStation>() : null;
            if (mended > 0 && station != null && station.m_repairItemDoneEffects != null)
            {
                station.m_repairItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            }
            Say.Center($"The Sharpening Stone repaired {Gear.Items(mended)}");
        }

        /// <summary>Ends the repair: its motion off, its state cleared.</summary>
        private static void End()
        {
            if (!running)
            {
                return;
            }
            running = false;
            Look(false);
            entries.Clear();
            who = null;
            firstStone = null;
            stepPlayer = null;
        }

        /// <summary>The items of this repair still in his inventory and still worn: each by itself, or, if the game
        /// replaced it with a copy (moved to another cell), the same item by name, quality, world level, variant and
        /// crafter.</summary>
        private static List<ItemDrop.ItemData> Still(Player player)
        {
            var still = new List<ItemDrop.ItemData>();
            Inventory inventory = player.GetInventory();
            foreach (Entry entry in entries)
            {
                ItemDrop.ItemData item = inventory.ContainsItem(entry.Item) ? entry.Item : null;
                if (item == null || still.Contains(item))
                {
                    item = null;
                    foreach (ItemDrop.ItemData other in inventory.GetAllItems())
                    {
                        if (entry.Same(other) && !still.Contains(other) && !IsEntry(other) && Gear.Worn(other))
                        {
                            item = other;
                            break;
                        }
                    }
                }
                if (item != null && Gear.CanBeRepaired(item, out _) && Gear.Worn(item))
                {
                    still.Add(item);
                }
            }
            return still;
        }

        private static bool IsEntry(ItemDrop.ItemData item)
        {
            foreach (Entry entry in entries)
            {
                if (ReferenceEquals(entry.Item, item))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Pays for a repair of <paramref name="count"/> items, before anything is repaired: one stone taken (the
        /// most worn first, so a worn stone is the one used up), or the stones worn by the share. False when it couldn't
        /// be paid (nothing is then repaired).</summary>
        private static bool Pay(Player player, ItemDrop.ItemData stone, int count)
        {
            Inventory inventory = player.GetInventory();
            StoneCost cost = Settings.Cost.Value;
            List<ItemDrop.ItemData> stacks = StoneWear.Stacks(inventory, stone);
            if (stacks.Count == 0)
            {
                return false;
            }
            float stoneMax = StoneWear.Max(stacks[0]);
            if (cost == StoneCost.OneStone)
            {
                ItemDrop.ItemData from = stacks[0];
                bool worn = from.m_durability < stoneMax - Rules.Tiny;
                if (!StoneWear.TakeOne(inventory, from))
                {
                    return false;
                }
                if (worn && inventory.ContainsItem(from) && from.m_stack > 0)
                {
                    from.m_durability = stoneMax;   // the worn top stone was the one used up; the next is whole
                }
                return true;
            }
            float wear = Rules.WearCost(cost, stoneMax, Settings.PerUse.Value, Settings.PerItem.Value, Mathf.Max(1, count));
            return StoneWear.Wear(inventory, StoneWear.GameRows(player), stone, wear);
        }

        /// <summary>Why a press can't start a repair now: in an attack, a dodge, a sprint, staggered, dead, teleporting
        /// or in a cutscene the press is quietly ignored, as the game ignores an equip (a sprint would cancel it at once);
        /// swimming without footing and at a ship's helm it says so.</summary>
        private static bool Refused(Player player, out string why)
        {
            why = null;
            if (player.InAttack() || player.InDodge() || player.IsRunning() || player.IsStaggering() || player.IsDead() ||
                player.IsTeleporting() || player.InCutscene())
            {
                return true;
            }
            if (player.IsSwimming() && !player.IsOnGround())
            {
                why = "You can't repair while swimming";
                return true;
            }
            if (AtTheHelm(player))
            {
                why = "You can't repair while steering a ship";
                return true;
            }
            return false;
        }

        /// <summary>What stops a running repair, checked every tick; null while nothing does.</summary>
        private static string StopsIt(Player player)
        {
            if (StoneWear.Count(player.GetInventory()) <= 0)
            {
                return "Repair cancelled: no Sharpening Stone left";
            }
            if (player.IsTeleporting())
            {
                return "Repair cancelled";
            }
            if (player.IsSwimming() && !player.IsOnGround())
            {
                return "Repair cancelled: you're swimming";
            }
            if (AtTheHelm(player))
            {
                return "Repair cancelled";
            }
            if (Settings.CancelOnStagger.Value && player.IsStaggering())
            {
                return "Repair cancelled: you were staggered";
            }
            return null;
        }

        /// <summary>At a ship's helm, not merely on a ship's bench (IsAttachedToShip is true for both: Chair.m_inShip).</summary>
        private static bool AtTheHelm(Player player) => player.GetDoodadController() is ShipControlls;

        /// <summary>While the game's own equip, reload or attack runs first: the repair's motion gives way, but never
        /// switches off the game's own equip motion, which is the same "equipping".</summary>
        private static void GiveWay(Player player)
        {
            if (look == RepairLook.EquipMotion && player.GetActionQueueCount() > 0)
            {
                player.GetActionProgress(out _, out _, out Player.MinorActionData data);
                if (data != null && data.m_animation == "equipping")
                {
                    return;
                }
            }
            Look(false);
        }

        /// <summary>The character's motion while the bar runs, as "While repairing" said at the press. Other players see
        /// it (the animator's values go out with the character). The crafting pose leaves an open station's own pose
        /// alone.</summary>
        private static void Look(bool on)
        {
            ZSyncAnimation animation = who != null ? who.GetZAnim() : null;
            if (animation == null)
            {
                return;
            }
            switch (look)
            {
                case RepairLook.EquipMotion:
                    animation.SetBool("equipping", on);
                    break;
                case RepairLook.CraftingPose:
                    if (who.GetCurrentCraftingStation() == null)
                    {
                        animation.SetInt("crafting", on ? 1 : 0);
                    }
                    break;
            }
        }
    }
}
