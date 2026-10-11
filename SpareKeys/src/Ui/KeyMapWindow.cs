using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace SpareKeys
{
    /// <summary>
    /// The key map: a window in the game's look with the keyboard picture (and the mouse) on top, every key coloured by
    /// who uses it with the modifiers shown; a row None · Alt · Ctrl · Shift that follows a held modifier; a line saying
    /// who uses the key under the pointer; and the list, browsed the pack's way: a list on the left picks what shows,
    /// headings that follow the pick, keyboard order, a search box. A click on a key keeps only its rows. Opened by its key
    /// or the button in F1; Escape or the key closes it. It holds the game's input while open, and gives it back.
    /// </summary>
    internal static class KeyMapWindow
    {
        private const float Width = 1240f;
        private const float FullHeight = 790f;
        private const float Left = 20f;
        private const float Top = 60f;
        private const float RailWidth = 232f;
        private const float RowHeight = 26f;
        private const float RailRowHeight = 28f;

        private static GameObject window;
        private static Canvas canvas;
        private static Image background;
        private static bool built;
        private static KeyboardKind builtKeyboard;
        private static bool builtMouse;
        private static ShowWhat builtShow;
        private static float height = FullHeight;
        private static bool blocking;
        private static bool unblockNextFrame;
        private static bool openRequested;

        private static KeyboardView picture;
        private static RectTransform rail;
        private static RectTransform results;
        private static InputField searchBox;
        private static Text infoText;
        private static Text layerText;
        private static Text whereText;
        private static Button onlyChip;
        private static readonly Dictionary<string, Button> modifierButtons = new Dictionary<string, Button>();

        // What shows: the pick (kept for the session, "what I picked last"), the modifiers shown, the search, one key.
        private static Pick pick = new Pick(PickKind.Everything);
        private static int[] pickedLayer = Combo.NoMods;
        private static int[] layer = Combo.NoMods;
        private static string query = string.Empty;
        private static string typed = string.Empty;
        private static float searchDue = -1f;
        private static int onlyKey = -1;
        private static int drawnVersion = -1;
        private static int[] drawnLayer = Combo.NoMods;
        private static int drawnLook = -1;
        private static readonly List<int> heldNow = new List<int>();
        private static bool listDirty;
        private static Vector3 lastPointer;
        private static int hovered = -1;
        private static Row hoveredRow;

        internal static bool IsOpen => window != null && window.activeSelf;

        /// <summary>The button in F1 asks; it opens at the next frame, outside the configuration manager's drawing.</summary>
        internal static void RequestOpen() => openRequested = true;

        /// <summary>Every frame while the module is on (Frames.Update).</summary>
        internal static void Update()
        {
            if (unblockNextFrame)
            {
                unblockNextFrame = false;
                Unblock();
            }
            if (window == null && blocking)
            {
                Unblock();   // the scene changed under the open window
            }
            if (openRequested)
            {
                openRequested = false;
                if (!IsOpen && InWorld)
                {
                    Reach.CloseManager();
                    Open();
                }
                return;
            }
            if (!IsOpen)
            {
                if (KeyPressed() && CanOpen())
                {
                    Open();
                }
                return;
            }
            if (Player.m_localPlayer != null && (Player.m_localPlayer.IsDead() || Player.m_localPlayer.IsTeleporting()))
            {
                Close();
                return;
            }
            if (!SameShape())
            {
                // The keyboard, the mouse or what it shows changed in F1 while it was open: made again, as it was.
                Rebuild();
                window.SetActive(true);
                drawnVersion = -1;
            }
            FitCanvas();
            if (ZInput.GetKeyDown(KeyCode.Escape, false) || (!Ui.Typing() && KeyPressed()))
            {
                CloseSoon();
                return;
            }
            Collector.Watch();
            FollowHeldModifiers();
            if (searchDue >= 0f && Time.unscaledTime >= searchDue)
            {
                searchDue = -1f;
                query = typed;
                listDirty = true;
            }
            KeyTable table = Collector.Table;
            bool newMap = Collector.Version != drawnVersion || Settings.LookVersion != drawnLook;
            bool newLayer = !Combo.Same(layer, drawnLayer);
            if (table != null && (newMap || newLayer || listDirty))
            {
                drawnVersion = Collector.Version;
                drawnLook = Settings.LookVersion;
                drawnLayer = layer;
                if (newMap || newLayer)
                {
                    Paint(table);
                }
                // Only Everything and Free Keys show keys with the modifiers held; every other pick lists uses as they are,
                // so a modifier pressed or let go remakes only the list on the left's counts.
                bool rowsFollowLayer = pick.Kind == PickKind.Everything || pick.Kind == PickKind.Free;
                RenderLists(table, newMap || listDirty || rowsFollowLayer);
                listDirty = false;
                hovered = -2;   // the line under the picture says it again
            }
            Hover(table);
            Ui.Wheel(RowHeight, results, rail);
        }

        // ---- Opening and closing ----

        private static bool keyWarned;

        /// <summary>Its key is Insert and Bindrune, which uses Insert too, is installed: the key is left to Bindrune.</summary>
        internal static bool LeftToBindrune
        {
            get
            {
                if (!Reach.BindruneThere)
                {
                    return false;
                }
                KeyboardShortcut key = Settings.OpenKey.Value;
                return key.MainKey == KeyCode.Insert && !key.Modifiers.Any();
            }
        }

        // The key to open it, read inside the module's guard. A key the game can't read is never asked about (ZInput.GetKey
        // throws for it; LESSONS_LEARNED, archive 2026-09): said once, and the key does nothing until it changes.
        private static bool KeyPressed()
        {
            KeyboardShortcut key = Settings.OpenKey.Value;
            if (key.MainKey == KeyCode.None)
            {
                return false;
            }
            if (LeftToBindrune)
            {
                return false;   // left to Bindrune (said once at the start)
            }
            if (KeyCatalog.IdOf(key.MainKey) < 0 || key.Modifiers.Any(modifier => KeyCatalog.IdOf(modifier) < 0))
            {
                if (!keyWarned)
                {
                    keyWarned = true;
                    Module.Log?.LogWarning($"\"Key to open the key map\" is {key}, which the game can't read: the key does nothing. Pick another key, or use the button in F1.");
                }
                return false;
            }
            keyWarned = false;
            return ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
        }

        /// <summary>In a world (the main menu reads its own keys behind any window: Jötunn's input hold works only in the
        /// world).</summary>
        internal static bool InWorld => Player.m_localPlayer != null && GUIManager.CustomGUIFront != null;

        // In the world, as the game decides whether the player takes input.
        private static bool CanOpen()
        {
            if (!InWorld || ZInput.s_IsRebindActive || Reach.AnyManagerOpen || Ui.Typing())
            {
                return false;
            }
            Player player = Player.m_localPlayer;
            return !player.IsDead() && !player.InCutscene() && !player.IsTeleporting() &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !StoreGui.IsVisible() && !InventoryGui.IsVisible() && !Menu.IsVisible() &&
                   (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() && !GameCamera.InFreeFly() &&
                   !Hud.IsPieceSelectionVisible();
        }

        private static void Open()
        {
            if (GUIManager.CustomGUIFront == null)
            {
                return;
            }
            if (window == null || !SameShape())
            {
                Rebuild();
            }
            switch (Settings.Opens.Value)
            {
                case OpensOn.Everything:
                    pick = new Pick(PickKind.Everything);
                    break;
                case OpensOn.FreeKeys:
                    pick = new Pick(PickKind.Free);
                    break;
            }
            Collector.MarkDirty();   // read everything afresh as it opens
            drawnVersion = -1;
            window.SetActive(true);
            FitCanvas();
            if (!blocking)
            {
                blocking = true;
                GUIManager.BlockInput(true);
            }
            unblockNextFrame = false;
        }

        // Whether the window was made for the settings its shape is made from.
        private static bool SameShape() =>
            built && builtKeyboard == Settings.Keyboard.Value && builtMouse == Settings.ShowMouse.Value && builtShow == Settings.Show.Value;

        private static void Rebuild()
        {
            if (window != null)
            {
                UnityEngine.Object.Destroy(canvas != null ? canvas.gameObject : window);
                window = null;
                canvas = null;
            }
            builtKeyboard = Settings.Keyboard.Value;
            builtMouse = Settings.ShowMouse.Value;
            builtShow = Settings.Show.Value;
            Build();
            built = true;
        }

        /// <summary>Closes at once and gives the game its input back. Also the module's undo step.</summary>
        internal static void Close()
        {
            if (window != null && window.activeSelf)
            {
                window.SetActive(false);
            }
            openRequested = false;
            unblockNextFrame = false;
            Unblock();
        }

        // On Escape or the key: the game's input comes back a frame later, so the same press can't also open the game's
        // menu behind the closing window.
        private static void CloseSoon()
        {
            if (window != null)
            {
                window.SetActive(false);
            }
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

        private static void FitCanvas()
        {
            if (canvas == null)
            {
                return;
            }
            Canvas game = Ui.GameCanvas();
            float gameScale = game != null ? game.scaleFactor : 1f;
            float scale = gameScale * Ui.SizeFactor(Settings.Size.Value, gameScale, Screen.width, Screen.height, Width, height);
            if (!Mathf.Approximately(canvas.scaleFactor, scale))
            {
                canvas.scaleFactor = scale;
                var rect = (RectTransform)window.transform;
                rect.anchoredPosition = Vector2.zero;   // a size change re-centres it, so it can't hang off the screen
            }
            if (background != null)
            {
                float alpha = Mathf.Clamp(Settings.Opacity.Value, 0.2f, 1f);
                if (!Mathf.Approximately(background.color.a, alpha))
                {
                    background.color = new Color(background.color.r, background.color.g, background.color.b, alpha);
                }
            }
        }

        // ---- Modifiers ----

        // Holding Shift, Ctrl or Alt shows the keys with that held; letting go goes back to the row's pick.
        private static void FollowHeldModifiers()
        {
            heldNow.Clear();
            if (!Ui.Typing())
            {
                foreach (KeyCode modifier in KeyCatalog.LayerModifiers)
                {
                    if (ZInput.GetKey(modifier, false))
                    {
                        heldNow.Add((int)modifier);
                    }
                }
            }
            // A new set only when what is held changed (no array a frame).
            bool same = heldNow.Count == 0 ? ReferenceEquals(layer, pickedLayer) || Combo.Same(layer, pickedLayer)
                                           : heldNow.Count == layer.Length && heldNow.TrueForAll(id => Array.IndexOf(layer, id) >= 0);
            if (!same)
            {
                layer = heldNow.Count > 0 ? Combo.Sorted(heldNow) : pickedLayer;
                ShowLayer(heldNow.Count > 0);
            }
        }

        private static void PickLayer(KeyCode modifier)
        {
            int[] wanted = modifier == KeyCode.None ? Combo.NoMods : new[] { (int)modifier };
            pickedLayer = Combo.Same(wanted, pickedLayer) && wanted.Length > 0 ? Combo.NoMods : wanted;
            layer = pickedLayer;
            ShowLayer(false);
        }

        private static void ShowLayer(bool following)
        {
            if (layerText != null)
            {
                layerText.text = "Showing keys with " + Combo.LayerText(layer) + (following ? " (as you hold it)" : string.Empty);
            }
            foreach (KeyValuePair<string, Button> each in modifierButtons)
            {
                bool on = each.Key == "None" ? layer.Length == 0 : layer.Any(id => KeyCatalog.NameOf(id).EndsWith(each.Key, StringComparison.Ordinal));
                Text label = each.Value.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = on ? Ui.Orange : Ui.Plain;
                    string side = each.Key == "None" ? string.Empty : layer.Where(id => KeyCatalog.NameOf(id).EndsWith(each.Key, StringComparison.Ordinal))
                                                                       .Select(id => KeyCatalog.NameOf(id).Split(' ')[0]).FirstOrDefault();
                    label.text = string.IsNullOrEmpty(side) ? each.Key : $"{side} {each.Key}";
                }
            }
        }

        // ---- Drawing ----

        private static void Paint(KeyTable table)
        {
            if (picture == null)
            {
                return;
            }
            int[] shown = layer;
            picture.Paint(key => table.StateOf(key, shown), Settings.Letters.Value);
        }

        private static void RenderLists(KeyTable table, bool rowsToo)
        {
            if (rail == null || results == null)
            {
                return;
            }
            List<RailRow> railRows = Browse.Rail(table, layer, query, onlyKey);
            if (!railRows.Any(row => row.Pick.Equals(pick)))
            {
                pick = new Pick(PickKind.Everything);
                rowsToo = true;
            }
            Ui.Clear(rail);
            foreach (RailRow row in railRows)
            {
                RailRowView(row);
            }
            if (!rowsToo)
            {
                return;
            }
            hoveredRow = null;   // the rows are made again: none is under the pointer until it moves
            List<Row> rows = Browse.Rows(pick, table, layer, query, onlyKey);
            RailRow picked = railRows.First(row => row.Pick.Equals(pick));
            if (whereText != null)
            {
                whereText.text = $"{picked.Label}: {rows.Count(row => !row.IsHeading)}" +
                                 (pick.Kind == PickKind.Everything || pick.Kind == PickKind.Free ? $", with {Combo.LayerText(layer)}" : string.Empty);
            }
            if (onlyChip != null)
            {
                onlyChip.gameObject.SetActive(onlyKey >= 0);
                Text chipText = onlyChip.GetComponentInChildren<Text>();
                if (chipText != null && onlyKey >= 0)
                {
                    chipText.text = $"Only {KeyCatalog.NameOf(onlyKey)} ×";
                }
            }
            Ui.Clear(results);
            if (rows.Count == 0)
            {
                GameObject empty = Ui.Row(results, 40f);
                Ui.Label(empty.transform, query.Length > 0 || onlyKey >= 0 ? "Nothing here matches. Clear the search, or the key picked." : "Nothing here.",
                         8f, 0f, ResultsWidth - 30f, 40f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
                return;
            }
            foreach (Row row in rows.Take(400))
            {
                if (row.IsHeading)
                {
                    GameObject heading = Ui.Row(results, 30f);
                    Ui.Label(heading.transform, row.Heading, 6f, 2f, ResultsWidth - 30f, 28f, 17, TextAnchor.LowerLeft, Ui.Orange, bold: true);
                    continue;
                }
                ResultRowView(row, table);
            }
        }

        private static void RailRowView(RailRow row)
        {
            bool picked = row.Pick.Equals(pick);
            Clicks clicks = Ui.ListRow(rail, RailRowHeight, out Image back);
            back.color = picked ? Ui.RowPicked : Ui.RowPlain;
            Text label = Ui.Label(clicks.transform, row.Label, 10f, 0f, RailWidth - 70f, RailRowHeight, 15, TextAnchor.MiddleLeft, Ui.Plain);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 15;
            Ui.Label(clicks.transform, row.Count.ToString(), RailWidth - 62f, 0f, 36f, RailRowHeight, 12, TextAnchor.MiddleRight, picked ? Ui.Plain : Ui.Quiet);
            clicks.Enter = () => back.color = picked ? Ui.RowPicked : Ui.RowHover;
            clicks.Exit = () => back.color = picked ? Ui.RowPicked : Ui.RowPlain;
            Pick target = row.Pick;
            clicks.Left = () =>
            {
                pick = target;
                listDirty = true;
                ScrollToTop(results);
            };
        }

        private static float ResultsWidth => Width - ResultsX - Left;

        // A column's text shrinks (and wraps to two lines) rather than losing its end in a row one line tall.
        private static void Fit(Text text, int size)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = size;
        }

        // Where the list sits (right of the list on the left), whenever the window shows one.
        private const float ResultsX = Left + RailWidth + 16f;

        private static void ResultRowView(Row row, KeyTable table)
        {
            Clicks clicks = Ui.ListRow(results, RowHeight, out Image back);
            Status status = row.State != null ? row.State.Status : row.UseStatus;
            Ui.Box("Swatch", clicks.transform, 6f, 5f, 16f, 16f, Settings.ColourOf(status));
            float w = ResultsWidth - 30f;
            if (row.State != null)
            {
                KeyState state = row.State;
                string name = state.Layer.Length > 0 && (pick.Kind == PickKind.Clashes || pick.Kind == PickKind.Shared)
                    ? Combo.Describe(state.Key, state.Layer)
                    : KeyCatalog.NameOf(state.Key);
                Fit(Ui.Label(clicks.transform, name, 30f, 0f, 200f, RowHeight, 15, TextAnchor.MiddleLeft, Ui.Plain), 15);
                Fit(Ui.Label(clicks.transform, Browse.ShortWord(state.Status), 234f, 0f, 150f, RowHeight, 13, TextAnchor.MiddleLeft, Ui.Quiet), 13);
                Text users = Ui.Label(clicks.transform, Browse.Users(state, 4), 388f, 0f, w - 388f, RowHeight, 14, TextAnchor.MiddleLeft, Ui.Plain);
                users.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
            {
                Use use = row.Use;
                Fit(Ui.Label(clicks.transform, use.Keys, 30f, 0f, 230f, RowHeight, 15, TextAnchor.MiddleLeft, Ui.Plain), 15);
                string owner = pick.Kind == PickKind.Mod || pick.Kind == PickKind.Pack || pick.Kind == PickKind.Game || pick.Kind == PickKind.Console ? use.What : use.Who;
                Fit(Ui.Label(clicks.transform, owner, 264f, 0f, 380f, RowHeight, 14, TextAnchor.MiddleLeft, Ui.Plain), 14);
                string note = (string.IsNullOrEmpty(use.When) ? (use.Held ? "held with something else" : Unknown(use)) : use.When) +
                              (table.Options.IsMaybe(use) ? " (maybe)" : string.Empty);
                Text when = Ui.Label(clicks.transform, note, 650f, 0f, w - 650f, RowHeight, 13, TextAnchor.MiddleLeft, Ui.Quiet);
                when.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            int key = Browse.KeyOf(row);
            clicks.Enter = () =>
            {
                back.color = Ui.RowHover;
                hoveredRow = row;
                ShowRow(row, table);
            };
            clicks.Exit = () =>
            {
                back.color = Ui.RowPlain;
                if (hoveredRow == row)
                {
                    hoveredRow = null;
                    hovered = -2;
                }
            };
            clicks.Left = () => PickKey(key);
        }

        // A use whose when isn't known says so in the list (it counts as working anywhere).
        private static string Unknown(Use use) => use.Known || use.Maybe ? string.Empty : "when not known";

        private static void ShowRow(Row row, KeyTable table)
        {
            if (infoText == null)
            {
                return;
            }
            if (row.State != null)
            {
                infoText.text = Browse.Describe(row.State, table, Layout.Of(Settings.Keyboard.Value), Settings.Suggest.Value, Settings.ShowWhen.Value);
                picture?.Mark(row.State.Key);
                return;
            }
            Use use = row.Use;
            KeyState state = table.StateOf(use.Main, use.Mods);
            infoText.text = $"{use.Keys}: {Browse.UseText(use, Settings.ShowWhen.Value)}" + (string.IsNullOrEmpty(use.Section) ? string.Empty : $", in \"{use.Section}\"") +
                            (string.IsNullOrEmpty(use.Detail) ? string.Empty : $" ({use.Detail})") + ". " +
                            Browse.Describe(state, table, Layout.Of(Settings.Keyboard.Value), Settings.Suggest.Value, Settings.ShowWhen.Value);
            picture?.Mark(use.Main);
        }

        // The key under the pointer on the picture, said under it; nothing new while the pointer stays still.
        private static void Hover(KeyTable table)
        {
            if (picture == null || infoText == null || table == null || hoveredRow != null)
            {
                return;
            }
            Vector3 pointer = ZInput.pointerPosition;
            if (pointer == lastPointer && hovered != -2)
            {
                return;
            }
            lastPointer = pointer;
            int key = picture.KeyAt(pointer);
            if (key == hovered)
            {
                return;
            }
            hovered = key;
            picture.Mark(key >= 0 ? key : onlyKey);
            infoText.text = key >= 0
                ? Browse.Describe(table.StateOf(key, layer), table, Layout.Of(Settings.Keyboard.Value), Settings.Suggest.Value, Settings.ShowWhen.Value)
                : "Point at a key to see who uses it; click it to list only its rows. Hold Shift, Ctrl or Alt to see the keys with it held.";
        }

        private static void PickKey(int key)
        {
            onlyKey = onlyKey == key ? -1 : key;
            listDirty = true;
            ScrollToTop(results);
        }

        private static void ScrollToTop(RectTransform content)
        {
            ScrollRect scroll = content != null ? content.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        // ---- Building the window, once (again when the keyboard, the mouse or what it shows changes) ----

        private static void Build()
        {
            canvas = Ui.RootCanvas("SpareKeys_Canvas", 2002, clickable: true);
            ShowWhat show = Settings.Show.Value;
            bool hasPicture = show != ShowWhat.List;
            bool hasList = show != ShowWhat.Picture;
            KeyboardKind kind = Settings.Keyboard.Value;
            bool mouse = Settings.ShowMouse.Value;
            Layout layout = Layout.Of(kind);
            float pictureUnits = layout.Width + (mouse ? Layout.MouseGap + Layout.MouseWidth : 0f);
            float pictureX = hasList ? ResultsX : Left;
            float pictureWidth = Width - pictureX - Left;
            float unit = Mathf.Min(hasList ? 36f : 44f, pictureWidth / pictureUnits);
            float pictureHeight = hasPicture ? Mathf.Max(layout.Height, mouse ? 1.25f + Layout.MouseHeight : 0f) * unit : 0f;
            float infoY = Top + 40f + (hasPicture ? pictureHeight + 8f : 0f);
            height = hasList ? FullHeight : infoY + 56f + 16f;

            GUIManager gui = GUIManager.Instance;
            window = gui.CreateWoodpanel(canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, height, true);
            window.name = "SpareKeys_Window";
            background = window.GetComponent<Image>();
            Transform root = window.transform;

            // The title, the colours' legend beside it, and Close.
            Ui.Label(root, Module.Name, Left, 12f, 200f, 36f, 26, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            Legend(root, Left + 230f, 22f);
            Ui.Button(root, "Close", Width - Left - 110f, 14f, 110f, 32f, CloseSoon);

            // The modifier row (left click: the left key; right click: the right one), and what's shown.
            float x = pictureX;
            modifierButtons.Clear();
            foreach (KeyValuePair<string, KeyCode[]> each in new[]
            {
                new KeyValuePair<string, KeyCode[]>("None", new[] { KeyCode.None, KeyCode.None }),
                new KeyValuePair<string, KeyCode[]>("Ctrl", new[] { KeyCode.LeftControl, KeyCode.RightControl }),
                new KeyValuePair<string, KeyCode[]>("Alt", new[] { KeyCode.LeftAlt, KeyCode.RightAlt }),
                new KeyValuePair<string, KeyCode[]>("Shift", new[] { KeyCode.LeftShift, KeyCode.RightShift }),
            })
            {
                KeyCode leftKey = each.Value[0];
                KeyCode rightKey = each.Value[1];
                Button button = Ui.Button(root, each.Key, x, Top, 104f, 30f, () => PickLayer(leftKey));
                button.gameObject.AddComponent<Clicks>().Right = () => PickLayer(rightKey);
                modifierButtons[each.Key] = button;
                x += 108f;
            }
            layerText = Ui.Label(root, string.Empty, x + 6f, Top, Width - Left - x - 6f, 30f, 14, TextAnchor.MiddleLeft, Ui.Quiet);

            picture = hasPicture ? KeyboardView.Build(root, pictureX, Top + 40f, unit, kind, mouse, PickKey) : null;
            infoText = Ui.Label(root, string.Empty, pictureX, infoY, Width - pictureX - Left, 52f, 14, TextAnchor.UpperLeft, Ui.Plain);
            infoText.resizeTextForBestFit = true;   // a long line shrinks a little rather than losing its end
            infoText.resizeTextMinSize = 10;
            infoText.resizeTextMaxSize = 14;

            rail = null;
            results = null;
            searchBox = null;
            whereText = null;
            onlyChip = null;
            if (hasList)
            {
                float listTop = infoY + 58f;
                rail = Ui.Scroll("SpareKeys_Rail", root, Left, Top, RailWidth, height - Top - 20f);
                GameObject field = gui.CreateInputField(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, InputField.ContentType.Standard,
                                                        "Search keys, mods, settings", 15, 300f, 30f);
                Ui.Place(field, ResultsX, listTop, 300f, 30f);
                searchBox = field.GetComponent<InputField>();
                searchBox.SetTextWithoutNotify(typed);
                searchBox.onValueChanged.AddListener(text => Module.Guard("searching the key map", () =>
                {
                    typed = text ?? string.Empty;
                    searchDue = Time.unscaledTime + 0.25f;   // drawn again once he pauses, not at every letter
                }));
                whereText = Ui.Label(root, string.Empty, ResultsX + 312f, listTop, 380f, 30f, 15, TextAnchor.MiddleLeft, Ui.Quiet);
                onlyChip = Ui.Button(root, string.Empty, ResultsX + ResultsWidth - 230f, listTop, 230f, 30f, () => PickKey(onlyKey));
                onlyChip.gameObject.SetActive(false);
                results = Ui.Scroll("SpareKeys_Results", root, ResultsX, listTop + 36f, ResultsWidth, height - listTop - 36f - 20f);
            }
            ShowLayer(false);
            hovered = -2;
            window.SetActive(false);
        }

        private static void Legend(Transform root, float x, float y)
        {
            foreach (KeyValuePair<Status, string> each in new[]
            {
                new KeyValuePair<Status, string>(Status.Free, "Free"),
                new KeyValuePair<Status, string>(Status.WithModifier, "With a modifier"),
                new KeyValuePair<Status, string>(Status.Used, "Used"),
                new KeyValuePair<Status, string>(Status.Shared, "Shared"),
                new KeyValuePair<Status, string>(Status.Clash, "Clash"),
                new KeyValuePair<Status, string>(Status.Maybe, "Maybe"),
            })
            {
                GameObject swatch = Ui.Box("SpareKeys_Swatch", root, x, y, 16f, 16f, Settings.ColourOf(each.Key));
                swatch.AddComponent<LegendSwatch>().Status = each.Key;
                float width = each.Key == Status.WithModifier ? 120f : each.Key == Status.Shared ? 58f : 50f;
                Ui.Label(root, each.Value, x + 20f, y - 4f, width, 24f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
                x += 20f + width + 8f;
            }
        }
    }

    /// <summary>A legend's colour box, which follows its colour setting.</summary>
    internal sealed class LegendSwatch : MonoBehaviour
    {
        internal Status Status;
        private Image image;

        private void Update()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }
            if (image != null && Module.IsOn)
            {
                image.color = Settings.ColourOf(Status);
            }
        }
    }
}
