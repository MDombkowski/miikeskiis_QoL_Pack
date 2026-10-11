using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MenUI
{
    /// <summary>
    /// After every frame: fits the main menu's list, and the in-game menu, onto the window when their buttons reach past
    /// it, and gives the main menu's arrow keys every button. Nothing is patched; whatever the game, its animators or
    /// other mods set on a menu this frame is taken as the menu's own size and place, and the fit is worked out from that.
    /// </summary>
    internal static class MenuFit
    {
        private static readonly Fitter MainMenu = new Fitter("main menu", anchor: 1f, isMainMenu: true);
        private static readonly Fitter InGame = new Fitter("in-game menu", anchor: 0.5f, isMainMenu: false);

        private static readonly List<Button> Shown = new List<Button>();
        private static Button[] gameArrowKeys;
        private static Button[] ourArrowKeys;
        private static float arrowKeysCheckedAt = -1f;
        private const float ArrowKeysEvery = 0.25f;

        internal static IEnumerable<Action> UndoSteps => new Action[]
        {
            () => MainMenu.Release(quietly: false),
            () => InGame.Release(quietly: false),
            () => GiveBackArrowKeys(),
        };

        internal static void AfterFrame(bool on)
        {
            RectTransform mainList = GameHooks.MainMenuList();
            MainMenu.Tick(mainList, mainList, on);
            RectTransform dialog = GameHooks.InGameMenu();
            InGame.Tick(dialog, GameHooks.InGameEntries(dialog), on && Settings.InGameMenu.Value);

            if (on && Settings.ArrowKeysReachAll.Value && mainList != null && GameHooks.CanSetArrowKeys)
            {
                CheckArrowKeys(mainList);
            }
            else if (ourArrowKeys != null)
            {
                GiveBackArrowKeys();
            }
        }

        /// <summary>A few times a second: when the game's arrow-key list lacks a button the main menu shows, give it every
        /// shown button, in the order they are shown (as ConfigurationManager does for its own). The game reads this list
        /// only for the keyboard's up and down arrows.</summary>
        private static void CheckArrowKeys(RectTransform list)
        {
            if (arrowKeysCheckedAt >= 0f && Time.unscaledTime - arrowKeysCheckedAt < ArrowKeysEvery)
            {
                return;
            }
            arrowKeysCheckedAt = Time.unscaledTime;

            list.GetComponentsInChildren(false, Shown);
            if (Shown.Count == 0)
            {
                return;
            }
            Button[] current = GameHooks.ArrowKeyButtons();
            if (current != null && ContainsAll(current, Shown))
            {
                return;
            }
            if (ourArrowKeys == null || !ReferenceEquals(current, ourArrowKeys))
            {
                gameArrowKeys = current;
            }
            ourArrowKeys = Shown.ToArray();
            GameHooks.SetArrowKeyButtons(ourArrowKeys);
        }

        private static void GiveBackArrowKeys()
        {
            Button[] current = GameHooks.ArrowKeyButtons();
            if (ourArrowKeys != null && ReferenceEquals(current, ourArrowKeys) && gameArrowKeys != null)
            {
                GameHooks.SetArrowKeyButtons(gameArrowKeys);
            }
            ourArrowKeys = null;
            gameArrowKeys = null;
            arrowKeysCheckedAt = -1f;
        }

        private static bool ContainsAll(Button[] list, List<Button> wanted)
        {
            foreach (Button button in wanted)
            {
                if (Array.IndexOf(list, button) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// One menu: what it holds, where the game put it, and what this mod changed. Its size (localScale) and its place
        /// (anchoredPosition, which a change of the window's or the GUI's size leaves alone, and which UI animators and
        /// layout groups write) are tracked apart: each is this mod's only while it still holds what this mod wrote.
        /// </summary>
        private sealed class Fitter
        {
            private const float Same = 0.0005f;
            private const float Edge = 0.01f;

            private readonly string what;
            private readonly float anchor;
            private readonly bool isMainMenu;
            private readonly List<Button> buttons = new List<Button>();
            private readonly Vector3[] corners = new Vector3[4];

            private RectTransform target;
            private Canvas canvas;

            private Vector3 ownScale;
            private bool scaleOurs;
            private Vector3 wroteScale;

            private Vector2 ownPlace;
            private bool placeOurs;
            private Vector2 wrotePlace;

            private bool hasButtons;
            private float localBottom;
            private float localTop;
            private float localLeft;
            private float localRight;

            private bool told;
            private bool described;
            private int lastSaid = -1;

            internal Fitter(string what, float anchor, bool isMainMenu)
            {
                this.what = what;
                this.anchor = anchor;
                this.isMainMenu = isMainMenu;
            }

            /// <param name="menu">What is scaled and moved, or null while the menu isn't shown.</param>
            /// <param name="measured">Where its buttons are looked for (the menu itself, or the part holding them).</param>
            internal void Tick(RectTransform menu, RectTransform measured, bool wanted)
            {
                if (!ReferenceEquals(menu, target))
                {
                    // Hidden, or a new menu (a new scene): put the old one back without a word.
                    Release(quietly: true);
                    target = menu;
                    canvas = null;
                }
                if (target == null)
                {
                    return;
                }

                // What the game, its animators or another mod set this frame is the menu's own; only an unchanged copy
                // of this mod's last write is ours. Size and place each on their own.
                if (!scaleOurs || !Near(target.localScale, wroteScale))
                {
                    ownScale = target.localScale;
                    scaleOurs = false;
                }
                if (!placeOurs || !Near(target.anchoredPosition, wrotePlace))
                {
                    ownPlace = target.anchoredPosition;
                    placeOurs = false;
                }

                if (!wanted)
                {
                    Release(quietly: false);
                    return;
                }

                Measure(measured != null ? measured : target);
                if (!hasButtons)
                {
                    Release(quietly: false);
                    return;
                }
                Place();
            }

            /// <summary>Puts the menu back where the game had it, whatever of it this mod changed.</summary>
            internal void Release(bool quietly)
            {
                if (target != null)
                {
                    if (scaleOurs && Near(target.localScale, wroteScale))
                    {
                        target.localScale = ownScale;
                    }
                    if (placeOurs && Near(target.anchoredPosition, wrotePlace))
                    {
                        target.anchoredPosition = ownPlace;
                    }
                }
                if (told && !quietly)
                {
                    Module.Log.LogInfo($"MenUI: the {what} is back as the game had it.");
                    lastSaid = -1;
                }
                scaleOurs = false;
                placeOurs = false;
                told = false;
            }

            /// <summary>Every frame: the shown buttons' extent, in the menu's own coordinates (so this mod's scale and
            /// move don't change it). Every frame, so a menu that has just opened, or a button just added, is right at
            /// once; the list is reused, so nothing is allocated.</summary>
            private void Measure(RectTransform root)
            {
                hasButtons = false;
                root.GetComponentsInChildren(false, buttons);
                float bottom = float.MaxValue, top = float.MinValue, left = float.MaxValue, right = float.MinValue;
                foreach (Button button in buttons)
                {
                    if (!(button.transform is RectTransform rect))
                    {
                        continue;
                    }
                    rect.GetWorldCorners(corners);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 local = target.InverseTransformPoint(corners[i]);
                        bottom = Math.Min(bottom, local.y);
                        top = Math.Max(top, local.y);
                        left = Math.Min(left, local.x);
                        right = Math.Max(right, local.x);
                    }
                }
                if (bottom < top)
                {
                    hasButtons = true;
                    localBottom = bottom;
                    localTop = top;
                    localLeft = left;
                    localRight = right;
                }
            }

            private void Place()
            {
                if (canvas == null)
                {
                    Canvas found = target.GetComponentInParent<Canvas>();
                    canvas = found != null ? found.rootCanvas : null;
                }
                RectTransform window = canvas != null ? canvas.transform as RectTransform : null;
                if (window == null)
                {
                    return;
                }
                window.GetWorldCorners(corners);
                float windowBottom = corners[0].y;
                float windowTop = corners[1].y;
                float windowHeight = windowTop - windowBottom;
                if (windowHeight <= 0f)
                {
                    return;
                }

                // Where the buttons are with the menu at its own size and place. Its own local position is today's,
                // moved by however far its own anchored position is from today's.
                Transform parent = target.parent;
                Vector3 ownLocal = target.localPosition + (Vector3)(ownPlace - target.anchoredPosition);
                Matrix4x4 own = (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity)
                                * Matrix4x4.TRS(ownLocal, target.localRotation, ownScale);
                Vector3 lowCorner = own.MultiplyPoint3x4(new Vector3(localLeft, localBottom, 0f));
                Vector3 highCorner = own.MultiplyPoint3x4(new Vector3(localRight, localTop, 0f));
                Vector3 pivot = own.MultiplyPoint3x4(Vector3.zero);
                float bottom = Math.Min(lowCorner.y, highCorner.y);
                float top = Math.Max(lowCorner.y, highCorner.y);
                float middle = (lowCorner.x + highCorner.x) / 2f;

                // Only a menu that really reaches past the window is fitted; then it fits inside the gap.
                bool past = bottom < windowBottom - Edge || top > windowTop + Edge;
                float scale = 1f;
                float shiftY = 0f;
                if (past)
                {
                    float gap = windowHeight * Settings.SpaceBelow.Value / 100f;
                    float low = windowBottom + gap;
                    float high = isMainMenu
                        ? Math.Max(top, windowBottom + windowHeight * Settings.HighestPoint.Value / 100f)
                        : windowTop - gap;
                    high = Math.Min(high, windowTop);
                    if (!FitMath.Solve(bottom, top, pivot.y, low, high, anchor, Settings.HowToFit.Value,
                                       Settings.SmallestSize.Value / 100f, out scale, out shiftY))
                    {
                        scale = 1f;
                        shiftY = 0f;
                    }
                }

                // Scaling about the pivot moves the buttons sideways too unless the pivot is their middle: keep their
                // middle where it was.
                float shiftX = (middle - pivot.x) * (1f - scale);
                Vector3 wantScale = ownScale * scale;
                Vector2 wantPlace = ownPlace;
                if (Math.Abs(shiftX) > Same || Math.Abs(shiftY) > Same)
                {
                    Vector3 move = new Vector3(shiftX, shiftY, 0f);
                    if (parent != null)
                    {
                        move = parent.InverseTransformVector(move);
                    }
                    wantPlace = ownPlace + new Vector2(move.x, move.y);
                }

                if (Near(wantScale, ownScale) && Near(wantPlace, ownPlace))
                {
                    Release(quietly: false);
                    return;
                }

                // One line when a menu is first fitted, and again only when the fit changes by a 5% step or starts or
                // stops moving (not at every opening, nor at every frame of a window being dragged).
                told = true;
                int said = (int)Math.Round(scale * 20f) * 2 + (Math.Abs(shiftY) > 0.5f ? 1 : 0);
                if (said != lastSaid)
                {
                    lastSaid = said;
                    Module.Log.LogInfo($"MenUI: the {what}'s {buttons.Count} buttons reached past the window; fitted at " +
                                       $"{scale * 100f:0}% of their size{(Math.Abs(shiftY) > 0.5f ? ", moved" : string.Empty)}.");
                    if (!described)
                    {
                        described = true;
                        Module.Log.LogInfo(Describe(window, bottom, top));
                    }
                }

                Apply(wantScale, wantPlace);
            }

            private void Apply(Vector3 wantScale, Vector2 wantPlace)
            {
                if (Near(wantScale, ownScale))
                {
                    if (scaleOurs && Near(target.localScale, wroteScale))
                    {
                        target.localScale = ownScale;
                    }
                    scaleOurs = false;
                }
                else
                {
                    if (!Near(target.localScale, wantScale))
                    {
                        target.localScale = wantScale;
                    }
                    scaleOurs = true;
                    wroteScale = target.localScale;
                }

                if (Near(wantPlace, ownPlace))
                {
                    if (placeOurs && Near(target.anchoredPosition, wrotePlace))
                    {
                        target.anchoredPosition = ownPlace;
                    }
                    placeOurs = false;
                }
                else
                {
                    if (!Near(target.anchoredPosition, wantPlace))
                    {
                        target.anchoredPosition = wantPlace;
                    }
                    placeOurs = true;
                    wrotePlace = target.anchoredPosition;
                }
            }

            /// <summary>Once a session per menu, for his test: what the menu is and what it holds, as found.</summary>
            private string Describe(RectTransform window, float bottom, float top)
            {
                var text = new StringBuilder();
                text.Append($"MenUI: the {what} is \"{target.name}\" (pivot {target.pivot}, anchors {target.anchorMin}-{target.anchorMax}");
                text.Append($", parent layout group: {(target.parent != null && target.parent.GetComponent<LayoutGroup>() != null ? "yes" : "no")}");
                text.Append($", animator above: {(HasAnimatorAbove(target) ? "yes" : "no")}");
                text.Append($", canvas {(canvas != null ? canvas.renderMode.ToString() : "?")}, window {window.rect.width:0}x{window.rect.height:0})");
                text.Append($"; its buttons span {bottom:0} to {top:0} in world height:");
                int named = 0;
                foreach (Button button in buttons)
                {
                    if (named++ == 16)
                    {
                        text.Append(" …");
                        break;
                    }
                    text.Append(named == 1 ? " " : ", ").Append(button.name);
                }
                return text.ToString();
            }

            /// <summary>Whether an Animator sits on the menu or above it (looked up by name: the pack doesn't reference
            /// Unity's animation module for one log line).</summary>
            private static bool HasAnimatorAbove(Transform from)
            {
                for (Transform at = from; at != null; at = at.parent)
                {
                    foreach (Component component in at.GetComponents<Component>())
                    {
                        if (component != null && component.GetType().Name == "Animator")
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private static bool Near(Vector3 a, Vector3 b) =>
                Math.Abs(a.x - b.x) < Same && Math.Abs(a.y - b.y) < Same && Math.Abs(a.z - b.z) < Same;

            private static bool Near(Vector2 a, Vector2 b) =>
                Math.Abs(a.x - b.x) < Same && Math.Abs(a.y - b.y) < Same;
        }
    }
}
