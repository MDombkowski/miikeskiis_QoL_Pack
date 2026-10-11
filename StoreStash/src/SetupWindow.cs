using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;   // StoreStash's own Button (the quick stack's) would hide it

namespace StoreStash
{
    /// <summary>
    /// A chest's setup window (card A41): what the chest takes when things are quick stacked, picked from the pack's
    /// catalogue as Add to Cart's Take tab browses it (PROJECT.md rule 7): the list on the left (All, this chest's setup,
    /// the families opening to their members), under a search box, picks what shows, the headings follow the pick,
    /// progression order. Each heading that is a family, a member (Trophies' and Creature Parts' biomes too) or one of
    /// Gear's kinds says whether the chest takes it, and a click takes it, leaves it out, or (while biomes stand alone)
    /// narrows the chest to it from them ("Only this"); a right-click takes it out. A click on a thumbnail takes that thing
    /// or leaves it out of a family the chest takes; its mark (a check, or a minus for a thing left out) takes it out or
    /// back, and a right-click takes it out. A row of biomes ("From:") narrows the families and kinds. The title shows the
    /// chest's name, and renames it. It's part of the inventory screen, over the crafting panel, beside the open chest, so
    /// it scales and closes with it; every change is written into the chest at once (Setups.Write: while its window is
    /// open, the chest is his), so there is nothing to save. While one of its text boxes has the keyboard, the game's keys
    /// wait (TypingGuard).
    /// </summary>
    internal static class SetupWindow
    {
        private const float Width = 570f;
        private const float Height = 650f;
        private const float Pad = 16f;
        private const float TitleY = 12f;
        private const float SummaryY = 46f;
        private const float BiomesY = 96f;
        private const float BiomeHeight = 24f;
        private const float BiomeGap = 6f;
        private const float FromWidth = 48f;
        private const float ListY = 156f;
        private const float SearchHeight = 28f;
        private const float RailY = ListY + SearchHeight + 4f;
        private const float ListBottom = 556f;
        private const float RailWidth = 176f;
        private const float ResultsX = Pad + RailWidth + 8f;
        private const float ResultsWidth = Width - ResultsX - Pad;
        private const float RowHeight = 24f;
        private const float HeadingHeight = 28f;
        private const float GroupButtonWidth = 128f;
        private const float FromButtonWidth = 92f;
        private const float QuickRowHeight = 26f;
        private const float QuickIndent = 18f;
        private const float ChipHeight = 22f;
        private const float ChipGap = 4f;
        private const float CellSize = 56f;
        private const float CellGap = 6f;
        private const float MarkSize = 20f;
        private const int Columns = 5;
        private const float HoverY = 562f;
        private const float ButtonsY = 606f;

        /// <summary>What the line at the bottom says when nothing is under the pointer (two lines at most).</summary>
        private const string Hint =
            "In the lists, a click takes a thing or leaves it out; a right-click or its check takes it out; a minus takes it " +
            "back. From: narrows every group; a group's own biome button narrows only it.";

        private static readonly string[] BiomeChoices = Taxonomy.Biomes.Concat(new[] { Taxonomy.OtherBiome }).ToArray();

        private static GameObject window;
        private static Text titleText;
        private static InputField nameField;
        private static Text nameLabel;
        private static Text summaryText;
        private static Text hoverText;
        private static Image hoverIcon;
        private static InputField searchBox;
        private static GameObject clearSearch;
        private static RectTransform rail;
        private static RectTransform grid;
        private static UIButton copyButton;
        private static UIButton pasteButton;
        private static readonly List<(string Biome, UIButton Button)> BiomeButtons = new List<(string, UIButton)>();
        private static readonly List<CellView> Cells = new List<CellView>();
        private static readonly List<GroupView> Groups = new List<GroupView>();

        // A group's own biomes (card A81): the group whose row of biomes is open under its heading (one at a time), whether
        // that heading is the page's own line, and the row's biome buttons.
        private static ItemPick? biomesOpen;
        private static bool biomesOpenOnPage;
        private static readonly List<(string Biome, UIButton Button)> Chips = new List<(string, UIButton)>();

        private static Container chest;
        private static ItemPicks picks;
        private static string shownText;
        private static List<Listing> listings;
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Selection pick = Selection.All;
        private static bool setupView = true;
        private static readonly HashSet<string> OpenFamilies = new HashSet<string>();
        private static bool relist;
        private static bool dirty;
        private static bool scrollUp;
        private static bool shownKeepHeld;

        // The chest's name (decision 27): as the title last showed it ("" for none), the chest's kind in his language, what
        // the name box showed (so what it held when he began typing), what he left the box with (applied the next frame;
        // null: nothing waiting) over what it showed then, and whether a box's edit just ended (it's let go of next frame).
        private static string shownName = string.Empty;
        private static string kindName = string.Empty;
        private static string boxShowed = string.Empty;
        private static string typedName;
        private static string typedOver = string.Empty;
        private static bool boxLetGo;

        // The search (decision 24), and the chest it was typed for: the box empties for another chest.
        private static readonly SetupSearch SearchState = new SetupSearch();
        private static Container searchedIn;

        // The game's keys, held while a box has the keyboard (decision 24).
        private static readonly TypingGuard KeyGuard = new TypingGuard(on => GUIManager.BlockInput(on));

        /// <summary>True while the window is open for a chest (shown once the chest is his: see Update).</summary>
        internal static bool IsOpen => window != null && chest != null;

        /// <summary>The chest it's open for, or null.</summary>
        internal static Container Chest => IsOpen ? chest : null;

        /// <summary>Every frame, from Module.Update: holds the game's keys while a box here has the keyboard (and gives them
        /// back the frame after), closes the window once its chest's window is gone (or setups are off), shows it only
        /// while the chest is his (the game shows a chest's window as its owner's yes arrives, before the ownership does,
        /// and hides its panel till then), renames the chest as he typed, follows a name or setup changed another way (a
        /// friend, MyLittleUI, the paste key), searches once he pauses typing, and scrolls.</summary>
        internal static void Update()
        {
            // First, whatever else this frame brings (also once the window went with its scene).
            KeyGuard.Tick(Typing(), Time.frameCount);
            if (ReferenceEquals(chest, null))
            {
                return;
            }
            if (window == null || !SetupSettings.Active || !StillOpen())
            {
                Close();   // gone with the inventory after a world change, switched off, or its chest's window closed
                return;
            }
            bool mine = chest.IsOwner();
            if (window.activeSelf != mine)
            {
                window.SetActive(mine);
                if (mine)
                {
                    Load();   // the chest's newest record came with the ownership
                    dirty = true;
                }
                else
                {
                    typedName = null;   // a name typed as the chest went to someone else goes with the window
                }
            }
            if (!mine)
            {
                return;
            }
            if (boxLetGo)
            {
                boxLetGo = false;
                LetGoOfBoxes();
            }
            ShowNameBox();
            ApplyName();
            FollowName();
            string now = ChestSetup.Read(chest);
            if (now != shownText)
            {
                // Changed another way (the paste key): drawn afresh, so "This chest's setup" lists what it takes now.
                Load();
                dirty = true;
            }
            if (shownKeepHeld != SetupSettings.KeepHeld.Value)
            {
                dirty = true;   // the top line's ending follows the setting
            }
            if (SearchState.Due(Time.time))
            {
                FlushSearch();
                dirty = true;
                scrollUp = true;
            }
            ShowClearSearch();
            SetupUi.Wheel(2f, CellSize + CellGap, grid, rail);
            if (relist)
            {
                relist = false;
                dirty = false;
                ListItems();
                Render();
            }
            else if (dirty)
            {
                dirty = false;
                Render();
            }
            if (scrollUp)
            {
                scrollUp = false;
                SetupUi.ScrollToTop(grid);
            }
        }

        /// <summary>Opens the window for the chest whose window is open, or closes it if it shows that chest already.</summary>
        internal static void Toggle(Container which)
        {
            if (IsOpen && chest == which)
            {
                CloseClicked();
            }
            else
            {
                Open(which);
            }
        }

        /// <summary>Opens the window for <paramref name="which"/>, whose window must be open (the chest is then his).</summary>
        internal static void Open(Container which)
        {
            if (which != null && SetupSettings.Active && !QoLMods.Chests.ChestAccess.MayTouch(which))
            {
                Messages.Center($"{Mine.Whose(which)} personal chest: look, don't touch.");   // card A80: its setup is theirs
                return;
            }
            InventoryGui gui = InventoryGui.instance;
            if (!SetupSettings.Active || which == null || gui == null || !Setups.Settable(which) || GameHooks.OpenChest(gui) != which ||
                GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                return;
            }
            if (window == null)   // Unity's own test: gone with the inventory after a world change
            {
                Build(gui);
                if (window == null)
                {
                    return;
                }
            }
            Dock(gui);
            if (!ReferenceEquals(searchedIn, which))
            {
                // Another chest: an empty search box.
                searchedIn = which;
                SearchState.Clear();
                searchBox.SetTextWithoutNotify(string.Empty);
            }
            chest = which;
            Load();
            setupView = true;   // each chest opens on what it takes
            biomesOpen = null;
            shownName = SetupName.Read(which);
            kindName = GameFacts.Localize(which.m_name);
            typedName = null;
            boxLetGo = false;
            ShowNameBox();
            ShowName();
            // Listed afresh each time it opens: what he has found since shows.
            relist = false;
            ListItems();
            Render();
            SetupUi.ScrollToTop(grid);
            window.SetActive(which.IsOwner());   // else shown by Update once the chest is his
            window.transform.SetAsLastSibling();
            dirty = false;
            scrollUp = false;
        }

        /// <summary>Closes the window, and gives the game's keys back if a box had them. Also the module's undo step, and
        /// what switching setups off does. A name being typed is dropped (the X and Done keep it: CloseClicked).</summary>
        internal static void Close()
        {
            KeyGuard.Release();   // first, whatever hiding the window brings (review round 1)
            if (window != null)
            {
                window.SetActive(false);   // a box with the keyboard lets go here: its onEndEdit only notes what was typed
            }
            LetGoOfBoxes();   // not left selected while hidden, to take Enter as the window opens again
            typedName = null;
            boxLetGo = false;
            SearchState.Apply(out _);   // a search still waiting counts, so box and list agree when it opens again
            chest = null;
            picks = null;
            shownText = null;
            biomesOpen = null;
        }

        /// <summary>A setting that changes what the window lists or shows: it follows at once (the Copy and Paste buttons as
        /// it's drawn, Refresh).</summary>
        internal static void SettingsChanged()
        {
            relist = true;
        }

        // The X and Done: a name typed just before is kept (the click took the keyboard from the box, whose end came
        // first), then the window closes.
        private static void CloseClicked()
        {
            ApplyName();
            Close();
        }

        // The chest's window is still open on this chest, and it can still carry a setup (carts' setting).
        private static bool StillOpen()
        {
            InventoryGui gui = InventoryGui.instance;
            return chest != null && gui != null && InventoryGui.IsVisible() && GameHooks.OpenChest(gui) == chest && Setups.Settable(chest);
        }

        // ---- The keyboard (decisions 24 and 27)

        // Whether one of the window's text boxes has the keyboard now.
        private static bool Typing() =>
            window != null && window.activeInHierarchy &&
            ((nameField != null && nameField.isFocused) || (searchBox != null && searchBox.isFocused));

        // After a box's edit ends, the box isn't left selected: the UI's submit (Enter) would give it the keyboard again.
        private static void LetGoOfBoxes()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null || events.alreadySelecting)
            {
                return;
            }
            if ((nameField != null && selected == nameField.gameObject && !nameField.isFocused) ||
                (searchBox != null && selected == searchBox.gameObject && !searchBox.isFocused))
            {
                events.SetSelectedGameObject(null);
            }
        }

        // ---- The chest's name (decision 27)

        // The box to rename it while renaming is on (the setting, followed at once); else the name as a plain label. A box
        // switched off while he types in it ends its edit, which is then dropped (ApplyName reads the setting).
        private static void ShowNameBox()
        {
            bool rename = SetupSettings.Rename.Value;
            if (nameField != null && nameField.gameObject.activeSelf != rename)
            {
                nameField.gameObject.SetActive(rename);
            }
            if (nameLabel != null && nameLabel.gameObject.activeSelf == rename)
            {
                nameLabel.gameObject.SetActive(!rename);
            }
        }

        // The title's name: the chest's own, or its kind while it has none. The box gets it only while he isn't typing.
        private static void ShowName()
        {
            string text = shownName.Length > 0 ? shownName : kindName;
            if (nameLabel != null)
            {
                nameLabel.text = text;
            }
            if (nameField != null && !nameField.isFocused)
            {
                nameField.SetTextWithoutNotify(text);
                boxShowed = nameField.text;   // as the box holds it (at most its 32 characters)
            }
        }

        // The name box's edit ended: Enter, a click elsewhere, Esc, or the box switched off with its window (Unity's
        // InputField.OnDisable). Only noted here, and applied the next frame (ApplyName): never while the window closes.
        private static void NameEdited(string text)
        {
            boxLetGo = true;
            if (nameField != null && nameField.wasCanceled)
            {
                typedName = null;   // Esc: Unity has put the name back as it was, and nothing is written
                return;
            }
            typedName = text ?? string.Empty;
            typedOver = boxShowed;
        }

        // What he typed in the name box, the frame after he left it (or as X or Done closes the window): written into the
        // chest if it changes its name (SetupName.ToWrite). Not his chest just now: he's told, and the box shows the name
        // the chest has.
        private static void ApplyName()
        {
            string typed = typedName;
            typedName = null;
            if (typed == null || chest == null || !SetupSettings.Rename.Value)
            {
                return;
            }
            // Against the name as the record holds it (review round 2 of the last look: a name made only of characters the
            // window doesn't show can still be cleared).
            string name = SetupName.ToWrite(typed, typedOver, SetupName.Raw(chest), kindName);
            if (name == null && typed != typedOver && SetupName.ShowsNothing(typed))
            {
                // No dollar sign in it: the game's messages translate every word after one (review round 5).
                Messages.Center("Nothing in that name can be shown (tags, angle brackets, dollar signs and blank characters are left out), so its name stays as it was.");
            }
            if (name != null)
            {
                if (SetupName.Write(chest, name))
                {
                    shownName = SetupName.Read(chest);
                }
                else
                {
                    Messages.Center("Couldn't rename the chest: it isn't yours right now. Try again in a moment.");
                }
            }
            ShowName();
        }

        // A name given elsewhere (MyLittleUI, a friend): the title follows, unless he's typing in the box. One read of the
        // chest's record a frame.
        private static void FollowName()
        {
            if (typedName != null || (nameField != null && nameField.isFocused))
            {
                return;
            }
            string now = SetupName.Read(chest);
            if (now != shownName)
            {
                shownName = now;
                ShowName();
            }
        }

        // ---- The setup as shown

        // Reads the chest's setup afresh into the copy the window changes.
        private static void Load()
        {
            shownText = ChestSetup.Read(chest);
            ItemPicks read = ItemPicks.Parse(shownText);
            picks = read == null ? new ItemPicks() : read.Copy();
        }

        // After a change of his: written into the chest at once, then read back from it (so what shows is what the chest
        // holds: excepts left over from a setup emptied are gone with it), and shown. False when the chest wasn't his.
        private static bool Changed()
        {
            bool hadSetup = !string.IsNullOrEmpty(shownText);
            bool written = Setups.Write(chest, picks.IsEmpty ? null : picks);
            if (!written)
            {
                Messages.Center("Couldn't change the setup: the chest isn't yours right now. Try again in a moment.");
            }
            Load();
            ShowSetupCount();
            if (biomesOpen.HasValue && !OwnBiomesShown(picks, biomesOpen.Value, biomesOpenOnPage))
            {
                // Its group can't have biomes of its own now (unpicked, left out): its row of biomes goes, and only it, so
                // "This chest's setup" keeps listing what was just taken out, to take back (review n1).
                CloseChipRow();
            }
            Refresh();
            if (written && hadSetup && string.IsNullOrEmpty(shownText))
            {
                // Its last pick taken out (review round 1): said, as Clear says it.
                Messages.Center("Nothing is picked any more: it's an ordinary chest again.");
            }
            return written;
        }

        // Before a click changes anything: the setup can be changed (not a newer version's; one too long to follow only
        // cleared), the chest is his, and what shows is what it holds (read in the frame the ownership came, or changed
        // since); otherwise the click only brings the window up to date.
        private static bool CanChange(bool clearing = false)
        {
            if (chest == null || !chest.IsOwner())
            {
                Messages.Center("Couldn't change the setup: the chest isn't yours right now. Try again in a moment.");
                return false;
            }
            if (ChestSetup.Read(chest) != shownText)
            {
                Load();
                Render();
                return false;
            }
            if (picks != null && (picks.Readable || (clearing && picks.TooLong)))
            {
                return true;
            }
            Messages.Center(picks != null && picks.TooLong
                ? "This chest's setup is longer than a setup can be (another mod's, or another version's): Clear gives it a new one."
                : "This chest was set up by a newer version of the pack: update StoreStash to change it.");
            return false;
        }

        // ---- What it lists

        // Every item this character may pick: one per kind of item (the game's own name), with its picture, classified by
        // the catalogue as Add to Cart's Take tab classifies it, with the words the search box finds it by (Listings.Item:
        // its name, family, member, kind, type and biome). Only what he has found, while that setting is on.
        private static void ListItems()
        {
            listings = new List<Listing>();
            Icons.Clear();
            ObjectDB db = ObjectDB.instance;
            Player player = Player.m_localPlayer;
            if (db == null)
            {
                return;
            }
            Biomes.Build(line => Module.Log.LogInfo(line));   // once per world; its log line the first time
            bool hideUnknown = SetupSettings.HideUnknown.Value;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData shared = drop != null ? drop.m_itemData?.m_shared : null;
                string name = shared?.m_name;
                if (string.IsNullOrEmpty(name) || name.IndexOf('|') >= 0 || !seen.Add(name))
                {
                    continue;
                }
                if (hideUnknown && player != null && !player.IsKnownMaterial(name))
                {
                    continue;
                }
                GameObject canonical = GameFacts.Canonical(name);
                ItemDrop shown = canonical != null ? canonical.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData data = (shown != null ? shown : drop).m_itemData;
                if (data?.m_shared?.m_icons == null || data.m_shared.m_icons.Length == 0)
                {
                    continue;   // nothing a player can carry
                }
                listings.Add(Listings.Item(Shelf.Take, name, Setups.ItemName(name), name, null, string.Empty, 1));
                Icons[name] = data.m_shared.m_icons[0];
            }
        }

        // ---- The search (decision 24)

        // A search waiting for him to pause runs now (he paused, or clicked the list: it goes first, so it can't undo his
        // click). One that starts from an empty box shows everything (All), as Add to Cart's filter does. True if one was
        // waiting.
        private static bool FlushSearch()
        {
            if (!SearchState.Apply(out bool starts))
            {
                return false;
            }
            if (starts)
            {
                setupView = false;
                pick = Selection.All;
            }
            return true;
        }

        // The search box's x: shown while the box holds something.
        private static void ShowClearSearch()
        {
            bool any = searchBox != null && searchBox.text.Length > 0;
            if (clearSearch != null && clearSearch.activeSelf != any)
            {
                clearSearch.SetActive(any);
            }
        }

        private static void ClearSearch()
        {
            if (searchBox != null)
            {
                searchBox.SetTextWithoutNotify(string.Empty);
            }
            SearchState.Clear();
            Render();
            SetupUi.ScrollToTop(grid);
        }

        /// <summary>Whether a thing shows on the right: in what's picked on the left (in "This chest's setup", in the setup:
        /// <paramref name="inSetup"/>), with every word typed in the search box in its name, family, member, kind or biome
        /// (the catalogue's filter, Browse.Matches, on the words Listings.Item gave it).</summary>
        internal static bool Shows(Listing e, bool setupView, Selection pick, Func<Listing, bool> inSetup, string query) =>
            (setupView ? inSetup(e) : Browse.InPick(e, pick, null, null)) && Browse.Matches(e, query);

        // ---- Drawing

        private static void Render()
        {
            RenderRail();
            RenderGrid();
            Refresh();
        }

        // The list on the left: All, this chest's setup, then the families, each opening to its members; while searching,
        // each counts what matches (and every family shows, as Add to Cart's).
        private static void RenderRail()
        {
            SetupUi.Clear(rail);
            RailRow("All", Count(Selection.All), !setupView && pick.Pick == Pick.All, false, null, () => ShowPick(Selection.All));
            setupRowText = RailRow(SetupRowLabel, SetupCount(), setupView, false, null, ShowSetup);
            List<TreeBlock> tree = Browse.Tree(Shelf.Take, listings, Orders.Default);
            foreach (KeyValuePair<string, List<ShownRow>> block in Browse.Shown(tree, Count, SearchState.Query.Length > 0, true, OpenFamilies))
            {
                GameObject title = SetupUi.Row(rail, RowHeight);
                SetupUi.Label(title.transform, block.Key, 6f, 0f, RailWidth - 20f, RowHeight, 13, TextAnchor.LowerLeft, SetupUi.Quiet, bold: true);
                foreach (ShownRow row in block.Value)
                {
                    ShownRow each = row;
                    string mark = each.OpenId == null ? null : each.Open ? "[-] " : "[+] ";
                    RailRow(each.Row.Label, each.Count, !setupView && pick.Equals(each.Row.Pick), each.Member, mark, () => RailClicked(each));
                }
            }
        }

        private static Text RailRow(string label, int count, bool picked, bool member, string mark, Action click)
        {
            GameObject row = SetupUi.Row(rail, RowHeight, picked ? SetupUi.RowPicked : SetupUi.RowPlain);
            Image background = row.GetComponent<Image>();
            background.raycastTarget = true;
            SetupClicks clicks = row.AddComponent<SetupClicks>();
            clicks.Left = click;
            clicks.Enter = () => { if (!picked) { background.color = SetupUi.RowHover; } };
            clicks.Exit = () => background.color = picked ? SetupUi.RowPicked : SetupUi.RowPlain;
            float indent = member ? 22f : 6f;
            return SetupUi.Label(row.transform, (mark ?? string.Empty) + label + $" ({count})", indent, 0f, RailWidth - indent - 14f, RowHeight, 14,
                                 TextAnchor.MiddleLeft, picked ? SetupUi.Plain : SetupUi.Quiet);
        }

        private const string SetupRowLabel = "This chest's setup";

        private static Text setupRowText;

        // After a change: only this row's count can have changed, so only it is written again (the list isn't rebuilt).
        private static void ShowSetupCount()
        {
            if (setupRowText != null)
            {
                setupRowText.text = $"{SetupRowLabel} ({SetupCount()})";
            }
        }

        private static int Count(Selection which) => Matching().Count(e => Browse.InPick(e, which, null, null));

        private static int SetupCount() => Matching().Count(InSetupView);

        private static List<Listing> matching;
        private static List<Listing> matchedFrom;
        private static string matchedQuery;

        // What the search finds of everything listed, worked out once per search (the list's counts ask it for every row).
        private static List<Listing> Matching()
        {
            string query = SearchState.Query;
            if (matching == null || !ReferenceEquals(matchedFrom, listings) || matchedQuery != query)
            {
                matchedFrom = listings;
                matchedQuery = query;
                matching = query.Length == 0 ? listings : listings.Where(e => Browse.Matches(e, query)).ToList();
            }
            return matching;
        }

        private static void RailClicked(ShownRow row)
        {
            if (row.OpenId != null)
            {
                // A family opens as it's picked, and a click on the family already picked closes it again.
                bool pickedAlready = !setupView && pick.Equals(row.Row.Pick);
                if (!row.Open)
                {
                    OpenFamilies.Add(row.OpenId);
                }
                else if (pickedAlready)
                {
                    OpenFamilies.Remove(row.OpenId);
                }
            }
            ShowPick(row.Row.Pick);
        }

        private static void ShowPick(Selection to)
        {
            FlushSearch();   // a search still waiting goes first, and can't undo this pick
            setupView = false;
            pick = to;
            biomesOpen = null;
            Render();
            SetupUi.ScrollToTop(grid);
        }

        private static void ShowSetup()
        {
            FlushSearch();
            setupView = true;
            biomesOpen = null;
            Render();
            SetupUi.ScrollToTop(grid);
        }

        // In "This chest's setup": what it takes, and what it names by name to leave out.
        private static bool InSetupView(Listing e) =>
            picks != null && (picks.Fit(e.Key) > 0 || picks.Has(ItemPick.OfItem(e.Key)) || picks.Has(ItemPick.OfItem(e.Key), except: true));

        // The thumbnails under their headings, for what is picked on the left and searched for.
        private static void RenderGrid()
        {
            SetupUi.Clear(grid);
            Cells.Clear();
            Groups.Clear();
            Chips.Clear();
            chipRow = null;
            chipsPlaced = false;
            string query = SearchState.Query;
            IEnumerable<Listing> rows = listings.Where(e => Shows(e, setupView, pick, InSetupView, query));
            Dimension dimension;
            string whole;
            if (setupView)
            {
                dimension = Browse.Family(Orders.Default);
                whole = "This chest's setup";
                // The quick picks it takes or leaves out (card A81, review n2): always here, to take them out again, whatever
                // the setting, the search or what has been found.
                List<QuickPick> held = Taxonomy.QuickPicks.Where(quick => QuickHeld(picks, quick)).ToList();
                if (held.Count > 0)
                {
                    Heading("Quick picks", null);
                    foreach (QuickPick quick in held)
                    {
                        QuickRow(quick);
                    }
                }
            }
            else
            {
                dimension = Browse.AutoDimension(Shelf.Take, pick, Orders.Default);
                whole = Browse.Label(pick, null);
                // The pick itself, when it's a family or a member of one: one line to take it whole.
                ItemPick? itself = pick.Pick == Pick.Family ? ItemPick.OfFamily(pick.Value)
                    : pick.Pick == Pick.Member ? ItemPick.OfMember(pick.Value, pick.Member)
                    : (ItemPick?)null;
                if (itself.HasValue)
                {
                    Heading("All of " + (pick.Pick == Pick.Member ? pick.Member : pick.Value), itself, page: true);
                    // Then its quick picks (card A81).
                    foreach (QuickPick quick in QuickPicksOn(pick))
                    {
                        bool found = listings.Any(e => e.Item != null && e.Item.Family == quick.Family && e.Item.InGroup(quick.Label));
                        if (QuickRowShown(QuickHeld(picks, quick), SetupSettings.QuickPicks.Value, query.Length > 0, found))
                        {
                            QuickRow(quick);
                        }
                    }
                }
            }
            List<KeyValuePair<string, List<Listing>>> sections = Browse.Sections(rows, dimension, whole, Browse.Sorter(Browse.Progression));
            if (sections.Count == 0)
            {
                Note(EmptyNote(query));
            }
            foreach (KeyValuePair<string, List<Listing>> section in sections)
            {
                Heading(section.Key, HeadingGroup(setupView, pick, dimension, section.Key));
                ThumbnailRows(section.Value);
            }
            if (!chipsPlaced)
            {
                biomesOpen = null;   // its heading isn't on this page, or can't have biomes of its own now
            }
        }

        private static bool chipsPlaced;

        /// <summary>Whether a setup takes a quick pick itself or leaves it out (card A81).</summary>
        internal static bool QuickHeld(ItemPicks picks, QuickPick quick)
        {
            if (picks == null)
            {
                return false;
            }
            ItemPick group = ItemPick.OfGroup(quick.Family, quick.Label);
            return picks.Has(group) || picks.Has(group, except: true);
        }

        /// <summary>Whether a quick pick's row shows on its page (card A81): one the setup takes or leaves out, always (so
        /// it can be taken out again, review n2); any other while the setting shows quick picks, no search narrows the page,
        /// and something in it is listed (the browsing rule: only choices that change something).</summary>
        internal static bool QuickRowShown(bool held, bool setting, bool searching, bool found) => held || (setting && !searching && found);

        /// <summary>The quick picks a page of the list on the left shows under its own line (card A81): a family's page,
        /// every quick pick of the family; a member's page, those inside that member; any other page, none.</summary>
        internal static IEnumerable<QuickPick> QuickPicksOn(Selection page)
        {
            foreach (QuickPick quick in Taxonomy.QuickPicks)
            {
                if ((page.Pick == Pick.Family && quick.Family == page.Value) ||
                    (page.Pick == Pick.Member && quick.Family == page.Value && quick.Member.Length > 0 && quick.Member == page.Member))
                {
                    yield return quick;
                }
            }
        }

        // What the right side says when it shows nothing.
        private static string EmptyNote(string query)
        {
            if (query.Length == 0)
            {
                if (!setupView)
                {
                    return "Nothing here that you have found yet.";
                }
                // Only an empty setup is an ordinary chest (review round 2 of the last look): one whose picks come only from
                // biomes its From: row leaves out, or that takes only things not listed here, is still a set-up chest; and one
                // this version can't read says so (review round 3).
                if (picks != null && !picks.Readable)
                {
                    return "This version can't follow this chest's setup: the line at the top says why.";
                }
                return picks == null || picks.IsEmpty
                    ? "Nothing picked yet: pick a family, a kind or single things from the list on the left. Until then this is an ordinary chest."
                    : "Nothing it takes is listed here: what it picks comes only from biomes its From: row leaves out, or isn't listed " +
                      "(not found yet, or another mod's). The line at the top says what it takes.";
            }
            int all = Matching().Count;
            if ((setupView || pick.Pick != Pick.All) && all > 0)
            {
                return $"Nothing matches \"{query}\" here. All has {all} for it.";
            }
            return $"Nothing matches \"{query}\"." + (SetupSettings.HideUnknown.Value ? " Only what this character has found is listed." : string.Empty);
        }

        /// <summary>
        /// The group a heading names, if it's one that can be picked whole: a family (in "This chest's setup", and under All,
        /// by family), a member (a family's headings), one of Gear's kinds (a member's headings), or, in Trophies and
        /// Creature Parts, whose headings are biomes, the member that biome is (the catalogue files each of their things
        /// under its biome of origin: Setups.IsBiomeFamily). Other headings have none.
        /// </summary>
        internal static ItemPick? HeadingGroup(bool setupView, Selection pick, Dimension dimension, string key)
        {
            if (setupView || dimension.Id == "family")
            {
                return ItemPick.OfFamily(key);
            }
            switch (dimension.Id)
            {
                case "section" when pick.Pick == Pick.Family:
                    return ItemPick.OfMember(pick.Value, key);
                case "sub" when pick.Pick == Pick.Member:
                    return ItemPick.OfKind(pick.Value, pick.Member, key);
                case "biome" when pick.Pick == Pick.Family && Setups.IsBiomeFamily(pick.Value):
                    return ItemPick.OfMember(pick.Value, key);
                default:
                    return null;
            }
        }

        // A heading: its label, and for a group that can be picked whole its buttons (GroupButtons). The page's own line
        // ("All of Weapons") is <paramref name="page"/>: its biome button shows before it's taken too (card A81).
        private static void Heading(string label, ItemPick? group, bool page = false)
        {
            GameObject row = SetupUi.Row(grid, HeadingHeight);
            Text text = SetupUi.Label(row.transform, label, 4f, 0f, LabelWidth(4f, false), HeadingHeight, 16, TextAnchor.MiddleLeft,
                                      SetupUi.Orange, bold: true);
            if (group.HasValue)
            {
                GroupButtons(row, text, group.Value, page, HeadingHeight, 4f);
            }
        }

        // A quick pick's row (card A81), under its page's own line: its label, set in, and its buttons as a heading's;
        // pointing at it says what it is.
        private static void QuickRow(QuickPick quick)
        {
            GameObject row = SetupUi.Row(grid, QuickRowHeight, SetupUi.RowPlain);
            Image background = row.GetComponent<Image>();
            background.raycastTarget = true;
            Text text = SetupUi.Label(row.transform, quick.Label, QuickIndent, 0f, LabelWidth(QuickIndent, false), QuickRowHeight, 14,
                                      TextAnchor.MiddleLeft, SetupUi.Plain, bold: true);
            SetupClicks clicks = row.AddComponent<SetupClicks>();
            clicks.Enter = () =>
            {
                background.color = SetupUi.RowHover;
                ShowQuickHover(quick);
            };
            clicks.Exit = () =>
            {
                background.color = SetupUi.RowPlain;
                ClearHover();
            };
            GroupButtons(row, text, ItemPick.OfGroup(quick.Family, quick.Label), false, QuickRowHeight, QuickIndent);
        }

        // A group's buttons on its heading's row: the one that takes it, leaves it out or takes it back (a right-click takes
        // it out), and for a group that can have biomes of its own its biome button left of it (shown while it can, as the
        // setup stands: ShowGroup), with its row of biomes under the heading while that's open.
        private static void GroupButtons(GameObject row, Text label, ItemPick each, bool page, float height, float labelX)
        {
            float right = ResultsWidth - 12f - 8f;
            UIButton button = SetupUi.Button(row.transform, string.Empty, right - GroupButtonWidth, 3f, GroupButtonWidth, height - 6f,
                                              () => Module.Guard("changing a chest's setup", () => GroupClicked(each)));
            // A right-click on it takes the group out: the button keeps its left click (SetupClicks adds only this).
            button.gameObject.AddComponent<SetupClicks>().Right = () => GroupRightClicked(each);
            Text text = SetupUi.TextOf(button);
            if (text != null)
            {
                // 13 points, smaller only for the group's name while searching ("Take all Hides and Pelts").
                text.fontSize = 13;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 9;
                text.resizeTextMaxSize = 13;
            }
            var view = new GroupView
            {
                Pick = each,
                Button = button,
                Text = text,
                Label = label,
                Page = page,
                LabelWide = LabelWidth(labelX, false),
                LabelNarrow = LabelWidth(labelX, true),
            };
            if (CanHaveOwnBiomes(each))
            {
                UIButton from = SetupUi.Button(row.transform, string.Empty, right - GroupButtonWidth - 6f - FromButtonWidth, 3f, FromButtonWidth, height - 6f,
                                                () => Module.Guard("showing a group's own biomes", () => FromClicked(each, page)));
                SetupClicks hover = from.gameObject.AddComponent<SetupClicks>();
                hover.Enter = () => ShowFromHover(each);
                hover.Exit = ClearHover;
                Text fromText = SetupUi.TextOf(from);
                if (fromText != null)
                {
                    fromText.fontSize = 12;
                    fromText.resizeTextForBestFit = true;
                    fromText.resizeTextMinSize = 9;
                    fromText.resizeTextMaxSize = 12;
                }
                from.gameObject.SetActive(false);
                view.From = from;
                view.FromText = fromText;
            }
            Groups.Add(view);
            if (biomesOpen.HasValue && biomesOpen.Value.Equals(each) && biomesOpenOnPage == page && picks != null && OwnBiomesShown(picks, each, page))
            {
                ChipRow(each);
            }
        }

        // A heading's label width from its left edge: up to its group's button (as before card A81), or, while its biome
        // button shows, up to that.
        private static float LabelWidth(float x, bool beforeBiomeButton)
        {
            float right = ResultsWidth - 12f - 8f - GroupButtonWidth - 4f;
            return (beforeBiomeButton ? right - FromButtonWidth - 6f : right) - x;
        }

        // The row of biomes of a group's own (card A81), under its heading, in the From: row's order (two rows of five).
        private static void ChipRow(ItemPick group)
        {
            GameObject row = SetupUi.Row(grid, 2f * ChipHeight + ChipGap + 6f, new Color(0f, 0f, 0f, 0.25f));
            chipRow = row;
            float left = QuickIndent;
            float width = ResultsWidth - 12f - 8f - left;
            float chipWidth = (width - 4f * ChipGap) / 5f;
            for (int i = 0; i < BiomeChoices.Length; i++)
            {
                string biome = BiomeChoices[i];
                float x = left + (i % 5) * (chipWidth + ChipGap);
                float y = 3f + (i / 5) * (ChipHeight + ChipGap);
                UIButton chip = SetupUi.Button(row.transform, biome, x, y, chipWidth, ChipHeight,
                                                () => Module.Guard("changing a group's own biomes", () => OwnBiomeClicked(group, biome)));
                SetupClicks hover = chip.gameObject.AddComponent<SetupClicks>();
                hover.Enter = () => ShowChipHover(group, biome);
                hover.Exit = ClearHover;
                Text text = SetupUi.TextOf(chip);
                if (text != null)
                {
                    text.fontSize = 11;
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 8;
                    text.resizeTextMaxSize = 11;
                }
                Chips.Add((biome, chip));
            }
            chipsPlaced = true;
        }

        private static GameObject chipRow;

        // The open row of a group's own biomes closed alone, the rest of the list as it is.
        private static void CloseChipRow()
        {
            biomesOpen = null;
            Chips.Clear();
            if (chipRow != null)
            {
                chipRow.SetActive(false);   // gone from the layout this frame; destroyed at the end of it
                UnityEngine.Object.Destroy(chipRow);
            }
            chipRow = null;
        }

        private static void Note(string text)
        {
            GameObject row = SetupUi.Row(grid, 60f);
            SetupUi.Label(row.transform, text, 6f, 4f, ResultsWidth - 30f, 52f, 14, TextAnchor.UpperLeft, SetupUi.Quiet);
        }

        private static void ThumbnailRows(List<Listing> things)
        {
            GameObject row = null;
            for (int i = 0; i < things.Count; i++)
            {
                if (i % Columns == 0)
                {
                    row = SetupUi.Row(grid, CellSize + 2f);
                }
                Listing listing = things[i];
                float x = 4f + (i % Columns) * (CellSize + CellGap);
                GameObject cell = SetupUi.Box("StoreStash_SetupCell", row.transform, x, 1f, CellSize, CellSize, SetupUi.Cell);
                Image background = cell.GetComponent<Image>();
                background.raycastTarget = true;
                Icons.TryGetValue(listing.Key, out Sprite sprite);
                Image icon = SetupUi.Icon(cell.transform, sprite, 4f, 4f, CellSize - 8f);
                var view = new CellView { Listing = listing, Cell = cell, Background = background, Icon = icon, Picture = sprite };
                SetupClicks clicks = cell.AddComponent<SetupClicks>();
                clicks.Left = () => CellClicked(view);
                clicks.Right = () => CellRightClicked(view);
                clicks.Enter = () => ShowHover(listing, sprite);
                clicks.Exit = ClearHover;
                // The mark is made only for a thumbnail that needs one (ShowCell): most never do.
                Cells.Add(view);
            }
        }

        // Every cell, heading, biome and line as the setup stands now, without building anything again.
        private static void Refresh()
        {
            if (picks == null)
            {
                return;
            }
            shownKeepHeld = SetupSettings.KeepHeld.Value;
            summaryText.text = Setups.Sentence(picks, shownKeepHeld);
            bool buttons = SetupSettings.CopyButtons.Value;   // the setting, as it is now
            if (copyButton.gameObject.activeSelf != buttons)
            {
                copyButton.gameObject.SetActive(buttons);
                pasteButton.gameObject.SetActive(buttons);
            }
            foreach (CellView cell in Cells)
            {
                ShowCell(cell);
            }
            foreach (GroupView group in Groups)
            {
                ShowGroup(group);
            }
            foreach ((string biome, UIButton button) in BiomeButtons)
            {
                ShowBiomeButton(button, picks.InBiomes.Contains(biome));
            }
            // An open row of a group's own biomes (card A81): lit as the From: row's are.
            IReadOnlyList<string> own = biomesOpen.HasValue ? picks.OwnBiomesOf(biomesOpen.Value) : null;
            foreach ((string biome, UIButton chip) in Chips)
            {
                ShowBiomeButton(chip, own != null && own.Contains(biome));
            }
        }

        private static void ShowBiomeButton(UIButton button, bool on)
        {
            button.image.color = on ? new Color(1f, 0.82f, 0.45f, 1f) : Color.white;
            Text text = SetupUi.TextOf(button);
            if (text != null)
            {
                text.color = on ? SetupUi.Orange : SetupUi.Quiet;
            }
        }

        // A thumbnail as the setup takes its thing: its colours, and its mark (decision 25): a check while it's taken (a
        // cross while the pointer is on it), a minus while it's left out by name (a check while the pointer is on it).
        private static void ShowCell(CellView cell)
        {
            if (picks == null)
            {
                return;
            }
            string name = cell.Listing.Key;
            bool taken = picks.Fit(name) > 0;
            bool leftOut = picks.Has(ItemPick.OfItem(name), except: true);
            cell.Background.color = taken ? SetupUi.CellTaken : leftOut ? SetupUi.CellExcepted : SetupUi.Cell;
            cell.Icon.color = new Color(1f, 1f, 1f, taken ? 1f : leftOut ? 0.5f : 0.4f);
            MarkShape shape = SetupMarks.Of(taken, leftOut, cell.Hot);
            if (shape == MarkShape.None)
            {
                cell.Hot = false;   // a hidden mark hears no "pointer left"
                if (cell.Mark != null && cell.Mark.gameObject.activeSelf)
                {
                    cell.Mark.gameObject.SetActive(false);
                }
                return;
            }
            if (cell.Mark == null)
            {
                cell.Mark = MakeMark(cell);
            }
            else if (!cell.Mark.gameObject.activeSelf)
            {
                cell.Mark.gameObject.SetActive(true);
            }
            cell.Mark.sprite = SetupMarks.Picture(shape);
            cell.Mark.color = shape == MarkShape.Check ? SetupUi.Good : SetupUi.Bad;
        }

        // A thumbnail's mark, in its top-right corner, with clicks of its own (a raycast target over the picture).
        private static Image MakeMark(CellView cell)
        {
            GameObject box = SetupUi.Box("StoreStash_SetupMark", cell.Cell.transform, CellSize - MarkSize - 1f, 1f, MarkSize, MarkSize);
            Image image = box.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = true;
            SetupClicks clicks = box.AddComponent<SetupClicks>();
            clicks.Left = () => MarkClicked(cell);
            clicks.Right = () => CellRightClicked(cell);
            clicks.Enter = () =>
            {
                cell.Hot = true;
                ShowCell(cell);
                ShowHover(cell.Listing, cell.Picture);
            };
            clicks.Exit = () =>
            {
                cell.Hot = false;
                ShowCell(cell);
            };
            return image;
        }

        private static void ShowGroup(GroupView group)
        {
            if (group.Text == null)
            {
                return;
            }
            GroupState state = StateOf(picks, group.Pick);
            // While a search shows only part of a group, the words that take it name the whole group (review round 1).
            string searched = SearchState.Query.Length > 0 ? Setups.GroupLabel(group.Pick) : null;
            group.Text.text = GroupWords(state, TakenPartly(picks, group.Pick), searched);
            group.Text.color = state == GroupState.Taken || state == GroupState.TakenWithin ? SetupUi.Good
                : state == GroupState.LeftOut ? SetupUi.Bad
                : state == GroupState.OutsideBiomes ? SetupUi.Quiet
                : SetupUi.Plain;
            if (group.From == null)
            {
                return;
            }
            // Its biome button (card A81): while it can have biomes of its own, saying which it's taken from.
            bool show = OwnBiomesShown(picks, group.Pick, group.Page);
            if (group.From.gameObject.activeSelf != show)
            {
                group.From.gameObject.SetActive(show);
            }
            if (group.Label != null)
            {
                group.Label.rectTransform.sizeDelta = new Vector2(show ? group.LabelNarrow : group.LabelWide, group.Label.rectTransform.sizeDelta.y);
                group.Label.resizeTextForBestFit = show;
                group.Label.resizeTextMinSize = 10;
                group.Label.resizeTextMaxSize = group.Label.fontSize;
            }
            if (show && group.FromText != null)
            {
                group.FromText.text = FromWords(picks, group.Pick);
                group.FromText.color = picks.OwnBiomesOf(group.Pick).Count > 0 ? SetupUi.Orange : SetupUi.Quiet;
            }
        }

        /// <summary>Whether biomes narrow a group a heading's button names (its words then say "Taken", "Take"): the From:
        /// row's, or its own (card A81).</summary>
        internal static bool NarrowedByBiomes(ItemPicks picks, ItemPick group) => picks.InBiomes.Count > 0 || picks.OwnBiomesOf(group).Count > 0;

        /// <summary>Whether a heading's button says its group is taken only in part ("Taken", "Take", not "Takes all", "Take
        /// all"): biomes narrow it (NarrowedByBiomes), or a quick pick left out cuts across it (review m3: "Metals, Iron,
        /// except Metal Ingots" leaves Iron's bar out). A quick pick inside another member doesn't touch it.</summary>
        internal static bool TakenPartly(ItemPicks picks, ItemPick group)
        {
            if (NarrowedByBiomes(picks, group))
            {
                return true;
            }
            if (group.Level == PickLevel.Item || group.Level == PickLevel.Group)
            {
                return false;
            }
            foreach (ItemPick each in picks.Excepts)
            {
                if (each.Level != PickLevel.Group || each.Family != group.Family)
                {
                    continue;
                }
                string member = Taxonomy.QuickPickOf(each.Family, each.Group)?.Member ?? string.Empty;
                if (group.Level == PickLevel.Family || member.Length == 0 || member == group.Member)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a group can have biomes of its own (card A81): a family, member, kind or quick pick, never a
        /// single thing, nor a member of a biome family (Trophies › Swamp is one biome already).</summary>
        internal static bool CanHaveOwnBiomes(ItemPick group) =>
            group.Level != PickLevel.Item && !(group.Level == PickLevel.Member && Setups.IsBiomeFamily(group.Family));

        /// <summary>Whether a broader group the chest takes already takes the whole of this one, from every biome (its own
        /// biomes, else the From: row's, hold them all): this one's own biomes would then change nothing (review m1:
        /// Weapons from the Mistlands beside Gear taken whole still takes every weapon), so it has no biome button, and its
        /// words name it plain.</summary>
        internal static bool TakenWholeByBroader(ItemPicks picks, ItemPick group)
        {
            foreach (ItemPick each in picks.Takes)
            {
                if (Within(group, each) && BiomeChoices.All(biome => Allows(picks, each, biome)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a heading shows its group's biome button, as the setup stands (card A81): a group that can
        /// have biomes of its own, taken itself; or not taken at all on the page's own line ("All of Weapons"), where a
        /// biome picked takes it from that biome. Never where a broader group taken takes it whole from every biome
        /// (TakenWholeByBroader, review m1), nor in a setup this version can't change.</summary>
        internal static bool OwnBiomesShown(ItemPicks picks, ItemPick group, bool page)
        {
            if (picks == null || !picks.Readable || !CanHaveOwnBiomes(group) || TakenWholeByBroader(picks, group))
            {
                return false;
            }
            GroupState state = StateOf(picks, group);
            return state == GroupState.Taken || (page && state == GroupState.None);
        }

        /// <summary>What a group's biome button says (card A81): its own biomes ("Mistlands", "2 biomes"), else the From:
        /// row's that narrow it, else "Any biome"; "Pick a biome" while it isn't taken (the page's own line: review n5). A
        /// record's label is shown as words only (Setups.Shown, review m4).</summary>
        internal static string FromWords(ItemPicks picks, ItemPick group)
        {
            if (!picks.Has(group))
            {
                return "Pick a biome";
            }
            IReadOnlyList<string> own = picks.OwnBiomesOf(group);
            IReadOnlyList<string> from = own.Count > 0 ? own : picks.InBiomes;
            return from.Count == 0 ? "Any biome" : from.Count == 1 ? Setups.Shown(from[0]) : $"{from.Count} biomes";
        }

        /// <summary>What a heading's button says, as its group stands (StateOf): "Takes all" (picked itself; "Taken" while
        /// biomes narrow it), "Taken" (within a broader pick), "Only this" (taken with everything from the biomes: a click
        /// narrows the chest to it), "Left out", "Biome not picked" (a biome family's member whose biome the From: row
        /// leaves out), or "Take all" ("Take" while biomes narrow it: then not all of it, the last look). While a search shows
        /// only part of the group, the two that take it name the whole group (<paramref name="searched"/>, its label): "Take
        /// all Trophies", "Only Swamp Trophies" (review round 1: a click takes the whole group, not only what the search
        /// shows), and "Take Trophies" while biomes narrow it (review round 2).</summary>
        internal static string GroupWords(GroupState state, bool biomes, string searched = null)
        {
            switch (state)
            {
                case GroupState.Taken:
                    return biomes ? "Taken" : "Takes all";
                case GroupState.TakenWithin:
                    return "Taken";
                case GroupState.InBiomes:
                    return searched != null ? "Only " + searched : "Only this";
                case GroupState.LeftOut:
                    return "Left out";
                case GroupState.OutsideBiomes:
                    return "Biome not picked";
                default:
                    // While biomes narrow it, not "all" of the group (review round 2, and the last look): "Take", "Take Trophies".
                    if (searched == null)
                    {
                        return biomes ? "Take" : "Take all";
                    }
                    return biomes ? "Take " + searched : "Take all " + searched;
            }
        }

        private static void ShowHover(Listing listing, Sprite sprite)
        {
            if (picks == null)
            {
                return;
            }
            hoverIcon.sprite = sprite;
            hoverIcon.enabled = sprite != null;
            string where = listing.Family;
            if (listing.Section.Length > 0 && listing.Section != listing.Biome)
            {
                where += " › " + listing.Section;
            }
            if (listing.Sub.Length > 0)
            {
                where += " › " + listing.Sub;
            }
            string name = listing.Key;
            string state = picks.Has(ItemPick.OfItem(name)) ? "<color=#99e680>taken, picked by name</color>"
                : picks.Has(ItemPick.OfItem(name), except: true) ? "<color=#ff735a>left out</color>"
                : picks.Fit(name) > 0 ? "<color=#99e680>taken</color>"
                : "not taken";
            hoverText.text = $"<b>{listing.Name}</b>   {state}\n{where} · {listing.Biome}";
        }

        private static void ClearHover()
        {
            hoverIcon.enabled = false;
            hoverText.text = Hint;
        }

        // A quick pick pointed at (card A81): what it is, and how the chest takes it.
        private static void ShowQuickHover(QuickPick quick)
        {
            if (picks == null)
            {
                return;
            }
            hoverIcon.enabled = false;
            hoverText.text = $"<b>{quick.Label}</b>: {quick.About}\n{QuickState(picks, ItemPick.OfGroup(quick.Family, quick.Label))}";
        }

        /// <summary>How a quick pick stands in a setup, in the hover line's words (card A81).</summary>
        internal static string QuickState(ItemPicks picks, ItemPick group)
        {
            switch (StateOf(picks, group))
            {
                case GroupState.Taken:
                    IReadOnlyList<string> own = picks.OwnBiomesOf(group);
                    return own.Count > 0 ? $"This chest takes them, only from {Setups.BiomesInWords(own)}." : "This chest takes them.";
                case GroupState.TakenWithin:
                    return "This chest takes them, with a larger group.";
                case GroupState.InBiomes:
                    return "This chest takes them, with everything from its biomes.";
                case GroupState.LeftOut:
                    return "This chest leaves them out.";
                default:
                    return "This chest doesn't take them.";
            }
        }

        // A group's biome button pointed at (card A81).
        private static void ShowFromHover(ItemPick group)
        {
            if (picks == null)
            {
                return;
            }
            hoverIcon.enabled = false;
            hoverText.text = FromHover(picks, group);
        }

        /// <summary>What the bottom line says of a group's biome button (card A81): where the group comes from now, and what
        /// a click does.</summary>
        internal static string FromHover(ItemPicks picks, ItemPick group)
        {
            string label = Setups.GroupLabel(group);
            IReadOnlyList<string> own = picks.OwnBiomesOf(group);
            string now = own.Count > 0 ? $"<b>{label}</b>: only from {Setups.BiomesInWords(own)}, its own biomes (the From: row doesn't narrow it)."
                : picks.InBiomes.Count > 0 ? $"<b>{label}</b>: from {Setups.BiomesInWords(picks.InBiomes)}, as the From: row says."
                : $"<b>{label}</b>: from any biome.";
            return now + "\nClick to pick biomes for this group alone.";
        }

        // A biome of a group's own row pointed at (card A81): what a click does.
        private static void ShowChipHover(ItemPick group, string biome)
        {
            if (picks == null)
            {
                return;
            }
            hoverIcon.enabled = false;
            string label = Setups.GroupLabel(group);
            string where = biome == Taxonomy.OtherBiome ? "things of no biome" : "the " + biome;
            hoverText.text = picks.OwnBiomesOf(group).Contains(biome)
                ? $"Click: <b>{label}</b> no longer from {where}."
                : picks.OwnBiomesOf(group).Count > 0 ? $"Click: <b>{label}</b> from {where} too."
                : $"Click: <b>{label}</b> only from {where} (the rest of this chest keeps its From: row).";
        }

        // ---- A group's own biomes (card A81)

        // Its biome button: opens the row of its own biomes under its heading, or closes it (one row open at a time).
        private static void FromClicked(ItemPick group, bool page)
        {
            bool open = !(biomesOpen.HasValue && biomesOpen.Value.Equals(group) && biomesOpenOnPage == page);
            biomesOpen = open ? group : (ItemPick?)null;
            biomesOpenOnPage = page;
            RenderGrid();
            Refresh();
        }

        // A biome in a group's own row.
        private static void OwnBiomeClicked(ItemPick group, string biome)
        {
            if (!CanChange())
            {
                return;
            }
            bool idle = IdleFromRow(picks);
            string why = ClickOwnBiome(picks, group, biome);
            if (why != null)
            {
                Messages.Center(why);
                return;
            }
            if (Changed() && !idle && IdleFromRow(picks))
            {
                Messages.Center("Every group of this chest has biomes of its own now: its From: row narrows nothing.");
            }
        }

        /// <summary>What a click on a biome of a group's own row does to a setup (card A81). Taken itself: the biome becomes
        /// one of its own, or stops being (with its last gone, it follows the From: row again). Not taken (its page's own
        /// line): taken, from that biome only. Null when done; else why not, nothing changed.</summary>
        internal static string ClickOwnBiome(ItemPicks picks, ItemPick group, string biome)
        {
            if (!CanHaveOwnBiomes(group))
            {
                return $"{Setups.GroupLabel(group)} can't have biomes of their own.";
            }
            if (TakenWholeByBroader(picks, group))
            {
                // Review m1: a broader group taken whole takes it from every biome already.
                return $"A larger group this chest takes already takes all {Setups.GroupLabel(group)}, from every biome: give that group " +
                       "biomes, or take it off, first.";
            }
            switch (StateOf(picks, group))
            {
                case GroupState.Taken:
                    if (picks.OwnBiomesOf(group).Contains(biome))
                    {
                        picks.RemoveOwnBiome(group, biome);
                        return null;
                    }
                    return picks.AddOwnBiome(group, biome) ? null : Full;
                case GroupState.None:
                    if (!picks.Add(group))
                    {
                        return Full;
                    }
                    if (picks.AddOwnBiome(group, biome))
                    {
                        return null;
                    }
                    picks.Remove(group);   // as it was
                    return Full;
                default:
                    return $"{Setups.GroupLabel(group)} aren't taken on their own here: take them first, then pick their biomes.";
            }
        }

        /// <summary>Whether a setup's From: row narrows nothing (card A81): it has biomes, and every family, member, kind and
        /// quick pick it takes has biomes of its own (nor did a newer version pick anything).</summary>
        internal static bool IdleFromRow(ItemPicks picks)
        {
            if (picks == null || !picks.Readable || picks.InBiomes.Count == 0 || picks.NewerPicks)
            {
                return false;
            }
            bool any = false;
            foreach (ItemPick each in picks.Takes)
            {
                if (each.Level == PickLevel.Item)
                {
                    continue;
                }
                if (picks.OwnBiomesOf(each).Count == 0)
                {
                    return false;
                }
                any = true;
            }
            return any;
        }

        // ---- His clicks on things (decision 25)

        // A left click on a thumbnail: in the lists, takes it or leaves it out (ClickThing); in "This chest's setup",
        // nothing (his point 4: there only its mark, or a right-click, changes it).
        private static void CellClicked(CellView cell)
        {
            if (setupView || !CanChange())
            {
                return;
            }
            string name = cell.Listing.Key;
            if (!ClickThing(picks, name, picks.Fit(name)))
            {
                Messages.Center(Full);
                return;
            }
            Changed();
            ShowHover(cell.Listing, cell.Picture);
        }

        // A right-click anywhere on a thumbnail (its mark too): takes it out while the chest takes it; else nothing.
        private static void CellRightClicked(CellView cell)
        {
            if (picks != null && picks.Fit(cell.Listing.Key) > 0)
            {
                TakeOut(cell);
            }
        }

        // A left click on a thumbnail's mark: the check (a cross while pointed at) takes it out; the minus (a check while
        // pointed at) takes it back.
        private static void MarkClicked(CellView cell)
        {
            if (picks == null)
            {
                return;
            }
            string name = cell.Listing.Key;
            if (picks.Fit(name) > 0)
            {
                TakeOut(cell);
            }
            else if (picks.Has(ItemPick.OfItem(name), except: true) && CanChange())
            {
                if (TakeBack(picks, name))
                {
                    Changed();
                    ShowHover(cell.Listing, cell.Picture);
                }
            }
        }

        private static void TakeOut(CellView cell)
        {
            if (!CanChange())
            {
                return;
            }
            string name = cell.Listing.Key;
            if (!RemoveThing(picks, name, () => picks.Fit(name)))
            {
                Messages.Center(Full);
                Load();   // nothing of it is written: the window shows the chest's setup as it is
                Refresh();
                return;
            }
            Changed();
            ShowHover(cell.Listing, cell.Picture);
        }

        /// <summary>What a click on a thing does to a setup, given how the setup takes it (<paramref name="fit"/>, as
        /// ItemPicks.Fit says): undoes the thing's own pick or except; leaves it out by name if the setup takes it (with a
        /// family, member or kind, or biomes alone); else takes it by name. False when the setup is full.</summary>
        internal static bool ClickThing(ItemPicks picks, string name, int fit)
        {
            ItemPick item = ItemPick.OfItem(name);
            if (picks.Has(item) || picks.Has(item, except: true))
            {
                picks.Remove(item);
                return true;
            }
            return picks.Add(item, except: fit > 0);
        }

        /// <summary>Takes a thing out of a setup (its check mark, or a right-click): unpicks it if it's picked by name; if
        /// the setup still takes it (<paramref name="fit"/>, asked again after: with a family, member or kind, or biomes
        /// alone), leaves it out by name. False when the setup is full (an except can't be added), with nothing changed:
        /// unpicking first frees the room its except takes.</summary>
        internal static bool RemoveThing(ItemPicks picks, string name, Func<int> fit)
        {
            ItemPick item = ItemPick.OfItem(name);
            if (picks.Has(item))
            {
                picks.Remove(item);
            }
            return fit() <= 0 || picks.Add(item, except: true);
        }

        /// <summary>Takes back a thing left out by name (its minus mark, a check while pointed at): its except comes off, and
        /// if the setup still doesn't take it (left out with something larger too, review round 1; or nothing else takes
        /// it, outside the setup's biomes or its families, review round 2) it's picked by name, which beats any group left
        /// out and comes from any biome: so the check's promise holds. False when it isn't left out by name.</summary>
        internal static bool TakeBack(ItemPicks picks, string name) => TakeBack(picks, name, setup => setup.Fit(name));

        /// <summary>The same, with how a setup takes the thing given (<paramref name="fitOf"/>, as ItemPicks.Fit does in the
        /// game): for the scratch harness, which has no catalogue of the game's own.</summary>
        internal static bool TakeBack(ItemPicks picks, string name, Func<ItemPicks, int> fitOf)
        {
            ItemPick item = ItemPick.OfItem(name);
            if (!picks.Has(item, except: true) || !picks.Remove(item))
            {
                return false;
            }
            // Still not taken: by name (the except's room is free).
            return fitOf(picks) > 0 || picks.Add(item);
        }

        // ---- His clicks on headings (decision 23)

        private static void GroupClicked(ItemPick group)
        {
            if (!CanChange())
            {
                return;
            }
            bool hadGroups = picks.TakesGroups;
            string why = ClickGroup(picks, group);
            if (why != null)
            {
                Messages.Center(why);
                return;
            }
            if (Changed())
            {
                Say(Widening(hadGroups, picks));
            }
        }

        // A right-click on a heading's button: takes the group out, as its state allows (RightClickGroup); nothing (and
        // no word) where there's nothing to take out.
        private static void GroupRightClicked(ItemPick group)
        {
            if (picks == null)
            {
                return;
            }
            GroupState state = StateOf(picks, group);
            if (state == GroupState.None || state == GroupState.LeftOut || (state == GroupState.OutsideBiomes && !picks.Has(group)) || !CanChange())
            {
                return;
            }
            bool hadGroups = picks.TakesGroups;
            string why = RightClickGroup(picks, group);
            if (why != null)
            {
                Messages.Center(why);
                return;
            }
            if (Changed())
            {
                Say(Widening(hadGroups, picks));
            }
        }

        private static void Say(string text)
        {
            if (text != null)
            {
                Messages.Center(text);
            }
        }

        /// <summary>
        /// What a left click on a heading's button does to a setup. Null when done; else why not, for him to read, with
        /// nothing changed. Taken itself: unpicked. Left out itself: taken back. Taken within a broader pick: left out.
        /// Taken with everything from the biomes alone: "Only this", the chest narrows to it from those biomes (so the
        /// Swamp and Plains, then Trophies, give Swamp and Plains trophies, as Trophies, then the biomes, do). Left out
        /// with a larger group under biomes alone: refused, since taking it would narrow the whole chest to it rather than
        /// take it back (no setup can say "everything from the Plains but Hides and Fabrics, with Hides and Pelts").
        /// A biome family's member whose biome the From: row leaves out: unpicked if picked (it took nothing itself; when it
        /// was the last group beside the biomes, the chest now takes everything from them, and he's told so, as for a taken
        /// group: decision 23); else refused (it would take nothing). Otherwise taken. Refused too when the setup is full.
        /// </summary>
        internal static string ClickGroup(ItemPicks picks, ItemPick group)
        {
            switch (StateOf(picks, group))
            {
                case GroupState.Taken:
                    picks.Remove(group);
                    return null;
                case GroupState.LeftOut when picks.Has(group, except: true):
                    picks.Remove(group);
                    if (StateOf(picks, group) == GroupState.LeftOut)
                    {
                        // Still left out with something larger (review round 1): taken on its own then, as a more specific
                        // pick beats a larger left-out, unless the biomes stand alone, where taking it would narrow the whole
                        // chest to it.
                        if (BiomesAlone(picks))
                        {
                            picks.Add(group, except: true);   // as it was
                            return LeftOutWithLarger;
                        }
                        return picks.Add(group) ? null : Full;
                    }
                    return null;
                case GroupState.TakenWithin:
                    return picks.Add(group, except: true) ? null : Full;
                case GroupState.InBiomes:
                    return picks.Add(group) ? null : Full;
                case GroupState.LeftOut when BiomesAlone(picks):
                    return LeftOutWithLarger;
                case GroupState.OutsideBiomes:
                    return picks.Has(group) && picks.Remove(group) ? null : OutsideWhy(group);
                default:
                    return picks.Add(group) ? null : Full;
            }
        }

        /// <summary>What a right-click on a heading's button does to a setup: the group taken itself, unpicked; taken
        /// within something broader (a family, or the biomes alone), left out; a biome family's member outside the setup's
        /// biomes, unpicked if picked; anything else, nothing. Null when done (or nothing to do); "full" when an except
        /// can't be added. Refused, nothing changed, where taking the group out would leave the biomes alone: the chest
        /// would then take everything from them (review round 2 for a group taken, the last look for one outside them).</summary>
        internal static string RightClickGroup(ItemPicks picks, ItemPick group)
        {
            switch (StateOf(picks, group))
            {
                case GroupState.Taken:
                    List<string> own = picks.OwnBiomesOf(group).ToList();   // put back with it, if it's put back (card A81)
                    picks.Remove(group);
                    if (BiomesAlone(picks))
                    {
                        // The last group beside biomes (review round 2): without it the chest would take everything from
                        // them, this group too, the opposite of taking it out. Refused, nothing changed.
                        picks.Add(group);
                        picks.SetOwnBiomes(group, own);
                        return LastBesideBiomes;
                    }
                    // Still taken with a larger pick (its family picked too: review round 1): left out as well, so it's out.
                    if (StateOf(picks, group) == GroupState.TakenWithin && !picks.Add(group, except: true))
                    {
                        picks.Add(group);   // as it was: the setup is full
                        picks.SetOwnBiomes(group, own);
                        return Full;
                    }
                    return null;
                case GroupState.TakenWithin:
                case GroupState.InBiomes:
                    return picks.Add(group, except: true) ? null : Full;
                case GroupState.OutsideBiomes:
                    return picks.Has(group) ? UnpickOutside(picks, group) : null;
                default:
                    return null;
            }
        }

        // A right-click on a biome family's member outside the setup's biomes, picked: unpicked (it took nothing), unless
        // it's the last group beside the biomes (the last look): without it the chest would take everything from them, the
        // opposite of taking it out, so it's refused and put back as it was, as round 2 refuses it for a group taken. A left
        // click unpicks it and says the chest widened (ClickGroup, decision 23).
        private static string UnpickOutside(ItemPicks picks, ItemPick group)
        {
            picks.Remove(group);
            if (BiomesAlone(picks))
            {
                picks.Add(group);
                return LastOutsideWhy(group);
            }
            return null;
        }

        /// <summary>What he's told after taking a group off (<paramref name="hadGroups"/>: the setup took a family, member or
        /// kind before) left biomes alone: the chest now takes everything from them, and how to empty it. Null otherwise.</summary>
        internal static string Widening(bool hadGroups, ItemPicks picks) =>
            hadGroups && picks != null && picks.Readable && BiomesAlone(picks)
                ? $"This chest now takes {Setups.EverythingFrom(picks.InBiomes)}: nothing is left for its biomes to narrow. " +
                  "Click them off, or Clear, to empty it."
                : null;

        // Why a biome family's member outside the setup's biomes can't be taken.
        private static string OutsideWhy(ItemPick group)
        {
            string from = group.Member == Taxonomy.OtherBiome ? "no biome" : "the " + group.Member;
            return $"{Setups.GroupLabel(group)} come only from {from}, which this chest's From: row leaves out: pick {group.Member} there first.";
        }

        private const string Full = "This chest's setup is full.";

        private const string LastBesideBiomes =
            "This is all this chest takes from its biomes: without it, it would take everything from them. Click the biomes " +
            "off first, or Clear.";

        // Why a right-click can't take out the last group beside the biomes when its own biome isn't among them.
        private static string LastOutsideWhy(ItemPick group)
        {
            // The From: row's own button for things of no biome (review round 3: "leaves no biome out" read as its opposite).
            string from = group.Member == Taxonomy.OtherBiome ? Taxonomy.OtherBiome : "the " + group.Member;
            return $"{Setups.GroupLabel(group)} take nothing here already (the From: row leaves {from} out), and without them this " +
                   "chest would take everything from its biomes. Click the biomes off first, or Clear.";
        }

        private const string LeftOutWithLarger =
            "This chest takes everything from its biomes but what it leaves out, and this is left out with a larger group: " +
            "take that group back first.";

        private static void BiomeClicked(string biome)
        {
            if (!CanChange())
            {
                return;
            }
            bool alone = BiomesAlone(picks);
            string why = ClickBiome(picks, biome);
            if (why != null)
            {
                Messages.Center(why);
                return;
            }
            if (Changed())
            {
                Say(ByNameWidened(alone, picks));
            }
        }

        /// <summary>What he's told when a biome given to a setup of things picked by name alone (<paramref name="alone"/>:
        /// whether its biomes stood alone before) makes it take everything from that biome besides them (review round 1).
        /// Null otherwise.</summary>
        internal static string ByNameWidened(bool alone, ItemPicks picks) =>
            !alone && picks != null && picks.Readable && BiomesAlone(picks) && picks.Takes.Count > 0
                ? $"This chest now takes {Setups.EverythingFrom(picks.InBiomes)}, as well as what's picked by name: biomes on " +
                  "their own take everything from them. Take a family to narrow them to it."
                : null;

        /// <summary>What a click on a biome does to a setup: takes it off, or adds it. Null when done; else why not: the
        /// setup is full, or it's the last biome of a setup that takes nothing else, whose left-out things would be lost
        /// with it (a setup of left-outs alone is no setup).</summary>
        internal static string ClickBiome(ItemPicks picks, string biome)
        {
            if (!picks.InBiomes.Contains(biome))
            {
                return picks.AddBiome(biome) ? null : Full;
            }
            if (picks.InBiomes.Count == 1 && picks.Takes.Count == 0 && picks.Excepts.Count > 0)
            {
                return "Pick the other biome first, then take this one off: without a biome this chest's setup would be empty, " +
                       "and what it leaves out lost. (Clear empties it.)";
            }
            picks.RemoveBiome(biome);
            return null;
        }

        private static void ClearClicked()
        {
            if (!CanChange(clearing: true) || picks.IsEmpty)
            {
                return;
            }
            picks = new ItemPicks();
            Changed();
            Messages.Center("Setup cleared: it's an ordinary chest again.");
        }

        /// <summary>How a heading's group stands: taken as a whole (picked itself), taken within a broader pick, taken with
        /// everything from the biomes alone, left out (itself, or within a broader except), outside the setup's biomes (a
        /// biome family's member), or not taken.</summary>
        internal enum GroupState
        {
            None,
            Taken,
            TakenWithin,
            InBiomes,
            LeftOut,
            OutsideBiomes,
        }

        /// <summary>How a group stands in a setup: the most specific pick and except covering it decide, as for a single
        /// thing (an except wins a tie); with no family, member or kind picked, biomes alone take every group (as Fit takes
        /// everything from them: ItemPicks.TakesGroups). A member of a biome family (Trophies › Swamp) whose biome the
        /// setup's biomes leave out takes nothing, whatever is picked (OutsideBiomes).</summary>
        internal static GroupState StateOf(ItemPicks picks, ItemPick group)
        {
            if (OutsideBiomes(picks, group))
            {
                return GroupState.OutsideBiomes;
            }
            if (picks.Has(group))
            {
                return GroupState.Taken;
            }
            if (picks.Has(group, except: true))
            {
                return GroupState.LeftOut;
            }
            int take = 0;
            bool biomeMember = IsBiomeMember(group);
            foreach (ItemPick each in picks.Takes)
            {
                // A biome family's member lies within its family taken only where the family's biomes hold its biome (its
                // own, card A81, else the From: row's: review m2).
                if (Within(group, each) && (!biomeMember || Allows(picks, each, group.Member)))
                {
                    take = Math.Max(take, each.Strength);
                }
            }
            bool alone = take == 0 && BiomesAlone(picks);
            if (alone)
            {
                take = ItemPicks.BiomesOnlyStrength;
            }
            int except = 0;
            foreach (ItemPick each in picks.Excepts)
            {
                if (Within(group, each))
                {
                    except = Math.Max(except, each.Strength);
                }
            }
            if (take > 0 && take > except)
            {
                return alone ? GroupState.InBiomes : GroupState.TakenWithin;
            }
            return except > 0 ? GroupState.LeftOut : GroupState.None;
        }

        /// <summary>Biomes alone: a biome or more, and no family, member or kind picked (nor a newer version's pick): the
        /// chest takes everything from them (ItemPicks.Fit).</summary>
        internal static bool BiomesAlone(ItemPicks picks) => !picks.TakesGroups && picks.InBiomes.Count > 0;

        /// <summary>A member of a biome family (Trophies › Swamp: its things all come from that one biome) that nothing can
        /// take as the biomes stand: picked itself, its biomes (its own, else the From: row's) leave its biome out; not
        /// picked, the From: row leaves it out (taking it would follow the From: row) and no family taken with biomes of
        /// its own holds it (card A81, review m2: "Swamp Trophies" beside Wood from the Plains takes the Swamp trophies).
        /// Without biomes of a group's own, as before: the From: row leaves its biome out.</summary>
        internal static bool OutsideBiomes(ItemPicks picks, ItemPick group)
        {
            if (!IsBiomeMember(group))
            {
                return false;
            }
            if (picks.Has(group))
            {
                return !Allows(picks, group, group.Member);
            }
            if (picks.InBiomes.Count == 0 || picks.InBiomes.Contains(group.Member))
            {
                return false;
            }
            foreach (ItemPick each in picks.Takes)
            {
                if (Within(group, each) && picks.OwnBiomesOf(each).Contains(group.Member))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsBiomeMember(ItemPick group) => group.Level == PickLevel.Member && Setups.IsBiomeFamily(group.Family);

        /// <summary>Whether a pick taken takes from a biome: its own biomes (card A81), else the From: row's (none: every
        /// biome).</summary>
        internal static bool Allows(ItemPicks picks, ItemPick take, string biome)
        {
            IReadOnlyList<string> own = picks.OwnBiomesOf(take);
            return own.Count > 0 ? own.Contains(biome) : picks.InBiomes.Count == 0 || picks.InBiomes.Contains(biome);
        }

        // Whether a broader pick covers the whole of a group: a family covers its members, kinds and quick picks, a member
        // its kinds and the quick picks inside it (card A81: Taxonomy.QuickPicks' Member). A quick pick covers no member or
        // kind whole, nor another quick pick.
        internal static bool Within(ItemPick group, ItemPick broader)
        {
            if (broader.Level == PickLevel.Item || broader.Level == PickLevel.Group || broader.Family != group.Family)
            {
                return false;
            }
            if (group.Level == PickLevel.Group)
            {
                if (broader.Level == PickLevel.Family)
                {
                    return true;
                }
                QuickPick quick = Taxonomy.QuickPickOf(group.Family, group.Group);
                return broader.Level == PickLevel.Member && quick != null && quick.Member.Length > 0 && quick.Member == broader.Member;
            }
            if (broader.Level >= group.Level)
            {
                return false;
            }
            return broader.Level == PickLevel.Family || broader.Member == group.Member;
        }

        // ---- Building, once per inventory screen

        private static void Build(InventoryGui gui)
        {
            Transform parent = gui.m_crafting != null ? gui.m_crafting.parent : gui.m_inventoryRoot;
            if (parent == null)
            {
                return;
            }
            GUIManager ui = GUIManager.Instance;
            window = ui.CreateWoodpanel(parent, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Width, Height, false);
            window.name = "StoreStash_SetupWindow";
            Transform root = window.transform;

            // The title: "Chest setup:", then the chest's name, in a box to rename it (a plain label with renaming off).
            titleText = SetupUi.Label(root, "Chest setup:", Pad, TitleY, 170f, 30f, 22, TextAnchor.MiddleLeft, SetupUi.Orange, bold: true);
            float titleWidth = Mathf.Ceil(titleText.preferredWidth);
            if (titleWidth < 60f || titleWidth > 200f)
            {
                titleWidth = 140f;   // the text's own measure, unless the font couldn't say
            }
            titleText.rectTransform.sizeDelta = new Vector2(titleWidth, 30f);
            float nameX = Pad + titleWidth + 6f;
            float nameWidth = Width - Pad - 34f - 10f - nameX;
            nameField = SetupUi.Field(root, nameX, TitleY, nameWidth, 30f, 17, null);
            nameField.characterLimit = SetupName.MaxLength;
            nameField.onEndEdit.AddListener(text => Module.Guard("renaming the chest", () => NameEdited(text)));
            nameField.gameObject.AddComponent<SetupTypingWatch>().Selected = KeyGuard.Hold;
            nameLabel = SetupUi.Label(root, string.Empty, nameX + 4f, TitleY, nameWidth - 4f, 30f, 20, TextAnchor.MiddleLeft, SetupUi.Plain, bold: true);
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 14;
            nameLabel.resizeTextMaxSize = 20;
            nameLabel.gameObject.SetActive(false);
            SetupUi.Button(root, "X", Width - Pad - 34f, TitleY, 34f, 30f, () => Module.Guard("closing the setup window", CloseClicked));
            summaryText = SetupUi.Label(root, string.Empty, Pad, SummaryY, Width - 2f * Pad, 46f, 15, TextAnchor.UpperLeft, SetupUi.Plain);
            summaryText.resizeTextForBestFit = true;   // a long setup's sentence shrinks a little rather than losing its end
            summaryText.resizeTextMinSize = 12;
            summaryText.resizeTextMaxSize = 15;

            // The biomes: "From:", then two rows of five.
            SetupUi.Label(root, "From:", Pad, BiomesY, FromWidth - 4f, 2f * BiomeHeight + 4f, 14, TextAnchor.MiddleLeft, SetupUi.Quiet, bold: true);
            BiomeButtons.Clear();
            float biomeWidth = (Width - 2f * Pad - FromWidth - 4f * BiomeGap) / 5f;
            for (int i = 0; i < BiomeChoices.Length; i++)
            {
                string biome = BiomeChoices[i];
                float x = Pad + FromWidth + (i % 5) * (biomeWidth + BiomeGap);
                float y = BiomesY + (i / 5) * (BiomeHeight + 4f);
                UIButton button = SetupUi.Button(root, biome, x, y, biomeWidth, BiomeHeight,
                                                  () => Module.Guard("changing a chest's biomes", () => BiomeClicked(biome)));
                Text text = SetupUi.TextOf(button);
                if (text != null)
                {
                    text.fontSize = 13;
                    text.resizeTextForBestFit = false;
                }
                BiomeButtons.Add((biome, button));
            }

            // The list on the left under its search box (as Add to Cart's), with an x to empty it; the things on the right.
            searchBox = SetupUi.Field(root, Pad, ListY, RailWidth - 26f, SearchHeight, 15, "Search");
            searchBox.onValueChanged.AddListener(text => Module.Guard("searching the setup window", () => SearchState.Typed(text, Time.time)));
            searchBox.onEndEdit.AddListener(text => boxLetGo = true);
            searchBox.gameObject.AddComponent<SetupTypingWatch>().Selected = KeyGuard.Hold;
            clearSearch = SetupUi.Box("StoreStash_SetupSearchClear", root, Pad + RailWidth - 23f, ListY + 3f, 22f, 22f);
            Image clearImage = clearSearch.AddComponent<Image>();
            clearImage.sprite = SetupMarks.Picture(MarkShape.Cross);
            clearImage.preserveAspect = true;
            clearImage.color = SetupUi.Quiet;
            SetupClicks clearClicks = clearSearch.AddComponent<SetupClicks>();
            clearClicks.Left = ClearSearch;
            clearClicks.Enter = () => clearImage.color = SetupUi.Plain;
            clearClicks.Exit = () => clearImage.color = SetupUi.Quiet;
            clearSearch.SetActive(false);
            rail = SetupUi.Scroll("StoreStash_SetupList", root, Pad, RailY, RailWidth, ListBottom - RailY);
            SetupUi.List(rail, 0f);
            grid = SetupUi.Scroll("StoreStash_SetupThings", root, ResultsX, ListY, ResultsWidth, ListBottom - ListY);
            SetupUi.List(grid, 2f);

            hoverIcon = SetupUi.Icon(root, null, Pad, HoverY + 2f, 38f);
            hoverIcon.enabled = false;
            hoverText = SetupUi.Label(root, Hint, Pad + 46f, HoverY, Width - 2f * Pad - 46f, 40f, 14, TextAnchor.UpperLeft, SetupUi.Quiet);
            hoverText.resizeTextForBestFit = true;   // two lines at most
            hoverText.resizeTextMinSize = 11;
            hoverText.resizeTextMaxSize = 14;

            copyButton = SetupUi.Button(root, "Copy", Pad, ButtonsY, 104f, 34f, () => Module.Guard("copying a chest's setup", () => SetupClipboard.Copy(chest)));
            pasteButton = SetupUi.Button(root, "Paste", Pad + 112f, ButtonsY, 104f, 34f, () => Module.Guard("pasting a chest's setup", () => SetupClipboard.Paste(chest)));
            SetupUi.Button(root, "Clear", Pad + 224f, ButtonsY, 104f, 34f, () => Module.Guard("clearing a chest's setup", ClearClicked));
            SetupUi.Button(root, "Done", Width - Pad - 120f, ButtonsY, 120f, 34f, () => Module.Guard("closing the setup window", CloseClicked));
            bool buttons = SetupSettings.CopyButtons.Value;
            copyButton.gameObject.SetActive(buttons);
            pasteButton.gameObject.SetActive(buttons);
            // A new window (a new world): an empty search box.
            SearchState.Clear();
            searchedIn = null;
            window.SetActive(false);
        }

        // Over the crafting panel, in its place: the same anchors, pivot and position, so the window follows wherever the
        // game (or another mod) puts that panel, and scales with the inventory screen.
        private static void Dock(InventoryGui gui)
        {
            var rect = (RectTransform)window.transform;
            RectTransform craft = gui.m_crafting;
            if (craft != null && rect.parent == craft.parent)
            {
                rect.anchorMin = craft.anchorMin;
                rect.anchorMax = craft.anchorMax;
                rect.pivot = craft.pivot;
                rect.anchoredPosition = craft.anchoredPosition;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-40f, -200f);
            }
            rect.sizeDelta = new Vector2(Width, Height);
        }

        private sealed class CellView
        {
            internal Listing Listing;
            internal GameObject Cell;
            internal Image Background;
            internal Image Icon;
            internal Sprite Picture;
            internal Image Mark;

            /// <summary>The pointer is on the mark: a check shows as a cross, a minus as a check.</summary>
            internal bool Hot;
        }

        private sealed class GroupView
        {
            internal ItemPick Pick;
            internal UIButton Button;
            internal Text Text;

            // Card A81: its heading's label and its widths (up to the group's button, or to its biome button), whether it's
            // the page's own line, and its biome button (null for a group that can't have biomes of its own).
            internal Text Label;
            internal float LabelWide;
            internal float LabelNarrow;
            internal bool Page;
            internal UIButton From;
            internal Text FromText;
        }
    }
}
