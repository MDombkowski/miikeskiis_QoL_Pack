using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using Outfitter.Loadouts;
using Outfitter.Stands;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// A loadout stand's places beside Outfitter's window (his words, 2026-10-07: "a hidden section that you can click an
    /// 'expand' button to view or a toggle to view that shows the Armor Stand's slots and what each slot is currently set to
    /// be occupied by, in order to visualize what the stand's display slots are and which are/aren't taken up (and by
    /// what)"): for the loadout picked, each place of the game's armour stand (StandPrototype) and the item of it a stand
    /// shows there (StandPlan.Pieces: each cell's Displayed, beside the grid), whether that item is also worn, and which
    /// shown items lose their place to another or have none. In the place of the other panels beside the window, one at a
    /// time; it shows and never changes anything. Built once with the window, as its child.
    /// </summary>
    internal static class StandPanel
    {
        private const float Left = 20f;
        private const float RowsTop = 104f;
        private const float RowHeight = 54f;
        private const int MostRows = 8;

        private static GameObject panel;
        private static Text subtitle;
        private static Text notes;
        private static readonly List<(Image Icon, Text Place, Text What)> rows = new List<(Image, Text, Text)>();
        private static string shown;

        /// <summary>"Hide": the window takes it away. Set by the window.</summary>
        internal static Action Hidden;

        internal static bool Shown => panel != null && panel.activeSelf;

        internal static void Build(Transform windowRoot, float x, float y, float height)
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
            }
            rows.Clear();
            shown = null;
            GUIManager gui = GUIManager.Instance;
            float width = Drawer.Width;
            panel = gui.CreateWoodpanel(windowRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height, false);
            panel.name = "Outfitter_StandSlots";
            Ui.Place(panel, x, y, width, height);
            Transform root = panel.transform;
            Ui.Label(root, "Stand slots", Left, 12f, 300f, 34f, 22, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Button hide = Ui.Button(root, "Hide", width - 120f, 16f, 100f, 30f, () => Module.Guard("hiding the stand slots", () => Hidden?.Invoke()));
            Ui.Help(hide, "Hides the stand's slots.");
            subtitle = Ui.Label(root, string.Empty, Left, 50f, width - 2f * Left, 50f, 14, TextAnchor.UpperLeft, Ui.Plain);
            for (int i = 0; i < MostRows; i++)
            {
                float rowY = RowsTop + i * RowHeight;
                Image icon = Ui.Icon(root, Left, rowY + 2f, 46f);
                Text place = Ui.Label(root, string.Empty, Left + 56f, rowY, 260f, 22f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
                Text what = Ui.Label(root, string.Empty, Left + 56f, rowY + 22f, width - 2f * Left - 56f, 26f, 14, TextAnchor.MiddleLeft, Ui.Plain);
                rows.Add((icon, place, what));
            }
            notes = Ui.Label(root, string.Empty, Left, RowsTop + MostRows * RowHeight + 6f, width - 2f * Left, height - RowsTop - MostRows * RowHeight - 26f,
                             14, TextAnchor.UpperLeft, Ui.Quiet);
            panel.SetActive(false);
        }

        internal static void SetShown(bool open)
        {
            if (panel != null && panel.activeSelf != open)
            {
                panel.SetActive(open);
            }
            shown = null;
        }

        /// <summary>The module's undo step, or a new scene: hidden, and drawn afresh next time.</summary>
        internal static void Forget() => SetShown(false);

        /// <summary>Every frame the window shows: drawn again when the loadout or the list changes.</summary>
        internal static void Refresh(Loadout loadout)
        {
            if (panel == null || !panel.activeSelf)
            {
                return;
            }
            string state = $"{loadout?.Id}|{Store.Version}|{StandPrototype.Places}";
            if (state == shown)
            {
                return;
            }
            shown = state;
            int places = StandPrototype.Places;
            foreach ((Image icon, Text place, Text what) in rows)
            {
                icon.enabled = false;
                place.text = string.Empty;
                what.text = string.Empty;
            }
            notes.text = string.Empty;
            if (loadout == null)
            {
                subtitle.text = "Pick a loadout on the left.";
                return;
            }
            if (places == 0)
            {
                subtitle.text = "The game's armour stand isn't in this game (another mod may have taken it out).";
                return;
            }
            subtitle.text = $"{loadout.Name} on a loadout stand: each place, and what it shows there. A cell's Displayed (beside the grid) " +
                            "puts it on the stand; its Worn puts it on you when you equip the loadout.";
            Func<string, int> fit = StandPrototype.FitForPrefab;
            List<StandPiece> pieces = StandPlan.PiecesBy(loadout, fit);
            IReadOnlyList<int> listed = StandPrototype.Listed;
            for (int row = 0; row < listed.Count && row < rows.Count; row++)
            {
                int i = listed[row];
                (Image icon, Text place, Text what) = rows[row];
                place.text = StandPrototype.NameOf(i);
                StandPiece piece = pieces.FirstOrDefault(each => each.Place == i);
                if (piece == null)
                {
                    what.text = "Empty";
                    what.color = Ui.Quiet;
                    continue;
                }
                LoadoutItem item = piece.Item;
                ItemDrop.ItemData template = Codes.Template(item.Prefab);
                Sprite[] sprites = template?.m_shared?.m_icons;
                icon.sprite = sprites != null && sprites.Length > 0 ? sprites[Mathf.Clamp(item.Variant, 0, sprites.Length - 1)] : null;
                icon.enabled = icon.sprite != null;
                what.text = $"{Equip.Messages.NameOf(item)}: {(item.Worn != WornAs.None ? "shown, and worn when you put it on" : "shown only, carried when you put it on")}";
                what.color = Ui.Plain;
            }
            List<LoadoutItem> crowded = StandPlan.NotShown(loadout, pieces, fit, out List<LoadoutItem> noPlace);
            var lines = new List<string>();
            if (crowded.Count > 0)
            {
                lines.Add("Displayed, but another item has its place (the first marked Displayed by hand wins, else the first in the loadout): " +
                          string.Join(", ", crowded.Select(each => $"{Equip.Messages.NameOf(each)} ({StandPrototype.NameOf(fit(each.Prefab))})")) + ".");
            }
            if (noPlace.Count > 0)
            {
                lines.Add("Displayed, but no place of a stand takes it: " + string.Join(", ", noPlace.Select(each => Equip.Messages.NameOf(each))) + ".");
            }
            if (listed.Count > rows.Count)
            {
                lines.Add($"The stand has {listed.Count} places; the first {rows.Count} are listed.");
            }
            notes.text = string.Join("\n", lines);
        }
    }
}
