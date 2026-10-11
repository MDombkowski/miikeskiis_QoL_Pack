// The QoL Mods item stands part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemStands\. Every mod that uses
// it carries a copy in its own parts\ItemStands\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QoLMods.ItemStands
{
    /// <summary>
    /// A mark on one item's icon: made the first time it shows, as a child of the icon's host (an inventory cell's root,
    /// so a mod that scales the icon or moves the cell takes the mark along), drawn over what the host already shows,
    /// never catching a click, and dressed again only when what it shows changes; hidden, it stays for the next time.
    /// Each caller names its marks (&lt;its module's Id&gt;_StandMark for the item stands' arrows, _ArmorMark for the
    /// armor stand's), so modules never touch each other's, and a host carries one mark of each name.
    /// </summary>
    internal static class StandBadge
    {
        /// <summary>The icon size a mark's size is given for: a full inventory cell.</summary>
        internal const float FullIcon = 64f;

        private sealed class Made
        {
            internal string Name;
            internal GameObject Root;
            internal RectTransform Rect;
            internal Image Picture;
            internal TextMeshProUGUI Text;
            internal Fit Shown;
            internal Fit Dressed;
            internal StandLook Look;
            internal float Icon;
            internal bool AsText;
        }

        /// <summary>The marks made, by name, then by host.</summary>
        private static readonly Dictionary<string, Dictionary<Transform, Made>> ByName = new Dictionary<string, Dictionary<Transform, Made>>();
        private static int pruneAt = 256;

        private static Dictionary<Transform, Made> Named(string name)
        {
            if (!ByName.TryGetValue(name ?? string.Empty, out Dictionary<Transform, Made> byHost))
            {
                byHost = ByName[name ?? string.Empty] = new Dictionary<Transform, Made>();
            }
            return byHost;
        }

        private static TMP_FontAsset font;
        private static Material fontMaterial;

        /// <summary>
        /// Shows the mark named <paramref name="name"/> on <paramref name="host"/> for <paramref name="fit"/> (one mark's
        /// part of an item's fit: Wall, Flat or Both for the arrows, Armor for the armor stand's), in
        /// <paramref name="look"/>, or hides it when the fit is None, the look doesn't mark it, or the look is null.
        /// <paramref name="icon"/> is the size in the host's units of the icon the mark sits on (64 for an inventory cell,
        /// 32 for the crafting list's): the look's size is for a 64-unit icon, so smaller icons get smaller marks.
        /// </summary>
        internal static void Show(Transform host, string name, Fit fit, StandLook look, float icon)
        {
            if (host == null)
            {
                return;
            }
            bool on = look != null && fit != Fit.None && look.Shows(fit);
            Dictionary<Transform, Made> byHost = Named(name);
            byHost.TryGetValue(host, out Made made);
            if (made != null && made.Root == null)
            {
                byHost.Remove(host);   // destroyed with something of the host's
                made = null;
            }
            if (!on)
            {
                if (made != null && made.Shown != Fit.None)
                {
                    made.Root.SetActive(false);
                    made.Shown = Fit.None;
                }
                return;
            }
            if (made == null)
            {
                made = Make(host, name);
                byHost[host] = made;
                Prune(byHost);
            }
            if (made.Dressed != fit || !ReferenceEquals(made.Look, look) || made.Icon != icon || (made.AsText != WantsText(look)))
            {
                Dress(made, fit, look, icon);
            }
            if (!made.Root.activeSelf)
            {
                made.Root.SetActive(true);
            }
            made.Shown = fit;
        }

        /// <summary>Hides one host's mark, if it has one of this name.</summary>
        internal static void Hide(Transform host, string name)
        {
            if (host != null && Named(name).TryGetValue(host, out Made made) && made.Root != null && made.Shown != Fit.None)
            {
                made.Root.SetActive(false);
                made.Shown = Fit.None;
            }
        }

        /// <summary>Hides every mark of this name at once (a module switched off, or its error's undo step).</summary>
        internal static void HideAll(string name)
        {
            foreach (Made made in Named(name).Values)
            {
                if (made.Root != null && made.Shown != Fit.None)
                {
                    made.Root.SetActive(false);
                    made.Shown = Fit.None;
                }
            }
        }

        /// <summary>How many marks of this name show now (a place that shows none can skip hiding them).</summary>
        internal static int Showing(string name)
        {
            int count = 0;
            foreach (Made made in Named(name).Values)
            {
                if (made.Root != null && made.Shown != Fit.None)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>The fit a host's mark of this name shows now (None when hidden or never made), for the harness and the
        /// log.</summary>
        internal static Fit ShownOn(Transform host, string name) =>
            host != null && Named(name).TryGetValue(host, out Made made) && made.Root != null ? made.Shown : Fit.None;

        // Forgets hosts destroyed since (a grid rebuilt for a bigger inventory, a crafting list filled again), now and then.
        private static void Prune(Dictionary<Transform, Made> byHost)
        {
            if (byHost.Count < pruneAt)
            {
                return;
            }
            var gone = new List<Transform>();
            foreach (KeyValuePair<Transform, Made> each in byHost)
            {
                if (each.Key == null || each.Value.Root == null)
                {
                    gone.Add(each.Key);
                }
            }
            foreach (Transform host in gone)
            {
                byHost.Remove(host);
            }
            int most = 0;
            foreach (Dictionary<Transform, Made> each in ByName.Values)
            {
                most = Mathf.Max(most, each.Count);
            }
            pruneAt = Mathf.Max(256, most * 2);
        }

        private static bool WantsText(StandLook look) => look.Look != MarkLook.DrawnArrows && FindFont();

        private static Made Make(Transform host, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.SetParent(host, false);
            rect.SetAsLastSibling();   // over the icon, the counts and the game's own marks
            Image picture = root.GetComponent<Image>();
            picture.raycastTarget = false;   // never catches a click: dragging, splitting and clicking the item work as before
            picture.preserveAspect = true;
            return new Made { Name = name, Root = root, Rect = rect, Picture = picture, Shown = Fit.None, Dressed = Fit.None };
        }

        private static void Dress(Made made, Fit fit, StandLook look, float icon)
        {
            float scale = (icon > 0f ? icon : FullIcon) / FullIcon;
            bool asText = WantsText(look);
            float inset = StandLook.Inset * scale;
            Vector2 size;
            float margin;
            if (asText)
            {
                made.Picture.enabled = false;
                TextMeshProUGUI text = TextOf(made);
                float fontSize = look.Size * scale;
                text.fontSize = fontSize;
                text.color = look.Colour;
                text.text = look.TextOf(fit);
                text.alignment = AlignmentOf(look.PlaceOf(fit));
                text.gameObject.SetActive(true);
                size = new Vector2(fontSize * 2.4f, fontSize * 1.3f);
                margin = 0f;
            }
            else
            {
                if (made.Text != null)
                {
                    made.Text.gameObject.SetActive(false);
                }
                ArrowKind kind = StandArrows.KindOf(fit, look.BothShape);
                float perUnit = look.Size / StandArrows.Length * scale;
                made.Picture.sprite = StandArrows.Of(fit, look.BothShape, look.Outline);
                made.Picture.color = look.Colour;
                made.Picture.enabled = true;
                size = StandArrows.UnitSize(kind) * perUnit;
                // The picture's clear margin, less its outline, sits outside the inset: the arrow's outline starts there.
                margin = (StandArrows.Margin - (look.Outline ? StandArrows.Rim : 0f)) * perUnit;
            }
            Place(made.Rect, look.PlaceOf(fit), look.NudgeOf(fit), size, inset - margin, scale);
            made.Dressed = fit;
            made.Look = look;
            made.Icon = icon;
            made.AsText = asText;
        }

        /// <summary>Anchors a mark at its place on the host, <paramref name="inset"/> in from the edges it touches, then
        /// nudged by its offset (right and up, in pixels on a full-size icon).</summary>
        internal static void Place(RectTransform rect, MarkPlace place, Vector2 nudge, Vector2 size, float inset, float scale)
        {
            Vector2 corner;
            Vector2 away;
            switch (place)
            {
                case MarkPlace.TopLeft:
                    corner = new Vector2(0f, 1f);
                    away = new Vector2(inset, -inset);
                    break;
                case MarkPlace.TopRight:
                    corner = new Vector2(1f, 1f);
                    away = new Vector2(-inset, -inset);
                    break;
                case MarkPlace.BottomLeft:
                    corner = new Vector2(0f, 0f);
                    away = new Vector2(inset, inset);
                    break;
                case MarkPlace.BottomRight:
                    corner = new Vector2(1f, 0f);
                    away = new Vector2(-inset, inset);
                    break;
                case MarkPlace.RightMiddle:
                    corner = new Vector2(1f, 0.5f);
                    away = new Vector2(-inset, 0f);
                    break;
                default:
                    corner = new Vector2(0f, 0.5f);
                    away = new Vector2(inset, 0f);
                    break;
            }
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = away + nudge * scale;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static TextAlignmentOptions AlignmentOf(MarkPlace place)
        {
            switch (place)
            {
                case MarkPlace.TopLeft:
                    return TextAlignmentOptions.TopLeft;
                case MarkPlace.TopRight:
                    return TextAlignmentOptions.TopRight;
                case MarkPlace.BottomLeft:
                    return TextAlignmentOptions.BottomLeft;
                case MarkPlace.BottomRight:
                    return TextAlignmentOptions.BottomRight;
                case MarkPlace.RightMiddle:
                    return TextAlignmentOptions.Right;
                default:
                    return TextAlignmentOptions.Left;
            }
        }

        // The text of a text look, made the first time one is shown: its font set before it wakes (TextMeshPro would
        // otherwise look for its own default font, which the game doesn't ship, and warn in the log).
        private static TextMeshProUGUI TextOf(Made made)
        {
            if (made.Text != null)
            {
                return made.Text;
            }
            var go = new GameObject(made.Name + "_Text", typeof(RectTransform));
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.SetParent(made.Root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            if (fontMaterial != null)
            {
                text.fontSharedMaterial = fontMaterial;
            }
            text.raycastTarget = false;
            text.richText = false;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            made.Text = text;
            return text;
        }

        /// <summary>The game's inventory level number's font (Averia Sans, outlined), looked for until the game's GUI
        /// exists; until then a text look is drawn as arrows.</summary>
        private static bool FindFont()
        {
            if (font != null)
            {
                return true;
            }
            GameObject prefab = InventoryGui.instance != null && InventoryGui.instance.m_playerGrid != null
                ? InventoryGui.instance.m_playerGrid.m_elementPrefab
                : null;
            InventoryElement element = prefab != null ? prefab.GetComponent<InventoryElement>() : null;
            TMP_Text number = element != null ? element.m_quality : null;
            if (number == null || number.font == null)
            {
                return false;
            }
            font = number.font;
            fontMaterial = number.fontSharedMaterial;
            return true;
        }
    }
}
