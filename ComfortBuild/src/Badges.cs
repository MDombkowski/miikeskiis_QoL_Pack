using System.Collections.Generic;
using QoLMods.Pictures;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComfortBuild
{
    /// <summary>
    /// What the mod adds to a piece's button in the build menu: its comfort in a corner (the game's own level-number
    /// look), and for a piece that would add nothing where he stands, its picture in grey (or darkened) with the game's
    /// small tick in a bottom corner. The buttons are the game's, kept in a pool and set up again for another piece each
    /// time the grid is filled (BuildUiPieceButton.Setup), so each is dressed again then, and whenever what is near him
    /// changes. Everything added lives under one child of the button, named ComfortBuild_Badge.
    /// </summary>
    internal static class Badges
    {
        private const string BadgeName = "ComfortBuild_Badge";

        private sealed class Badge
        {
            internal GameObject Root;
            internal TextMeshProUGUI Number;
            internal Image Veil;
            internal Image Mark;
            internal TextMeshProUGUI PlainMark;
            internal Sprite GreyShown;
        }

        private static readonly Dictionary<BuildUiPieceButton, Badge> ByButton = new Dictionary<BuildUiPieceButton, Badge>();

        /// <summary>Dresses one button for the piece it now shows (after the game's Setup).</summary>
        internal static void Dress(BuildUiPieceButton button)
        {
            if (button == null)
            {
                return;
            }
            Piece piece = button.Piece;
            bool show = Module.IsOn && piece != null && ComfortTab.IsComfort(piece) && (Menu.OnOurTab || Settings.EveryTab.Value);
            ByButton.TryGetValue(button, out Badge badge);
            if (!show)
            {
                if (badge != null)
                {
                    Hide(button, badge);
                }
                return;
            }
            if (badge == null || badge.Root == null)
            {
                Looks.Find(Menu.Ui);
                badge = Make(button);
                ByButton[button] = badge;
            }
            badge.Root.SetActive(true);
            Color color = Settings.NumberColor.Value;
            Corner corner = Settings.NumberCorner.Value;
            Place(badge.Number.rectTransform, corner, 30f, 20f, 3f, 1f);
            badge.Number.alignment = corner == Corner.TopLeft ? TextAlignmentOptions.TopLeft
                : corner == Corner.TopRight ? TextAlignmentOptions.TopRight
                : corner == Corner.BottomLeft ? TextAlignmentOptions.BottomLeft
                : TextAlignmentOptions.BottomRight;
            badge.Number.text = piece.m_comfort.ToString();
            badge.Number.color = color;

            bool nothing = Settings.GreyOut.Value && Nearby.Gain(piece) == 0;
            NothingLook look = Settings.Look.Value;
            // The tick goes in a bottom corner the number doesn't use: bottom right, else bottom left.
            Corner markCorner = corner == Corner.BottomRight ? Corner.BottomLeft : Corner.BottomRight;
            GameObject mark = badge.Mark != null ? badge.Mark.gameObject : badge.PlainMark.gameObject;
            Place((RectTransform)mark.transform, markCorner, 16f, 16f, 3f, 3f);
            mark.SetActive(nothing);
            if (badge.Mark != null)
            {
                badge.Mark.color = color;
            }
            else
            {
                badge.PlainMark.color = color;
            }

            Image icon = Game.Icon(button);
            bool wantGrey = nothing && look == NothingLook.GreyWithTick;
            Sprite grey = wantGrey && icon != null ? Greys.Of(piece.m_icon) : null;
            // A picture whose grey copy can't be made (or isn't made yet) is darkened instead, so it never looks as if
            // it adds something.
            bool veil = nothing && (look == NothingLook.DimmedWithTick || (wantGrey && grey == null));
            badge.Veil.gameObject.SetActive(veil);
            if (icon != null)
            {
                Sprite shown = grey != null ? grey : piece.m_icon;
                if (icon.sprite != shown)
                {
                    icon.sprite = shown;
                }
                badge.GreyShown = grey;
            }
        }

        /// <summary>Dresses every button of the grid again (what's near him changed, a grey copy arrived, a setting).</summary>
        internal static void DressAll(BuildUi ui)
        {
            List<BuildUiPieceButton> buttons = ui != null ? Game.PieceButtons(ui) : null;
            if (buttons == null)
            {
                return;
            }
            // Every button of the grid, also one a search hid: the search shows it again without setting it up.
            foreach (BuildUiPieceButton button in buttons)
            {
                if (button != null)
                {
                    Dress(button);
                }
            }
            // A button taken back into the game's pool keeps nothing of ours (one the search only hid is still the
            // grid's, and keeps its badge for when the search is cleared).
            var inGrid = new HashSet<BuildUiPieceButton>(buttons);
            foreach (KeyValuePair<BuildUiPieceButton, Badge> each in ByButton)
            {
                if (each.Key != null && !inGrid.Contains(each.Key))
                {
                    Hide(each.Key, each.Value);
                }
            }
        }

        /// <summary>Takes everything back: every badge hidden, every picture in colour again.</summary>
        internal static void UndressAll()
        {
            var gone = new List<BuildUiPieceButton>();
            foreach (KeyValuePair<BuildUiPieceButton, Badge> each in ByButton)
            {
                if (each.Key == null)
                {
                    gone.Add(each.Key);
                    continue;
                }
                Hide(each.Key, each.Value);
            }
            foreach (BuildUiPieceButton button in gone)
            {
                ByButton.Remove(button);
            }
        }

        /// <summary>Forgets every badge (a new build menu, in a new world: the old buttons are gone).</summary>
        internal static void Forget() => ByButton.Clear();

        private static void Hide(BuildUiPieceButton button, Badge badge)
        {
            if (badge.Root != null)
            {
                badge.Root.SetActive(false);
            }
            if (badge.Veil != null)
            {
                badge.Veil.gameObject.SetActive(false);
            }
            Image icon = Game.Icon(button);
            if (icon != null && badge.GreyShown != null && icon.sprite == badge.GreyShown && button.Piece != null)
            {
                icon.sprite = button.Piece.m_icon;
            }
            badge.GreyShown = null;
        }

        private static Badge Make(BuildUiPieceButton button)
        {
            Transform old = button.transform.Find(BadgeName);
            if (old != null)
            {
                Object.Destroy(old.gameObject);
            }
            var root = new GameObject(BadgeName, typeof(RectTransform));
            root.transform.SetParent(button.transform, false);
            root.transform.SetAsLastSibling();   // drawn over the picture, the star and the arrow
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var badge = new Badge { Root = root };

            // The veil for "Dimmed": just over the picture (inset 2, as the game's own picture is), under the game's
            // star and arrow, so it darkens the picture only.
            Transform oldVeil = button.transform.Find(BadgeName + "_Veil");
            if (oldVeil != null)
            {
                Object.Destroy(oldVeil.gameObject);
            }
            var veil = new GameObject(BadgeName + "_Veil", typeof(RectTransform));
            veil.transform.SetParent(button.transform, false);
            Image icon = Game.Icon(button);
            if (icon != null && icon.transform.parent == button.transform)
            {
                veil.transform.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1);
            }
            var veilRect = (RectTransform)veil.transform;
            veilRect.anchorMin = Vector2.zero;
            veilRect.anchorMax = Vector2.one;
            veilRect.offsetMin = new Vector2(2f, 2f);
            veilRect.offsetMax = new Vector2(-2f, -2f);
            badge.Veil = veil.AddComponent<Image>();
            badge.Veil.color = new Color(0f, 0f, 0f, Looks.VeilAlpha);
            badge.Veil.raycastTarget = false;
            veil.SetActive(false);

            badge.Number = Looks.Text(root.transform, "Number", true, 16f, true);

            if (Looks.Tick != null)
            {
                var mark = new GameObject("Mark", typeof(RectTransform));
                mark.transform.SetParent(root.transform, false);
                badge.Mark = mark.AddComponent<Image>();
                badge.Mark.sprite = Looks.Tick;
                badge.Mark.preserveAspect = true;
                badge.Mark.raycastTarget = false;
            }
            else
            {
                badge.PlainMark = Looks.Text(root.transform, "Mark", true, 16f, true);
                badge.PlainMark.text = "=";
                badge.PlainMark.alignment = TextAlignmentOptions.Center;
            }
            return badge;
        }

        // A child of the 64 × 64 button, width × height, in a corner, inset by x and y.
        private static void Place(RectTransform rect, Corner corner, float width, float height, float x, float y)
        {
            bool left = corner == Corner.TopLeft || corner == Corner.BottomLeft;
            bool top = corner == Corner.TopLeft || corner == Corner.TopRight;
            var anchor = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left ? x : -x, top ? -y : y);
        }
    }
}
