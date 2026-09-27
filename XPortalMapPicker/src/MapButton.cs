using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// The Map button on XPortal's destination row: list, then Map, then Ping. When XPortal hides
    /// Ping (no destination chosen yet), the Map button takes Ping's place at the end of the row.
    /// </summary>
    internal static class MapButton
    {
        private const string ObjectName = "XPortalMapPicker_MapButton";

        // XPortal's own spacing between the controls on its panel (its "padding").
        private const float Gap = 24f;

        private const float MinimumWidth = 60f;

        // Room either side of the label inside the button.
        private const float LabelPadding = 12f;

        private static GameObject button;
        private static Text label;

        // The Enabled and ButtonLabel values last acted on, so a change made in game is noticed.
        private static bool? enabledApplied;
        private static string labelApplied;

        // The list's caption (the text of the chosen row), as XPortal built it: its width, and the room the list leaves
        // around it (mostly for the arrow). They are measured when XPortal builds its panel, before anything has moved,
        // so the button can also be added later, when the mod is switched on in game. The caption keeps its width
        // however wide the list is, so it is narrowed along with the list; otherwise a long name would run on under
        // the button.
        private static GameObject measuredPanel;
        private static float captionWidth;
        private static float captionRoom;

        /// <summary>Called after XPortal's InitialiseUI, which runs every time a portal is opened but only builds the
        /// panel the first time in each world session.</summary>
        internal static void OnPanelBuilt(object panel)
        {
            GameObject mainPanel = XPortalHooks.MainPanel(panel);
            if (mainPanel == null)
            {
                return;   // XPortal built nothing: no screen (a dedicated server), or it couldn't find its parent object
            }
            if (mainPanel != measuredPanel)
            {
                Dropdown list = XPortalHooks.DestinationList(panel);
                captionWidth = list.captionText.rectTransform.rect.width;
                captionRoom = ((RectTransform)list.transform).rect.width - captionWidth;
                measuredPanel = mainPanel;
            }
            if (Settings.Enabled.Value)
            {
                AddTo(panel);
            }
        }

        /// <summary>Runs every frame: follows the Enabled and ButtonLabel settings.</summary>
        internal static void Tick()
        {
            bool enabled = Settings.Enabled.Value;
            if (enabledApplied == null)
            {
                enabledApplied = enabled;
            }
            else if (enabled != enabledApplied)
            {
                enabledApplied = enabled;
                object panel = XPortalHooks.Panel;
                if (enabled)
                {
                    // Back at once if XPortal's panel is already built; otherwise when XPortal builds it.
                    AddTo(panel);
                    Arrange(panel);
                }
                else
                {
                    Remove();
                }
            }
            else if (button != null && labelApplied != Settings.ButtonLabel.Value)
            {
                Arrange(XPortalHooks.Panel);
            }
        }

        /// <summary>Called right after XPortal has laid out the destination row, which it does each time the
        /// list is filled or its choice changes. Places the button and ends the list one gap before it.</summary>
        internal static void Arrange(object panel)
        {
            GameObject mainPanel = XPortalHooks.MainPanel(panel);
            if (button == null || mainPanel == null || button.transform.parent != mainPanel.transform)
            {
                return;
            }

            labelApplied = Settings.ButtonLabel.Value;
            label.text = labelApplied;
            Dropdown list = XPortalHooks.DestinationList(panel);
            RectTransform caption = list.captionText.rectTransform;

            // The rule XPortal applies to its own Ping button: no map features in a world without a map, or when
            // the server has turned map pings off. XPortal has already sized the list for that case.
            bool allowed = !Game.m_noMap && !XPortalHooks.ServerTurnedOffMapPing();
            button.SetActive(allowed);
            if (!allowed)
            {
                caption.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, captionWidth);
                return;
            }

            GameObject pingButton = XPortalHooks.PingButton(panel);
            RectTransform ping = (RectTransform)pingButton.transform;
            float width = Mathf.Clamp(label.preferredWidth + 2f * LabelPadding, MinimumWidth, Mathf.Max(MinimumWidth, ping.rect.width));

            // Left edges measured from the anchor the button shares with Ping (the panel's top-right corner).
            float pingLeft = ping.anchoredPosition.x - ping.pivot.x * ping.rect.width;
            float left = pingButton.activeSelf ? pingLeft - Gap - width : pingLeft + ping.rect.width - width;
            RectTransform own = (RectTransform)button.transform;
            own.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            own.anchoredPosition = new Vector2(left + own.pivot.x * width, ping.anchoredPosition.y);

            // XPortal ended the list one gap before Ping, or before the panel's edge when Ping is hidden.
            // Move its right edge to one gap before the button instead; its left edge stays where it is.
            RectTransform listRect = (RectTransform)list.transform;
            Rect panelArea = ((RectTransform)mainPanel.transform).rect;
            float buttonLeft = Mathf.Lerp(panelArea.xMin, panelArea.xMax, ping.anchorMax.x) + left;
            float listRightAnchor = Mathf.Lerp(panelArea.xMin, panelArea.xMax, listRect.anchorMax.x);
            listRect.offsetMax = new Vector2(buttonLeft - Gap - listRightAnchor, listRect.offsetMax.y);

            caption.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(captionWidth, listRect.rect.width - captionRoom));
        }

        /// <summary>Takes the button away and lets XPortal lay its row out as it would without this mod: when the
        /// mod is switched off in the settings, and after an error.</summary>
        internal static void Remove()
        {
            if (button == null)
            {
                return;
            }
            UnityEngine.Object.Destroy(button);
            button = null;
            label = null;

            object panel = XPortalHooks.Panel;
            if (XPortalHooks.MainPanel(panel) != measuredPanel)
            {
                return;
            }
            XPortalHooks.DestinationList(panel).captionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, captionWidth);
            XPortalHooks.ArrangeDestinationRow.Invoke(panel, new object[] { XPortalHooks.PingButton(panel).activeSelf });
        }

        // Adds the button to XPortal's panel, once per panel, if XPortal has built the panel and it was measured.
        private static void AddTo(object panel)
        {
            GameObject mainPanel = XPortalHooks.MainPanel(panel);
            if (mainPanel == null || mainPanel != measuredPanel || (button != null && button.transform.parent == mainPanel.transform))
            {
                return;
            }

            // Built like Jotunn's GUIManager.CreateButton, which XPortal uses for Ping, but styled before it is
            // parented: XPortal's panel is usually inactive here, and CreateButton can't find the label under an
            // inactive parent. Same anchors, pivot and size as Ping; Arrange() puts it in its place.
            RectTransform ping = (RectTransform)XPortalHooks.PingButton(panel).transform;
            button = DefaultControls.CreateButton(GUIManager.Instance.ValheimControlResources);
            button.name = ObjectName;
            GUIManager.Instance.ApplyButtonStyle(button.GetComponent<Button>());
            label = button.GetComponentInChildren<Text>(includeInactive: true);
            labelApplied = Settings.ButtonLabel.Value;
            label.text = labelApplied;
            button.transform.SetParent(mainPanel.transform, worldPositionStays: false);

            RectTransform own = (RectTransform)button.transform;
            own.anchorMin = ping.anchorMin;
            own.anchorMax = ping.anchorMax;
            own.pivot = ping.pivot;
            own.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ping.rect.width);
            own.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ping.rect.height);

            button.GetComponent<Button>().onClick.AddListener(OnClick);
            Module.Log.LogDebug("Added the Map button to XPortal's panel");
        }

        private static void OnClick() => Module.Guard("opening the map", PickMode.Start);
    }
}
