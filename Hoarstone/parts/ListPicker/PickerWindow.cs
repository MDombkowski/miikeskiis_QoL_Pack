// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using QoLMods.Catalogue;
using UnityEngine;

namespace QoLMods.ListPicker
{
    /// <summary>
    /// The picker window (card B03): drawn over the settings window (F1) with the same immediate-mode GUI, as Unity's modal
    /// window, which "will always be on top of all other GUI" and is the "sole recipient of all GUI input" while it shows
    /// (Unity's reference for GUI.ModalWindow), so F1 stays as it was under it; F1's own keys are held meanwhile
    /// (Manager.HoldKeys), so Escape closes only the picker. Tabs where a list holds more than one kind of thing; on the left the list of picks (PickerView);
    /// in the middle the pictures under their headings, a click adding a thing at the end of the list or taking every
    /// entry naming it out; on the right the list itself in its order, with an amount box where it has amounts, a handle
    /// and Up and Down where its order means something, and an X to take an entry out (names the game doesn't know too);
    /// at the bottom the name of what the pointer is on, "Put back as it was" and Done. Every change is written to the
    /// setting at once, as F1's own boxes write, by ListText, so the module reads it as before and nothing it doesn't
    /// touch changes.
    ///
    /// Immediate mode draws every control again for each event, a Layout pass first: so what is drawn changes only in a
    /// Layout pass (the view is worked out there), the pictures, the rows on the left and the list's rows are hit-tested
    /// by hand rather than drawn as controls (their number changes as they scroll), and the only controls whose number
    /// changes, the amount boxes, come last, each named, letting go of the keyboard whenever the rows under them move
    /// (LESSONS_LEARNED, a list drawn in F1). Plain characters only: the window's font may lack arrows and symbols.
    /// </summary>
    internal static class PickerWindow
    {
        // A number of its own: the settings window is -68, its editors -680 and -6800.
        private const int WindowId = -70341;

        private const string SearchName = "QoLMods_ListPicker_Search";
        private const string AmountName = "QoLMods_ListPicker_Amount:";

        private const float Pad = 8f;
        private const float TopY = 24f;
        private const float RowH = 22f;
        private const float BodyY = 92f;
        private const float FooterH = 56f;
        private const float RailW = 200f;
        private const float BarW = 14f;
        private const float RailRowH = 22f;
        private const float HeadingH = 24f;
        private const float ListRowH = 30f;
        private const float Gap = 6f;

        private sealed class ViewState
        {
            internal int Tab;
            internal string Pick;
            internal string Search = string.Empty;
            internal string Group = Browse.Auto;
            internal string Sort = Browse.Progression;
            internal readonly HashSet<string> Open = new HashSet<string>();
            internal float GridScroll;
            internal float RailScroll;
        }

        private enum Popup
        {
            None,
            Group,
            Sort,
        }

        private static readonly Dictionary<string, ViewState> Views = new Dictionary<string, ViewState>();
        private static readonly PickerView Model = new PickerView();
        private static readonly List<float> LineTops = new List<float>();
        private static readonly Dictionary<Thing, string[]> KeysByThing = new Dictionary<Thing, string[]>();
        private static readonly Dictionary<string, Thing> ThingByKey = new Dictionary<string, Thing>();
        private static readonly HashSet<string> OnKeys = new HashSet<string>();
        private static readonly Dictionary<ListEntry, string> Typed = new Dictionary<ListEntry, string>();
        private static readonly List<Thing> EntryThings = new List<Thing>();

        private static ConfigEntryBase entry;
        private static ListSpec spec;
        private static ViewState view;
        private static List<ListEntry> entries = new List<ListEntry>();
        private static string before = string.Empty;
        private static string seen = string.Empty;
        private static string title = string.Empty;
        private static bool dirty = true;
        private static bool letGo;
        private static int thingsVersion = -1;
        private static int columns = 1;
        private static float gridHeight;
        private static float listScroll;
        private static Rect window;
        private static Popup popup;
        private static ObjectDB openedWith;
        private static PickerHost host;
        private static int dragFrom = -1;
        private static int dragTo = -1;
        private static int dragControl;
        private static string hover = string.Empty;
        private static bool closing;

        // Opened from shudnal's edit window, on its stand-in entry: what the picker writes there is kept by that window's
        // Apply, not at once.
        private static bool standIn;

        private static Texture2D white;
        private static GUISkin stylesFor;
        private static GUIStyle bold;
        private static GUIStyle small;
        private static GUIStyle note;
        private static GUIStyle centred;
        private static GUIStyle count;

        /// <summary>A picker is open.</summary>
        internal static bool IsOpen => entry != null;

        /// <summary>Opens the picker for a list setting, over the settings window.</summary>
        internal static void Open(ConfigEntryBase setting, ListSpec with)
        {
            if (setting == null || with == null)
            {
                return;
            }
            if (host == null)
            {
                var holder = new GameObject(typeof(PickerWindow).Assembly.GetName().Name + "_ListPicker") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(holder);
                host = holder.AddComponent<PickerHost>();
            }
            entry = setting;
            spec = with;
            standIn = !Lists.IsAttached(setting);
            before = seen = setting.BoxedValue as string ?? string.Empty;
            entries = ListText.Read(before, spec.Format);
            // Kept by the setting's file, section and key, so shudnal's stand-in entry in its edit window shares it.
            string viewName = Lists.NameOf(setting);
            if (!Views.TryGetValue(viewName, out view) || !PickerSettings.RememberOn)
            {
                view = new ViewState();
                if (spec.OpensOn.HasValue)
                {
                    Selection pick = spec.OpensOn.Value;
                    view.Pick = PickerView.IdOf(pick);
                    // A row inside another shows only while that one is open (Browse.Shown): open it (review 1, m1:
                    // Grounded's Flooring is a member of Building Structures).
                    if (pick.Pick == Pick.Member)
                    {
                        view.Open.Add(Browse.OpenId(new Selection(Pick.Family, pick.Value)));
                    }
                    else if (pick.Pick == Pick.Category && Array.IndexOf(Taxonomy.BuildingSubs, pick.Value) >= 0)
                    {
                        view.Open.Add(Browse.OpenId(new Selection(Pick.Category, Taxonomy.BuildingParent)));
                    }
                    else if (pick.Pick == Pick.Material)
                    {
                        foreach ((string _, string label, string family) in Taxonomy.StructuralMaterials)
                        {
                            if (label == pick.Value)
                            {
                                view.Open.Add(Browse.OpenId(new Selection(Pick.MaterialFamily, family)));
                            }
                        }
                    }
                }
                Views[viewName] = view;
            }
            view.Tab = Mathf.Clamp(view.Tab, 0, spec.Kinds.Length - 1);
            string name = ModuleSettingsName(setting);
            title = (setting.Definition.Section ?? spec.Module) + ": " + name;
            openedWith = ObjectDB.instance;
            thingsVersion = -1;
            popup = Popup.None;
            dragFrom = dragTo = -1;
            listScroll = 0f;
            Typed.Clear();
            hover = string.Empty;
            dirty = true;
            letGo = true;
            closing = false;
            window = Placed();
            Manager.HoldKeys(true);
        }

        private static string ModuleSettingsName(ConfigEntryBase setting) =>
            QoLMods.Kit.ModuleSettings.HintsOf(setting)?.DispName ?? setting.Definition.Key;

        /// <summary>Closes the picker; what it wrote stays written.</summary>
        internal static void Close()
        {
            entry = null;
            spec = null;
            view = null;
            entries = new List<ListEntry>();
            Typed.Clear();
            popup = Popup.None;
            dragFrom = dragTo = -1;
            Manager.HoldKeys(false);
        }

        /// <summary>From the host's Update: closes the picker when the settings window under it was closed, or the game's
        /// item list it was opened with is gone (a new scene).</summary>
        internal static void Tick()
        {
            if (!IsOpen)
            {
                return;
            }
            if (closing || !Manager.WindowShows() || ObjectDB.instance != openedWith)
            {
                Close();
            }
        }

        // ---------------------------------------------------------------- the window

        /// <summary>From the host's OnGUI.</summary>
        internal static void OnGUI()
        {
            if (!IsOpen || closing)
            {
                return;
            }
            Matrix4x4 matrix = GUI.matrix;
            int depth = GUI.depth;
            Color colour = GUI.color;
            try
            {
                float scale = Manager.Scale();
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.depth = -3000;   // above the settings window's -100
                window = GUI.ModalWindow(WindowId, window, DrawWindow, string.Empty);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                Fail(error);
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.depth = depth;
                GUI.color = colour;
            }
        }

        // A failure closes the picker, says so once in the module's log, and leaves the setting as it was last written.
        private static void Fail(Exception error)
        {
            ListSpec failed = spec;
            ConfigEntryBase setting = entry;
            closing = true;
            Manager.HoldKeys(false);
            failed?.Warn?.Invoke($"The picker window for \"{setting?.Definition.Key}\" failed ({error.GetType().Name}: {error.Message}) and " +
                                 "closed; the setting keeps what was written, and its typed box works as before.");
        }

        // Where the window goes: its size by the setting, in the middle of the screen (in the settings window's scale).
        private static Rect Placed()
        {
            float scale = Manager.Scale();
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float part;
            switch (PickerSettings.Size)
            {
                case PickerSize.Small:
                    part = 0.62f;
                    break;
                case PickerSize.Medium:
                    part = 0.74f;
                    break;
                default:
                    part = 0.86f;
                    break;
            }
            float w = Mathf.Min(width - 20f, Mathf.Max(900f, width * part));
            float h = Mathf.Min(height - 20f, Mathf.Max(480f, height * Mathf.Min(0.9f, part + 0.04f)));
            return new Rect((width - w) / 2f, (height - h) / 2f, w, h);
        }

        private static void DrawWindow(int id)
        {
            if (!IsOpen || closing)
            {
                return;
            }
            try
            {
                Draw();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        private static void Draw()
        {
            Event e = Event.current;
            EventType type = e.type;
            Styles();
            float w = window.width;
            float h = window.height;
            bool amounts = spec.Format.Amounts;
            float rightW = amounts ? 330f : 280f;
            var rail = new Rect(Pad, BodyY, RailW, h - BodyY - FooterH);
            // The pictures start after the left list's scrollbar (review 1, m4) and end before their own.
            var grid = new Rect(rail.xMax + BarW + Pad, BodyY + RowH, Mathf.Max(60f, w - RailW - rightW - Pad * 4 - BarW * 3), h - BodyY - FooterH - RowH);
            var list = new Rect(w - rightW - Pad - BarW, BodyY + RowH, rightW, h - BodyY - FooterH - RowH);

            if (type == EventType.Layout)
            {
                Refresh(grid.width);
            }
            if (type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (popup != Popup.None)
                {
                    popup = Popup.None;
                }
                else
                {
                    closing = true;
                    Manager.HoldKeys(false);
                }
                e.Use();
                return;
            }

            // The window's ground, solid so the settings under it don't show through, and its title.
            Fill(new Rect(0f, 0f, w, h), new Color(0.11f, 0.11f, 0.12f, 0.97f));
            Frame(new Rect(0f, 0f, w, h), new Color(0.45f, 0.38f, 0.2f, 1f), 1f);
            GUI.Label(new Rect(Pad, 2f, w - Pad * 2f, RowH), title, bold);

            // An open Group or Sort list takes the click first; a click anywhere else closes it.
            Rect popupRect = PopupRect(w);
            if (popup != Popup.None && type == EventType.MouseDown)
            {
                if (popupRect.Contains(e.mousePosition))
                {
                    List<Choice> choices = popup == Popup.Group ? Model.Groups : Model.Sorts;
                    int at = Mathf.FloorToInt((e.mousePosition.y - popupRect.y) / RowH);
                    if (at >= 0 && at < choices.Count)
                    {
                        if (popup == Popup.Group)
                        {
                            view.Group = choices[at].Id;
                        }
                        else
                        {
                            view.Sort = choices[at].Id;
                        }
                        view.GridScroll = 0f;
                        dirty = true;
                    }
                }
                popup = Popup.None;
                e.Use();
            }

            int dragId = GUIUtility.GetControlID(FocusType.Passive);
            dragControl = dragId;
            DrawTop(w);
            DrawRail(rail);
            DrawGrid(grid);
            DrawList(list);
            DrawFooter(w, h);
            DrawAmounts(list);
            DrawPopup(popupRect);

            // The rows under the amount boxes moved: the box with the keyboard would become another entry's, so it lets go.
            // Asked at the end of a Layout pass, once this pass's boxes have their names (review 1, M2; the third fault of
            // the lesson "a list drawn in F1", which Portion Control's KindDrawers.LetGo answers the same way).
            // While a Group or Sort list is open the boxes aren't drawn: the request waits for a pass that draws them (review 2).
            if (type == EventType.Layout && letGo && popup == Popup.None)
            {
                if (GUI.GetNameOfFocusedControl().StartsWith(AmountName, StringComparison.Ordinal))
                {
                    GUIUtility.keyboardControl = 0;
                }
                letGo = false;
            }

            // Nothing under the picker hears a click, a key or the wheel while it is open.
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel ||
                e.type == EventType.KeyDown || e.type == EventType.KeyUp || e.type == EventType.MouseDrag)
            {
                e.Use();
            }
        }

        // ---------------------------------------------------------------- the view

        // In the Layout pass: reads the setting again if something else changed it, and works the view out again when
        // anything it shows changed.
        private static void Refresh(float gridWidth)
        {
            string now = entry.BoxedValue as string ?? string.Empty;
            if (now != seen)
            {
                seen = now;
                entries = ListText.Read(now, spec.Format);
                Typed.Clear();
                letGo = true;
                dirty = true;
            }
            if (GameThings.Version != thingsVersion)
            {
                thingsVersion = GameThings.Version;
                dirty = true;
            }
            float cell = CellSize();
            int fit = Mathf.Max(1, Mathf.FloorToInt((gridWidth + Gap) / (cell + Gap)));
            if (fit != columns)
            {
                columns = fit;
                dirty = true;
            }
            if (!dirty)
            {
                return;
            }
            dirty = false;
            Rebuild();
        }

        private static void Rebuild()
        {
            // Every thing of the list's kinds, by each name the module would take for it.
            KeysByThing.Clear();
            ThingByKey.Clear();
            foreach (ThingKind kind in spec.Kinds)
            {
                List<Thing> things = GameThings.Of(kind);
                GameThings.Refresh(things);
                foreach (Thing thing in things)
                {
                    foreach (string key in KeysOf(thing))
                    {
                        if (!ThingByKey.ContainsKey(key))
                        {
                            ThingByKey[key] = thing;
                        }
                    }
                }
            }
            OnKeys.Clear();
            EntryThings.Clear();
            foreach (ListEntry each in entries)
            {
                string key = ListText.Key(each.Name, spec.Format);
                OnKeys.Add(key);
                Thing thing = ThingFor(each);
                EntryThings.Add(thing);
            }

            ThingKind tab = spec.Kinds[Mathf.Clamp(view.Tab, 0, spec.Kinds.Length - 1)];
            var offered = new List<Thing>();
            var seenThings = new HashSet<Thing>();
            foreach (Thing thing in GameThings.Of(tab))
            {
                bool listed = OnList(thing);
                if (listed || ((spec.Holds == null || Holds(thing)) && (PickerSettings.UnfoundOn || thing.Found)))
                {
                    if (seenThings.Add(thing))
                    {
                        offered.Add(thing);
                    }
                }
            }
            foreach (Thing thing in EntryThings)
            {
                if (thing != null && thing.Kind == tab && seenThings.Add(thing))
                {
                    offered.Add(thing);
                }
            }
            FavoritePieceList store = tab == ThingKind.Pieces || tab == ThingKind.Items ? GameLists.Store : null;
            Func<Thing, bool> starred = store == null ? null : (Func<Thing, bool>)(thing => GameLists.IsStarred(store, thing.Listing?.FavoriteKey));
            Func<Thing, int, bool> inList = store == null ? null : (Func<Thing, int, bool>)((thing, list) => GameLists.InList(store, thing.Listing?.FavoriteKey, list));
            Model.Build(tab, offered, OnList, view.Search, view.Pick, view.Group, view.Sort, view.Open, columns, starred,
                        GameLists.ListCount(store), list => GameLists.ListName(store, list), inList);

            LineTops.Clear();
            float y = 0f;
            float cellH = CellHeight();
            foreach (GridLine line in Model.Lines)
            {
                LineTops.Add(y);
                y += line.Heading != null ? HeadingH : cellH + Gap;
            }
            gridHeight = y;
        }

        private static bool Holds(Thing thing)
        {
            try
            {
                return spec.Holds(thing);
            }
            catch (Exception)
            {
                return true;
            }
        }

        // The names a thing goes by in this list, as the module compares them.
        private static string[] KeysOf(Thing thing)
        {
            if (KeysByThing.TryGetValue(thing, out string[] keys))
            {
                return keys;
            }
            var set = new HashSet<string> { ListText.Key(thing.Name, spec.Format) };
            if (spec.ShownNames && thing.Kind != ThingKind.ItemTypes)
            {
                set.Add(ListText.Key(thing.Shown, spec.Format));
                if (!string.IsNullOrEmpty(thing.Token))
                {
                    set.Add(ListText.Key(thing.Token, spec.Format));
                }
            }
            set.Remove(string.Empty);
            keys = set.ToArray();
            KeysByThing[thing] = keys;
            return keys;
        }

        private static bool OnList(Thing thing)
        {
            foreach (string key in KeysOf(thing))
            {
                if (OnKeys.Contains(key))
                {
                    return true;
                }
            }
            return false;
        }

        // The thing an entry names: one the window lists, else one the game has by that exact name (a piece in no build
        // menu, a copy of an item), else none (another mod's, or a name the game doesn't have).
        private static Thing ThingFor(ListEntry each)
        {
            if (ThingByKey.TryGetValue(ListText.Key(each.Name, spec.Format), out Thing thing))
            {
                return thing;
            }
            foreach (ThingKind kind in spec.Kinds)
            {
                Thing extra = GameThings.Extra(kind, each.Name);
                if (extra != null)
                {
                    KeysOf(extra);
                    return extra;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- changes

        private static void Toggle(Thing thing)
        {
            ListText.Toggle(entries, KeysOf(thing), thing.Name, spec.Format);
            Changed(true);
        }

        // Writes the entries to the setting (only when the text changes), joined as the list's text joins its entries (as
        // it is now, else as it was when the window opened, else the module's own way), and works the view out again.
        private static void Changed(bool rowsMoved)
        {
            string joinFrom = ListText.HasJoin(seen, spec.Format) ? seen : before;
            string text = ListText.Write(entries, spec.Format, joinFrom);
            if (text != (entry.BoxedValue as string ?? string.Empty))
            {
                entry.BoxedValue = text;
            }
            seen = entry.BoxedValue as string ?? string.Empty;
            if (seen != text)
            {
                // The setting refused or changed the text: read back what it holds.
                entries = ListText.Read(seen, spec.Format);
            }
            if (rowsMoved)
            {
                Typed.Clear();
                letGo = true;
            }
            dirty = true;
        }

        // ---------------------------------------------------------------- the top: tabs, search, Group and Sort

        private static void DrawTop(float w)
        {
            Event e = Event.current;
            float x = Pad;
            if (spec.Kinds.Length > 1)
            {
                for (int i = 0; i < spec.Kinds.Length; i++)
                {
                    string label = TabLabel(spec.Kinds[i]);
                    float width = Mathf.Max(70f, GUI.skin.button.CalcSize(new GUIContent(label)).x + 12f);
                    var tab = new Rect(x, TopY, width, RowH);
                    bool current = i == view.Tab;
                    Fill(tab, current ? new Color(0.35f, 0.3f, 0.18f, 1f) : new Color(0.2f, 0.2f, 0.22f, 1f));
                    GUI.Label(tab, label, centred);
                    if (e.type == EventType.MouseDown && e.button == 0 && tab.Contains(e.mousePosition))
                    {
                        if (!current)
                        {
                            view.Tab = i;
                            view.Pick = null;
                            view.GridScroll = view.RailScroll = 0f;
                            dirty = true;
                        }
                        e.Use();
                    }
                    x += width + 4f;
                }
                x += Pad;
            }
            GUI.Label(new Rect(x, TopY, 50f, RowH), "Search");
            x += 52f;
            float sortW = 170f;
            float groupW = 210f;
            var sortRect = new Rect(w - Pad - sortW, TopY, sortW, RowH);
            var groupRect = new Rect(sortRect.x - 6f - groupW, TopY, groupW, RowH);
            // As wide as a quarter of the window, but never over Group (three tabs in a narrow window: review 1).
            float searchW = Mathf.Clamp(groupRect.x - 8f - 28f - x, 80f, Mathf.Max(140f, w * 0.25f));
            GUI.SetNextControlName(SearchName);
            string typed = GUI.TextField(new Rect(x, TopY, searchW, RowH), view.Search ?? string.Empty, 60);
            if (typed != view.Search)
            {
                view.Search = typed;
                view.GridScroll = 0f;
                dirty = true;
            }
            x += searchW + 4f;
            if (GUI.Button(new Rect(x, TopY, 24f, RowH), new GUIContent("X", "Clear the search")))
            {
                view.Search = string.Empty;
                GUIUtility.keyboardControl = 0;
                dirty = true;
            }
            if (GUI.Button(groupRect, "Group: " + (Model.Group?.Label ?? "Auto")))
            {
                popup = popup == Popup.Group ? Popup.None : Popup.Group;
            }
            if (GUI.Button(sortRect, "Sort: " + (Model.Sort?.Label ?? "Progression")))
            {
                popup = popup == Popup.Sort ? Popup.None : Popup.Sort;
            }
            // What the list does, and how to pick.
            string says = string.IsNullOrEmpty(spec.Says) ? string.Empty : spec.Says + " ";
            says += standIn
                ? "Click a picture to put it on the list or take it off; the edit window's Apply keeps it."
                : "Click a picture to put it on the list or take it off; every change is saved at once.";
            GUI.Label(new Rect(Pad, TopY + RowH + 2f, w - Pad * 2, BodyY - TopY - RowH - 4f), says, note);
        }

        private static string TabLabel(ThingKind kind)
        {
            switch (kind)
            {
                case ThingKind.Pieces:
                    return "Pieces";
                case ThingKind.Items:
                    return "Items";
                case ThingKind.Creatures:
                    return "Creatures";
                default:
                    return "Kinds of Item";
            }
        }

        private static Rect PopupRect(float w)
        {
            List<Choice> choices = popup == Popup.Group ? Model.Groups : popup == Popup.Sort ? Model.Sorts : null;
            if (choices == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }
            float width = popup == Popup.Group ? 210f : 170f;
            float x = popup == Popup.Group ? w - Pad - 170f - 6f - 210f : w - Pad - 170f;
            return new Rect(x, TopY + RowH, width, RowH * choices.Count);
        }

        private static void DrawPopup(Rect rect)
        {
            if (popup == Popup.None || Event.current.type != EventType.Repaint)
            {
                return;
            }
            List<Choice> choices = popup == Popup.Group ? Model.Groups : Model.Sorts;
            string current = popup == Popup.Group ? Model.Group?.Id : Model.Sort?.Id;
            Fill(rect, new Color(0.16f, 0.16f, 0.18f, 1f));
            for (int i = 0; i < choices.Count; i++)
            {
                var row = new Rect(rect.x, rect.y + i * RowH, rect.width, RowH);
                if (row.Contains(Event.current.mousePosition))
                {
                    Fill(row, new Color(0.3f, 0.3f, 0.34f, 1f));
                }
                GUI.Label(new Rect(row.x + 6f, row.y, row.width - 6f, row.height), (choices[i].Id == current ? "* " : "  ") + choices[i].Label);
            }
        }

        // ---------------------------------------------------------------- the list on the left

        private static void DrawRail(Rect rail)
        {
            Event e = Event.current;
            List<RailRow> rows = Model.Rail;
            float content = rows.Count * RailRowH;
            view.RailScroll = Mathf.Clamp(view.RailScroll, 0f, Mathf.Max(0f, content - rail.height));
            if (e.type == EventType.ScrollWheel && rail.Contains(e.mousePosition))
            {
                view.RailScroll = Mathf.Clamp(view.RailScroll + e.delta.y * 20f, 0f, Mathf.Max(0f, content - rail.height));
                e.Use();
            }
            var bar = new Rect(rail.xMax, rail.y, BarW, rail.height);
            view.RailScroll = GUI.VerticalScrollbar(bar, view.RailScroll, rail.height, 0f, Mathf.Max(content, rail.height));
            if (e.type == EventType.MouseDown && e.button == 0 && rail.Contains(e.mousePosition))
            {
                int at = Mathf.FloorToInt((e.mousePosition.y - rail.y + view.RailScroll) / RailRowH);
                if (at >= 0 && at < rows.Count && !rows[at].Title)
                {
                    RailRow row = rows[at];
                    if (row.OpenId != null && (e.mousePosition.x < rail.x + 22f || view.Pick == row.Id || Model.Picked == row))
                    {
                        if (!view.Open.Remove(row.OpenId))
                        {
                            view.Open.Add(row.OpenId);
                        }
                    }
                    else if (row.OpenId != null)
                    {
                        view.Open.Add(row.OpenId);
                    }
                    view.Pick = row.Id;
                    view.GridScroll = 0f;
                    dirty = true;
                }
                e.Use();
            }
            if (e.type != EventType.Repaint)
            {
                return;
            }
            Fill(rail, new Color(0.15f, 0.15f, 0.16f, 1f));
            GUI.BeginGroup(rail);
            try
            {
                int first = Mathf.Max(0, Mathf.FloorToInt(view.RailScroll / RailRowH));
                for (int i = first; i < rows.Count; i++)
                {
                    float y = i * RailRowH - view.RailScroll;
                    if (y > rail.height)
                    {
                        break;
                    }
                    RailRow row = rows[i];
                    var place = new Rect(0f, y, rail.width, RailRowH);
                    if (row.Title)
                    {
                        GUI.Label(new Rect(6f, y + 2f, rail.width - 6f, RailRowH), row.Label, bold);
                        continue;
                    }
                    if (row == Model.Picked)
                    {
                        Fill(place, new Color(0.38f, 0.32f, 0.16f, 1f));
                    }
                    else if (place.Contains(Event.current.mousePosition))
                    {
                        Fill(place, new Color(0.25f, 0.25f, 0.28f, 1f));
                    }
                    float indent = row.Member ? 24f : 8f;
                    if (row.OpenId != null)
                    {
                        GUI.Label(new Rect(4f, y, 18f, RailRowH), row.Open ? "-" : "+", centred);
                        indent = 22f;
                    }
                    GUI.Label(new Rect(indent, y, rail.width - indent - 40f, RailRowH), row.Label);
                    GUI.Label(new Rect(rail.width - 44f, y, 40f, RailRowH), row.Count.ToString(CultureInfo.InvariantCulture), count);
                }
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        // ---------------------------------------------------------------- the pictures

        private static float CellSize() => PickerSettings.LargePictures ? 56f : 40f;

        private static float CellHeight() => CellSize() + (PickerSettings.NamesOn ? 28f : 0f);

        private static void DrawGrid(Rect grid)
        {
            Event e = Event.current;
            // The line above the pictures: what is shown, how it is grouped, how many.
            string grouped = Model.Group == null || Model.Group.Id == Browse.None ? "not grouped"
                : Model.Group.Id == Browse.Auto ? (Model.Group.Label == "Auto" ? "one heading" : "by " + Model.Group.Dimension.Label.ToLowerInvariant())
                : "by " + Model.Group.Label.ToLowerInvariant();
            string where = (Model.Picked?.Label ?? "All") + " · " + grouped + " · " + Model.Shown.ToString(CultureInfo.InvariantCulture) + " shown";
            if (!string.IsNullOrWhiteSpace(view.Search))
            {
                where += " · searching \"" + view.Search.Trim() + "\"";
            }
            GUI.Label(new Rect(grid.x, grid.y - RowH, grid.width, RowH), where, small);

            float content = gridHeight;
            view.GridScroll = Mathf.Clamp(view.GridScroll, 0f, Mathf.Max(0f, content - grid.height));
            if (e.type == EventType.ScrollWheel && grid.Contains(e.mousePosition))
            {
                view.GridScroll = Mathf.Clamp(view.GridScroll + e.delta.y * 20f, 0f, Mathf.Max(0f, content - grid.height));
                e.Use();
            }
            var bar = new Rect(grid.xMax, grid.y, BarW, grid.height);
            view.GridScroll = GUI.VerticalScrollbar(bar, view.GridScroll, grid.height, 0f, Mathf.Max(content, grid.height));

            float cell = CellSize();
            float cellH = CellHeight();
            if (e.type == EventType.MouseDown && e.button == 0 && grid.Contains(e.mousePosition))
            {
                Thing hit = ThingAt(grid, e.mousePosition, cell, cellH);
                if (hit != null)
                {
                    Toggle(hit);
                }
                e.Use();
            }
            if (e.type != EventType.Repaint)
            {
                return;
            }
            Thing pointed = grid.Contains(e.mousePosition) ? ThingAt(grid, e.mousePosition, cell, cellH) : null;
            if (pointed != null)
            {
                hover = Describe(pointed);
            }
            GUI.BeginGroup(grid);
            try
            {
                if (Model.Lines.Count == 0)
                {
                    ThingKind tab = spec.Kinds[Mathf.Clamp(view.Tab, 0, spec.Kinds.Length - 1)];
                    string nothing = !GameThings.Ready(tab, out string why) ? "The game's " + TabLabel(tab).ToLowerInvariant() + " can be read " + why + "."
                        : string.IsNullOrWhiteSpace(view.Search) ? "Nothing here." : "Nothing matches the search here.";
                    GUI.Label(new Rect(4f, 4f, grid.width - 8f, RowH * 2f), nothing, note);
                }
                for (int i = 0; i < Model.Lines.Count; i++)
                {
                    float y = LineTops[i] - view.GridScroll;
                    GridLine line = Model.Lines[i];
                    float height = line.Heading != null ? HeadingH : cellH + Gap;
                    if (y + height < 0f)
                    {
                        continue;
                    }
                    if (y > grid.height)
                    {
                        break;
                    }
                    if (line.Heading != null)
                    {
                        GUI.Label(new Rect(2f, y + 3f, grid.width - 4f, HeadingH - 3f),
                                  line.Heading + "  (" + line.Count.ToString(CultureInfo.InvariantCulture) + ")", bold);
                        continue;
                    }
                    for (int c = 0; c < line.Cells.Count; c++)
                    {
                        DrawCell(new Rect(c * (cell + Gap), y, cell, cellH), line.Cells[c], cell);
                    }
                }
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private static Thing ThingAt(Rect grid, Vector2 mouse, float cell, float cellH)
        {
            float y = mouse.y - grid.y + view.GridScroll;
            for (int i = 0; i < Model.Lines.Count; i++)
            {
                GridLine line = Model.Lines[i];
                float top = LineTops[i];
                float height = line.Heading != null ? HeadingH : cellH + Gap;
                if (y < top || y >= top + height)
                {
                    continue;
                }
                if (line.Heading != null || y >= top + cellH)
                {
                    return null;
                }
                float x = mouse.x - grid.x;
                int column = Mathf.FloorToInt(x / (cell + Gap));
                if (column < 0 || column >= line.Cells.Count || x - column * (cell + Gap) > cell)
                {
                    return null;
                }
                return line.Cells[column];
            }
            return null;
        }

        private static void DrawCell(Rect place, Thing thing, float cell)
        {
            bool listed = OnList(thing);
            var square = new Rect(place.x, place.y, cell, cell);
            bool pointed = square.Contains(Event.current.mousePosition);
            Fill(square, listed ? new Color(0.24f, 0.36f, 0.2f, 1f) : pointed ? new Color(0.3f, 0.3f, 0.33f, 1f) : new Color(0.19f, 0.19f, 0.21f, 1f));
            Thumbs.Draw(new Rect(square.x + 3f, square.y + 3f, cell - 6f, cell - 6f), thing.Icon, thing.Shown, !thing.Found);
            if (listed)
            {
                Frame(square, new Color(0.55f, 0.85f, 0.35f, 1f), 2f);
                string mark = Mark(thing);
                if (mark.Length > 0)
                {
                    var tag = new Rect(square.xMax - 30f, square.yMax - 16f, 29f, 15f);
                    Fill(tag, new Color(0f, 0f, 0f, 0.65f));
                    GUI.Label(tag, mark, count);
                }
            }
            if (PickerSettings.NamesOn)
            {
                GUI.Label(new Rect(place.x - 2f, place.y + cell, cell + 4f, 28f), thing.Shown, small);
            }
        }

        // What a ticked picture shows in its corner: its place in an ordered list (its first entry's, as the modules read a
        // name given twice), or its amount as the module reads it when it is given twice (review 1): the amounts added up
        // where the module adds them (Hoarstone's cost), else the last one's (Topper-Upper's); an entry whose amount can't
        // be read is left out, as both modules leave it out. "?" when none can be read.
        private static string Mark(Thing thing)
        {
            string[] keys = KeysOf(thing);
            int first = -1;
            int? amount = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (Array.IndexOf(keys, ListText.Key(entries[i].Name, spec.Format)) < 0)
                {
                    continue;
                }
                if (first < 0)
                {
                    first = i;
                }
                if (spec.Format.Amounts)
                {
                    int? count = entries[i].Count(spec.Format);
                    if (!count.HasValue)
                    {
                        continue;
                    }
                    amount = spec.Format.AmountsAddUp ? Math.Min(spec.Format.MostAmount, (amount ?? 0) + count.Value) : count;
                }
            }
            if (first < 0)
            {
                return string.Empty;
            }
            if (spec.Format.Amounts)
            {
                return amount.HasValue ? "x" + amount.Value.ToString(CultureInfo.InvariantCulture) : "?";
            }
            return spec.Format.Ordered ? (first + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Describe(Thing thing)
        {
            string text = thing.Shown;
            if (thing.Name != thing.Shown)
            {
                text += "  (" + thing.Name + ")";
            }
            if (!string.IsNullOrEmpty(thing.Where))
            {
                text += "  ·  " + thing.Where;
            }
            if (!thing.Found)
            {
                text += "  ·  not found yet";
            }
            if (spec.Holds != null && !Holds(thing))
            {
                text += "  ·  not one this list uses";
            }
            return text;
        }

        // ---------------------------------------------------------------- the list itself

        private static float AmountX(Rect list) => list.width - 22f * (spec.Format.Ordered ? 3f : 1f) - 6f - 58f;

        private static void DrawList(Rect list)
        {
            Event e = Event.current;
            bool ordered = spec.Format.Ordered;
            string head = "On the List: " + entries.Count.ToString(CultureInfo.InvariantCulture) + (ordered ? " (in order: the first comes first)" : string.Empty);
            GUI.Label(new Rect(list.x, list.y - RowH, list.width, RowH), head, bold);
            float content = entries.Count * ListRowH;
            listScroll = Mathf.Clamp(listScroll, 0f, Mathf.Max(0f, content - list.height));
            if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition))
            {
                listScroll = Mathf.Clamp(listScroll + e.delta.y * 20f, 0f, Mathf.Max(0f, content - list.height));
                letGo = true;
                e.Use();
            }
            var bar = new Rect(list.xMax, list.y, BarW, list.height);
            float scrolled = GUI.VerticalScrollbar(bar, listScroll, list.height, 0f, Mathf.Max(content, list.height));
            if (Mathf.Abs(scrolled - listScroll) > 0.01f)
            {
                listScroll = scrolled;
                letGo = true;
            }

            // Its buttons, hit by hand: the handle starts a drag, Up and Down move, X takes the entry out.
            float buttons = list.width - 22f * (ordered ? 3f : 1f) - 4f;
            if (e.type == EventType.MouseDown && e.button == 0 && list.Contains(e.mousePosition) && popup == Popup.None)
            {
                int at = Mathf.FloorToInt((e.mousePosition.y - list.y + listScroll) / ListRowH);
                float x = e.mousePosition.x - list.x;
                if (at >= 0 && at < entries.Count)
                {
                    if (ordered && x < 16f)
                    {
                        dragFrom = dragTo = at;
                        GUIUtility.hotControl = dragControl;
                        e.Use();
                    }
                    else if (x >= buttons)
                    {
                        int button = Mathf.FloorToInt((x - buttons) / 22f);
                        if (ordered && button == 0)
                        {
                            ListText.Move(entries, at, at - 1);
                            Changed(true);
                        }
                        else if (ordered && button == 1)
                        {
                            ListText.Move(entries, at, at + 1);
                            Changed(true);
                        }
                        else if (button == (ordered ? 2 : 0))
                        {
                            entries.RemoveAt(at);
                            Changed(true);
                        }
                        e.Use();
                    }
                    else if (!spec.Format.Amounts || x < AmountX(list))
                    {
                        e.Use();
                    }
                }
            }
            if (dragFrom >= 0)
            {
                if (e.type == EventType.MouseDrag && GUIUtility.hotControl == dragControl)
                {
                    dragTo = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - list.y + listScroll) / ListRowH), 0, Math.Max(0, entries.Count - 1));
                    e.Use();
                }
                else if (e.rawType == EventType.MouseUp)
                {
                    if (GUIUtility.hotControl == dragControl)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    if (dragTo != dragFrom && dragFrom < entries.Count)
                    {
                        ListText.Move(entries, dragFrom, dragTo);
                        Changed(true);
                    }
                    dragFrom = dragTo = -1;
                    e.Use();
                }
            }

            if (e.type != EventType.Repaint)
            {
                return;
            }
            Fill(list, new Color(0.15f, 0.15f, 0.16f, 1f));
            if (entries.Count == 0)
            {
                string empty = "Nothing on the list yet.";
                if (!string.IsNullOrEmpty(spec.EmptyMeans))
                {
                    empty += " Empty means: " + spec.EmptyMeans + ".";
                }
                GUI.Label(new Rect(list.x + 6f, list.y + 6f, list.width - 12f, RowH * 3f), empty, note);
                return;
            }
            GUI.BeginGroup(list);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    float y = i * ListRowH - listScroll;
                    if (y + ListRowH < 0f)
                    {
                        continue;
                    }
                    if (y > list.height)
                    {
                        break;
                    }
                    DrawRow(i, y, list.width, buttons);
                }
                if (dragFrom >= 0 && dragTo >= 0)
                {
                    float y = (dragTo > dragFrom ? dragTo + 1 : dragTo) * ListRowH - listScroll;
                    Fill(new Rect(0f, y - 1f, list.width, 2f), new Color(0.95f, 0.8f, 0.3f, 1f));
                }
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private static void DrawRow(int i, float y, float width, float buttons)
        {
            ListEntry each = entries[i];
            Thing thing = i < EntryThings.Count ? EntryThings[i] : null;
            bool ordered = spec.Format.Ordered;
            var row = new Rect(0f, y, width, ListRowH);
            Vector2 mouse = Event.current.mousePosition;
            if (row.Contains(mouse))
            {
                Fill(row, new Color(0.24f, 0.24f, 0.27f, 1f));
                hover = thing != null ? Describe(thing) : "\"" + each.Name + "\": " + Unknown(true) + "; kept on the list.";
            }
            if (dragFrom == i)
            {
                Fill(row, new Color(0.32f, 0.28f, 0.16f, 1f));
            }
            float x = 2f;
            if (ordered)
            {
                GUI.Label(new Rect(x, y, 14f, ListRowH), "::", centred);
                x += 16f;
            }
            Thumbs.Draw(new Rect(x, y + 2f, ListRowH - 4f, ListRowH - 4f), thing?.Icon, thing?.Shown ?? each.Name, thing != null && !thing.Found);
            x += ListRowH;
            float labelW = (spec.Format.Amounts ? AmountX(new Rect(0f, 0f, width, 0f)) : buttons) - x - 4f;
            string label = (ordered ? (i + 1).ToString(CultureInfo.InvariantCulture) + ". " : string.Empty) + (thing?.Shown ?? each.Name);
            string why = thing == null ? Unknown(false) : spec.Holds != null && !Holds(thing) ? "not one this list uses" : null;
            if (why != null)
            {
                GUI.Label(new Rect(x, y, labelW, ListRowH / 2f + 2f), label, small);
                GUI.Label(new Rect(x, y + ListRowH / 2f - 2f, labelW, ListRowH / 2f + 2f), why, note);
            }
            else
            {
                GUI.Label(new Rect(x, y + 4f, labelW, ListRowH - 4f), label);
            }
            float bx = buttons;
            if (ordered)
            {
                ButtonFace(new Rect(bx, y + 4f, 20f, ListRowH - 8f), "Up");
                ButtonFace(new Rect(bx + 22f, y + 4f, 20f, ListRowH - 8f), "Dn");
                bx += 44f;
            }
            ButtonFace(new Rect(bx, y + 4f, 20f, ListRowH - 8f), "X");
        }

        // What an entry the game has no thing for is, in words: at the main menu the world's own pieces and creatures can't
        // be read yet (only the build menus' and the item list are), so it may well be a real one (review 1, m5).
        private static string Unknown(bool longer)
        {
            bool waits = ZNetScene.instance == null && Array.Exists(spec.Kinds, kind => kind == ThingKind.Pieces || kind == ThingKind.Creatures);
            if (waits)
            {
                return longer ? "not found among what the game has read so far; the world's own pieces and creatures are read in a world"
                    : "read in a world";
            }
            return longer ? "not a name this game has (another mod's, or a typo)" : "not in this game";
        }

        // The amount boxes, last of the window's controls (their number changes with the list), each named.
        private static void DrawAmounts(Rect list)
        {
            if (!spec.Format.Amounts || popup != Popup.None)
            {
                return;
            }
            float x = list.x + AmountX(list);
            for (int i = 0; i < entries.Count; i++)
            {
                float y = list.y + i * ListRowH - listScroll;
                if (y < list.y - 1f || y + ListRowH > list.yMax + 1f)
                {
                    continue;
                }
                ListEntry each = entries[i];
                string shown = Typed.TryGetValue(each, out string typed) ? typed : each.Amount ?? string.Empty;
                GUI.SetNextControlName(AmountName + i.ToString(CultureInfo.InvariantCulture));
                var box = new Rect(x, y + 4f, 56f, ListRowH - 8f);
                // No length limit: a box limited to fewer characters than an amount already written would cut it just by
                // drawing it, and the cut amount would be written (review 2).
                string now = GUI.TextField(box, shown);
                if (now.Length == 0 && Event.current.type == EventType.Repaint && spec.Format.AmountLeftOut.HasValue)
                {
                    GUI.Label(new Rect(box.x + 4f, box.y, box.width, box.height), spec.Format.AmountLeftOut.Value.ToString(CultureInfo.InvariantCulture), note);
                }
                if (now != shown)
                {
                    Typed[each] = now;
                    if (ListText.SetAmount(each, now, spec.Format))
                    {
                        Changed(false);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the bottom

        private static void DrawFooter(float w, float h)
        {
            var line = new Rect(Pad, h - FooterH + 4f, w - Pad * 2f, RowH);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Label(line, hover, small);
                hover = string.Empty;
            }
            float y = h - RowH - Pad;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && seen != before;
            if (GUI.Button(new Rect(Pad, y, 180f, RowH), new GUIContent("Put back as it was", "The list as it was when this window opened.")))
            {
                entries = ListText.Read(before, spec.Format);
                entry.BoxedValue = before;
                seen = entry.BoxedValue as string ?? string.Empty;
                Typed.Clear();
                letGo = true;
                dirty = true;
            }
            GUI.enabled = enabled;
            string text = seen.Length > 0 ? seen : "(empty)";
            GUI.Label(new Rect(Pad + 190f, y, w - Pad * 2f - 190f - 110f, RowH), "Saved as: " + text, small);
            if (GUI.Button(new Rect(w - Pad - 100f, y, 100f, RowH), "Done"))
            {
                closing = true;
                Manager.HoldKeys(false);
            }
        }

        // ---------------------------------------------------------------- drawing

        private static void Styles()
        {
            GUISkin skin = GUI.skin;
            if (bold != null && stylesFor == skin)
            {
                return;
            }
            stylesFor = skin;
            bold = new GUIStyle(skin.label) { fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            // A skin's font size 0 is its font's own (about 12 for the window's): a little smaller than the labels.
            small = new GUIStyle(skin.label) { fontSize = skin.label.fontSize > 0 ? Mathf.Max(9, skin.label.fontSize - 1) : 11, clipping = TextClipping.Clip, wordWrap = false };
            note = new GUIStyle(skin.label) { fontStyle = FontStyle.Italic, wordWrap = true };
            note.normal.textColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            centred = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleCenter };
            count = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleRight, fontSize = small.fontSize };
        }

        private static void Fill(Rect rect, Color colour)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (white == null)
            {
                white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Color was = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, white);
            GUI.color = was;
        }

        private static void Frame(Rect rect, Color colour, float thick)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thick), colour);
            Fill(new Rect(rect.x, rect.yMax - thick, rect.width, thick), colour);
            Fill(new Rect(rect.x, rect.y, thick, rect.height), colour);
            Fill(new Rect(rect.xMax - thick, rect.y, thick, rect.height), colour);
        }

        private static void ButtonFace(Rect rect, string text)
        {
            Fill(rect, rect.Contains(Event.current.mousePosition) ? new Color(0.42f, 0.42f, 0.46f, 1f) : new Color(0.3f, 0.3f, 0.33f, 1f));
            GUI.Label(rect, text, centred);
        }
    }

    /// <summary>The picker's place in the scene: Unity calls a component's OnGUI, so the window has one of its own (made
    /// at the first Pick, kept across scenes, hidden).</summary>
    internal sealed class PickerHost : MonoBehaviour
    {
        private void OnGUI() => PickerWindow.OnGUI();

        private void Update() => PickerWindow.Tick();
    }
}
