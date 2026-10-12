// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>What other players may do at a chest someone marked as theirs (ChestAccess, card A80).</summary>
    internal enum OthersMay
    {
        /// <summary>Open and use it as any chest: nothing changes (a chest marked before card A80 reads so).</summary>
        Open = 0,

        /// <summary>Open it and see what's inside, but take, put in or move nothing.</summary>
        LookOnly = 1,

        /// <summary>Not open it at all.</summary>
        KeepOut = 2,
    }

    /// <summary>
    /// Other players at a chest someone marked as theirs (card A80, his words of 2026-10-09: whether other players can open
    /// one's personal chests, and "You can look, but don't touch."). The marking player's choice is written into each chest
    /// he marked, beside the "mine" mark (ChestMine), as one number in its record (Key), by his own game as the chest's
    /// owner (StoreStash: when he marks it, and again whenever his game holds a chest of his whose number differs from his
    /// setting), so it reaches every other game. The number: its lowest two bits the choice (OthersMay: 0 open, 1 look
    /// only, 2 kept out; 3, which no version writes, reads as kept out, the strictest), and ModsMayReachBit set when his
    /// setting lets other players' mods still use the chest though their hands are kept out. A chest without the number,
    /// or unmarked, is open: nothing changes for a chest marked before this, or for a friend who never picks a choice.
    ///
    /// Every other game with the pack honours it, here in the chest service rather than in StoreStash, so it holds while
    /// any module using the service is on (Active, as sharing does: Add to Cart, Craft from Chests, Reserve Stock,
    /// StoreStash, Outfitter), also for a friend whose StoreStash stays off beside HexQuickStackStorage, and the hand's rule
    /// and the mods' rule come from this one place:
    /// - Kept out: his press of E on the chest does nothing but say whose it is (ChestPatches.InteractPrefix, RefusesOpen),
    ///   and a window another mod opens on it directly is refused (InventoryGui.Show).
    /// - Look only: the window opens, its Take all and Place stacks greyed, and every way of taking, putting in or moving
    ///   in it is refused with a short line (a click, drag or drop in the chest's cells, Ctrl or Shift + click into it, the
    ///   split, a right-click on its items, Take all, Place stacks, holding E, dropping a stack carried from it). Portion
    ///   Control's -1 and Max weight follow the game's greyed Take all; StoreStash's sort, trash can, setup and tidy
    ///   buttons and Reserve Stock's padlock ask MayTouch. If any member it blocks through can't be found (a game update),
    ///   look only can't be held, and such a chest stays closed as if kept out (LookOnlyHolds); so it does while
    ///   MultiUserChest is loaded, which passes some of a second player's moves through patches of its own.
    /// - Either way, his hammer can't remove such a chest while it holds anything (Container.CanBeRemoved), as the game
    ///   keeps a Personal Chest that isn't empty. The Obliterator is never anyone's personal chest, whatever an older mark
    ///   on it says.
    /// - The pack's own reaching in (quick stacks, Craft from Chests, Add to Cart, Outfitter, a tidy, Reserve Stock's key,
    ///   a take through a friend's open chest): ChestFinder.WhyNot, the one place the chest rules are written, refuses the
    ///   chest under look only and kept out, unless the marker's setting lets mods in (ModsMay); ChestShare.MayShare asks
    ///   the same of the player a take is for, on the game that has the chest open.
    /// The player who marked it is never kept out, and the game's own Personal Chest keeps its own rule. A game without
    /// the pack, or with an older one, knows nothing of this: client-side only (his rule 6), so it holds between friends
    /// on the pack.
    /// </summary>
    internal static class ChestAccess
    {
        /// <summary>The choice's name in the chest's record. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.qolmods.mineaccess";

        /// <summary>Set when the marker lets other players' mods use the chest though their hands are kept out.</summary>
        internal const int ModsMayReachBit = 4;

        private const int ChoiceBits = 3;

        /// <summary>The bits this version writes (the choice and ModsMayReachBit). Others a later version may write are kept
        /// when this one rewrites the number (Mixed).</summary>
        internal const int OwnBits = ChoiceBits | ModsMayReachBit;

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Tells him something in the middle of the screen. Only the scratch test harness sets another.</summary>
        internal static Action<string> Say = text => QoLMods.Kit.Say.Center(text);   // fitted to the screen (card B14)

        private static FieldInfo dragGo;
        private static FieldInfo dragInventory;
        private static Container told;
        private static bool greyed;
        private static int saidIn = -1;
        private static string saidText;
        private static readonly HashSet<string> Faults = new HashSet<string>();

        /// <summary>Whether look only can be held in this game: every member it blocks through was found and patched. While
        /// false, a chest that is look only for him stays closed, as if kept out.</summary>
        internal static bool LookOnlyHolds { get; set; }

        /// <summary>Whether other players' marks are honoured on this computer now: while any module using the chest service
        /// is on, as his open chests are shared (ChestShare.Sharing).</summary>
        internal static bool Active => ChestShare.Sharing;

        /// <summary>Whether a container is the Obliterator's (ChestFinder.IsObliterator): never a personal chest, so a mark an
        /// older version let onto one means nothing to anyone. Only the scratch test harness sets another: it has no engine to
        /// ask about pieces.</summary>
        internal static Func<Container, bool> IsObliterator = ChestFinder.IsObliterator;

        /// <summary>Whether MultiUserChest is loaded (ChestShare.MultiUserChest, learnt at the service's first turn). It lets a
        /// second player open a chest that isn't his game's, and passes some of that player's moves through patches of its
        /// own: look only can't be vouched for there, so it stays closed instead. Only the scratch test harness sets
        /// another.</summary>
        internal static Func<bool> MultiUserChest = () => ChestShare.MultiUserChest;

        // ---- The number in the record, and the rule (plain values: the scratch harness checks them directly)

        /// <summary>The number written into a chest for a choice: the choice, and ModsMayReachBit unless other players' mods
        /// follow it.</summary>
        internal static int Encode(OthersMay choice, bool modsFollow) => ((int)choice & ChoiceBits) | (modsFollow ? 0 : ModsMayReachBit);

        /// <summary>The choice a number holds: 0 open, 1 look only, 2 (and 3, written by no version yet) kept out.</summary>
        internal static OthersMay ChoiceOf(int stored)
        {
            switch (stored & ChoiceBits)
            {
                case 0:
                    return OthersMay.Open;
                case 1:
                    return OthersMay.LookOnly;
                default:
                    return OthersMay.KeepOut;
            }
        }

        /// <summary>Whether other players' mods follow the choice (as their hands do).</summary>
        internal static bool ModsFollow(int stored) => (stored & ModsMayReachBit) == 0;

        /// <summary>What the hands of character <paramref name="me"/> may do at a chest marked by <paramref name="marker"/>
        /// (0: unmarked) holding <paramref name="stored"/>: everything for an unmarked chest and for the marker himself, else
        /// what the marker chose.</summary>
        internal static OthersMay ByHand(long marker, int stored, long me) =>
            marker == 0L || marker == me ? OthersMay.Open : ChoiceOf(stored);

        /// <summary>Whether the pack's own mods of character <paramref name="me"/> may count, take from or put into such a
        /// chest: where his hands may do everything, or where the marker lets mods in.</summary>
        internal static bool ModsMay(long marker, int stored, long me) => ByHand(marker, stored, me) == OthersMay.Open || !ModsFollow(stored);

        // ---- On a chest

        /// <summary>The number a chest holds (0: none, or not loaded): read from this computer's copy of its record.</summary>
        internal static int Stored(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() ? view.GetZDO().GetInt(Key) : 0;
        }

        /// <summary>What the hands of character <paramref name="me"/> may do at this chest. The Obliterator is never anyone's
        /// personal chest (a mark an older version let onto it means nothing).</summary>
        internal static OthersMay ByHand(Container chest, long me)
        {
            if (chest == null)
            {
                return OthersMay.Open;
            }
            long marker = ChestMine.MarkOf(chest);
            if (marker == 0L || marker == me)
            {
                return OthersMay.Open;
            }
            OthersMay may = ByHand(marker, Stored(chest), me);
            // Asked last, and only where it would keep him from something: the piece is looked up only then.
            return may == OthersMay.Open || IsObliterator(chest) ? OthersMay.Open : may;
        }

        /// <summary>Whether the pack's mods of character <paramref name="me"/> may use this chest.</summary>
        internal static bool ModsMayUse(Container chest, long me) => chest == null || ModsMay(ChestMine.MarkOf(chest), Stored(chest), me);

        /// <summary>Whether his own hands may take from, put into, move in or change this chest (its sort, its setup, its
        /// reserve mark): the open chest's buttons ask it. True for an unmarked chest and one he marked.</summary>
        internal static bool MayTouch(Container chest) => HisHands(chest) == OthersMay.Open;

        /// <summary>What his own hands may do at this chest, as this computer enforces it: look only becomes kept out where it
        /// can't be held (LookOnlyHolds: a member it needs is missing; MultiUserChest loaded, whose own patches pass some of a
        /// second player's moves on).</summary>
        internal static OthersMay HisHands(Container chest)
        {
            OthersMay may = ByHand(chest, Me());
            return may == OthersMay.LookOnly && (!LookOnlyHolds || MultiUserChest()) ? OthersMay.KeepOut : may;
        }

        /// <summary>The number to write where a chest holds <paramref name="stored"/> and this version wants
        /// <paramref name="wanted"/> (its own bits): this version's bits replaced, any a later version wrote kept.</summary>
        internal static int Mixed(int stored, int wanted) => (stored & ~OwnBits) | (wanted & OwnBits);

        /// <summary>Why the pack's mods of character <paramref name="me"/> may not use this chest, as words that follow "the
        /// chest is" (ChestFinder.WhyNot); null if they may.</summary>
        internal static string WhyNot(Container chest, long me)
        {
            long marker = ChestMine.MarkOf(chest);
            if (marker == 0L || marker == me)
            {
                return null;
            }
            int stored = Stored(chest);
            return ModsMay(marker, stored, me) ? null : Words(chest, ChoiceOf(stored));
        }

        /// <summary>Why the hands of character <paramref name="me"/> may not change this chest (a key acting on it by hand,
        /// such as Reserve Stock's), as words that follow "the chest is"; null if they may.</summary>
        internal static string WhyNotByHand(Container chest, long me)
        {
            OthersMay may = ByHand(chest, me);
            return may == OthersMay.Open ? null : Words(chest, may);
        }

        /// <summary>The words for a pointing line: "look only" or "closed to you"; empty for an open chest.</summary>
        internal static string HoverWords(OthersMay may) =>
            may == OthersMay.LookOnly ? "look only" : may == OthersMay.KeepOut ? "closed to you" : string.Empty;

        /// <summary>
        /// Writes the number into a chest this computer owns, and sends its record at once; true, sending nothing, when it
        /// holds that number already. False, changing nothing, on a chest it doesn't own or that isn't loaded. Never a
        /// removal (a value removed from a record reaches no other player): open is 0. The caller checks first that the
        /// chest is marked as his and may be changed now (ChestClaims.MayChange).
        /// </summary>
        internal static bool Write(Container chest, int stored)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }
            ZDO record = view.GetZDO();
            if (record.GetInt(Key) == stored)
            {
                return true;
            }
            record.Set(Key, stored);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            return true;
        }

        // ---- His hands at another player's marked chest (the patches, from ChestHooks; each does nothing unless Active)

        /// <summary>From the patch on Container.Interact (his own press of E): true, saying whose it is, when he's kept out
        /// of this chest (the game's open request is then never sent). Never throws.</summary>
        internal static bool RefusesOpen(Container chest)
        {
            try
            {
                if (!Active || chest == null || HisHands(chest) != OthersMay.KeepOut)
                {
                    return false;
                }
                Tell($"{ChestMine.Whose(chest)} personal chest:\nonly they can open it.");
                return true;
            }
            catch (Exception error)
            {
                Fault("pressing E on a chest", error);
                return false;
            }
        }

        /// <summary>
        /// Looks up the members it blocks through and patches them under the chest service's Harmony ID (ChestHooks.Find
        /// calls it once). Each is patched alone, so one that can't be found or patched costs only that one; look only
        /// holds only if every one it needs was patched, and the log says so otherwise.
        /// </summary>
        internal static void Patch(Harmony harmony)
        {
            Type gui = typeof(InventoryGui);
            dragGo = gui.GetField("m_dragGo", AnyInstance);
            dragInventory = gui.GetField("m_dragInventory", AnyInstance);
            var lost = new List<string>();
            if (dragGo == null || dragGo.FieldType != typeof(GameObject))
            {
                dragGo = null;
                lost.Add("InventoryGui.m_dragGo");
            }
            if (dragInventory == null || dragInventory.FieldType != typeof(Inventory))
            {
                dragInventory = null;
                lost.Add("InventoryGui.m_dragInventory");
            }
            bool all = lost.Count == 0;
            all &= Hook(harmony, gui.GetMethod("OnSelectedItem", AnyInstance, null,
                new[] { typeof(InventoryGrid), typeof(ItemDrop.ItemData), typeof(Vector2i), typeof(InventoryGrid.Modifier) }, null),
                nameof(SelectedItemPrefix), Priority.First, lost, "InventoryGui.OnSelectedItem");
            // After the item marks' own prefix (Priority.First), so a right-click that marks a favourite isn't a touch; before
            // other mods' ordinary ones (High), so one that uses the item itself doesn't get there first.
            all &= Hook(harmony, gui.GetMethod("OnRightClickItem", AnyInstance, null,
                new[] { typeof(InventoryGrid), typeof(ItemDrop.ItemData), typeof(Vector2i) }, null),
                nameof(RightClickPrefix), Priority.High, lost, "InventoryGui.OnRightClickItem");
            all &= Hook(harmony, gui.GetMethod("OnTakeAll", AnyInstance, null, Type.EmptyTypes, null),
                nameof(WindowButtonPrefix), Priority.First, lost, "InventoryGui.OnTakeAll");
            // First, before StoreStash's Place stacks (which looks) and Reserve Stock's own.
            all &= Hook(harmony, gui.GetMethod("OnStackAll", AnyInstance, null, Type.EmptyTypes, null),
                nameof(WindowButtonPrefix), Priority.First, lost, "InventoryGui.OnStackAll");
            all &= Hook(harmony, gui.GetMethod("OnDropOutside", AnyInstance, null, Type.EmptyTypes, null),
                nameof(DropOutsidePrefix), Priority.First, lost, "InventoryGui.OnDropOutside");
            all &= Hook(harmony, typeof(Container).GetMethod(nameof(Container.StackAll), AnyInstance, null, Type.EmptyTypes, null),
                nameof(ContainerStackAllPrefix), Priority.First, lost, "Container.StackAll");
            all &= Hook(harmony, gui.GetMethod("UpdateContainer", AnyInstance, null, new[] { typeof(Player) }, null),
                nameof(UpdateContainerPostfix), -1, lost, "InventoryGui.UpdateContainer", postfix: true);
            // Not needed for look only: a window another mod opens directly on a chest he's kept out of, and the hammer's
            // removal of a chest he may only look at or is kept out of, while it holds anything.
            Hook(harmony, gui.GetMethod("Show", AnyInstance, null, new[] { typeof(Container), typeof(int) }, null),
                nameof(ShowPrefix), Priority.First, lost, "InventoryGui.Show");
            Hook(harmony, typeof(Container).GetMethod(nameof(Container.CanBeRemoved), AnyInstance, null, Type.EmptyTypes, null),
                nameof(CanBeRemovedPostfix), -1, lost, "Container.CanBeRemoved", postfix: true);
            LookOnlyHolds = all;
            if (lost.Count > 0)
            {
                ZLog.LogWarning("[QoL Mods chest service] Other players' personal chests: the game has changed, and " + string.Join(", ", lost) +
                                (all ? " couldn't be used: another mod could still open a chest its marker keeps closed."
                                     : " couldn't be used: a chest its marker lets you only look at stays closed instead."));
            }
        }

        // One patch; false (and its name in lost) if the member is missing or the patch fails.
        private static bool Hook(Harmony harmony, MethodInfo target, string body, int priority, List<string> lost, string name, bool postfix = false)
        {
            if (target == null || harmony == null)
            {
                lost.Add(name);
                return false;
            }
            try
            {
                var method = new HarmonyMethod(AccessTools.Method(typeof(ChestAccess), body));
                if (priority >= 0)
                {
                    method.priority = priority;
                }
                if (postfix)
                {
                    harmony.Patch(target, postfix: method);
                }
                else
                {
                    harmony.Patch(target, prefix: method);
                }
                return true;
            }
            catch (Exception error)
            {
                lost.Add($"{name} ({error.Message})");
                return false;
            }
        }

        /// <summary>
        /// Whether a click in his inventory window (InventoryGui.OnSelectedItem: a click, drag or drop on a cell, with
        /// Ctrl, Shift or the split's modifier) would touch an open chest he may only look at: a click on one of the chest's
        /// items or a drop on its cells, dropping a stack carried from it anywhere, or moving an item of his into it (Ctrl or
        /// Shift + click). A click on an empty cell of the chest with nothing carried does nothing in the game either. Plain
        /// values, for the harness; null chest: nothing is look only.
        /// </summary>
        internal static bool Touches(Inventory lookOnly, Inventory clickedGrid, ItemDrop.ItemData item, InventoryGrid.Modifier modifier,
                                     bool dragging, Inventory draggedFrom)
        {
            if (lookOnly == null)
            {
                return false;
            }
            if (clickedGrid == lookOnly)
            {
                return dragging || item != null;
            }
            if (dragging)
            {
                return draggedFrom == lookOnly;
            }
            return item != null && modifier == InventoryGrid.Modifier.Move;
        }

        // The chest open in his window that he may only look at (or is kept out of: opened by another mod, or closed to him
        // while it was open), or null.
        private static Container LookOnlyOpen(InventoryGui gui)
        {
            if (!Active || gui == null)
            {
                return null;
            }
            Container open = ChestHooks.OpenChest(gui);
            return open != null && HisHands(open) != OthersMay.Open ? open : null;
        }

        private static bool Dragging(InventoryGui gui, out Inventory from)
        {
            from = dragInventory != null ? dragInventory.GetValue(gui) as Inventory : null;
            GameObject carried = dragGo != null ? dragGo.GetValue(gui) as GameObject : null;
            return carried != null;   // Unity's own null test
        }

        // A move refused: a short line, once a frame.
        private static void Refused(Container chest) => Tell($"{ChestMine.Whose(chest)} personal chest:\nlook, don't touch.");

        private static void Tell(string text)
        {
            int frame = ChestClaims.Frame();
            if (frame == saidIn && text == saidText)
            {
                return;
            }
            saidIn = frame;
            saidText = text;
            Say(text);
        }

        private static void Fault(string what, Exception error)
        {
            if (Faults.Add(what))
            {
                ZLog.LogWarning($"[QoL Mods chest service] Other players' personal chests: {what} couldn't be checked, so it went ahead: {error}");
            }
        }

        // ---- The patch bodies (positional arguments, so a renamed parameter can't stop a patch from applying)

        /// <summary>Prefix, first, on InventoryGui.OnSelectedItem(grid, item, pos, modifier).</summary>
        internal static bool SelectedItemPrefix(InventoryGui __instance, InventoryGrid __0, ItemDrop.ItemData __1, InventoryGrid.Modifier __3, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return true;   // stopped already: a true can't undo it
            }
            try
            {
                Container open = LookOnlyOpen(__instance);
                if (open == null)
                {
                    return true;
                }
                bool dragging = Dragging(__instance, out Inventory from);
                if (!Touches(open.GetInventory(), __0 != null ? __0.GetInventory() : null, __1, __3, dragging, from))
                {
                    return true;
                }
                Refused(open);
                return false;
            }
            catch (Exception error)
            {
                Fault("a click in a chest's window", error);
                return true;
            }
        }

        /// <summary>Prefix on InventoryGui.OnRightClickItem(grid, item, pos): using an item in the chest's cells (eating it,
        /// equipping it from there) takes it.</summary>
        internal static bool RightClickPrefix(InventoryGui __instance, InventoryGrid __0, ItemDrop.ItemData __1, bool __runOriginal)
        {
            if (!__runOriginal || __1 == null || __0 == null)
            {
                return true;
            }
            try
            {
                Container open = LookOnlyOpen(__instance);
                if (open == null || __0.GetInventory() != open.GetInventory())
                {
                    return true;
                }
                Refused(open);
                return false;
            }
            catch (Exception error)
            {
                Fault("a right-click in a chest's window", error);
                return true;
            }
        }

        /// <summary>Prefix, first, on InventoryGui.OnTakeAll and OnStackAll: Take all and Place stacks.</summary>
        internal static bool WindowButtonPrefix(InventoryGui __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return true;
            }
            try
            {
                Container open = LookOnlyOpen(__instance);
                if (open == null)
                {
                    return true;
                }
                Refused(open);
                return false;
            }
            catch (Exception error)
            {
                Fault("Take all or Place stacks", error);
                return true;
            }
        }

        /// <summary>Prefix, first, on InventoryGui.OnDropOutside: dropping on the ground a stack carried from the chest.</summary>
        internal static bool DropOutsidePrefix(InventoryGui __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return true;
            }
            try
            {
                Container open = LookOnlyOpen(__instance);
                if (open == null || !Dragging(__instance, out Inventory from) || from != open.GetInventory())
                {
                    return true;
                }
                Refused(open);
                return false;
            }
            catch (Exception error)
            {
                Fault("dropping a stack from a chest", error);
                return true;
            }
        }

        /// <summary>Prefix, first, on Container.StackAll: holding E with its window open (or another mod's call) asks the
        /// chest's owner to stack into it.</summary>
        internal static bool ContainerStackAllPrefix(Container __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return true;
            }
            try
            {
                if (!Active || __instance == null || HisHands(__instance) == OthersMay.Open)
                {
                    return true;
                }
                Refused(__instance);
                return false;
            }
            catch (Exception error)
            {
                Fault("holding E at a chest", error);
                return true;
            }
        }

        /// <summary>Prefix, first, on InventoryGui.Show(container, group): a window another mod opens directly on a chest
        /// he's kept out of is refused (his own E never gets that far). Each opening may say again that it's look only, and
        /// starts with Take all and Place stacks given back (greyed again within the frame if it's look only), so a button
        /// another module copies from Place stacks as the window opens is never a greyed copy.</summary>
        internal static bool ShowPrefix(InventoryGui __instance, Container __0, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return true;
            }
            try
            {
                told = null;
                if (__instance != null)
                {
                    Grey(__instance, false);
                }
                if (__0 == null || !Active || HisHands(__0) != OthersMay.KeepOut)
                {
                    return true;
                }
                Tell($"{ChestMine.Whose(__0)} personal chest:\nonly they can open it.");
                return false;
            }
            catch (Exception error)
            {
                Fault("opening a chest's window", error);
                return true;
            }
        }

        /// <summary>
        /// Postfix on Container.CanBeRemoved, which the hammer's removal asks (Piece.CanBeRemoved): a chest another player
        /// marked that he may only look at or is kept out of can't be removed while it holds anything, as the game keeps a
        /// Personal Chest that isn't empty (its contents would drop at his feet); the game then says it can't be removed now.
        /// Empty, it goes as the game lets any chest go.
        /// </summary>
        internal static void CanBeRemovedPostfix(Container __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            try
            {
                if (Active && __instance != null && HisHands(__instance) != OthersMay.Open && __instance.GetInventory() != null &&
                    __instance.GetInventory().NrOfItems() > 0)
                {
                    __result = false;
                }
            }
            catch (Exception error)
            {
                Fault("removing a chest", error);
            }
        }

        /// <summary>Postfix on InventoryGui.UpdateContainer (every frame while his inventory shows): while the chest showing
        /// is look only for him, Take all and Place stacks are greyed (Portion Control's -1 and Max weight follow Take all),
        /// and the first frame it shows says so; put back as they were once it isn't.</summary>
        internal static void UpdateContainerPostfix(InventoryGui __instance)
        {
            try
            {
                Container open = LookOnlyOpen(__instance);
                bool look = open != null && open.IsOwner();
                Grey(__instance, look);
                if (look && !ReferenceEquals(told, open))
                {
                    told = open;
                    Tell($"{ChestMine.Whose(open)} personal chest:\nyou can look, but not take or put anything in.");
                }
            }
            catch (Exception error)
            {
                Fault("greying a chest window's buttons", error);
            }
        }

        // Take all and Place stacks greyed while look only, and given back once (only if greyed here).
        private static void Grey(InventoryGui gui, bool on)
        {
            if (!on && !greyed)
            {
                return;
            }
            greyed = on;
            if (gui.m_takeAllButton != null)
            {
                gui.m_takeAllButton.interactable = !on;
            }
            if (gui.m_stackAllButton != null)
            {
                gui.m_stackAllButton.interactable = !on;
            }
        }

        // His character's player ID, or 0 before he has one.
        private static long Me()
        {
            try
            {
                return ChestClaims.PlayerId();
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        // "Ada's personal chest (look, don't touch)": words that follow "the chest is".
        private static string Words(Container chest, OthersMay may) =>
            $"{ChestMine.Whose(chest, capital: false)} personal chest ({(may == OthersMay.LookOnly ? "look, don't touch" : "kept to themselves")})";
    }
}
