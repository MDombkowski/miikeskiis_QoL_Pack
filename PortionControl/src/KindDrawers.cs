using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace

namespace PortionControl
{
    /// <summary>
    /// The three lists of "Amounts by item, category and stack size" in a configuration manager's window (F1), drawn with
    /// the window's own immediate-mode GUI in place of their text (his feedback of 2026-09-30: items ticked from a list he
    /// can search). Each writes its setting back as AmountRules reads it ("key: amounts; …") at every change, so the file
    /// and the split window follow at once. The item list draws only its rows in view, so its few hundred items cost what
    /// a screenful does.
    /// </summary>
    internal static class KindDrawers
    {
        private const float RowHeight = 22f;
        private const float ItemsHeight = 320f;
        private const float CategoriesHeight = 260f;
        private const float NameWidth = 230f;
        private const float AmountsWidth = 170f;

        private static string search = string.Empty;
        private static bool onlyTicked;
        private static Vector2 itemScroll;
        private static Vector2 categoryScroll;
        private static GUIStyle heading;
        private static GUIStyle headingFrom;
        private static GUIStyle hint;
        private static GUIStyle hintFrom;

        // What each setting read last, so a list is read again only when its text changes.
        private static readonly Dictionary<ConfigEntryBase, (string Text, List<KeyValuePair<string, string>> Entries)> read =
            new Dictionary<ConfigEntryBase, (string, List<KeyValuePair<string, string>>)>();

        /// <summary>The drawer of "By item".</summary>
        internal static void Items(ConfigEntryBase entry) => Guarded(entry, DrawItems);

        /// <summary>The drawer of "By category".</summary>
        internal static void Categories(ConfigEntryBase entry) => Guarded(entry, DrawCategories);

        /// <summary>The drawer of "By stack size".</summary>
        internal static void StackSizes(ConfigEntryBase entry) => Guarded(entry, DrawStackSizes);

        /// <summary>The three lists take the whole row: their own titles, not the manager's name column.</summary>
        internal static void TakeWholeRow(params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase entry in entries)
            {
                Hints hints = ModuleSettings.HintsOf(entry);
                if (hints != null)
                {
                    hints.HideSettingName = true;
                }
            }
        }

        // A drawer's error is written once and the plain text drawn instead: the manager's window must never break, and
        // nothing in the game depends on it.
        private static readonly HashSet<ConfigEntryBase> broken = new HashSet<ConfigEntryBase>();

        // The groups a drawer has open (Begin… without its End… yet), so an error part-way closes them in order and the
        // manager's own window stays balanced (review round 1).
        private enum Group
        {
            Horizontal,
            Vertical,
            Scroll,
        }

        private static readonly Stack<Group> open = new Stack<Group>();

        /// <summary>The name every amounts box carries, so the list can tell when one of them has the keyboard.</summary>
        private const string BoxName = "PortionControl_Amounts:";

        private static void Guarded(ConfigEntryBase entry, Action<ConfigEntryBase> draw)
        {
            if (broken.Contains(entry))
            {
                GUILayout.Label(entry.Definition.Key + ": " + entry.BoxedValue, GUILayout.ExpandWidth(true));
                return;
            }
            open.Clear();
            try
            {
                BeginVertical(GUILayout.ExpandWidth(true));
                draw(entry);
                EndVertical();
            }
            catch (ExitGUIException)
            {
                open.Clear();   // Unity's own way out of a GUI pass: it puts its groups right itself
                throw;
            }
            catch (Exception error)
            {
                broken.Add(entry);
                CloseOpenGroups();
                Module.Log?.LogWarning($"The list \"{entry.Definition.Key}\" in the settings window failed ({error.GetType().Name}: " +
                                       $"{error.Message}); it shows as plain text from now on, and can be edited in the settings file. " +
                                       "Its setting still works.");
            }
        }

        private static void CloseOpenGroups()
        {
            while (open.Count > 0)
            {
                try
                {
                    switch (open.Pop())
                    {
                        case Group.Horizontal:
                            GUILayout.EndHorizontal();
                            break;
                        case Group.Vertical:
                            GUILayout.EndVertical();
                            break;
                        default:
                            GUILayout.EndScrollView();
                            break;
                    }
                }
                catch (Exception)
                {
                    // Closing what the error left open is all that's wanted; a failure here changes nothing more.
                }
            }
        }

        private static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(style, options);
            open.Push(Group.Horizontal);
        }

        private static void BeginHorizontal(params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            open.Push(Group.Horizontal);
        }

        private static void EndHorizontal()
        {
            open.Pop();
            GUILayout.EndHorizontal();
        }

        private static void BeginVertical(params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(options);
            open.Push(Group.Vertical);
        }

        private static void EndVertical()
        {
            open.Pop();
            GUILayout.EndVertical();
        }

        private static Vector2 BeginScrollView(Vector2 at, params GUILayoutOption[] options)
        {
            Vector2 now = GUILayout.BeginScrollView(at, options);
            open.Push(Group.Scroll);
            return now;
        }

        private static void EndScrollView()
        {
            open.Pop();
            GUILayout.EndScrollView();
        }

        private static List<KeyValuePair<string, string>> Entries(ConfigEntryBase entry)
        {
            string text = entry.BoxedValue as string ?? string.Empty;
            if (!read.TryGetValue(entry, out (string Text, List<KeyValuePair<string, string>> Entries) known) || known.Text != text)
            {
                known = (text, AmountRules.ReadEntries(text));
                read[entry] = known;
            }
            return known.Entries;
        }

        private static void Write(ConfigEntryBase entry, List<KeyValuePair<string, string>> entries)
        {
            string text = AmountRules.WriteEntries(entries);
            // The file gets the cleaned text; the boxes keep what was typed (a space just typed stays, so "10 20" can be
            // typed: review round 1), for as long as the file holds what was written here.
            read[entry] = (text, entries);
            entry.BoxedValue = text;
        }

        private static int IndexOf(List<KeyValuePair<string, string>> entries, string key) =>
            entries.FindIndex(each => string.Equals(each.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Sets one key's amounts (null: takes the key out), keeping every other entry as written.</summary>
        private static void Set(ConfigEntryBase entry, string key, string amounts)
        {
            var entries = new List<KeyValuePair<string, string>>(Entries(entry));
            int at = IndexOf(entries, key);
            if (amounts == null)
            {
                if (at >= 0)
                {
                    entries.RemoveAt(at);
                }
            }
            else if (at >= 0)
            {
                entries[at] = new KeyValuePair<string, string>(entries[at].Key, amounts);
            }
            else
            {
                entries.Add(new KeyValuePair<string, string>(key, amounts));
            }
            Write(entry, entries);
        }

        private static void Title(string text, string help)
        {
            GUILayout.Label(text, HeadingStyle());
            if (!string.IsNullOrEmpty(help))
            {
                GUILayout.Label(help, HintStyle());
            }
        }

        private static bool NotReady()
        {
            if (KindList.Ready())
            {
                return false;
            }
            GUILayout.Label("The list fills once the game has read its items (at the main menu, or in a world).", HintStyle());
            return true;
        }

        // ---- By item

        private sealed class Row
        {
            internal string Heading;
            internal KindItem Item;
        }

        private static readonly List<Row> rows = new List<Row>();
        private static string rowsFor;
        private static int first;
        private static int last;
        private static string shownFor;
        private static bool letGo;

        private static void DrawItems(ConfigEntryBase entry)
        {
            Title("By item", "Tick an item and type its amounts, a % after a percentage: \"10, 20, 50, 25%\". Empty: its category's or the general ones.");
            if (NotReady())
            {
                return;
            }
            List<KeyValuePair<string, string>> entries = Entries(entry);
            // Unity draws a window twice for every event (its layout, then the event), and both passes must draw the same
            // controls: so the rows and the part in view change only in a layout pass, and a change typed or ticked in the
            // event pass shows from the next one.
            if (Event.current.type == EventType.Layout)
            {
                string ticked = TickedKeys(entries);
                Filter(entries, ticked);
                first = Mathf.Clamp(Mathf.FloorToInt(itemScroll.y / RowHeight), 0, rows.Count);
                last = Mathf.Clamp(first + Mathf.CeilToInt(ItemsHeight / RowHeight) + 2, first, rows.Count);
                // Unity knows a text box by its place among the boxes drawn, not by its item: when the rows in view or
                // the ticks change, the box that has the keyboard would become another item's, so it lets go of it
                // (review round 1), below, once this pass's boxes have their names (review round 2). Typing never
                // changes these, so typing keeps it.
                string shown = first + "|" + last + "|" + rowsFor + "|" + ticked;
                letGo = shown != shownFor;
                shownFor = shown;
            }
            BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(60f));
            search = GUILayout.TextField(search ?? string.Empty, GUILayout.ExpandWidth(true));
            onlyTicked = GUILayout.Toggle(onlyTicked, " Only the ticked ones", GUILayout.Width(170f));
            EndHorizontal();

            itemScroll = BeginScrollView(itemScroll, GUILayout.Height(ItemsHeight));
            GUILayout.Space(first * RowHeight);
            for (int index = first; index < last && index < rows.Count; index++)
            {
                Row row = rows[index];
                BeginHorizontal(GUIStyle.none, GUILayout.Height(RowHeight));   // no margin: every row exactly RowHeight, as the scrolling counts them
                if (row.Heading != null)
                {
                    GUILayout.Label(row.Heading, HeadingStyle(), GUILayout.Height(RowHeight));
                }
                else
                {
                    DrawItemRow(entry, entries, row.Item);
                }
                EndHorizontal();
            }
            GUILayout.Space(Math.Max(0, rows.Count - last) * RowHeight);
            EndScrollView();
            LetGo(false);

            int unknown = 0;
            foreach (KeyValuePair<string, string> each in entries)
            {
                if (!KindList.Knows(each.Key))
                {
                    unknown++;
                }
            }
            string count = entries.Count == 1 ? "1 item set" : entries.Count + " items set";
            if (unknown > 0)
            {
                count += $" ({unknown} not in this game: kept)";
            }
            GUILayout.Label(count + (rows.Count == 0 ? ". Nothing matches." : "."), HintStyle());
        }

        /// <summary>
        /// When the item list's rows under its boxes moved (letGo, from its Layout pass), lets go of the keyboard if one of
        /// our amounts boxes has it: asked at the end of each list's Layout pass, once that list's boxes have their names,
        /// since the categories' and stack sizes' boxes come after the item list's and Unity numbers them all in one run
        /// (review round 2 and its last look). The last list spends the request.
        /// </summary>
        private static void LetGo(bool lastList)
        {
            if (!letGo || Event.current.type != EventType.Layout)
            {
                return;
            }
            if (GUI.GetNameOfFocusedControl().StartsWith(BoxName, StringComparison.Ordinal))
            {
                GUIUtility.keyboardControl = 0;
                letGo = false;
            }
            if (lastList)
            {
                letGo = false;
            }
        }

        private static string TickedKeys(List<KeyValuePair<string, string>> entries)
        {
            var keys = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, string> each in entries)
            {
                keys.Append(each.Key).Append('\n');
            }
            return keys.ToString();
        }

        // The rows the search and "only the ticked ones" leave, under their category's heading.
        private static void Filter(List<KeyValuePair<string, string>> entries, string ticked)
        {
            string filter = (search ?? string.Empty).Trim().ToLowerInvariant();
            string key = filter + "|" + onlyTicked + "|" + (onlyTicked ? ticked : string.Empty) + "|" + KindList.Version;
            if (key == rowsFor)
            {
                return;
            }
            rowsFor = key;
            rows.Clear();
            string lastGroup = null;
            foreach (KindItem item in KindList.Items)
            {
                if ((filter.Length > 0 && !item.Search.Contains(filter)) || (onlyTicked && IndexOf(entries, item.SharedName) < 0))
                {
                    continue;
                }
                string group = string.IsNullOrEmpty(item.Member) ? item.Family : item.Family + " / " + item.Member;
                if (group != lastGroup)
                {
                    lastGroup = group;
                    rows.Add(new Row { Heading = group });
                }
                rows.Add(new Row { Item = item });
            }
        }

        private static void DrawItemRow(ConfigEntryBase entry, List<KeyValuePair<string, string>> entries, KindItem item)
        {
            int at = IndexOf(entries, item.SharedName);
            bool ticked = at >= 0;
            bool now = GUILayout.Toggle(ticked, " " + item.Name, GUILayout.Width(NameWidth), GUILayout.Height(RowHeight));
            if (now != ticked)
            {
                Set(entry, item.SharedName, now ? string.Empty : null);
                return;
            }
            if (ticked)
            {
                string amounts = entries[at].Value;
                GUI.SetNextControlName(BoxName + item.SharedName);
                string typed = GUILayout.TextField(amounts, GUILayout.Width(AmountsWidth), GUILayout.Height(RowHeight - 2f));
                if (typed != amounts)
                {
                    Set(entry, item.SharedName, typed);
                }
            }
            GUILayout.Label("stacks to " + item.StackSize.ToString(CultureInfo.InvariantCulture), HintStyle(), GUILayout.Height(RowHeight));
        }

        // ---- By category

        private static void DrawCategories(ConfigEntryBase entry)
        {
            Title("By category", "The pack's categories and their members, as Add to Cart lists them. A member's amounts win over its category's; empty: not set.");
            if (NotReady())
            {
                return;
            }
            List<KeyValuePair<string, string>> entries = Entries(entry);
            categoryScroll = BeginScrollView(categoryScroll, GUILayout.Height(CategoriesHeight));
            foreach (KindCategory category in KindList.Categories)
            {
                BeginHorizontal(GUIStyle.none, GUILayout.Height(RowHeight));   // no margin: every row exactly RowHeight
                if (category.IsMember)
                {
                    GUILayout.Space(20f);
                }
                string label = category.Label + " (" + category.Count.ToString(CultureInfo.InvariantCulture) + ")";
                GUILayout.Label(label, category.IsMember ? GUI.skin.label : HeadingStyle(),
                                GUILayout.Width(category.IsMember ? NameWidth - 20f : NameWidth), GUILayout.Height(RowHeight));
                AmountsField(entry, entries, category.Key);
                EndHorizontal();
            }
            EndScrollView();
            LetGo(false);
        }

        // ---- By stack size

        private static void DrawStackSizes(ConfigEntryBase entry)
        {
            Title("By stack size", "Every item that stacks to one size. Empty: not set.");
            if (NotReady())
            {
                return;
            }
            List<KeyValuePair<string, string>> entries = Entries(entry);
            foreach (KeyValuePair<int, int> size in KindList.StackSizes)
            {
                BeginHorizontal(GUIStyle.none, GUILayout.Height(RowHeight));   // no margin: every row exactly RowHeight
                string kinds = size.Value == 1 ? "1 kind" : size.Value.ToString(CultureInfo.InvariantCulture) + " kinds";
                GUILayout.Label("Stacks of " + size.Key.ToString(CultureInfo.InvariantCulture) + " (" + kinds + ")",
                                GUILayout.Width(NameWidth), GUILayout.Height(RowHeight));
                AmountsField(entry, entries, size.Key.ToString(CultureInfo.InvariantCulture));
                EndHorizontal();
            }
            LetGo(true);
        }

        // A key's amounts box: typing sets them, emptying it takes the key out.
        private static void AmountsField(ConfigEntryBase entry, List<KeyValuePair<string, string>> entries, string key)
        {
            int at = IndexOf(entries, key);
            string amounts = at >= 0 ? entries[at].Value : string.Empty;
            GUI.SetNextControlName(BoxName + entry.Definition.Key + ":" + key);
            string typed = GUILayout.TextField(amounts, GUILayout.Width(AmountsWidth), GUILayout.Height(RowHeight - 2f));
            if (typed != amounts)
            {
                Set(entry, key, typed.Trim().Length == 0 ? null : typed);
            }
        }

        // Made from the window's own skin, and made again if the window changes its skin.
        private static GUIStyle HeadingStyle()
        {
            if (heading == null || headingFrom != GUI.skin.label)
            {
                headingFrom = GUI.skin.label;
                heading = new GUIStyle(headingFrom) { fontStyle = FontStyle.Bold };
            }
            heading.fontSize = headingFrom.fontSize;   // the window sets its font size for each call
            return heading;
        }

        private static GUIStyle HintStyle()
        {
            if (hint == null || hintFrom != GUI.skin.label)
            {
                hintFrom = GUI.skin.label;
                hint = new GUIStyle(hintFrom) { wordWrap = true, fontStyle = FontStyle.Italic };
            }
            hint.fontSize = hintFrom.fontSize;
            return hint;
        }
    }
}
