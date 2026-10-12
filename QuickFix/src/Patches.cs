using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches (research parts A and C read each target's body in Valheim 1.0.17). Each one does nothing unless
// the mod is on (Module.IsOn) and its own setting asks for it, so patches left in place while the mod is off change
// nothing; the work inside runs in Module.Guard, so an error switches the mod off instead of reaching the game.
namespace QuickFix
{
    /// <summary>The Use key at a Grinding Wheel or Smith's Anvil, which have no Interactable of their own: the repair,
    /// on a press only (never a hold), never during an attack or dodge (the game's own gate in Player.Interact), with the
    /// game's interact gesture. Another mod's Interactable on the piece still gets its turn.</summary>
    [HarmonyPatch(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
    internal static class InteractPatch
    {
        private static bool Prefix(Player __instance, GameObject go, bool hold)
        {
            if (!Module.IsOn || !Settings.PressE.Value || go == null || __instance != Player.m_localPlayer)
            {
                return true;
            }
            RepairPiece piece = Pieces.Which(go, out StationExtension extension);
            if (piece == RepairPiece.None)
            {
                return true;
            }
            if (!hold && !__instance.InAttack() && !__instance.InDodge())
            {
                Module.Guard("repairing at a Grinding Wheel or Smith's Anvil", () =>
                {
                    Pieces.Repair(__instance, piece, extension, speak: true);
                    GameHooks.InteractGesture(__instance, extension.gameObject);
                });
            }
            return go.GetComponentInParent<Interactable>() != null;
        }
    }

    /// <summary>The line "[E] Repair …" under the Grinding Wheel's and Smith's Anvil's names.</summary>
    [HarmonyPatch(typeof(StationExtension), nameof(StationExtension.GetHoverText))]
    internal static class HoverTextPatch
    {
        private static void Postfix(StationExtension __instance, ref string __result)
        {
            if (!Module.IsOn || !Settings.PressE.Value || __instance == null)
            {
                return;
            }
            RepairPiece piece = Pieces.Which(__instance.GetComponent<Piece>());
            if (piece == RepairPiece.None)
            {
                return;
            }
            string text = __result;
            Module.Guard("showing what a Grinding Wheel or Smith's Anvil repairs", () => text += Pieces.HoverLine(piece));
            __result = text;
        }
    }

    /// <summary>The build ghost of a Grinding Wheel or Smith's Anvil with no Forge near: valid, when that is all that's
    /// wrong (the ghost's colour, and TryPlacePiece, which calls this again before it places).</summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost", new[] { typeof(bool) })]
    internal static class PlacementPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!Module.IsOn || !Settings.PlaceAnywhere.Value || __instance != Player.m_localPlayer ||
                GameHooks.PlacementStatus(__instance) != Player.PlacementStatus.ExtensionMissingStation)
            {
                return;
            }
            Module.Guard("placing a Grinding Wheel or Smith's Anvil with no Forge near", () => Pieces.AllowWithoutForge(__instance));
        }
    }

    /// <summary>A crafting station opened: with "Repair on using a crafting station", everything it repairs, at once.</summary>
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
    internal static class StationOpenedPatch
    {
        private static void Postfix(CraftingStation __instance, Humanoid user, bool repeat)
        {
            if (!Module.IsOn || !Settings.OnUse.Value || repeat)
            {
                return;
            }
            Module.Guard("repairing at a crafting station", () => Stations.Opened(__instance, user, repeat));
        }
    }

    /// <summary>A press of the Sharpening Stone in his own inventory (the hotbar, or a right-click): the game reaches
    /// ToggleEquipped only after anything he looks at has refused the stone (Humanoid.UseItem), so the game's order
    /// stands; answering true keeps the game's "Use Sharpening Stone on what?" away.</summary>
    [HarmonyPatch(typeof(Player), "ToggleEquipped", new[] { typeof(ItemDrop.ItemData) })]
    internal static class StonePressPatch
    {
        private static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!Module.IsOn || !Settings.Stone.Value || __instance != Player.m_localPlayer || !Gear.IsStone(item))
            {
                return true;
            }
            __result = true;
            Module.Guard("starting a Sharpening Stone repair", () => StoneRepair.Press(__instance, item));
            return false;
        }
    }

    /// <summary>The repair's bar runs on the game's own action tick (no closure made per step: the step is passed through
    /// StoneRepair's fields).</summary>
    [HarmonyPatch(typeof(Player), "UpdateActionQueue", new[] { typeof(float) })]
    internal static class StoneTickPatch
    {
        private static void Postfix(Player __instance, float dt)
        {
            if (!StoneRepair.IsHis(__instance))
            {
                return;
            }
            StoneRepair.SetStep(__instance, dt);
            Module.Guard("running a Sharpening Stone repair", StoneRepair.TickStep);
        }
    }

    /// <summary>The repair drawn in the game's own action bar while nothing of the game's is in it.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetActionProgress),
                  new[] { typeof(string), typeof(float), typeof(Player.MinorActionData) },
                  new[] { ArgumentType.Out, ArgumentType.Out, ArgumentType.Out })]
    internal static class StoneBarPatch
    {
        private static void Postfix(Player __instance, ref string name, ref float progress, ref Player.MinorActionData data)
        {
            if (StoneRepair.IsHis(__instance))
            {
                StoneRepair.Bar(__instance, ref name, ref progress, ref data);
            }
        }
    }

    /// <summary>The game's own cancels of its equip timer (an attack started, a sprint, a jump, a dodge) cancel the
    /// repair too.</summary>
    [HarmonyPatch(typeof(Player), "ClearActionQueue")]
    internal static class StoneCancelPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!StoneRepair.IsHis(__instance))
            {
                return;
            }
            Module.Guard("cancelling a Sharpening Stone repair", () => StoneRepair.Cancel(__instance, "Repair cancelled"));
        }
    }

    /// <summary>An attack press, or moving with "Moving cancels", stops the repair.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class StoneControlsPatch
    {
        private static void Postfix(Player __instance, Vector3 movedir, bool attack, bool secondaryAttack, bool autoRun)
        {
            if (!StoneRepair.IsHis(__instance))
            {
                return;
            }
            StoneRepair.SetControls(__instance, movedir, attack || secondaryAttack, autoRun);
            Module.Guard("watching for a cancelled repair", StoneRepair.ControlsStep);
        }
    }

    /// <summary>The stack an added Sharpening Stone goes onto: never a worn stack, and a worn stone onto nothing
    /// (StoneWear.Chosen; a worn stone never stacks).</summary>
    [HarmonyPatch(typeof(Inventory), "FindFreeStackItem", new[] { typeof(string), typeof(int), typeof(float) })]
    internal static class StoneStackPatch
    {
        private static void Postfix(Inventory __instance, string name, int quality, float worldLevel, ref ItemDrop.ItemData __result)
        {
            if (__result == null || name != Gear.StoneName || !Module.IsOn || !StoneWear.On)
            {
                return;
            }
            ItemDrop.ItemData found = __result;
            Module.Guard("choosing the stack a Sharpening Stone joins",
                         () => found = StoneWear.Chosen(__instance, name, quality, worldLevel, found));
            __result = found;
        }
    }

    /// <summary>The game's "is there room?" for a Sharpening Stone, while the stone wears: by the rule that a worn stone
    /// never stacks (StoneWear.CanAdd), so auto-pickup and crafting never start an add that would fail part way. The
    /// GameObject overload calls this one.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
    internal static class StoneRoomPatch
    {
        private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
        {
            if (!__result || !Module.IsOn || !StoneWear.On || !Gear.IsStone(item))
            {
                return;
            }
            bool fits = true;
            Module.Guard("checking the room for a Sharpening Stone", () => fits = StoneWear.CanAdd(__instance, item, stack));
            __result = fits;
        }
    }

    /// <summary>Sharpening Stones on the ground don't merge by themselves while the stone wears (the game's auto-stack,
    /// with over 200 drops loaded, keeps the absorbing drop's durability, so a worn stone's wear would vanish).</summary>
    [HarmonyPatch(typeof(ItemDrop), "AutoStackItems")]
    internal static class StoneGroundPatch
    {
        private static bool Prefix(ItemDrop __instance)
        {
            return !(Module.IsOn && StoneWear.On && __instance != null && Gear.IsStone(__instance.m_itemData));
        }
    }

    /// <summary>What a stone being added carried in, kept from the prefix to the finalizer.</summary>
    internal struct AddState
    {
        internal bool Set;
        internal StoneWear.Adding Before;
    }

    /// <summary>A Sharpening Stone added to an inventory (a pickup, a take from a chest, a craft: Inventory.AddItem and
    /// its overload with a cell): a worn one is noted while the game picks the stacks its stones join, and the note put
    /// away again whatever happens.</summary>
    [HarmonyPatch]
    internal static class StoneAddPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) });
        }

        private static void Prefix(ItemDrop.ItemData item, out AddState __state)
        {
            __state = default;
            if (!Module.IsOn || !StoneWear.On || !Gear.IsStone(item))
            {
                return;
            }
            __state.Before = StoneWear.BeginAdd(item);
            __state.Set = true;
        }

        private static Exception Finalizer(Exception __exception, AddState __state)
        {
            if (__state.Set)
            {
                StoneWear.EndAdd(__state.Before);
            }
            return __exception;
        }
    }

    /// <summary>What a drag of a stone into an empty cell needs after it: whether it was part of a worn stack.</summary>
    internal struct DragState
    {
        internal ItemDrop.ItemData Item;
        internal int X;
        internal int Y;
        internal bool PartOfWorn;
    }

    /// <summary>A Sharpening Stone dragged (the game's private Inventory.AddItem with a cell): onto a worn stack, or all of
    /// a worn stack onto another, the drag is refused (a worn stone never stacks); part of a worn stack moves whole stones
    /// only, so a copy made in an empty cell is whole.</summary>
    [HarmonyPatch(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
    internal static class StoneDragPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, int x, int y, ref bool __result, out DragState __state)
        {
            __state = default;
            if (!Module.IsOn || !StoneWear.On || !Gear.IsStone(item))
            {
                return true;
            }
            DragState state = default;
            bool refuse = false;
            Module.Guard("keeping a worn Sharpening Stone in a cell of its own", () =>
            {
                ItemDrop.ItemData onto = __instance.GetItemAt(x, y);
                int moving = Mathf.Min(amount, item.m_stack);
                if (onto == null)
                {
                    state = new DragState { Item = item, X = x, Y = y, PartOfWorn = moving < item.m_stack && StoneWear.Worn(item) };
                    return;
                }
                if (ReferenceEquals(onto, item) || !Gear.IsStone(onto))
                {
                    return;
                }
                int units = Mathf.Min(onto.m_shared.m_maxStackSize - onto.m_stack, moving);
                refuse = units > 0 && StoneWear.RefusesDrag(onto, item, units);
            });
            __state = state;
            if (refuse)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static void Postfix(Inventory __instance, DragState __state)
        {
            if (!__state.PartOfWorn || !Module.IsOn)
            {
                return;
            }
            DragState state = __state;
            Module.Guard("keeping a worn Sharpening Stone in a cell of its own", () =>
            {
                ItemDrop.ItemData copy = __instance.GetItemAt(state.X, state.Y);
                if (copy != null && !ReferenceEquals(copy, state.Item) && Gear.IsStone(copy))
                {
                    copy.m_durability = StoneWear.Max(copy);
                }
            });
        }
    }
}
