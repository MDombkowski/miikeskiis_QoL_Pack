using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StoreStash
{
    /// <summary>
    /// One stack by a click (card A82, his words of 2026-10-09: "middle-mouse-button-click that stack of 7 bread to
    /// quick-stack just that stack of 7 bread, even if it's favorited, without it stacking my stack of 20 bread"). With his
    /// inventory open, the key or button of "One stack: key or button" (the middle mouse button out of the box, which the
    /// game's inventory never uses: InventoryGrid answers only left and right clicks, and with the inventory open
    /// Player.TakeInput is false, so the middle button's SecondaryAttack doesn't fire either) quick-stacks the stack it
    /// points at, by the quick stack key's own rules (QuickStack.AskOne, Settings.OneStackRules): its chests, its routing,
    /// chest setups, "only where held", the chests others marked, even a favourite, even on the hotbar, and only that very
    /// stack, never more of it than he saw. Never what he wears or holds, a stack he's carrying, a quest item, or a row
    /// below the game's own (EquipmentAndQuickSlots'). The stack is the one under the pointer as a click would land on it
    /// (the event system's topmost hit, so a window over the inventory hides it), or with a gamepad in use the cell its
    /// selection is on. A shortcut on the left or right mouse button needs a modifier held, and then the game's own click
    /// on a stack in his inventory is skipped (the patches below), so it does only this.
    /// </summary>
    internal static class OneStack
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>InventoryGui.OnSelectedItem(grid, item, cell, modifier): a left click on a cell. Null if the game has
        /// changed: then a shortcut on the left button isn't offered.</summary>
        internal static readonly MethodInfo Select = typeof(InventoryGui).GetMethod("OnSelectedItem", AnyInstance, null,
            new[] { typeof(InventoryGrid), typeof(ItemDrop.ItemData), typeof(Vector2i), typeof(InventoryGrid.Modifier) }, null);

        /// <summary>InventoryGui.OnRightClickItem(grid, item, cell): a right click on a cell (use, equip). Null if the game
        /// has changed: then a shortcut on the right button isn't offered.</summary>
        internal static readonly MethodInfo RightClick = typeof(InventoryGui).GetMethod("OnRightClickItem", AnyInstance, null,
            new[] { typeof(InventoryGrid), typeof(ItemDrop.ItemData), typeof(Vector2i) }, null);

        // InventoryGui.m_dragItem: the stack he's carrying (picked up with a click, or split off). Null if the game has
        // changed: then nothing counts as carried.
        private static readonly FieldInfo DragItem = typeof(InventoryGui).GetField("m_dragItem", AnyInstance);

        /// <summary>Whether he's carrying a stack (dragging it). Only the scratch test harness sets another.</summary>
        internal static Func<bool> Carrying = () => InventoryGui.instance != null && DragItem != null && DragItem.GetValue(InventoryGui.instance) != null;

        /// <summary>Whether his inventory shows. Only the scratch test harness sets another.</summary>
        internal static Func<bool> Showing = () => InventoryGui.instance != null && InventoryGui.IsVisible();

        /// <summary>Whether a chest's window is open beside it. Only the scratch test harness sets another.</summary>
        internal static Func<bool> ChestOpen = () => InventoryGui.instance != null && GameHooks.OpenChest(InventoryGui.instance) != null;

        /// <summary>The chest whose window is open beside it, or null. Only the scratch test harness sets another.</summary>
        internal static Func<Container> OpenChest = () => InventoryGui.instance != null ? GameHooks.OpenChest(InventoryGui.instance) : null;

        /// <summary>Whether his hands may take from or put into a chest (ChestAccess.MayTouch). Only the scratch test harness
        /// sets another.</summary>
        internal static Func<Container, bool> MayTouch = ChestAccess.MayTouch;

        /// <summary>
        /// Whether one stack leaves <paramref name="chest"/> alone as the chest open in his window that his hands may only
        /// look at (another player's marked chest, card A80): a click on his stack with that window open moves it into the
        /// chest by hand, as a Ctrl + click would (which look only refuses, ChestAccess.Touches), so it never goes there,
        /// whatever that player's "Their mods follow it too" says (44-B's last look). Other chests nearby still take it.
        /// </summary>
        internal static bool LooksOnly(Container chest)
        {
            if (chest == null)
            {
                return false;
            }
            Container open = OpenChest();
            return open != null && open == chest && !MayTouch(chest);
        }

        /// <summary>The stack the key points at in his inventory now, or null. Only the scratch test harness sets
        /// another.</summary>
        internal static Func<ItemDrop.ItemData> Pointed = PointedInGame;

        /// <summary>Whether a key is held now, read through the game's own input. Only the scratch test harness sets
        /// another.</summary>
        internal static Func<KeyCode, bool> Held = key => ZInput.GetKey(key, false);

        /// <summary>The frame number, to find the stack pointed at once a frame. Only the scratch test harness sets
        /// another.</summary>
        internal static Func<int> Frame = () => Time.frameCount;

        /// <summary>The name he reads. Only the scratch test harness sets another.</summary>
        internal static Func<string, string> Localize = name => Localization.instance != null ? Localization.instance.Localize(name) : name;

        private static int pointedIn = -1;
        private static ItemDrop.ItemData pointedAt;

        // This frame's one-stack press, as Keys and the click patches each see it (whichever runs first): Keys handled it
        // (pressedIn), it started (startedIn), or the game's own click on that stack had it (declinedIn).
        private static int pressedIn = -1;
        private static int startedIn = -1;
        private static int declinedIn = -1;

        /// <summary>
        /// The stack a click picked (card A82), and the most of it that may still go: what it held when he clicked, lowered
        /// to what it holds at each look the quick stack takes (its first plan, each claim round's), so across the rounds
        /// no more of it goes than he clicked, however a pickup tops it up meanwhile (review M1).
        /// </summary>
        internal sealed class Clicked
        {
            internal readonly ItemDrop.ItemData Stack;

            internal Clicked(ItemDrop.ItemData stack)
            {
                Stack = stack;
                Most = stack.m_stack;
            }

            /// <summary>How many of it may still go.</summary>
            internal int Most { get; private set; }

            /// <summary>StackRules.Pick: this very stack while it holds no more than Most, which then becomes what it
            /// holds.</summary>
            internal bool Picks(ItemDrop.ItemData item)
            {
                if (!ReferenceEquals(item, Stack) || item.m_stack > Most)
                {
                    return false;
                }
                Most = item.m_stack;
                return true;
            }

            /// <summary>It holds more than may go: topped up since the last look.</summary>
            internal bool Grown => Stack.m_stack > Most;
        }

        /// <summary>
        /// Whether the key may act now (Keys counts it only then, so it never takes a press from another key elsewhere):
        /// the setting not None, his inventory showing, a chest's window open only if "One stack with a chest open" is on,
        /// and a mouse button the game clicks with (left, right) only with a modifier, never the favourite or trash mark's
        /// own right-click, and only while the game's click there can be skipped; a left click never while he carries a
        /// stack (that click puts it down, the game's and other mods' own, EquipmentAndQuickSlots' equipping included); a
        /// right click never while the favourite or trash mark's modifier is held (that right-click marks: review N2).
        /// </summary>
        internal static bool Ready()
        {
            KeyboardShortcut key = Settings.OneStackKey.Value;
            if (key.MainKey == KeyCode.None || !Showing() || (!Settings.OneStackWithChestOpen.Value && ChestOpen()))
            {
                return false;
            }
            KeyCode favorite = Settings.FavoriteModifier.Value;
            KeyCode trash = TrashSettings.On != null && TrashSettings.On.Value && TrashSettings.Modifier != null ? TrashSettings.Modifier.Value : KeyCode.None;
            if (!Allowed(key, favorite, trash, Select != null, RightClick != null))
            {
                return false;
            }
            if (key.MainKey == KeyCode.Mouse0)
            {
                return !Carrying();
            }
            return key.MainKey != KeyCode.Mouse1 || !((favorite != KeyCode.None && Held(favorite)) || (trash != KeyCode.None && Held(trash)));
        }

        /// <summary>
        /// Whether a shortcut may be the one-stack key (plain code, for the harness too): a key or a button the game's
        /// inventory doesn't click with as it is; the left or right button only with a modifier held, and only where the
        /// game's own click can be skipped (<paramref name="canSkipLeft"/>, <paramref name="canSkipRight"/>); the right
        /// button never with the favourite or trash mark's modifier (that right-click marks); never a gamepad button (each
        /// already does something in the inventory: InventoryGrid.UpdateGamepad, InventoryGui.Update; review N3).
        /// </summary>
        internal static bool Allowed(KeyboardShortcut key, KeyCode favoriteModifier, KeyCode trashModifier, bool canSkipLeft, bool canSkipRight)
        {
            if (key.MainKey >= KeyCode.JoystickButton0)
            {
                return false;
            }
            if (key.MainKey == KeyCode.Mouse0 || key.MainKey == KeyCode.Mouse1)
            {
                if (!key.Modifiers.Any())
                {
                    return false;   // the game's own click
                }
                if (key.MainKey == KeyCode.Mouse0 ? !canSkipLeft : !canSkipRight)
                {
                    return false;
                }
                if (key.MainKey == KeyCode.Mouse1 && key.Modifiers.Any(modifier => modifier != KeyCode.None && (modifier == favoriteModifier || modifier == trashModifier)))
                {
                    return false;   // that right-click marks a favourite or trash
                }
            }
            return true;
        }

        /// <summary>Whether the key points at a stack of his now (Keys asks only once the key went down).</summary>
        internal static bool Pointing() => Target() != null;

        /// <summary>The stack the key points at, found once a frame.</summary>
        internal static ItemDrop.ItemData Target()
        {
            int frame = Frame();
            if (frame != pointedIn)
            {
                pointedIn = frame;
                pointedAt = Pointed();
            }
            return pointedAt;
        }

        /// <summary>
        /// The key went down, pointing at a stack (Keys: one key a press, not while he types): that stack goes, by the quick
        /// stack's rules, or he's told in a few words why it can't. The stack pointed at stays this frame's answer, so a
        /// later look in the same frame (the setup keys ask Keys again) sees the same winner (review N4). With a left or
        /// right click as the key, nothing if the game's own click on that stack already had this press, and no words when
        /// it can't go: the game's click is what happens then (TakesClick).
        /// </summary>
        internal static void Press()
        {
            int frame = Frame();
            if (declinedIn == frame)
            {
                return;
            }
            pressedIn = frame;
            ItemDrop.ItemData stack = Target();
            Player player = Player.m_localPlayer;
            if (!Module.IsOn || stack == null || player == null)
            {
                return;
            }
            string why = WhyNot(stack, player);
            if (why != null)
            {
                if (!GameClicks(Settings.OneStackKey.Value.MainKey))
                {
                    Messages.AsSet($"{Name(stack)}: {why}");
                }
                return;
            }
            if (QuickStack.AskOne(stack))
            {
                startedIn = frame;
            }
        }

        // A button the game's inventory clicks with.
        private static bool GameClicks(KeyCode button) => button == KeyCode.Mouse0 || button == KeyCode.Mouse1;

        /// <summary>Why a stack can't go by itself, in a few words, or null when it can (the rest is the quick stack's to
        /// decide).</summary>
        internal static string WhyNot(ItemDrop.ItemData stack, Player player) =>
            WhyNot(stack, ChestHooks.Worn(player, stack), ChestHooks.InHisHand(stack), StackItems.GameRows(player));

        /// <summary>The same from the facts alone, for the harness too.</summary>
        internal static string WhyNot(ItemDrop.ItemData stack, bool worn, bool inHand, int gameRows)
        {
            if (worn || stack.m_equipped)
            {
                return "you're wearing or holding it";
            }
            if (inHand)
            {
                return "put it down first";
            }
            if (stack.m_shared.m_questItem)
            {
                return "it can't go into a chest";
            }
            if (stack.m_gridPos.y < 0 || stack.m_gridPos.y >= gameRows)
            {
                return "only your inventory's own rows are quick stacked";
            }
            return null;
        }

        /// <summary>
        /// Why nothing of the stack went, in a few words, for the line after it (Messages.DescribeOne): it changed or went
        /// meanwhile (more in it than the <paramref name="most"/> he clicked, or no longer his); the chests nearby that the
        /// routing would use have no room for it; none of them takes it (the chests someone has open or that can't be had
        /// are named by the line itself); found from this computer's copies, by the routing the quick stack used.
        /// </summary>
        internal static string WhyNothing(StackResult result, Clicked clicked, StackRules rules)
        {
            Player player = Player.m_localPlayer;
            ItemDrop.ItemData stack = clicked?.Stack;
            if (result.NoItems || stack == null || clicked.Grown || stack.m_stack <= 0 || player == null || !player.GetInventory().ContainsItem(stack))
            {
                return "it changed meanwhile, try again";
            }
            if (player != null && rules != null && stack != null)
            {
                var chests = new List<Container>();
                var contents = new List<Inventory>();
                foreach (Container chest in ChestFinder.Find(ChestFinder.Where(player), rules.Chests))
                {
                    if (chest != null && !ChestFinder.OpenElsewhere(chest) && !ChestClaims.CantClaim(chest, Module.Chests))
                    {
                        chests.Add(chest);
                        contents.Add(chest.GetInventory());
                    }
                }
                if (Routed(StackRouting.Tiers(new[] { stack }, chests, contents, rules)))
                {
                    return "no room for it in the chests nearby that take it";
                }
            }
            if (result.ChestsInUse + result.ChestsNoAnswer + result.ChestsOutOfReach + result.ChestsReserved > 0)
            {
                return "nothing went";   // the line names the chests left out
            }
            return rules != null && rules.OnlyWhereHeld ? "no chest nearby holds any" : "no chest nearby takes it";
        }

        // Whether the routing found a chest for the (one) item in any of its tiers.
        private static bool Routed(List<List<List<int>>> tiers) => tiers.Any(item => item.Any(tier => tier.Count > 0));

        /// <summary>A stack's name as he reads it.</summary>
        internal static string Name(ItemDrop.ItemData stack) => Localize(stack.m_shared.m_name) ?? stack.m_shared.m_name;

        /// <summary>
        /// Whether the game's own click on a cell of <paramref name="grid"/> is this key's, and must be skipped: StoreStash
        /// on, the key a left or right click (<paramref name="button"/>) with its modifiers all held, on a stack
        /// (<paramref name="item"/>: an empty cell's click stays the game's, so a carried stack can still be put down
        /// there) in his inventory's grid, the key ready (Ready), and only when the one stack really starts (review N1):
        /// Keys would run it (the winning key, keys taken, no question up), that stack may go (WhyNot) and no quick stack
        /// is under way (QuickStack.CanAskOne). Its work is done in Keys, in the same frame; whichever runs first, the two
        /// agree: a press Keys started is skipped here, one it didn't start isn't, and one declined here Keys leaves alone.
        /// </summary>
        internal static bool TakesClick(KeyCode button, InventoryGrid grid, ItemDrop.ItemData item)
        {
            if (!Module.IsOn || item == null)
            {
                return false;
            }
            KeyboardShortcut key = Settings.OneStackKey.Value;
            if (key.MainKey != button || !key.Modifiers.Any())
            {
                return false;
            }
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || grid == null || grid != gui.m_playerGrid || !Ready() || !key.Modifiers.All(Held))
            {
                return false;
            }
            int frame = Frame();
            if (startedIn == frame)
            {
                return true;   // Keys ran first this frame, and the stack went (or is on its way)
            }
            if (pressedIn == frame || declinedIn == frame)
            {
                return false;   // Keys ran first and nothing started, or decided here already
            }
            Player player = Player.m_localPlayer;
            bool starts = player != null && Keys.Pressed() == Keys.Which.OneStack && Keys.TakingKeys() && !Keys.Asking() &&
                          ReferenceEquals(Target(), item) && WhyNot(item, player) == null && QuickStack.CanAskOne();
            if (!starts)
            {
                declinedIn = frame;
            }
            return starts;
        }

        // The stack under the pointer in his inventory, as a click would land on it (the event system's topmost hit: a
        // window over the inventory hides it), or with a gamepad in use the cell its selection is on, as the game's own
        // InventoryGrid.UpdateGui chooses; null for none, an empty cell, or another grid.
        private static ItemDrop.ItemData PointedInGame()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            InventoryGrid grid = gui != null ? gui.m_playerGrid : null;
            if (grid == null || player == null)
            {
                return null;
            }
            Inventory inventory = player.GetInventory();
            if (grid.GetInventory() != inventory)
            {
                return null;   // not his yet (the window opening)
            }
            if (ZInput.IsExclusiveGamepadActive())
            {
                ItemDrop.ItemData selected = grid.GetGamepadSelectedItem();   // null unless his grid has the gamepad
                return selected != null && inventory.ContainsItem(selected) ? selected : null;
            }
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return null;
            }
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = ZInput.pointerPosition }, hits);
            if (hits.Count == 0 || hits[0].gameObject == null)
            {
                return null;
            }
            // One of his grid's own cells (each made under its m_gridRoot): never another grid's, such as another mod's
            // drawn inside the inventory, whose cells number their own way.
            InventoryElement element = hits[0].gameObject.GetComponentInParent<InventoryElement>();
            if (element == null || grid.m_gridRoot == null || element.transform.parent != grid.m_gridRoot)
            {
                return null;
            }
            Vector2i cell = element.Position;
            return inventory.GetItemAt(cell.x, cell.y);
        }
    }

    /// <summary>A left click with the one-stack key's modifiers held, on his inventory, when that key is a left click
    /// (OneStack.TakesClick): the game's own (picking up, splitting, moving) is skipped.</summary>
    [HarmonyPatch]
    internal static class OneStackLeftClick
    {
        private static bool Prepare() => OneStack.Select != null;

        private static MethodBase TargetMethod() => OneStack.Select;

        private static bool Prefix(InventoryGrid __0, ItemDrop.ItemData __1, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            bool taken = false;
            Module.Guard("taking a click for one stack", () => taken = OneStack.TakesClick(KeyCode.Mouse0, __0, __1));
            return !taken;
        }
    }

    /// <summary>A right click with the one-stack key's modifiers held, on his inventory, when that key is a right click:
    /// the game's own (using, equipping) is skipped.</summary>
    [HarmonyPatch]
    internal static class OneStackRightClick
    {
        private static bool Prepare() => OneStack.RightClick != null;

        private static MethodBase TargetMethod() => OneStack.RightClick;

        private static bool Prefix(InventoryGrid __0, ItemDrop.ItemData __1, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            bool taken = false;
            Module.Guard("taking a click for one stack", () => taken = OneStack.TakesClick(KeyCode.Mouse1, __0, __1));
            return !taken;
        }
    }
}
