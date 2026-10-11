using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using QoLMods.Catalogue;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// The nearby items drawer (job 30, Session 2; his words of 2026-10-06): a panel beside Outfitter's window, Add to
    /// Cart's Take tab ported. It lists every item in the chests Outfitter's equip takes from (its range and "Which
    /// chests"), and with "The drawer lists what I carry" what he carries too, one thumbnail per kind as a loadout counts
    /// it (prefab and quality), with how many the chests and he hold. He browses it as Add to Cart's Take tab (the list on
    /// the left, Group, Sort, his favourites first, the filter: F) and drags a thumbnail onto a cell of the loadout
    /// (Drag); a click goes to the window, which decides what it does (Drag.Clicked; a left click only while "Left click
    /// on a drawer item" isn't Nothing). Built once with the window, as its child; the
    /// window shows and hides it and calls Refresh every frame it shows. The chests are read at once when it shows and
    /// at most once a second after; the thumbnails are made again only when the kinds change (never during a drag:
    /// DragSource ends a drag whose thumbnail goes), and otherwise only their counts change. DrawerItems holds the rules.
    /// </summary>
    internal static class Drawer
    {
        /// <summary>Its size at Small, beside the window's 900 by 760 (as tall as the window: job 30, Session 3).</summary>
        internal const float Width = 640f;

        internal const float Height = LoadoutWindow.Height;

        private const float Left = 20f;
        private const float Top = 54f;
        private const float RailWidth = 170f;
        private const float RailRowHeight = 24f;
        private const float ResultsX = Left + RailWidth + 8f;
        private const float ResultsWidth = Width - ResultsX - 20f;
        private const float ControlsY = 90f;
        private const float GridY = 126f;
        private const float GridBottom = 670f;
        private const float CellSize = 60f;
        private const float CellGap = 6f;
        private const int Columns = 6;

        /// <summary>How long after the last letter typed the filter applies (Add to Cart's pause).</summary>
        private const float SearchPause = 0.25f;

        /// <summary>How often, at most, the chests are read again while it shows.</summary>
        private const float ScanEvery = 1f;

        /// <summary>How far a notch of the wheel scrolls, in rows of thumbnails (Add to Cart's default).</summary>
        private const float RowsPerNotch = 2f;

        private static readonly Func<string, bool> Favourite = name => Favorites.List.Has(name);
        private static readonly string PlainHex = ColorUtility.ToHtmlStringRGB(Ui.Plain);
        private static readonly string QuietHex = ColorUtility.ToHtmlStringRGB(Ui.Quiet);

        /// <summary>A thumbnail: its kind, and what changes on it between looks.</summary>
        private sealed class Thumb
        {
            internal string Key;
            internal Text Count;
            internal GameObject Star;
            internal int CountShown = -1;

            /// <summary>Its picture, greyed while there's nothing to take (only what "leave one" keeps).</summary>
            internal Image Icon;

            internal bool Grey;
        }

        private static GameObject panel;
        private static RectTransform rail;
        private static RectTransform grid;
        private static ScrollRect gridScroll;
        private static ScrollRect[] scrolls = new ScrollRect[0];
        private static InputField searchBox;
        private static Text whereText;
        private static Button clearChip;
        private static Button allChip;
        private static Dropdown groupList;
        private static Dropdown sortList;
        private static Toggle firstBox;
        private static Image hoverIcon;
        private static Text hoverName;
        private static Text hoverWhere;
        private static Text chestsText;

        private static readonly List<Thumb> Thumbs = new List<Thumb>();
        private static readonly List<string> GroupShown = new List<string>();
        private static readonly List<string> SortShown = new List<string>();
        private static List<GroupOption> groupOptions = new List<GroupOption>();
        private static List<SortOption> sortOptions = new List<SortOption>();
        private static string sortShownId = Browse.Progression;

        private static DrawerSurvey survey = new DrawerSurvey();
        private static string shownKeys;
        private static bool renderDue;
        private static bool scanDue;
        private static float nextScan;
        private static int shownSettings = -1;
        private static float searchDue = -1f;
        private static bool focusSearch;
        private static string hovered;
        private static bool hoveredOwn;
        private static object viewFor;
        private static DrawerView view;

        /// <summary>Whether it shows (with the window): what the window's toggle reads, and what "as I left it" keeps.</summary>
        internal static bool Shown => panel != null && panel.activeSelf;

        /// <summary>Builds it, hidden, as a child of the window, its top-left corner at (<paramref name="x"/>,
        /// <paramref name="y"/>) from the window's top-left corner, y downwards. Once, with the window (a new window,
        /// in a new world, builds it again).</summary>
        internal static void Build(Transform windowRoot, float x, float y)
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
            }
            Thumbs.Clear();
            GroupShown.Clear();
            SortShown.Clear();
            survey = new DrawerSurvey();
            shownKeys = null;
            renderDue = scanDue = focusSearch = false;
            searchDue = -1f;
            hovered = null;
            shownSettings = -1;

            GUIManager gui = GUIManager.Instance;
            panel = gui.CreateWoodpanel(windowRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Width, Height, false);
            panel.name = "Outfitter_Drawer";
            Ui.Place(panel, x, y, Width, Height);
            Transform root = panel.transform;
            Ui.Label(root, "Nearby items", Left, 12f, 300f, 34f, 22, TextAnchor.MiddleLeft, Ui.Orange, bold: true);

            // The list on the left: the filter box (F, as in Add to Cart and the game's build menu), then the rows.
            GameObject field = gui.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, InputField.ContentType.Standard,
                                                    "Filter", 16, RailWidth - 28f, 30f);
            Ui.Place(field, Left, Top, RailWidth - 28f, 30f);
            searchBox = field.GetComponent<InputField>();
            searchBox.characterLimit = 40;
            searchBox.lineType = InputField.LineType.SingleLine;
            searchBox.onValueChanged.AddListener(text => Module.Guard("filtering the nearby items", () => Typed(text)));
            Ui.Label(root, "F", Left + RailWidth - 26f, Top, 24f, 30f, 14, TextAnchor.MiddleCenter, Ui.Quiet, bold: true);
            rail = DrawerUi.Scroll("Outfitter_DrawerList", root, Left, Top + 34f, RailWidth, GridBottom - Top - 34f, out ScrollRect railScroll);
            DrawerUi.List(rail, 0f);

            // Above the results: what is shown and how many, the filter's chip, and one click back to everything. A long
            // name shrinks a little rather than wrapping.
            whereText = Ui.Label(root, string.Empty, ResultsX, Top + 2f, ResultsWidth - 200f, 26f, 18, TextAnchor.MiddleLeft, Ui.Quiet);
            whereText.supportRichText = true;   // its words are the catalogue's and the drawer's own, never typed
            whereText.resizeTextForBestFit = true;
            whereText.resizeTextMinSize = 12;
            whereText.resizeTextMaxSize = 18;
            clearChip = DrawerUi.Chip(root, ResultsX + ResultsWidth - 194f, Top, 104f, 28f, () => Module.Guard("clearing the drawer's filter", ClearQuery));
            allChip = DrawerUi.Chip(root, ResultsX + ResultsWidth - 86f, Top, 86f, 28f, () => Module.Guard("showing every nearby item", ShowAll));
            DrawerUi.SetChip(allChip, "Show All ×");
            clearChip.gameObject.SetActive(false);
            allChip.gameObject.SetActive(false);

            // Group, Sort, and his favourites first.
            Ui.Label(root, "Group", ResultsX, ControlsY, 46f, 30f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
            groupList = DrawerUi.Dropdown(root, ResultsX + 46f, ControlsY, 128f, 30f, 220f);
            groupList.onValueChanged.AddListener(index => Module.Guard("grouping the nearby items", () => GroupPicked(index)));
            Ui.Label(root, "Sort", ResultsX + 182f, ControlsY, 36f, 30f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
            sortList = DrawerUi.Dropdown(root, ResultsX + 218f, ControlsY, 110f, 30f, 200f);
            sortList.onValueChanged.AddListener(index => Module.Guard("sorting the nearby items", () => SortPicked(index)));
            firstBox = DrawerUi.Checkbox(root, ResultsX + 336f, ControlsY + 4f, 22f);
            firstBox.SetIsOnWithoutNotify(true);
            firstBox.onValueChanged.AddListener(on => Module.Guard("switching favourites first", () => FirstSwitched(on)));
            Sprite star = DrawerUi.Star();
            float firstX = ResultsX + 362f;
            if (star != null)
            {
                DrawerUi.Icon(root, star, firstX, ControlsY + 6f, 18f);
                firstX += 20f;
            }
            Text first = Ui.Label(root, star != null ? "First" : "* First", firstX, ControlsY, ResultsX + ResultsWidth - firstX, 30f, 15,
                                  TextAnchor.MiddleLeft, Ui.Plain);
            first.raycastTarget = true;
            Clicks firstClicks = first.gameObject.AddComponent<Clicks>();
            firstClicks.Lights = false;
            firstClicks.OnLeft = () => firstBox.isOn = !firstBox.isOn;

            grid = DrawerUi.Scroll("Outfitter_DrawerItems", root, ResultsX, GridY, ResultsWidth, GridBottom - GridY, out gridScroll);
            DrawerUi.List(grid, 4f);
            scrolls = new[] { gridScroll, railScroll };

            // Under the results: the thumbnail under the pointer (its picture, what it is, how many where), and what was
            // looked at.
            hoverIcon = DrawerUi.Icon(root, null, Left, GridBottom + 6f, 40f);
            hoverName = Ui.Label(root, string.Empty, Left + 48f, GridBottom + 4f, Width - 2f * Left - 48f, 22f, 15, TextAnchor.MiddleLeft, Ui.Plain);
            hoverName.resizeTextForBestFit = true;
            hoverName.resizeTextMinSize = 11;
            hoverName.resizeTextMaxSize = 15;
            hoverWhere = Ui.Label(root, string.Empty, Left + 48f, GridBottom + 26f, Width - 2f * Left - 48f, 20f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            chestsText = Ui.Label(root, string.Empty, Left, GridBottom + 52f, Width - 2f * Left, 22f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            chestsText.resizeTextForBestFit = true;   // the line grows with "one of each stays" and set-up chests only
            chestsText.resizeTextMinSize = 10;
            chestsText.resizeTextMaxSize = 13;

            // A new window starts with an empty filter box: so does the filter (Add to Cart's rule).
            ViewNow().Query = string.Empty;
            panel.SetActive(false);
        }

        /// <summary>Shows or hides it. Shown, it reads the chests at once and is drawn afresh; with "Nearby items drawer"
        /// off it stays hidden.</summary>
        internal static void SetShown(bool shown)
        {
            if (panel == null)
            {
                return;
            }
            shown &= Settings.Drawer.Value;
            if (panel.activeSelf == shown)
            {
                scanDue |= shown;   // shown again with the window: read the chests now, not up to a second later
                return;
            }
            panel.SetActive(shown);
            hovered = null;
            focusSearch = false;
            if (!shown)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                scanDue = true;
                return;
            }
            Scan(player, Time.unscaledTime);
            Render();
            ScrollToTop();
        }

        /// <summary>
        /// Every frame the window shows: F (the filter), the wheel, the filter once he pauses, and the chests read again
        /// when a second has passed or a setting changed. Nothing is made or read while nothing is due.
        /// </summary>
        internal static void Refresh(Player player)
        {
            if (panel == null || !panel.activeSelf || player == null || Popup.Showing)
            {
                return;
            }
            if (!Settings.Drawer.Value)
            {
                SetShown(false);
                return;
            }
            Keys();
            float now = Time.unscaledTime;
            if (searchDue >= 0f && now >= searchDue)
            {
                searchDue = -1f;
                // Typing searches every item, as the game's filter does; a pick on the left then narrows it.
                DrawerView shownView = ViewNow();
                if (shownView.Query.Length > 0 && shownView.Pick.Pick != Pick.All)
                {
                    shownView.Pick = Selection.All;
                }
                Render();
                ScrollToTop();
            }
            if (Settings.Version != shownSettings)
            {
                scanDue = true;   // the range, which chests, or what he carries may have changed
            }
            if (scanDue || now >= nextScan)
            {
                Scan(player, now);
            }
            if (renderDue && Drag.Current == null)
            {
                Render();
            }
        }

        /// <summary>The module's undo step, or a new scene: hides it and drops what it read (the chests' items and the
        /// listings made). Drawn afresh when next shown.</summary>
        internal static void Forget()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
            survey = new DrawerSurvey();
            DrawerItems.Forget();
            Thumbs.Clear();
            shownKeys = null;
            renderDue = scanDue = focusSearch = false;
            searchDue = -1f;
            hovered = null;
        }

        // ---- Every frame it shows

        private static void Keys()
        {
            if (focusSearch)
            {
                // A frame after F was pressed, so the F itself isn't typed into the box.
                focusSearch = false;
                searchBox.Select();
                searchBox.ActivateInputField();
            }
            else if (ZInput.GetKeyDown(KeyCode.F, false) && !Ui.Typing())
            {
                focusSearch = true;
            }
            // Unity gives a dropdown the keyboard again once a choice is made: let go of it, so Enter can't open it again
            // (as Ui.Button's navigation is off: job 11's lesson).
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != null && (selected == groupList.gameObject || selected == sortList.gameObject) &&
                !DrawerUi.Expanded(groupList) && !DrawerUi.Expanded(sortList))
            {
                events.SetSelectedGameObject(null);
            }
            // The wheel scrolls the list under the pointer, but never what lies behind an open Group or Sort list.
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel != 0f && !DrawerUi.Expanded(groupList) && !DrawerUi.Expanded(sortList))
            {
                DrawerUi.Wheel(wheel, RowsPerNotch * (CellSize + CellGap), scrolls);
            }
        }

        // Reads the chests as the equip does (Exchange.Found: its range and "Which chests", no reserved chest, no chest
        // someone else has open that his game can't share; and as its plan takes, one of each stackable left in each chest
        // by "Leave one of each item in chests", only what a chest is set up for by "Take the loadout's items from:
        // SetUpChestsOnly": review round 1 of Session 2, m6), and what he carries; then the thumbnails are made again if
        // the kinds changed, else their counts follow.
        private static void Scan(Player player, float now)
        {
            scanDue = false;
            nextScan = now + ScanEvery;
            shownSettings = Settings.Version;
            if (!Biomes.Built)
            {
                Biomes.Build(line => Module.Log.LogInfo(line));   // once a world: the log says where the items' biomes came from
            }
            ChestRules rules = Settings.ChestRules();
            bool setUpOnly = Settings.Fetch.Value == FetchFrom.SetUpChestsOnly;
            var chests = new List<DrawerChest>();
            int reserved = 0;
            foreach (Container chest in ChestFinder.Find(player.transform.position, rules, withReserved: true))
            {
                if (!rules.UseReserved && ChestReserve.IsReserved(chest))
                {
                    reserved++;
                    continue;
                }
                if (ChestFinder.OpenElsewhere(chest) && !ChestShare.CanShare(chest))
                {
                    continue;
                }
                Inventory inventory = chest.GetInventory();
                if (inventory != null)
                {
                    chests.Add(new DrawerChest { Items = inventory.GetAllItems(), Gives = setUpOnly ? SetUpFor(chest) : null });
                }
            }
            bool carried = Settings.DrawerCarried.Value;
            DrawerSurvey before = survey;
            survey = DrawerItems.Read(chests, carried ? player.GetInventory().GetAllItems() : null, Game.m_worldLevel, Favourite, rules.LeaveOne);
            chestsText.text = DrawerItems.ChestsLine(survey.Chests, reserved, rules.Range, carried, rules.LeaveOne, setUpOnly);
            if (survey.Keys != shownKeys)
            {
                renderDue = true;   // another kind, or another favourite, or something more or less he carries
            }
            else if (sortShownId == DrawerItems.CountSort && CountsChanged(before, survey))
            {
                renderDue = true;   // sorted by count: the order may change
            }
            else
            {
                Paint();
            }
            if (hovered != null)
            {
                ShowHover(hovered, hoveredOwn);
            }
            else
            {
                ShowHint();
            }
        }

        // What a chest gives with "Take the loadout's items from: SetUpChestsOnly": what its setup takes (nothing without
        // one), as the equip's plan asks it (EquipPlanner.Allowed).
        private static Func<ItemDrop.ItemData, bool> SetUpFor(Container chest)
        {
            Func<ItemDrop.ItemData, int> fit = Equip.Setups.Reading(chest);
            return fit == null ? (Func<ItemDrop.ItemData, bool>)(item => false) : item => fit(item) > 0;
        }

        private static bool CountsChanged(DrawerSurvey before, DrawerSurvey after)
        {
            foreach (DrawerKind kind in after.Kinds)
            {
                DrawerKind was = before.Find(kind.Key);
                if (was == null || was.Count != kind.Count)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- What it shows

        // The character, by its profile (the same after a death, which makes a new Player): another character starts
        // afresh, with an empty filter.
        private static DrawerView ViewNow()
        {
            object owner = Game.instance != null && Game.instance.GetPlayerProfile() != null ? Game.instance.GetPlayerProfile() : (object)Player.m_localPlayer;
            if (view == null || owner != viewFor)
            {
                viewFor = owner;
                view = new DrawerView();
                if (searchBox != null)
                {
                    searchBox.SetTextWithoutNotify(string.Empty);
                }
                searchDue = -1f;
            }
            return view;
        }

        /// <summary>Draws the list on the left and the results afresh; put off while anything is dragged.</summary>
        private static void Render()
        {
            if (rail == null || grid == null)
            {
                return;
            }
            if (Drag.Current != null)
            {
                renderDue = true;
                return;
            }
            renderDue = false;
            DrawerView shownView = ViewNow();
            DrawerItems.Resolve(shownView, survey);
            RenderRail(shownView);
            RenderGrid(shownView);
            shownKeys = survey.Keys;
        }

        // A filter still waiting for him to pause applies at once (typing searches every item: the pick goes back to All),
        // then the click that called this narrows it. True if one was waiting.
        private static bool FlushSearch()
        {
            if (searchDue < 0f)
            {
                return false;
            }
            searchDue = -1f;
            DrawerView shownView = ViewNow();
            if (shownView.Query.Length > 0)
            {
                shownView.Pick = Selection.All;
            }
            return true;
        }

        private static void Typed(string text)
        {
            ViewNow().Query = (text ?? string.Empty).Trim();
            searchDue = Time.unscaledTime + SearchPause;   // drawn once he pauses, not at every letter
        }

        private static void ClearQuery()
        {
            searchBox.SetTextWithoutNotify(string.Empty);
            searchDue = -1f;
            ViewNow().Query = string.Empty;
            Render();
        }

        private static void ShowAll()
        {
            FlushSearch();
            ViewNow().Pick = Selection.All;
            Render();
            ScrollToTop();
        }

        private static void GroupPicked(int index)
        {
            if (index >= 0 && index < groupOptions.Count)
            {
                ViewNow().Group = groupOptions[index].Id;
                RenderGrid(ViewNow());
            }
        }

        private static void SortPicked(int index)
        {
            if (index >= 0 && index < sortOptions.Count)
            {
                ViewNow().Sort = sortOptions[index].Id;
                RenderGrid(ViewNow());
            }
        }

        private static void FirstSwitched(bool on)
        {
            ViewNow().FavouritesFirst = on;
            RenderGrid(ViewNow());
        }

        private static void ScrollToTop()
        {
            if (gridScroll != null)
            {
                gridScroll.verticalNormalizedPosition = 1f;
            }
        }

        // ---- The list on the left

        private static void RenderRail(DrawerView shownView)
        {
            DrawerUi.Clear(rail);
            foreach (DrawerRow row in DrawerItems.Rail(survey, shownView))
            {
                if (row.Heading)
                {
                    Title(row);
                }
                else
                {
                    Row(row, shownView);
                }
            }
        }

        // A heading, in the game's orange: a click folds its rows away, or brings them back (for the session).
        private static void Title(DrawerRow row)
        {
            Clicks clicks = DrawerUi.ListRow(rail, RailRowHeight, out Image _);
            Arrow(clicks.transform, row.Open, Ui.Orange);
            Ui.Label(clicks.transform, row.Label.ToUpperInvariant(), 18f, 0f, RailWidth - 12f - 8f - 18f, RailRowHeight, 13, TextAnchor.MiddleLeft,
                     Ui.Orange, bold: true);
            string title = row.Label;
            clicks.OnLeft = () => TitleClicked(title);
        }

        private static void TitleClicked(string title)
        {
            bool flushed = FlushSearch();   // a filter still waiting applies now, as with any click on the list
            DrawerView shownView = ViewNow();
            if (!shownView.Folded.Remove(title))
            {
                shownView.Folded.Add(title);
            }
            if (flushed)
            {
                Render();
                ScrollToTop();
            }
            else
            {
                RenderRail(shownView);
            }
        }

        private static void Row(DrawerRow row, DrawerView shownView)
        {
            bool picked = shownView.Pick.Equals(row.Pick);
            Clicks clicks = DrawerUi.ListRow(rail, RailRowHeight, out Image back);
            clicks.Normal = picked ? Ui.RowPicked : Ui.RowPlain;
            clicks.Lights = !picked;
            back.color = clicks.Normal;
            float x = row.Member ? 30f : 18f;
            if (row.OpenId != null)
            {
                // The twisty opens and closes the family without picking it.
                GameObject twisty = Arrow(clicks.transform, row.Open, Ui.Quiet);
                twisty.GetComponent<Graphic>().raycastTarget = true;
                Clicks twist = twisty.AddComponent<Clicks>();
                twist.Lights = false;
                twist.OnLeft = () => RowClicked(row, onTwisty: true);
            }
            if (row.Favourites)
            {
                Sprite star = DrawerUi.Star();
                if (star != null)
                {
                    DrawerUi.Icon(clicks.transform, star, x, 4f, 16f);
                    x += 20f;
                }
            }
            // One line: a long name ("Meads and Potions") shrinks a little rather than wrapping.
            const float countX = RailWidth - 12f - 8f - 30f;
            Text label = Ui.Label(clicks.transform, row.Label, x, 0f, countX - x - 2f, RailRowHeight, row.Member ? 14 : 15, TextAnchor.MiddleLeft,
                                  row.Count == 0 ? DrawerUi.Faint : Ui.Plain);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = row.Member ? 14 : 15;
            Ui.Label(clicks.transform, row.Count.ToString(CultureInfo.InvariantCulture), countX, 0f, 28f, RailRowHeight, 12, TextAnchor.MiddleRight,
                     picked ? Ui.Plain : Ui.Quiet);
            clicks.OnLeft = () => RowClicked(row, onTwisty: false);
        }

        // Clicking a family picks it and opens it; clicking it again closes it; the twisty only opens and closes.
        private static void RowClicked(DrawerRow row, bool onTwisty)
        {
            bool flushed = FlushSearch();   // a filter still waiting applies now, and can't undo this pick
            DrawerView shownView = ViewNow();
            if (row.OpenId != null && (onTwisty || shownView.Pick.Equals(row.Pick)))
            {
                if (!shownView.Open.Remove(row.OpenId))
                {
                    shownView.Open.Add(row.OpenId);
                }
            }
            else if (row.OpenId != null)
            {
                shownView.Open.Add(row.OpenId);
            }
            if (!onTwisty)
            {
                shownView.Pick = row.Pick;
                Render();
                ScrollToTop();
            }
            else if (flushed)
            {
                Render();
                ScrollToTop();
            }
            else
            {
                RenderRail(shownView);
            }
        }

        // The game's own dropdown arrow (its map marker), pointing right when closed and down when open; "+" and "−" if
        // the game ever lacks it.
        private static GameObject Arrow(Transform row, bool open, Color color)
        {
            Sprite arrow = GUIManager.Instance.GetSprite("map_marker");
            if (arrow != null)
            {
                Image image = DrawerUi.Icon(row, arrow, 3f, 6f, 12f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = new Vector2(9f, -RailRowHeight / 2f);
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, open ? 180f : -90f);
                image.color = color;
                return image.gameObject;
            }
            return Ui.Label(row, open ? "−" : "+", 0f, 0f, 18f, RailRowHeight, 15, TextAnchor.MiddleCenter, color, bold: true).gameObject;
        }

        // ---- The results: the line above them, Group, Sort, and the thumbnails under their headings

        private static void RenderGrid(DrawerView shownView)
        {
            if (Drag.Current != null)
            {
                renderDue = true;   // a thumbnail taken away mid-drag would end the drag (DragSource): drawn once it's over
                return;
            }
            DrawerUi.Clear(grid);
            Thumbs.Clear();
            DrawerShown shown = DrawerItems.Results(survey, shownView);
            groupOptions = shown.GroupOptions;
            sortOptions = shown.SortOptions;
            sortShownId = shown.SortId;
            DrawerUi.SetChoices(groupList, GroupShown, groupOptions.Select(option => option.Label).ToList(), shown.GroupIndex);
            DrawerUi.SetChoices(sortList, SortShown, sortOptions.Select(option => option.Label).ToList(), shown.SortIndex);
            firstBox.SetIsOnWithoutNotify(shownView.FavouritesFirst);
            bool own = shownView.Pick.Equals(DrawerItems.WithYou);
            foreach (DrawerSection section in shown.Sections)
            {
                Section(section, own, shown.SortId);
            }
            if (shown.Empty != null)
            {
                GameObject empty = DrawerUi.Row(grid, 110f);
                Ui.Label(empty.transform, shown.Empty, 8f, 0f, ResultsWidth - 40f, 110f, 15, TextAnchor.UpperLeft, Ui.Quiet);
            }
            // What is shown and how many; Group and Sort say the rest (Add to Cart, his call on 0.3.0).
            whereText.text = $"<b><color=#{PlainHex}>{shown.Whole}</color></b>  {shown.Total} item{(shown.Total == 1 ? string.Empty : "s")}";
            string query = shownView.Query;
            clearChip.gameObject.SetActive(query.Length > 0);
            if (query.Length > 0)
            {
                DrawerUi.SetChip(clearChip, $"\"{(query.Length > 8 ? query.Substring(0, 8) + "…" : query)}\" ×");
            }
            allChip.gameObject.SetActive(shown.Narrowed);
            if (hovered != null && survey.Find(hovered) == null)
            {
                hovered = null;
                ShowHint();
            }
        }

        private static void Section(DrawerSection section, bool own, string sortId)
        {
            GameObject title = DrawerUi.Row(grid, 26f);
            float x = 4f;
            Sprite star = section.Star ? DrawerUi.Star() : null;
            if (star != null)
            {
                DrawerUi.Icon(title.transform, star, x, 5f, 18f);
                x += 22f;
            }
            Text heading = Ui.Label(title.transform, $"{section.Heading}  <size=12><color=#{QuietHex}>{section.Kinds.Count}</color></size>", x, 0f,
                                    ResultsWidth - 40f - x, 26f, 16, TextAnchor.LowerLeft, Ui.Orange, bold: true);
            heading.supportRichText = true;   // the catalogue's heading and a number, never typed
            int rows = (section.Kinds.Count + Columns - 1) / Columns;
            GameObject block = DrawerUi.Row(grid, rows * CellSize + Math.Max(0, rows - 1) * CellGap);
            GridLayoutGroup layout = block.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CellSize, CellSize);
            layout.spacing = new Vector2(CellGap, CellGap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            foreach (DrawerKind kind in section.Kinds)
            {
                Thumbnail(block.transform, kind, own, sortId);
            }
        }

        // A thumbnail: its picture (under With you, his own copy's style), its level or the food value sorted by (top
        // left), how many the chests and he hold (bottom left) and the favourite star (top right). Dragged, it starts a
        // DragItem; clicked, it goes to the window (Drag.Clicked); under the pointer, the line below says what it is.
        private static void Thumbnail(Transform block, DrawerKind kind, bool own, string sortId)
        {
            GameObject cell = Ui.Box("Outfitter_DrawerItem", block, 0f, 0f, CellSize, CellSize, Ui.Cell);
            cell.GetComponent<Image>().raycastTarget = true;
            Image icon = DrawerUi.Icon(cell.transform, IconOf(own && kind.Own != null ? kind.Own : kind.Sample), 6f, 6f, CellSize - 12f);
            string corner = DrawerItems.FoodCorner(kind.Listing, sortId) ?? (kind.Quality > 1 ? kind.Quality.ToString(CultureInfo.InvariantCulture) : null);
            if (corner != null)
            {
                Ui.Label(cell.transform, corner, 3f, 1f, CellSize - 6f, 18f, 13, TextAnchor.UpperLeft, Ui.Plain);
            }
            var thumb = new Thumb { Key = kind.Key, Icon = icon };
            thumb.Count = Ui.Label(cell.transform, string.Empty, 3f, CellSize - 22f, CellSize - 6f, 20f, 13, TextAnchor.LowerLeft, Ui.Plain);
            Sprite star = DrawerUi.Star();
            thumb.Star = star != null
                ? DrawerUi.Icon(cell.transform, star, CellSize - 19f, 2f, 17f).gameObject
                : Ui.Label(cell.transform, "*", CellSize - 18f, -2f, 16f, 20f, 20, TextAnchor.UpperRight, DrawerUi.Gold, bold: true).gameObject;
            string key = kind.Key;
            DragSource source = cell.AddComponent<DragSource>();
            source.Make = () => Item(key, own);
            Clicks clicks = cell.AddComponent<Clicks>();
            clicks.Lights = false;
            clicks.OnLeft = () => Clicked(key, own, false);
            clicks.OnRight = () => Clicked(key, own, true);
            clicks.OnEnter = () => Hover(key, own);
            clicks.OnExit = () => Unhover(key);
            Thumbs.Add(thumb);
            Paint(thumb, kind);
        }

        // What changes on the thumbnails between looks, while the kinds stay the same.
        private static void Paint()
        {
            foreach (Thumb thumb in Thumbs)
            {
                DrawerKind kind = survey.Find(thumb.Key);
                if (kind != null)
                {
                    Paint(thumb, kind);
                }
            }
        }

        private static void Paint(Thumb thumb, DrawerKind kind)
        {
            if (thumb.CountShown != kind.Count)
            {
                thumb.CountShown = kind.Count;
                thumb.Count.text = DrawerItems.Short(kind.Count);
            }
            if (thumb.Star.activeSelf != kind.Favourite)
            {
                thumb.Star.SetActive(kind.Favourite);
            }
            bool grey = kind.Count == 0;   // only what "leave one" keeps: listed, as he asked for all, with nothing to take
            if (thumb.Icon != null && thumb.Grey != grey)
            {
                thumb.Grey = grey;
                thumb.Icon.color = grey ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
        }

        // What a thumbnail drags or clicks: its kind as the latest look found it (null, so nothing starts, once it's gone).
        private static DragItem Item(string key, bool own)
        {
            DrawerKind kind = survey.Find(key);
            if (kind == null)
            {
                return null;
            }
            ItemDrop.ItemData sample = own && kind.Own != null ? kind.Own : kind.Sample;
            return new DragItem
            {
                Prefab = kind.Prefab,
                Quality = kind.Quality,
                Variant = sample != null ? sample.m_variant : 0,
                Count = kind.Count,
                Sample = sample,
                Icon = IconOf(sample),
            };
        }

        // A click goes to the window, which decides what it does; a left click only while "Left click on a drawer item"
        // says it does something.
        private static void Clicked(string key, bool own, bool right)
        {
            if (Drag.Current != null)
            {
                return;   // a drag let go over its own thumbnail (Unity calls that a click too), or a click during a drag
            }
            if (!right && Settings.DrawerClicks.Value == DrawerClick.Nothing)
            {
                return;
            }
            DragItem item = Item(key, own);
            if (item == null)
            {
                return;
            }
            Drag.Clicked?.Invoke(item, right);   // a favourite changed elsewhere shows at the next look, within a second
        }

        private static void Hover(string key, bool own)
        {
            hovered = key;
            hoveredOwn = own;
            ShowHover(key, own);
        }

        private static void Unhover(string key)
        {
            if (hovered == key)
            {
                hovered = null;
                ShowHint();
            }
        }

        private static void ShowHover(string key, bool own)
        {
            DrawerKind kind = survey.Find(key);
            if (kind == null)
            {
                ShowHint();
                return;
            }
            hoverName.text = DrawerItems.HoverLine(kind, survey.Carried);
            hoverWhere.text = DrawerItems.Describe(kind.Listing);
            Sprite icon = IconOf(own && kind.Own != null ? kind.Own : kind.Sample);
            hoverIcon.sprite = icon;
            hoverIcon.enabled = icon != null;
        }

        // With nothing under the pointer: how to use it, as "Left click on a drawer item" says.
        private static void ShowHint()
        {
            hoverIcon.enabled = false;
            hoverName.text = "Drag an item onto a cell of the loadout.";
            hoverWhere.text = Settings.DrawerClicks.Value == DrawerClick.FirstFreeCell ? "A click puts it in the first free cell." : string.Empty;
        }

        // An item's picture in its style, or null (a style the item lacks shows its first).
        private static Sprite IconOf(ItemDrop.ItemData item)
        {
            Sprite[] icons = item?.m_shared?.m_icons;
            if (icons == null || icons.Length == 0)
            {
                return null;
            }
            return icons[Mathf.Clamp(item.m_variant, 0, icons.Length - 1)];
        }
    }
}
