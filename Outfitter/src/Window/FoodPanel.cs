using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using Outfitter.Loadouts;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// A food slot's rule, edited beside Outfitter's window (his words, 2026-10-06: "a special 'Food' item mode for loadouts
    /// that lets me specify food stat thresholds for loadout slots ... a 'Food Pool' mode where I can add a slot for food,
    /// and then be able to manually select all the foods that slot could be filled by - very similar to how the selection
    /// works in the 'Chest setup' feature ... a toggle for each slot ... whether the system should try to maximize or
    /// minimize the value"; job 30, Session 3, card A34 decision 72). In the panel's place beside the window (the nearby
    /// items' or his inventory's), for the slot being edited: By its stats (a from and a to for Health, Stamina and Eitr,
    /// empty for any) or From a list (every food of the game, by biome in progression order, ticked), and which food it
    /// prefers (the most or the least of one stat, or of all three). Each change is saved at once, as every edit of the
    /// window is (with Undo). The list shows each food's three values, and in By its stats whether it meets the ranges.
    /// The food the slot would take now comes from the window's preview. Built once with the window, as its child.
    /// </summary>
    internal static class FoodPanel
    {
        private const float Left = 20f;
        private const float ListTop = 300f;
        private const float RowHeight = 34f;

        private static GameObject panel;
        private static Text subtitle;
        private static Button byStatsButton;
        private static Button fromListButton;
        private static readonly InputField[] froms = new InputField[3];
        private static readonly InputField[] tos = new InputField[3];
        private static Button mostButton;
        private static Button leastButton;
        private static Button statFirstButton;
        private static Button amountFirstButton;
        private static Text firstHint;
        private static readonly Button[] statButtons = new Button[4];
        private static InputField search;
        private static readonly Button[] sortButtons = new Button[4];
        private static RectTransform list;
        private static ScrollRect listScroll;
        private static Text nowText;
        private static Text listHint;

        private static readonly List<FoodRow> rows = new List<FoodRow>();
        private static List<ItemDrop.ItemData> foods;
        private static object foodsFor;
        private static string shownQuery = string.Empty;
        private static string shownRule;

        // How the list is sorted (his words, 2026-10-07: "sort the results by each of the health/stamina/eitr values"): 0 by
        // biome, 1 to 3 by Health, Stamina or Eitr (FoodStat + 1), the most first unless pressed again.
        private static int sortBy;
        private static bool leastFirst;
        private static readonly string[] SortNames = { "Biome", "Health", "Stamina", "Eitr" };

        // The foods he has nearby, by their names in the game: his own and the chests Outfitter takes from, read at most
        // every two seconds while the panel shows (his words, 2026-10-07: "only shows me foods I either have available
        // nearby or have personally discovered the recipes for").
        private static HashSet<string> nearby = new HashSet<string>(StringComparer.Ordinal);
        private static float nextNearby;
        private const float NearbyEvery = 2f;

        // What it was last drawn for (review round 1, m9): the slot's item, the window's preview and the filter. Edits,
        // Undo and the preview all make new objects, so the same three mean nothing to draw again.
        private static LoadoutItem keySlot;
        private static object keyPreview;
        private static string keySearch;

        // The slot being edited: its loadout and cell.
        private static string loadoutId;
        private static Vector2i cell;

        /// <summary>A change to the rule, for the window to save (an Undo entry, folded with the ones just before).</summary>
        internal static Action<Vector2i, FoodRule> Changed;

        /// <summary>"Done": back to the panel that showed before. Set by the window.</summary>
        internal static Action Done;

        private sealed class FoodRow
        {
            internal string Prefab;
            internal ItemDrop.ItemData Template;
            internal GameObject Root;
            internal Toggle Tick;
            internal Text Name;
            internal Text Values;
            internal bool IsHeading;
        }

        internal static bool Shown => panel != null && panel.activeSelf;

        /// <summary>The slot it edits, while it shows.</summary>
        internal static bool Edits(string id, Vector2i at) => Shown && loadoutId == id && cell.x == at.x && cell.y == at.y;

        /// <summary>Whether it shows a slot of this loadout.</summary>
        internal static bool EditsLoadout(string id) => Shown && loadoutId == id;

        private static ScrollRect[] wheeled;

        internal static void Build(Transform windowRoot, float x, float y, float height)
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
            }
            rows.Clear();
            foods = null;
            foodsFor = null;
            Redraw();
            GUIManager gui = GUIManager.Instance;
            float width = Drawer.Width;
            panel = gui.CreateWoodpanel(windowRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height, false);
            panel.name = "Outfitter_FoodSlot";
            Ui.Place(panel, x, y, width, height);
            Transform root = panel.transform;
            Ui.Label(root, "Food slot", Left, 12f, 300f, 34f, 22, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Button done = Ui.Button(root, "Done", width - 120f, 16f, 100f, 30f, () => Module.Guard("closing the food slot", () => Done?.Invoke()));
            Ui.Help(done, "Closes the food slot's rule (every change is saved already).");
            subtitle = Ui.Label(root, string.Empty, Left, 48f, width - 2f * Left, 40f, 14, TextAnchor.UpperLeft, Ui.Plain);

            // How it finds its food.
            Ui.Label(root, "Find its food", Left, 92f, 160f, 30f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            byStatsButton = Ui.Button(root, "By its stats", Left + 160f, 92f, 150f, 30f, () => Module.Guard("a food slot's mode", () => Edit(rule => rule.Mode = FoodMode.ByStats)));
            Ui.Help(byStatsButton, "The slot takes any food whose Health, Stamina and Eitr are in the ranges below (an empty box: any).");
            fromListButton = Ui.Button(root, "From a list", Left + 316f, 92f, 150f, 30f, () => Module.Guard("a food slot's mode", () => Edit(rule => rule.Mode = FoodMode.FromList)));
            Ui.Help(fromListButton, "The slot takes one of the foods you tick in the list below (your \"Food Pool\").");

            // The ranges.
            string[] names = { "Health", "Stamina", "Eitr" };
            for (int i = 0; i < 3; i++)
            {
                int stat = i;
                float rowY = 130f + i * 36f;
                Ui.Label(root, names[i], Left, rowY, 90f, 30f, 15, TextAnchor.MiddleLeft, Ui.Plain);
                froms[i] = Field(root, Left + 90f, rowY, value => Module.Guard("a food slot's range", () => SetEnd(stat, from: true, value)));
                Ui.Label(root, "to", Left + 176f, rowY, 30f, 30f, 15, TextAnchor.MiddleCenter, Ui.Quiet);
                tos[i] = Field(root, Left + 206f, rowY, value => Module.Guard("a food slot's range", () => SetEnd(stat, from: false, value)));
            }
            Ui.Label(root, "Empty: any. A food meets the slot when every range given holds.", Left + 300f, 130f, width - Left - 320f, 44f, 13,
                     TextAnchor.UpperLeft, Ui.Quiet);

            // What it puts first (his words, 2026-10-07: "a toggle for each slot, to either have the slot prioritize the amount
            // or to prioritize the stat").
            Ui.Label(root, "First", Left + 300f, 176f, 56f, 30f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            statFirstButton = Ui.Button(root, "Its stat", Left + 356f, 176f, 110f, 30f, () => Module.Guard("a food slot's first", () => Edit(rule => rule.First = FoodFirst.Stat)));
            Ui.Help(statFirstButton, () => $"The food it prefers (Prefer, below) comes first, as long as at least {Settings.FoodStatAtLeast.Value} are nearby (or its whole count, if fewer: F1, Outfitter, \"Food slots by stat: at least\"), even when another food would fill its count: 3 Honey glazed chickens before 10 Cooked deer meats.");
            amountFirstButton = Ui.Button(root, "Amount", Left + 470f, 176f, 110f, 30f, () => Module.Guard("a food slot's first", () => Edit(rule => rule.First = FoodFirst.Amount)));
            Ui.Help(amountFirstButton, "A food that fills its whole count comes first (0.4's way); it prefers by stat only among those.");
            firstHint = Ui.Label(root, string.Empty, Left + 300f, 208f, width - Left - 320f, 30f, 13, TextAnchor.UpperLeft, Ui.Quiet);

            // What it prefers.
            Ui.Label(root, "Prefer", Left, 244f, 90f, 30f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            mostButton = Ui.Button(root, "Most", Left + 90f, 244f, 70f, 30f, () => Module.Guard("a food slot's preference", () => Edit(rule => rule.Prefer = FoodPrefer.Most)));
            Ui.Help(mostButton, "Of the foods nearby that fit, the slot takes the one with the most of the stat on the right (his \"Maximize\").");
            leastButton = Ui.Button(root, "Least", Left + 164f, 244f, 70f, 30f, () => Module.Guard("a food slot's preference", () => Edit(rule => rule.Prefer = FoodPrefer.Least)));
            Ui.Help(leastButton, "Of the foods nearby that fit, the slot takes the one with the least of the stat on the right (his \"Minimize\").");
            string[] statNames = { "Health", "Stamina", "Eitr", "All three" };
            for (int i = 0; i < 4; i++)
            {
                var stat = (FoodStat)i;
                statButtons[i] = Ui.Button(root, statNames[i], Left + 250f + i * 88f, 244f, 84f, 30f, () => Module.Guard("a food slot's preference", () => Edit(rule => rule.Of = stat)));
                Ui.Help(statButtons[i], i == 3 ? "Prefers by Health, Stamina and Eitr added together." : $"Prefers by {statNames[i]}.");
            }

            // Every food of the game, by biome.
            GameObject field = gui.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, InputField.ContentType.Standard,
                                                    "Filter", 15, 160f, 30f);
            Ui.Place(field, Left, ListTop - 2f, 160f, 30f);
            search = field.GetComponent<InputField>();
            search.characterLimit = 40;
            search.lineType = InputField.LineType.SingleLine;
            search.onValueChanged.AddListener(text => Module.Guard("filtering the foods", Redraw));
            Ui.Label(root, "Sort", Left + 170f, ListTop - 2f, 46f, 30f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            for (int i = 0; i < SortNames.Length; i++)
            {
                int by = i;
                sortButtons[i] = Ui.Button(root, SortNames[i], Left + 216f + i * 88f, ListTop - 2f, 84f, 30f,
                                           () => Module.Guard("sorting the foods", () => SortBy(by)));
                Ui.Help(sortButtons[i], i == 0 ? "The foods by biome, in the order you reach them."
                                               : $"The foods by {SortNames[i]}, the most first; press it again for the least first.");
            }
            listHint = Ui.Label(root, string.Empty, Left, ListTop + 32f, width - 2f * Left, 30f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            list = DrawerUi.Scroll("Outfitter_Foods", root, Left, ListTop + 66f, width - 2f * Left, height - ListTop - 66f - 70f, out listScroll);
            DrawerUi.List(list, 2f);
            nowText = Ui.Label(root, string.Empty, Left, height - 64f, width - 2f * Left, 44f, 14, TextAnchor.UpperLeft, Ui.Plain);
            panel.SetActive(false);
        }

        private static InputField Field(Transform root, float x, float y, Action<string> ended)
        {
            GameObject box = GUIManager.Instance.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                  InputField.ContentType.IntegerNumber, "any", 15, 80f, 30f);
            Ui.Place(box, x, y, 80f, 30f);
            InputField input = box.GetComponent<InputField>();
            input.characterLimit = 4;
            input.lineType = InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(text => ended(text));
            return input;
        }

        /// <summary>Shows it for a slot of a loadout.</summary>
        internal static void Open(string id, Vector2i at)
        {
            if (panel == null)
            {
                return;
            }
            loadoutId = id;
            cell = at;
            nextNearby = 0f;   // read what's nearby at once
            Redraw();
            if (!panel.activeSelf)
            {
                panel.SetActive(true);
            }
        }

        internal static void Hide()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
            loadoutId = null;
        }

        /// <summary>The module's undo step, or a new scene.</summary>
        internal static void Forget()
        {
            Hide();
            foods = null;
            foodsFor = null;
            rows.Clear();
            Redraw();
        }

        /// <summary>A cell of a loadout moved (dragged onto another, swapping when both hold something): the panel follows
        /// its slot, not the cell (review round 1, n7).</summary>
        internal static void Moved(Loadout moved, GridShape shape, Vector2i from, Vector2i to)
        {
            if (moved == null || shape == null || !Shown || loadoutId != moved.Id)
            {
                return;
            }
            if (cell.x == from.x && cell.y == from.y)
            {
                cell = to;
            }
            else if (cell.x == to.x && cell.y == to.y && LoadoutEdit.At(moved, shape, from.x, from.y)?.Food != null)
            {
                cell = from;   // the slot it edits was swapped into the cell the other one left
            }
            Redraw();
        }

        // Draw it again at the next refresh.
        private static void Redraw()
        {
            shownRule = null;
            keySlot = null;
            keyPreview = null;
            keySearch = null;
        }

        // A change to a copy of the rule, handed to the window to save.
        private static FoodRule editing;

        private static void Edit(Action<FoodRule> change)
        {
            if (editing == null)
            {
                return;
            }
            FoodRule changed = editing.Clone();
            FoodMode before = changed.Mode;
            change(changed);
            if (changed.Mode != before)
            {
                changed.Of = changed.StartingStat();
            }
            if (!changed.SameAs(editing))
            {
                editing = changed;   // a second change before the next refresh builds on this one
                Changed?.Invoke(cell, changed);
            }
            Redraw();   // and a change that changed nothing (a 65th food ticked) drawn again as it is (review round 1, n6)
        }

        private static void SetEnd(int stat, bool from, string text)
        {
            int? value = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int read))
                {
                    Redraw();   // draw the box again as it was
                    return;
                }
                value = Mathf.Clamp(read, 0, FoodRule.MaxStat);
            }
            Edit(rule =>
            {
                bool first = !rule.Health.Given && !rule.Stamina.Given && !rule.Eitr.Given;
                FoodRange range = rule.RangeOf((FoodStat)stat);
                range = from ? new FoodRange(value, range.To) : new FoodRange(range.From, value);
                rule.SetRange((FoodStat)stat, range);
                if (first && range.Given && rule.Mode == FoodMode.ByStats && rule.Of == FoodStat.All)
                {
                    rule.Of = (FoodStat)stat;   // the first range given: prefer by that stat (the plan's words)
                }
            });
        }

        /// <summary>Every frame the window shows, after its own refresh: the wheel over the list; drawn again when the
        /// slot, the window's preview (<paramref name="previewNow"/>) or the filter changes; hidden when the slot is gone.
        /// <paramref name="nowOf"/> says what the slot would do put on now.</summary>
        internal static void Refresh(Loadout loadout, GridShape shape, object previewNow, Func<LoadoutItem, string> nowOf)
        {
            if (panel == null || !panel.activeSelf)
            {
                return;
            }
            // The list scrolls with the wheel, as the drawer's do (review round 1, m7); read first, one array kept (round 2, n5).
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel != 0f)
            {
                if (wheeled == null || wheeled[0] != listScroll)
                {
                    wheeled = new[] { listScroll };
                }
                DrawerUi.Wheel(wheel, RowHeight * 3f, wheeled);
            }
            float now = Time.unscaledTime;
            if (now >= nextNearby)
            {
                nextNearby = now + NearbyEvery;
                HashSet<string> seen = NearbyFoods();
                if (!seen.SetEquals(nearby))
                {
                    nearby = seen;
                    Redraw();
                }
            }
            LoadoutItem slot = loadout != null && shape != null && loadout.Id == loadoutId ? LoadoutEdit.At(loadout, shape, cell.x, cell.y) : null;
            FoodRule rule = slot?.Food;
            if (rule == null)
            {
                Done?.Invoke();   // the slot went (Undo, Take out, another loadout picked)
                return;
            }
            editing = rule;
            if (ReferenceEquals(slot, keySlot) && ReferenceEquals(previewNow, keyPreview) && search.text == keySearch)
            {
                return;   // nothing new: no strings built (m9)
            }
            keySlot = slot;
            keyPreview = previewNow;
            keySearch = search.text;
            string state = Fingerprint(rule) + "|" + search.text + "|" + slot.Count + "|" + nowOf?.Invoke(slot) + "|" + rule.First + "|" + Settings.FoodStatAtLeast.Value;
            if (state == shownRule)
            {
                return;
            }
            shownRule = state;
            subtitle.text = $"{loadout.Name}: {Capital(rule.Describe(Equip.Messages.FoodName))}, {(rule.First == FoodFirst.Amount ? "a whole count first" : "its stat first")}.";
            Ui.Set(byStatsButton, rule.Mode == FoodMode.ByStats ? Ui.Look.Lit : Ui.Look.On);
            Ui.Set(fromListButton, rule.Mode == FoodMode.FromList ? Ui.Look.Lit : Ui.Look.On);
            for (int i = 0; i < 3; i++)
            {
                FoodRange range = rule.RangeOf((FoodStat)i);
                bool byStats = rule.Mode == FoodMode.ByStats;
                SetField(froms[i], range.From, byStats);
                SetField(tos[i], range.To, byStats);
            }
            Ui.Set(mostButton, rule.Prefer == FoodPrefer.Most ? Ui.Look.Lit : Ui.Look.On);
            Ui.Set(leastButton, rule.Prefer == FoodPrefer.Least ? Ui.Look.Lit : Ui.Look.On);
            Ui.Set(statFirstButton, rule.First == FoodFirst.Stat ? Ui.Look.Lit : Ui.Look.On);
            Ui.Set(amountFirstButton, rule.First == FoodFirst.Amount ? Ui.Look.Lit : Ui.Look.On);
            firstHint.text = rule.First == FoodFirst.Stat
                ? $"Its preferred food, if at least {Mathf.Min(Mathf.Max(1, Settings.FoodStatAtLeast.Value), slot.Count)} are nearby."
                : "A food that fills its count, then its preference.";
            for (int i = 0; i < statButtons.Length; i++)
            {
                Ui.Set(statButtons[i], (int)rule.Of == i ? Ui.Look.Lit : Ui.Look.On);
            }
            ShowList(rule);
            nowText.text = nowOf?.Invoke(slot) ?? string.Empty;
        }

        private static void SetField(InputField field, int? value, bool live)
        {
            if (!field.isFocused)
            {
                string text = value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                if (field.text != text)
                {
                    field.SetTextWithoutNotify(text);
                }
            }
            field.interactable = live;
            CanvasGroup group = field.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = field.gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = live ? 1f : 0.35f;
        }

        // The list: every food of the game by biome, ticked (From a list) or marked by whether it meets the ranges.
        private static void ShowList(FoodRule rule)
        {
            if (foods == null || foodsFor != (object)ObjectDB.instance)
            {
                MakeRows();
            }
            string query = search.text?.Trim() ?? string.Empty;
            bool byList = rule.Mode == FoodMode.FromList;
            int meets = 0, listed = 0;
            FoodRow heading = null;
            bool headingHas = false;
            foreach (FoodRow row in rows)
            {
                if (row.IsHeading)
                {
                    ShowHeading(heading, headingHas);
                    heading = row;
                    headingHas = false;
                    continue;
                }
                // Only a food he has nearby, whose recipe he knows, or that the slot ticks already (so it can be unticked).
                bool shown = (query.Length == 0 || row.Name.text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) && Known(row, rule);
                if (row.Root.activeSelf != shown)
                {
                    row.Root.SetActive(shown);
                }
                if (!shown)
                {
                    continue;
                }
                listed++;
                headingHas = true;
                bool ticked = rule.Foods.Contains(row.Prefab);
                bool fits = FoodFits(rule, row.Template);
                if (fits && !byList)
                {
                    meets++;
                }
                row.Tick.SetIsOnWithoutNotify(byList ? ticked : fits);
                row.Tick.interactable = byList;
                row.Name.color = byList ? (ticked ? Ui.Plain : Ui.Quiet) : (fits ? Ui.Good : Ui.Quiet);
            }
            ShowHeading(heading, headingHas);
            Order();
            for (int i = 0; i < sortButtons.Length; i++)
            {
                Ui.Set(sortButtons[i], i == sortBy ? Ui.Look.Lit : Ui.Look.On);
            }
            string order = sortBy == 0 ? "by biome" : $"by {SortNames[sortBy]}, {(leastFirst ? "the least" : "the most")} first";
            listHint.text = (byList
                ? $"Tick the foods it may take ({rule.Foods.Count} ticked, {FoodRule.MaxFoods} at most). "
                : $"{meets} of these {listed} meet the ranges (green). ") +
                $"Listed: foods you have nearby or know the recipe of, {order}.";
        }

        // A biome's heading shows while a food under it does, and only when the list is by biome.
        private static void ShowHeading(FoodRow heading, bool has)
        {
            bool shown = heading != null && has && sortBy == 0;
            if (heading != null && heading.Root.activeSelf != shown)
            {
                heading.Root.SetActive(shown);
            }
        }

        // Listed: a food he has nearby, one whose recipe he knows (the game's own record of recipes he has found), or one
        // the slot ticks already.
        private static bool Known(FoodRow row, FoodRule rule)
        {
            string name = row.Template.m_shared.m_name;
            Player player = Player.m_localPlayer;
            return rule.Foods.Contains(row.Prefab) || nearby.Contains(name) || (player != null && player.IsRecipeKnown(name));
        }

        // The foods he has nearby: his own, and in the chests Outfitter takes from (its range and "Which chests").
        private static HashSet<string> NearbyFoods()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return seen;
            }
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (FoodIs(item))
                {
                    seen.Add(item.m_shared.m_name);
                }
            }
            foreach (Container chest in ChestFinder.Find(player.transform.position, Settings.ChestRules()))
            {
                Inventory inventory = chest != null ? chest.GetInventory() : null;
                if (inventory == null)
                {
                    continue;
                }
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (FoodIs(item))
                    {
                        seen.Add(item.m_shared.m_name);
                    }
                }
            }
            return seen;
        }

        // A sort button: by biome; or by a stat, the most first, and the same button again for the least first.
        private static void SortBy(int by)
        {
            if (by == sortBy && by != 0)
            {
                leastFirst = !leastFirst;
            }
            else
            {
                sortBy = by;
                leastFirst = false;
            }
            Redraw();
        }

        // The rows in the order chosen: by biome as made (headings between), or by the stat, ties by name.
        private static void Order()
        {
            if (sortBy == 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].Root.transform.SetSiblingIndex(i);
                }
                return;
            }
            var stat = (FoodStat)(sortBy - 1);
            int index = 0;
            foreach (FoodRow row in rows.Where(each => !each.IsHeading)
                                        .OrderBy(each => leastFirst ? Value(each.Template, stat) : -Value(each.Template, stat))
                                        .ThenBy(each => each.Name.text, StringComparer.Ordinal))
            {
                row.Root.transform.SetSiblingIndex(index++);
            }
        }

        private static void MakeRows()
        {
            DrawerUi.Clear(list);
            rows.Clear();
            foods = AllFoods();
            foodsFor = ObjectDB.instance;
            if (!Biomes.Built)
            {
                Biomes.Build(line => Module.Log.LogInfo(line));
            }
            string biome = null;
            foreach (ItemDrop.ItemData food in foods.OrderBy(each => Biomes.Order(Biomes.OfItem(each.m_shared.m_name)))
                                                    .ThenBy(each => Value(each, FoodStat.All))
                                                    .ThenBy(each => Name(each), StringComparer.Ordinal))
            {
                string label = Biomes.Label(Biomes.OfItem(food.m_shared.m_name));
                if (label != biome)
                {
                    biome = label;
                    GameObject headingRow = DrawerUi.Row(list, 26f);
                    Ui.Label(headingRow.transform, label, 6f, 0f, 400f, 26f, 15, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
                    rows.Add(new FoodRow { Prefab = label, Root = headingRow, IsHeading = true });
                }
                rows.Add(MakeRow(food));
            }
        }

        private static FoodRow MakeRow(ItemDrop.ItemData food)
        {
            GameObject row = DrawerUi.Row(list, RowHeight);
            var made = new FoodRow { Template = food, Root = row, Prefab = PrefabOf(food) };
            made.Tick = DrawerUi.Checkbox(row.transform, 6f, 6f, 22f);
            string prefab = made.Prefab;
            made.Tick.onValueChanged.AddListener(on => Module.Guard("ticking a food", () => Edit(rule =>
            {
                rule.Foods.Remove(prefab);
                if (on && rule.Foods.Count < FoodRule.MaxFoods)
                {
                    rule.Foods.Add(prefab);
                }
            })));
            DrawerUi.Icon(row.transform, food.GetIcon(), 36f, 2f, 30f);
            made.Name = Ui.Label(row.transform, Name(food), 72f, 0f, 250f, RowHeight, 15, TextAnchor.MiddleLeft, Ui.Plain);
            made.Values = Ui.Label(row.transform, Values(food), 326f, 0f, 250f, RowHeight, 14, TextAnchor.MiddleLeft, Ui.Quiet);
            return made;
        }

        // Every food in the game: what can be eaten for Health, Stamina or Eitr (FoodPick's own rule).
        private static List<ItemDrop.ItemData> AllFoods()
        {
            var found = new List<ItemDrop.ItemData>();
            if (ObjectDB.instance == null)
            {
                return found;
            }
            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && FoodIs(drop.m_itemData))
                {
                    ItemDrop.ItemData template = drop.m_itemData;
                    if (template.m_dropPrefab == null)
                    {
                        template.m_dropPrefab = prefab;
                    }
                    found.Add(template);
                }
            }
            return found;
        }

        private static string PrefabOf(ItemDrop.ItemData food) => food.m_dropPrefab != null ? food.m_dropPrefab.name : string.Empty;

        private static string Name(ItemDrop.ItemData food) => Equip.Messages.Localize(food.m_shared.m_name);

        private static string Values(ItemDrop.ItemData food) =>
            $"Health {food.m_shared.m_food:0} · Stamina {food.m_shared.m_foodStamina:0} · Eitr {food.m_shared.m_foodEitr:0}";

        private static float Value(ItemDrop.ItemData food, FoodStat stat)
        {
            switch (stat)
            {
                case FoodStat.Health:
                    return food.m_shared.m_food;
                case FoodStat.Stamina:
                    return food.m_shared.m_foodStamina;
                case FoodStat.Eitr:
                    return food.m_shared.m_foodEitr;
                default:
                    return food.m_shared.m_food + food.m_shared.m_foodStamina + food.m_shared.m_foodEitr;
            }
        }

        // ---- the food core's rules (FoodPick): what a food is, and whether one meets the ranges ----

        private static bool FoodIs(ItemDrop.ItemData template) => Equip.FoodPick.IsFood(template);

        // Whether a food is in the rule's ranges (the list aside: By its stats' green).
        private static bool FoodFits(FoodRule rule, ItemDrop.ItemData template)
        {
            FoodRule byStats = rule.Clone();
            byStats.Mode = FoodMode.ByStats;
            return Equip.FoodPick.Meets(byStats, null, template);
        }

        private static string Fingerprint(FoodRule rule) =>
            $"{rule.Mode}/{rule.Health.From}-{rule.Health.To}/{rule.Stamina.From}-{rule.Stamina.To}/{rule.Eitr.From}-{rule.Eitr.To}/{rule.Prefer}/{rule.Of}/{string.Join(",", rule.Foods)}";

        private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
