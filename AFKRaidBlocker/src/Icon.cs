using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AFKRaidBlocker
{
    /// <summary>
    /// The AFK sign (card B19): a small panel of the game's own wood (the trophy panel's sprite and lit material, as
    /// Jötunn styles the game's windows) with "AFK" in the game's orange and its bold font, at the top middle of his screen,
    /// shown the whole time the mod counts him as away and never otherwise (Away.Update decides, every frame).
    ///
    /// It sits on a root canvas of its own, drawn after the game's whole GUI: Jötunn's canvas for mods' windows is sorted
    /// 2000, the pack's own windows 2001 and 2002, so the inventory, a chest, the crafting and build menus, the map and the
    /// pack's windows all stay under it. It never takes a click (no raycaster, and nothing on it is a raycast target). It is
    /// drawn at the game's GUI scale, so it keeps its size on every screen and at every resolution as the game's own HUD
    /// does, and it hides while the game hides its HUD (Ctrl+F3, a cutscene), as the game's messages do. The canvas lives in
    /// the scene the game's GUI is in, so it goes with it when he leaves the world, and is made again when needed.
    ///
    /// A nicety, as the away mark is: an error showing it switches only the sign off for the session, with one line in the
    /// log, never the mod (which holds off the raids).
    /// </summary>
    internal static class Icon
    {
        /// <summary>Over Jötunn's canvas for mods' windows (2000) and the pack's windows (Add to Cart 2001, Outfitter and
        /// Spare Keys 2002).</summary>
        internal const int SortingOrder = 2010;

        internal const float Width = 76f;
        internal const float Height = 34f;

        /// <summary>From the top edge of the screen, in the game's GUI units.</summary>
        internal const float Top = 10f;

        internal const string Text = "AFK";

        private static Canvas canvas;
        private static GameObject panel;
        private static bool broken;

        /// <summary>Whether the sign shows now (for a harness and the log).</summary>
        internal static bool Showing => panel != null && panel.activeSelf;

        /// <summary>Shows the sign while <paramref name="wanted"/> and the game's HUD shows; hides it otherwise. Called
        /// every frame.</summary>
        internal static void Show(bool wanted)
        {
            if (broken)
            {
                return;
            }
            try
            {
                if (!wanted || !HudShown())
                {
                    if (panel != null && panel.activeSelf)
                    {
                        panel.SetActive(false);
                    }
                    return;
                }
                if (!Ready())
                {
                    return;
                }
                Fit();
                if (!panel.activeSelf)
                {
                    panel.SetActive(true);
                }
            }
            catch (Exception error)
            {
                broken = true;
                Module.Log.LogWarning($"Switched the AFK sign off for this session after an error showing it; raids are " +
                                      $"still held off as before. {error.GetType().Name}: {error.Message}");
                Remove();
            }
        }

        /// <summary>Takes the sign away altogether: an undo step of the module.</summary>
        internal static void Remove()
        {
            try
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }
                if (canvas != null)
                {
                    UnityEngine.Object.Destroy(canvas.gameObject);
                }
            }
            finally
            {
                canvas = null;
                panel = null;
            }
        }

        /// <summary>The game's own test of whether its HUD shows (hidden by Ctrl+F3 or the gamepad's toggle, or in a
        /// cutscene: Hud.Update, SetVisible).</summary>
        private static bool HudShown()
        {
            Hud hud = Hud.instance;
            return hud != null && hud.IsVisible();
        }

        /// <summary>Makes the canvas and the sign once the game's GUI is there; false while it isn't.</summary>
        private static bool Ready()
        {
            if (canvas != null && panel != null)
            {
                return true;
            }
            GameObject front = GUIManager.CustomGUIFront;
            if (front == null || GUIManager.Instance == null)
            {
                return false;
            }
            if (canvas != null)
            {
                UnityEngine.Object.Destroy(canvas.gameObject);   // half made before: start again
            }
            canvas = null;
            panel = null;

            var holder = new GameObject("AFKRaidBlocker_IconCanvas", typeof(RectTransform), typeof(Canvas));
            holder.layer = front.layer;
            if (holder.scene != front.scene)
            {
                SceneManager.MoveGameObjectToScene(holder, front.scene);
            }
            Canvas made = holder.GetComponent<Canvas>();
            made.renderMode = RenderMode.ScreenSpaceOverlay;
            made.sortingOrder = SortingOrder;
            Canvas frontCanvas = front.GetComponent<Canvas>();
            if (frontCanvas != null)
            {
                made.additionalShaderChannels = frontCanvas.additionalShaderChannels;
            }
            Canvas game = frontCanvas != null ? frontCanvas.rootCanvas : null;
            if (game != null)
            {
                made.pixelPerfect = game.pixelPerfect;
                made.referencePixelsPerUnit = game.referencePixelsPerUnit;
                made.scaleFactor = game.scaleFactor;
            }
            canvas = made;

            var sign = new GameObject("AFKRaidBlocker_Icon", typeof(RectTransform), typeof(Image));
            sign.SetActive(false);
            sign.transform.SetParent(holder.transform, false);
            GUIManager gui = GUIManager.Instance;
            gui.ApplyWoodpanelStyle(sign.transform);   // the game's wood: sprite, slicing, lit material
            Image image = sign.GetComponent<Image>();
            image.raycastTarget = false;
            var rect = (RectTransform)sign.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -Top);
            rect.sizeDelta = new Vector2(Width, Height);

            GameObject label = gui.CreateText(Text, sign.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                              gui.AveriaSerifBold, 20, gui.ValheimOrange, true, Color.black, Width, Height, false);
            label.name = "AFKRaidBlocker_IconText";
            UnityEngine.UI.Text text = label.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
            }
            panel = sign;
            return true;
        }

        /// <summary>Follows the game's GUI scale (its own setting), so the sign is drawn as big as the game's HUD.</summary>
        private static void Fit()
        {
            GameObject front = GUIManager.CustomGUIFront;
            Canvas frontCanvas = front != null ? front.GetComponent<Canvas>() : null;
            Canvas game = frontCanvas != null ? frontCanvas.rootCanvas : null;
            if (game != null && !Mathf.Approximately(canvas.scaleFactor, game.scaleFactor))
            {
                canvas.scaleFactor = game.scaleFactor;
            }
        }
    }
}
