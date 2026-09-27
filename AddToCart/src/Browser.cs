using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jotunn.Managers;
using QoLMods.Catalogue;
using UnityEngine;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>The window's three tabs.</summary>
    internal enum Tab
    {
        Build,
        Craft,
        Take,
    }

    /// <summary>
    /// The window's left side, one way to browse (the browsing philosophy; PROJECT.md rule 7; the Add to Cart plan, agreed
    /// 2026-09-27): a list on the left picks what shows (All, his lists, then the tab's own categories, kinds or
    /// families), Group makes the headings (Auto follows the pick and names itself), Sort orders inside each heading
    /// (progression first), "First" puts his favorites in a heading of their own, and a line above the results says what
    /// is shown. The rules themselves are the QoL Mods catalogue part's (Browse, Orders); this draws them.
    /// </summary>
    internal static class Browser
    {
        internal const float Left = 22f;
        internal const float TabsY = 50f;
        private const float Top = 90f;
        private const float RailWidth = 214f;
        private const float RailRowHeight = 24f;
        internal const float ResultsX = Left + RailWidth + 8f;
        internal const float ResultsWidth = 594f;
        private const float ControlsY = 126f;
        private const float GridY = 162f;
        internal const float GridBottom = 650f;
        internal const float CellSize = 64f;
        internal const float CellGap = 6f;
        private const int Columns = 8;
        private const float SearchPause = 0.25f;
        // Reading a copy back waits once a frame for the graphics card; more copies in the same frame cost little (review 1).
        private const int GreysPerFrame = 30;

        /// <summary>What one tab shows: the pick on the left, its Group and Sort, which families are open, and which of the
        /// list's headings are folded away.</summary>
        private sealed class View
        {
            internal Selection Pick = Selection.All;
            internal string Group;
            internal string Sort;
            internal readonly HashSet<string> Open = new HashSet<string>();
            internal readonly HashSet<string> Folded = new HashSet<string>();
        }

        private sealed class CellView
        {
            internal Entry Entry;
            internal Image Back;
            internal Thumbnail Picture;
            internal bool Unavailable;
            internal Text Count;
            internal Text Stock;
            internal GameObject Star;
        }

        private static readonly Dictionary<Tab, View> Views = new Dictionary<Tab, View>();
        private static readonly List<CellView> Cells = new List<CellView>();
        private static readonly Dictionary<Tab, Button> TabButtons = new Dictionary<Tab, Button>();
        private static readonly List<string> GroupShown = new List<string>();
        private static readonly List<string> SortShown = new List<string>();

        private static Tab tab = Tab.Build;
        private static string query = string.Empty;
        private static float searchDue = -1f;
        private static bool focusSearch;
        private static object viewsFor;
        private static Orders orders = Orders.Default;
        private static readonly string[] OrdersFrom = new string[5];
        private static bool shownExpandable;
        private static bool shownFirst;
        private static string sortShownId = Browse.Progression;
        private static List<GroupOption> groupOptions = new List<GroupOption>();
        private static List<SortOption> sortOptions = new List<SortOption>();

        private static RectTransform rail;
        private static RectTransform grid;
        private static InputField searchBox;
        private static Text whereText;
        private static Button clearChip;
        private static Button allChip;
        private static Dropdown groupList;
        private static Dropdown sortList;
        private static Toggle firstBox;

        internal static Tab Current => tab;

        /// <summary>The thumbnails' scroll content, for the mouse wheel.</summary>
        internal static RectTransform Grid => grid;

        /// <summary>The list on the left's scroll content, for the mouse wheel.</summary>
        internal static RectTransform Rail => rail;

        internal static bool Searching => searchBox != null && searchBox.isFocused;

        /// <summary>True while the Group or Sort list is open: the wheel then leaves the thumbnails behind it alone.</summary>
        internal static bool ListOpen => Expanded(groupList) || Expanded(sortList);

        // Unity's Dropdown shows its list as a child named "Dropdown List" while open (and for its fade as it closes).
        private static bool Expanded(Dropdown dropdown)
        {
            Transform list = dropdown != null ? dropdown.transform.Find("Dropdown List") : null;
            return list != null && list.gameObject.activeInHierarchy;
        }

        /// <summary>One of his lists was renamed here: every tab that has it picked keeps it.</summary>
        internal static void ListRenamed(int list, string name)
        {
            foreach (View view in Views.Values)
            {
                if (view.Pick.Pick == Pick.List && view.Pick.List == list)
                {
                    view.Pick = Browse.ListPick(list, name);
                }
            }
        }

        // ---- Building, once per window

        internal static void Build(Transform root)
        {
            TabButtons.Clear();
            TabButtons[Tab.Build] = Ui.Button(root, "Build", Left, TabsY, 100f, 32f, () => Module.Guard("switching tabs", () => ShowTab(Tab.Build)));
            TabButtons[Tab.Craft] = Ui.Button(root, "Craft", Left + 104f, TabsY, 100f, 32f, () => Module.Guard("switching tabs", () => ShowTab(Tab.Craft)));
            TabButtons[Tab.Take] = Ui.Button(root, "Take", Left + 208f, TabsY, 100f, 32f, () => Module.Guard("switching tabs", () => ShowTab(Tab.Take)));

            // The list on the left: the filter box (F, as in the game's build menu), then the rows.
            GameObject field = GUIManager.Instance.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                    InputField.ContentType.Standard, "Filter", 16, RailWidth - 30f, 30f);
            Ui.Place(field, Left, Top, RailWidth - 30f, 30f);
            searchBox = field.GetComponent<InputField>();
            searchBox.onValueChanged.AddListener(text => Module.Guard("filtering", () => Typed(text)));
            Ui.Label(root, "F", Left + RailWidth - 26f, Top, 24f, 30f, 14, TextAnchor.MiddleCenter, Ui.Quiet, bold: true);
            rail = Ui.Scroll("List", root, Left, Top + 34f, RailWidth, GridBottom - Top - 34f);
            Ui.List(rail, 0f);

            // Above the results: what is shown and how many, and one click back to everything. A long name shrinks a
            // little rather than wrapping: the box is one line tall (review 1).
            whereText = Ui.Label(root, string.Empty, ResultsX, Top + 2f, ResultsWidth - 254f, 26f, 18, TextAnchor.MiddleLeft, Ui.Quiet);
            whereText.resizeTextForBestFit = true;
            whereText.resizeTextMinSize = 13;
            whereText.resizeTextMaxSize = 18;
            clearChip = Ui.Chip(root, ResultsX + ResultsWidth - 248f, Top, 146f, 28f, () => Module.Guard("clearing the filter", ClearQuery));
            allChip = Ui.Chip(root, ResultsX + ResultsWidth - 98f, Top, 98f, 28f, () => Module.Guard("showing everything", ShowAll));
            SetChip(allChip, "Show All ×");

            // Group, Sort, and First.
            Ui.Label(root, "Group", ResultsX, ControlsY, 46f, 30f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
            groupList = Ui.Dropdown(root, ResultsX + 46f, ControlsY, 210f, 30f, 240f);
            groupList.onValueChanged.AddListener(index => Module.Guard("grouping", () => GroupPicked(index)));
            Ui.Label(root, "Sort", ResultsX + 262f, ControlsY, 38f, 30f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
            sortList = Ui.Dropdown(root, ResultsX + 300f, ControlsY, 176f, 30f, 230f);
            sortList.onValueChanged.AddListener(index => Module.Guard("sorting", () => SortPicked(index)));
            firstBox = Ui.Checkbox(root, ResultsX + 486f, ControlsY + 4f, 22f);
            firstBox.SetIsOnWithoutNotify(Settings.FavoritesFirst.Value);
            firstBox.onValueChanged.AddListener(on => Module.Guard("switching favorites first", () => FirstSwitched(on)));
            Sprite star = Favorites.Star();
            if (star != null)
            {
                Ui.Icon(root, star, ResultsX + 512f, ControlsY + 6f, 18f);
            }
            Text first = Ui.Label(root, star != null ? "First" : "* First", ResultsX + 532f, ControlsY, 60f, 30f, 15, TextAnchor.MiddleLeft, Ui.Plain);
            first.raycastTarget = true;
            first.gameObject.AddComponent<Clicks>().Left = () => firstBox.isOn = !firstBox.isOn;

            grid = Ui.Scroll("Thumbnails", root, ResultsX, GridY, ResultsWidth, GridBottom - GridY);
            Ui.List(grid, 4f);
            GroupShown.Clear();
            SortShown.Clear();
            Cells.Clear();
            // A new window (a new world) starts with an empty filter box: so does the filter.
            query = string.Empty;
            searchDue = -1f;
            focusSearch = false;
        }

        // ---- Every frame, while the window is open

        internal static void Update()
        {
            if (focusSearch)
            {
                // A frame after F was pressed, so the F itself isn't typed into the box.
                focusSearch = false;
                searchBox.Select();
                searchBox.ActivateInputField();
            }
            else if (!Ui.Typing() && !ListsPopup.IsOpen && ZInput.GetKeyDown(KeyCode.F, false))
            {
                focusSearch = true;
            }
            // Grey copies of the pictures, a few a frame (Greys): the cells waiting for one show it once it is made.
            if (Greys.Work(GreysPerFrame) > 0)
            {
                foreach (CellView cell in Cells)
                {
                    cell.Picture.Show(cell.Unavailable);
                }
            }
            if (searchDue >= 0f && Time.time >= searchDue)
            {
                searchDue = -1f;
                // Typing searches the whole tab, as the game's filter does; a pick on the left then narrows it.
                View view = ViewOf(tab);
                if (query.Length > 0 && view.Pick.Pick != Pick.All)
                {
                    view.Pick = Selection.All;
                }
                Render();
                ScrollToTop();
            }
            // A setting changed in a configuration manager while the window is open: the window follows.
            bool defaults = DefaultsChanged();
            if (OrdersChanged() || defaults || shownExpandable != Settings.ExpandableFamilies.Value || shownFirst != Settings.FavoritesFirst.Value)
            {
                firstBox.SetIsOnWithoutNotify(Settings.FavoritesFirst.Value);
                Render();
            }
        }

        private static DefaultGroup? shownGroupDefault;
        private static DefaultSort? shownSortDefault;

        // A new default group or sort applies to every tab at once, as in the mock-up (he changes it to see it); only the
        // one that changed.
        private static bool DefaultsChanged()
        {
            bool group = shownGroupDefault.HasValue && shownGroupDefault.Value != Settings.GroupAtFirst.Value;
            bool sort = shownSortDefault.HasValue && shownSortDefault.Value != Settings.SortAtFirst.Value;
            shownGroupDefault = Settings.GroupAtFirst.Value;
            shownSortDefault = Settings.SortAtFirst.Value;
            if (!group && !sort)
            {
                return false;
            }
            foreach (Tab each in new[] { Tab.Build, Tab.Craft, Tab.Take })
            {
                View view = ViewOf(each);
                View fresh = Fresh(each);
                if (group)
                {
                    view.Group = fresh.Group;
                }
                if (sort)
                {
                    view.Sort = fresh.Sort;
                }
                if (Settings.RememberEachTab.Value)
                {
                    Memory.KeepView(each.ToString(), view.Group, view.Sort);
                }
            }
            return true;
        }

        private static bool OrdersChanged()
        {
            // Every one compared (| not ||), so each remembers its value.
            bool changed = Swap(0, Settings.CategoryOrder.Value) | Swap(1, Settings.MaterialOrder.Value) | Swap(2, Settings.KindOrder.Value) |
                           Swap(3, Settings.StationOrder.Value) | Swap(4, Settings.FamilyOrder.Value);
            if (changed)
            {
                orders = Settings.ListOrders();
            }
            return changed;
        }

        private static bool Swap(int at, string value)
        {
            if (OrdersFrom[at] == value)
            {
                return false;
            }
            OrdersFrom[at] = value;
            return true;
        }

        // ---- The state of each tab

        private static View ViewOf(Tab which)
        {
            // The character, by its profile: the same after a death, which makes a new Player.
            object owner = Game.instance != null && Game.instance.GetPlayerProfile() != null ? Game.instance.GetPlayerProfile() : (object)Player.m_localPlayer;
            if (owner != viewsFor)
            {
                // Another character: its own remembered Group and Sort.
                viewsFor = owner;
                Views.Clear();
            }
            if (!Views.TryGetValue(which, out View view))
            {
                view = Views[which] = Fresh(which);
                if (Settings.RememberEachTab.Value && Memory.View(which.ToString(), out string group, out string sort))
                {
                    view.Group = group;
                    view.Sort = sort;
                }
                // The headings he folded away stay folded, whatever "Remember each tab" says: he folds one to be rid of it.
                view.Folded.UnionWith(Memory.FoldedOf(which.ToString()));
            }
            return view;
        }

        private static View Fresh(Tab which)
        {
            var view = new View
            {
                Group = Settings.GroupAtFirst.Value == DefaultGroup.Biome ? Browse.ByBiome
                    : Settings.GroupAtFirst.Value == DefaultGroup.None ? Browse.None
                    : Browse.Auto,
                Sort = Settings.SortAtFirst.Value == DefaultSort.Name ? Browse.ByName : Browse.Progression,
            };
            if (which == Tab.Build)
            {
                // Building Structures starts open, so Walls and the rest are one click away.
                view.Open.Add(Browse.OpenId(new Selection(Pick.Category, Taxonomy.BuildingParent)));
            }
            return view;
        }

        private static void Keep(View view)
        {
            if (Settings.RememberEachTab.Value)
            {
                Memory.KeepView(tab.ToString(), view.Group, view.Sort);
            }
        }

        private static Shelf ShelfOf(Tab which) => which == Tab.Build ? Shelf.Build : which == Tab.Craft ? Shelf.Craft : Shelf.Take;

        private static string Noun(Tab which, int count) =>
            (which == Tab.Build ? "piece" : which == Tab.Craft ? "recipe" : "item") + (count == 1 ? string.Empty : "s");

        // ---- Changing what is shown

        /// <summary>When the window opens: the remembered view of this tab, drawn afresh.</summary>
        internal static void Opened()
        {
            OrdersChanged();
            ShowTab(tab);
        }

        internal static void ShowTab(Tab to)
        {
            FlushSearch();   // a filter still waiting applies now, on the tab he typed on, and can't undo this
            tab = to;
            foreach (KeyValuePair<Tab, Button> each in TabButtons)
            {
                each.Value.interactable = each.Key != tab;
            }
            View view = ViewOf(tab);
            if (!Settings.RememberEachTab.Value)
            {
                // Every tab opens on the defaults; what is picked and open stays.
                View fresh = Fresh(tab);
                view.Group = fresh.Group;
                view.Sort = fresh.Sort;
            }
            CartWindow.StopPicking();
            Render();
            ScrollToTop();
            CartWindow.ShowLocate();
        }

        /// <summary>Draws the list on the left and the results afresh.</summary>
        internal static void Render()
        {
            if (rail == null || grid == null)
            {
                return;
            }
            FavoritePieceList store = Favorites.Store;
            // A list deleted in the game's build menu moves the later ones down a number: every tab's pick follows its
            // list by name, or goes back to All.
            foreach (Tab each in new[] { Tab.Build, Tab.Craft, Tab.Take })
            {
                View view = ViewOf(each);
                view.Pick = Browse.Resolve(view.Pick, GameLists.ListCount(store), list => GameLists.ListName(store, list));
            }
            RenderRail();
            RenderGrid();
        }

        /// <summary>The Take tab's items changed (the chests' contents).</summary>
        internal static void TakeChanged()
        {
            if (tab == Tab.Take && CartWindow.IsOpen)
            {
                Render();
            }
        }

        // A filter still waiting for him to pause applies at once: typing searches the whole tab (the pick goes back to
        // All), then the click that called this narrows it. True if one was waiting.
        private static bool FlushSearch()
        {
            if (searchDue < 0f)
            {
                return false;
            }
            searchDue = -1f;
            if (query.Length > 0)
            {
                ViewOf(tab).Pick = Selection.All;
            }
            return true;
        }

        private static void Typed(string text)
        {
            query = (text ?? string.Empty).Trim();
            searchDue = Time.time + SearchPause;   // redrawn once he pauses, not at every letter
        }

        private static void ClearQuery()
        {
            searchBox.text = string.Empty;   // calls Typed
            searchDue = -1f;
            query = string.Empty;
            Render();
        }

        private static void ShowAll()
        {
            FlushSearch();
            ViewOf(tab).Pick = Selection.All;
            Render();
            ScrollToTop();
        }

        private static void GroupPicked(int index)
        {
            if (index < 0 || index >= groupOptions.Count)
            {
                return;
            }
            View view = ViewOf(tab);
            view.Group = groupOptions[index].Id;
            Keep(view);
            RenderGrid();
        }

        private static void SortPicked(int index)
        {
            if (index < 0 || index >= sortOptions.Count)
            {
                return;
            }
            View view = ViewOf(tab);
            view.Sort = sortOptions[index].Id;
            Keep(view);
            RenderGrid();
        }

        private static void FirstSwitched(bool on)
        {
            Settings.FavoritesFirst.Value = on;
            RenderGrid();
        }

        private static void ScrollToTop()
        {
            ScrollRect scroll = grid != null ? grid.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        // ---- The list on the left

        private sealed class RailRow
        {
            internal string Label;
            internal Selection Pick;
            internal int Count;
            internal bool Member;
            internal string OpenId;
            internal bool Open;
            internal bool Favorites;
            internal int List = -1;
        }

        private static void RenderRail()
        {
            shownExpandable = Settings.ExpandableFamilies.Value;
            Ui.Clear(rail);
            View view = ViewOf(tab);
            FavoritePieceList store = Favorites.Store;
            List<Listing> listings = CartWindow.EntriesOf(tab).Select(entry => entry.Listing).Where(listing => listing != null).ToList();
            List<Listing> matching = listings.Where(listing => Browse.Matches(listing, query)).ToList();
            bool searching = query.Length > 0;
            Func<Listing, bool> starred = Starred(store);
            Func<Listing, int, bool> inList = InList(store);
            int Count(Selection pick) => matching.Count(listing => Browse.InPick(listing, pick, starred, inList));

            Row(new RailRow { Label = "All", Pick = Selection.All, Count = Count(Selection.All) }, view);
            if (Title(YourLists, view))
            {
                if (store != null)
                {
                    var favorites = new Selection(Pick.Favorites);
                    Row(new RailRow { Label = "Favorites", Pick = favorites, Count = Count(favorites), Favorites = true }, view);
                    for (int list = 0; list < GameLists.ListCount(store); list++)
                    {
                        Selection pick = Browse.ListPick(list, GameLists.ListName(store, list));
                        Row(new RailRow { Label = GameLists.ListName(store, list), Pick = pick, Count = Count(pick), List = list }, view);
                    }
                    AddListRow();
                }
                else
                {
                    Note("Your favorites and lists appear once your character is in a world.");
                }
            }
            // No dead ends: only what has something in it, as the game hides empty categories (the part's rule).
            foreach (KeyValuePair<string, List<ShownRow>> block in Browse.Shown(Browse.Tree(ShelfOf(tab), listings, orders), Count, searching,
                                                                                shownExpandable, view.Open))
            {
                if (!Title(block.Key, view))
                {
                    continue;
                }
                foreach (ShownRow row in block.Value)
                {
                    Row(new RailRow { Label = row.Row.Label, Pick = row.Row.Pick, Count = row.Count, Member = row.Member, OpenId = row.OpenId, Open = row.Open }, view);
                }
            }
        }

        private const string YourLists = "Your Lists";

        private static Func<Listing, bool> Starred(FavoritePieceList store) => listing => GameLists.IsStarred(store, listing.FavoriteKey);

        private static Func<Listing, int, bool> InList(FavoritePieceList store) => (listing, list) => GameLists.InList(store, listing.FavoriteKey, list);

        // A heading of the list on the left, in the game's orange: a click folds its rows away, or brings them back (his
        // call on 0.3.0). Folded headings are kept with the character, per tab. True while its rows show.
        private static bool Title(string title, View view)
        {
            bool open = !view.Folded.Contains(title);
            Clicks clicks = Ui.ListRow(rail, RailRowHeight, out Image back);
            Arrow(clicks.transform, open, Ui.Orange);
            Ui.Label(clicks.transform, title.ToUpperInvariant(), 18f, 0f, RailWidth - 12f - 8f - 18f, RailRowHeight, 13, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            clicks.Enter = () => back.color = Ui.RowHover;
            clicks.Exit = () => back.color = Ui.RowPlain;
            clicks.Left = () => TitleClicked(title);
            return open;
        }

        private static void TitleClicked(string title)
        {
            bool flushed = FlushSearch();   // a filter still waiting applies now, as with any click on the list
            View view = ViewOf(tab);
            if (!view.Folded.Remove(title))
            {
                view.Folded.Add(title);
            }
            Memory.KeepFolded(tab.ToString(), view.Folded);
            if (flushed)
            {
                Render();
                ScrollToTop();
            }
            else
            {
                RenderRail();
            }
        }

        // The game's own dropdown arrow (its map marker), pointing right when closed and down when open; "+" and "−" if
        // the game ever lacks it.
        private static GameObject Arrow(Transform row, bool open, Color color)
        {
            Sprite arrow = GUIManager.Instance.GetSprite("map_marker");
            if (arrow != null)
            {
                Image image = Ui.Icon(row, arrow, 3f, 6f, 12f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = new Vector2(9f, -RailRowHeight / 2f);
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, open ? 180f : -90f);
                image.color = color;
                return image.gameObject;
            }
            return Ui.Label(row, open ? "−" : "+", 0f, 0f, 18f, RailRowHeight, 15, TextAnchor.MiddleCenter, color, bold: true).gameObject;
        }

        private static void Note(string text)
        {
            GameObject row = Ui.Row(rail, 40f);
            Ui.Label(row.transform, text, 6f, 0f, RailWidth - 30f, 40f, 12, TextAnchor.MiddleLeft, Ui.Quiet);
        }

        private static void Row(RailRow row, View view)
        {
            bool picked = view.Pick.Equals(row.Pick);
            Clicks clicks = Ui.ListRow(rail, RailRowHeight, out Image back);
            back.color = picked ? Ui.RowPicked : Ui.RowPlain;
            float x = row.Member ? 30f : 18f;
            Color ink = row.Count == 0 ? Ui.Faint : Ui.Plain;
            if (row.OpenId != null)
            {
                // The twisty opens and closes the family without picking it.
                GameObject twisty = Arrow(clicks.transform, row.Open, Ui.Quiet);
                twisty.GetComponent<Graphic>().raycastTarget = true;
                twisty.AddComponent<Clicks>().Left = () => RowClicked(row, onTwisty: true);
            }
            if (row.Favorites)
            {
                Sprite star = Favorites.Star();
                if (star != null)
                {
                    Ui.Icon(clicks.transform, star, x, 4f, 16f);
                    x += 20f;
                }
            }
            // One line: a long name ("Food Preparation Table") shrinks a little rather than wrapping (his 0.2.0 note); at
            // 11 points two lines can't fit the row, so it never wraps.
            Text label = Ui.Label(clicks.transform, row.Label, x, 0f, RailWidth - 12f - 8f - x - 40f, RailRowHeight, row.Member ? 14 : 15, TextAnchor.MiddleLeft, ink);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = row.Member ? 14 : 15;
            Ui.Label(clicks.transform, row.Count.ToString(), RailWidth - 12f - 8f - 40f, 0f, 36f, RailRowHeight, 12, TextAnchor.MiddleRight,
                     picked ? Ui.Plain : Ui.Quiet);
            clicks.Enter = () => back.color = picked ? Ui.RowPicked : Ui.RowHover;
            clicks.Exit = () => back.color = picked ? Ui.RowPicked : Ui.RowPlain;
            clicks.Left = () => RowClicked(row, onTwisty: false);
            if (row.List >= 0)
            {
                int list = row.List;
                clicks.Right = () => ListsPopup.Rename(list);
            }
        }

        private static void AddListRow()
        {
            Clicks clicks = Ui.ListRow(rail, RailRowHeight, out Image back);
            Text label = Ui.Label(clicks.transform, "Add List…", 18f, 0f, RailWidth - 60f, RailRowHeight, 14, TextAnchor.MiddleLeft, Ui.Quiet);
            label.fontStyle = FontStyle.Italic;
            clicks.Enter = () => back.color = Ui.RowHover;
            clicks.Exit = () => back.color = Ui.RowPlain;
            clicks.Left = () => ListsPopup.AddList(null);
        }

        // Clicking a family picks it and opens it; clicking it again closes it; the twisty only opens and closes.
        private static void RowClicked(RailRow row, bool onTwisty)
        {
            bool flushed = FlushSearch();   // a filter still waiting applies now, and can't undo this pick
            View view = ViewOf(tab);
            if (row.OpenId != null && (onTwisty || view.Pick.Equals(row.Pick)))
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
            if (!onTwisty)
            {
                view.Pick = row.Pick;
                Render();
                ScrollToTop();
            }
            else if (flushed)
            {
                Render();   // the filter that just applied changes the results too
                ScrollToTop();
            }
            else
            {
                RenderRail();
            }
        }

        // ---- The results: the line above them, Group, Sort, and the thumbnails under their headings

        private static void RenderGrid()
        {
            shownFirst = Settings.FavoritesFirst.Value;
            Ui.Clear(grid);
            Cells.Clear();
            View view = ViewOf(tab);
            FavoritePieceList store = Favorites.Store;
            List<Entry> entries = CartWindow.EntriesOf(tab).Where(entry => entry.Listing != null).ToList();
            Func<Listing, bool> starred = Starred(store);
            List<Entry> shown = entries.Where(entry => Browse.InPick(entry.Listing, view.Pick, starred, InList(store)) && Browse.Matches(entry.Listing, query)).ToList();
            List<Listing> rows = shown.Select(entry => entry.Listing).ToList();
            var byKey = new Dictionary<string, Entry>();
            foreach (Entry entry in shown)
            {
                byKey[entry.Listing.Key] = entry;
            }

            groupOptions = Browse.GroupOptions(ShelfOf(tab), view.Pick, rows, orders);
            Dictionary<string, Func<Listing, double>> numbers = Numbers(byKey);
            sortOptions = Browse.SortOptions(rows, Extras(rows, numbers));
            GroupOption group = groupOptions.FirstOrDefault(option => option.Id == view.Group) ?? groupOptions[0];
            string sortId = sortOptions.Any(option => option.Id == view.Sort) ? view.Sort : Browse.Progression;
            sortShownId = sortId;
            Ui.SetChoices(groupList, GroupShown, groupOptions.Select(option => option.Label).ToList(), groupOptions.IndexOf(group));
            Ui.SetChoices(sortList, SortShown, sortOptions.Select(option => option.Label).ToList(), sortOptions.FindIndex(option => option.Id == sortId));

            Comparison<Listing> order = Browse.Sorter(sortId, numbers.TryGetValue(sortId, out Func<Listing, double> number) ? number : null);
            string whole = Browse.Label(view.Pick, list => GameLists.ListName(store, list));
            List<Listing> rest = rows;
            int total = rows.Count;
            if (Settings.FavoritesFirst.Value && view.Pick.Pick != Pick.Favorites && view.Pick.Pick != Pick.List && store != null)
            {
                List<Listing> favorites = rows.Where(starred).ToList();
                if (favorites.Count > 0)
                {
                    favorites = Browse.Sorted(favorites, order);
                    Section("Favorites", favorites.Count, favorites.Select(listing => byKey[listing.Key]), star: true);
                    rest = rows.Where(listing => !starred(listing)).ToList();
                }
            }
            foreach (KeyValuePair<string, List<Listing>> section in Browse.Sections(rest, group.Dimension, whole, order))
            {
                Section(section.Key, section.Value.Count, section.Value.Select(listing => byKey[listing.Key]),
                        star: view.Pick.Pick == Pick.Favorites && group.Id == Browse.None);
            }
            if (total == 0)
            {
                string text = tab == Tab.Take && entries.Count == 0 ? "The chests in range hold nothing you may take."
                    : $"Nothing here{(query.Length > 0 ? $" for \"{query}\"" : string.Empty)}." +
                      (view.Pick.Pick == Pick.List || view.Pick.Pick == Pick.Favorites ? " Middle-click a thumbnail on any tab to star it or put it in a list." : string.Empty);
                if (query.Length > 0 && view.Pick.Pick != Pick.All)
                {
                    // A pick on the left narrowed the search to nothing: say what All has (review 1).
                    int elsewhere = entries.Count(entry => Browse.Matches(entry.Listing, query));
                    if (elsewhere > 0)
                    {
                        text += $" All has {elsewhere} {Noun(tab, elsewhere)} for it (Show All ×).";
                    }
                    else if (tab == Tab.Take)
                    {
                        text += " " + CartWindow.NotOnTake(query);   // nothing under All either: why (review 2)
                    }
                }
                else if (tab == Tab.Take && query.Length > 0)
                {
                    // Where a thing of that name is, when the cart leaves it out (his 0.3.0 test: a Shovel in a chest).
                    text += " " + CartWindow.NotOnTake(query);
                }
                GameObject empty = Ui.Row(grid, 130f);
                Ui.Label(empty.transform, text, 8f, 0f, ResultsWidth - 40f, 130f, 15, TextAnchor.UpperLeft, Ui.Quiet);
            }

            // What is shown and how many; Group and Sort say the rest (his call on 0.3.0).
            whereText.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(Ui.Plain)}>{whole}</color></b>  {total} {Noun(tab, total)}";
            clearChip.gameObject.SetActive(query.Length > 0);
            if (query.Length > 0)
            {
                SetChip(clearChip, $"\"{(query.Length > 12 ? query.Substring(0, 12) + "…" : query)}\" ×");
            }
            allChip.gameObject.SetActive(view.Pick.Pick != Pick.All);
            RefreshCells();
        }

        // The window's own sorts: how many (the chests' stock on Take, how many he could make on Build and Craft), and
        // his fetch history.
        private static Dictionary<string, Func<Listing, double>> Numbers(Dictionary<string, Entry> byKey)
        {
            Func<Entry, double> count = CartWindow.Counter();
            return new Dictionary<string, Func<Listing, double>>
            {
                { "count", listing => byKey.TryGetValue(listing.Key, out Entry entry) ? count(entry) : 0 },
                { "recent", listing => Memory.Of(listing.Key).Last },
                { "frequent", listing => Memory.Of(listing.Key).Count },
            };
        }

        // Offered only when they would change something: some of what is shown differs by them.
        private static IEnumerable<SortOption> Extras(List<Listing> rows, Dictionary<string, Func<Listing, double>> numbers)
        {
            bool Differs(string id) => rows.Select(numbers[id]).Distinct().Skip(1).Any();
            if (Differs("count"))
            {
                yield return new SortOption { Id = "count", Label = tab == Tab.Take ? "Count (Chests)" : "Count (Can Make)" };
            }
            if (Memory.AnyHistory && Differs("recent"))
            {
                yield return new SortOption { Id = "recent", Label = "Recent (Your Fetches)" };
            }
            if (Memory.AnyHistory && Differs("frequent"))
            {
                yield return new SortOption { Id = "frequent", Label = "Frequent (Your Fetches)" };
            }
        }

        private static void SetChip(Button chip, string text)
        {
            Text label = chip.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void Section(string heading, int count, IEnumerable<Entry> entries, bool star = false)
        {
            GameObject title = Ui.Row(grid, 26f);
            float x = 4f;
            if (star && Favorites.Star() != null)
            {
                Ui.Icon(title.transform, Favorites.Star(), x, 5f, 18f);
                x += 22f;
            }
            string quiet = ColorUtility.ToHtmlStringRGB(Ui.Quiet);
            Ui.Label(title.transform, $"{heading}  <size=12><color=#{quiet}>{count}</color></size>", x, 0f, ResultsWidth - 40f - x, 26f, 16,
                     TextAnchor.LowerLeft, Ui.Orange, bold: true);
            List<Entry> list = entries.ToList();
            int rows = (list.Count + Columns - 1) / Columns;
            GameObject block = Ui.Row(grid, rows * CellSize + Math.Max(0, rows - 1) * CellGap);
            GridLayoutGroup layout = block.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CellSize, CellSize);
            layout.spacing = new Vector2(CellGap, CellGap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            foreach (Entry entry in list)
            {
                Cell(block.transform, entry);
            }
        }

        private static void Cell(Transform block, Entry entry)
        {
            GameObject cell = Ui.Box("Cell", block, 0f, 0f, CellSize, CellSize, Ui.Cell);
            var view = new CellView
            {
                Entry = entry,
                Back = cell.GetComponent<Image>(),
                Picture = Ui.Thumbnail(cell.transform, entry.Icon, 6f, 6f, CellSize - 12f),
                Count = Ui.Label(cell.transform, string.Empty, 2f, CellSize - 24f, CellSize - 6f, 22f, 16, TextAnchor.LowerRight, Ui.Orange, bold: true),
                Stock = Ui.Label(cell.transform, string.Empty, 3f, CellSize - 22f, CellSize - 6f, 20f, 13, TextAnchor.LowerLeft, Ui.Plain),
            };
            // Top left: the food value sorted by, a piece's comfort, what a recipe makes, a Take item's level.
            string corner = FoodCorner(entry.Listing);
            Color cornerColor = Ui.Plain;
            if (corner == null)
            {
                corner = entry.Comfort > 0 ? entry.Comfort.ToString()
                    : entry.Makes > 1 ? $"×{entry.Makes}"
                    : entry.Quality > 1 ? entry.Quality.ToString()
                    : null;
                cornerColor = entry.Comfort > 0 ? Ui.Comfort : Ui.Plain;
            }
            if (corner != null)
            {
                Ui.Label(cell.transform, corner, 3f, 1f, CellSize - 6f, 18f, 13, TextAnchor.UpperLeft, cornerColor, bold: entry.Comfort > 0);
            }
            // Top right: the favorite star, the build menu's own when it can be found.
            Sprite starSprite = Favorites.Star();
            view.Star = starSprite != null
                ? Ui.Icon(cell.transform, starSprite, CellSize - 19f, 2f, 17f).gameObject
                : Ui.Label(cell.transform, "*", CellSize - 18f, -2f, 16f, 20f, 20, TextAnchor.UpperRight, Ui.Gold, bold: true).gameObject;
            Clicks clicks = cell.AddComponent<Clicks>();
            clicks.Left = () => CartWindow.LeftClick(entry);
            clicks.Right = () => CartWindow.RightClick(entry);
            clicks.Middle = () => ListsPopup.Open(entry);
            clicks.Enter = () => CartWindow.ShowHover(entry);
            clicks.Exit = CartWindow.ClearHover;
            Cells.Add(view);
        }

        // With a food sort on, each food shows the number it is sorted by (his call: "a tile then shows that number").
        private static string FoodCorner(Listing listing)
        {
            if (listing == null)
            {
                return null;
            }
            switch (sortShownId)
            {
                case "health":
                    return listing.Health > 0f ? Mathf.RoundToInt(listing.Health).ToString() : null;
                case "stamina":
                    return listing.Stamina > 0f ? Mathf.RoundToInt(listing.Stamina).ToString() : null;
                case "eitr":
                    return listing.Eitr > 0f ? Mathf.RoundToInt(listing.Eitr).ToString() : null;
                case "duration":
                    return listing.Duration > 0f ? $"{Mathf.RoundToInt(listing.Duration / 60f)}m" : null;
                default:
                    return null;
            }
        }

        /// <summary>What each thumbnail shows that changes with the cart and the chests: greyed, in the cart, stock, star.</summary>
        internal static void RefreshCells()
        {
            if (Cells.Count == 0)
            {
                return;
            }
            FavoritePieceList store = Favorites.Store;
            Func<Entry, bool> greyed = CartWindow.Greying();
            foreach (CellView cell in Cells)
            {
                int count = CartWindow.CountOf(cell.Entry);
                cell.Back.color = count > 0 ? Ui.CellInCart : Ui.Cell;
                cell.Unavailable = greyed(cell.Entry);
                cell.Picture.Show(cell.Unavailable);
                cell.Count.text = count > 0 ? count.ToString() : string.Empty;
                cell.Stock.text = cell.Entry.Literal ? CartWindow.Short(CartWindow.StockOf(cell.Entry.Costs[0].Item)) : string.Empty;
                cell.Star.SetActive(Favorites.IsFavorite(store, cell.Entry));
            }
        }
    }
}
