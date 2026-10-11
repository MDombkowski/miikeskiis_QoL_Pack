using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// The cells he has picked to save (his words: "hold a keyboard shortcut to select what inventory slots you want
    /// saved"): in his inventory, hold the pick key (Right Alt; Shift, Ctrl and Left Alt clicks are taken by the game,
    /// MyLittleUI and StoreStash) and left-click a cell to pick it, again to unpick it; or, in Outfitter's window, the
    /// "Choose cells" button. A picked cell carries an orange frame in his inventory. Picks are cells, not items: they
    /// stay as they are when items move, until he clears them, saves, or the character changes.
    /// </summary>
    internal static class Picks
    {
        private static readonly HashSet<Vector2i> Cells = new HashSet<Vector2i>();
        private static Player pickedFor;
        private static bool keyFailed;
        private static KeyboardShortcut failedKey = KeyboardShortcut.Empty;

        /// <summary>Goes up at every change, for the window to redraw.</summary>
        internal static int Version { get; private set; }

        internal static int Count
        {
            get
            {
                Forget();
                return Cells.Count;
            }
        }

        internal static bool Has(Vector2i cell)
        {
            Forget();
            return Cells.Contains(cell);
        }

        internal static List<Vector2i> All()
        {
            Forget();
            return new List<Vector2i>(Cells);
        }

        internal static void Toggle(Vector2i cell)
        {
            Forget();
            if (!Cells.Remove(cell))
            {
                Cells.Add(cell);
            }
            Version++;
        }

        internal static void Set(IEnumerable<Vector2i> cells)
        {
            Forget();
            Cells.Clear();
            foreach (Vector2i cell in cells)
            {
                Cells.Add(cell);
            }
            Version++;
        }

        /// <summary>Unpicks every cell. Also the module's undo step (with HideFrames).</summary>
        internal static void Clear()
        {
            if (Cells.Count > 0)
            {
                Cells.Clear();
                Version++;
            }
        }

        // Picks belong to one character: another (or a respawned one) starts with none.
        private static void Forget()
        {
            if (Player.m_localPlayer != pickedFor)
            {
                pickedFor = Player.m_localPlayer;
                Clear();
            }
        }

        /// <summary>Whether the pick key is held now. A key the game's input can't read (F13 to F15, Help, SysReq, the
        /// shifted characters: LESSONS_LEARNED) is said once and not tried again until it changes; never an error into the
        /// game's own click handling.</summary>
        internal static bool KeyHeld()
        {
            KeyboardShortcut key = Settings.PickKey.Value;
            if (key.MainKey == KeyCode.None || Settings.Picking.Value == PickingCells.ChooseCellsInTheWindow)
            {
                return false;
            }
            if (keyFailed && key.Equals(failedKey))
            {
                return false;
            }
            keyFailed = false;
            try
            {
                if (!ZInput.GetKey(key.MainKey, false))
                {
                    return false;
                }
                foreach (KeyCode modifier in key.Modifiers)
                {
                    if (!ZInput.GetKey(modifier, false))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception error)
            {
                keyFailed = true;
                failedKey = key;
                Module.Warn($"The key {key} of \"Pick cells\" can't be read ({error.Message}); pick in the window until it's changed.");
                return false;
            }
        }

        private static FieldInfo elements;

        /// <summary>The inventory window's player grid's cells (InventoryGrid's private m_elements).</summary>
        internal static List<InventoryElement> ElementsOf(InventoryGrid grid)
        {
            elements ??= AccessTools.Field(typeof(InventoryGrid), "m_elements");
            return elements?.GetValue(grid) as List<InventoryElement>;
        }

        private const string FrameName = "Outfitter_Pick";

        /// <summary>Shows or hides each cell's frame in his inventory: made once per cell, never catching a click, changed
        /// only when it should.</summary>
        internal static void DressFrames(InventoryGrid grid)
        {
            List<InventoryElement> cells = ElementsOf(grid);
            if (cells == null)
            {
                return;
            }
            bool on = Module.IsOn && Cells.Count > 0 && Player.m_localPlayer == pickedFor;
            foreach (InventoryElement cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }
                bool picked = on && Cells.Contains(cell.Position);
                Transform frame = cell.transform.Find(FrameName);
                if (frame == null)
                {
                    if (!picked)
                    {
                        continue;
                    }
                    frame = MakeFrame(cell.transform);
                }
                if (frame.gameObject.activeSelf != picked)
                {
                    frame.gameObject.SetActive(picked);
                }
            }
        }

        /// <summary>Hides every frame (switched off, or after an error).</summary>
        internal static void HideFrames()
        {
            InventoryGui gui = InventoryGui.instance;
            List<InventoryElement> cells = gui != null && gui.m_playerGrid != null ? ElementsOf(gui.m_playerGrid) : null;
            if (cells == null)
            {
                return;
            }
            foreach (InventoryElement cell in cells)
            {
                Transform frame = cell != null ? cell.transform.Find(FrameName) : null;
                if (frame != null)
                {
                    frame.gameObject.SetActive(false);
                }
            }
        }

        // Four thin orange bars just inside the cell's edge.
        private static Transform MakeFrame(Transform cell)
        {
            var frame = new GameObject(FrameName, typeof(RectTransform));
            frame.transform.SetParent(cell, false);
            var rect = (RectTransform)frame.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            Color colour = new Color(1f, 0.6f, 0.1f, 0.95f);
            Bar(frame.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero, colour);
            Bar(frame.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f), colour);
            Bar(frame.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f), colour);
            Bar(frame.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), Vector2.zero, colour);
            frame.transform.SetAsLastSibling();
            return frame.transform;
        }

        private static void Bar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color colour)
        {
            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = bar.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }
    }

    /// <summary>The pick: with the pick key held, a left click on a cell of his inventory picks or unpicks it, and the game's
    /// own click (pick up, split, move) is skipped. Off, or without the key, the game's click goes on.</summary>
    [HarmonyPatch(typeof(InventoryGrid), "OnLeftDown")]
    internal static class PickClick
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(InventoryGrid __instance, UIInputHandler clickHandler)
        {
            if (!Module.IsOn)
            {
                return true;
            }
            bool skip = false;
            Module.Guard("picking a cell", () =>
            {
                InventoryGui gui = InventoryGui.instance;
                if (gui == null || __instance != gui.m_playerGrid || clickHandler == null || !Picks.KeyHeld())
                {
                    return;
                }
                InventoryElement cell = clickHandler.gameObject.GetComponent<InventoryElement>();
                if (cell == null)
                {
                    return;
                }
                Picks.Toggle(cell.Position);
                skip = true;
            });
            return !skip;
        }
    }

    /// <summary>The frames on picked cells, drawn after the game draws his inventory's cells.</summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class PickFrames
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGrid __instance)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || __instance != gui.m_playerGrid)
            {
                return;
            }
            if (!Module.IsOn)
            {
                return;   // HideFrames took them down when it was switched off
            }
            Module.Guard("framing the picked cells", () => Picks.DressFrames(__instance));
        }
    }
}
