using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// A feature's or a group's title row in a configuration manager's window (F1), drawn in place of its "…Unfolded"
    /// setting with the window's own immediate-mode GUI: "[+] Chest setups", which a click opens, "[-]" when open, and
    /// "(changed)" while any of its settings (its groups' too) isn't at its default; a group's row indented under its
    /// feature's ("      [+] Copy and paste"). Where rows don't fold (Folds.Live false) it's the title alone, over its
    /// settings. Plain characters, as the window's own headings use: the window's font may lack arrows. After Map View
    /// Rules' FoldDrawer.
    /// </summary>
    internal static class FoldDrawer
    {
        /// <summary>What a group's row starts with, so it sits under its feature's.</summary>
        internal const string GroupIndent = "      ";

        private static GUIStyle row;
        private static GUIStyle rowFrom;
        private static GUIStyle title;
        private static GUIStyle titleFrom;
        private static bool lookedForColours;
        private static FieldInfo changedColour;
        private static FieldInfo defaultColour;

        /// <summary>The drawer of a feature's or a group's title row.</summary>
        internal static Action<ConfigEntryBase> For(Folds.Fold fold) => entry => Draw(entry, fold);

        /// <summary>A row's words: "[+] Copy and paste   (changed)", indented for a group; its title alone where rows don't
        /// fold.</summary>
        internal static string Text(Folds.Fold fold, bool open, bool live, bool changed) =>
            (fold.IsGroup ? GroupIndent : string.Empty) + (live ? (open ? "[-] " : "[+] ") : string.Empty) + fold.Title +
            (changed ? "   (changed)" : string.Empty);

        private static void Draw(ConfigEntryBase entry, Folds.Fold fold)
        {
            bool changed = fold.Changed();
            Color before = GUI.contentColor;
            // shudnal's colours the row by its own value, so an open feature would show as changed: by its settings instead.
            if (ValueColour(changed) is Color colour)
            {
                GUI.contentColor = colour;
            }
            try
            {
                if (!Folds.Live)
                {
                    GUILayout.Label(Text(fold, false, false, changed), TitleStyle(), GUILayout.ExpandWidth(true));
                    return;
                }
                bool open = entry.BoxedValue is bool unfolded && unfolded;
                if (GUILayout.Button(Text(fold, open, true, changed), RowStyle(), GUILayout.ExpandWidth(true)))
                {
                    entry.BoxedValue = !open;
                }
            }
            finally
            {
                GUI.contentColor = before;
            }
        }

        // The colours shudnal's ConfigurationManager gives a value at its default and a changed one (its public
        // _fontColorValueDefault and _fontColorValueChanged); null with another manager, which leaves the colour as it is.
        private static Color? ValueColour(bool changed)
        {
            if (!lookedForColours)
            {
                lookedForColours = true;
                Type manager = ManagerLink.Plugin()?.GetType();
                changedColour = manager?.GetField("_fontColorValueChanged", BindingFlags.Public | BindingFlags.Static);
                defaultColour = manager?.GetField("_fontColorValueDefault", BindingFlags.Public | BindingFlags.Static);
            }
            return (changed ? changedColour : defaultColour)?.GetValue(null) is ConfigEntry<Color> entry ? entry.Value : (Color?)null;
        }

        // Made from the window's own skin, and made again if the window changes its skin.
        private static GUIStyle RowStyle()
        {
            if (row == null || rowFrom != GUI.skin.button)
            {
                rowFrom = GUI.skin.button;
                row = new GUIStyle(rowFrom) { alignment = TextAnchor.MiddleLeft };
            }
            row.fontSize = rowFrom.fontSize;   // the window sets its font size for each call
            return row;
        }

        private static GUIStyle TitleStyle()
        {
            if (title == null || titleFrom != GUI.skin.label)
            {
                titleFrom = GUI.skin.label;
                title = new GUIStyle(titleFrom) { fontStyle = FontStyle.Bold };
            }
            title.fontSize = titleFrom.fontSize;
            return title;
        }
    }
}
