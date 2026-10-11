using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UseCase
{
    /// <summary>
    /// The panel beside the game's tooltip (the card's decision 1). It is a child of the tooltip the game shows, so it has
    /// the game's GUI scale, shows only once the tooltip does, and goes when the tooltip goes, with nothing left to clean
    /// up. It is placed each frame, right after the game has moved the tooltip to the pointer and kept it on screen
    /// (UITooltip.LateUpdate's postfix): right of the tooltip's frame, or left when there's no room, then kept on screen.
    /// It never catches the pointer. Its words are made once per item and kept until something they depend on changes;
    /// its objects are made once per tooltip shown.
    /// </summary>
    internal static class Panel
    {
        internal const string ObjectName = "UseCase_Panel";

        private const float Gap = 4f;           // between the tooltip's frame and the panel, in the canvas's units
        private const float Padding = 10f;      // inside the panel
        private const float ColumnGap = 18f;
        private const float TitleGap = 4f;
        private const float ScreenMargin = 4f;  // pixels kept free at the screen's edge
        private static readonly Color PlainBackground = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color HeadingFallback = new Color(1f, 0.718f, 0.36f);   // the item tooltip's topic, #FFB75C

        private static GameObject shown;
        private static GameObject shownOn;
        private static string shownFor;
        private static Where shownWhere;
        private static Where pinnedWhere;
        private static int shownStamp;
        private static GameObject pinned;
        private static GameObject pinnedFrom;
        private static string pinnedFor;

        private static readonly Dictionary<string, PanelText> words = new Dictionary<string, PanelText>(StringComparer.Ordinal);
        private static int wordsStamp;
        private static View view;
        private static int viewVersion = -1;
        private static Loose neverShow = new Loose();
        private static int knowledgeStamp;
        private static float knowledgeAt = -10f;

        /// <summary>For the key choice "toggled by a key": starts on, so choosing it doesn't hide the panel until pressed.</summary>
        internal static bool ToggledOn = true;

        /// <summary>The item the panel under the pointer is for, or null.</summary>
        internal static string ShownFor => shown != null ? shownFor : null;

        /// <summary>
        /// Called once a frame for the tooltip the game shows now, after the game placed it: puts the panel beside it, or
        /// takes it away when this tooltip shouldn't have one.
        /// </summary>
        internal static void Follow(UITooltip owner)
        {
            GameObject tooltip = GameHooks.Tooltip();
            if (tooltip == null)
            {
                return;
            }
            PanelText text = Wanted(owner, out string resource, out Where where, out int stamp);
            if (text == null)
            {
                DropFrom(tooltip);
                return;
            }
            if (!tooltip.activeInHierarchy)
            {
                // The game's half-second delay: the panel is made the frame the tooltip shows (the game shows it at the top
                // of this same LateUpdate), so its texts are measured awake. Made under a hidden parent, TextMeshPro would
                // measure everything at a tenth of its size (review 1).
                return;
            }
            if (tooltip == pinnedFrom && resource == pinnedFor && pinned != null)
            {
                DropFrom(tooltip);   // just pinned: the pinned copy stands where this one would
                return;
            }
            if (shown == null || shownOn != tooltip || shownFor != resource || shownStamp != stamp)
            {
                DropFrom(tooltip);
                shown = Make(tooltip, text);
                shownOn = tooltip;
                shownFor = resource;
                shownWhere = where;
                shownStamp = stamp;
            }
            if (shown != null)
            {
                Place(shown.transform as RectTransform, tooltip);
            }
        }

        /// <summary>Takes away every panel, pinned or not (switched off, or the module's undo).</summary>
        internal static void HideAll()
        {
            Destroy(ref shown);
            Destroy(ref pinned);
            shownOn = null;
            shownFor = null;
        }

        /// <summary>The words are made again from scratch (a new world's index).</summary>
        internal static void Forget()
        {
            words.Clear();
            HideAll();
        }

        /// <summary>The pin key: the panel showing now stays where it is; pressed with none showing, the pinned one goes.</summary>
        internal static void Pin()
        {
            if (shown != null && shownOn != null)
            {
                Destroy(ref pinned);
                pinned = shown;
                pinnedFrom = shownOn;
                pinnedFor = shownFor;
                pinnedWhere = shownWhere;
                Transform canvas = shownOn.transform.parent;
                pinned.transform.SetParent(canvas, true);
                pinned.transform.SetAsLastSibling();
                pinned.name = ObjectName + "_Pinned";
                shown = null;
                shownOn = null;
                shownFor = null;
                return;
            }
            Destroy(ref pinned);
        }

        /// <summary>A pinned panel goes once the window it was pinned in has closed: the inventory or the trader, or for
        /// the build panel's costs, once the build menu is shut and no tool is placing (review 2: a hammer in hand kept a
        /// chest's pin up).</summary>
        internal static void Tidy()
        {
            if (pinned == null)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            bool open;
            switch (pinnedWhere)
            {
                case Where.BuildCost:
                    open = Hud.IsPieceSelectionVisible() || (player != null && player.InPlaceMode());
                    break;
                case Where.Trader:
                    open = StoreGui.IsVisible();
                    break;
                default:
                    open = InventoryGui.IsVisible();   // the inventory, chests and the crafting panel
                    break;
            }
            if (!open)
            {
                Destroy(ref pinned);
            }
        }

        // ---- What to show

        private static PanelText Wanted(UITooltip owner, out string resource, out Where where, out int stamp)
        {
            stamp = 0;
            where = Where.Inventory;
            if (!Module.IsOn || !Settings.ShowPanel.Value || !Sources.TryGet(owner, out resource, out where, out bool fresh) || !On(where))
            {
                resource = null;
                return null;
            }
            // The note must still be this tooltip's: the game reuses a slot's tooltip for another item, and the build
            // panel fills the slot after a piece's costs with its station by hand.
            bool still = where == Where.Trader
                || (where == Where.Inventory && (fresh || owner.m_topic == resource))
                || ((where == Where.CraftingCost || where == Where.BuildCost) && owner.m_text == QoLMods.Catalogue.GameFacts.Localize(resource));
            if (!still || !Showing())
            {
                return null;
            }
            UseIndex index = Index.Current;
            Knowledge know = Knowledge.ThisFrame();
            if (index == null || know == null)
            {
                return null;
            }
            if (viewVersion != Settings.Version)
            {
                viewVersion = Settings.Version;
                view = Settings.ReadView(QoLMods.Catalogue.GameFacts.Localize);
                neverShow = Loose.From(Settings.NeverShowFor.Value);
            }
            if (!neverShow.IsEmpty && neverShow.HasAny(resource, QoLMods.Catalogue.GameFacts.Localize(resource), CanonicalPrefab(resource)))
            {
                return null;
            }
            if (Time.unscaledTime - knowledgeAt > 0.25f)
            {
                knowledgeAt = Time.unscaledTime;
                knowledgeStamp = know.Stamp((int)Time.unscaledTime);
            }
            bool unfolded = view.Long == LongLists.FoldedUntilKeyHeld && !Module.Typing() && Settings.Held(Settings.Key.Value);
            unchecked
            {
                stamp = Settings.Version;
                stamp = stamp * 31 + index.GetHashCode();
                stamp = stamp * 31 + index.Version;
                stamp = stamp * 31 + knowledgeStamp;
                stamp = stamp * 31 + (unfolded ? 1 : 0);
                stamp = stamp * 31 + (Localization.instance != null ? Localization.instance.GetSelectedLanguage().GetHashCode() : 0);
            }
            if (wordsStamp != stamp)
            {
                words.Clear();
                wordsStamp = stamp;
            }
            if (!words.TryGetValue(resource, out PanelText text))
            {
                int value = QoLMods.Catalogue.GameFacts.FactsOf(resource)?.Value ?? 0;
                List<Use> uses = index.For(resource, view.KindOn[(int)UseKind.Selling], view.AnythingRules, value);
                text = Lines.Build(uses, view, know, unfolded);
                words[resource] = text;   // null too: nothing to list, remembered as such
            }
            return text;
        }

        private static bool On(Where where)
        {
            switch (where)
            {
                case Where.Inventory: return Settings.InInventory.Value;
                case Where.Trader: return Settings.InTrader.Value;
                case Where.CraftingCost: return Settings.InCraftingCosts.Value;
                default: return Settings.InBuildCosts.Value;
            }
        }

        // The key choices; with no key set they show the panel on hover, as their description says.
        private static bool Showing()
        {
            BepInEx.Configuration.KeyboardShortcut key = Settings.Key.Value;
            bool hasKey = key.MainKey != KeyCode.None;
            switch (Settings.When.Value)
            {
                case ShowWhen.WhileKeyHeld:
                    return !hasKey || (!Module.Typing() && Settings.Held(key));
                case ShowWhen.ToggledByKey:
                    return !hasKey || ToggledOn;
                default:
                    return true;
            }
        }

        private static string CanonicalPrefab(string resource)
        {
            GameObject prefab = QoLMods.Catalogue.GameFacts.Canonical(resource);
            return prefab != null ? prefab.name : null;
        }

        // ---- Making it

        private static GameObject Make(GameObject tooltip, PanelText text)
        {
            RectTransform frame = tooltip.transform.childCount > 0 ? tooltip.transform.GetChild(0) as RectTransform : null;
            if (frame == null)
            {
                return null;
            }
            // The item tooltip's own look: its frame and its topic's words. The small tooltip of the cost icons has
            // neither (one line of outlined text), so the panel borrows them from the item tooltip's prefab.
            GameObject itemTooltip = ItemTooltipPrefab();
            Image look = frame.GetComponent<Image>();
            if ((look == null || look.sprite == null) && itemTooltip != null && itemTooltip.transform.childCount > 0)
            {
                look = itemTooltip.transform.GetChild(0).GetComponent<Image>();
            }
            TMP_Text bodyStyle = FindText(tooltip.transform, "Text") ?? (itemTooltip != null ? FindText(itemTooltip.transform, "Text") : null);
            if (bodyStyle == null || bodyStyle.font == null)
            {
                return null;   // no font to borrow: the game ships none of TextMeshPro's own, and a text without one throws
            }
            // A tooltip sizes itself at the end of the frame it first shows (the small one's row, the item one's frame): done
            // now, and kept on screen as the game keeps it, so the room around it is measured by its real size (review 2
            // and the last look).
            LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)tooltip.transform);
            Utils.ClampUIToScreen(frame);
            TMP_Text topicStyle = FindText(tooltip.transform, "Topic") ?? (itemTooltip != null ? FindText(itemTooltip.transform, "Topic") : null);
            float size = Settings.TextSize.Value > 0 ? Settings.TextSize.Value : SizeOf(bodyStyle, 16f);
            float titleSize = Settings.TextSize.Value > 0 ? Settings.TextSize.Value : SizeOf(topicStyle, size);
            Color headingColour = topicStyle != null ? topicStyle.color : HeadingFallback;

            var root = new GameObject(ObjectName, typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(tooltip.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            // The cost icons' small tooltip lays out its children in a row and sizes itself to them: the panel is left out
            // of that (review 1), so neither it nor the game's own line is moved or squeezed.
            root.AddComponent<LayoutElement>().ignoreLayout = true;
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;   // never takes the pointer from the slot, which would hide the game's tooltip
            group.interactable = false;
            Image background = root.AddComponent<Image>();
            background.raycastTarget = false;
            if (look != null && look.sprite != null)
            {
                background.sprite = look.sprite;
                background.type = look.type;
                background.pixelsPerUnitMultiplier = look.pixelsPerUnitMultiplier;
                background.material = look.material;
                background.color = Faded(look.color);
            }
            else
            {
                background.color = Faded(PlainBackground);
            }

            TMP_Text title = null;
            if (text.Title.Length > 0)
            {
                title = NewText(root.transform, "Title", topicStyle, titleSize);
                title.text = "<noparse>" + text.Title + "</noparse>";
            }
            var columnTexts = new List<TMP_Text>();
            root.SetActive(true);   // measured awake, laid out in the same frame, before anything is drawn
            if (title != null)
            {
                title.alignment = TextAlignmentOptions.TopLeft;
                title.gameObject.SetActive(true);
            }

            // Rows per column: as many as fit on the screen at this GUI scale, or the setting's number.
            float scale = Mathf.Max(0.01f, frame.lossyScale.y);
            TMP_Text probe = NewText(root.transform, "Column0", bodyStyle, size);
            probe.gameObject.SetActive(true);
            float lineHeight = Mathf.Max(1f, probe.GetPreferredValues("Ag\nAg").y - probe.GetPreferredValues("Ag").y);
            float titleHeight = title != null ? title.GetPreferredValues(title.text).y + TitleGap : 0f;
            float high = Screen.height - 2f * ScreenMargin;
            if (Settings.Side.Value == PanelSide.Below)
            {
                frame.GetWorldCorners(Corners);
                high = Mathf.Max(0f, Corners[0].y - Gap * scale - ScreenMargin);   // under the tooltip, never over it
            }
            float room = high / scale - 2f * Padding - titleHeight;
            int fit = Mathf.Max(2, Mathf.FloorToInt(room / lineHeight));
            int most = Settings.MostLinesPerColumn.Value > 0 ? Mathf.Min(fit, Settings.MostLinesPerColumn.Value) : fit;
            float across = RoomAcross(frame, scale);
            List<List<Row>> columns = Columns.Split(text.Rows, most);
            Show(columns, columnTexts, probe, root.transform, bodyStyle, size, headingColour);
            // Fewer columns when they're wider than the room beside the tooltip: the ones kept filled to the full height,
            // the rest said as "and N more".
            for (int keep = Fits(columnTexts, across); keep < columns.Count && keep >= 1; keep = Fits(columnTexts, across))
            {
                columns = Columns.Take(text.Rows, keep, most);
                Show(columns, columnTexts, probe, root.transform, bodyStyle, size, headingColour);
                if (keep == 1)
                {
                    break;
                }
            }

            // Laid out by hand: the title across the top, the columns side by side under it.
            float x = Padding;
            float top = -Padding;
            float width = 0f;
            if (title != null)
            {
                Vector2 titleSizePx = title.GetPreferredValues(title.text);
                Put(title.rectTransform, new Vector2(Padding, top), titleSizePx);
                top -= titleHeight;
                width = titleSizePx.x;
            }
            float tallest = 0f;
            float columnsWidth = 0f;
            foreach (TMP_Text column in columnTexts)
            {
                Vector2 preferred = column.GetPreferredValues(column.text);
                Put(column.rectTransform, new Vector2(x, top), preferred);
                x += preferred.x + ColumnGap;
                columnsWidth += preferred.x + (columnsWidth > 0f ? ColumnGap : 0f);
                tallest = Mathf.Max(tallest, preferred.y);
            }
            width = Mathf.Max(width, columnsWidth);
            rect.sizeDelta = new Vector2(width + 2f * Padding, -top + tallest + Padding);
            return root;
        }

        // One text per column, the first the probe; texts no longer needed go.
        private static void Show(List<List<Row>> columns, List<TMP_Text> texts, TMP_Text probe, Transform root, TMP_Text style, float size, Color headingColour)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (i >= texts.Count)
                {
                    TMP_Text column = i == 0 ? probe : NewText(root, "Column" + i, style, size);
                    column.gameObject.SetActive(true);
                    texts.Add(column);
                }
                texts[i].text = Join(columns[i], headingColour);
            }
            for (int i = texts.Count - 1; i >= columns.Count; i--)
            {
                UnityEngine.Object.Destroy(texts[i].gameObject);
                texts.RemoveAt(i);
            }
        }

        // How many of the columns, from the first, fit across the room (at least one).
        private static int Fits(List<TMP_Text> texts, float room)
        {
            float used = 0f;
            for (int i = 0; i < texts.Count; i++)
            {
                float width = texts[i].GetPreferredValues(texts[i].text).x + (i > 0 ? ColumnGap : 0f);
                if (i > 0 && used + width > room)
                {
                    return i;
                }
                used += width;
            }
            return texts.Count;
        }

        // The room across for the panel's columns, in the canvas's units: on the side it will take, so it never lies over
        // the game's own tooltip (review 2): beside, the wider side; always right or left, that side; below, the screen.
        private static float RoomAcross(RectTransform frame, float scale)
        {
            frame.GetWorldCorners(Corners);
            float left = Corners[0].x - Gap * scale - ScreenMargin;
            float right = Screen.width - ScreenMargin - Corners[2].x - Gap * scale;
            float room;
            switch (Settings.Side.Value)
            {
                case PanelSide.AlwaysRight:
                    room = right;
                    break;
                case PanelSide.AlwaysLeft:
                    room = left;
                    break;
                case PanelSide.Below:
                    room = Screen.width - 2f * ScreenMargin;
                    break;
                default:
                    room = Mathf.Max(left, right);
                    break;
            }
            return Mathf.Max(0f, room) / scale - 2f * Padding;
        }

        private static string Join(List<Row> rows, Color headingColour)
        {
            string colour = ColorUtility.ToHtmlStringRGB(headingColour);
            var joined = new StringBuilder();
            foreach (Row row in rows)
            {
                if (joined.Length > 0)
                {
                    joined.Append('\n');
                }
                // Names are shown as they are, never read as TextMeshPro's tags (another mod's item could carry a "<").
                if (row.Heading)
                {
                    joined.Append("<b><color=#").Append(colour).Append("><noparse>").Append(row.Text).Append("</noparse></color></b>");
                }
                else
                {
                    joined.Append("<noparse>").Append(row.Text).Append("</noparse>");
                }
            }
            return joined.ToString();
        }

        // A text made the way the game's own are: asleep, its font set before it wakes (TextMeshPro would otherwise warn
        // that its own default font is missing: LESSONS_LEARNED), copied from the tooltip's own text.
        private static TMP_Text NewText(Transform parent, string name, TMP_Text style, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (style != null)
            {
                text.font = style.font;
                text.fontSharedMaterial = style.fontSharedMaterial;
                text.color = style.color;
                text.fontStyle = style.fontStyle;
                text.lineSpacing = style.lineSpacing;
            }
            text.fontSize = size;
            text.isOrthographic = true;   // what a text of the game's own canvas has; one made from code starts without it
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.margin = Vector4.zero;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return text;   // still asleep: the caller shows it

        }

        private static void Put(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 1f, Mathf.Ceil(size.y) + 1f);
        }

        // The size a text shows at: its largest when the game sizes it to fit (the item tooltip's 18 to 19).
        private static float SizeOf(TMP_Text text, float otherwise) =>
            text == null ? otherwise : text.enableAutoSizing ? Mathf.Max(text.fontSizeMax, text.fontSize) : text.fontSize;

        private static GameObject itemTooltipPrefab;

        // The item tooltip's prefab (InventoryTooltip), from the inventory's slot prefab; null before the inventory exists.
        private static GameObject ItemTooltipPrefab()
        {
            if (itemTooltipPrefab == null)
            {
                InventoryGui gui = InventoryGui.instance;
                GameObject slot = gui != null && gui.m_playerGrid != null ? gui.m_playerGrid.m_elementPrefab : null;
                InventoryElement element = slot != null ? slot.GetComponent<InventoryElement>() : null;
                itemTooltipPrefab = element != null && element.m_tooltip != null ? element.m_tooltip.m_tooltipPrefab : null;
            }
            return itemTooltipPrefab;
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            Transform found = Utils.FindChild(root, name);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private static Color Faded(Color colour)
        {
            colour.a *= Mathf.Clamp01(Settings.Opacity.Value);
            return colour;
        }

        // ---- Placing it

        /// <summary>
        /// Beside the tooltip's frame (its first child, which the game keeps on screen), in screen pixels: the in-game
        /// canvases draw over the screen, so a UI object's world position is its pixel position (Utils.ClampUIToScreen
        /// works the same way).
        /// </summary>
        private static readonly Vector3[] Corners = new Vector3[4];

        private static void Place(RectTransform panel, GameObject tooltip)
        {
            if (panel == null || tooltip.transform.childCount == 0 || !(tooltip.transform.GetChild(0) is RectTransform frame))
            {
                return;
            }
            Vector3[] corners = Corners;
            frame.GetWorldCorners(corners);
            float left = corners[0].x;
            float bottom = corners[0].y;
            float right = corners[2].x;
            float top = corners[2].y;
            float scale = panel.lossyScale.x;
            float width = panel.rect.width * scale;
            float height = panel.rect.height * scale;
            float gap = Gap * scale;
            float x;
            float y = top;
            switch (Settings.Side.Value)
            {
                case PanelSide.AlwaysRight:
                    x = right + gap;
                    break;
                case PanelSide.AlwaysLeft:
                    x = left - gap - width;
                    break;
                case PanelSide.Below:
                    x = left;
                    y = bottom - gap;
                    break;
                default:
                    bool fitsRight = right + gap + width <= Screen.width - ScreenMargin;
                    bool fitsLeft = left - gap - width >= ScreenMargin;
                    x = fitsRight || !fitsLeft && Screen.width - right >= left ? right + gap : left - gap - width;
                    break;
            }
            x = Mathf.Clamp(x, ScreenMargin, Mathf.Max(ScreenMargin, Screen.width - ScreenMargin - width));
            y = Mathf.Clamp(y, Mathf.Min(Screen.height - ScreenMargin, ScreenMargin + height), Screen.height - ScreenMargin);
            panel.position = new Vector3(x, y, frame.position.z);
        }

        private static void DropFrom(GameObject tooltip)
        {
            if (shown != null && (shownOn == tooltip || shownOn == null))
            {
                Destroy(ref shown);
                shownOn = null;
                shownFor = null;
            }
        }

        private static void Destroy(ref GameObject panel)
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
            }
            panel = null;
        }
    }
}
