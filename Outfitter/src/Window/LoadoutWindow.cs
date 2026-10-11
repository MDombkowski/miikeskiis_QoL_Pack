using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Jotunn.Managers;
using Outfitter.Equip;
using Outfitter.Loadouts;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// Outfitter's window (card A34, decision 1: a window of its own behind its own key, L): his loadouts on the left;
    /// the one he picks on the right, in its places in his inventory's shape (what isn't nearby in red, what it puts on
    /// marked blue), with what it will weigh; the three modes under "Equip mode", beside Equip; saving the cells he
    /// highlighted in his inventory (Right Alt + click in the game's own, or Highlight cells in My inventory here), saving
    /// over, renaming, deleting; copying a share code and pasting one; highlighting cells to copy and paste them between
    /// his inventory and loadouts (job 30, Session 3). Beside it, one panel at a time: the nearby items, or his inventory.
    /// A button that would do nothing now is faded, and one acting on the highlighted cells lit (his words, 2026-10-06);
    /// each says what it does when pointed at (Tips). 0.5.0 (his words, 2026-10-07): each loadout's own "Favourites go too"
    /// beside the modes, ↑ and ↓ on the row picked, Save new from the loadout's highlighted cells, a cell's Worn and Shown,
    /// Remove, "Equip now", a food slot's rule opened by a click on it, and the stand's slots beside the window.
    /// Built once, from the game's own pieces through Jötunn (as Add to
    /// Cart's window is), on Outfitter's own canvas. While it shows, the game's input waits; Escape or L closes it. A
    /// plain window of its own, not the game's inventory grid, so Use Case's panel beside the game's tooltip doesn't show
    /// over it (the README says so).
    /// </summary>
    internal static class LoadoutWindow
    {
        private const float Width = 900f;

        /// <summary>60 more than 0.3.0's 700, for the cell tools' row and the modes' heading (job 30, Session 3).</summary>
        internal const float Height = 760f;
        private const float RightX = 290f;
        private const float GridTop = 100f;
        private const float InfoX = 730f;

        private static GameObject window;
        private static bool blocking;
        private static bool unblockNextFrame;
        private static bool keyFailed;
        private static KeyboardShortcut failedKey = KeyboardShortcut.Empty;

        private static string selectedId;
        private static EquipMode mode;
        private static bool modeChosen;
        private static EquipMode settingMode;
        private static bool wentOff;
        private static GridShape shape;

        // Highlight cells (job 30, Session 3): while on, a click on a cell of either grid highlights it, instead of picking
        // it for editing. The loadout's highlighted cells are kept here, his inventory's are the picks (Picks); one grid at
        // a time holds highlights, so Copy cells always knows which.
        private static bool highlighting;
        private static readonly HashSet<Vector2i> loadoutHighlights = new HashSet<Vector2i>();
        private static int loadoutHighlightsVersion;

        /// <summary>The grid's room: from its top to just above the cell tools.</summary>
        private const float GridRoom = 440f;

        private static int shownStore = -1;
        private static int shownPicks = -1;
        private static int shownSettings = -1;
        private static string shownSelected;
        private static bool shownHighlighting;
        private static int shownHighlights = -1;
        private static int shownClipboard = -1;
        private static int seenPicks = -1;
        private static float nextLook;
        private static EquipPlan preview;
        private static Vector2i? hovered;

        private static RectTransform list;
        private static readonly List<ListRow> rows = new List<ListRow>();

        // A row of the loadout list: its name, the stand it's on, and on the row picked ↑ and ↓ to move it (0.5.0).
        private sealed class ListRow
        {
            internal Clicks Clicks;
            internal Image Background;
            internal Text Name;
            internal Text Stand;
            internal GameObject StandBox;
            internal Button Up;
            internal Button Down;
        }

        // The stand bar (job 30, Session 2): the stand he opened the window at, its name, the loadout it holds.
        private static Stands.IStand standFocus;
        private static GameObject standBar;
        private static Text standLabel;
        private static Button renameStandButton;
        private static Button unassignButton;
        private static Button assignButton;
        private static Button personalButton;

        // The stands near him, as last listed (each loadout's stand in the list, the dropdown).
        private static List<Stands.StandEdits.Listed> standsNear = new List<Stands.StandEdits.Listed>();
        private static Text header;
        private static Text info;
        private static Text weight;
        private static Text status;
        private static Text anywhereTitle;
        private static readonly List<Image> anywhere = new List<Image>();
        private static GridView grid;
        private static Button[] modeButtons;
        private static Text modeHeading;
        private static Button equipButton;
        private static Button saveNewButton;
        private static Button saveOverButton;
        private static Button renameButton;
        private static Button deleteButton;
        private static Button copyButton;
        private static Button pasteButton;
        private static Button moveStandButton;
        private static Button standSlotsButton;
        private static Button favouritesButton;

        // The cell tools under the grid (job 30, Session 3).
        private static Button highlightButton;
        private static Button copyCellsButton;
        private static Button pasteCellsButton;
        private static Button clearHighlightsButton;
        private static Button inventoryButton;

        // Editing a loadout by hand (job 30, Session 2): the cell being edited, and what Undo takes back (each loadout as
        // it was before an edit, newest last), while the window is open.
        private const int MostUndos = 30;
        private static Vector2i? selectedCell;
        private static readonly List<Loadout> undo = new List<Loadout>();

        // The list's version after the window's own last change: any other change (another character, "Where loadouts are
        // kept", a new scene) and Undo forgets everything (review round 1 of Session 2, m1).
        private static int undoVersion = -1;

        // Count changes of one cell close together are one Undo entry (the wheel: review round 1 of Session 2, n3).
        private const float FoldSeconds = 1.5f;
        private static string lastFold;
        private static float lastFoldTime;
        private static Button newButton;
        private static Button undoButton;
        private static Button lessButton;
        private static Button moreButton;
        private static InputField countField;
        private static Button wornButton;
        private static Button shownButton;
        private static Button takeOutButton;
        private static Button foodButton;
        private static Button drawerButton;

        internal static bool Open => window != null && window.activeSelf;

        /// <summary>Every frame (the module's Update, also while it's switched off).</summary>
        internal static void Update()
        {
            bool popupUsedEscape = Popup.Update();
            if (!Module.IsOn)
            {
                if (!wentOff)
                {
                    // Once, as it's switched off: the window, a question still showing (a pending delete or rename must not
                    // act while off: R1, finding 9) and the frames go.
                    wentOff = true;
                    Close();
                    Popup.Close();
                    Picks.HideFrames();
                }
                return;
            }
            wentOff = false;
            if (unblockNextFrame)
            {
                unblockNextFrame = false;
                Unblock();
            }
            if (!Open || Popup.Showing)
            {
                Tips.Update(false);
            }
            if (Open)
            {
                Ui.FitCanvas();
                if (Popup.Showing)
                {
                    if (Drag.Current != null)
                    {
                        Drag.Cancel();   // a question over the window ends a drag: let go on its shade, it took the item out (n1)
                    }
                    return;
                }
                bool escape = !popupUsedEscape && ZInput.GetKeyDown(KeyCode.Escape, false);
                if (!Ui.Typing() && (KeyPressed() || escape))
                {
                    CloseSoon();
                    return;
                }
                Refresh();
                Player player = Player.m_localPlayer;
                if (Open && player != null)
                {
                    Drawer.Refresh(player);   // every frame: it reads the chests at most once a second
                    InventoryPanel.Refresh(player, Picks.Version);
                    FoodPanel.Refresh(Selected, shape, preview, FoodNow);
                    StandPanel.Refresh(Selected);
                }
                Tips.Update(Open && !Popup.Showing);   // after the refresh, so a tip asked again reads the buttons as they are now (the last look's n2)
            }
            else if (window == null && blocking)
            {
                Unblock();   // the scene changed under the open window
                Closed();    // and what closing ends, Undo with it (m1)
            }
            else if (!Popup.Showing && KeyPressed() && CanOpen())
            {
                Stands.IStand stand = StandAtPointer(out string note);
                Show(stand);
                if (status != null)
                {
                    status.text = note ?? string.Empty;   // each opening starts its line afresh (the last look's n1)
                }
            }
        }

        // The armour stand he points at, when "Outfitter's key at a stand opens it on the stand" is on (the stand bar), and
        // only one he may use, as the setting says (review round 1 of Session 2, n11): at any other, the window opens plain,
        // and the note says why (round 2, j2).
        private static Stands.IStand StandAtPointer(out string note)
        {
            note = null;
            if (!Stands.Stands.Active || !Stands.StandSettings.PanelKey.Value)
            {
                return null;
            }
            Stands.IStand stand = null;
            string why = null;
            Module.Guard("finding the stand he points at", () =>
            {
                Stands.IStand pointed = Stands.Stands.Hovered();
                if (pointed == null || !pointed.Valid)
                {
                    return;
                }
                bool built = Stands.StandSettings.Which.Value == Stands.WhichStands.OnlyOnesIBuilt;
                if (pointed.MayUse(built, flash: false))
                {
                    stand = pointed;
                }
                else
                {
                    why = built && pointed.MayUse(false, flash: false)
                        ? "You didn't build this stand (your settings: only stands you built), so no stand bar."
                        : "You may not use this stand (a ward), so no stand bar.";
                }
            });
            note = why;
            return stand;
        }

        /// <summary>Closes the window at once and gives back its hold on the game's input (Jötunn counts each request:
        /// a pop-up still showing keeps its own). Also the module's undo step.</summary>
        internal static void Close()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            Closed();
            unblockNextFrame = false;
            Unblock();
        }

        // Closing on Escape or the key: the game's input comes back a frame later, so the same press can't also open the
        // game's menu (Escape) behind the closing window.
        private static void CloseSoon()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
            Closed();
            unblockNextFrame = true;
        }

        // What closing ends: highlighting, the loadout's highlighted cells, a drag under way, the edits' Undo, the cell
        // being edited, the hover help; the canvas then sizes for the pop-up alone.
        private static void Closed()
        {
            highlighting = false;
            ClearLoadoutHighlights();
            Drag.Cancel();
            StandMenu.Close();
            Tips.Forget();
            undo.Clear();
            selectedCell = null;
            standFocus = null;
            Ui.Content = Popup.Size;
        }

        /// <summary>Which panel shows beside the window.</summary>
        private enum Beside
        {
            None,
            NearbyItems,
            MyInventory,

            /// <summary>A food slot's rule (FoodPanel), opened by its button and closed by Done.</summary>
            FoodSlot,

            /// <summary>A loadout stand's places and what of the loadout each shows (StandPanel, 0.5.0).</summary>
            StandSlots,
        }

        // The panel beside the window last shown, for "The panel beside the window opens with": AsILeftIt (never the food
        // slot's: that one goes back to the panel before it).
        private static Beside leftBeside = Beside.None;

        // The panel that showed before a food slot's rule opened, which Done brings back.
        private static Beside beforeFood = Beside.None;

        private static Beside Shown => Drawer.Shown ? Beside.NearbyItems : InventoryPanel.Shown ? Beside.MyInventory : FoodPanel.Shown ? Beside.FoodSlot
                                     : StandPanel.Shown ? Beside.StandSlots : Beside.None;

        // What the canvas shows while the window is open: the window, with the panel beside it when one shows.
        private static Vector2 ContentSize => new Vector2(Width + (Shown != Beside.None ? Drawer.Width : 0f), Height);

        // Shows one panel beside the window, or none (each only with its setting on): the pair stays centred on the
        // screen, and "Window size" keeps both to it. His words: one "swaps the 'Nearby items' container for the Personal
        // Inventory window when clicked, and vice versa".
        private static void SetBeside(Beside which)
        {
            if (which == Beside.NearbyItems && !Settings.Drawer.Value)
            {
                which = Beside.None;
            }
            if (which == Beside.MyInventory && !Settings.InventoryPanel.Value)
            {
                which = Beside.None;
            }
            if (which == Beside.FoodSlot && !FoodPanel.Shown)
            {
                which = Beside.None;   // a food slot's rule opens only through OpenFood
            }
            if (which == Beside.StandSlots && !Stands.Stands.Active)
            {
                which = Beside.None;
            }
            Drawer.SetShown(which == Beside.NearbyItems);
            InventoryPanel.SetShown(which == Beside.MyInventory);
            StandPanel.SetShown(which == Beside.StandSlots);
            if (which != Beside.FoodSlot)
            {
                FoodPanel.Hide();
                leftBeside = which;
            }
            if (window != null)
            {
                ((RectTransform)window.transform).anchoredPosition = new Vector2(which != Beside.None ? -Drawer.Width / 2f : 0f, 0f);
            }
            Ui.Content = ContentSize;
            if (drawerButton != null)
            {
                Ui.SetButtonText(drawerButton, which == Beside.NearbyItems ? "Hide nearby items" : "Show nearby items");
            }
            if (inventoryButton != null)
            {
                Ui.SetButtonText(inventoryButton, which == Beside.MyInventory ? "Hide my inventory" : "Show my inventory");
            }
            if (standSlotsButton != null)
            {
                Ui.SetButtonText(standSlotsButton, which == Beside.StandSlots ? "Hide stand slots" : "Stand slots");
            }
        }

        // A panel's button: shows it, or hides it when it shows.
        private static void ToggleBeside(Beside which) => SetBeside(Shown == which ? Beside.None : which);

        // Opens a food slot's rule beside the window, in the place of the panel there (Done brings that one back).
        private static void OpenFood(Vector2i cell)
        {
            Loadout loadout = Selected;
            if (loadout == null)
            {
                return;
            }
            if (Shown != Beside.FoodSlot)
            {
                beforeFood = Shown;
            }
            Drawer.SetShown(false);
            InventoryPanel.SetShown(false);
            StandPanel.SetShown(false);
            FoodPanel.Open(loadout.Id, cell);
            SetBeside(Beside.FoodSlot);
        }

        private static void Unblock()
        {
            if (blocking)
            {
                blocking = false;
                GUIManager.BlockInput(false);
            }
        }

        // The window key, read where an error can't reach the game; a key the game's input can't read is said once and
        // not tried again until it changes (Item Stand Icons' lesson).
        private static bool KeyPressed()
        {
            KeyboardShortcut key = Settings.WindowKey.Value;
            if (key.MainKey == KeyCode.None || (keyFailed && key.Equals(failedKey)))
            {
                return false;
            }
            keyFailed = false;
            try
            {
                if (!ZInput.GetKeyDown(key.MainKey, false))
                {
                    return false;
                }
                return key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
            }
            catch (System.Exception error)
            {
                keyFailed = true;
                failedKey = key;
                Module.Warn($"The window key {key} can't be read ({error.Message}); choose another in the settings.");
                return false;
            }
        }

        // As the game decides whether the player takes input: no other window, menu, map or text box. His inventory may be
        // open: Outfitter's window takes its place.
        private static bool CanOpen()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() && !player.IsTeleporting() &&
                   GUIManager.CustomGUIFront != null &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !StoreGui.IsVisible() && !Menu.IsVisible() &&
                   (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() && !GameCamera.InFreeFly() &&
                   !Hud.IsPieceSelectionVisible() && !(InventoryGui.instance != null && InventoryGui.instance.IsContainerOpen());
        }

        private static void Show(Stands.IStand stand)
        {
            if (InventoryGui.IsVisible())
            {
                InventoryGui.instance.Hide();
            }
            if (window == null)
            {
                Build();
            }
            standFocus = stand;
            if (stand != null)
            {
                // The loadout the stand holds, if it's his, is the one picked.
                Stands.StandMark mark = Stands.StandMark.Read(stand);
                if (mark.Marked && mark.By == Stands.Stands.Me() && Store.Find(mark.Id) != null)
                {
                    selectedId = mark.Id;
                }
            }
            if (!modeChosen || Settings.Mode.Value != settingMode)
            {
                // The toggle starts at the setting, and follows it when it changes (R1, finding 31).
                settingMode = Settings.Mode.Value;
                mode = settingMode;
                modeChosen = false;
            }
            if (Store.Find(selectedId ?? string.Empty) == null)
            {
                selectedId = Store.All.Count > 0 ? Store.All[0].Id : null;
            }
            shownStore = shownPicks = shownSettings = shownHighlights = shownClipboard = -1;
            shownSelected = null;
            nextLook = 0f;
            // The panel beside it, by "The panel beside the window opens with" (as he left it, the nearby items, his
            // inventory, none).
            switch (Settings.DrawerOpening.Value)
            {
                case DrawerOpens.Always:
                    SetBeside(Beside.NearbyItems);
                    break;
                case DrawerOpens.AlwaysMyInventory:
                    SetBeside(Beside.MyInventory);
                    break;
                case DrawerOpens.Never:
                    SetBeside(Beside.None);
                    break;
                default:
                    SetBeside(leftBeside);
                    break;
            }
            Ui.FitCanvas();
            window.SetActive(true);
            window.transform.SetAsLastSibling();
            if (!blocking)
            {
                blocking = true;
                GUIManager.BlockInput(true);
            }
            unblockNextFrame = false;
            Refresh();
        }

        private static Loadout Selected => selectedId != null ? Store.Find(selectedId) : null;

        // ---- Keeping what it shows up to date ----

        private static void Refresh()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Close();
                return;
            }
            bool listChanged = Store.Version != shownStore;
            if (undo.Count > 0 && Store.Version != undoVersion)
            {
                undo.Clear();   // the list changed other than by the window's own edits: Undo would put back what isn't so (m1)
                lastFold = null;
            }
            if (listChanged)
            {
                shownStore = Store.Version;
                if (Selected == null)
                {
                    selectedId = Store.All.Count > 0 ? Store.All[0].Id : null;
                }
                ShowList();
            }
            bool settingsChanged = Settings.Version != shownSettings;
            if (Settings.Mode.Value != settingMode)
            {
                settingMode = Settings.Mode.Value;
                mode = settingMode;
                modeChosen = false;
            }
            shownSettings = Settings.Version;
            float now = Time.unscaledTime;
            bool look = now >= nextLook;
            if (look)
            {
                nextLook = now + 1f;
            }
            if (Picks.Version != seenPicks)
            {
                // His inventory's highlights changed (here, or by Right Alt in the game's own): highlights live in one grid
                // at a time, so the loadout's go (job 30, Session 3).
                seenPicks = Picks.Version;
                if (Picks.Count > 0)
                {
                    ClearLoadoutHighlights();
                }
            }
            bool redraw = listChanged || settingsChanged || look || shownSelected != selectedId || shownHighlighting != highlighting ||
                          Picks.Version != shownPicks || loadoutHighlightsVersion != shownHighlights || CellClipboard.Version != shownClipboard;
            if (!redraw)
            {
                return;
            }
            if (shownSelected != selectedId)
            {
                ClearLoadoutHighlights();   // another loadout picked: its cells aren't the ones highlighted
            }
            shownSelected = selectedId;
            shownHighlighting = highlighting;
            shownPicks = Picks.Version;
            shownHighlights = loadoutHighlightsVersion;
            shownClipboard = CellClipboard.Version;
            if (look || listChanged || settingsChanged)
            {
                ShowList();   // the worn mark follows what he wears
            }
            Loadout loadout = Selected;
            shape = GameHooks.Shape(player);   // once a refresh (R1, finding 8)
            equipStand = EquipStand(loadout);   // once a refresh, before the preview: its mode decides the loans (round 2, n1)
            preview = loadout != null && loadout.Items.Count > 0 ? Outfit.Preview(PreviewOf(loadout)) : null;
            ShowGrid(player, loadout);
            ShowControls(player, loadout);
        }

        private static void ClearLoadoutHighlights()
        {
            if (loadoutHighlights.Count > 0)
            {
                loadoutHighlights.Clear();
                loadoutHighlightsVersion++;
            }
        }

        // The preview's request: the loadout itself, never a copy, since the grid and the info column find what's missing
        // (and a food slot's food) by the loadout's own items (job 30, Session 3: the food agent's window-preview-copy; the
        // red marks had never matched). The preview only reads it.
        private static EquipRequest Request(Loadout loadout) => new EquipRequest
        {
            Loadout = loadout,
            Mode = Settings.AskMode.Value ? mode : Settings.Mode.Value,
            Source = "Outfitter",
        };

        // The preview's request, through the loadout's stand when the window's Equip would go through it ("Equip in the
        // window puts a stand's loadout on at its stand", A46 decision 47): the chests found around the stand, and the pieces
        // his stands would give counted as there (no red for them).
        private static EquipRequest PreviewOf(Loadout loadout)
        {
            EquipRequest request = Request(loadout);
            if (equipStand != null && !Settings.AskMode.Value)
            {
                request.Mode = Stands.StandJob.Mode();   // as the stand's Equip would (review round 1, j1), its loans too (round 2, n1)
            }
            Stands.StandJob.PreviewRequest(request);
            return request;
        }

        // The stand the window's Equip goes through for the loadout picked, as the last refresh found it (null: none).
        private static Stands.IStand equipStand;

        // Why "Make a food slot" would be refused for the cell picked, as the last refresh found it (null: it wouldn't).
        private static string foodWhyNot;

        // The stand Equip goes through, named as his messages name it: its own name, else by its loadout ("Raid's stand"),
        // so two unnamed stands are told apart (review round 2, n4).
        private static string StandCalled(Stands.IStand stand, Loadout loadout)
        {
            string own = Stands.StandTitle.Read(stand);
            if (own.Length > 0 || loadout == null)
            {
                return own.Length > 0 ? own : "its stand";
            }
            return loadout.Name.IndexOf("stand", System.StringComparison.OrdinalIgnoreCase) >= 0 ? loadout.Name : loadout.Name + "'s stand";
        }

        // The stand the window's Equip would go through for this loadout, or null.
        private static Stands.IStand EquipStand(Loadout loadout)
        {
            Player player = Player.m_localPlayer;
            return loadout != null && player != null ? Stands.StandJob.WindowStand(loadout, player.transform.position) : null;
        }

        // Equip's request: a copy, read once as the equip begins, so an edit meanwhile changes nothing under way.
        private static EquipRequest EquipFor(Loadout loadout)
        {
            EquipRequest request = Request(loadout);
            request.Loadout = loadout.Clone();
            return request;
        }

        private static void ShowList()
        {
            IReadOnlyList<Loadout> loadouts = Store.All;
            while (rows.Count < loadouts.Count)
            {
                rows.Add(MakeRow(rows.Count));
            }
            Player player = Player.m_localPlayer;
            // Each loadout's stand: the stands near him marked with it by him (job 30, Session 2), named by the nearest he
            // may use, else the nearest, and counted when there's more than one (review round 2 of Session 2, m1).
            bool stands = Stands.Stands.Active && player != null;
            var standOf = new Dictionary<string, (string Name, bool Usable, int Count)>();
            standsNear = stands ? Stands.StandEdits.Near(player.transform.position) : new List<Stands.StandEdits.Listed>();
            foreach (Stands.StandEdits.Listed near in standsNear)
            {
                if (!near.Mine || !near.Mark.Marked)
                {
                    continue;
                }
                if (standOf.TryGetValue(near.Mark.Id, out (string Name, bool Usable, int Count) had))
                {
                    bool better = !had.Usable && near.Usable;
                    standOf[near.Mark.Id] = (better ? near.Name : had.Name, had.Usable || near.Usable, had.Count + 1);
                }
                else
                {
                    standOf[near.Mark.Id] = (near.Name, near.Usable, 1);
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                ListRow row = rows[i];
                bool shown = i < loadouts.Count;
                if (row.Clicks.gameObject.activeSelf != shown)
                {
                    row.Clicks.gameObject.SetActive(shown);
                }
                if (!shown)
                {
                    continue;
                }
                Loadout loadout = loadouts[i];
                bool picked = loadout.Id == selectedId;
                row.Clicks.Normal = picked ? Ui.RowPicked : Ui.RowPlain;
                row.Background.color = row.Clicks.Normal;
                // ↑ and ↓ on the row picked (his words, 2026-10-07), each faded at its end of the list.
                bool moves = picked && loadouts.Count > 1;
                Show(row.Up, moves);
                Show(row.Down, moves);
                if (moves)
                {
                    bool writable = Store.Writable;
                    Ui.Set(row.Up, i > 0 && writable ? Ui.Look.On : Ui.Look.Off);
                    Ui.Set(row.Down, i < loadouts.Count - 1 && writable ? Ui.Look.On : Ui.Look.Off);
                }
                Ui.Place(row.Name.gameObject, 8f, 2f, moves ? 172f : 228f, 22f);
                string worn = player != null && Outfit.Wears(player, loadout) ? "  (worn)" : string.Empty;
                row.Name.text = loadout.Name + worn;
                if (row.StandBox.activeSelf != stands)
                {
                    row.StandBox.SetActive(stands);
                }
                row.Stand.text = !stands ? string.Empty
                    : standOf.TryGetValue(loadout.Id, out (string Name, bool Usable, int Count) on)
                        ? $"Stand: {on.Name}{(on.Count > 1 ? $" (+{on.Count - 1})" : string.Empty)}"
                    : "Stand: none nearby";
            }
        }

        // A row of the loadout list: two lines, the name, and the stand it's on (words only: a click anywhere on the row
        // picks the loadout and nothing more; Move to stand… moves it: his words, 2026-10-06, job 30, Session 3).
        private static ListRow MakeRow(int index)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(list, false);
            LayoutElement element = row.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 46f;
            Image background = row.AddComponent<Image>();
            background.color = Ui.RowPlain;
            Clicks clicks = row.AddComponent<Clicks>();
            clicks.Background = background;
            clicks.OnLeft = () => Pick(index);
            Text name = Ui.Label(row.transform, string.Empty, 8f, 2f, 228f, 22f, 16);
            GameObject standBox = Ui.Box("Stand", row.transform, 8f, 24f, 228f, 20f);
            Text stand = Ui.Label(standBox.transform, string.Empty, 0f, 0f, 228f, 20f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            Button up = Arrow(row.transform, "\u2191", 184f, () => MoveRow(index, -1));
            Ui.Help(up, "Moves this loadout up the list.");
            Button down = Arrow(row.transform, "\u2193", 212f, () => MoveRow(index, 1));
            Ui.Help(down, "Moves this loadout down the list.");
            up.gameObject.SetActive(false);
            down.gameObject.SetActive(false);
            return new ListRow { Clicks = clicks, Background = background, Name = name, Stand = stand, StandBox = standBox, Up = up, Down = down };
        }

        // A row's ↑ or ↓: a small button, its arrow in Unity's own font (the game's may lack arrows), "^" or "v" without it.
        private static Button Arrow(Transform row, string arrow, float x, System.Action clicked)
        {
            Button button = Ui.Button(row, arrow, x, 2f, 26f, 22f, () => Module.Guard("moving a loadout in the list", clicked));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                Font font = ArrowFont();
                if (font != null)
                {
                    label.font = font;
                }
                else
                {
                    label.text = arrow == "\u2191" ? "^" : "v";
                }
                label.fontSize = 16;
                label.resizeTextForBestFit = false;
            }
            return button;
        }

        private static Font arrowFont;
        private static bool arrowFontRead;

        private static Font ArrowFont()
        {
            if (!arrowFontRead)
            {
                arrowFontRead = true;
                try
                {
                    arrowFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    arrowFont = null;
                }
            }
            return arrowFont;
        }

        // ↑ or ↓ on the row picked: the loadout one place up or down his list (Undo keeps every entry: it never changes a loadout).
        private static void MoveRow(int index, int by)
        {
            IReadOnlyList<Loadout> loadouts = Store.All;
            if (index < 0 || index >= loadouts.Count || index + by < 0 || index + by >= loadouts.Count)
            {
                return;
            }
            if (!Store.Writable)
            {
                status.text = Store.WhyNotWritable;
                return;
            }
            Loadout moving = loadouts[index];
            if (Store.Move(moving.Id, index + by))
            {
                UndoStaysGood();
                selectedId = moving.Id;
                status.text = $"Moved {moving.Name} {(by < 0 ? "up" : "down")}.";
            }
            else
            {
                status.text = Store.WhyNotWritable;
            }
            nextLook = 0f;
        }

        private const float MoveStandY = 616f;

        // Move to stand…: the stands' dropdown for the loadout picked.
        private static void MoveToStand()
        {
            Loadout loadout = Selected;
            if (loadout != null)
            {
                StandsFor(loadout);
            }
        }

        // The stands' dropdown for a loadout: off its stand, or onto one of the stands near him (others' stands shown, not
        // offered: only they change those).
        private static void StandsFor(Loadout loadout)
        {
            Player player = Player.m_localPlayer;
            if (loadout == null || player == null || !Stands.Stands.Active)
            {
                return;
            }
            standsNear = Stands.StandEdits.Near(player.transform.position);
            var choices = new List<(string, bool, System.Action)>();
            bool onOne = standsNear.Exists(each => each.Mine && each.Mark.Id == loadout.Id);
            choices.Add(("Off its stand", onOne, () => Stands.StandEdits.MoveTo(loadout, null)));
            foreach (Stands.StandEdits.Listed near in standsNear)
            {
                string holds = !near.Mark.Marked ? "free"
                    : near.Mine ? (near.Mark.Id == loadout.Id ? "holds it now" : $"holds {(near.Loadout != null ? near.Loadout.Name : near.Mark.Name)}")
                    : $"{(near.Mark.ByName.Length > 0 ? near.Mark.ByName + "'s" : "another player's")}";
                // A stand he may not use is shown, not offered (review round 1 of Session 2, n11), nor someone else's
                // personal stand (review round 1 of Session 3, m3).
                string use = near.Blocked ? $", {(near.PersonalName.Length > 0 ? near.PersonalName + "'s" : "another player's")} personal stand"
                           : near.Usable ? string.Empty : ", not yours to use";
                bool offered = (!near.Mark.Marked || near.Mine) && near.Mark.Id != loadout.Id && near.Usable && !near.Blocked;
                Stands.IStand stand = near.Stand;
                choices.Add(($"{near.Name}, {near.Distance:0} m: {holds}{use}", offered, () => Stands.StandEdits.Assign(stand, loadout)));
            }
            if (standsNear.Count == 0)
            {
                choices.Add(($"No armour stand within {Stands.StandSettings.ListRange.Value:0} m", false, null));
            }
            // Beside its button, the menu's foot level with the button's (review round 1, n16).
            float bottom = MoveStandY + 34f;
            StandMenu.Show(window.transform, 276f, Mathf.Max(8f, bottom - StandMenu.HeightFor(choices.Count)), $"Put {loadout.Name} on a stand", choices);
        }

        private static void Pick(int index)
        {
            IReadOnlyList<Loadout> loadouts = Store.All;
            if (index >= 0 && index < loadouts.Count)
            {
                selectedId = loadouts[index].Id;
                selectedCell = null;
                status.text = string.Empty;
                ShowList();
            }
        }

        private static void ShowGrid(Player player, Loadout loadout)
        {
            grid.Shape(shape, GridRoom);
            if (selectedCell.HasValue && (highlighting || LoadoutEdit.At(loadout, shape, selectedCell.Value.x, selectedCell.Value.y) == null))
            {
                selectedCell = null;   // its item went (an edit, Undo, another loadout picked), or a click highlights now
            }
            grid.Select(selectedCell);
            foreach (KeyValuePair<string, SlotCell> slot in shape.Slots)
            {
                grid.Label(slot.Value.X, slot.Value.Y, slot.Key.StartsWith("Quick", System.StringComparison.Ordinal) ? slot.Key.Substring(5) : slot.Key);
            }
            foreach (Image image in anywhere)
            {
                image.enabled = false;
            }
            anywhereTitle.text = string.Empty;
            if (loadout == null)
            {
                header.text = Store.Writable || Store.All.Count > 0 ? "No loadout yet: Save new, or New (empty)" : Store.WhyNotWritable;
                return;
            }
            header.text = loadout.Name + (Outfit.Wears(player, loadout) ? "  (you wear it)" : string.Empty);
            var missing = new HashSet<LoadoutItem>();
            if (preview != null)
            {
                foreach (Want want in preview.Wants)
                {
                    if (want.Missing > 0)
                    {
                        foreach (LoadoutItem li in want.Items)
                        {
                            missing.Add(li);
                        }
                    }
                }
            }
            int loose = 0;
            foreach (LoadoutItem item in loadout.Items)
            {
                // A food slot shows the food it would take now (its own cell tinted green); red when none would come.
                bool slot = item.Food != null;
                ItemDrop.ItemData template = slot ? null : Codes.Template(item.Prefab);
                Sprite icon = slot ? FoodIcon(item) : IconOf(template, item.Variant);
                int level = template != null && template.m_shared.m_maxQuality > 1 ? item.Quality : 0;
                bool worn = item.Worn != WornAs.None;
                bool red = missing.Contains(item) || (!slot && template == null);
                if (Places.CellFor(item, shape, out int x, out int y))
                {
                    // A slot's cell counts what would come, at most a stack of its food (review round 1, n3).
                    ItemDrop.ItemData food = slot ? FoodOf(item) : null;
                    int count = food != null ? FoodComing(item, food) : item.Count;
                    grid.Set(x, y, icon, count, level, worn, red, false, slot ? "Food" : null, food: slot);
                }
                else
                {
                    if (loose < anywhere.Count)
                    {
                        Image image = anywhere[loose];
                        image.sprite = icon;
                        image.enabled = icon != null;
                        image.color = red ? Ui.Missing : Color.white;
                    }
                    loose++;
                }
            }
            foreach (Vector2i cell in loadoutHighlights)
            {
                grid.Pick(cell.x, cell.y, true);
            }
            if (loose > 0)
            {
                // Six show; the heading counts the rest (review round 1 of Session 2, n4).
                anywhereTitle.text = loose > anywhere.Count ? $"In any free cell (+{loose - anywhere.Count} more)" : "In any free cell";
            }
        }

        // ---- Food slots (job 30, Session 3; card A34, decision 72) ----

        // The food a slot would take now, by the preview (null: none, or no preview).
        private static ItemDrop.ItemData FoodOf(LoadoutItem slot) => slot?.Food != null ? preview?.FoodFor(slot) : null;

        // The food a slot would take now, in words ("Honey x10"), for the slot's panel; null when none would come.
        private static string ChosenFood(LoadoutItem slot)
        {
            ItemDrop.ItemData food = FoodOf(slot);
            return food == null ? null : $"{Messages.Localize(food.m_shared.m_name)} x{FoodComing(slot, food)}";
        }

        // How many of its food a slot would have, put on now: what the preview has and fetches for it (a slot putting its stat
        // first may settle for fewer than its count: review 1's M1), else its count, at most a stack.
        private static int FoodComing(LoadoutItem slot, ItemDrop.ItemData food)
        {
            int whole = FoodPick.Target(slot, food);
            Want want = preview?.Wants.Find(each => each.IsFoodSlot && ReferenceEquals(each.First, slot));
            int got = want != null && want.Template != null ? want.HaveCount + want.TakeCount : 0;
            return got > 0 ? Mathf.Min(got, whole) : whole;
        }

        // What a slot would do put on now, in words, for its panel (review round 1, n2): the food and how many; in Swap
        // gear only, nothing; else why no food would come.
        private static string FoodNow(LoadoutItem slot)
        {
            if (slot?.Food == null)
            {
                return null;
            }
            EquipMode now = equipStand != null && !Settings.AskMode.Value ? Stands.StandJob.Mode() : Settings.AskMode.Value ? mode : Settings.Mode.Value;
            if (now == EquipMode.SwapGearOnly)
            {
                return "Put on now in Swap gear only, food slots are left alone (as all food is).";
            }
            string chosen = ChosenFood(slot);
            if (chosen != null)
            {
                return $"Put on now, it would take {chosen}.";
            }
            string why = preview != null && preview.Foods.TryGetValue(slot, out FoodChoice choice) ? choice?.Why : null;
            return why != null ? $"Put on now, it would stay empty: {why}." : "Put on now, no food nearby fits it.";
        }

        // The food slot's button: its rule beside the window (a slot), a food made into a slot, or a new slot in the first
        // free cell of his rows from the bottom up (as food lands in the game).
        private static void FoodClicked()
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            LoadoutItem item = selectedCell.HasValue ? LoadoutEdit.At(loadout, shape, selectedCell.Value.x, selectedCell.Value.y) : null;
            if (item?.Food != null)
            {
                OpenFood(selectedCell.Value);
                return;
            }
            Vector2i cell;
            if (item != null)
            {
                cell = selectedCell.Value;
            }
            else if (!LoadoutEdit.FreeFromBottom(loadout, shape, out cell))
            {
                status.text = $"{loadout.Name} has no free cell left for a food slot.";
                return;
            }
            Loadout made = LoadoutEdit.MakeFoodSlot(loadout, shape, cell.x, cell.y, Settings.FoodSlotCount.Value, Codes.Template, out why);
            Apply(loadout, made, item != null ? $"{Messages.NameOf(item)} is a food slot now: it may take any food you tick." : "Added a food slot: it takes any food until you give it a rule.", why);
            if (made != null)
            {
                selectedCell = cell;
                OpenFood(cell);
            }
        }

        // A change to a slot's rule from its panel: saved at once, a run of changes to one slot one Undo entry.
        private static void FoodRuleChanged(Vector2i cell, FoodRule rule)
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            Loadout changed = LoadoutEdit.SetFoodRule(loadout, shape, cell.x, cell.y, rule, out why);
            Apply(loadout, changed, $"Food slot: {rule.Describe(Messages.FoodName)}, {(rule.First == FoodFirst.Amount ? "a whole count first" : "its stat first")}.", why,
                  fold: $"food {loadout.Id} {cell.x},{cell.y}");
        }

        // A slot's picture: the food it would take now, else its list's first food this game has, else none.
        private static Sprite FoodIcon(LoadoutItem slot)
        {
            ItemDrop.ItemData food = FoodOf(slot);
            if (food == null && slot?.Food != null)
            {
                foreach (string prefab in slot.Food.Foods)
                {
                    food = Codes.Template(prefab);
                    if (food != null)
                    {
                        break;
                    }
                }
            }
            return food != null ? IconOf(food, 0) : null;
        }

        private static Sprite IconOf(ItemDrop.ItemData template, int variant)
        {
            Sprite[] icons = template?.m_shared?.m_icons;
            if (icons == null || icons.Length == 0)
            {
                return null;
            }
            return icons[Mathf.Clamp(variant, 0, icons.Length - 1)];
        }

        // Every button's look by what it would do now (his words, 2026-10-06: lit when it acts on the highlighted cells,
        // faded almost away when it would do nothing; job 30, Session 3).
        private static void ShowControls(Player player, Loadout loadout)
        {
            bool has = loadout != null;
            bool writable = Store.Writable;
            bool codes = Settings.ShareCodes.Value;
            int picks = Picks.Count;
            int marked = loadoutHighlights.Count;
            bool savesPicks = Settings.SaveOnlyPicked.Value;
            Ui.Look Acts(bool does, bool onHighlights) => !does ? Ui.Look.Off : onHighlights ? Ui.Look.Lit : Ui.Look.On;

            // Save new: the loadout's highlighted cells as a new loadout, else his inventory's (his words, 2026-10-07: "when I
            // highlight a bunch of slots, I can then click 'Save new' to save the highlighted slots to a new Loadout").
            bool fromLoadout = has && marked > 0;
            Ui.Set(saveNewButton, Acts(writable && !Store.Full && (fromLoadout || !savesPicks || picks > 0), fromLoadout || (savesPicks && picks > 0)));
            Ui.Set(saveOverButton, Acts(has && writable && (!savesPicks || picks > 0), savesPicks && picks > 0));
            Ui.Set(renameButton, Acts(has && writable, false));
            Ui.Set(deleteButton, Acts(has && writable, false));
            Show(copyButton, codes);
            Show(pasteButton, codes);
            Ui.Set(copyButton, Acts(has, false));
            Ui.Set(pasteButton, Acts(writable && !Store.Full, false));
            Ui.Set(newButton, Acts(writable && !Store.Full, false));
            Ui.Set(undoButton, Acts(undo.Count > 0 && writable, false));
            Show(moveStandButton, Stands.Stands.Active);
            Show(standSlotsButton, Stands.Stands.Active);
            Ui.Set(standSlotsButton, StandPanel.Shown ? Ui.Look.Lit : has ? Ui.Look.On : Ui.Look.Off);
            if (StandPanel.Shown && !Stands.Stands.Active)
            {
                SetBeside(Beside.None);   // the stands went off with the panel open
            }
            Ui.Set(moveStandButton, Acts(has && !Stands.StandJob.Busy && !Outfit.Busy, false));   // every pick claims (round 2, n2)

            Ui.Set(highlightButton, highlighting ? Ui.Look.Lit : Ui.Look.On);
            Ui.SetButtonText(highlightButton, highlighting ? "Highlighting: on" : "Highlight cells");
            int highlighted = marked > 0 ? marked : picks;
            Ui.Set(copyCellsButton, Acts(highlighted > 0, true));
            Ui.SetButtonText(copyCellsButton, highlighted > 0 ? $"Copy cells ({highlighted})" : "Copy cells");
            Ui.Set(pasteCellsButton, Acts(CellClipboard.Count > 0 && has && writable, false));
            Ui.SetButtonText(pasteCellsButton, CellClipboard.Count > 0 ? $"Paste cells ({CellClipboard.Count})" : "Paste cells");
            Ui.Set(clearHighlightsButton, Acts(highlighted > 0, true));

            // The modes: under their heading, the chosen one lit, the other two faded until the pointer is over them. Beside
            // them the loadout's own favourites switch (his words, 2026-10-07), lit while on.
            bool ask = Settings.AskMode.Value;
            Show(modeHeading, ask);
            Show(favouritesButton, has);
            if (has)
            {
                bool go = FavouritesGo(loadout);
                Ui.Set(favouritesButton, !writable ? Ui.Look.Off : go ? Ui.Look.Lit : Ui.Look.On);
                Ui.SetButtonText(favouritesButton, go ? "Favourites go too" : "Favourites stay");
            }
            for (int i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].gameObject.SetActive(ask);
                bool chosen = (EquipMode)i == mode;
                Image image = modeButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = chosen ? new Color(1f, 0.75f, 0.4f, 1f) : Color.white;
                }
                Ui.Set(modeButtons[i], Ui.Look.On);
                Ui.SetFaded(modeButtons[i], !chosen);
            }
            // An empty loadout puts nothing on: its equip would only take things off him (review round 1 of Session 2, m2).
            Ui.Set(equipButton, Acts(has && loadout.Items.Count > 0 && !Outfit.Busy && !Stands.StandJob.Busy, false));
            Ui.SetButtonText(equipButton, "Equip now");   // never "Equip at" a stand's name: read as a biome's (his words, 2026-10-07)
            weight.text = WeightLine(player);
            weight.color = Ui.Plain;
            if (preview != null && Settings.WeightWarning.Value && After(player) > player.GetMaxCarryWeight())
            {
                weight.color = Ui.Bad;
            }
            Show(drawerButton, Settings.Drawer.Value);
            Show(inventoryButton, Settings.InventoryPanel.Value);
            if ((!Settings.Drawer.Value && Drawer.Shown) || (!Settings.InventoryPanel.Value && InventoryPanel.Shown))
            {
                SetBeside(Beside.None);   // its setting went off with the panel open
            }
            ShowEditControls(loadout);
            ShowStandBar(loadout);
            ShowInfo(player, loadout);
        }

        // The stand bar, while the window is open on a stand: its name, what it holds, Rename, Unassign, and Assign to armor
        // stand for the loadout picked on the left.
        private static void ShowStandBar(Loadout loadout)
        {
            bool shown = standFocus != null && standFocus.Valid && Stands.Stands.Active;
            if (standBar.activeSelf != shown)
            {
                standBar.SetActive(shown);
            }
            if (!shown)
            {
                return;
            }
            string name = Stands.StandTitle.Shown(standFocus, Stands.Stands.GameName(standFocus));
            Stands.StandMark mark = Stands.StandMark.Read(standFocus);
            long me = Stands.Stands.Me();
            bool mine = mark.Marked && mark.By == me;
            string holds;
            if (!mark.Marked)
            {
                holds = "holds no loadout";
            }
            else if (mine)
            {
                Loadout held = Store.Find(mark.Id);
                holds = $"holds {(held != null ? held.Name : mark.Name)}";
            }
            else
            {
                holds = $"holds {(mark.ByName.Length > 0 ? mark.ByName + "'s" : "another player's")} {mark.Name}";
            }
            // Whose personal stand it is (A46 decision 45): someone else's leaves every button of his but the help off.
            (long whose, string whoseName) = Stands.StandPersonal.Read(standFocus);
            bool theirs = whose != 0 && whose != me;
            string personal = whose == 0 ? string.Empty : whose == me ? ", personal" : $", {(whoseName.Length > 0 ? whoseName + "'s" : "another player's")} personal stand";
            standLabel.text = name + "\n" + holds + personal;
            Show(unassignButton, mine);
            Ui.Set(unassignButton, theirs ? Ui.Look.Off : Ui.Look.On);
            Ui.Set(assignButton, loadout != null && !(mine && mark.Id == loadout.Id) && !(mark.Marked && !mine) && !theirs ? Ui.Look.On : Ui.Look.Off);
            // Another player's loadout on it: only they rename it (j1); someone else's personal stand likewise.
            Ui.Set(renameStandButton, !(mark.Marked && !mine) && !theirs ? Ui.Look.On : Ui.Look.Off);
            Ui.Set(personalButton, !Stands.StandPersonal.MayToggle(standFocus) ? Ui.Look.Off : whose == me ? Ui.Look.Lit : Ui.Look.On);
            Ui.SetButtonText(personalButton, whose == me ? "Personal: on" : "Personal");
        }

        // The stands' own name box (taken down with them), its own name filled in: empty gives it the game's name back (m4).
        private static void RenameStand()
        {
            Stands.IStand stand = standFocus;
            if (stand == null || !stand.Valid)
            {
                return;
            }
            Stands.StandEdits.AskRename(stand);
        }

        private static void AssignHere()
        {
            Loadout loadout = Selected;
            if (standFocus == null || loadout == null)
            {
                return;
            }
            Stands.StandEdits.Assign(standFocus, loadout);
            nextLook = 0f;
        }

        // The edit controls beside the grid, for the cell being edited: its count (a stackable's), worn or carried (what
        // can be worn or held), and taking it out.
        private static void ShowEditControls(Loadout loadout)
        {
            LoadoutItem item = selectedCell.HasValue && shape != null
                ? LoadoutEdit.At(loadout, shape, selectedCell.Value.x, selectedCell.Value.y)
                : null;
            ItemDrop.ItemData template = item != null && item.Food == null ? Codes.Template(item.Prefab) : null;
            bool slot = item?.Food != null;
            bool stackable = LoadoutEdit.Stackable(template) || slot;   // a food slot counts what it brings, 1 to 100
            bool wearable = template != null && (Matching.BodyPlace(template.m_shared.m_itemType) != WornAs.None || template.IsEquipable());
            Show(lessButton, stackable);
            Show(moreButton, stackable);
            Show(countField, stackable);
            if (stackable)
            {
                // Each faded when it would do nothing: at 1, at a full stack (a food slot's 100) (review round 1, m6).
                int most = slot ? FoodRule.MaxCount : Mathf.Max(1, template.m_shared.m_maxStackSize);
                Ui.Set(lessButton, item.Count > 1 && Store.Writable ? Ui.Look.On : Ui.Look.Off);
                Ui.Set(moreButton, item.Count < most && Store.Writable ? Ui.Look.On : Ui.Look.Off);
            }
            if (stackable && !countField.isFocused)
            {
                countField.text = item.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            // The food slot's button: its rule (a slot), make one of a food, or add one in the first free cell.
            string food = slot ? "Edit food slot" : FoodPick.IsFood(template) ? "Make a food slot" : item == null && loadout != null && !highlighting ? "Add a food slot" : null;
            Show(foodButton, food != null);
            if (food != null)
            {
                Ui.SetButtonText(foodButton, food);
                // Adding or making one needs room for a slot (a free cell, fewer than 16), and a list that can be written.
                bool can = Store.Writable && (slot || (loadout.FoodSlots < Loadout.MaxFoodSlots && (item != null || LoadoutEdit.FreeFromBottom(loadout, shape, out Vector2i _))));
                // Made of a food: faded when it would be refused (another cell names that food), its tip saying why
                // (review round 2, j2; decision 68).
                foodWhyNot = null;
                if (can && !slot && item != null &&
                    LoadoutEdit.MakeFoodSlot(loadout, shape, selectedCell.Value.x, selectedCell.Value.y, 1, Codes.Template, out string refused) == null)
                {
                    foodWhyNot = refused;
                    can = !Settings.HoverHelp.Value;   // with no tip to say why, a click says it (the last look's n5)
                }
                Ui.Set(foodButton, slot && FoodPanel.Edits(loadout.Id, selectedCell.Value) ? Ui.Look.Lit : can ? Ui.Look.On : Ui.Look.Off);
            }
            // Worn and Displayed, each its own switch, lit while on (his words, 2026-10-07): Displayed for what a loadout stand
            // could show, while the stands work.
            Show(wornButton, item != null && wearable);
            if (item != null && wearable)
            {
                bool fixedSlot = LoadoutEdit.SlotWears(shape, selectedCell.Value.x, selectedCell.Value.y) != WornAs.None;
                Ui.Set(wornButton, fixedSlot || !Store.Writable ? Ui.Look.Off : item.Worn != WornAs.None ? Ui.Look.Lit : Ui.Look.On);
                Ui.SetButtonText(wornButton, item.Worn != WornAs.None ? "Worn: on" : "Worn: off");
            }
            bool showable = item != null && template != null && Stands.Stands.Active && Stands.StandPrototype.FitForPrefab(item.Prefab) >= 0;
            Show(shownButton, showable);
            if (showable)
            {
                Ui.Set(shownButton, !Store.Writable ? Ui.Look.Off : item.Displayed ? Ui.Look.Lit : Ui.Look.On);
                Ui.SetButtonText(shownButton, item.Displayed ? "Displayed: on" : "Displayed: off");
            }
            Show(takeOutButton, item != null);
        }

        // Whether putting a loadout on puts his favourites away: its own switch, else the setting's.
        private static bool FavouritesGo(Loadout loadout) => loadout?.FavouritesGo ?? Settings.FavouritesGo.Value;

        // The favourites switch beside the modes: this loadout's own from now on (Undo takes it back).
        private static void ToggleFavourites()
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            Loadout changed = loadout.Clone();
            changed.FavouritesGo = !FavouritesGo(loadout);
            Apply(loadout, changed, changed.FavouritesGo == true
                ? $"Putting {loadout.Name} on puts your favourites away too, like any other item."
                : $"Putting {loadout.Name} on leaves your favourites as your settings say (Leave my favourites alone, My favourite gear in an equip).", null);
        }

        // Displayed (shown on a loadout stand), for the cell being edited.
        private static void ToggleShown()
        {
            string why = null;
            if (!selectedCell.HasValue || !CanEdit(out why))
            {
                status.text = why ?? status.text;
                return;
            }
            Vector2i cell = selectedCell.Value;
            Loadout loadout = Selected;
            LoadoutItem item = LoadoutEdit.At(loadout, shape, cell.x, cell.y);
            if (item == null)
            {
                return;
            }
            bool show = !item.Displayed;
            Loadout changed = LoadoutEdit.SetShown(loadout, shape, cell.x, cell.y, show);
            Apply(loadout, changed, show
                ? $"{Messages.NameOf(item)} shows on a loadout stand now{(item.Worn != WornAs.None ? string.Empty : ", and stays carried when you put it on")}."
                : $"{Messages.NameOf(item)} doesn't show on a loadout stand now.", null);
        }

        private static void Show(Component thing, bool shown)
        {
            if (thing != null && thing.gameObject.activeSelf != shown)
            {
                thing.gameObject.SetActive(shown);
            }
        }

        // What he'll carry after the equip, by the preview: what he carries, less what goes away, plus what comes.
        private static float After(Player player)
        {
            float total = player.GetInventory().GetTotalWeight();
            if (preview == null)
            {
                return total;
            }
            foreach (Away goes in preview.Aways)
            {
                total -= goes.Item.GetWeight(goes.Amount);
            }
            foreach (Take take in preview.Takes)
            {
                ItemDrop.ItemData item = take.Copy ?? take.Want.Template;
                if (item != null)
                {
                    total += item.GetWeight(take.Amount);
                }
            }
            return Mathf.Max(0f, total);
        }

        private static string WeightLine(Player player)
        {
            if (preview == null)
            {
                // Why Equip is grey (review round 2 of Session 2, n2).
                Loadout picked = Selected;
                return picked != null && picked.Items.Count == 0
                    ? $"{picked.Name} is empty: drag items into it from the nearby items or your inventory (beside the window) before you put it on."
                    : string.Empty;
            }
            string carry = $"You'll carry {After(player):0} of {player.GetMaxCarryWeight():0}.";
            int missing = preview.MissingCount;
            string lacks = missing > 0 ? $" {missing} missing: {Messages.MissingWords(preview, 2)}." : string.Empty;
            int fromStands = 0;
            foreach (Take take in preview.Takes)
            {
                if (Exchange.IsLent(take))
                {
                    fromStands += take.Amount;
                }
            }
            int fromChests = preview.FetchCount - fromStands;
            string comes = (fromChests > 0 ? $" {fromChests} from chests." : string.Empty) +
                           (fromStands > 0 ? $" {fromStands} from your stands." : string.Empty);
            return carry + comes + lacks;
        }

        private static void ShowInfo(Player player, Loadout loadout)
        {
            Vector2i? shown = hovered ?? selectedCell;
            if (shown == null)
            {
                info.text = loadout == null ? string.Empty
                    : $"{loadout.Items.Count} item{(loadout.Items.Count == 1 ? "" : "s")}." + (loadout.SavedBy == 0 ? " From a code." : string.Empty) +
                      (highlighting
                          ? "\n\nHighlighting: click cells to highlight them (again to take it off), then Copy cells."
                          : "\n\nDrag items in from the panel beside the window, or between cells; click a cell to change it.");
                return;
            }
            Vector2i cell = shown.Value;
            if (shape == null)
            {
                return;
            }
            foreach (LoadoutItem li in loadout?.Items ?? new List<LoadoutItem>())
            {
                if (!Places.CellFor(li, shape, out int x, out int y) || x != cell.x || y != cell.y)
                {
                    continue;
                }
                string line = Messages.NameOf(li) + (li.Count > 1 ? " x" + li.Count : string.Empty);
                if (li.Worn != WornAs.None)
                {
                    line += li.Shown == false && Stands.Stands.Active ? "\nWorn, not shown on a stand" : "\nWorn";
                }
                else if (li.Displayed && Stands.Stands.Active)
                {
                    line += "\nShown on a stand, carried";
                }
                string food = li.Food != null ? ChosenFood(li) : null;
                if (food != null)
                {
                    line += $"\nNow it would take {food}";
                }
                if (preview != null)
                {
                    foreach (Want want in preview.Wants)
                    {
                        if (want.Items.Contains(li) && want.Missing > 0)
                        {
                            line += $"\nMissing: {want.Why}";
                        }
                        else if (want.Items.Contains(li) && want.TakeCount > 0)
                        {
                            line += want.Takes.TrueForAll(Exchange.IsLent) ? "\nFrom your stand" : "\nFrom a chest nearby";
                        }
                    }
                }
                info.text = line;
                return;
            }
            info.text = string.Empty;
        }

        // ---- What the buttons do ----

        private static void CellClicked(Vector2i cell)
        {
            if (highlighting)
            {
                // Highlighting: the cell's highlight goes on or off; highlights live in one grid at a time, so his
                // inventory's go (job 30, Session 3).
                if (Selected != null && shape != null && shape.Usable(cell.x, cell.y))
                {
                    if (!loadoutHighlights.Remove(cell))
                    {
                        loadoutHighlights.Add(cell);
                    }
                    loadoutHighlightsVersion++;
                    if (loadoutHighlights.Count > 0)
                    {
                        Picks.Clear();
                        seenPicks = Picks.Version;
                    }
                }
                return;
            }
            // Editing: a cell holding an item is the one the controls beside the grid change (again: none). A food slot's
            // rule opens beside the window at the click (his words, 2026-10-07), and goes with the slot's frame.
            LoadoutItem holds = shape != null ? LoadoutEdit.At(Selected, shape, cell.x, cell.y) : null;
            bool again = selectedCell.HasValue && selectedCell.Value.x == cell.x && selectedCell.Value.y == cell.y;
            selectedCell = holds != null && !again ? cell : (Vector2i?)null;
            grid.Select(selectedCell);
            nextLook = 0f;
            if (holds?.Food != null && Selected != null)
            {
                if (!again)
                {
                    OpenFood(cell);
                }
                else if (FoodPanel.Edits(Selected.Id, cell))
                {
                    FoodPanel.Done?.Invoke();
                }
            }
        }

        // A click on a cell of his inventory beside the window: its highlight, while highlighting (and the window may
        // pick: "Picking the cells to save"); the loadout's highlights go.
        private static void InventoryCellClicked(Vector2i cell)
        {
            Player player = Player.m_localPlayer;
            if (!InventoryHighlights() || player == null)
            {
                status.text = !highlighting
                    ? "Press Highlight cells first, then click the cells of your inventory to highlight them."
                    : "Your settings pick cells only with the key (Picking the cells to save: HeldKeyAndClicks).";
                return;
            }
            if (GameHooks.Shape(player).Usable(cell.x, cell.y))
            {
                Picks.Toggle(cell);
                ClearLoadoutHighlights();
            }
        }

        // Whether a click in his inventory beside the window highlights now.
        private static bool InventoryHighlights() => highlighting && Settings.Picking.Value != PickingCells.HeldKeyAndClicks;

        private static void ToggleHighlighting()
        {
            highlighting = !highlighting;
            selectedCell = null;
            hovered = null;
            status.text = highlighting
                ? "Highlighting: click cells of the loadout, or of your inventory (Show my inventory), to highlight them."
                : string.Empty;
            nextLook = 0f;
        }

        private static void ClearHighlights()
        {
            ClearLoadoutHighlights();
            Picks.Clear();
            nextLook = 0f;
        }

        // Copy cells: the highlighted cells (the loadout's, else his inventory's) onto Outfitter's own clipboard.
        private static void CopyCells()
        {
            Player player = Player.m_localPlayer;
            Loadout loadout = Selected;
            if (player == null)
            {
                return;
            }
            List<LoadoutItem> items;
            string from;
            if (loadoutHighlights.Count > 0 && loadout != null && shape != null)
            {
                items = CellClipboard.FromLoadout(loadout, shape, loadoutHighlights);
                from = loadout.Name;
            }
            else if (Picks.Count > 0)
            {
                items = Capture.Cells(player, string.Empty, Picks.All()).Items.ToList();
                from = "your inventory";
            }
            else
            {
                status.text = "Highlight cells first: press Highlight cells and click them, in the loadout or in your inventory.";
                return;
            }
            if (items.Count == 0)
            {
                status.text = "The highlighted cells are empty: nothing to copy.";
                return;
            }
            CellClipboard.Set(items, from);
            status.text = $"Copied {items.Count} item{(items.Count == 1 ? "" : "s")} from {from}: pick a loadout and press Paste cells.";
        }

        // Paste cells: what's copied into the loadout picked, each into its own cell where that's free, else the first free
        // one; all or nothing; one Undo.
        private static void PasteCells()
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            List<LoadoutItem> items = CellClipboard.Items();
            Loadout pasted = CellClipboard.Paste(loadout, shape, items, Codes.Template, out why);
            Apply(loadout, pasted, $"Pasted {items.Count} item{(items.Count == 1 ? "" : "s")} from {CellClipboard.From} into {loadout.Name}.", why);
        }

        // ---- Editing a loadout by hand (job 30, Session 2) ----

        // Whether the picked loadout may be edited now, and if not, why.
        private static bool CanEdit(out string why)
        {
            why = null;
            if (Selected == null)
            {
                why = "Pick a loadout on the left first, or press New.";
            }
            else if (!Store.Writable)
            {
                why = Store.WhyNotWritable;
            }
            else if (shape == null)
            {
                why = "Your inventory isn't there yet.";
            }
            return why == null;
        }

        // Saves an edit, keeping the loadout as it was for Undo; edits of one kind close together (fold: the same text) are
        // one Undo entry, the first one's "before".
        private static void Apply(Loadout before, Loadout changed, string done, string why, string fold = null)
        {
            if (changed == null)
            {
                if (!string.IsNullOrEmpty(why))
                {
                    status.text = why;
                }
                return;
            }
            if (!Store.Save(changed))
            {
                status.text = Store.WhyNotSaved;
                return;
            }
            float now = Time.unscaledTime;
            bool folded = fold != null && fold == lastFold && now - lastFoldTime <= FoldSeconds && undo.Count > 0 && undo[undo.Count - 1].Id == before.Id;
            if (!folded)
            {
                undo.Add(before.Clone());
                if (undo.Count > MostUndos)
                {
                    undo.RemoveAt(0);
                }
            }
            lastFold = fold;
            lastFoldTime = now;
            undoVersion = Store.Version;
            int starved = NewlyStarved(before, changed);
            if (starved > 0)
            {
                // The loadout names as items every food a slot's list ticks: that slot can take none (review round 2, j1).
                done = $"{done} {(starved == 1 ? "A food slot" : starved + " food slots")} of it can take none of {(starved == 1 ? "its" : "their")} ticked foods now: the loadout names them as items (you eat one of each).".Trim();
            }
            status.text = done;
            nextLook = 0f;
        }

        // How many of a loadout's "From a list" slots tick only foods it names as items of their own (none of which a slot
        // then takes) after an edit that didn't before, each slot by its place (the last look's n6).
        private static int NewlyStarved(Loadout before, Loadout after)
        {
            HashSet<string> was = Starved(before);
            int starved = 0;
            foreach (string place in Starved(after))
            {
                if (!was.Contains(place))
                {
                    starved++;
                }
            }
            return starved;
        }

        // The places of a loadout's starved "From a list" slots.
        private static HashSet<string> Starved(Loadout loadout)
        {
            var named = new HashSet<string>();
            var starved = new HashSet<string>();
            if (loadout == null)
            {
                return starved;
            }
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item.Food == null)
                {
                    named.Add(item.Prefab);
                }
            }
            foreach (LoadoutItem slot in loadout.Items)
            {
                if (slot.Food != null && slot.Food.Mode == FoodMode.FromList && slot.Food.Foods.Count > 0 && slot.Food.Foods.TrueForAll(named.Contains))
                {
                    starved.Add(PlaceKey(slot));
                }
            }
            return starved;
        }

        // An item's kept place in words (its kind of cell, column, rows up and slot), whatever the grid.
        private static string PlaceKey(LoadoutItem item) => $"{item.Cell}/{item.Column}/{item.RowsUp}/{item.SlotId}";

        // A loadout's food slots' places, in its order, as one string (to see whether an Undo moved one).
        private static string SlotPlaces(Loadout loadout)
        {
            if (loadout == null)
            {
                return string.Empty;
            }
            var places = new List<string>();
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item.Food != null)
                {
                    places.Add(PlaceKey(item));
                }
            }
            return string.Join("|", places);
        }

        // A change of the window's own outside the edits (a rename, a save over, a delete): Undo forgets that loadout's
        // entries, which would quietly take it back (m1); the others stay good.
        private static void ForgetUndo(string id)
        {
            undo.RemoveAll(each => each.Id == id);
            lastFold = null;
            undoVersion = Store.Version;
        }

        // A new loadout saved by the window (Save new, New (empty), Paste code): every Undo entry stays good.
        private static void UndoStaysGood() => undoVersion = Store.Version;

        // What a drawer item asks for when it goes into a loadout: its kind, quality and style, and how many by "A
        // stackable dragged in comes as".
        private static Putting PuttingOf(DragItem item)
        {
            ItemDrop.ItemData template = Codes.Template(item.Prefab);
            int count = 1;
            if (LoadoutEdit.Stackable(template))
            {
                int stack = template.m_shared.m_maxStackSize;
                switch (Settings.StackDragged.Value)
                {
                    case DraggedStack.One:
                        count = 1;
                        break;
                    case DraggedStack.WhatTheChestsHold:
                        count = Mathf.Clamp(item.Count, 1, stack);
                        break;
                    default:
                        count = stack;
                        break;
                }
            }
            return new Putting { Prefab = item.Prefab, Quality = item.Quality, Variant = item.Variant, Count = count, Template = template };
        }

        private static bool WearDragged => Settings.GearDragged.Value == DraggedGear.WhenNothingElseIsWornThere;

        // A drag let go on a cell: a drawer item goes in there; a cell's item moves there (an item there swaps).
        private static void DroppedOnCell(DragItem item, Vector2i cell)
        {
            string why = null;
            if (!Open || Popup.Showing || !CanEdit(out why))
            {
                status.text = why ?? status.text;
                return;
            }
            Loadout loadout = Selected;
            if (item.FromCell.HasValue)
            {
                Vector2i from = item.FromCell.Value;
                Loadout moved = LoadoutEdit.Move(loadout, shape, from.x, from.y, cell.x, cell.y, Codes.Template, out why);
                Apply(loadout, moved, "Moved.", why);
                if (moved != null)
                {
                    selectedCell = cell;
                    FoodPanel.Moved(moved, shape, from, cell);
                }
                return;
            }
            if (item.Exact != null)
            {
                // From his inventory beside the window: that very item, worn as he wears it, or as gear dragged in goes on.
                ItemDrop.ItemData template = Codes.Template(item.Exact.Prefab);
                bool wear = item.Exact.Worn != WornAs.None || (WearDragged && template != null && template.IsEquipable());
                Loadout exact = CellClipboard.PutExact(loadout, shape, cell.x, cell.y, item.Exact, Codes.Template, wear, out why);
                Apply(loadout, exact, $"Put your {Messages.NameOf(item.Exact)} in {loadout.Name}.", why);
                if (exact != null)
                {
                    selectedCell = cell;
                }
                return;
            }
            Putting putting = PuttingOf(item);
            Loadout put = LoadoutEdit.Put(loadout, shape, cell.x, cell.y, putting, WearDragged, out why);
            Apply(loadout, put, $"Put {Messages.NameOf(new LoadoutItem { Prefab = putting.Prefab, Quality = putting.Quality })} in {loadout.Name}.", why);
            if (put != null)
            {
                selectedCell = cell;
            }
        }

        // A drag let go anywhere but a cell: a cell's item dragged out of the grid leaves the loadout. Not while a question
        // shows: a drop on its shade isn't "out of the grid" (review round 2 of Session 2, n1).
        private static void DroppedElsewhere(DragItem item)
        {
            if (!Open || Popup.Showing || !item.FromCell.HasValue || !CanEdit(out string why))
            {
                return;
            }
            TakeOut(item.FromCell.Value);
        }

        // A drawer item clicked: by "Left click on a drawer item", into the first free cell (gear into a free gear slot of
        // EquipmentAndQuickSlots' when it would be worn).
        private static void DrawerClicked(DragItem item, bool right)
        {
            if (!Open || right || Settings.DrawerClicks.Value != DrawerClick.FirstFreeCell)
            {
                return;
            }
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            Putting putting = PuttingOf(item);
            if (!LoadoutEdit.FirstFree(loadout, shape, putting.Template, WearDragged, out int x, out int y))
            {
                status.text = $"{loadout.Name} has no free cell left: drag it onto a cell to put it there instead.";
                return;
            }
            Loadout put = LoadoutEdit.Put(loadout, shape, x, y, putting, WearDragged, out why);
            Apply(loadout, put, $"Put {Messages.NameOf(new LoadoutItem { Prefab = putting.Prefab, Quality = putting.Quality })} in {loadout.Name}.", why);
            if (put != null)
            {
                selectedCell = new Vector2i(x, y);
            }
        }

        // What dragging a cell drags: its item, while a loadout is being edited.
        private static DragItem DragFrom(Vector2i cell)
        {
            if (!Open || !CanEdit(out string _))
            {
                return null;
            }
            LoadoutItem item = LoadoutEdit.At(Selected, shape, cell.x, cell.y);
            if (item == null)
            {
                return null;
            }
            ItemDrop.ItemData template = Codes.Template(item.Prefab);
            return new DragItem
            {
                Prefab = item.Prefab,
                Quality = item.Quality,
                Variant = item.Variant,
                Count = item.Count,
                FromCell = cell,
                Icon = item.Food != null ? FoodIcon(item) : IconOf(template, item.Variant),
            };
        }

        private static void TakeOut(Vector2i cell)
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            LoadoutItem item = LoadoutEdit.At(loadout, shape, cell.x, cell.y);
            Loadout changed = LoadoutEdit.Remove(loadout, shape, cell.x, cell.y);
            Apply(loadout, changed, item != null ? $"Removed {Messages.NameOf(item)} from {loadout.Name}." : string.Empty, null);
            if (changed != null && selectedCell.HasValue && selectedCell.Value.x == cell.x && selectedCell.Value.y == cell.y)
            {
                selectedCell = null;
            }
        }

        // A stackable's count, by the wheel (Shift: ten a notch), the − and + buttons, or the box.
        private static void Count(Vector2i cell, int to)
        {
            if (!CanEdit(out string why))
            {
                status.text = why;
                return;
            }
            Loadout loadout = Selected;
            LoadoutItem item = LoadoutEdit.At(loadout, shape, cell.x, cell.y);
            if (item == null)
            {
                return;
            }
            Loadout changed = LoadoutEdit.SetCount(loadout, shape, cell.x, cell.y, to, Codes.Template(item.Prefab));
            if (changed != null)
            {
                Apply(loadout, changed, $"{Messages.NameOf(item)}: {LoadoutEdit.At(changed, shape, cell.x, cell.y)?.Count}.", null,
                      fold: $"count {loadout.Id} {cell.x},{cell.y}");
            }
        }

        private static void Scrolled(Vector2i cell, int notches)
        {
            LoadoutItem item = Open ? LoadoutEdit.At(Selected, shape, cell.x, cell.y) : null;
            if (item != null)
            {
                Count(cell, item.Count + notches * (Shift() ? 10 : 1));
            }
        }

        private static void Step(int by)
        {
            LoadoutItem item = selectedCell.HasValue ? LoadoutEdit.At(Selected, shape, selectedCell.Value.x, selectedCell.Value.y) : null;
            if (item != null)
            {
                Count(selectedCell.Value, item.Count + by * (Shift() ? 10 : 1));
            }
        }

        private static void Typed(string text)
        {
            if (selectedCell.HasValue && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int to))
            {
                Count(selectedCell.Value, to);
            }
            nextLook = 0f;
        }

        // Worn or carried, for the cell being edited.
        private static void ToggleWorn()
        {
            string why = null;
            if (!selectedCell.HasValue || !CanEdit(out why))
            {
                status.text = why ?? status.text;
                return;
            }
            Vector2i cell = selectedCell.Value;
            Loadout loadout = Selected;
            LoadoutItem item = LoadoutEdit.At(loadout, shape, cell.x, cell.y);
            if (item == null)
            {
                return;
            }
            bool wear = item.Worn == WornAs.None;
            Loadout changed = LoadoutEdit.SetWorn(loadout, shape, cell.x, cell.y, wear, Codes.Template(item.Prefab), out why);
            string stand = !Stands.Stands.Active || Stands.StandPrototype.FitForPrefab(item.Prefab) < 0 ? string.Empty
                : item.Displayed ? ", and still shown on a loadout stand" : ", not shown on a loadout stand (Displayed: off)";
            Apply(loadout, changed, $"{Messages.NameOf(item)} is {(wear ? "worn" : "carried")} now{stand}.", why);
        }

        private static void Undo()
        {
            if (undo.Count == 0 || !Store.Writable)
            {
                return;
            }
            Loadout before = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            lastFold = null;
            string slotsWere = SlotPlaces(Store.Find(before.Id));
            if (Store.Save(before))
            {
                undoVersion = Store.Version;
                selectedId = before.Id;
                status.text = $"Undone: {before.Name} as it was.";
                if (FoodPanel.EditsLoadout(before.Id) && SlotPlaces(before) != slotsWere)
                {
                    FoodPanel.Done?.Invoke();   // a slot moved: its cell may hold another now (review round 2, n7; the last look's n3)
                }
            }
            else
            {
                status.text = Store.WhyNotSaved;
            }
            nextLook = 0f;
        }

        // An empty loadout to fill from the drawer.
        private static void NewEmpty()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            if (!Store.Writable)
            {
                status.text = Store.WhyNotWritable;
                return;
            }
            if (Store.Full)
            {
                status.text = Store.FullWords;
                return;
            }
            Popup.Name("Name the new, empty loadout", "Loadout " + (Store.All.Count + 1), name =>
            {
                Loadout made = LoadoutEdit.Empty(Store.NewId(), name, player.GetPlayerID(), player.GetPlayerName());
                if (Store.Save(made))
                {
                    UndoStaysGood();
                    selectedId = made.Id;
                    selectedCell = null;
                    status.text = $"Made {made.Name}: drag items into it from the panel beside the window.";
                }
                else
                {
                    status.text = Store.WhyNotSaved;
                }
            });
        }

        // Shift held (the wheel and − + by ten), read where an error can't reach the game.
        private static bool Shift()
        {
            try
            {
                return ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void SaveNew()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            if (!Store.Writable)
            {
                status.text = Store.WhyNotWritable;
                return;
            }
            if (Store.Full)
            {
                status.text = Store.FullWords;
                return;
            }
            Loadout from = Selected;
            if (from != null && loadoutHighlights.Count > 0 && shape != null)
            {
                SaveNewFrom(player, from);
                return;
            }
            List<Vector2i> picked = Picks.All();
            bool onlyPicked = Settings.SaveOnlyPicked.Value;
            if (onlyPicked && picked.Count == 0)
            {
                status.text = "Highlight the cells to save first: Highlight cells, then click cells of the loadout, or of your inventory (Show my inventory; or hold Right Alt and click them in the game's own).";
                return;
            }
            Popup.Name("Name the new loadout", "Loadout " + (Store.All.Count + 1), name =>
            {
                Loadout made = Capture.Cells(player, name, onlyPicked ? picked : null);
                if (made.Items.Count == 0)
                {
                    status.text = "There's nothing in the picked cells to save.";
                    return;
                }
                if (Store.Save(made))
                {
                    UndoStaysGood();
                    selectedId = made.Id;
                    Picks.Clear();
                    status.text = $"Saved {made.Name}: {made.Items.Count} item{(made.Items.Count == 1 ? "" : "s")}.";
                }
                else
                {
                    status.text = Store.WhyNotSaved;
                }
            });
        }

        // Save new with cells of the loadout picked highlighted: those cells' items (as they are, food slots too, each in its
        // cell) as a new loadout.
        private static void SaveNewFrom(Player player, Loadout from)
        {
            List<LoadoutItem> items = CellClipboard.FromLoadout(from, shape, loadoutHighlights);
            if (items.Count == 0)
            {
                status.text = "The highlighted cells are empty: nothing to save.";
                return;
            }
            var cells = new HashSet<Vector2i>(loadoutHighlights);
            Popup.Name($"Name the new loadout ({items.Count} item{(items.Count == 1 ? "" : "s")} of {from.Name})", "Loadout " + (Store.All.Count + 1), name =>
            {
                Loadout made = LoadoutEdit.Empty(Store.NewId(), name, player.GetPlayerID(), player.GetPlayerName());
                made.FavouritesGo = from.FavouritesGo;
                made.Items.AddRange(CellClipboard.FromLoadout(Store.Find(from.Id) ?? from, shape, cells));
                if (made.Items.Count == 0)
                {
                    status.text = "The highlighted cells are empty now: nothing saved.";
                    return;
                }
                if (Store.Save(made))
                {
                    UndoStaysGood();
                    selectedId = made.Id;
                    selectedCell = null;
                    status.text = $"Saved {made.Name}: {made.Items.Count} item{(made.Items.Count == 1 ? "" : "s")} of {from.Name}.";
                }
                else
                {
                    status.text = Store.WhyNotSaved;
                }
            });
        }

        private static void SaveOver()
        {
            Player player = Player.m_localPlayer;
            Loadout loadout = Selected;
            if (player == null || loadout == null)
            {
                return;
            }
            List<Vector2i> picked = Picks.All();
            bool onlyPicked = Settings.SaveOnlyPicked.Value;
            if (onlyPicked && picked.Count == 0)
            {
                status.text = "Highlight the cells to save first: Show my inventory, Highlight cells and click them (or hold Right Alt and click them in the game's own inventory).";
                return;
            }
            string what = onlyPicked ? "the picked cells" : "everything you carry and wear";
            int slots = loadout.FoodSlots;
            string keeps = slots > 0 ? $" Its {slots} food slot{(slots == 1 ? "" : "s")} stay{(slots == 1 ? "s" : "")}, with {(slots == 1 ? "its rule" : "their rules")}." : string.Empty;
            Popup.Ask($"Save {what} over {loadout.Name}? Its items become what's there now.{keeps}", "Save over", "Cancel", yes =>
            {
                if (!yes)
                {
                    return;
                }
                Loadout made = Capture.Cells(player, loadout.Name, onlyPicked ? picked : null);
                if (made.Items.Count == 0)
                {
                    status.text = "There's nothing there to save.";
                    return;
                }
                made.Id = loadout.Id;
                // Its food slots stay, with their rules (review round 1 of job 30's Session 3, M1): the loadout as it is now.
                Loadout before = Store.Find(loadout.Id) ?? loadout;
                made = LoadoutEdit.KeepFoodSlots(before, made, shape, out string tooMany, out string slotsSaid, Settings.FoodStatAtLeast.Value);
                if (made == null)
                {
                    status.text = tooMany;
                    return;
                }
                made = LoadoutEdit.KeepOwnSettings(before, made);   // its favourites switch, what a stand shows (0.5.0)
                if (Store.Save(made))
                {
                    ForgetUndo(made.Id);
                    Picks.Clear();
                    status.text = $"Saved over {made.Name}: {made.Items.Count} item{(made.Items.Count == 1 ? "" : "s")}{(slotsSaid != null ? "; " + slotsSaid : string.Empty)}.";
                }
                else
                {
                    status.text = Store.WhyNotSaved;
                }
            });
        }

        private static void Rename()
        {
            Loadout loadout = Selected;
            if (loadout == null)
            {
                return;
            }
            Popup.Name("Rename the loadout", loadout.Name, name =>
            {
                Loadout renamed = loadout.Clone();
                renamed.Name = name;
                if (Store.Save(renamed))
                {
                    ForgetUndo(renamed.Id);
                    status.text = $"Renamed to {renamed.Name}.";
                }
                else
                {
                    status.text = Store.WhyNotSaved;
                }
            });
        }

        private static void Delete()
        {
            Loadout loadout = Selected;
            if (loadout == null)
            {
                return;
            }
            Popup.Ask($"Delete {loadout.Name}? Only the loadout goes: no item is touched.", "Delete", "Cancel", yes =>
            {
                if (!yes)
                {
                    return;
                }
                if (Store.Delete(loadout.Id))
                {
                    ForgetUndo(loadout.Id);
                    selectedId = null;
                    status.text = $"Deleted {loadout.Name}.";
                }
                else
                {
                    status.text = Store.WhyNotWritable;
                }
            });
        }

        private static void CopyCode()
        {
            Loadout loadout = Selected;
            if (loadout == null)
            {
                return;
            }
            string code = Codes.Write(loadout);
            if (code.Length > ShareCode.MaxCode)
            {
                // Paste would refuse it (R2, finding 9): long prefab names (other mods' items) on many items.
                status.text = $"{loadout.Name}'s code would be {code.Length} characters, more than a code may be ({ShareCode.MaxCode}): " +
                              "copy fewer of its items into a new loadout (Copy cells, Paste cells) and copy that one's code.";
                return;
            }
            GUIUtility.systemCopyBuffer = code;
            status.text = $"Copied {loadout.Name}'s code to the clipboard ({code.Length} characters): paste it to a friend.";
        }

        private static void PasteCode()
        {
            if (!Store.Writable)
            {
                status.text = Store.WhyNotWritable;
                return;
            }
            if (Store.Full)
            {
                status.text = Store.FullWords;
                return;
            }
            Loadout read = Codes.Read(GUIUtility.systemCopyBuffer, out string why);
            if (read == null)
            {
                status.text = $"Couldn't read the clipboard as a loadout: {why}.";
                return;
            }
            int unknown = read.Items.Count(item => item.Food == null && Codes.Template(item.Prefab) == null);
            if (Store.Save(read))
            {
                UndoStaysGood();
                selectedId = read.Id;
                status.text = $"Added {read.Name} from the code: {read.Items.Count} item{(read.Items.Count == 1 ? "" : "s")}" +
                              (unknown > 0 ? $", {unknown} not in your game." : ".");
            }
            else
            {
                status.text = Store.WhyNotSaved;
            }
        }

        private static void Equip()
        {
            Loadout loadout = Selected;
            if (loadout == null || Outfit.Busy || Stands.StandJob.Busy)
            {
                return;   // a stand's job under way: its pieces mustn't go into the chests meanwhile (job 30, review round 1, m2)
            }
            if (loadout.Items.Count == 0)
            {
                status.text = $"{loadout.Name} is empty: drag items into it from the panel beside the window first.";
                return;
            }
            // On one of his stands nearby: what Left Alt + E does there, without walking there (A46 decision 47).
            // In the mode chosen above; with the modes hidden, the stand's own "Equip mode at a stand" (review round 1, j1).
            if (Stands.StandJob.EquipFromWindow(loadout, Settings.AskMode.Value ? mode : Stands.StandJob.Mode(), said =>
                {
                    if (status != null)
                    {
                        status.text = said;
                    }
                    nextLook = 0f;
                }))
            {
                nextLook = 0f;
                return;
            }
            bool begun = Outfit.Begin(EquipFor(loadout), result =>
            {
                if (status != null)
                {
                    status.text = result.Line;
                }
                nextLook = 0f;
            });
            if (begun && Outfit.Busy)
            {
                status.text = $"Putting {loadout.Name} on...";
            }
            nextLook = 0f;   // the buttons grey at once while it runs (review round 1, m6)
        }

        private static void ChooseMode(EquipMode chosen)
        {
            mode = chosen;
            modeChosen = true;
            nextLook = 0f;
        }

        // ---- Building the window, once ----

        // The cell tools' row, the modes' heading and buttons, the weight, Equip and the status line, from the top (job 30,
        // Session 3: the window grew by 60 for the cell tools and the heading).
        private const float ToolsY = 548f;
        private const float ModeHeadingY = 588f;
        private const float ModesY = 610f;
        private const float WeightY = 650f;
        private const float EquipY = 680f;
        private const float StatusY = 726f;

        private static void Build()
        {
            GUIManager gui = GUIManager.Instance;
            window = gui.CreateWoodpanel(Ui.CanvasRoot(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, Height, true);
            window.name = "Outfitter_Window";
            Transform root = window.transform;
            rows.Clear();
            anywhere.Clear();

            Ui.Label(root, "Outfitter", 20f, 12f, 250f, 34f, 26, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Ui.Label(root, "Loadouts", 20f, 56f, 250f, 26f, 18, TextAnchor.MiddleLeft, Ui.Orange, bold: true);

            // The stand bar (job 30, Session 2), shown while the window is open on a stand.
            standBar = Ui.Box("Outfitter_StandBar", root, RightX, 6f, 590f, 46f, new Color(0f, 0f, 0f, 0.25f));
            standLabel = Ui.Label(standBar.transform, string.Empty, 8f, 0f, 170f, 46f, 13, TextAnchor.MiddleLeft);
            renameStandButton = Ui.Button(standBar.transform, "Rename stand", 180f, 7f, 104f, 32f, () => Module.Guard("renaming a stand", RenameStand));
            Ui.Help(renameStandButton, "Gives this stand a name of its own, shown where the game shows \"Armor stand\". Leave the box empty for the game's name again.");
            unassignButton = Ui.Button(standBar.transform, "Unassign", 288f, 7f, 84f, 32f, () => Module.Guard("unassigning a stand", () =>
            {
                Stands.StandEdits.Unassign(standFocus);
                nextLook = 0f;
            }));
            Ui.Help(unassignButton, "Makes this an ordinary stand again: its gear stays on it, and the loadout stays in your list.");
            personalButton = Ui.Button(standBar.transform, "Personal", 376f, 7f, 90f, 32f, () => Module.Guard("a personal stand", () =>
            {
                Stands.StandEdits.TogglePersonal(standFocus);
                nextLook = 0f;
            }));
            Ui.Help(personalButton, () =>
            {
                Stands.IStand stand = standFocus;
                if (stand == null || !stand.Valid)
                {
                    return null;
                }
                string why = Stands.StandPersonal.WhyNotToggle(stand);
                return why != null ? why
                    : Stands.StandPersonal.Mine(stand)
                        ? "Your personal stand: other players' Outfitter keys and buttons leave it alone. Press to make it an ordinary stand again."
                        : "Makes this your personal stand: other players' Outfitter keys and buttons (save, put on, hang back, rename, assign) leave it alone. The game's own E is the ward's to allow, as for any stand.";
            });
            assignButton = Ui.Button(standBar.transform, "Assign to stand", 470f, 7f, 116f, 32f, () => Module.Guard("assigning a loadout to a stand", AssignHere));
            Ui.Help(assignButton, () => $"Puts the loadout picked on the left on this stand: hang it back there with {Stands.StandInput.Text(Stands.StandSettings.EquipKey.Value)}.");
            standBar.SetActive(false);

            // The list, and what acts on the loadout picked in it.
            list = Ui.Scroll("Outfitter_List", root, 20f, 86f, 250f, 360f);
            saveNewButton = Ui.Button(root, "Save new", 20f, 456f, 122f, 34f, () => Module.Guard("saving a loadout", SaveNew));
            Ui.Help(saveNewButton, () => loadoutHighlights.Count > 0 && Selected != null
                ? $"Saves the {loadoutHighlights.Count} highlighted cell{(loadoutHighlights.Count == 1 ? "" : "s")} of {Selected.Name} as a new loadout, each item in its cell."
                : Settings.SaveOnlyPicked.Value
                    ? "Saves the highlighted cells as a new loadout: Highlight cells, then click cells of the loadout picked, or of your inventory (Show my inventory; or Right Alt in the game's own)."
                    : "Saves everything you carry and wear as a new loadout (or, with cells of the loadout highlighted, those cells).");
            saveOverButton = Ui.Button(root, "Save over", 148f, 456f, 122f, 34f, () => Module.Guard("saving over a loadout", SaveOver));
            Ui.Help(saveOverButton, () => Settings.SaveOnlyPicked.Value
                ? "Saves the highlighted cells of your inventory over the loadout picked: its items become what's there now."
                : "Saves everything you carry and wear over the loadout picked: its items become what's there now.");
            renameButton = Ui.Button(root, "Rename", 20f, 496f, 122f, 34f, () => Module.Guard("renaming a loadout", Rename));
            Ui.Help(renameButton, "Renames the loadout picked on the left.");
            deleteButton = Ui.Button(root, "Delete", 148f, 496f, 122f, 34f, () => Module.Guard("deleting a loadout", Delete));
            Ui.Help(deleteButton, "Deletes the loadout picked on the left, after asking. Only the loadout goes: no item is touched.");
            newButton = Ui.Button(root, "New (empty)", 20f, 536f, 122f, 34f, () => Module.Guard("making an empty loadout", NewEmpty));
            Ui.Help(newButton, "Makes an empty loadout, to fill by dragging items in from the panel beside the window.");
            undoButton = Ui.Button(root, "Undo", 148f, 536f, 122f, 34f, () => Module.Guard("undoing an edit", Undo));
            Ui.Help(undoButton, "Takes back your last edit to a loadout (up to 30, while the window stays open). A rename, a save over or a delete can't be taken back.");
            copyButton = Ui.Button(root, "Copy code", 20f, 576f, 122f, 34f, () => Module.Guard("copying a loadout's code", CopyCode));
            Ui.Help(copyButton, "Copies the loadout picked as a share code to the clipboard, to paste to a friend.");
            pasteButton = Ui.Button(root, "Paste code", 148f, 576f, 122f, 34f, () => Module.Guard("reading a loadout's code", PasteCode));
            Ui.Help(pasteButton, "Adds a loadout from a share code on the clipboard. A code is only a list: it never makes an item.");
            moveStandButton = Ui.Button(root, "Move to stand…", 20f, MoveStandY, 122f, 34f, () => Module.Guard("choosing a stand", MoveToStand));
            Ui.Help(moveStandButton, "Puts the loadout picked on an armour stand near you, moves it to another, or takes it off its stand: the stands within reach, in a list.");
            standSlotsButton = Ui.Button(root, "Stand slots", 148f, MoveStandY, 122f, 34f, () => Module.Guard("showing the stand slots", () => ToggleBeside(Beside.StandSlots)));
            Ui.Help(standSlotsButton, () => StandPanel.Shown
                ? "Hides the stand's slots."
                : "Shows beside the window each place of a loadout stand and what of the loadout picked it shows there (each cell's Displayed, beside the grid), and what doesn't fit.");

            // The loadout picked, and the panels beside the window.
            header = Ui.Label(root, string.Empty, RightX, 56f, 318f, 30f, 18, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            inventoryButton = Ui.Button(root, "Show my inventory", Width - 284f, 54f, 130f, 32f, () => Module.Guard("showing your inventory", () => ToggleBeside(Beside.MyInventory)));
            Ui.Help(inventoryButton, () => InventoryPanel.Shown
                ? "Hides your inventory beside the window."
                : "Shows your own inventory beside the window: drag a cell into the loadout (that very item goes in), or highlight cells to copy them or save them.");
            drawerButton = Ui.Button(root, "Show nearby items", Width - 150f, 54f, 130f, 32f, () => Module.Guard("showing the nearby items", () => ToggleBeside(Beside.NearbyItems)));
            Ui.Help(drawerButton, () => Drawer.Shown
                ? "Hides the nearby items beside the window."
                : "Shows every item in the chests near you beside the window, to drag into the loadout and build it from what your base holds.");
            Drawer.Build(root, Width, 0f);   // to the window's right, hidden until opened
            InventoryPanel.Build(root, Width, 0f, Height);   // in the same place, one or the other
            FoodPanel.Build(root, Width, 0f, Height);   // and a food slot's rule, there too
            StandPanel.Build(root, Width, 0f, Height);   // and the stand's slots
            StandPanel.Hidden = () => SetBeside(Beside.None);
            FoodPanel.Changed = FoodRuleChanged;
            FoodPanel.Done = () => SetBeside(beforeFood);
            InventoryPanel.Clicked = InventoryCellClicked;
            InventoryPanel.Highlighting = InventoryHighlights;
            grid = new GridView(root, RightX, GridTop);
            grid.Clicked = CellClicked;
            grid.RightClicked = cell =>
            {
                if (!highlighting && LoadoutEdit.At(Selected, shape, cell.x, cell.y) != null)
                {
                    TakeOut(cell);
                }
            };
            grid.DragFrom = DragFrom;
            grid.Scrolled = Scrolled;
            grid.Hovered = cell =>
            {
                hovered = cell;
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    ShowInfo(player, Selected);
                }
            };
            Drag.DroppedOnCell = DroppedOnCell;
            Drag.DroppedElsewhere = DroppedElsewhere;
            Drag.Clicked = DrawerClicked;

            // The info column: what's under the pointer or being edited, its edit controls, what has no cell.
            info = Ui.Label(root, string.Empty, InfoX, GridTop, 150f, 150f, 14, TextAnchor.UpperLeft, Ui.Plain);
            lessButton = Ui.Button(root, "−", InfoX, GridTop + 156f, 36f, 30f, () => Module.Guard("changing a count", () => Step(-1)));
            Ui.Help(lessButton, "One fewer (with Shift: ten). The wheel over its cell does the same.");
            GameObject box = GUIManager.Instance.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                  InputField.ContentType.IntegerNumber, "1", 14, 70f, 30f);
            Ui.Place(box, InfoX + 40f, GridTop + 156f, 70f, 30f);
            countField = box.GetComponent<InputField>();
            countField.characterLimit = 4;
            countField.lineType = InputField.LineType.SingleLine;
            countField.onEndEdit.AddListener(text => Module.Guard("typing a count", () => Typed(text)));
            moreButton = Ui.Button(root, "+", InfoX + 114f, GridTop + 156f, 36f, 30f, () => Module.Guard("changing a count", () => Step(1)));
            Ui.Help(moreButton, "One more (with Shift: ten), up to a stack (a food slot: up to 100). The wheel over its cell does the same.");
            wornButton = Ui.Button(root, "Worn: off", InfoX, GridTop + 192f, 150f, 30f, () => Module.Guard("wearing or carrying an item", ToggleWorn));
            Ui.Help(wornButton, () => selectedCell.HasValue && shape != null && LoadoutEdit.SlotWears(shape, selectedCell.Value.x, selectedCell.Value.y) != WornAs.None
                ? "An item in a gear slot is always worn."
                : "Worn: putting the loadout on puts it on you (lit: on); off, it's carried in its cell. Wearing one piece takes \"worn\" off another in its place, by the game's own rules for hands. Whether a stand shows it is Displayed's.");
            shownButton = Ui.Button(root, "Displayed: off", InfoX, GridTop + 228f, 150f, 30f, () => Module.Guard("showing an item on a stand", ToggleShown));
            Ui.Help(shownButton, "Displayed: a loadout stand holding this loadout shows it (lit: on), worn or not: a build hammer on the stand that never goes into your hands when you put it on. One item a place: Stand slots (under the list) shows which.");
            takeOutButton = Ui.Button(root, "Remove", InfoX, GridTop + 264f, 150f, 30f, () => Module.Guard("removing an item", () =>
            {
                if (selectedCell.HasValue)
                {
                    TakeOut(selectedCell.Value);
                }
            }));
            Text removeLabel = takeOutButton.GetComponentInChildren<Text>();
            if (removeLabel != null)
            {
                removeLabel.color = Ui.Bad;   // red (his words, 2026-10-07: "a red 'X' or something, or at least rename it to 'Remove'")
            }
            Ui.Help(takeOutButton, "Removes the item from the loadout, never from your inventory or a chest (a right click on its cell, or dragging it out of the grid, does the same).");
            foodButton = Ui.Button(root, "Add a food slot", InfoX, GridTop + 192f, 150f, 30f, () => Module.Guard("a food slot", FoodClicked));
            Ui.Help(foodButton, () => foodButton.GetComponentInChildren<Text>()?.text == "Edit food slot"
                ? "Opens this food slot's rule beside the window: by its stats or from a list of foods, and which it prefers."
                : foodButton.GetComponentInChildren<Text>()?.text == "Make a food slot"
                    ? foodWhyNot ?? "Makes this food a food slot: instead of this one food, it takes one of the foods you tick (this one ticked to start)."
                    : "Adds a food slot in the first free cell: a cell that takes, at each equip, a food nearby that meets its rule (its stats, or a list you tick).");
            anywhereTitle = Ui.Label(root, string.Empty, InfoX, GridTop + 302f, 150f, 20f, 14, TextAnchor.MiddleLeft, Ui.Quiet);
            for (int i = 0; i < 6; i++)
            {
                anywhere.Add(Ui.Icon(root, InfoX + (i % 3) * 46f, GridTop + 326f + (i / 3) * 46f, 42f));
            }

            // The cell tools (job 30, Session 3): his words, "highlight cells", "Copy Cells", "Paste Cells".
            highlightButton = Ui.Button(root, "Highlight cells", RightX, ToolsY, 142f, 32f, () => Module.Guard("highlighting cells", ToggleHighlighting));
            // Where a click highlights: under "Picking the cells to save: HeldKeyAndClicks" his inventory's cells only by
            // the key (review round 1, n8).
            Ui.Help(highlightButton, () =>
            {
                string where = Settings.Picking.Value == PickingCells.HeldKeyAndClicks
                    ? "in the loadout (your inventory's cells only with the key, by Picking the cells to save)"
                    : "in the loadout or in your inventory beside the window";
                return highlighting
                    ? $"On: a click on a cell highlights it, or takes its highlight off, {where}. Press again to edit cells by clicking."
                    : $"Turns highlighting on: then a click on a cell highlights it, {where}, to copy the highlighted cells or save them.";
            });
            copyCellsButton = Ui.Button(root, "Copy cells", RightX + 149f, ToolsY, 142f, 32f, () => Module.Guard("copying cells", CopyCells));
            Ui.Help(copyCellsButton, () => loadoutHighlights.Count + Picks.Count == 0
                ? "Copies the highlighted cells' items: highlight some first (Highlight cells)."
                : $"Copies the items in the highlighted cells ({(loadoutHighlights.Count > 0 ? "the loadout's" : "your inventory's")}) to Outfitter's own clipboard, to paste into a loadout.");
            pasteCellsButton = Ui.Button(root, "Paste cells", RightX + 298f, ToolsY, 142f, 32f, () => Module.Guard("pasting cells", PasteCells));
            Ui.Help(pasteCellsButton, () => CellClipboard.Count == 0
                ? "Puts copied cells into the loadout picked: copy some first (Copy cells)."
                : $"Puts the {CellClipboard.Count} item{(CellClipboard.Count == 1 ? "" : "s")} copied from {CellClipboard.From} into the loadout picked: each into the cell it came from when that's free, else the first free cell. All or nothing.");
            clearHighlightsButton = Ui.Button(root, "Clear highlights", RightX + 447f, ToolsY, 142f, 32f, () => Module.Guard("clearing the highlights", ClearHighlights));
            Ui.Help(clearHighlightsButton, "Takes every highlight off, in the loadout and in your inventory.");

            // The modes, under a heading of their own (his words: "under a small header to make it clear they are options
            // of one setting"); the chosen one lit, the others faded until pointed at.
            modeHeading = Ui.Label(root, "Equip mode", RightX, ModeHeadingY, 300f, 20f, 14, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            string[] modeNames = { "Fully replace", "Swap gear only", "Swap gear + the rest" };
            string[] modeHelp =
            {
                "Your inventory becomes the loadout: everything you carry and wear that it doesn't hold goes into the chests nearby (quest items, and your favourites by your settings, stay), and its items come from the chests into their cells.",
                "Only your gear changes: what you wear goes into the chests, and the loadout's gear comes on. Your food, materials and everything else stay as they are; the loadout's other items aren't fetched.",
                "Your gear changes as in Swap gear only, and the loadout's other items (food, arrows, materials) come from the chests too, topped up to its counts. Nothing else of yours goes away.",
            };
            modeButtons = new Button[modeNames.Length];
            for (int i = 0; i < modeNames.Length; i++)
            {
                var chosen = (EquipMode)i;
                modeButtons[i] = Ui.Button(root, modeNames[i], RightX + i * 148f, ModesY, 144f, 34f, () => Module.Guard("choosing the mode", () => ChooseMode(chosen)));
                Ui.Help(modeButtons[i], modeHelp[i]);
                Text modeLabel = modeButtons[i].GetComponentInChildren<Text>();
                if (modeLabel != null)
                {
                    modeLabel.resizeTextForBestFit = true;
                    modeLabel.resizeTextMinSize = 11;
                    modeLabel.resizeTextMaxSize = modeLabel.fontSize;
                }
            }
            // The loadout's own favourites switch (his words, 2026-10-07: "a radio toggle button or checkbox for each loadout
            // beside the equip mode options", the setting its default).
            favouritesButton = Ui.Button(root, "Favourites go too", RightX + 446f, ModesY, 144f, 34f, () => Module.Guard("the favourites switch", ToggleFavourites));
            Text favouritesLabel = favouritesButton.GetComponentInChildren<Text>();
            if (favouritesLabel != null)
            {
                favouritesLabel.resizeTextForBestFit = true;
                favouritesLabel.resizeTextMinSize = 11;
                favouritesLabel.resizeTextMaxSize = favouritesLabel.fontSize;
            }
            Ui.Help(favouritesButton, () =>
            {
                Loadout picked = Selected;
                bool go = FavouritesGo(picked);
                string own = picked?.FavouritesGo == null ? " (as \"Equip modes put away my favourites\" says, until you press it)" : " (this loadout's own)";
                return go
                    ? $"On{own}: putting it on puts your favourites away like any other item, in every mode: Fully replace stores every favourite it doesn't hold, the swap modes your favourite gear. Press for off."
                    : $"Off{own}: your favourites follow Leave my favourites alone and My favourite gear in an equip (F1, Outfitter). Press for on.";
            });
            weight = Ui.Label(root, string.Empty, RightX, WeightY, 590f, 26f, 14, TextAnchor.MiddleLeft);
            equipButton = Ui.Button(root, "Equip", RightX + 210f, EquipY, 200f, 38f, () => Module.Guard("putting a loadout on", Equip));
            Text equipLabel = equipButton.GetComponentInChildren<Text>();
            if (equipLabel != null)
            {
                equipLabel.resizeTextForBestFit = true;
                equipLabel.resizeTextMinSize = 11;
                equipLabel.resizeTextMaxSize = equipLabel.fontSize;
            }
            Ui.Help(equipButton, () =>
            {
                Stands.IStand through = equipStand;
                bool asked = Settings.AskMode.Value;
                return through == null
                    ? $"Puts the loadout picked on, {(asked ? "in the mode chosen above" : $"in Outfitter's Equip mode ({Settings.Mode.Value})")}: its items from the chests nearby, what it replaces into them."
                    : $"Puts the loadout picked on as {Stands.StandInput.Text(Stands.StandSettings.EquipKey.Value)} at its stand does " +
                      $"({StandCalled(through, Selected)}): the stand's gear comes to you, the rest from the chests " +
                      $"around the stand, and what you wore goes home onto its own stands. {(asked ? "In the mode chosen above." : $"In the stands' Equip mode at a stand ({Stands.StandJob.Mode()}).")}";
            });
            Button close = Ui.Button(root, "Close", RightX + 450f, EquipY, 140f, 38f, () => Module.Guard("closing", CloseSoon));
            Ui.Help(close, "Closes the window (L or Escape do too).");
            status = Ui.Label(root, string.Empty, 20f, StatusY, Width - 40f, 26f, 14, TextAnchor.MiddleLeft, Ui.Quiet);
            window.SetActive(false);
        }
    }
}

