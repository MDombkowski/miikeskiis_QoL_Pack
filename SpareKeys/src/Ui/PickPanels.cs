using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace SpareKeys
{
    /// <summary>
    /// The two panels beside a "press a key": in shudnal's configuration manager (F1) while one of its key settings waits
    /// for a key, and in the game's Settings, Controls tab while it rebinds. Display only: they sit on a canvas of their
    /// own with no raycaster and no raycast target anywhere, so a click on them reaches whatever is under them as if they
    /// weren't there; and since a click there would be taken as the new key, they offer nothing to click. The key under
    /// the pointer is found by measuring, and named on the panel. They follow a held modifier where the key being picked
    /// can have one (a shortcut in F1), never in the Controls menu (the game's buttons carry none). Each is placed clear of
    /// the windows it belongs to (PanelPlace).
    /// </summary>
    internal static class PickPanels
    {
        private static readonly PickPanel f1 = new PickPanel("SpareKeys_F1Panel");
        private static readonly PickPanel controls = new PickPanel("SpareKeys_ControlsPanel");
        private static KeyboardMouseSettings bindingIn;
        private static bool rebindSeen;
        private static readonly List<Rect> clear = new List<Rect>();

        // The F1 panel's title, made again only when the setting picked for changes.
        private static ConfigEntryLike titledFor;
        private static string f1Title;
        private static int f1Mark = -1;
        private static Use f1Self;

        // The Controls panel's, made again only when the button being bound changes.
        private static string boundFor;
        private static string controlsTitle;
        private static int controlsMark = -1;
        private static Use controlsSelf;

        /// <summary>Every frame while the module is on.</summary>
        internal static void Update()
        {
            UpdateF1();
            UpdateControls();
        }

        /// <summary>Hides both. Also the module's undo step.</summary>
        internal static void HideAll()
        {
            f1.Hide();
            controls.Hide();
        }

        private static void UpdateF1()
        {
            PanelKind kind = Settings.F1Panel.Value;
            if (kind == PanelKind.Off || !Reach.F1PanelPossible || KeyMapWindow.IsOpen || !Reach.PickingInManager(out ConfigEntryLike entry, out bool inEdit))
            {
                f1.Hide();
                return;
            }
            if (!ReferenceEquals(entry, titledFor) || f1Version != Collector.Version)
            {
                titledFor = entry;
                KeyTable unused = Collector.Table;   // the whens of other mods' keys are read with the map
                f1Version = Collector.Version;
                f1Self = Collector.SelfFor(entry);
                Combo now = entry.KeyCodeOnly ? KeyText.Single(entry.Value, out _) : KeyText.Shortcut(entry.Value, out _);
                string what = entry.Name.Length > 0 ? (entry.Plugin.Length > 0 ? $"{entry.Plugin}: {entry.Name}" : entry.Name) : "a key setting";
                f1Title = $"Picking a key for {what}" + WhenWords(f1Self) + (now.IsNone ? "." : $", now {now.Text}.") +
                          (f1Self != null && f1Self.Known && Settings.SharedKeys.Value == SharedRule.ShownAsShared
                              ? " Green is free; the shared colour is used, but only where this never works, so free for it; grey is taken where this works."
                              : " Green is free.") +
                          (Browse.BlueFiresToo(entry.KeyCodeOnly, entry.PluginId)
                              ? " Blue isn't safe for this key: it also fires with that modifier held."
                              : " Blue is free alone, taken with a modifier.");
                f1Mark = now.IsNone ? -1 : now.Main;
            }
            clear.Clear();
            Rect main = Reach.ManagerRect();
            clear.Add(main);
            Rect? edit = Reach.EditRect();
            if (edit.HasValue)
            {
                clear.Add(edit.Value);
            }
            // Beside the window that waits for the key: the Edit window's when it is that one.
            Rect beside = inEdit && edit.HasValue ? edit.Value : main;
            f1.Show(kind, f1Title, followModifiers: !entry.KeyCodeOnly, f1Mark, beside, clear, sides: true, self: f1Self);
        }

        private static void UpdateControls()
        {
            PanelKind kind = Settings.ControlsPanel.Value;
            bool rebinding = ZInput.s_IsRebindActive;
            if (rebinding && !rebindSeen)
            {
                bindingIn = UnityEngine.Object.FindFirstObjectByType<KeyboardMouseSettings>();
            }
            rebindSeen = rebinding;
            if (kind == PanelKind.Off || !rebinding || bindingIn == null || !Reach.Binding(bindingIn, out GameObject dialog, out string button))
            {
                controls.Hide();
                if (!rebinding)
                {
                    bindingIn = null;
                    boundFor = null;
                }
                return;
            }
            if (button != boundFor || controlsTitle == null)
            {
                boundFor = button;
                string current = null;
                controlsMark = -1;
                if (!string.IsNullOrEmpty(button) && ZInput.instance != null)
                {
                    string path = null;
                    try
                    {
                        path = ZInput.instance.GetButtonDef(button)?.GetActionPath(true);
                    }
                    catch (Exception)
                    {
                        // A button with no bind: nothing to mark.
                    }
                    controlsMark = KeyCatalog.IdOfPath(path);
                    current = controlsMark >= 0 ? KeyCatalog.NameOf(controlsMark) : null;
                }
                controlsSelf = GameKeys.Template(button);
                controlsTitle = $"Binding {GameKeys.ActionName(button) ?? "a control"}" + WhenWords(controlsSelf) + (current != null ? $", now {current}." : ".") +
                                " Green: free of the game and every mod" +
                                (controlsSelf != null && controlsSelf.Known && Settings.SharedKeys.Value == SharedRule.ShownAsShared
                                    ? "; the shared colour: used, but only where this never works, so free for it"
                                    : string.Empty) +
                                ". Blue: used with a modifier, which would fire this too.";
            }
            Rect around = ScreenRect(dialog);
            clear.Clear();
            clear.Add(around);
            controls.Show(kind, controlsTitle, followModifiers: false, controlsMark, around, clear, sides: false, self: controlsSelf);
        }

        private static int f1Version = -1;

        // " (the hammer in hand)": where the setting picked for works, when that's known.
        private static string WhenWords(Use self) =>
            self != null && self.Known ? $" ({(string.IsNullOrEmpty(self.When) ? self.Place.Words : self.When)})" : string.Empty;

        private static readonly Vector3[] corners = new Vector3[4];

        // A GameObject's rect on the screen, in pixels from the top left.
        private static Rect ScreenRect(GameObject thing)
        {
            var rect = thing.transform as RectTransform;
            if (rect == null)
            {
                return new Rect(Screen.width / 2f, Screen.height / 2f, 0f, 0f);
            }
            rect.GetWorldCorners(corners);
            Canvas root = thing.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
            return new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
        }
    }

    /// <summary>One display-only panel: built on first show (again when its look changes), placed beside a rect each frame.</summary>
    internal sealed class PickPanel
    {
        private const float Unit = 26f;
        private const float Pad = 12f;

        private readonly string name;
        private Canvas canvas;
        private GameObject panel;
        private PanelKind builtKind;
        private KeyboardKind builtKeyboard;
        private bool builtMouse;
        private KeyboardView picture;
        private Text titleText;
        private Text layerText;
        private Text listText;
        private Text infoText;
        private float width;
        private float height;
        private int drawnVersion = -1;
        private int[] drawnLayer = Combo.NoMods;
        private int drawnLook = -1;
        private int hovered = -2;
        private bool wasShown;
        private readonly List<int> held = new List<int>();
        private int[] layer = Combo.NoMods;
        private Use drawnSelf;

        internal PickPanel(string name)
        {
            this.name = name;
        }

        internal void Hide()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
            wasShown = false;
        }

        internal void Show(PanelKind kind, string title, bool followModifiers, int mark, Rect beside, IList<Rect> clear, bool sides, Use self = null)
        {
            if (GUIManager.CustomGUIFront == null)
            {
                return;
            }
            if (panel == null || builtKind != kind || builtKeyboard != Settings.Keyboard.Value || builtMouse != Settings.ShowMouse.Value)
            {
                if (canvas != null)
                {
                    UnityEngine.Object.Destroy(canvas.gameObject);
                }
                Build(kind);
                drawnVersion = -1;
            }
            if (!wasShown)
            {
                wasShown = true;
                Collector.MarkDirty();   // read afresh as it shows
                drawnVersion = -1;
                hovered = -2;
            }
            if (!panel.activeSelf)
            {
                panel.SetActive(true);
            }
            Collector.Watch();

            Canvas game = Ui.GameCanvas();
            float scale = game != null ? game.scaleFactor : 1f;
            if (!Mathf.Approximately(canvas.scaleFactor, scale))
            {
                canvas.scaleFactor = scale;
            }
            Vector2 at = PanelPlace.Choose(new Vector2(width * scale, height * scale), beside, clear, sides, new Vector2(Screen.width, Screen.height));
            ((RectTransform)panel.transform).anchoredPosition = new Vector2(at.x / scale, -at.y / scale);

            FollowModifiers(followModifiers);
            KeyTable table = Collector.Table;
            if (titleText.text != title)
            {
                titleText.text = title;
            }
            if (Collector.Version != drawnVersion || Settings.LookVersion != drawnLook || !Combo.Same(layer, drawnLayer) || !ReferenceEquals(self, drawnSelf))
            {
                drawnVersion = Collector.Version;
                drawnLook = Settings.LookVersion;
                drawnLayer = layer;
                drawnSelf = self;
                int[] shown = layer;
                layerText.text = "Keys with " + Combo.LayerText(shown) + (followModifiers ? " (hold Shift, Ctrl or Alt to see them with it)" : string.Empty);
                if (picture != null)
                {
                    picture.Paint(key => table.StateFor(key, shown, self), Settings.Letters.Value);
                }
                if (listText != null)
                {
                    listText.text = FreeList(table, shown, self);
                }
                hovered = -2;
            }
            if (picture != null)
            {
                int key = picture.KeyAt(ZInput.pointerPosition);
                if (key != hovered)
                {
                    hovered = key;
                    picture.Mark(key >= 0 ? key : mark);
                    infoText.text = key >= 0
                        ? Browse.DescribeFor(table.StateFor(key, layer, self), self, table, Layout.Of(Settings.Keyboard.Value), Settings.Suggest.Value, Settings.ShowWhen.Value)
                        : "Point at a key to see who uses it. This panel only shows: press the key you want.";
                }
            }
        }

        // The modifiers held now (a new array only when they change).
        private void FollowModifiers(bool follow)
        {
            held.Clear();
            if (follow)
            {
                foreach (KeyCode modifier in KeyCatalog.LayerModifiers)
                {
                    if (ZInput.GetKey(modifier, false))
                    {
                        held.Add((int)modifier);
                    }
                }
            }
            if (held.Count != layer.Length || held.Any(id => Array.IndexOf(layer, id) < 0))
            {
                layer = Combo.Sorted(held);
            }
        }

        // The free keys in words, by where they sit: "Letters: J, N, O · Digits: 9, 0 …"; for a setting whose when is
        // known, with the keys used only where it never works.
        private static string FreeList(KeyTable table, int[] layer, Use self)
        {
            IEnumerable<string> parts = Browse.ListedKeys(table)
                .Where(key => key.Region != Region.Gamepad && KeyTable.IsFreeFor(table.StateFor(key.Id, layer, self).Status))
                .GroupBy(key => key.Region).OrderBy(group => group.Key)
                .Select(group => Browse.RegionHeading(group.Key) + ": " + string.Join(", ", group.Select(key => key.Cap.Length > 0 && key.Region != Region.NumberPad ? key.Cap : key.Name)));
            string free = string.Join("\n", parts);
            return free.Length > 0 ? "Free: \n" + free : "No key is free with these modifiers held.";
        }

        private void Build(PanelKind kind)
        {
            builtKind = kind;
            builtKeyboard = Settings.Keyboard.Value;
            builtMouse = Settings.ShowMouse.Value;
            canvas = Ui.RootCanvas(name + "_Canvas", 2003, clickable: false);
            Layout layout = Layout.Of(builtKeyboard);
            float pictureWidth = (layout.Width + (builtMouse ? Layout.MouseGap + Layout.MouseWidth : 0f)) * Unit;
            float pictureHeight = Mathf.Max(layout.Height, builtMouse ? 1.25f + Layout.MouseHeight : 0f) * Unit;
            bool showPicture = kind == PanelKind.KeyboardPicture;
            width = showPicture ? pictureWidth + 2f * Pad : 520f;
            float bodyHeight = showPicture ? pictureHeight : 190f;
            height = Pad + 40f + 20f + 6f + bodyHeight + 6f + 48f + Pad;

            GUIManager gui = GUIManager.Instance;
            panel = gui.CreateWoodpanel(canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height, false);
            panel.name = name;
            var rect = (RectTransform)panel.transform;
            rect.pivot = new Vector2(0f, 1f);
            Transform root = panel.transform;
            Ui.Label(root, Module.Name, Pad, Pad - 2f, 120f, 22f, 16, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            titleText = Ui.Label(root, string.Empty, Pad + 110f, Pad - 2f, width - 2f * Pad - 110f, 42f, 13, TextAnchor.UpperLeft, Ui.Plain);
            layerText = Ui.Label(root, string.Empty, Pad, Pad + 40f, width - 2f * Pad, 20f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            float bodyTop = Pad + 40f + 20f + 6f;
            picture = showPicture ? KeyboardView.Build(root, Pad, bodyTop, Unit, builtKeyboard, builtMouse, null) : null;
            listText = showPicture ? null : Ui.Label(root, string.Empty, Pad, bodyTop, width - 2f * Pad, bodyHeight, 14, TextAnchor.UpperLeft, Ui.Plain);
            infoText = Ui.Label(root, string.Empty, Pad, bodyTop + bodyHeight + 6f, width - 2f * Pad, 48f, 13, TextAnchor.UpperLeft, Ui.Plain);
            infoText.resizeTextForBestFit = true;
            infoText.resizeTextMinSize = 9;
            infoText.resizeTextMaxSize = 13;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 9;
            titleText.resizeTextMaxSize = 13;

            // Display only: nothing on it may take a click (one would be taken as the new key), whatever Jötunn's parts
            // set up.
            foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            foreach (Selectable selectable in panel.GetComponentsInChildren<Selectable>(true))
            {
                selectable.interactable = false;
            }
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            panel.SetActive(false);
        }
    }
}
