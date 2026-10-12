// What the QoL Pack does where its own game serves the world, a part: the one copy to edit is
// Games\Valheim\QoLMods\parts\Serving\. Every mod that uses it carries a copy in its own parts\Serving\ folder, kept
// identical by QoLMods\tools\sync-kit.ps1, so each mod still builds and publishes on its own. The rules for a part are in
// QoLMods\MODULES.md. A mod that carries it carries the chest service too (parts\Chests\), whose ServerPack is the other
// side of the handshake.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Chests;
using UnityEngine;

namespace QoLMods.Serving
{
    /// <summary>Where this game is, for a module with a server half (card B29): who decides what it does here.</summary>
    internal enum Place
    {
        /// <summary>Not in a world: the main menu.</summary>
        NoWorld,

        /// <summary>This game serves the world (alone, or the game's Start Server), and the QoL Pack serves it: no plugin
        /// of the server pack's kind is here, and this copy answers friends' games.</summary>
        Own,

        /// <summary>This game serves the world, no plugin of the server pack's kind is here, but this copy doesn't answer
        /// friends' games: another of the pack's mods installed on its own registered first.</summary>
        OwnUnheard,

        /// <summary>This game serves the world, and a plugin of the server pack's kind is here: it decides.</summary>
        OwnServerHalf,

        /// <summary>Joined a server, whose answer hasn't come yet and still may.</summary>
        Asking,

        /// <summary>Joined a server whose answer lists the module's server half, on.</summary>
        ServerRuns,

        /// <summary>Joined a server whose answer doesn't list it, or lists it off.</summary>
        ServerWithout,

        /// <summary>Joined a server that hasn't answered: no server pack there (or its answer stopped).</summary>
        NoAnswer,
    }

    /// <summary>
    /// The line in the settings window (F1) under a module's Enabled switch that says what applies right now (card B29, his
    /// pick of the answer's "Possible fix", 2026-10-12): this server runs it, this server doesn't, or this is a world your
    /// own game runs and who decides there. The module gives the words for each Place; the part decides the Place and draws
    /// the switch as shudnal's ConfigurationManager draws one (its own colours, words and style, found by reflection; Unity's
    /// own toggle without it), with the line under it.
    /// </summary>
    internal static class Where
    {
        /// <summary>How long after this game's first hello on a server its answer may still come, in seconds: the first
        /// hello goes at once, and is said again every ten.</summary>
        internal const float AnswerMayCome = 10f;

        /// <summary>The Place now, for the server half with this id ("NapTimeServer").</summary>
        internal static Place Now(string serverModuleId)
        {
            ZNet net = ZNet.instance;
            if (net == null)
            {
                return Place.NoWorld;
            }
            if (net.IsServer())
            {
                // A dedicated server has no settings window to show this in.
                return ServerHalves.OwnWorld ? Decide(true, true, ServerHalves.KitHere, HostPack.AnswersHere, false, false, float.NaN) : Place.NoWorld;
            }
            float first = ServerPack.FirstSaidAt;
            float since = float.IsNegativeInfinity(first) ? float.NaN : Time.realtimeSinceStartup - first;
            return Decide(true, false, false, false, ServerPack.Answered, ServerPack.Runs(serverModuleId), since);
        }

        /// <summary>
        /// The decision on plain values (for the scratch harness): <paramref name="inWorld"/>, whether this game
        /// <paramref name="serves"/> the world, a plugin of the server pack's kind here (<paramref name="kitHere"/>), this
        /// copy answering friends (<paramref name="answersHere"/>), a server's answer standing (<paramref name="answered"/>)
        /// and running the module on (<paramref name="runs"/>), and the seconds since this game's first hello on this
        /// server (<paramref name="sinceFirstHello"/>; NaN before any).
        /// </summary>
        internal static Place Decide(bool inWorld, bool serves, bool kitHere, bool answersHere, bool answered, bool runs, float sinceFirstHello)
        {
            if (!inWorld)
            {
                return Place.NoWorld;
            }
            if (serves)
            {
                return kitHere ? Place.OwnServerHalf : answersHere ? Place.Own : Place.OwnUnheard;
            }
            if (answered)
            {
                return runs ? Place.ServerRuns : Place.ServerWithout;
            }
            return float.IsNaN(sinceFirstHello) || sinceFirstHello < AnswerMayCome ? Place.Asking : Place.NoAnswer;
        }

        // ---- The switch and its line in the settings window ----

        private const string ManagerGuid = "_shudnal.ConfigurationManager";

        private static readonly Dictionary<ConfigEntryBase, string> Lines = new Dictionary<ConfigEntryBase, string>();
        private static readonly HashSet<ConfigEntryBase> Broken = new HashSet<ConfigEntryBase>();
        private static readonly HashSet<ConfigEntryBase> Plain = new HashSet<ConfigEntryBase>();

        /// <summary>The most entries the line is kept for: shudnal's edit window hands the drawer a stand-in entry each
        /// time it opens.</summary>
        private const int MostKept = 64;

        private static int clearedFrame = -1;
        private static GUIStyle note;
        private static GUISkin noteFor;
        private static bool lookedForManager;
        private static Func<bool, GUIStyle> managerToggle;
        private static ConfigEntry<string> enabledText;
        private static ConfigEntry<string> disabledText;
        private static ConfigEntry<Color> widgetColour;
        private static ConfigEntry<Color> enabledColour;

        /// <summary>
        /// Draws a module's Enabled switch (a bool <paramref name="entry"/>) with <paramref name="line"/>'s words under it,
        /// worked out once a frame (in the layout pass, so each pass draws the same). Hand it to the switch's hints as its
        /// CustomDrawer. A failure is said once through <paramref name="warn"/>, and that switch is drawn plainly from then.
        /// </summary>
        internal static void DrawSwitch(ConfigEntryBase entry, Func<string> line, Action<string> warn)
        {
            if (entry == null)
            {
                return;
            }
            // Drawn plainly from the next layout pass after a failure, never in the middle of one frame's passes: each pass
            // of a frame must draw the controls its layout pass measured.
            bool layout = Event.current.type == EventType.Layout;
            if (layout)
            {
                if (Broken.Contains(entry))
                {
                    Plain.Add(entry);
                }
                if (Lines.Count > MostKept && Time.frameCount != clearedFrame)
                {
                    // Only at a frame's first layout pass, so no entry drawn earlier in this frame loses its line.
                    clearedFrame = Time.frameCount;
                    Lines.Clear();
                }
            }
            if (Plain.Contains(entry))
            {
                PlainSwitch(entry);
                return;
            }
            bool opened = false;
            try
            {
                if (layout || !Lines.ContainsKey(entry))
                {
                    string text;
                    try
                    {
                        text = line?.Invoke() ?? string.Empty;
                    }
                    catch (Exception error)
                    {
                        text = string.Empty;
                        Broken.Add(entry);
                        warn?.Invoke($"The line under \"{entry.Definition.Key}\" in the settings window failed ({error.GetType().Name}: " +
                                     $"{error.Message}); the switch shows without it from now on, and works as before.");
                    }
                    Lines[entry] = text;
                }
                Lines.TryGetValue(entry, out string words);
                GUILayout.BeginVertical();
                opened = true;
                bool on = entry.BoxedValue is bool value && value;
                bool next = Switch(entry, on);
                if (!string.IsNullOrEmpty(words))
                {
                    Color colour = GUI.contentColor;
                    GUI.contentColor = new Color(colour.r, colour.g, colour.b, colour.a * 0.75f);
                    GUILayout.Label(words, Note(), GUILayout.MinWidth(40f), GUILayout.ExpandWidth(true));
                    GUI.contentColor = colour;
                }
                GUILayout.EndVertical();
                opened = false;
                if (next != on)
                {
                    entry.BoxedValue = next;   // once everything is drawn, so each pass draws what it measured
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (opened)
                {
                    try
                    {
                        GUILayout.EndVertical();
                    }
                    catch (Exception)
                    {
                        // Closing what the error left open is all that's wanted.
                    }
                }
                Broken.Add(entry);
                warn?.Invoke($"The switch \"{entry.Definition.Key}\" in the settings window failed to draw ({error.GetType().Name}: " +
                             $"{error.Message}); it shows plainly from now on, and works as before.");
            }
        }

        // The switch alone, as the settings window draws a bool.
        private static void PlainSwitch(ConfigEntryBase entry)
        {
            bool on = entry.BoxedValue is bool value && value;
            bool next = GUILayout.Toggle(on, on ? "Enabled" : "Disabled", GUILayout.ExpandWidth(true));
            if (next != on)
            {
                entry.BoxedValue = next;
            }
        }

        // The switch as shudnal's ConfigurationManager draws a bool (its DrawBoolField: its widget colour, its enabled
        // colour while on, its words, and its toggle style for a value at or off its default); Unity's own toggle without it.
        private static bool Switch(ConfigEntryBase entry, bool on)
        {
            LookForManager();
            bool atDefault = entry.DefaultValue is bool initial && initial == on;
            GUIStyle style = null;
            try
            {
                style = managerToggle?.Invoke(atDefault);
            }
            catch (Exception)
            {
                managerToggle = null;
            }
            string words = on ? enabledText?.Value ?? "Enabled" : disabledText?.Value ?? "Disabled";
            // The window tints a custom drawer's text with its font colour, which its toggle style already carries: drawn
            // untinted, as the window draws its own switches, so the words aren't darker than theirs (review, minor 1).
            Color tint = GUI.contentColor;
            if (style != null)
            {
                GUI.contentColor = Color.white;
            }
            Color background = GUI.backgroundColor;
            if (widgetColour != null)
            {
                GUI.backgroundColor = widgetColour.Value;
            }
            Color before = GUI.backgroundColor;
            if (on && enabledColour != null)
            {
                GUI.backgroundColor = enabledColour.Value;
            }
            bool next = style != null
                ? GUILayout.Toggle(on, words, style, GUILayout.ExpandWidth(true))
                : GUILayout.Toggle(on, words, GUILayout.ExpandWidth(true));
            GUI.backgroundColor = widgetColour != null ? before : background;
            GUI.contentColor = tint;
            return next;
        }

        private static void LookForManager()
        {
            if (lookedForManager)
            {
                return;
            }
            lookedForManager = true;
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(ManagerGuid, out PluginInfo info) || info?.Instance == null)
                {
                    return;
                }
                Assembly manager = info.Instance.GetType().Assembly;
                Type styles = manager.GetType("ConfigurationManager.ConfigurationManagerStyles", false);
                MethodInfo toggle = styles?.GetMethod("GetToggleStyle", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (toggle != null && toggle.ReturnType == typeof(GUIStyle))
                {
                    managerToggle = atDefault => toggle.Invoke(null, new object[] { atDefault }) as GUIStyle;
                }
                Type window = manager.GetType("ConfigurationManager.ConfigurationManager", false);
                enabledText = Field<ConfigEntry<string>>(window, "_enabledText");
                disabledText = Field<ConfigEntry<string>>(window, "_disabledText");
                widgetColour = Field<ConfigEntry<Color>>(window, "_widgetBackgroundColor");
                enabledColour = Field<ConfigEntry<Color>>(window, "_enabledBackgroundColor");
            }
            catch (Exception)
            {
                managerToggle = null;   // Unity's own toggle, and its own words
            }
        }

        private static T Field<T>(Type type, string name) where T : class =>
            type?.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as T;

        // The line's style: the window's label, wrapped and in italics, at the window's own font size (it sets its own
        // before each call).
        private static GUIStyle Note()
        {
            if (note == null || noteFor != GUI.skin)
            {
                noteFor = GUI.skin;
                note = new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Italic };
            }
            note.fontSize = GUI.skin.label.fontSize;
            return note;
        }
    }
}
