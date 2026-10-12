using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// A part's title row in a configuration manager's window (F1), drawn in place of its "Unfolded" setting with the
    /// window's own immediate-mode GUI: "[+] Friendly fire", which a click opens, "[-]" when open, and "(changed)" while
    /// any of its settings isn't at its default. Where parts don't fold (Folds.Live false) it's the part's title alone,
    /// over its settings. Plain characters: the window's font may lack arrows. Use Case's FoldDrawer (Map View Rules',
    /// adapted), unchanged but for its namespace.
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

        /// <summary>The drawer of a part's title row; <paramref name="settings"/> is filled as the part is bound.</summary>
        internal static Action<ConfigEntryBase> For(string part, List<ConfigEntryBase> settings) => entry => Draw(entry, part, settings);

        /// <summary>A title row whose title is worked out each time it's drawn (With the Hoarstone's says whether Hoarstone
        /// is on now: card B26).</summary>
        internal static Action<ConfigEntryBase> For(Func<string> title, List<ConfigEntryBase> settings) => entry => Draw(entry, title(), settings);

        private static void Draw(ConfigEntryBase entry, string part, List<ConfigEntryBase> settings)
        {
            bool changed = Changed(settings);
            string mark = changed ? "   (changed)" : string.Empty;
            Color before = GUI.contentColor;
            // shudnal's colours the row by its own value, so an open part would show as changed: by its settings instead.
            if (ValueColour(changed) is Color colour)
            {
                GUI.contentColor = colour;
            }
            try
            {
                if (!Folds.Live)
                {
                    GUILayout.Label(part + mark, TitleStyle(), GUILayout.ExpandWidth(true));
                    return;
                }
                bool open = entry.BoxedValue is bool unfolded && unfolded;
                if (GUILayout.Button((open ? "[-] " : "[+] ") + part + mark, RowStyle(), GUILayout.ExpandWidth(true)))
                {
                    entry.BoxedValue = !open;
                }
            }
            finally
            {
                GUI.contentColor = before;
            }
        }

        private static bool Changed(List<ConfigEntryBase> settings)
        {
            foreach (ConfigEntryBase setting in settings)
            {
                if (!Equals(setting.BoxedValue, setting.DefaultValue))
                {
                    return true;
                }
            }
            return false;
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
