using System.Collections.Generic;
using QoLMods.Pictures;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ComfortBuild
{
    /// <summary>
    /// The Comfort tab in the game's build menu (BuildUIV2): a fifth tab after Favorites, made from a copy of the game's
    /// own tab button, its list the fifth of the menu's lists (ComfortTab), so the game's own code fills it, selects it,
    /// searches it and switches to it with Q and E or a gamepad's shoulder buttons. On it, two rows sit above the list of
    /// groups: "Comfort here: 9" and the switch "Grey Out Unneeded" (the favourites dropdown's checkbox). They are taken
    /// out of the list whenever another tab fills it, so the game's own rows and its divider stay where it puts them.
    ///
    /// The tab shows only while the mod and its setting are on and the tool in hand has comfort pieces he can build.
    /// Hidden, its button leaves the tab handler's list empty at its place (a missing button is skipped by Q and E), so
    /// the places of the game's own tabs, and of any another mod adds, never move.
    /// </summary>
    internal static class Menu
    {
        private const int SwitchTag = -77;

        /// <summary>How to take the menu off the Comfort tab (LeaveTab).</summary>
        private enum Leave
        {
            /// <summary>By the game's own tab switch, which fills the first tab's list at once: from the module's Update.</summary>
            Refill,

            /// <summary>By the menu's fields only, the game filling the list itself: in OpenBuildMenu's prefix.</summary>
            Fields,

            /// <summary>By the fields, closing the menu if it's open: an undo step, perhaps inside the game's own code.</summary>
            FieldsAndClose,
        }

        private static BuildUi ui;
        private static ComfortTab list;
        private static int index = -1;
        private static Button tabButton;
        private static TabHandler.Tab tabEntry;
        private static bool shown;

        private static GameObject holder;
        private static GameObject hereRow;
        private static TextMeshProUGUI hereText;
        private static BuildUiTagButton greySwitch;
        private static GameObject greyTick;

        private static bool drewAnything;
        private static PieceTable countedTool;
        private static int countedAvailable = -1;
        private static bool toolHasComfort;

        private static int drawnVersion = -1;
        private static int drawnGreys = -1;
        private static int drawnSettings = -1;
        private static int drawnOrder = -1;
        private static float differentSince = -1f;

        /// <summary>The build menu the mod has joined, or null.</summary>
        internal static BuildUi Ui => ui;

        /// <summary>True while the build menu's list is the Comfort tab.</summary>
        internal static bool OnOurTab => ui != null && index >= 0 && Game.CurrentList(ui) == index;

        /// <summary>True while the build menu is open.</summary>
        internal static bool Open => ui != null && ui.gameObject.activeSelf;

        /// <summary>After the build menu wakes (BuildUi.Awake): adds the Comfort list and its tab button, hidden until
        /// the first frame decides.</summary>
        internal static void Attach(BuildUi buildUi)
        {
            if (buildUi == null || buildUi == ui)
            {
                return;
            }
            ui = buildUi;
            list = null;
            index = -1;
            tabButton = null;
            tabEntry = null;
            shown = false;
            holder = null;
            hereRow = null;
            hereText = null;
            greySwitch = null;
            greyTick = null;
            countedTool = null;
            drawnVersion = drawnGreys = drawnSettings = drawnOrder = -1;
            Badges.Forget();
            Line.Forget();
            Greys.Prune();   // a new world may load the game's pictures afresh

            List<IPieceList> lists = Game.PieceLists(ui);
            TabHandler handler = Game.Tabs(ui);
            RectTransform tabs = Game.TabContainer(ui);
            if (lists == null || handler == null || tabs == null || tabs.childCount == 0)
            {
                Module.Log.LogWarning("The build menu has no tabs to add to; the Comfort tab is left out (numbers on every tab still work).");
                return;
            }
            // The tab handler's tabs and the menu's lists go by the same numbers: add ours only where both agree.
            if (handler.m_tabs.Count != lists.Count || tabs.childCount != lists.Count)
            {
                Module.Log.LogWarning($"The build menu has {lists.Count} lists, {handler.m_tabs.Count} tabs and {tabs.childCount} tab buttons; " +
                                      "another mod may have changed it, so the Comfort tab is left out (numbers on every tab still work).");
                return;
            }

            // A copy of the game's own last tab button (Favorites): the tab bar's layout sizes it like the others.
            Button source = handler.m_tabs[lists.Count - 1].m_button;
            GameObject original = source != null && source.transform.parent == tabs ? source.gameObject : tabs.GetChild(tabs.childCount - 1).gameObject;
            GameObject copy = Object.Instantiate(original, tabs);
            copy.name = "ComfortBuild_Tab";
            copy.SetActive(false);
            Button button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnTabClicked);
            button.interactable = true;
            var comfortHolder = new GameObject("ComfortBuild_Rows", typeof(RectTransform));
            comfortHolder.SetActive(false);
            comfortHolder.transform.SetParent(ui.transform, false);

            // The list and the tab handler's entry together, last, so the two numberings never part.
            tabButton = button;
            holder = comfortHolder;
            list = new ComfortTab();
            tabEntry = new TabHandler.Tab { m_button = null, m_page = null, m_default = false, m_onClick = new UnityEvent() };
            index = lists.Count;
            lists.Add(list);
            handler.m_tabs.Add(tabEntry);
            LabelTab();
            Module.Log.LogInfo($"The Comfort tab joins the build menu as its tab {index + 1}.");
        }

        private static void OnTabClicked()
        {
            Module.Guard("opening the Comfort tab", () =>
            {
                if (ui != null && shown)
                {
                    ui.SelectPieceList(index);
                }
            });
        }

        /// <summary>Before the build menu opens (BuildUi.OpenBuildMenu): a fresh look around him, and the tab shown or
        /// hidden for the tool in hand. The grid is filled by the game next, with the settings as they are now.</summary>
        internal static void BeforeOpen(BuildUi buildUi)
        {
            if (buildUi != ui)
            {
                return;
            }
            Looks.Find(ui);
            LabelTab();
            Nearby.Look(true);
            Player player = Player.m_localPlayer;
            ShowTab(player != null ? player.GetBuildTool() : null, Leave.Fields);
            drawnOrder = Settings.OrderVersion;
        }

        /// <summary>Before the list of groups is filled again (BuildUi.UpdateTagButtons): our rows leave it, so the game's
        /// own rows keep their places.</summary>
        internal static void BeforeTags(BuildUi buildUi)
        {
            if (buildUi == ui)
            {
                ParkRows();
            }
        }

        /// <summary>After the list is filled: on the Comfort tab, our two rows go above "Show All".</summary>
        internal static void AfterTags(BuildUi buildUi)
        {
            if (buildUi != ui || !OnOurTab || !Module.IsOn)
            {
                return;
            }
            PlaceRows();
            Game.ConfigureNavigation(ui);
        }

        /// <summary>Every frame (the module's Update, outside the game's own calls): follows the switches, what is near
        /// him and the grey copies.</summary>
        internal static void Tick()
        {
            if (ui == null)
            {
                return;
            }
            if (!Module.IsOn)
            {
                if (drewAnything)
                {
                    drewAnything = false;
                    LeaveTab(Leave.Refill);
                    HideTab();
                    ParkRows();
                    Badges.UndressAll();
                    Line.Hide();
                    Nearby.Forget();
                    drawnVersion = drawnSettings = -1;
                }
                return;
            }
            drewAnything = true;
            Player player = Player.m_localPlayer;
            if (player == null || !player.InPlaceMode())
            {
                return;
            }
            ShowTab(Open ? Game.CurrentTool(ui) : player.GetBuildTool(), Leave.Refill);
            bool changed = Nearby.Look(false);
            Nearby.CompareWithGame(ref differentSince);
            if (!Open)
            {
                return;
            }
            Greys.Work(4);
            if (Settings.OrderVersion != drawnOrder)
            {
                drawnOrder = Settings.OrderVersion;
                if (OnOurTab)
                {
                    ui.SelectPieceList(index, true);   // the game fills the grid again, in the new order
                }
            }
            if (changed || Settings.Version != drawnSettings || Greys.Version != drawnGreys || Nearby.Version != drawnVersion)
            {
                Redraw();
            }
        }

        /// <summary>Draws what depends on where he stands and on the settings: the buttons, the groups' labels, the rows.</summary>
        internal static void Redraw()
        {
            if (ui == null || !Open)
            {
                return;
            }
            drawnVersion = Nearby.Version;
            drawnGreys = Greys.Version;
            drawnSettings = Settings.Version;
            Badges.DressAll(ui);
            if (!OnOurTab)
            {
                return;
            }
            List<BuildUiTagButton> tags = Game.TagButtons(ui);
            if (tags != null && list != null)
            {
                foreach (BuildUiTagButton tag in tags)
                {
                    if (tag != null)
                    {
                        tag.Setup(list.Label(tag.m_tagId), tag.m_tagId, false);
                    }
                }
            }
            RefreshRows();
        }

        /// <summary>The undo steps after an error (the kit runs each, even if one before it fails): everything the mod
        /// shows in the menu goes, and the menu leaves the Comfort tab last. That may happen inside the game's own filling
        /// of the grid, so the menu isn't filled again here: it is closed, and opens on its first tab.</summary>
        internal static IEnumerable<System.Action> UndoSteps => new System.Action[]
        {
            HideTab, ParkRows, Badges.UndressAll, Line.Hide, Nearby.Forget, () => LeaveTab(Leave.FieldsAndClose),
        };

        // Takes the menu off the Comfort tab (Leave says how).
        private static void LeaveTab(Leave how)
        {
            if (ui == null || index < 0 || Game.CurrentList(ui) != index)
            {
                return;
            }
            if (how == Leave.Refill && Open && Game.CurrentTool(ui) != null)
            {
                ui.SelectPieceList(0);
                return;
            }
            // The Comfort tab's groups are numbers the next list would read as its own tags: back to "Show All". The
            // tab bar shows the first tab as chosen, and the game fills its list again at its next opening, or on its
            // next frame while it's open (BuildUi.Update refills when the count of pieces it last filled differs; after
            // an error inside the game's own opening, the first tab's list of categories may show only "Show All" until
            // he changes tab).
            Game.CurrentList(ui) = 0;
            Game.CurrentTag(ui) = -1;
            Game.AvailableCount(ui) = -1;
            TabHandler handler = Game.Tabs(ui);
            if (handler != null && handler.m_tabs.Count > 0)
            {
                handler.SetActiveTabWithoutInvokingOnClick(0);
            }
            if (how == Leave.FieldsAndClose && Open)
            {
                ui.Close();
            }
        }

        private static void HideTab()
        {
            if (tabEntry != null)
            {
                tabEntry.m_button = null;
            }
            if (tabButton != null)
            {
                tabButton.gameObject.SetActive(false);
            }
            shown = false;
        }

        // Shows the tab while the mod and its setting are on and the tool has comfort pieces; else hides it, leaving the
        // Comfort tab first if the menu is on it.
        private static void ShowTab(PieceTable tool, Leave how)
        {
            if (tabButton == null || tabEntry == null)
            {
                return;
            }
            bool want = Module.IsOn && Settings.ComfortTab.Value && tool != null && !tool.m_hideAdvancedMenu && HasComfort(tool);
            if (want == shown)
            {
                return;
            }
            if (!want)
            {
                LeaveTab(how);
                HideTab();
                return;
            }
            shown = true;
            tabEntry.m_button = tabButton;
            tabButton.interactable = !OnOurTab;
            tabButton.gameObject.SetActive(true);
        }

        // The tab's label, in the player's language (again at each opening: the language can change in game).
        private static void LabelTab()
        {
            if (tabButton == null || Localization.instance == null)
            {
                return;
            }
            string label = Localization.instance.Localize(ComfortTab.Token);
            foreach (TMP_Text text in tabButton.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text != label)
                {
                    text.text = label;
                }
            }
        }

        // Whether the tool has comfort pieces he can build, counted again only when its pieces change.
        private static bool HasComfort(PieceTable tool)
        {
            if (tool != countedTool || tool.m_availablePieces.Count != countedAvailable)
            {
                countedTool = tool;
                countedAvailable = tool.m_availablePieces.Count;
                toolHasComfort = ComfortTab.HasAny(tool);
            }
            return toolHasComfort;
        }

        private static void PlaceRows()
        {
            RectTransform content = Game.TagContent(ui);
            if (content == null)
            {
                return;
            }
            MakeRows();
            if (hereRow != null)
            {
                hereRow.transform.SetParent(content, false);
                hereRow.transform.SetSiblingIndex(0);
            }
            if (greySwitch != null)
            {
                greySwitch.transform.SetParent(content, false);
                greySwitch.transform.SetSiblingIndex(hereRow != null ? 1 : 0);
            }
            RefreshRows();
        }

        private static void ParkRows()
        {
            if (holder == null)
            {
                return;
            }
            if (hereRow != null && hereRow.transform.parent != holder.transform)
            {
                hereRow.transform.SetParent(holder.transform, false);
            }
            if (greySwitch != null && greySwitch.transform.parent != holder.transform)
            {
                greySwitch.transform.SetParent(holder.transform, false);
            }
        }

        private static void RefreshRows()
        {
            string hex = Looks.Hex(Settings.NumberColor.Value);
            if (hereText != null)
            {
                hereText.text = Words.Here(Nearby.Level, Nearby.Sheltered, hex);
            }
            if (greyTick != null)
            {
                greyTick.SetActive(Settings.GreyOut.Value);
            }
            else if (greySwitch != null)
            {
                greySwitch.Setup((Settings.GreyOut.Value ? "[x] " : "[ ] ") + Words.GreyOutSwitch, SwitchTag, false);
            }
        }

        // The two rows, made once per menu, in the list's own looks: a line of the list's text, and a copy of the list's
        // own row button with the favourites dropdown's checkbox at its left. Each shrinks its text to fit its row (the
        // list is narrower once its scrollbar shows).
        private static void MakeRows()
        {
            if (holder == null)
            {
                return;
            }
            Looks.Find(ui);
            if (hereRow == null)
            {
                hereRow = new GameObject("ComfortBuild_Here", typeof(RectTransform));
                hereRow.transform.SetParent(holder.transform, false);
                var rect = (RectTransform)hereRow.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, 30f);
                LayoutElement layout = hereRow.AddComponent<LayoutElement>();
                layout.minHeight = 30f;
                layout.preferredHeight = 30f;
                hereText = Looks.Text(hereRow.transform, "Text", false, 15f, false);
                RectTransform textRect = hereText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 0f);
                textRect.offsetMax = new Vector2(-4f, 0f);
                hereText.alignment = TextAlignmentOptions.MidlineLeft;
                hereText.color = Looks.ListText;
                hereText.enableAutoSizing = true;
                hereText.fontSizeMin = 10f;
                hereText.fontSizeMax = 15f;
            }
            if (greySwitch == null)
            {
                GameObject prefab = Game.TagButtonPrefab(ui);
                if (prefab == null)
                {
                    return;
                }
                GameObject row = Object.Instantiate(prefab, holder.transform);
                row.name = "ComfortBuild_GreySwitch";
                greySwitch = row.GetComponent<BuildUiTagButton>();
                greySwitch.Setup(Words.GreyOutSwitch, SwitchTag, false);
                greySwitch.Selected += _ => Module.Guard("switching the grey", () =>
                {
                    Settings.GreyOut.Value = !Settings.GreyOut.Value;
                    Redraw();
                });
                foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 10f;
                }
                // The checkbox: the favourites dropdown's own box and tick, at the row's left; the text moves right.
                if (Looks.CheckBox != null)
                {
                    GameObject box = Object.Instantiate(Looks.CheckBox, row.transform);
                    box.name = "Check";
                    var boxRect = (RectTransform)box.transform;
                    boxRect.anchorMin = new Vector2(0f, 0.5f);
                    boxRect.anchorMax = new Vector2(0f, 0.5f);
                    boxRect.pivot = new Vector2(0f, 0.5f);
                    boxRect.sizeDelta = new Vector2(18f, 18f);
                    boxRect.anchoredPosition = new Vector2(6f, 0f);
                    foreach (Graphic graphic in box.GetComponentsInChildren<Graphic>(true))
                    {
                        graphic.raycastTarget = false;
                    }
                    Image tick = box.transform.childCount > 0 ? box.transform.GetChild(0).GetComponent<Image>() : null;
                    greyTick = tick != null ? tick.gameObject : null;
                    foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                    {
                        RectTransform textRect = text.rectTransform;
                        textRect.offsetMin = new Vector2(textRect.offsetMin.x + 24f, textRect.offsetMin.y);
                    }
                }
            }
        }
    }
}
