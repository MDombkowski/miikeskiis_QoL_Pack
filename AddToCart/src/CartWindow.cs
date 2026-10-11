using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using Jotunn.Managers;
using QoLMods.Catalogue;
using QoLMods.Chests;
using QoLMods.Pictures;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>
    /// The Add to Cart window. On the left, the browser (Browser): three tabs, Build (pieces), Craft (recipes, and what
    /// the processing stations make) and Take (every item in the chests in range, taken as it is), each with the list on
    /// the left, Group, Sort and the thumbnails under their headings. On the right: the cart, the materials it needs
    /// (have/need, from the chests, short), the weight and room after a fetch, and Fetch. Locate, on Take, labels the
    /// chests holding an item.
    ///
    /// Clicks on a thumbnail: left adds one (Shift: 5, or on Take a full stack; Ctrl: as many as the chests allow), right
    /// takes one away (Shift: 5 or a stack), middle stars it and opens his lists (ListsPopup). A thumbnail greys out when
    /// the chests can't cover one more of it after the rest of the cart; a click on a greyed Build or Craft thumbnail asks
    /// "Add all available?". The cart is kept, for the session, until a fetch has covered all of it.
    /// </summary>
    internal static class CartWindow
    {
        private const float RightX = Browser.ResultsX + Browser.ResultsWidth + 20f;
        private const float RightWidth = 374f;
        private const float Width = RightX + RightWidth + 22f;
        private const float Height = 750f;
        private const float CountEvery = 1f;
        private const int BuildStep = 5;

        private sealed class Line
        {
            internal Entry Entry;
            internal int Count;
        }

        private static readonly List<Line> Cart = new List<Line>();
        private static List<Entry> catalogue = new List<Entry>();
        private static List<Entry> takeEntries = new List<Entry>();
        private static string takeKeys = string.Empty;
        private static ChestSurvey survey = new ChestSurvey();
        private static Dictionary<string, int> carrying = new Dictionary<string, int>();
        private static float nextCount;
        private static bool blocking;
        private static bool unblockNextFrame;
        private static bool picking;
        private static bool cartDirty;

        private static GameObject window;
        private static Canvas ownCanvas;
        private static Canvas gameCanvas;
        private static Button locateButton;
        private static RectTransform cartList;
        private static RectTransform materialsList;
        private static Image hoverIcon;
        private static Text hoverText;
        private static Text chestsText;
        private static Text weightText;
        private static Text roomText;
        private static Text statusText;
        private static Button fetchButton;
        private static GameObject popup;
        private static Text popupText;
        private static Action popupYes;
        private static GameObject trueSwitch;
        private static Toggle trueBox;
        private static Text trueLabel;
        private static GameObject lastPopup;
        private static Text lastPopupText;
        private static Action lastAnyway;
        private static Action lastAllButOne;

        internal static bool IsOpen => window != null && window.activeSelf;

        /// <summary>True while one of the window's questions shows (the browser's F leaves the filter alone then).</summary>
        internal static bool PopupOpen => (popup != null && popup.activeSelf) || (lastPopup != null && lastPopup.activeSelf);

        /// <summary>What the chests may give, by key, as the window counts it: as leave-one allows (one kept back in each
        /// chest), or with "True counts" the whole count (card A83). The cart's maths, the thumbnails, the hover lines and the
        /// sorts all count from this, so they always agree; a fetch then asks before it takes a last one (Fetch).</summary>
        private static Dictionary<string, int> Available => Settings.TrueCounts.Value ? survey.Whole : survey.Stock;

        /// <summary>Every frame, from Module.Update, also while the mod is switched off (then it only closes the window).</summary>
        internal static void Update()
        {
            if (unblockNextFrame)
            {
                unblockNextFrame = false;
                Unblock();
            }
            if (!Module.IsOn)
            {
                if (IsOpen || blocking)
                {
                    Close();
                }
                Locator.Stop();
                return;
            }
            if (IsOpen)
            {
                Player player = Player.m_localPlayer;
                if (player == null || player.IsDead() || player.IsTeleporting())
                {
                    Close();
                    return;
                }
                FitCanvas();   // the size setting, the game's GUI scale or the screen may have changed
                if (ZInput.GetKeyDown(KeyCode.Escape, false))
                {
                    if (ListsPopup.IsOpen)
                    {
                        ListsPopup.Close();
                    }
                    else if (lastPopup != null && lastPopup.activeSelf)
                    {
                        CancelLastOnes();   // Escape answers Cancel
                    }
                    else if (popup != null && popup.activeSelf)
                    {
                        popup.SetActive(false);
                    }
                    else if (picking)
                    {
                        StopPicking();
                        ShowLocate();
                    }
                    else
                    {
                        CloseSoon();
                    }
                    return;
                }
                if (!Ui.Typing() && KeyPressed())
                {
                    CloseSoon();
                    return;
                }
                // The wheel scrolls what's under the pointer, but never what lies behind the lists pop-up or an open
                // Group or Sort list.
                if (ListsPopup.IsOpen)
                {
                    Ui.Wheel(Settings.ScrollRows.Value, Browser.CellSize + Browser.CellGap, ListsPopup.Checklist);
                }
                else if (!Browser.ListOpen && !PopupOpen)
                {
                    Ui.Wheel(Settings.ScrollRows.Value, Browser.CellSize + Browser.CellGap, Browser.Grid, Browser.Rail, cartList, materialsList);
                }
                ListsPopup.Update();
                Browser.Update();
                if (cartDirty)
                {
                    cartDirty = false;
                    ShowCart();
                }
                if (Time.time >= nextCount)
                {
                    CountAndShow();
                }
            }
            else if (window == null && blocking)
            {
                Unblock();   // the scene changed under the open window
            }
            else if (KeyPressed() && CanOpen())
            {
                Open();
            }
            else if (Player.m_localPlayer != null)
            {
                // Once per world, soon after he arrives, rather than at his first press of the key: the one pause it
                // may cost comes while the world is still settling in. Returns at once once done.
                Biomes.Build(Log);
            }
        }

        private static void Log(string line) => Module.Log.LogInfo(line);

        /// <summary>Closes the window at once and gives the game its input back. Also the module's undo step.</summary>
        internal static void Close()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            HidePopups();
            ListsPopup.Close();
            picking = false;
            Unblock();
        }

        // The pop-ups are the window's children: hidden with it, they would show again when it opens, with a question about
        // a cart and chests as they were (a stale question about the last ones would fetch what was seen then).
        private static void HidePopups()
        {
            if (popup != null)
            {
                popup.SetActive(false);
            }
            if (lastPopup != null)
            {
                lastPopup.SetActive(false);
            }
        }

        // Closing on Escape or the key: the game's input comes back a frame later, so the same key press can't also open
        // the game's menu (Escape) behind the closing window.
        private static void CloseSoon()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            HidePopups();
            ListsPopup.Close();
            picking = false;
            unblockNextFrame = true;
        }

        private static void Unblock()
        {
            if (blocking)
            {
                blocking = false;
                GUIManager.BlockInput(false);
            }
        }

        private static bool KeyPressed()
        {
            KeyboardShortcut key = Settings.OpenKey.Value;
            if (key.MainKey == KeyCode.None || !ZInput.GetKeyDown(key.MainKey, false))
            {
                return false;
            }
            return Ui.OnlyItsModifiers(key.Modifiers, code => ZInput.GetKey(code, false), key.MainKey);   // strict: no extra Ctrl, Alt or Windows key held
        }

        // As the game decides whether the player takes input (Player.TakeInput): no other window, menu, map or text box.
        private static bool CanOpen()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() && !player.IsTeleporting() &&
                   GUIManager.CustomGUIFront != null &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !StoreGui.IsVisible() && !InventoryGui.IsVisible() && !Menu.IsVisible() &&
                   (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() && !GameCamera.InFreeFly() &&
                   !Hud.IsPieceSelectionVisible();
        }

        private static void Open()
        {
            if (window == null)
            {
                Build();
            }
            Biomes.Build(Log);
            catalogue = Catalogue.Read(Player.m_localPlayer, Settings.ListPlantings.Value);
            Favorites.MoveOldOnes();
            LogWhatIsListed();
            // The cart outlives the window: match its lines to the entries just read (Take lines are matched on the next
            // count, since those entries come from the chests).
            var byKey = new Dictionary<string, Entry>();
            foreach (Entry entry in catalogue)
            {
                byKey[entry.Key] = entry;
            }
            foreach (Line line in Cart.ToList())
            {
                if (line.Entry.Literal)
                {
                    continue;
                }
                if (byKey.TryGetValue(line.Entry.Key, out Entry fresh))
                {
                    line.Entry = fresh;
                }
                else
                {
                    Cart.Remove(line);
                }
            }
            shownMaterials = null;   // the window may be new (a new scene), with no rows yet
            takeKeys = null;         // and the Take tab's entries are read afresh with the catalogue
            Survey();                // before the window shows, so the browser draws once, below
            FitCanvas();
            window.SetActive(true);
            window.transform.SetAsLastSibling();
            if (!blocking)
            {
                blocking = true;
                GUIManager.BlockInput(true);
            }
            unblockNextFrame = false;
            picking = false;
            Browser.Opened();
            ShowCart();
            CountAndShow();
        }

        // What the window lists and what it leaves out, written to the log whenever that changes (once a session,
        // unless he learns something or changes "List plantings"), so a check of the catalogue is one look at the log.
        private static string loggedListing;

        private static void LogWhatIsListed()
        {
            int pieces = catalogue.Count(entry => entry.Kind == EntryKind.Piece);
            int recipes = catalogue.Count(entry => entry.Kind == EntryKind.Recipe);
            int processing = catalogue.Count(entry => entry.Kind == EntryKind.Conversion);
            var text = new StringBuilder($"The window lists {pieces} building pieces, {recipes} recipes and {processing} things the processing stations make");
            text.Append(Catalogue.InSeason > 0 ? $", {Catalogue.InSeason} of them seasonal and in season now." : ".");
            foreach (KeyValuePair<string, List<string>> reason in Catalogue.LeftOut)
            {
                text.Append($" Left out, {reason.Value.Count} {reason.Key}: {string.Join(", ", reason.Value)}.");
            }
            string listing = text.ToString();
            if (listing != loggedListing)
            {
                loggedListing = listing;
                Module.Log.LogInfo(listing);
            }
        }

        // ---- What the browser asks

        /// <summary>The thumbnails of a tab.</summary>
        internal static List<Entry> EntriesOf(Tab tab) => tab == Tab.Take ? takeEntries
            : catalogue.Where(entry => tab == Tab.Build ? entry.Kind == EntryKind.Piece : entry.Kind == EntryKind.Recipe || entry.Kind == EntryKind.Conversion).ToList();

        internal static int CountOf(Entry entry) => Cart.FirstOrDefault(line => line.Entry.Key == entry.Key)?.Count ?? 0;

        /// <summary>What the chests in range may give of an item, as the window counts (Available: after leaving one, where
        /// that is on, unless "True counts").</summary>
        internal static int StockOf(string key) => Get(Available, key);

        private static string loggedNotOnTake;

        /// <summary>
        /// For the Take tab when the filter finds nothing: where something of that name lies among the chests loaded near
        /// him, and why the cart leaves it out, in a sentence or two (his 0.3.0 test: a Shovel in a chest that Take didn't
        /// show). Written to the log as well, with each item's and chest's details.
        /// </summary>
        internal static string NotOnTake(string query)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Localization.instance == null)
            {
                return string.Empty;
            }
            string[] words = query.ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            ChestRules rules = Settings.Rules();
            bool whole = Settings.TrueCounts.Value;
            // With "True counts" the Take tab lists the last ones too: no item is left out for being one.
            ChestRules explaining = Settings.Rules();
            explaining.LeaveOne &= !whole;
            List<LeftOutItem> found = ChestStock.Explain(player.transform.position, explaining, item =>
            {
                string name = Localization.instance.Localize(item.m_shared.m_name).ToLowerInvariant();
                return words.All(word => name.Contains(word));
            });
            // The Take tab is up to a second old, the chests are read now: something that has just reached a chest isn't
            // a fault. Counted afresh (without touching the tab) before saying so (review 1).
            if (found.Any(each => each.Why == null) &&
                Catalogue.Take(ChestStock.Survey(player.transform.position, rules), whole).Any(entry => entry.Listing != null && Browse.Matches(entry.Listing, query)))
            {
                return "It has just reached a chest: it shows at the next count, within a second.";
            }
            string text = found.Count == 0
                ? "No chest near you holds anything of that name."
                : string.Join(" ", found.Take(2).Select(each =>
                {
                    string what = $"{Localization.instance.Localize(each.Name)} ×{each.Amount}";
                    return each.Why != null
                        ? $"{what}, in a chest {each.Distance:0} m away, is left out: {each.Why}."
                        : $"{what} is in a chest the cart counts, {each.Distance:0} m away, yet isn't listed: a fault in the mod, noted in its log.";
                }));
            string log = $"Take: nothing listed for \"{query}\". " + (found.Count == 0 ? text
                : string.Join(" ", found.Select(each => $"{each.Name} ×{each.Amount} ({each.Detail}): {each.Why ?? "counted, yet not listed"}.")));
            // Once per query and set of reasons: distances change as he walks, and would write a line at every redraw.
            string reasons = query + "|" + string.Join("|", found.Select(each => each.Name + ":" + new string((each.Why ?? "counted").Where(c => !char.IsDigit(c)).ToArray())));
            if (reasons != loggedNotOnTake)
            {
                loggedNotOnTake = reasons;
                Module.Log.LogInfo(log);
            }
            return text;
        }

        /// <summary>For each thumbnail, whether the chests can't cover one more of it after the rest of the cart: the
        /// cart's needs worked out once, for every thumbnail.</summary>
        internal static Func<Entry, bool> Greying()
        {
            Dictionary<string, Need> needs = CartMath.NeedsByItem(CartPairs());
            bool onlyShort = Settings.OnlyWhatImShort.Value;
            Dictionary<string, int> settled = CartMath.Settled(needs, carrying, Available, onlyShort);
            return entry => CartMath.ShortForOneMore(entry, needs, carrying, settled, onlyShort).Count > 0;
        }

        /// <summary>The Count sort: on Take, how many the chests hold; on Build and Craft, how many more he could make from
        /// what he carries and the chests hold, after the cart.</summary>
        internal static Func<Entry, double> Counter()
        {
            Dictionary<string, Need> needs = CartMath.NeedsByItem(CartPairs());
            bool onlyShort = Settings.OnlyWhatImShort.Value;
            Dictionary<string, int> settled = CartMath.Settled(needs, carrying, Available, onlyShort);
            return entry => entry.Literal ? StockOf(entry.Costs[0].Item) : CartMath.MostMoreSettled(entry, needs, carrying, settled, onlyShort);
        }

        internal static void Status(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        // ---- Counting ----

        // Counts the chests and his inventory; rebuilds the Take tab's entries when the kinds of item in the chests change.
        private static void Survey()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            survey = ChestStock.Survey(player.transform.position, Settings.Rules());
            carrying = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item.m_worldLevel < Game.m_worldLevel)
                {
                    continue;
                }
                Add(carrying, item.m_shared.m_name, item.m_stack);
                string key = ChestStock.KeyOf(item);
                if (key != item.m_shared.m_name)
                {
                    Add(carrying, key, item.m_stack);
                }
            }
            List<Entry> fresh = Catalogue.Take(survey, Settings.TrueCounts.Value);
            string keys = string.Join("\n", fresh.Select(entry => entry.Key).OrderBy(key => key, StringComparer.Ordinal));
            if (keys != takeKeys)
            {
                takeKeys = keys;
                takeEntries = fresh;
                // Take lines in the cart point at the new entries; one whose item has left the chests keeps its old entry
                // (and shows as short).
                var byKey = takeEntries.ToDictionary(entry => entry.Key);
                foreach (Line line in Cart)
                {
                    if (line.Entry.Literal && byKey.TryGetValue(line.Entry.Key, out Entry now))
                    {
                        line.Entry = now;
                    }
                }
                Browser.TakeChanged();
            }
        }

        private static void CountAndShow()
        {
            nextCount = Time.time + CountEvery;
            Survey();
            ChestRules rules = Settings.Rules();
            string reserved = survey.ReservedChests > 0 ? $" {survey.ReservedChests} reserved {(survey.ReservedChests == 1 ? "chest is" : "chests are")} left alone." : string.Empty;
            string leaving = !rules.LeaveOne ? "."
                : Settings.TrueCounts.Value ? ", the last one in each chest included (True counts)."
                : rules.LeaveOneOfGear ? ", leaving one of each item in each."
                : ", leaving one of each stackable item in each.";
            chestsText.text = $"Counting {survey.Chests} {(survey.Chests == 1 ? "chest" : "chests")} within {rules.Range:0} m" + leaving + reserved;
            Browser.RefreshCells();
            ShowMaterials();
            ShowLocate();
        }

        private static IEnumerable<KeyValuePair<Entry, int>> CartPairs() =>
            Cart.Select(line => new KeyValuePair<Entry, int>(line.Entry, line.Count));

        // An item key, as he reads it: "Iron Sword, level 3" for one quality of gear.
        private static string ItemName(string key)
        {
            ChestStock.Parse(key, out string name, out int quality);
            return Localization.instance.Localize(name) + (quality > 0 ? $", level {quality}" : string.Empty);
        }

        private static ItemDrop.ItemData ItemData(string key)
        {
            if (survey.Samples.TryGetValue(key, out ItemDrop.ItemData sample))
            {
                return sample;
            }
            ChestStock.Parse(key, out string name, out _);
            return Catalogue.Items.TryGetValue(name, out ItemDrop.ItemData item) ? item : null;
        }

        // 1234 as "1.2k", so a count fits a thumbnail's corner.
        internal static string Short(int amount) => amount >= 10000 ? $"{amount / 1000}k" : amount >= 1000 ? $"{amount / 100 / 10f:0.#}k" : amount.ToString();

        // ---- The thumbnails' clicks and hover ----

        // The name line, and one line of what one costs as "X/Y": X what one takes, Y what the chests in range may give.
        // X is red when that material is what greys the thumbnail (the chests, and what he carries where that counts,
        // can't cover one more after the rest of the cart).
        internal static void ShowHover(Entry entry)
        {
            var text = new StringBuilder($"<b>{entry.Name}</b>");
            if (entry.Makes > 1)
            {
                text.Append($" (makes {entry.Makes})");
            }
            string where = Catalogue.Describe(entry);
            if (where.Length > 0)
            {
                text.Append($" · {where}");
            }
            if (entry.Comfort > 0)
            {
                // As Comfort Build's line in the build menu begins: "Comfort 2 (Chairs)", in the catalogue's words.
                text.Append($" · <color=#{ColorUtility.ToHtmlStringRGB(Ui.Comfort)}>Comfort {entry.Comfort}</color> " +
                            $"({Taxonomy.ComfortKind(entry.ComfortGroup.ToString())})");
            }
            text.Append('\n');
            if (entry.Literal)
            {
                string key = entry.Costs[0].Item;
                int reserved = survey.ReservedOf(key);
                int inChests = StockOf(key);
                // With "True counts", how many can go without a chest's last one.
                string kept = Settings.TrueCounts.Value && survey.StockOf(key) < inChests ? $", {survey.StockOf(key)} leaving one in each chest" : string.Empty;
                text.Append($"In the chests: {inChests}{kept}" + (reserved > 0 ? $" (and {reserved} in reserved chests)" : string.Empty) +
                            $". You carry {Get(carrying, key)}. Alt+click, or Locate, to find the chests.");
            }
            else
            {
                var red = new HashSet<string>(CartMath.ShortForOneMore(entry, CartPairs(), carrying, Available, Settings.OnlyWhatImShort.Value).Select(each => each.Key));
                string bad = ColorUtility.ToHtmlStringRGB(Ui.Bad);
                text.Append(string.Join(", ", entry.Costs.Select(cost =>
                {
                    string amount = red.Contains(cost.Item) ? $"<color=#{bad}>{cost.Amount}</color>" : cost.Amount.ToString();
                    return $"{amount}/{StockOf(cost.Item)} {ItemName(cost.Item)}";
                })));
            }
            hoverText.text = text.ToString();
            // Its picture beside the words, so a glance down doesn't lose his place (his note on 0.3.0).
            hoverIcon.sprite = entry.Icon;
            hoverIcon.enabled = entry.Icon != null;
        }

        internal static void ClearHover()
        {
            if (hoverText != null)
            {
                hoverText.text = string.Empty;
            }
            if (hoverIcon != null)
            {
                hoverIcon.enabled = false;
            }
        }

        internal static void LeftClick(Entry entry)
        {
            if (entry.Literal && (picking || Ui.Alt()))
            {
                picking = false;
                Locate(entry);
                return;
            }
            if (Ui.Ctrl())
            {
                int most = CartMath.MostMore(entry, CartMath.NeedsByItem(CartPairs()), carrying, Available, Settings.OnlyWhatImShort.Value);
                if (most > 0)
                {
                    Change(entry, most);
                    return;
                }
            }
            AddSome(entry, Ui.Shift() ? Step(entry) : 1);
        }

        internal static void RightClick(Entry entry) => Change(entry, -(Ui.Shift() ? Step(entry) : 1));

        // Shift's step: a full stack on Take, 5 on Build and Craft.
        private static int Step(Entry entry)
        {
            if (!entry.Literal)
            {
                return BuildStep;
            }
            ItemDrop.ItemData item = ItemData(entry.Costs[0].Item);
            return item != null ? Mathf.Max(1, item.m_shared.m_maxStackSize) : 1;
        }

        private static void AddSome(Entry entry, int amount)
        {
            Dictionary<string, Need> needs = CartMath.NeedsByItem(CartPairs());
            bool onlyShort = Settings.OnlyWhatImShort.Value;
            Dictionary<string, int> settled = CartMath.Settled(needs, carrying, Available, onlyShort);
            List<KeyValuePair<string, int>> shortages = CartMath.ShortForOneMore(entry, needs, carrying, settled, onlyShort);
            if (shortages.Count == 0)
            {
                // As many of the amount as the chests cover; on Build and Craft, the rest is asked about below.
                int most = CartMath.MostMoreSettled(entry, needs, carrying, settled, onlyShort);
                Change(entry, Math.Min(amount, Math.Max(1, most)));
                return;
            }
            if (entry.Literal)
            {
                statusText.text = $"All the {entry.Name} in the chests is in the cart already.";
                return;
            }
            string missing = string.Join(", ", shortages.Select(each => $"{each.Value} {ItemName(each.Key)}"));
            Ask($"The chests can't cover one more {entry.Name}: {missing} short.\n\n" +
                "Add it to the cart anyway? A fetch then takes all that's available.",
                () => Change(entry, +1));
        }

        private static void Change(Entry entry, int by) => SetCount(entry, CountOf(entry) + by);

        // A count typed into a cart line. The field calls this as it loses the keyboard, which may be while the window
        // closes, while a fetch rebuilds the rows, or as he presses a button in another row: so the rows are never rebuilt
        // from here (that would lose his click, or build them twice), only marked to be rebuilt in the next frame.
        private static void Typed(Entry entry, string typed)
        {
            Line line = Cart.FirstOrDefault(each => each.Entry.Key == entry.Key);
            if (line == null || !int.TryParse(typed, out int wanted) || wanted < 0)
            {
                cartDirty = true;   // shows the count as it was
                return;
            }
            line.Count = wanted;
            if (wanted == 0)
            {
                Cart.Remove(line);
                cartDirty = true;
            }
            if (IsOpen)
            {
                Browser.RefreshCells();
                ShowMaterials();
            }
        }

        private static void SetCount(Entry entry, int count)
        {
            Line line = Cart.FirstOrDefault(each => each.Entry.Key == entry.Key);
            if (line == null)
            {
                if (count <= 0)
                {
                    return;
                }
                line = new Line { Entry = entry };
                Cart.Add(line);
            }
            line.Count = count;
            if (line.Count <= 0)
            {
                Cart.Remove(line);
            }
            ShowCart();
            Browser.RefreshCells();
            ShowMaterials();
        }

        // ---- Locate ----

        internal static void StopPicking() => picking = false;

        private static void Locate(Entry entry)
        {
            int chests = Locator.Start(entry.Costs[0].Item, entry.Name);
            statusText.text = chests > 0
                ? $"A label now floats over the {chests} {(chests == 1 ? "chest" : "chests")} holding {entry.Name}, seen through walls. Close the window and follow it; " +
                  (Settings.LocateClearsAll.Value && chests > 1 ? "opening any one of them clears them all." : "opening a chest clears its label.")
                : $"No chest in range holds {entry.Name}.";
            ShowLocate();
        }

        internal static void ShowLocate()
        {
            if (locateButton == null)
            {
                return;
            }
            locateButton.gameObject.SetActive(Browser.Current == Tab.Take || Locator.Active);
            locateButton.GetComponentInChildren<Text>().text = Locator.Active ? "Stop locating" : picking ? "Pick an item…" : "Locate";
            ShowTrueSwitch();
        }

        // ---- True counts (card A83) ----

        // The switch on the Take tab, beside Locate (setting "True counts switch on the Take tab"): its tick follows the
        // setting, which F1 may change too; with leave-one off it can't be switched (every count is whole already).
        private static void ShowTrueSwitch()
        {
            if (trueSwitch == null)
            {
                return;
            }
            trueSwitch.SetActive(Browser.Current == Tab.Take && Settings.TrueCountsSwitch.Value);
            bool leavesOne = Settings.Rules().LeaveOne;
            trueBox.SetIsOnWithoutNotify(Settings.TrueCounts.Value);
            trueBox.interactable = leavesOne;
            trueLabel.color = leavesOne ? Ui.Plain : Ui.Faint;
        }

        private static void TrueCountsSwitched(bool on)
        {
            if (!Settings.Rules().LeaveOne)
            {
                ShowTrueSwitch();
                return;
            }
            Settings.TrueCounts.Value = on;
            statusText.text = on
                ? "True counts: every count includes the last one in each chest. A fetch asks before it takes one."
                : "Every count leaves out the one kept back in each chest.";
            CountAndShow();
        }

        private static void ShowTrueHint()
        {
            hoverIcon.enabled = false;
            hoverText.text = Settings.Rules().LeaveOne
                ? "<b>True counts</b>: every count includes the one \"Leave one of each item in chests\" keeps back in each chest. " +
                  "A fetch that would take a chest's last one asks first."
                : "<b>True counts</b>: \"Leave one of each item in chests\" is off, so every count is the whole count already.";
        }

        private static void LocateClicked()
        {
            if (Locator.Active)
            {
                Locator.Stop();
                statusText.text = "Stopped locating.";
            }
            else
            {
                picking = !picking;
                statusText.text = picking ? "Click the item to find in the chests. Escape cancels." : string.Empty;
            }
            ShowLocate();
        }

        // ---- The right side: cart, materials, fetch ----

        private static void ShowCart()
        {
            Ui.Clear(cartList);
            if (Cart.Count == 0)
            {
                GameObject empty = Ui.Row(cartList, 60f);
                Ui.Label(empty.transform, "Click a thumbnail to put one in the cart; right-click takes one out. Shift and Ctrl add more. " +
                         "Right-click a line here to take it out.", 6f, 0f, RightWidth - 30f, 60f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
                return;
            }
            foreach (Line line in Cart)
            {
                Entry entry = line.Entry;
                GameObject row = Ui.Row(cartList, 34f, new Color(0f, 0f, 0f, 0.25f));
                Ui.Icon(row.transform, entry.Icon, 2f, 2f, 30f);
                string makes = entry.Makes > 1 ? $" ×{entry.Makes}" : string.Empty;
                Ui.Label(row.transform, entry.Name + makes, 38f, 0f, 176f, 34f, 15);
                Button less = Ui.Button(row.transform, "−", 218f, 3f, 28f, 28f, () => Module.Guard("changing the cart", () => Change(entry, -1)));
                GameObject field = GUIManager.Instance.CreateInputField(row.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                        InputField.ContentType.IntegerNumber, null, 15, 44f, 28f);
                Ui.Place(field, 248f, 3f, 44f, 28f);
                InputField count = field.GetComponent<InputField>();
                count.characterLimit = 5;
                count.SetTextWithoutNotify(line.Count.ToString());
                count.onEndEdit.AddListener(typed => Module.Guard("changing the cart", () => Typed(entry, typed)));
                Button more = Ui.Button(row.transform, "+", 294f, 3f, 28f, 28f, () => Module.Guard("changing the cart", () => AddSome(entry, 1)));
                Button remove = Ui.Button(row.transform, "×", 326f, 3f, 28f, 28f, () => Module.Guard("changing the cart", () => SetCount(entry, 0)));
                Clicks hover = row.AddComponent<Clicks>();
                hover.Enter = () => ShowHover(entry);
                hover.Exit = ClearHover;
                // A right-click anywhere on the line takes it out, as × does (his note on 0.3.0): on the line itself and on
                // each control in it, since a control takes the click it is under.
                hover.Right = () => SetCount(entry, 0);
                foreach (GameObject control in new[] { less.gameObject, field, more.gameObject, remove.gameObject })
                {
                    control.AddComponent<Clicks>().Right = () => SetCount(entry, 0);
                }
            }
        }

        private static List<MaterialLine> Materials() =>
            CartMath.Materials(CartPairs(), carrying, Available, Settings.OnlyWhatImShort.Value);

        // What the materials rows show now, so the once-a-second count rebuilds them only when something changed.
        private static string shownMaterials;

        private static void ShowMaterials()
        {
            List<MaterialLine> lines = Materials();
            Player player = Player.m_localPlayer;
            float extraWeight = 0f;
            int slots = 0;
            foreach (MaterialLine line in lines)
            {
                ItemDrop.ItemData item = ItemData(line.Item);
                if (item != null)
                {
                    extraWeight += line.FromChests * item.GetNonStackedWeight();   // a bigger fish weighs more
                    if (player != null && line.FromChests > 0)
                    {
                        int room = player.GetInventory().FindFreeStackSpace(item.m_shared.m_name, Game.m_worldLevel);
                        int stack = Mathf.Max(1, item.m_shared.m_maxStackSize);
                        slots += Mathf.Max(0, (line.FromChests - room + stack - 1) / stack);
                    }
                }
            }
            string showing = string.Join(";", lines.Select(line => $"{line.Item}|{line.Need}|{line.Carrying}|{line.FromChests}|{line.Short}"));
            if (showing != shownMaterials)
            {
                shownMaterials = showing;
                ShowMaterialRows(lines);
            }

            if (player == null)
            {
                return;
            }
            float weight = player.GetInventory().GetTotalWeight() + extraWeight;
            float most = player.GetMaxCarryWeight();
            bool heavy = weight > most;
            weightText.text = $"Weight after the fetch: {weight:0} of {most:0}" + (heavy ? ": too heavy, you'd walk slowly and tire." : string.Empty);
            weightText.color = heavy ? Ui.Bad : Ui.Plain;
            int free = player.GetInventory().GetEmptySlots();
            bool crowded = slots > free;
            roomText.text = $"Needs about {slots} free {(slots == 1 ? "slot" : "slots")}; you have {free}." + (crowded ? " Some won't fit." : string.Empty);
            roomText.color = crowded ? Ui.Bad : Ui.Plain;
            fetchButton.interactable = !ChestFetch.Busy && lines.Any(line => line.FromChests > 0);
        }

        private static void ShowMaterialRows(List<MaterialLine> lines)
        {
            Ui.Clear(materialsList);
            foreach (MaterialLine line in lines)
            {
                GameObject row = Ui.Row(materialsList, 26f);
                ItemDrop.ItemData item = ItemData(line.Item);
                if (item != null)
                {
                    Ui.Icon(row.transform, item.GetIcon(), 2f, 1f, 24f);
                }
                Ui.Label(row.transform, ItemName(line.Item), 30f, 0f, 150f, 26f, 15);
                Ui.Label(row.transform, $"{line.Carrying}/{line.Need}", 182f, 0f, 62f, 26f, 15, TextAnchor.MiddleRight, Ui.Quiet);
                Ui.Label(row.transform, line.FromChests > 0 ? line.FromChests.ToString() : "–", 246f, 0f, 50f, 26f, 15, TextAnchor.MiddleRight,
                         line.FromChests > 0 ? Ui.Good : Ui.Quiet);
                Ui.Label(row.transform, line.Short > 0 ? line.Short.ToString() : "–", 298f, 0f, 50f, 26f, 15, TextAnchor.MiddleRight,
                         line.Short > 0 ? Ui.Bad : Ui.Quiet);
            }
        }

        private static void Fetch()
        {
            if (ChestFetch.Busy)
            {
                return;
            }
            CountAndShow();
            List<MaterialLine> lines = Materials();
            Dictionary<string, int> order = CartMath.Order(lines);
            if (order.Count == 0)
            {
                statusText.text = "Nothing to fetch: you carry it all already, or the chests have none of it.";
                return;
            }
            bool coveredAtStart = lines.All(line => line.Short == 0);
            List<string> fetchedKeys = Cart.Select(line => line.Entry.Key).ToList();
            ChestRules rules = Settings.Rules();
            if (Settings.TrueCounts.Value && rules.LeaveOne)
            {
                // The counts were whole: whatever leave-one wouldn't allow is a chest's last one, and he is asked first
                // (card A83). The question carries the orders as seen now; its answers fetch exactly those.
                List<MaterialLine> keptLines = CartMath.Materials(CartPairs(), carrying, survey.Stock, Settings.OnlyWhatImShort.Value);
                Dictionary<string, int> kept = CartMath.Order(keptLines);
                List<LastOne> lastOnes = CartMath.LastOnes(order, kept);
                if (lastOnes.Count > 0)
                {
                    AskLastOnes(order, kept, lastOnes, rules, coveredAtStart, keptLines.All(line => line.Short == 0), fetchedKeys);
                    return;
                }
            }
            StartFetch(order, rules, coveredAtStart, fetchedKeys);
        }

        // A fetch of the order under these rules. "Fetching…" goes up first: the result may come back before Start does.
        private static void StartFetch(Dictionary<string, int> order, ChestRules rules, bool coveredAtStart, List<string> fetchedKeys)
        {
            statusText.text = "Fetching…";
            fetchButton.interactable = false;
            if (!ChestFetch.Start(Module.Chests, order, rules, result => Fetched(result, coveredAtStart, fetchedKeys, rules, false)))
            {
                statusText.text = "A fetch is under way: try again in a moment.";
                fetchButton.interactable = true;
            }
        }

        private static void Fetched(FetchResult result, bool coveredAtStart, List<string> fetchedKeys, ChestRules rules, bool lastOnesLeft)
        {
            string message = Describe(result, rules);
            if (lastOnesLeft)
            {
                message += " The last ones weren't taken: another fetch was under way. Fetch again for them.";
            }
            Module.Log.LogInfo(message);
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
            bool fetchedAny = result.Moved.Values.Any(amount => amount > 0);
            if (fetchedAny)
            {
                Memory.Fetched(fetchedKeys);
            }
            bool gotAll = result.Wanted.Keys.All(item => result.ShortOf(item) <= 0);
            if (gotAll && coveredAtStart)
            {
                Cart.Clear();   // done shopping
            }
            if (IsOpen)
            {
                statusText.text = message;
                ShowCart();
                CountAndShow();
                if (fetchedAny)
                {
                    Browser.Render();   // Recent and Frequent may be offered now, and change the order
                }
            }
        }

        private static string Describe(FetchResult result, ChestRules rules)
        {
            var text = new StringBuilder();
            var moved = result.Moved.Where(each => each.Value > 0).Select(each => $"{each.Value} {ItemName(each.Key)}").ToList();
            text.Append(moved.Count > 0
                ? $"Fetched {string.Join(", ", moved)} from {result.ChestsUsed} {(result.ChestsUsed == 1 ? "chest" : "chests")}" +
                  (result.ChestsShared > 0 ? $" ({result.ChestsShared} of them open by a friend)." : ".")
                : "Fetched nothing.");
            if (result.Dropped > 0)
            {
                text.Append($" {result.Dropped} didn't fit in your inventory and lie at your feet.");
            }
            var missing = result.Wanted.Keys.Where(item => result.ShortOf(item) > 0).Select(item => $"{result.ShortOf(item)} {ItemName(item)}").ToList();
            if (missing.Count > 0)
            {
                text.Append($" Didn't get {string.Join(", ", missing)}.");
            }
            if (result.ChestsInUse > 0)
            {
                text.Append($" Skipped {result.ChestsInUse} {(result.ChestsInUse == 1 ? "chest" : "chests")} someone has open.");
            }
            if (result.ChestsReserved > 0)
            {
                text.Append($" Skipped {result.ChestsReserved} {(result.ChestsReserved == 1 ? "chest" : "chests")} reserved meanwhile.");
            }
            if (missing.Count > 0 && Player.m_localPlayer != null)
            {
                // Which item, how far its chest is and why it couldn't be had (card A69), said and logged.
                var left = result.Wanted.Keys.Where(item => result.ShortOf(item) > 0).ToDictionary(item => item, item => result.ShortOf(item));
                List<ShortItem> items = ChestExplain.Shortfall(Module.Chests, left, Player.m_localPlayer.transform.position, rules);
                ChestExplain.Log(Module.Chests, "Fetching", items);
                string why = ChestExplain.ShortReason(items);
                if (why != null)
                {
                    text.Append($" {char.ToUpperInvariant(why[0])}{why.Substring(1)}.");
                }
                else if (result.ChestsOutOfReach > 0)
                {
                    text.Append($" Left out {result.ChestsOutOfReach} {(result.ChestsOutOfReach == 1 ? "chest" : "chests")} beyond the server's reach.");
                }
                else if (result.ChestsNoAnswer > 0)
                {
                    text.Append($" {result.ChestsNoAnswer} {(result.ChestsNoAnswer == 1 ? "chest wasn't" : "chests weren't")} handed to you in time.");
                }
            }
            else if (result.ChestsNoAnswer > 0)
            {
                text.Append($" {result.ChestsNoAnswer} {(result.ChestsNoAnswer == 1 ? "chest wasn't" : "chests weren't")} handed to you in time.");
            }
            if (result.InventoryFull)
            {
                text.Append(" Your inventory is full.");
            }
            return text.ToString();
        }

        // ---- The pop-up ----

        private static void Ask(string question, Action yes)
        {
            if (popup == null)
            {
                popup = Ui.Box("AddToCart_Popup", window.transform, 0f, 0f, Width, Height, new Color(0f, 0f, 0f, 0.55f));
                GameObject panel = GUIManager.Instance.CreateWoodpanel(popup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 500f, 250f, false);
                Ui.Label(panel.transform, "Add all available?", 20f, 16f, 460f, 34f, 22, TextAnchor.MiddleCenter, Ui.Orange, bold: true);
                popupText = Ui.Label(panel.transform, string.Empty, 30f, 56f, 440f, 120f, 16, TextAnchor.UpperCenter);
                Ui.Button(panel.transform, "Yes", 120f, 190f, 120f, 36f, () => Module.Guard("answering the pop-up", () =>
                {
                    popup.SetActive(false);
                    popupYes?.Invoke();
                }));
                Ui.Button(panel.transform, "No", 260f, 190f, 120f, 36f, () => popup.SetActive(false));
            }
            popupText.text = question;
            popupYes = yes;
            popup.SetActive(true);
            popup.transform.SetAsLastSibling();
        }

        /// <summary>The most items the question names one by one; the rest are counted.</summary>
        private const int LastOnesListed = 6;

        // "True counts" and a fetch that would take the last one of something from a chest (card A83, his words): which
        // items, how many, and three answers. One question for the whole fetch, naming every such item: one fetch, one
        // answer. Inside the window, which already holds the game's input, so it holds none of its own.
        // Each answer is a fetch as the window already knows them, so the cart afterwards follows its usual rule (it goes once
        // a fetch has covered all of it): "Take it anyway" as with leave-one off (all of it covered, counted whole: covered),
        // "Take all but one" as with True counts off (covered only if leave-one's counts covered it: keptCovered).
        // The rules are the ones the orders were counted with, carried to the answer (LESSONS_LEARNED: a question carries
        // what was seen), so a setting changed in F1 while it shows can't make the answer fetch from other chests.
        private static void AskLastOnes(Dictionary<string, int> whole, Dictionary<string, int> kept, List<LastOne> lastOnes,
                                        ChestRules rules, bool covered, bool keptCovered, List<string> fetchedKeys)
        {
            if (lastPopup == null)
            {
                lastPopup = Ui.Box("AddToCart_LastOnes", window.transform, 0f, 0f, Width, Height, new Color(0f, 0f, 0f, 0.55f));
                GameObject panel = GUIManager.Instance.CreateWoodpanel(lastPopup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 620f, 380f, false);
                Ui.Label(panel.transform, "Take the last ones?", 20f, 16f, 580f, 34f, 22, TextAnchor.MiddleCenter, Ui.Orange, bold: true);
                lastPopupText = Ui.Label(panel.transform, string.Empty, 30f, 56f, 560f, 252f, 16, TextAnchor.UpperCenter);
                // Long names that wrap shrink the text rather than lose its last lines (review of card A83).
                lastPopupText.resizeTextForBestFit = true;
                lastPopupText.resizeTextMinSize = 11;
                lastPopupText.resizeTextMaxSize = 16;
                Ui.Button(panel.transform, "Take it anyway", 30f, 322f, 190f, 38f, () => Module.Guard("answering the pop-up", () =>
                {
                    lastPopup.SetActive(false);
                    lastAnyway?.Invoke();
                }));
                Ui.Button(panel.transform, "Take all but one", 230f, 322f, 200f, 38f, () => Module.Guard("answering the pop-up", () =>
                {
                    lastPopup.SetActive(false);
                    lastAllButOne?.Invoke();
                }));
                Ui.Button(panel.transform, "Cancel", 440f, 322f, 150f, 38f, () => Module.Guard("answering the pop-up", CancelLastOnes));
            }
            var text = new StringBuilder("This fetch would take the last one from some chests (\"Leave one of each item in chests\"):\n");
            foreach (LastOne each in lastOnes.Take(LastOnesListed))
            {
                text.Append($"\n{ItemName(each.Item)} ×{each.Wanted}: {each.Count} of them the last in {(each.Count == 1 ? "its chest" : "their chests")}");
            }
            if (lastOnes.Count > LastOnesListed)
            {
                int more = lastOnes.Count - LastOnesListed;
                text.Append($"\n… and {more} more {(more == 1 ? "item" : "items")}");
            }
            text.Append("\n\nTake all but one leaves one of each in every chest.");
            lastPopupText.text = text.ToString();
            lastAnyway = () => TakeLastOnes(whole, kept, lastOnes, rules, covered, fetchedKeys);
            lastAllButOne = () => TakeAllButOne(kept, rules, keptCovered, fetchedKeys);
            lastPopup.SetActive(true);
            lastPopup.transform.SetAsLastSibling();
        }

        private static void CancelLastOnes()
        {
            if (lastPopup != null)
            {
                lastPopup.SetActive(false);
            }
            statusText.text = "Nothing fetched.";
        }

        // "Take it anyway": all leave-one allows first, then only the last ones named (LastOnesFetch).
        private static void TakeLastOnes(Dictionary<string, int> whole, Dictionary<string, int> kept, List<LastOne> lastOnes,
                                         ChestRules seen, bool coveredAtStart, List<string> fetchedKeys)
        {
            ChestRules first = With(seen, leaveOne: true);
            ChestRules second = With(seen, leaveOne: false);
            statusText.text = "Fetching…";
            fetchButton.interactable = false;
            if (!LastOnesFetch.Start(Module.Chests, whole, kept, lastOnes, first, second,
                                     (result, left) => Fetched(result, coveredAtStart, fetchedKeys, second, left)))
            {
                statusText.text = "A fetch is under way: try again in a moment.";
                fetchButton.interactable = true;
            }
        }

        // "Take all but one": the order as leave-one allows it, one left in every chest: what a fetch does with True counts off.
        private static void TakeAllButOne(Dictionary<string, int> kept, ChestRules seen, bool coveredAtStart, List<string> fetchedKeys)
        {
            var order = kept.Where(each => each.Value > 0).ToDictionary(each => each.Key, each => each.Value);
            if (order.Count == 0)
            {
                statusText.text = "Nothing fetched: leaving one in each chest, the chests can give none of it.";
                return;
            }
            StartFetch(order, With(seen, leaveOne: true), coveredAtStart, fetchedKeys);
        }

        // A copy of the rules with leave-one as given.
        private static ChestRules With(ChestRules rules, bool leaveOne) => new ChestRules
        {
            Range = rules.Range,
            OnlyBuiltByMe = rules.OnlyBuiltByMe,
            LeaveOne = leaveOne,
            LeaveOneOfGear = rules.LeaveOneOfGear,
            Skip = rules.Skip,
            UseReserved = rules.UseReserved,
        };

        // ---- The window's own canvas ----

        /// <summary>
        /// The canvas the window is drawn on: its own, over the one Jötunn keeps for mods' windows, so it can be drawn at
        /// the game's GUI scale times the setting "Window size". The text is then drawn at that size and stays sharp,
        /// where stretching the window would blur it. Made with the window, in the game's scene, and goes with it.
        /// </summary>
        private static Transform CanvasRoot()
        {
            if (ownCanvas == null)
            {
                Canvas front = GUIManager.CustomGUIFront.GetComponent<Canvas>();
                gameCanvas = front != null ? front.rootCanvas : null;
                var holder = new GameObject("AddToCart_Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                holder.layer = GUIManager.CustomGUIFront.layer;
                // In the scene the game's GUI is in, so it goes with it (a new world builds a new one).
                if (holder.scene != GUIManager.CustomGUIFront.scene)
                {
                    SceneManager.MoveGameObjectToScene(holder, GUIManager.CustomGUIFront.scene);
                }
                ownCanvas = holder.GetComponent<Canvas>();
                ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ownCanvas.sortingOrder = 2001;   // just over Jötunn's canvas for mods' windows (2000), where it was drawn until 0.3.1
                if (front != null)
                {
                    ownCanvas.additionalShaderChannels = front.additionalShaderChannels;
                }
                if (gameCanvas != null)
                {
                    ownCanvas.pixelPerfect = gameCanvas.pixelPerfect;   // text and borders snap to pixels as the game's do (review 1)
                }
            }
            return ownCanvas.transform;
        }

        private static void FitCanvas()
        {
            if (ownCanvas == null)
            {
                return;
            }
            float game = gameCanvas != null ? gameCanvas.scaleFactor : 1f;
            float scale = game * Looks.SizeFactor(Settings.Size.Value, game, Screen.width, Screen.height, Width, Height);
            if (gameCanvas != null && !Mathf.Approximately(ownCanvas.referencePixelsPerUnit, gameCanvas.referencePixelsPerUnit))
            {
                ownCanvas.referencePixelsPerUnit = gameCanvas.referencePixelsPerUnit;   // the wood panel's borders as the game draws them
            }
            if (Mathf.Approximately(ownCanvas.scaleFactor, scale))
            {
                return;
            }
            ownCanvas.scaleFactor = scale;
            // A window dragged near an edge and then made bigger would hang off the screen: back to the middle with it.
            // It hangs from the screen's middle (its anchors and pivot), so its middle lands where its offset, scaled, says.
            if (window != null)
            {
                var rect = (RectTransform)window.transform;
                Vector2 middle = new Vector2(Screen.width / 2f, Screen.height / 2f) + rect.anchoredPosition * scale;
                float halfWidth = Width * scale / 2f;
                float halfHeight = Height * scale / 2f;
                if (middle.x - halfWidth < 0f || middle.x + halfWidth > Screen.width || middle.y - halfHeight < 0f || middle.y + halfHeight > Screen.height)
                {
                    rect.anchoredPosition = Vector2.zero;
                }
            }
        }

        // ---- Building the window, once ----

        private static void Build()
        {
            GUIManager gui = GUIManager.Instance;
            window = gui.CreateWoodpanel(CanvasRoot(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, Height, true);
            window.name = "AddToCart_Window";
            Greys.Warn = line => Module.Log.LogWarning(line);
            Greys.Info = line => Module.Log.LogInfo(line);
            Greys.Prune();
            Transform root = window.transform;
            popup = null;
            lastPopup = null;

            // Left: the browser (tabs, the list on the left, Group, Sort, the thumbnails); the hover lines under it, with
            // the thumbnail under the pointer beside them.
            Ui.Label(root, "Add to Cart", Browser.Left, 12f, 400f, 34f, 26, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Browser.Build(root);
            locateButton = Ui.Button(root, "Locate", Browser.ResultsX + Browser.ResultsWidth - 176f, Browser.TabsY, 176f, 32f,
                                     () => Module.Guard("locating", LocateClicked));
            // True counts (card A83): a checkbox and its name, as "First" is drawn, left of Locate.
            trueSwitch = Ui.Box("AddToCart_TrueCounts", root, Browser.ResultsX + Browser.ResultsWidth - 176f - 142f, Browser.TabsY, 134f, 32f);
            trueBox = Ui.Checkbox(trueSwitch.transform, 0f, 5f, 22f);
            trueBox.onValueChanged.AddListener(on => Module.Guard("switching true counts", () => TrueCountsSwitched(on)));
            Clicks boxHover = trueBox.gameObject.AddComponent<Clicks>();
            boxHover.Enter = ShowTrueHint;
            boxHover.Exit = ClearHover;
            trueLabel = Ui.Label(trueSwitch.transform, "True counts", 28f, 0f, 106f, 32f, 15, TextAnchor.MiddleLeft, Ui.Plain);
            trueLabel.raycastTarget = true;
            Clicks labelClicks = trueLabel.gameObject.AddComponent<Clicks>();
            labelClicks.Left = () =>
            {
                if (trueBox.interactable)
                {
                    trueBox.isOn = !trueBox.isOn;
                }
            };
            labelClicks.Enter = ShowTrueHint;
            labelClicks.Exit = ClearHover;
            hoverIcon = Ui.Icon(root, null, Browser.Left, Browser.GridBottom + 5f, 46f);
            hoverIcon.enabled = false;
            hoverText = Ui.Label(root, string.Empty, Browser.Left + 54f, Browser.GridBottom + 4f, RightX - Browser.Left - 74f, 50f, 15, TextAnchor.UpperLeft);
            chestsText = Ui.Label(root, string.Empty, Browser.Left, Browser.GridBottom + 56f, RightX - Browser.Left - 20f, 24f, 14, TextAnchor.MiddleLeft, Ui.Quiet);

            // Right: cart, materials, weight and room, status, buttons.
            Ui.Label(root, "Cart", RightX, 12f, RightWidth, 34f, 22, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            cartList = Ui.Scroll("Cart", root, RightX, 54f, RightWidth, 250f);
            Ui.List(cartList, 2f);

            Ui.Label(root, "Materials", RightX, 312f, 150f, 26f, 18, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Ui.Label(root, "have/need", RightX + 160f, 312f, 90f, 26f, 13, TextAnchor.MiddleRight, Ui.Quiet);
            Ui.Label(root, "chests", RightX + 250f, 312f, 50f, 26f, 13, TextAnchor.MiddleRight, Ui.Quiet);
            Ui.Label(root, "short", RightX + 302f, 312f, 50f, 26f, 13, TextAnchor.MiddleRight, Ui.Quiet);
            materialsList = Ui.Scroll("Materials", root, RightX, 340f, RightWidth, 240f);
            Ui.List(materialsList, 1f);

            weightText = Ui.Label(root, string.Empty, RightX, 584f, RightWidth, 24f, 15);
            roomText = Ui.Label(root, string.Empty, RightX, 608f, RightWidth, 24f, 15);
            statusText = Ui.Label(root, string.Empty, RightX, 634f, RightWidth, 56f, 14, TextAnchor.UpperLeft, Ui.Quiet);

            fetchButton = Ui.Button(root, "Fetch", RightX, 696f, 150f, 36f, () => Module.Guard("fetching", Fetch));
            Ui.Button(root, "Clear", RightX + 158f, 696f, 100f, 36f, () => Module.Guard("clearing the cart", () =>
            {
                Cart.Clear();
                statusText.text = string.Empty;
                ShowCart();
                Browser.RefreshCells();
                ShowMaterials();
            }));
            Ui.Button(root, "Close", RightX + 266f, 696f, 108f, 36f, () => Module.Guard("closing", CloseSoon));

            ListsPopup.Built((RectTransform)root);
            window.SetActive(false);
        }

        private static void Add(Dictionary<string, int> into, string key, int amount)
        {
            into.TryGetValue(key, out int sum);
            into[key] = sum + amount;
        }

        private static int Get(Dictionary<string, int> amounts, string key) => amounts.TryGetValue(key, out int amount) ? amount : 0;
    }
}
