using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// The dropdown of stands in Outfitter's window (job 30, Session 2: his words, "click that slot of the armor stand,
    /// click from a dropdown, and pick a different armor stand to move it to"): a short list beside the row clicked, one
    /// line per choice; a click on a line picks it, a click anywhere else closes it. More choices than its rows: the
    /// wheel scrolls them, and the heading says which it shows (review round 1 of Session 2, m7).
    /// </summary>
    internal static class StandMenu
    {
        private const float Width = 330f;
        private const float RowHeight = 28f;
        private const int MostRows = 14;

        private static GameObject catcher;
        private static GameObject panel;
        private static Text title;
        private static readonly List<(Clicks clicks, Text label)> rows = new List<(Clicks, Text)>();
        private static readonly List<Action> picks = new List<Action>();
        private static List<(string Label, bool Enabled, Action Pick)> shown = new List<(string, bool, Action)>();
        private static string heading = string.Empty;
        private static int first;

        internal static bool Showing => panel != null && panel.activeSelf;

        /// <summary>Shows the menu at (x, y) of the window (its top-left), titled, with these choices.</summary>
        internal static void Show(Transform window, float x, float y, string heading, List<(string Label, bool Enabled, Action Pick)> choices)
        {
            Build(window);
            StandMenu.heading = heading ?? string.Empty;
            shown = choices ?? new List<(string, bool, Action)>();
            first = 0;
            Render();
            Ui.Place(panel, x, y, Width, HeightFor(shown.Count));
            catcher.SetActive(true);
            catcher.transform.SetAsLastSibling();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        /// <summary>The menu's height for this many choices (at most a screenful of rows; the rest scroll).</summary>
        internal static float HeightFor(int choices) => 34f + Mathf.Min(choices, MostRows) * (RowHeight + 2f) + 8f;

        internal static void Close()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
            if (catcher != null)
            {
                catcher.SetActive(false);
            }
        }

        // The rows from the first shown on, and the heading with which they are when there are more than fit.
        private static void Render()
        {
            int count = Mathf.Min(shown.Count, MostRows);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, shown.Count - MostRows));
            title.text = shown.Count > MostRows ? $"{heading}  ({first + 1} to {first + count} of {shown.Count}: scroll)" : heading;
            picks.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                (Clicks clicks, Text label) = rows[i];
                bool on = i < count;
                clicks.gameObject.SetActive(on);
                if (!on)
                {
                    continue;
                }
                (string text, bool enabled, Action pick) = shown[first + i];
                label.text = text;
                label.color = enabled ? Ui.Plain : Ui.Quiet;
                int index = i;
                picks.Add(enabled ? pick : null);
                clicks.OnLeft = () =>
                {
                    Action chosen = index < picks.Count ? picks[index] : null;
                    Close();
                    chosen?.Invoke();
                };
            }
        }

        private static void Scrolled(int notches)
        {
            if (!Showing || shown.Count <= MostRows)
            {
                return;
            }
            first -= notches;
            Render();
        }

        private static void Build(Transform window)
        {
            if (panel != null && panel.transform.parent == window)
            {
                return;
            }
            rows.Clear();
            // A click anywhere outside the menu closes it.
            catcher = Ui.Box("Outfitter_StandMenuCatcher", window, 0f, 0f, 10f, 10f, new Color(0f, 0f, 0f, 0.15f));
            var catcherRect = (RectTransform)catcher.transform;
            catcherRect.anchorMin = Vector2.zero;
            catcherRect.anchorMax = Vector2.one;
            catcherRect.offsetMin = catcherRect.offsetMax = Vector2.zero;
            Image shade = catcher.GetComponent<Image>();
            shade.raycastTarget = true;
            Clicks outside = catcher.AddComponent<Clicks>();
            outside.Lights = false;
            outside.OnLeft = Close;
            outside.OnRight = Close;
            panel = Ui.Box("Outfitter_StandMenu", window, 0f, 0f, Width, 100f, new Color(0.1f, 0.08f, 0.06f, 0.97f));
            panel.GetComponent<Image>().raycastTarget = true;
            panel.AddComponent<WheelCatcher>().Turned = Scrolled;
            title = Ui.Label(panel.transform, string.Empty, 10f, 4f, Width - 20f, 26f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            title.resizeTextForBestFit = true;   // a long heading with "1 to 14 of 20: scroll" shrinks rather than wraps
            title.resizeTextMinSize = 11;
            title.resizeTextMaxSize = 15;
            for (int i = 0; i < MostRows; i++)
            {
                GameObject row = Ui.Box("Row", panel.transform, 6f, 34f + i * (RowHeight + 2f), Width - 12f, RowHeight, Ui.RowPlain);
                Image background = row.GetComponent<Image>();
                background.raycastTarget = true;
                Clicks clicks = row.AddComponent<Clicks>();
                clicks.Background = background;
                Text label = Ui.Label(row.transform, string.Empty, 8f, 0f, Width - 28f, RowHeight, 14);
                rows.Add((clicks, label));
            }
            catcher.SetActive(false);
            panel.SetActive(false);
        }
    }
}
