using System;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StoreStash
{
    /// <summary>
    /// The setup keys (card A41). Pointing at a chest, the setup key (Left Alt + J) opens it as E does, through the game's
    /// own request (so a chest someone has open says "in use", and a ward or a private chest says no), and its setup window
    /// opens beside it once its window shows. With a chest's window open, the setup key opens or closes its setup window,
    /// and Ctrl+C and Ctrl+V copy and paste setups; only there, since in the world Left Ctrl crouches, C walks and V picks
    /// up (and is his quick slot 2). Never while a text box has the keyboard (MyLittleUI's rename box, chat, a search
    /// field: Ctrl+C and Ctrl+V are its own then), the split window is open, or a configuration manager's window (F1) is,
    /// nor while the trash can's question is up; and only when the press is theirs (Keys: one of StoreStash's keys a
    /// press, the one with the most modifiers).
    /// </summary>
    internal static class SetupKeys
    {
        /// <summary>Whether a chest's window is open: the copy and paste keys count only then. Only the scratch test
        /// harness sets another: it has no inventory screen.</summary>
        internal static Func<bool> ChestWindowOpen = () =>
        {
            InventoryGui gui = InventoryGui.instance;
            return gui != null && InventoryGui.IsVisible() && GameHooks.OpenChest(gui) != null;
        };

        /// <summary>How long a press of the setup key waits for its chest's window to open (the owner's answer and the
        /// chest's arrival) before it's forgotten, in seconds.</summary>
        internal const float WaitForWindow = 6f;

        private static Container waitingFor;
        private static float waitingSince;

        internal static void Update()
        {
            if (!SetupSettings.Active)
            {
                waitingFor = null;
                return;
            }
            InventoryGui gui = InventoryGui.instance;
            Container open = gui != null && InventoryGui.IsVisible() ? GameHooks.OpenChest(gui) : null;
            if (!ReferenceEquals(waitingFor, null))   // a C# test: a chest destroyed meanwhile is still waited for, and dropped
            {
                if (waitingFor == null || Time.time - waitingSince > WaitForWindow || (open != null && open != waitingFor))
                {
                    waitingFor = null;   // gone, no answer in time (or "in use"), or another chest was opened meanwhile
                }
                else if (open == waitingFor)
                {
                    // Its window shows as its owner's yes arrives; the setup window shows once the chest itself is his
                    // (SetupWindow.Update).
                    Container chest = waitingFor;
                    waitingFor = null;
                    SetupWindow.Open(chest);
                }
            }
            // One of StoreStash's keys a press (the one with the most modifiers), and none under the trash can's question.
            if (Keys.Asking() || Keys.Pressed() != Keys.Which.Setup)
            {
                return;
            }
            if (open != null)
            {
                if (!KeysFree(gui))
                {
                    return;
                }
                if (Keys.IsPressed(SetupSettings.Key.Value))
                {
                    if (Setups.Settable(open))
                    {
                        SetupWindow.Toggle(open);
                    }
                    else
                    {
                        Messages.Center("This chest can't have a setup.");
                    }
                }
                else if (Keys.IsPressed(SetupSettings.CopyKey.Value))
                {
                    SetupClipboard.Copy(open);
                }
                else if (Keys.IsPressed(SetupSettings.PasteKey.Value))
                {
                    SetupClipboard.Paste(open);
                }
                return;
            }
            if (InventoryGui.IsVisible() || !Keys.IsPressed(SetupSettings.Key.Value) || !QuickStack.TakesKeys())
            {
                return;
            }
            Player player = Player.m_localPlayer;
            Container pointed = Pointed(player);
            if (pointed == null)
            {
                return;   // pointing at nothing that can have a setup: the key does nothing, as E does on nothing
            }
            // The game's own way of opening it, as E: it asks the chest's owner, who hands it over or says it's in use.
            if (pointed.Interact(player, false, false))
            {
                waitingFor = pointed;
                waitingSince = Time.time;
            }
        }

        /// <summary>The undo step, and switching off: a press still waiting for its chest is forgotten.</summary>
        internal static void Cancel() => waitingFor = null;

        /// <summary>The chest he's pointing at, if it can have a setup, else null.</summary>
        internal static Container Pointed(Player player)
        {
            GameObject hover = player != null ? player.GetHoverObject() : null;
            Container chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            return Setups.Settable(chest) ? chest : null;
        }

        // With a chest's window open: the keys are the window's, not a text box's or the split window's.
        private static bool KeysFree(InventoryGui gui)
        {
            if (gui.m_splitDialog != null && gui.m_splitDialog.IsActive)
            {
                return false;
            }
            if ((Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() ||
                QuickStack.ManagerWindowOpen())
            {
                return false;
            }
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null)
            {
                TMPro.TMP_InputField tmp = selected.GetComponent<TMPro.TMP_InputField>();
                if (tmp != null && tmp.isFocused)
                {
                    return false;
                }
                InputField plain = selected.GetComponent<InputField>();
                if (plain != null && plain.isFocused)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
