using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// "Which chest first" in a configuration manager's window (F1), drawn with the window's own immediate-mode GUI in place
    /// of its text (card A41's second round, decision 28): its name, then a row per rule, each with a handle ("::") to drag
    /// it by, a box that switches it on, its words and Up and Down buttons, then a line saying what decides after the rules.
    /// A press on a row (anywhere but its box and buttons), a move and a release put it where the marker line shows; the two
    /// rules that sort set-up chests among themselves stay below "Chests set up for it" and move with it (ChestOrderList).
    /// A row that does nothing now is greyed, with a note why. Plain characters only: the window's font may lack arrows and
    /// symbols (FoldDrawer's note). After Map View Rules' ZoomDrawer and StoreStash's own FoldDrawer.
    /// </summary>
    internal static class ChestOrderDrawer
    {
        internal const string Title = "Which chest first";

        internal const string Then = "Then: chests already holding it, trophy chests, the nearest.";

        // Tells this drawer's control apart from the window's others (GUIUtility.GetControlID).
        private const int ControlHint = 0x5a0c4e17;

        private static readonly List<Rect> rows = new List<Rect>();
        private static int dragged = -1;
        private static int gap = -1;
        private static int control;
        private static int drawnFrame = -1;
        private static GUIStyle note;
        private static GUIStyle noteFrom;
        private static GUIStyle handle;
        private static GUIStyle handleFrom;

        /// <summary>Draws the setting, and writes it back when a row is switched or moved.</summary>
        internal static void Draw(ConfigEntryBase entry)
        {
            Event current = Event.current;
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            control = id;
            drawnFrame = Time.frameCount;
            if (dragged >= 0 && GUIUtility.hotControl != id)
            {
                dragged = -1;   // the drag ended where this drawer didn't see it
            }
            ChestOrderList list = ChestOrderList.Parse(entry?.BoxedValue as string);
            bool setupsOn = Settings.Enabled != null && Settings.Enabled.Value && SetupSettings.On != null && SetupSettings.On.Value;
            bool personalUsed = Settings.PersonalChests == null || Settings.PersonalChests.Value;
            bool measure = current.type != EventType.Layout;
            if (measure)
            {
                rows.Clear();
            }
            int switched = -1;
            int up = -1;
            int down = -1;
            Color colour = GUI.contentColor;
            bool enabled = GUI.enabled;
            GUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(Title, entry?.Description?.Description), GUILayout.ExpandWidth(true));
            for (int i = 0; i < list.Count; i++)
            {
                ChestOrder rule = list[i];
                string why = ChestOrderList.Note(rule, setupsOn, list.IsOn(ChestOrder.SetUp), personalUsed);
                // Every row draws the same controls in every pass, whatever changed: immediate-mode layout counts them.
                GUILayout.BeginHorizontal();
                GUILayout.Label("::", HandleStyle(), GUILayout.Width(18f));
                GUILayout.Space(ChestOrderList.Indented(rule) ? 18f : 0f);
                bool on = GUILayout.Toggle(list.IsOn(rule), GUIContent.none, GUILayout.Width(22f));
                if (on != list.IsOn(rule))
                {
                    switched = i;
                }
                GUI.contentColor = why.Length > 0 ? Dim(colour) : colour;
                GUILayout.Label(ChestOrderList.Words(rule), GUILayout.ExpandWidth(false));
                GUILayout.Label(why, NoteStyle(), GUILayout.ExpandWidth(true));
                GUI.contentColor = colour;
                GUI.enabled = enabled && list.CanUp(i);
                if (GUILayout.Button("Up", GUILayout.Width(44f)))
                {
                    up = i;
                }
                GUI.enabled = enabled && list.CanDown(i);
                if (GUILayout.Button("Down", GUILayout.Width(56f)))
                {
                    down = i;
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
                if (measure)
                {
                    rows.Add(GUILayoutUtility.GetLastRect());
                }
            }
            GUI.contentColor = Dim(colour);
            GUILayout.Label(Then, NoteStyle(), GUILayout.ExpandWidth(true));
            GUI.contentColor = colour;
            GUILayout.EndVertical();

            // Applied once every row is drawn, so each pass draws the rows it measured.
            bool changed = false;
            if (switched >= 0)
            {
                changed |= list.SetOn(list[switched], !list.IsOn(list[switched]));
            }
            if (up >= 0)
            {
                changed |= list.Up(up);
            }
            if (down >= 0)
            {
                changed |= list.Down(down);
            }
            changed |= Drag(id, current, list);
            if (changed && entry != null)
            {
                entry.BoxedValue = list.Format();
            }
        }

        /// <summary>Every frame, outside the window's drawing: a drag whose window closed, or whose row was folded away,
        /// mid-drag lets go, so the window's other controls answer clicks again.</summary>
        internal static void Tick()
        {
            if (dragged >= 0 && Time.frameCount - drawnFrame > 2)
            {
                Release();
            }
        }

        /// <summary>Lets go of a drag under way (also the undo step).</summary>
        internal static void Release()
        {
            if (dragged < 0)
            {
                return;
            }
            dragged = -1;
            try
            {
                if (GUIUtility.hotControl == control)
                {
                    GUIUtility.hotControl = 0;
                }
            }
            catch (Exception)
            {
                // Outside the GUI's own turn the engine may refuse: the next press in the window lets go instead.
            }
        }

        // A press on a row, the moves, and the release: true when the release moved a row.
        private static bool Drag(int id, Event current, ChestOrderList list)
        {
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (GUIUtility.hotControl == id)
                    {
                        // A release this drawer never saw (outside the window): this press only lets go.
                        GUIUtility.hotControl = 0;
                        dragged = -1;
                        current.Use();
                        return false;
                    }
                    int row = RowAt(current.mousePosition);
                    if (current.button == 0 && row >= 0)
                    {
                        GUIUtility.hotControl = id;
                        dragged = row;
                        gap = row;
                        current.Use();
                    }
                    return false;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && dragged >= 0)
                    {
                        gap = GapAt(current.mousePosition);
                        current.Use();
                    }
                    return false;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id)
                    {
                        return false;
                    }
                    GUIUtility.hotControl = 0;
                    bool moved = dragged >= 0 && dragged < list.Count && list.Move(dragged, gap);
                    dragged = -1;
                    current.Use();
                    return moved;
                case EventType.Repaint:
                    if (GUIUtility.hotControl == id && dragged >= 0 && dragged < rows.Count)
                    {
                        Mark(rows[dragged], gap);
                    }
                    return false;
                default:
                    return false;
            }
        }

        // The row dragged, faintly lit, and a line where it would drop.
        private static void Mark(Rect from, int at)
        {
            Color before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(from, Texture2D.whiteTexture);
            if (at >= 0 && rows.Count > 0)
            {
                float y = at < rows.Count ? rows[at].yMin : rows[rows.Count - 1].yMax;
                Rect first = rows[0];
                GUI.color = new Color(1f, 0.78f, 0.2f, 0.95f);
                GUI.DrawTexture(new Rect(first.xMin, y - 1f, first.width, 2f), Texture2D.whiteTexture);
            }
            GUI.color = before;
        }

        private static int RowAt(Vector2 point)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Contains(point))
                {
                    return i;
                }
            }
            return -1;
        }

        // The gap nearest the pointer: above the first row whose middle is below it, or below the last.
        private static int GapAt(Vector2 point)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (point.y < rows[i].center.y)
                {
                    return i;
                }
            }
            return rows.Count;
        }

        private static Color Dim(Color colour) => new Color(colour.r, colour.g, colour.b, colour.a * 0.5f);

        // Made from the window's own skin, and made again if the window changes its skin (as FoldDrawer's).
        private static GUIStyle NoteStyle()
        {
            if (note == null || noteFrom != GUI.skin.label)
            {
                noteFrom = GUI.skin.label;
                note = new GUIStyle(noteFrom) { wordWrap = true };
            }
            note.fontSize = noteFrom.fontSize;   // the window sets its font size for each call
            return note;
        }

        private static GUIStyle HandleStyle()
        {
            if (handle == null || handleFrom != GUI.skin.label)
            {
                handleFrom = GUI.skin.label;
                handle = new GUIStyle(handleFrom) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            handle.fontSize = handleFrom.fontSize;
            return handle;
        }
    }
}
