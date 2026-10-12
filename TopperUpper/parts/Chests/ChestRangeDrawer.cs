// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using BepInEx.Configuration;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// "Use this range in every mod" in a configuration manager's window (F1), drawn with the window's own immediate-mode
    /// GUI across its width (card B13, his ask of 2026-10-11): its name, the switch, a button that sets every mod's own
    /// range to 0 (two clicks: the first asks, the second does it, within a few seconds), then the list of every chest
    /// distance in the pack with what each is set to and what it reaches now (ChestSettings.Line), and below it the ones
    /// measured from somewhere else, which the switch and the button leave alone (0 there takes the range's number,
    /// measured from that place: card B05). Every pass draws the same controls whatever changed
    /// (immediate-mode layout counts them). Plain characters only: the window's font may lack arrows and symbols. After
    /// StoreStash's ChestOrderDrawer.
    /// </summary>
    internal static class ChestRangeDrawer
    {
        internal const string Title = "Use this range in every mod";

        /// <summary>How long the button waits for its second click, in seconds.</summary>
        internal const float ConfirmFor = 5f;

        private static float confirmUntil = -1f;
        private static GUIStyle wrapped;
        private static GUIStyle note;

        /// <summary>Draws the setting, the button and the list, and writes the switch back when it is clicked.</summary>
        internal static void Draw(ConfigEntryBase entry)
        {
            bool on = entry?.BoxedValue is bool value && value;
            float now = Time.realtimeSinceStartup;
            bool confirming = now < confirmUntil;
            int notFollowing = ChestSettings.NotFollowing();
            Color colour = GUI.contentColor;
            bool enabled = GUI.enabled;

            GUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(Title, entry?.Description?.Description), Wrapped(), GUILayout.ExpandWidth(true));
            // A short toggle and a wrapped line under it: the window's toggle style never wraps (review m3).
            bool next = GUILayout.Toggle(on, on ? " On" : " Off", GUILayout.ExpandWidth(false));
            GUI.contentColor = Dim(colour);
            GUILayout.Label(on
                ? "Every mod looks as far as \"Chest range\" above; their own ranges below are kept for when it's off."
                : "A mod uses \"Chest range\" above only where its own range below is 0.", Note());
            GUI.contentColor = colour;
            GUI.enabled = enabled && notFollowing > 0;
            string button = confirming
                ? $"Click again to set {notFollowing} setting{(notFollowing == 1 ? string.Empty : "s")} below to 0"
                : "Make every mod follow \"Chest range\" for good";
            bool clicked = GUILayout.Button(button, GUILayout.ExpandWidth(false));
            GUI.enabled = enabled;
            GUI.contentColor = Dim(colour);
            GUILayout.Label(notFollowing > 0
                ? "The button sets each mod's own range below to 0, which follows \"Chest range\" above."
                : "Every mod's own range below is 0: each follows \"Chest range\" above.", Note());
            GUI.contentColor = colour;

            GUILayout.Label("Every chest distance in the pack, where it is in this window, and what it reaches now:", Wrapped());
            bool any = false;
            foreach (ChestSettings.Distance each in ChestSettings.Distances)
            {
                if (each.How != ChestSettings.Follows.Elsewhere)
                {
                    GUILayout.Label("  " + ChestSettings.Line(each), Note());
                    any = true;
                }
            }
            if (!any)
            {
                GUILayout.Label("  (none)", Note());
            }
            bool elsewhere = false;
            foreach (ChestSettings.Distance each in ChestSettings.Distances)
            {
                if (each.How == ChestSettings.Follows.Elsewhere)
                {
                    if (!elsewhere)
                    {
                        GUILayout.Label("Measured from somewhere else, so the switch and the button leave them alone (0 there " +
                                        "takes \"Chest range\" above, measured from that place):", Wrapped());
                        elsewhere = true;
                    }
                    GUILayout.Label("  " + ChestSettings.Line(each), Note());
                }
            }
            GUILayout.EndVertical();

            // Applied once everything is drawn, so each pass draws what it measured.
            if (next != on && entry != null)
            {
                entry.BoxedValue = next;
            }
            if (clicked)
            {
                if (confirming)
                {
                    confirmUntil = -1f;
                    ChestSettings.FollowEverywhere();
                }
                else
                {
                    confirmUntil = now + ConfirmFor;
                }
            }
        }

        private static GUIStyle Wrapped()
        {
            if (wrapped == null)
            {
                wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            return wrapped;
        }

        private static GUIStyle Note()
        {
            if (note == null)
            {
                note = new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Italic };
            }
            return note;
        }

        private static Color Dim(Color colour) => new Color(colour.r, colour.g, colour.b, colour.a * 0.7f);
    }
}
