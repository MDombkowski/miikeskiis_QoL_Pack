using System;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using UiButton = UnityEngine.UI.Button;   // StoreStash.Button is the quick stack button

namespace StoreStash
{
    /// <summary>
    /// The trash can's question (card A53): what it would delete, "Delete" or "Keep", and a "Don't ask again" box, which
    /// with Delete sets "Ask before deleting" to Never. Built in the game's style from Jötunn's parts (a wood panel, the
    /// game's fonts, buttons and a checkbox), as Add to Cart's pop-up is, on the canvas Jötunn keeps for mods' windows,
    /// over a dark screen that takes every click meant for the window behind it. Delete takes a click (Enter does nothing,
    /// so no stray key deletes: Enter is also the split window's OK); Esc, or a gamepad's B, says Keep; while it asks, the
    /// game's keys that close the inventory (Esc, Tab, E, B, Y) are held back, so they close only the question. It goes as
    /// Keep if the inventory closes, the mod is switched off, or he dies or teleports. Tall enough for the longest text the
    /// can writes (Trash.PlanText: a count line, six kinds, "and N more kinds", what's kept), which shrinks to fit past that.
    /// </summary>
    internal static class TrashDialog
    {
        internal const float Width = 440f;

        internal const float Height = 356f;

        private static GameObject root;
        private static Text title;
        private static Text body;
        private static Toggle dontAsk;
        private static Action onDelete;

        /// <summary>The frame it last closed in: the game's inventory keys are held back through that frame too, whichever of
        /// the game and the mod reads the key first.</summary>
        internal static int ClosedInFrame = -1;

        internal static bool IsOpen => root != null && root.activeSelf;

        /// <summary>Asks; <paramref name="delete"/> runs after a Delete (inside the module's Guard).</summary>
        internal static void Open(string heading, string text, Action delete)
        {
            if (!Build())
            {
                // No canvas to ask on (Jötunn's isn't there): nothing is deleted without the question.
                Messages.TopLeft("Trash: the question couldn't be shown, so nothing was deleted (see the log)");
                return;
            }
            title.text = heading;
            body.text = text;
            dontAsk.isOn = false;
            onDelete = delete;
            UITooltip.HideTooltip();
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        /// <summary>Every frame (the kit calls it while the module is switched off too): Esc (or B) while it asks, and closing
        /// it as Keep once there's nothing left to ask about.</summary>
        internal static void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            if (!Module.IsOn || !TrashSettings.On.Value || player == null || player.IsDead() || player.IsTeleporting() || !InventoryGui.IsVisible())
            {
                Close(delete: false);
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.Escape, false) || ZInput.GetButtonDown("JoyButtonB"))
            {
                Close(delete: false);
            }
        }

        /// <summary>The undo step after an error: the question goes, nothing deleted.</summary>
        internal static void Remove()
        {
            onDelete = null;
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
            root = null;
        }

        private static void Close(bool delete)
        {
            ClosedInFrame = Time.frameCount;
            Action act = onDelete;
            onDelete = null;
            bool never = dontAsk != null && dontAsk.isOn;
            if (root != null)
            {
                root.SetActive(false);
            }
            if (!delete || act == null)
            {
                return;
            }
            // What he said yes to first: "Don't ask again" is a settings write, which could fail with the file busy.
            Module.Guard("deleting from the trash can", act);
            if (never)
            {
                TrashSettings.Ask.Value = TrashAsk.Never;
            }
        }

        // Makes it the first time it's needed, and again after Unity has destroyed it with its canvas.
        private static bool Build()
        {
            if (root != null)
            {
                return true;
            }
            GameObject front = GUIManager.CustomGUIFront;
            GUIManager gui = GUIManager.Instance;
            if (front == null || gui == null)
            {
                Module.Log.LogWarning("The trash can's question can't be shown: Jötunn's canvas for mods' windows isn't there.");
                return false;
            }
            root = new GameObject("StoreStash_TrashQuestion", typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.SetParent(front.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image dark = root.GetComponent<Image>();
            dark.color = new Color(0f, 0f, 0f, 0.45f);
            dark.raycastTarget = true;   // takes every click meant for the window behind it

            GameObject panel = gui.CreateWoodpanel(root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, Height, false);
            Transform p = panel.transform;
            title = Label(gui, p, 20f, 16f, Width - 40f, 32f, 22, TextAnchor.MiddleCenter, gui.ValheimOrange, bold: true);
            // Ten lines of the game's Averia Serif at 16 (19.35 each: its own line spacing, read from the game's font):
            // the longest text the can writes, and a longer one (a very long name) shrinks to fit instead of losing lines.
            body = Label(gui, p, 30f, 54f, Width - 60f, 200f, 16, TextAnchor.UpperCenter, new Color(0.95f, 0.92f, 0.85f, 1f), bold: false);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 11;
            body.resizeTextMaxSize = 16;

            dontAsk = Checkbox(gui, p, Width / 2f - 88f, 262f, 22f);
            Text boxLabel = Label(gui, p, Width / 2f - 60f, 260f, 180f, 26f, 15, TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.85f, 1f), bold: false);
            boxLabel.text = "Don't ask again";

            MakeButton(gui, p, "Delete", Width / 2f - 130f, 298f, () => Module.Guard("answering the trash can's question", () => Close(delete: true)));
            MakeButton(gui, p, "Keep", Width / 2f + 10f, 298f, () => Module.Guard("answering the trash can's question", () => Close(delete: false)));
            return true;
        }

        private static RectTransform Place(GameObject thing, float x, float y, float width, float height)
        {
            var rect = (RectTransform)thing.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Text Label(GUIManager gui, Transform parent, float x, float y, float width, float height, int size, TextAnchor align, Color colour, bool bold)
        {
            GameObject made = gui.CreateText(string.Empty, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                             bold ? gui.AveriaSerifBold : gui.AveriaSerif, size, colour, true, Color.black, width, height, false);
            Place(made, x, y, width, height);
            Text label = made.GetComponent<Text>();
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static void MakeButton(GUIManager gui, Transform parent, string text, float x, float y, Action clicked)
        {
            GameObject made = gui.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 120f, 36f);
            Place(made, x, y, 120f, 36f);
            UiButton button = made.GetComponent<UiButton>();
            button.onClick.AddListener(() => clicked());
            // A clicked button would keep the keyboard, and Enter press it again (the pack's other buttons' lesson).
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        private static Toggle Checkbox(GUIManager gui, Transform parent, float x, float y, float size)
        {
            GameObject made = gui.CreateToggle(parent, size, size);
            // Jötunn parents it keeping its place in the world, which scales it by one over the GUI's scale (Add to Cart's
            // review 1): its own size, rotation and depth, then.
            made.transform.localScale = Vector3.one;
            made.transform.localRotation = Quaternion.identity;
            Place(made, x, y, size, size);
            Vector3 at = made.transform.localPosition;
            made.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Text label = made.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }
            Toggle toggle = made.GetComponent<Toggle>();
            Navigation navigation = toggle.navigation;
            navigation.mode = Navigation.Mode.None;
            toggle.navigation = navigation;
            return toggle;
        }
    }

    /// <summary>The game's InventoryGui.Update, found by name at start: while the trash can asks (and through the frame it
    /// stops asking), the inventory counts as just shown, so the game's Esc, Tab and E don't close it under the question.</summary>
    [HarmonyPatch]
    internal static class HoldInventoryOpen
    {
        private static bool Prepare() => SortHooks.InventoryUpdate != null;

        private static MethodBase TargetMethod() => SortHooks.InventoryUpdate;

        private static void Prefix(InventoryGui __instance)
        {
            if (Module.IsOn && (TrashDialog.IsOpen || TrashDialog.ClosedInFrame == Time.frameCount))
            {
                SortHooks.HoldOpen(__instance);
            }
        }
    }
}
