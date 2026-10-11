using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// The grown tree's patches (card B01), applied by the kit with the rest of Terraformer. Each target was looked up and
// checked in TreeHooks; a class whose target is missing is skipped (its Prepare), and a class that can't be applied costs
// only the trees (its Cleanup, TreeHooks.Patching), never the rest of Terraformer. Each body that does real work runs
// inside Module.Guard. All but the grow do nothing while Terraformer is switched off; the grow's rewrite stays, and reads
// only a sapling's own record, so a sapling locked earlier grows locked whatever the switch says (plan decision 9).
namespace Terraformer.Trees
{
    /// <summary>Right after the game's own placement input: the trees' keys and wheel (TreeControls.Frame), with the turn
    /// the game had before it, to undo the wheel's turn while the size key is held.</summary>
    [HarmonyPatch]
    internal static class TreeInputPatch
    {
        private static bool Prepare() => !TreeHooks.Lost && GameHooks.UpdatePlacement != null;

        private static MethodBase TargetMethod() => GameHooks.UpdatePlacement;

        private static void Prefix(Player __instance, out int __state)
        {
            __state = __instance == Player.m_localPlayer && Module.IsOn ? GameHooks.PlaceRotation(__instance) : 0;
        }

        private static void Postfix(Player __instance, bool takeInput, int __state)
        {
            if (!Module.IsOn || __instance != Player.m_localPlayer)
            {
                return;
            }
            Module.Guard("reading the grown tree's keys", () => TreeControls.Frame(__instance, takeInput, __state));
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("the placement input (Player.UpdatePlacement)", TreePart.Preview, typeof(TreeInputPatch), original, ex, harmony);
    }

    /// <summary>After the game has placed the ghost: the grown tree shown at it, hidden, or gone. Last of every postfix on
    /// the method (review round 1, finding 3): PlantEasily's postfix there moves and snaps the main ghost, and so may
    /// another mod's, so the tree is placed where the ghost finally is, whatever order the mods loaded in. Priority.Last
    /// rather than HarmonyAfter PlantEasily's ID: it covers any mod that moves the ghost, and names no other mod's ID.</summary>
    [HarmonyPatch]
    internal static class TreeGhostPatch
    {
        private static bool Prepare() => !TreeHooks.Lost && GameHooks.UpdatePlacementGhost != null;

        private static MethodBase TargetMethod() => GameHooks.UpdatePlacementGhost;

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }
            if (!Module.IsOn)
            {
                Module.Guard("taking the grown tree away", TreeGhost.Clear);
                return;
            }
            Module.Guard("showing the grown tree", () => TreeControls.Place(__instance));
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("the placement ghost (Player.UpdatePlacementGhost)", TreePart.Preview, typeof(TreeGhostPatch), original, ex, harmony);
    }

    /// <summary>
    /// His click places a piece: with the grown tree showing, the click's pick waits for its saplings (TreeLock), and, with
    /// "Keep my turn" on, the random turn Player.PlacePiece draws for the next sapling (a piece with
    /// m_randomInitBuildRotation, as every sapling has) is undone. The game draws again when it remakes the ghost after the
    /// seed is used up (TreeTurnPatch, below); this pair still covers a click that uses nothing up (no cost), where the
    /// ghost isn't remade.
    /// </summary>
    [HarmonyPatch]
    internal static class TreePlacePatch
    {
        private const int NoTurn = int.MinValue;

        private static bool Prepare() => !TreeHooks.Lost;

        private static MethodBase TargetMethod() => TreeHooks.PlacePiece;

        private static void Prefix(Player __instance, Piece piece, out int __state)
        {
            int keep = NoTurn;
            if (Module.IsOn && __instance == Player.m_localPlayer)
            {
                Module.Guard("noting the grown tree a sapling is planted with", () =>
                {
                    bool previewing = TreeControls.ShowingFor(__instance, piece);
                    TreeLock.BeforePlacing(piece, previewing);
                    if (previewing && TreeSettings.KeepTurn.Value)
                    {
                        keep = GameHooks.PlaceRotation(__instance);
                    }
                });
            }
            __state = keep;
        }

        private static void Postfix(Player __instance, int __state)
        {
            if (__state == NoTurn || !Module.IsOn)
            {
                return;
            }
            Module.Guard("keeping the sapling's turn", () =>
                GameHooks.PlaceRotation(__instance) = TreeRules.TurnAfterPlacing(__state, GameHooks.PlaceRotation(__instance), true, true));
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("planting (Player.PlacePiece)", TreePart.Preview, typeof(TreePlacePatch), original, ex, harmony);
    }

    /// <summary>
    /// "Keep my turn" across the game's remaking of the ghost (review round 1, finding 1). Player.SetupPlacementGhost gives
    /// a sapling a new random turn every time it remakes the ghost (Player.cs:3489-3491), and it does so right after every
    /// planting: using up the seed (UpdatePlacement, 1396, ConsumeResources) changes the inventory, whose change
    /// (OnInventoryChanged, 5577) updates the pieces (UpdateAvailablePiecesList, 5365), which remakes the ghost. So the
    /// turn is kept before it and put back after it, while "Keep my turn" is on and the tree shows or a click's pick still
    /// waits for its saplings (TreeRules.KeepsTurn), and only while a sapling is still in hand (another piece takes the
    /// game's own turn). PlantEasily keeps the turn round the same method only while it isn't planting; both put back the
    /// same turn (the one from before the draw), so they never fight, and in a grid this one holds it while PlantEasily's
    /// can't.
    /// </summary>
    [HarmonyPatch]
    internal static class TreeTurnPatch
    {
        private const int NoTurn = int.MinValue;

        private static bool Prepare() => !TreeHooks.Lost && TreeHooks.SetupPlacementGhost != null;

        private static MethodBase TargetMethod() => TreeHooks.SetupPlacementGhost;

        private static void Prefix(Player __instance, out int __state)
        {
            int keep = NoTurn;
            if (Module.IsOn && __instance == Player.m_localPlayer)
            {
                Module.Guard("keeping the sapling's turn as the ghost is remade", () =>
                {
                    if (TreeControls.KeepsTurn(__instance))
                    {
                        keep = GameHooks.PlaceRotation(__instance);
                    }
                });
            }
            __state = keep;
        }

        private static void Postfix(Player __instance, int __state)
        {
            if (__state == NoTurn || !Module.IsOn)
            {
                return;
            }
            Module.Guard("putting the sapling's turn back", () =>
            {
                if (TreeControls.RestoresTurn(__instance, kept: true))
                {
                    GameHooks.PlaceRotation(__instance) = __state;
                }
            });
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("the ghost being remade (Player.SetupPlacementGhost)", TreePart.Turn, typeof(TreeTurnPatch), original, ex, harmony);
    }

    /// <summary>A sapling wakes: if it's a fresh one of his click's, the click's pick goes into its record. Every plant that
    /// loads passes here, so with no click waiting this costs one test.</summary>
    [HarmonyPatch]
    internal static class TreeAwakePatch
    {
        private static bool Prepare() => !TreeHooks.Lost;

        private static MethodBase TargetMethod() => TreeHooks.PlantAwake;

        private static void Prefix(Plant __instance, out bool __state)
        {
            bool fresh = false;
            if (Module.IsOn && TreeLock.Waiting)
            {
                Module.Guard("looking at a sapling just planted", () => fresh = TreeLock.IsFresh(__instance));
            }
            __state = fresh;
        }

        private static void Postfix(Plant __instance, bool __state)
        {
            if (!__state || !Module.IsOn)
            {
                return;
            }
            Module.Guard("locking a sapling's grown tree", () => TreeLock.AfterWaking(__instance, fresh: true));
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("a plant waking (Plant.Awake)", TreePart.Preview, typeof(TreeAwakePatch), original, ex, harmony);
    }

    /// <summary>
    /// The build bar's name line, set by the game every frame: Terraformer's one patch on it (review round 1 of A99/B02,
    /// F1), adding after the piece's name whichever applies (TreeRules.BarSuffix): the tree shown, with a sapling; or
    /// " · paint only", with a painting tool while paint only is on (PaintOnly.BarSuffix). An optional hook with a Prepare
    /// of its own, not tied to the trees' other hooks, so losing the trees never loses "paint only". An error here costs
    /// only the bar's words for the session (its own guard, TreeHooks.BarBroke), never the rest of Terraformer.
    /// </summary>
    [HarmonyPatch]
    internal static class TreeBarPatch
    {
        private static bool Prepare() => TreeHooks.SetupPieceInfo != null && TreeHooks.CanNameInBar;

        private static MethodBase TargetMethod() => TreeHooks.SetupPieceInfo;

        private static void Postfix(Hud __instance, Piece piece)
        {
            if (!Module.IsOn || piece == null || TreeHooks.BarFailed)
            {
                return;
            }
            try
            {
                string extra = TreeRules.BarSuffix(TreeControls.BarText(Player.m_localPlayer, piece), PaintOnly.BarSuffix(piece));
                string text = extra != null ? TreeHooks.BarText(__instance) : null;
                if (text != null)
                {
                    TreeHooks.SetBarText(__instance, text + extra);
                }
            }
            catch (Exception error)
            {
                TreeHooks.BarBroke(error);
            }
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("the build bar (Hud.SetupPieceInfo)", TreePart.Bar, typeof(TreeBarPatch), original, ex, harmony);
    }

    /// <summary>
    /// The grow: Plant.Grow's three random draws replaced by TreeLock's (TreeGrow.Rewrite). Where the method isn't as the
    /// game had it, it's left alone and the log says so once; saplings then grow as the game picks, and the preview still
    /// shows. Never throws: an error here would take every patch of Terraformer's off.
    /// </summary>
    [HarmonyPatch]
    internal static class TreeGrowPatch
    {
        /// <summary>Whether the rewrite went in (for the log and a harness).</summary>
        internal static bool Rewritten { get; private set; }

        private static bool Prepare() => TreeHooks.Grow != null && TreeHooks.RangeInt != null && TreeHooks.RangeFloat != null;

        private static MethodBase TargetMethod() => TreeHooks.Grow;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> original = instructions.ToList();
            string problem;
            try
            {
                List<CodeInstruction> rewritten = TreeGrow.Rewrite(original, TreeHooks.RangeInt, TreeHooks.RangeFloat,
                    TreeGrow.TreeMethod, TreeGrow.TurnMethod, TreeGrow.SizeMethod, out problem);
                if (rewritten != null)
                {
                    Rewritten = true;
                    return rewritten;
                }
            }
            catch (Exception error)
            {
                problem = "Reading Plant.Grow failed (" + error.Message + ")";
            }
            Rewritten = false;
            string warning = TreeGrow.WarningOnce(problem);
            if (warning != null)
            {
                Module.Log.LogWarning(warning);
            }
            return original;
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            TreeHooks.Patching("a sapling growing (Plant.Grow)", TreePart.Grow, typeof(TreeGrowPatch), original, ex, harmony);
    }
}
