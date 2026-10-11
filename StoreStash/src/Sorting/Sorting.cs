using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using QoLMods.Catalogue;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Sorting (card A53): his inventory by the key or its button, and a chest by its button or as it opens. His inventory
    /// is sorted in the game's own rows only (the character's row count; EquipmentAndQuickSlots' rows below are never
    /// touched), leaving in place what the settings keep (the hotbar, what he wears, his favourites) and whatever he has
    /// in his hand. A chest is sorted only while this computer owns it: as its window opens, in the frame the game hands
    /// it over and before it shows it (reloaded first, so the newest contents are what's sorted), or by its button while
    /// it's open. Never a grave (taking everything back from one puts each item in its old cell), and no chest at all while
    /// MultiUserChest is loaded (it lets two players have one chest open, each with a copy).
    /// </summary>
    internal static class Sorting
    {
        /// <summary>Whether a chest is a grave (its TombStone). Only the scratch test harness sets another.</summary>
        internal static System.Func<Container, bool> IsGrave = chest => chest.GetComponentInParent<TombStone>() != null;

        /// <summary>Whether he built the chest (or the cart or ship it's on). Only the scratch test harness sets another.</summary>
        internal static System.Func<Container, bool> BuiltByMe = chest =>
        {
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null && Game.instance != null && piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID();
        };

        /// <summary>Whether someone else marked the chest as theirs (the chest service's mine mark, Personal Stacker's). Only
        /// the scratch test harness sets another.</summary>
        internal static System.Func<Container, bool> MarkedByOther = chest => ChestMine.MarkedByOther(chest, ChestClaims.PlayerId());

        /// <summary>Reloads a chest from its record (ChestHooks.Reload). Only the scratch test harness sets another.</summary>
        internal static System.Action<Container> Reload = ChestHooks.Reload;

        /// <summary>Lets go of what he's carrying (GameHooks.LetGoOfDrag). Only the scratch test harness sets another.</summary>
        internal static System.Action<InventoryGui> LetGo = GameHooks.LetGoOfDrag;

        /// <summary>The game's move sound where he stands. Only the scratch test harness sets another.</summary>
        internal static System.Action Sound = () =>
        {
            Player player = Player.m_localPlayer;
            if (player != null && InventoryGui.instance != null)
            {
                InventoryGui.instance.m_moveItemEffects?.Create(player.transform.position, Quaternion.identity);
            }
        };

        private static Container openSorted;
        private static bool lookedForMultiUserChest;
        private static bool multiUserChest;
        private static bool toldCantSort;
        private static ZNetScene biomesAskedFor;

        /// <summary>Every frame (the kit calls it while the module is switched off too): forgets the chest sorted as it
        /// opened once it's no longer open.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container open = gui != null ? GameHooks.OpenChest(gui) : null;
            if (open != openSorted)
            {
                openSorted = null;
            }
        }

        /// <summary>Whether the sort key would sort now: sorting on and his inventory showing (S walks backwards while it's
        /// closed). Keys counts the sort key only then, so it never takes a press from another key.</summary>
        internal static bool KeyLive() => SortSettings.On.Value && InventoryGui.instance != null && InventoryGui.IsVisible();

        /// <summary>The sort key went down (Keys: it runs one key a press, not while he types, never under the trash can's
        /// question): his inventory, the open chest, or both, as "Sort key with a chest open" says.</summary>
        internal static void KeyPressed()
        {
            InventoryGui gui = InventoryGui.instance;
            if (!Module.IsOn || !KeyLive())
            {
                return;
            }
            // A chest it may sort, showing; else (none, not handed over yet, a grave, MultiUserChest) the key sorts his
            // inventory whatever the setting, so it never does nothing.
            Container open = GameHooks.OpenChest(gui);
            Container shown = Shown(open) && Sortable(open) ? open : null;
            switch (SortSettings.KeyWithChest.Value)
            {
                case SortKeyWithChest.TheChest when shown != null:
                    SortChest(shown, fromButton: true);
                    break;
                case SortKeyWithChest.Both when shown != null:
                    SortChest(shown, fromButton: true);
                    SortMine(fromButton: true);
                    break;
                default:
                    SortMine(fromButton: true);
                    break;
            }
        }

        /// <summary>Whether this chest's window is showing: open, his, and handed over (the game shows it only then).</summary>
        internal static bool Shown(Container chest) => chest != null && chest.IsOwner() && chest.IsInUse();

        /// <summary>His inventory, by the settings; returns whether anything changed.</summary>
        internal static bool SortMine(bool fromButton)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting() || !CanSort())
            {
                return false;
            }
            bool keepFavorites = SortSettings.KeepFavorites.Value;
            bool mergeFavorites = SortSettings.MergeFavorites.Value;
            if ((keepFavorites || !mergeFavorites) && !Favorites.List.Readable)
            {
                // His favourites would count as ordinary items and move, or merge.
                Messages.TopLeft("Sort: your favorites file can't be read, so nothing was sorted (see the log)");
                return false;
            }
            LetGo(InventoryGui.instance);   // as the game's own Place stacks does first
            bool keepWorn = SortSettings.KeepWorn.Value;
            int top = SortSettings.KeepHotbar.Value ? 1 : 0;
            int bottom = StackItems.GameRows(player);
            MineRules(keepWorn, keepFavorites, mergeFavorites, item => ChestHooks.Worn(player, item), Favorites.IsFavorite, ChestHooks.InHisHand,
                      out System.Func<ItemDrop.ItemData, bool> pinned, out System.Func<ItemDrop.ItemData, bool> unmerged,
                      out System.Func<ItemDrop.ItemData, bool> mergeInPlace);
            SortOutcome outcome = Sorter.Sort(player.GetInventory(), top, bottom, pinned, Rules(), unmerged, mergeInPlace);
            return After(outcome, "your inventory", fromButton);
        }

        /// <summary>
        /// What a sort of his inventory keeps in place, never merges, and merges in place, as the settings say (plain code
        /// over the item's facts, for the scratch test harness too). Kept in place: what he wears or holds ("Keep what you
        /// wear in place"), his favourites ("Keep favorites in place"), and the stack in his hand always. Never merged: what
        /// he wears or holds, kept in place or not (the game takes a thrown bomb or a shot arrow straight from the equipped
        /// stack, so an equipped stack emptied into another would give its amount twice), and his favourites while "Merge
        /// favorite stacks" is off (card A82). Merged in place: his favourites kept in place while it's on, among
        /// themselves only, each staying in its cell; never the stack in his hand.
        /// </summary>
        internal static void MineRules(bool keepWorn, bool keepFavorites, bool mergeFavorites, System.Func<ItemDrop.ItemData, bool> worn,
                                       System.Func<ItemDrop.ItemData, bool> favorite, System.Func<ItemDrop.ItemData, bool> inHand,
                                       out System.Func<ItemDrop.ItemData, bool> pinned, out System.Func<ItemDrop.ItemData, bool> unmerged,
                                       out System.Func<ItemDrop.ItemData, bool> mergeInPlace)
        {
            pinned = item => (keepWorn && worn(item)) || (keepFavorites && favorite(item)) || inHand(item);
            unmerged = item => worn(item) || (!mergeFavorites && favorite(item));
            mergeInPlace = item => mergeFavorites && keepFavorites && favorite(item) && !inHand(item);
        }

        /// <summary>An open chest, by its button or the key; returns whether anything changed.</summary>
        internal static bool SortChest(Container chest, bool fromButton)
        {
            if (!Sortable(chest) || !CanSort())
            {
                return false;
            }
            if (!chest.IsOwner())
            {
                return false;   // not his this moment: its window is closing
            }
            LetGo(InventoryGui.instance);
            return SortChestNow(chest, fromButton);
        }

        /// <summary>
        /// Called by the game's InventoryGui.UpdateContainer, just before it shows a chest's window: once per opening, in
        /// the first frame the chest is his and not yet marked as in use (the game marks it right after), the chest is
        /// reloaded from its record and sorted, if the settings say so.
        /// </summary>
        internal static void AsItOpens(InventoryGui gui)
        {
            if (!Module.IsOn || !SortSettings.On.Value || SortSettings.OnOpen.Value == SortOnOpenChoice.Off)
            {
                return;
            }
            Container chest = GameHooks.OpenChest(gui);
            if (chest == null || chest == openSorted || !chest.IsOwner() || chest.IsInUse())
            {
                return;   // none, done for this opening, not handed over yet, or already showing
            }
            openSorted = chest;
            if (!Sortable(chest) || !SortHooks.CanSort)
            {
                return;
            }
            if (SortSettings.OnOpen.Value == SortOnOpenChoice.ChestsIBuilt && !BuiltByMe(chest))
            {
                return;
            }
            // A chest someone else marked as theirs (Personal Stacker's mark) is left as they keep it, as the quick stack
            // leaves it alone: opening it is no choice to rearrange it. Its Sort button, his hand, still sorts it.
            if (!SortSettings.OthersMarked.Value && MarkedByOther(chest))
            {
                return;
            }
            // His copy may be a second old (the game reloads a chest once a second, and never once its window shows): the
            // newest contents came with the ownership, so they're read now, before anything is written back.
            Reload(chest);
            SortChestNow(chest, fromButton: false);
        }

        /// <summary>
        /// A chest a tidy changed (Stack Overflow, card A42), sorted by the settings in the frame it's his, as it would be
        /// sorted as it opens: saying nothing, but in the log when a check stopped it. Whether it changed.
        /// </summary>
        internal static bool SortForTidy(Container chest)
        {
            if (!Sortable(chest) || !SortHooks.CanSort || !chest.IsOwner())
            {
                return false;
            }
            Inventory inventory = chest.GetInventory();
            SortOutcome outcome = Sorter.Sort(inventory, 0, inventory.GetHeight(), ChestHooks.InHisHand, Rules());
            if (outcome.Refused != null)
            {
                Module.Log.LogWarning($"Sorting a chest after a tidy was stopped before anything moved: {outcome.Refused}.");
                return false;
            }
            return outcome.AnyChange;
        }

        private static bool SortChestNow(Container chest, bool fromButton)
        {
            Inventory inventory = chest.GetInventory();
            SortOutcome outcome = Sorter.Sort(inventory, 0, inventory.GetHeight(), ChestHooks.InHisHand, Rules());
            return After(outcome, "the chest", fromButton);
        }

        /// <summary>A chest that may be sorted: not a grave, not another player's chest he may only look at (card A80), and
        /// no MultiUserChest loaded.</summary>
        internal static bool Sortable(Container chest)
        {
            if (chest == null || IsGrave(chest) || !MayTouch(chest))
            {
                return false;
            }
            return !MultiUserChestLoaded();
        }

        /// <summary>Whether his hands may change the chest (QoLMods.Chests.ChestAccess.MayTouch: not another player's chest
        /// he may only look at). Only the scratch test harness sets another.</summary>
        internal static System.Func<Container, bool> MayTouch = ChestAccess.MayTouch;

        private static bool MultiUserChestLoaded()
        {
            if (!lookedForMultiUserChest)
            {
                lookedForMultiUserChest = true;
                multiUserChest = Chainloader.PluginInfos.TryGetValue(ChestHooks.MultiUserChestGuid, out BepInEx.PluginInfo info) && info.Instance != null;
                if (multiUserChest)
                {
                    Module.Log.LogInfo("MultiUserChest is loaded, so chests aren't sorted (it lets two players have one chest open, " +
                                       "each with a copy). Your inventory still is.");
                }
            }
            return multiUserChest;
        }

        private static bool CanSort()
        {
            if (SortHooks.CanSort)
            {
                return true;
            }
            if (!toldCantSort)
            {
                toldCantSort = true;
                Messages.TopLeft("Sort: the game has changed, so sorting is off (see the log)");
            }
            return false;
        }

        private static SortRules Rules()
        {
            SortRules rules = SortRules.FromSettings();
            if (rules.Order == SortOrderChoice.PacksOrder && biomesAskedFor != ZNetScene.instance)
            {
                // The pack's order reads each item's biome: worked out once per world (a world is a new scene), with a line
                // in the log saying what was placed (nothing when another module of the pack has already done it).
                biomesAskedFor = ZNetScene.instance;
                Biomes.Build(line => Module.Log.LogInfo(line));
            }
            return rules;
        }

        // Says what went wrong, if anything did; a plan the checks refused changed nothing, and says so. The game's move
        // sound after a sort by the key or a button that changed something.
        private static bool After(SortOutcome outcome, string what, bool fromButton)
        {
            if (outcome.Refused != null)
            {
                Module.Log.LogWarning($"Sorting {what} was stopped before anything moved: {outcome.Refused}.");
                if (fromButton)
                {
                    Messages.TopLeft("Sort: stopped before anything moved (see the log)");
                }
                return false;
            }
            if (outcome.AnyChange && fromButton && SortSettings.Sound.Value)
            {
                Sound();
            }
            return outcome.AnyChange;
        }
    }

    /// <summary>The game's InventoryGui.UpdateContainer(Player), found by name at start: a chest is sorted as its window
    /// opens, just before the game shows it (Sorting.AsItOpens).</summary>
    [HarmonyPatch]
    internal static class SortAsItOpens
    {
        private static bool Prepare() => SortHooks.UpdateContainer != null;

        private static MethodBase TargetMethod() => SortHooks.UpdateContainer;

        private static void Prefix(InventoryGui __instance)
        {
            if (Module.IsOn)
            {
                Module.Guard("sorting a chest as it opens", () => Sorting.AsItOpens(__instance));
            }
        }
    }
}
