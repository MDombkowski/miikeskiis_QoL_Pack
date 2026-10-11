using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StoreStash
{
    /// <summary>
    /// The quick stack to the chests nearby, from the key or the button, and the personal quick stack into his personal
    /// chests only, from its own key or button (card A43): it asks the chest service (ChestStacker) with the rules the
    /// settings give, waits a moment if another module's claim is under way, and says what happened. A press of either
    /// while one is still going does nothing (chests other players own take a moment to be handed over).
    /// </summary>
    internal static class QuickStack
    {
        /// <summary>How long a press waits for another module's claim of the chest service to end before giving up.</summary>
        internal const float WaitFor = 5f;

        /// <summary>How long a quick stack under way may take before a new press is let through anyway: a claim settles
        /// within about 13 seconds at most (a chest nobody owns yet waits for an owner, then for the answer and the
        /// ownership), and a quick stack claims up to three times.</summary>
        internal const float GiveUpAfter = 45f;

        /// <summary>The game's clock (Time.time). Only the scratch test harness sets another.</summary>
        internal static System.Func<float> Clock = () => Time.time;

        /// <summary>How often a waiting press asks again: each ask plans afresh (every chest within the radius), so not
        /// every frame.</summary>
        internal const float AskEvery = 0.25f;

        // A configuration manager's plugin IDs (shudnal's, BepInEx's): both say whether their window is open.
        private static readonly string[] ManagerGuids = { "_shudnal.ConfigurationManager", "com.bepis.bepinex.configurationmanager" };

        private static float pendingSince = -1f;
        private static float runningSince = -1f;
        private static float askedAt = -1f;
        private static bool pendingPersonal;
        private static OneStack.Clicked pendingStack;   // one stack by a click (card A82): that very stack, and how much may go
        private static int started;
        private static bool lookedForManager;
        private static BaseUnityPlugin manager;
        private static PropertyInfo managerWindowOpen;

        internal static bool Running => runningSince >= 0f && Clock() - runningSince < GiveUpAfter;

        /// <summary>Every frame: a press waiting for another module's claim to end asks again (the keys are read by Keys).</summary>
        internal static void Update()
        {
            if (!Module.IsOn)
            {
                pendingSince = -1f;
                pendingStack = null;
                return;
            }
            if (pendingSince >= 0f && Clock() - askedAt >= AskEvery)
            {
                TryStart();
            }
        }

        /// <summary>A press of the quick stack key or button, or, with <paramref name="personal"/>, of the personal quick
        /// stack's (card A43): into his personal chests only.</summary>
        internal static void Ask(bool personal = false)
        {
            if (!Module.IsOn || Running || pendingSince >= 0f)
            {
                return;
            }
            if (!Settings.WithChestOpen.Value && ChestWindowOpen())
            {
                return;
            }
            pendingPersonal = personal;
            pendingStack = null;
            pendingSince = Clock();
            TryStart();
        }

        /// <summary>One stack by a click (card A82, OneStack): just <paramref name="stack"/>, by the quick stack key's rules
        /// (Settings.OneStackRules), even a favourite or on the hotbar, never more of it than it holds now; with a chest open
        /// only while "One stack while a chest is open" is on. Like a press of the key, nothing while a quick stack is
        /// under way. Whether it started (or waits its turn).</summary>
        internal static bool AskOne(ItemDrop.ItemData stack)
        {
            if (!CanAskOne() || stack == null || stack.m_stack <= 0)
            {
                return false;
            }
            pendingPersonal = false;
            pendingStack = new OneStack.Clicked(stack);
            pendingSince = Clock();
            TryStart();
            return true;
        }

        /// <summary>Whether a click on one stack would start now: StoreStash on, no quick stack under way or waiting, and a
        /// chest open only while "One stack while a chest is open" is on.</summary>
        internal static bool CanAskOne() =>
            Module.IsOn && !Running && pendingSince < 0f && (Settings.OneStackWithChestOpen.Value || !OneStack.ChestOpen());

        /// <summary>The undo step: a press still waiting is dropped.</summary>
        internal static void Cancel()
        {
            pendingSince = -1f;
            runningSince = -1f;
            pendingStack = null;
        }

        private static void TryStart()
        {
            askedAt = Clock();
            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting() || player.IsDead())
            {
                pendingSince = -1f;
                pendingStack = null;
                return;
            }
            bool personal = pendingPersonal;
            OneStack.Clicked one = pendingStack;
            string what = personal ? "Personal quick stack" : "Quick stack";
            if (one == null && !Favorites.List.Readable)
            {
                // His favourites would count as ordinary items and go (one stack by a click goes whatever they say).
                pendingSince = -1f;
                Messages.TopLeft($"{what}: your favorites file can't be read, so nothing was put away (see the log)");
                return;
            }
            StackRules rules = one != null ? Settings.OneStackRules(one) : personal ? Settings.PersonalRules() : Settings.Rules();
            string none = personal ? Personal.WhyNoneNearby(player, rules) : null;
            if (none != null)
            {
                pendingSince = -1f;
                Messages.AsSet(none);
                return;
            }
            runningSince = Clock();
            int number = ++started;
            if (ChestStacker.Start(Module.Chests, rules, null, result => Done(number, result, personal, one, rules)))
            {
                pendingSince = -1f;
                pendingStack = null;
                return;
            }
            runningSince = -1f;   // another module's claim is under way: asked again next frame, for a while
            if (Clock() - pendingSince > WaitFor)
            {
                pendingSince = -1f;
                pendingStack = null;
                Messages.TopLeft($"{what}: the chests nearby are busy, press again in a moment");
            }
        }

        // One stack by a click (<paramref name="one"/>, card A82) says what went of that stack, or why none did, where the
        // quick stack's line goes; never the hotbar note (he picked the stack himself).
        private static void Done(int number, StackResult result, bool personal, OneStack.Clicked one = null, StackRules rules = null)
        {
            if (number == started)   // not one that was given up on, ending while a later one runs
            {
                runningSince = -1f;
            }
            if (one != null)
            {
                Player me = Player.m_localPlayer;
                int stayed = me != null && me.GetInventory().ContainsItem(one.Stack) ? one.Stack.m_stack : 0;
                Messages.AfterQuickStack(result, single: OneStack.Name(one.Stack), stayed: stayed,
                    whyNothing: result.Moved == 0 ? OneStack.WhyNothing(result, one, rules) : null);
            }
            else
            {
                Messages.AfterQuickStack(result, personal);
            }
            Player player = Player.m_localPlayer;
            if (result.Moved > 0 && Settings.Sound.Value && player != null && InventoryGui.instance != null)
            {
                InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
            }
            foreach (Container chest in result.ChestsUsed)
            {
                LightUp.Light(chest);
            }
            Tidy.AfterQuickStack(result);   // Stack Overflow's "Tidy after each quick stack" (card A42), a moment later
            if (one == null && result.FromHotbar > 0 && !Settings.HotbarNoteShown.Value && Settings.Hotbar.Value == HotbarChoice.UnfavoritedOnly &&
                Settings.Message.Value != MessagePlace.Off)
            {
                // Once, where favourites keep things on the hotbar: HexQuickStackStorage left the hotbar alone, and his
                // favourites came from it. Where the quick stack's own line isn't: each place shows one line at a time, and
                // a line waiting replaces the one showing after a second (MessageHud).
                // On short lines (card A82): one line of it ran off both sides of the screen in the middle (40 points,
                // no wrapping); each of these is about half the narrowest screen's width there, and Messages fits it too.
                Settings.HotbarNoteShown.Value = true;
                KeyCode modifier = Settings.FavoriteModifier.Value;
                string keep = modifier == KeyCode.None
                    ? "To keep things there, set \"The hotbar\" to Off\n(F1, StoreStash)."
                    : $"{Settings.Spaced(modifier)} + right-click what should stay,\nor set \"The hotbar\" to Off (F1, StoreStash).";
                string note = $"Quick stack takes from your hotbar too.\n{keep}";
                if (Settings.Message.Value == MessagePlace.Center)
                {
                    Messages.TopLeft(note);
                }
                else
                {
                    Messages.Center(note);
                }
            }
        }

        private static bool ChestWindowOpen() => InventoryGui.IsVisible() && GameHooks.OpenChest(InventoryGui.instance) != null;

        /// <summary>
        /// Whether a key press is meant for the game, by the game's own rule for taking keys (Player.TakeInput), less its
        /// one exception: the inventory may be open, as HexQuickStackStorage's P worked there. So never while he types in
        /// chat, the console, a sign or a map pin's name, the build menu's search, in a menu or the trader's window, not
        /// while a text box of Unity's UI has the keyboard (another mod's search field, a chest's rename box), and not while
        /// a configuration manager's window is open. A text box of Unity's older UI (IMGUI) in another mod's window isn't
        /// seen: its focus lives in the engine and outlasts its window, so reading it could stop P for good.
        /// </summary>
        internal static bool TakesKeys()
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting())
            {
                return false;
            }
            if ((Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible() ||
                Menu.IsVisible() || (TextViewer.instance != null && TextViewer.instance.IsVisible()) || Minimap.IsOpen() || GameCamera.InFreeFly() ||
                PlayerCustomizaton.IsBarberGuiVisible())
            {
                return false;
            }
            if (Hud.instance != null && Hud.instance.m_buildUi != null && Hud.instance.m_buildUi.SearchFieldFocused)
            {
                return false;
            }
            return !Typing() && !ManagerWindowOpen();
        }

        // Whether a configuration manager's window is open (its search box is drawn by Unity's older UI, which the event
        // system doesn't see); false without one. As Map View Rules reads it.
        internal static bool ManagerWindowOpen()
        {
            if (!lookedForManager)
            {
                lookedForManager = true;
                foreach (string guid in ManagerGuids)
                {
                    if (!Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) || info.Instance == null)
                    {
                        continue;
                    }
                    PropertyInfo open = info.Instance.GetType().GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public);
                    if (open != null && open.PropertyType == typeof(bool) && open.GetIndexParameters().Length == 0)
                    {
                        manager = info.Instance;
                        managerWindowOpen = open;
                        break;
                    }
                }
            }
            if (manager == null)
            {
                return false;
            }
            try
            {
                return (bool)managerWindowOpen.GetValue(manager, null);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // A text box of any mod has the keyboard: the event system's selected object is a text field being edited.
        private static bool Typing()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }
            TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused)
            {
                return true;
            }
            InputField plain = selected.GetComponent<InputField>();
            return plain != null && plain.isFocused;
        }
    }
}
