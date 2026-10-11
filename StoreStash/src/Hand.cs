using System.Reflection;
using HarmonyLib;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Place stacks and holding E at one chest, by StoreStash's rules instead of the game's (card A52): what he carries goes
    /// into that chest if it already holds it (trophies to a chest holding a trophy), never his favourites or what he wears,
    /// the hotbar as set. It's his hand at that chest, so the radius and which-chests rules don't apply, and a reserved chest
    /// takes it too unless "Place stacks and holding E leave reserved chests alone too" is on. Another mod that stops Place
    /// stacks first (Reserve Stock's own setting of that name) is respected. As the game's own, Place stacks says nothing and
    /// holding E says how many went.
    /// </summary>
    internal static class Hand
    {
        /// <summary>The frame number, to say something once a frame. Only the scratch test harness sets another.</summary>
        internal static System.Func<int> Frame = () => Time.frameCount;

        private static int toldIn = -1;

        /// <summary>The chest window's Place stacks button: true if StoreStash did it (the game's own must be skipped).</summary>
        internal static bool PlaceStacks(InventoryGui gui)
        {
            if (!Module.IsOn || !Settings.PlaceStacks.Value)
            {
                return false;
            }
            Container chest = GameHooks.OpenChest(gui);
            if (chest == null)
            {
                return false;
            }
            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting())
            {
                return false;   // as the game's own button: nothing then
            }
            GameHooks.LetGoOfDrag(gui);   // as the game's own does first, so a stack he was dragging can go too
            return StackInto(chest, holdingE: false);
        }

        /// <summary>Holding E at a chest, once its owner has said yes: true if StoreStash did it (the game's own must be
        /// skipped).</summary>
        internal static bool HoldE(Container chest)
        {
            if (!Module.IsOn || !Settings.HoldE.Value || chest == null || Player.m_localPlayer == null)
            {
                return false;
            }
            return StackInto(chest, holdingE: true);
        }

        private static bool StackInto(Container chest, bool holdingE)
        {
            if (Settings.ReservedByHand.Value && ChestReserve.IsReserved(chest))
            {
                Once("This chest is reserved: Place stacks leaves it alone (StoreStash's setting).");
                return true;
            }
            if (!Favorites.List.Readable)
            {
                // His favourites would count as ordinary items and go.
                Once("Your favorites file can't be read, so nothing was stacked (see the log).");
                return true;
            }
            // The chest is his while its window is open, so this happens now; if it isn't yet (holding E can answer before
            // the chest arrives), the chest service claims it first.
            bool started = ChestStacker.Start(Module.Chests, Settings.HandRules(), new[] { chest }, result => Stacked(chest, result, holdingE));
            if (!started)
            {
                Messages.Center("$msg_inuse");   // another module's claim is under way: as the game says when a chest is busy
            }
            return true;
        }

        // A line in the middle of the screen, once a frame (the game's button and E can both come in one).
        private static void Once(string text)
        {
            if (Frame() != toldIn)
            {
                toldIn = Frame();
                Messages.Center(text);
            }
        }

        // As the game's own Inventory.StackAll: Place stacks silent, holding E with its words and its sound at the chest when
        // anything moved, both counted in his statistics. A chest that turned out busy (another player has it open, or its
        // owner's game didn't answer) gets the game's "in use", as its own "no" to holding E does.
        private static void Stacked(Container chest, StackResult result, bool holdingE)
        {
            if (result.Moved == 0 && result.ChestsInUse + result.ChestsNoAnswer > 0)
            {
                Messages.Center("$msg_inuse");
                return;
            }
            if (holdingE)
            {
                Messages.Center(result.Moved > 0 ? "$msg_stackall " + result.Moved : "$msg_stackall_none");
                if (result.Moved > 0 && InventoryGui.instance != null && chest != null)
                {
                    InventoryGui.instance.m_moveItemEffects.Create(chest.transform.position, Quaternion.identity);
                }
            }
            if (Game.instance != null)
            {
                Game.instance.IncrementPlayerStat(PlayerStatType.PlaceStacks);
            }
        }
    }

    /// <summary>The chest window's Place stacks button (InventoryGui.OnStackAll), found by name at start.</summary>
    [HarmonyPatch]
    internal static class PlaceStacksButton
    {
        private static bool Prepare() => GameHooks.CanTakeOverPlaceStacks;

        private static MethodBase TargetMethod() => GameHooks.OnStackAll;

        // After a mod that stops Place stacks first (Reserve Stock's setting, at Priority.First): then it stays stopped.
        private static bool Prefix(InventoryGui __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            bool done = false;
            Module.Guard("doing Place stacks", () => done = Hand.PlaceStacks(__instance));
            return !done;
        }
    }

    /// <summary>Holding E at a chest: its owner's yes (Container.RPC_StackResponse(long uid, bool granted)) stacks by
    /// StoreStash's rules instead of the game's. A no is the game's own ("in use").</summary>
    [HarmonyPatch]
    internal static class HoldingE
    {
        private static bool Prepare() => GameHooks.StackResponse != null;

        private static MethodBase TargetMethod() => GameHooks.StackResponse;

        private static bool Prefix(Container __instance, bool __1, bool __runOriginal)
        {
            if (!__runOriginal || !__1)
            {
                return __runOriginal;
            }
            bool done = false;
            Module.Guard("stacking by holding E", () => done = Hand.HoldE(__instance));
            return !done;
        }
    }
}
