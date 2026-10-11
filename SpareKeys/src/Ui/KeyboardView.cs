using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpareKeys
{
    /// <summary>
    /// The keyboard picture (and the mouse beside it), built once under a parent: one cap per key, coloured by its state,
    /// with its label, a corner mark naming the modifiers others use it with (C, A, S), and with "Letters on keys" a letter
    /// for its state. Used by the window (whose keys take clicks) and by the two panels (whose keys never do: no raycast
    /// target anywhere, the pointer is found by measuring).
    /// </summary>
    internal sealed class KeyboardView
    {
        private sealed class Cap
        {
            internal int Id;
            internal RectTransform Rect;
            internal Image Back;
            internal Outline Edge;
            internal Text Label;
            internal Text Corner;
            internal Text Letter;
        }

        private static readonly Color EdgeColour = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color MarkedEdge = new Color(1f, 0.9f, 0.4f, 1f);
        private static readonly Dictionary<int, string> printed = new Dictionary<int, string>();

        private readonly List<Cap> caps = new List<Cap>();
        private int marked = -1;

        internal RectTransform Root { get; private set; }

        internal float Width { get; private set; }

        internal float Height { get; private set; }

        /// <summary>
        /// Builds the picture under <paramref name="parent"/> at (<paramref name="x"/>, <paramref name="y"/>), a key
        /// <paramref name="unit"/> pixels wide. <paramref name="onClick"/>: null for a picture that takes no clicks.
        /// </summary>
        internal static KeyboardView Build(Transform parent, float x, float y, float unit, KeyboardKind kind, bool mouse, Action<int> onClick)
        {
            var view = new KeyboardView();
            Layout layout = Layout.Of(kind);
            view.Width = (layout.Width + (mouse ? Layout.MouseGap + Layout.MouseWidth : 0f)) * unit;
            view.Height = Mathf.Max(layout.Height, mouse ? 1.25f + Layout.MouseHeight : 0f) * unit;
            GameObject root = Ui.Box("SpareKeys_Keyboard", parent, x, y, view.Width, view.Height);
            view.Root = (RectTransform)root.transform;
            foreach (KeySpot spot in layout.Keys)
            {
                view.AddCap(spot, 0f, 0f, unit, onClick);
            }
            if (mouse)
            {
                float mx = (layout.Width + Layout.MouseGap) * unit;
                float my = 1.25f * unit;
                Rect body = Layout.MouseBody;
                GameObject shell = Ui.Box("SpareKeys_MouseBody", root.transform, mx + body.x * unit, my + body.y * unit, body.width * unit, body.height * unit,
                                          new Color(0.16f, 0.14f, 0.12f, 0.95f));
                shell.AddComponent<Outline>().effectColor = EdgeColour;
                foreach (KeySpot spot in layout.Mouse)
                {
                    view.AddCap(spot, mx, my, unit, onClick);
                }
            }
            return view;
        }

        private void AddCap(KeySpot spot, float ox, float oy, float unit, Action<int> onClick)
        {
            const float gap = 0.06f;
            float px = ox + (spot.X + gap / 2f) * unit;
            float py = oy + (spot.Y + gap / 2f) * unit;
            float w = (spot.W - gap) * unit;
            float h = (spot.H - gap) * unit;
            GameObject box = Ui.Box("SpareKeys_Key", Root, px, py, w, h, Color.grey);
            var cap = new Cap { Id = spot.Id, Rect = (RectTransform)box.transform, Back = box.GetComponent<Image>() };
            cap.Edge = box.AddComponent<Outline>();
            cap.Edge.effectColor = EdgeColour;
            cap.Edge.effectDistance = new Vector2(1f, -1f);
            int size = Mathf.Clamp(Mathf.RoundToInt(unit * 0.34f), 8, 20);
            string text = Printed(spot.Id);
            cap.Label = Ui.Label(box.transform, text, 1f, 0f, w - 2f, h, text.Length > 4 ? size - 2 : size, TextAnchor.MiddleCenter, Ui.Plain);
            int small = Mathf.Clamp(Mathf.RoundToInt(unit * 0.24f), 7, 14);
            cap.Corner = Ui.Label(box.transform, string.Empty, 1f, 0f, w - 3f, small + 3f, small, TextAnchor.UpperRight, Ui.Plain, bold: true);
            cap.Letter = Ui.Label(box.transform, string.Empty, 3f, h - small - 3f, w - 4f, small + 3f, small, TextAnchor.LowerLeft, Ui.Plain, bold: true);
            if (onClick != null)
            {
                cap.Back.raycastTarget = true;
                int id = spot.Id;
                box.AddComponent<Clicks>().Left = () => onClick(id);
            }
            caps.Add(cap);
        }

        // What the key's cap says: the character his keyboard prints for a letter, digit or symbol (another layout may
        // print another one at that place; the Input System knows), else the map's own label. Looked up once per key.
        private static string Printed(int id)
        {
            if (printed.TryGetValue(id, out string text))
            {
                return text;
            }
            KeyInfo info = KeyCatalog.Get(id);
            text = info?.Cap ?? string.Empty;
            if (info != null && (info.Region == Region.Letters || info.Region == Region.Digits || info.Region == Region.Symbols))
            {
                try
                {
                    string shown = ZInput.KeyCodeToDisplayName((KeyCode)id);
                    if (!string.IsNullOrEmpty(shown) && shown.Length <= 3 && !shown.StartsWith("$", StringComparison.Ordinal))
                    {
                        text = shown.ToUpperInvariant();
                    }
                }
                catch (Exception)
                {
                    // No keyboard known to the Input System yet: the US label.
                }
            }
            printed[id] = text;
            return text;
        }

        /// <summary>Colours every key by its state with these modifiers held.</summary>
        internal void Paint(Func<int, KeyState> stateOf, bool letters)
        {
            foreach (Cap cap in caps)
            {
                KeyState state = stateOf(cap.Id);
                Color colour = Settings.ColourOf(state.Status);
                cap.Back.color = colour;
                bool dark = state.Status == Status.Held;
                cap.Label.color = dark ? new Color(0.1f, 0.08f, 0.05f, 1f) : Ui.Plain;
                cap.Label.GetComponent<Outline>()?.SetEnabled(!dark);
                cap.Corner.text = state.Status == Status.NotOffered || state.Status == Status.Held ? string.Empty : state.Corner;
                cap.Letter.text = letters ? LetterOf(state.Status) : string.Empty;
            }
        }

        internal static string LetterOf(Status status)
        {
            switch (status)
            {
                case Status.Free:
                    return "F";
                case Status.WithModifier:
                    return "M";
                case Status.Used:
                    return "U";
                case Status.Shared:
                    return "2";
                case Status.Clash:
                    return "C";
                case Status.Maybe:
                case Status.MaybeClash:
                    return "?";
                case Status.Held:
                    return "H";
                default:
                    return "x";
            }
        }

        /// <summary>Outlines one key (the one under the pointer, or the one being rebound); -1 for none.</summary>
        internal void Mark(int id)
        {
            if (id == marked)
            {
                return;
            }
            marked = id;
            foreach (Cap cap in caps)
            {
                bool on = cap.Id == id;
                cap.Edge.effectColor = on ? MarkedEdge : EdgeColour;
                cap.Edge.effectDistance = on ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
        }

        /// <summary>The key under a screen point (the pointer), or -1. Measured, so it works on a picture that takes no
        /// clicks.</summary>
        internal int KeyAt(Vector2 screenPoint)
        {
            if (Root == null || !Root.gameObject.activeInHierarchy)
            {
                return -1;
            }
            foreach (Cap cap in caps)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cap.Rect, screenPoint, null))
                {
                    return cap.Id;
                }
            }
            return -1;
        }
    }

    internal static class OutlineExtensions
    {
        /// <summary>Switches a text's outline (Jötunn gives every text one) on or off; nothing for a text without one.</summary>
        internal static void SetEnabled(this Outline outline, bool on)
        {
            if (outline != null)
            {
                outline.enabled = on;
            }
        }
    }
}
