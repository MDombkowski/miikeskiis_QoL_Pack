using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComfortBuild
{
    /// <summary>
    /// What the mod draws with, borrowed from the game so it looks like the game's own: the inventory's level number (a
    /// gear item's quality: Averia Sans, outlined, bold) for the comfort number, the build menu's list text (Averia
    /// Serif) for its lines, and the favourites dropdown's checkbox and tick (check_yes) for the switch and the mark.
    /// Each is looked for before anything is drawn with it, until found (the inventory's font only exists once the game's
    /// GUI does); what can't be found is drawn plainly instead, and the log says so once.
    /// </summary>
    internal static class Looks
    {
        /// <summary>The build menu's list text colour (BuildUiTagButton.m_baseTextColor in the prefab).</summary>
        internal static readonly Color ListText = new Color(0.7216f, 0.7216f, 0.7216f, 1f);

        /// <summary>How dark the veil over a piece's picture is, for the look "Dimmed".</summary>
        internal const float VeilAlpha = 0.55f;

        private static TMP_FontAsset numberFont;
        private static Material numberMaterial;
        private static TMP_FontAsset listFont;
        private static Material listMaterial;
        private static Sprite tick;
        private static GameObject checkBox;
        private static bool complete;
        private static bool reported;

        /// <summary>RRGGBB, for TextMeshPro's colour tags.</summary>
        internal static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        /// <summary>The game's tick (check_yes), or null.</summary>
        internal static Sprite Tick => tick;

        /// <summary>The favourites dropdown's checkbox (its box, with the tick inside), to copy; or null.</summary>
        internal static GameObject CheckBox => checkBox;

        internal static void Find(BuildUi ui)
        {
            if (complete || ui == null)
            {
                return;
            }
            if (numberFont == null)
            {
                TMP_Text level = Game.LevelNumber();
                if (level != null && level.font != null)
                {
                    numberFont = level.font;
                    numberMaterial = level.fontSharedMaterial;
                }
            }
            if (listFont == null)
            {
                GameObject tagPrefab = Game.TagButtonPrefab(ui);
                TMP_Text listText = tagPrefab != null ? tagPrefab.GetComponentInChildren<TMP_Text>(true) : null;
                if (listText != null && listText.font != null)
                {
                    listFont = listText.font;
                    listMaterial = listText.fontSharedMaterial;
                }
            }
            if (tick == null || checkBox == null)
            {
                BuildUiFavoriteCategoryCheckButton row = Game.CheckRow(ui);
                Image tickImage = Game.TickOf(row);
                if (tickImage != null)
                {
                    tick = tickImage.sprite;
                    checkBox = tickImage.transform.parent != null ? tickImage.transform.parent.gameObject : null;
                }
            }
            complete = numberFont != null && listFont != null && tick != null && checkBox != null;
            if (!complete && !reported && InventoryGui.instance != null)
            {
                reported = true;
                Module.Log.LogInfo("Some of the game's looks weren't found (" +
                                   $"number font {(numberFont != null ? "found" : "not found")}, list font {(listFont != null ? "found" : "not found")}, " +
                                   $"tick {(tick != null ? "found" : "not found")}, checkbox {(checkBox != null ? "found" : "not found")}); " +
                                   "what uses them is drawn plainly.");
            }
        }

        /// <summary>
        /// A text made from code the way the game's own is: its object is made inactive under <paramref name="parent"/>,
        /// its font is set before it wakes (TextMeshPro would otherwise look for its own default font, which the game
        /// doesn't ship, and warn in the log: LESSONS_LEARNED), then it is shown.
        /// </summary>
        internal static TextMeshProUGUI Text(Transform parent, string name, bool number, float size, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = number ? numberFont ?? listFont : listFont ?? numberFont;
            Material material = number ? (numberFont != null ? numberMaterial : listMaterial) : (listFont != null ? listMaterial : numberMaterial);
            if (font != null)
            {
                text.font = font;
                if (material != null)
                {
                    text.fontSharedMaterial = material;
                }
            }
            text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            go.SetActive(true);
            return text;
        }
    }
}
