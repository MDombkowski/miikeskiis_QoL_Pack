using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// Keeps XPortal's panel usable while it shows (card A71, 2026-10-08). XPortal gives its panel the game's window
    /// group (UIGroupHandler) at the lowest priority, 0, and every frame the game makes a group's buttons dead (its
    /// CanvasGroup's interactable off: no hover, no click, and the panel's own Enter and Escape hints do nothing) while
    /// any other group of a higher priority is active anywhere, seen or not. He met it at the portal at the world's
    /// centre, twice after dying and respawning there: the pointer moved, nothing lit up, Escape did nothing, and only
    /// Alt+F4 got him out (XPortal's own Up and Down keys still changed the destination, which don't go through the
    /// group). So, while the panel shows, its group is lifted above every other (Priority): a portal's panel is the one
    /// window on screen that takes input then (XPortal holds the game's input while it shows), and the game treats it
    /// as the front window again. What was in front is written to the log once each time it shows, to learn which
    /// window it was. And if the panel is still dead for any reason, Enter presses OK and Escape presses Cancel (his
    /// ask), only then, so the game's own hints and this never both press a button.
    /// </summary>
    internal static class PanelFront
    {
        /// <summary>The priority XPortal's group gets while it shows: above any window group the game or another mod
        /// gives (theirs are small numbers).</summary>
        internal const int Priority = 10000;

        private const string OkName = "XPortal_OkayButton";
        private const string CancelName = "XPortal_CancelButton";

        // The group lifted, and the priority XPortal gave it, to give back when the mod is switched off.
        private static UIGroupHandler lifted;
        private static int liftedFrom;
        private static bool shown;

        /// <summary>Every frame (Module.Update): while XPortal's panel shows, keeps its group in front and stands in for
        /// its dead keys.</summary>
        internal static void Tick()
        {
            if (!Settings.Enabled.Value)
            {
                Restore();
                shown = false;
                return;
            }
            object panel = XPortalHooks.Panel;
            GameObject main = panel != null ? XPortalHooks.MainPanel(panel) : null;
            if (main == null || !main.activeInHierarchy)
            {
                shown = false;
                return;
            }
            UIGroupHandler group = main.GetComponent<UIGroupHandler>();
            if (group != null)
            {
                if (lifted != group)
                {
                    lifted = group;   // XPortal builds its panel once a world session
                    liftedFrom = group.m_groupPriority;
                }
                if (!shown)
                {
                    TellWhatWasInFront(group, liftedFrom);
                }
                group.m_groupPriority = Priority;
            }
            shown = true;
            if (PickMode.IsPicking)
            {
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false))
            {
                PressIfDead(main, OkName);
            }
            else if (ZInput.GetKeyDown(KeyCode.Escape, false))
            {
                PressIfDead(main, CancelName);
            }
        }

        /// <summary>Gives XPortal's group back the priority XPortal gave it: when the mod is switched off, and after an
        /// error (an undo step).</summary>
        internal static void Restore()
        {
            if (lifted != null)
            {
                lifted.m_groupPriority = liftedFrom;
            }
            lifted = null;
        }

        // Presses one of the panel's buttons, only while the game has it dead (its own Enter or Escape hint would press it
        // otherwise, and pressing twice would close the panel twice).
        private static void PressIfDead(GameObject main, string name)
        {
            Transform found = main.transform.Find(name);
            Button button = found != null ? found.GetComponent<Button>() : null;
            if (button == null || !button.gameObject.activeInHierarchy || button.IsInteractable())
            {
                return;
            }
            Module.Log.LogInfo($"XPortal's panel didn't take input, so {(name == OkName ? "Enter pressed OK" : "Escape pressed Cancel")} for it.");
            button.onClick.Invoke();
        }

        // Once each time the panel shows: the window groups of a higher priority than XPortal gave it that are active, any
        // of which would have made it dead.
        private static void TellWhatWasInFront(UIGroupHandler own, int ownPriority)
        {
            var front = new List<string>();
            foreach (UIGroupHandler group in UnityEngine.Object.FindObjectsByType<UIGroupHandler>(FindObjectsSortMode.None))
            {
                if (group != null && group != own && group.gameObject.activeInHierarchy && group.m_groupPriority > ownPriority)
                {
                    front.Add($"{PathOf(group.transform)} (priority {group.m_groupPriority})");
                }
            }
            if (front.Count > 0)
            {
                Module.Log.LogInfo($"XPortal's panel opened with {front.Count} other window{(front.Count == 1 ? "" : "s")} in front of it, " +
                                   $"which would have made its buttons dead: {string.Join("; ", front)}. Lifted it above them.");
            }
        }

        // A transform's place in the scene, its last four levels: enough to tell which window it is.
        private static string PathOf(Transform transform)
        {
            var names = new List<string>();
            for (Transform each = transform; each != null && names.Count < 4; each = each.parent)
            {
                names.Insert(0, each.name);
            }
            return string.Join("/", names) + (transform.parent != null && names.Count == 4 ? " …" : "");
        }
    }
}
