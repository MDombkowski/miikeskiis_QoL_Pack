using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// A section's title row in a configuration manager's window (F1), drawn in place of its "Unfolded" setting with the
    /// window's own immediate-mode GUI: "[+] Pickables - Berries", which a click opens, "[-]" when open, and "(changed)"
    /// while any of its settings isn't at its default. Where sections don't fold (Folds.Live false) it's the section's
    /// title alone, over its settings. Plain characters, as the window's own headings use ("&gt; Name &lt;"): the
    /// window's font may lack arrows.
    /// </summary>
    internal static class FoldDrawer
    {
        private static GUIStyle row;
        private static GUIStyle rowFrom;
        private static GUIStyle title;
        private static GUIStyle titleFrom;
        private static bool lookedForColours;
        private static FieldInfo changedColour;
        private static FieldInfo defaultColour;

        /// <summary>The drawer of a section's title row.</summary>
        internal static Action<ConfigEntryBase> For(RuleSection section) => entry => Draw(entry, section);

        private static void Draw(ConfigEntryBase entry, RuleSection section)
        {
            bool changed = section.Changed();
            string mark = changed ? "   (changed)" : string.Empty;
            Color before = GUI.contentColor;
            // shudnal's colours the row by its own value, so an open section would show as changed: by its rules instead.
            if (ValueColour(changed) is Color colour)
            {
                GUI.contentColor = colour;
            }
            try
            {
                if (!Folds.Live)
                {
                    GUILayout.Label(section.Part + mark, TitleStyle(), GUILayout.ExpandWidth(true));
                    return;
                }
                bool open = entry.BoxedValue is bool unfolded && unfolded;
                if (GUILayout.Button((open ? "[-] " : "[+] ") + section.Part + mark, RowStyle(), GUILayout.ExpandWidth(true)))
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
