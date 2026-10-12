// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using QoLMods.Catalogue;
using QoLMods.Kit;
using UnityEngine;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace

namespace QoLMods.ListPicker
{
    /// <summary>
    /// What a module calls to give its list settings the picker (card B03): Attach, once per list setting, right after
    /// binding it in BindSettings, with the spec saying what the list holds and how its text is written; Resolve, from its
    /// Resolve, for the game's favourites and lists (the catalogue's GameLists). Attach sets the setting's drawer on the
    /// hints the kit bound it with (ModuleSettings.HintsOf), so a module's folds, which change only a setting's heading and
    /// whether it is listed, work on as before, and a setting folded away shows no button.
    ///
    /// The drawer, in the settings window (F1): the list's typed box as before, wrapped within the window's column as
    /// shudnal's ConfigurationManager draws a text setting (or, with "Keep the typed box beside the button" off, a line
    /// saying how many things it holds), and a Pick button that opens the picker window over F1. Not "Edit": shudnal's
    /// window has an Edit button of its own beside every setting (review 1, m2), which opens its edit window; there it draws
    /// this drawer for a stand-in entry of the same setting (same file and key), which gets the Pick button too, and its
    /// Apply takes what the picker wrote. Where the game's things can't be read yet the button says why ("Pick: in a
    /// world" for creatures at the main menu). With "Pick buttons on list settings" off it is the plain typed box. What it
    /// draws changes only in the window's Layout pass, so both of Unity's passes draw the same controls (LESSONS_LEARNED, a
    /// list drawn in F1); a failure is written once to the module's log and leaves the plain box.
    /// </summary>
    internal static class Lists
    {
        private static readonly Dictionary<ConfigEntryBase, ListSpec> Specs = new Dictionary<ConfigEntryBase, ListSpec>();
        private static readonly Dictionary<string, ListSpec> SpecsByName = new Dictionary<string, ListSpec>();
        private static readonly HashSet<string> Broken = new HashSet<string>();

        // What the drawers draw, decided in a Layout pass (both passes must draw the same controls).
        private static bool buttons = true;
        private static bool typedBox = true;
        private static readonly Dictionary<string, string> NotReady = new Dictionary<string, string>();
        private static GUISkin wrappedFor;
        private static GUIStyle wrapped;

        /// <summary>Gives a list setting its Pick button. Call it in BindSettings, right after binding the setting.</summary>
        internal static void Attach(ConfigEntryBase entry, ListSpec spec)
        {
            if (entry == null || spec == null || entry.SettingType != typeof(string))
            {
                return;
            }
            Hints hints = ModuleSettings.HintsOf(entry);
            if (hints == null)
            {
                return;
            }
            Specs[entry] = spec;
            SpecsByName[NameOf(entry)] = spec;
            hints.CustomDrawer = Draw;
        }

        /// <summary>
        /// From a using module's Resolve: the game's favourites and lists for the picker's list on the left (the catalogue's
        /// GameLists; without them the picker just shows none). A game whose build menu changed so far that even its types
        /// are gone leaves only that note: an optional part of the picker never switches a module off (review 1, m3).
        /// </summary>
        internal static void Resolve(List<string> missingOptional)
        {
            try
            {
                GameLists.Resolve(missingOptional);
            }
            catch (Exception)
            {
                missingOptional.Add("the game's build menu favourites (the list picker shows none)");
            }
        }

        /// <summary>The setting itself was attached (not shudnal's stand-in of it in its edit window).</summary>
        internal static bool IsAttached(ConfigEntryBase entry) => entry != null && Specs.ContainsKey(entry);

        /// <summary>A setting's lasting name: its file and its section and key, the same for shudnal's stand-in entry.</summary>
        internal static string NameOf(ConfigEntryBase entry) =>
            (entry.ConfigFile?.ConfigFilePath ?? string.Empty) + "\n" + entry.Definition.Section + "\n" + entry.Definition.Key;

        /// <summary>The spec a setting was attached with (or shudnal's stand-in of it, by file, section and key), or null.</summary>
        internal static ListSpec SpecOf(ConfigEntryBase entry)
        {
            if (entry == null)
            {
                return null;
            }
            if (Specs.TryGetValue(entry, out ListSpec spec))
            {
                return spec;
            }
            return SpecsByName.TryGetValue(NameOf(entry), out spec) ? spec : null;
        }

        private static void Draw(ConfigEntryBase entry)
        {
            if (entry == null)
            {
                return;
            }
            if (Event.current.type == EventType.Layout)
            {
                buttons = PickerSettings.ButtonsOn;
                typedBox = PickerSettings.TypedBoxOn;
            }
            string name = NameOf(entry);
            ListSpec spec = SpecOf(entry);
            if (spec == null || Broken.Contains(name) || !buttons)
            {
                PlainBox(entry);
                return;
            }
            bool opened = false;
            try
            {
                if (Event.current.type == EventType.Layout)
                {
                    NotReady[name] = Ready(spec);
                }
                NotReady.TryGetValue(name, out string why);
                string text = entry.BoxedValue as string ?? string.Empty;
                GUILayout.BeginHorizontal();
                opened = true;
                if (typedBox)
                {
                    TypedBox(entry, text);
                }
                else
                {
                    GUILayout.Label(Summary(text, spec), GUILayout.MinWidth(40f), GUILayout.ExpandWidth(true));
                }
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && why == null && !PickerWindow.IsOpen;
                var label = new GUIContent(why == null ? "Pick" : "Pick: " + why,
                    why == null ? "Pick from pictures of everything this list can hold." : "The game's things for this list can be read " + why + ".");
                bool clicked = GUILayout.Button(label, GUILayout.ExpandWidth(false));
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
                opened = false;
                if (clicked && why == null)
                {
                    PickerWindow.Open(entry, spec);
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
                        GUILayout.EndHorizontal();
                    }
                    catch (Exception)
                    {
                        // Closing what the error left open is all that's wanted.
                    }
                }
                Broken.Add(name);
                spec.Warn?.Invoke($"The Pick button of \"{entry.Definition.Key}\" in the settings window failed ({error.GetType().Name}: " +
                                  $"{error.Message}); the list shows as its typed box from now on, and works as before.");
            }
        }

        // The typed box alone, as the settings window draws a text setting.
        private static void PlainBox(ConfigEntryBase entry) => TypedBox(entry, entry.BoxedValue as string ?? string.Empty);

        // The typed box, wrapped within the column as shudnal's window draws a text setting: a box that doesn't wrap is as
        // wide as its whole text in Unity's layout (a list of 300 characters pushed the button and the window's own Reset
        // off the row, review 1, M1), so it wraps, and may be narrower than any one word.
        private static void TypedBox(ConfigEntryBase entry, string text)
        {
            if (wrapped == null || wrappedFor != GUI.skin)
            {
                wrappedFor = GUI.skin;
                wrapped = new GUIStyle(GUI.skin.textArea) { wordWrap = true };
            }
            wrapped.fontSize = GUI.skin.textArea.fontSize;   // the window sets its own font size before each call (review 2)
            string typed = GUILayout.TextArea(text, wrapped, GUILayout.MinWidth(40f), GUILayout.ExpandWidth(true));
            if (typed != text)
            {
                entry.BoxedValue = typed;
            }
        }

        /// <summary>Why the picker can't open for this list now (none of its kinds can be read yet), or null. A tab whose
        /// kind can't be read yet says so in the window.</summary>
        internal static string Ready(ListSpec spec)
        {
            string first = null;
            foreach (ThingKind kind in spec.Kinds)
            {
                if (GameThings.Ready(kind, out string why))
                {
                    return null;
                }
                first = first ?? why;
            }
            return first;
        }

        /// <summary>How many things a list holds, in a few words (with "Keep the typed box beside the button" off).</summary>
        internal static string Summary(string text, ListSpec spec)
        {
            List<ListEntry> entries = ListText.Read(text, spec.Format);
            if (entries.Count == 0)
            {
                return string.IsNullOrEmpty(spec.EmptyMeans) ? "Empty" : "Empty: " + spec.EmptyMeans;
            }
            string noun = spec.Kinds.Length == 1 ? Noun(spec.Kinds[0], entries.Count) : entries.Count == 1 ? "thing" : "things";
            return entries.Count.ToString(CultureInfo.InvariantCulture) + " " + noun;
        }

        internal static string Noun(ThingKind kind, int count)
        {
            bool one = count == 1;
            switch (kind)
            {
                case ThingKind.Pieces:
                    return one ? "piece" : "pieces";
                case ThingKind.Items:
                    return one ? "item" : "items";
                case ThingKind.Creatures:
                    return one ? "creature" : "creatures";
                default:
                    return one ? "kind of item" : "kinds of item";
            }
        }
    }
}
