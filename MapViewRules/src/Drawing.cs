using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MapViewRules
{
    /// <summary>
    /// Applies the rules where the maps draw their pins, and only there: every pin stays in the game's list, saved,
    /// shared through a cartography table and tracked by Cartur's Map Pins as before.
    ///
    /// The game draws the pins of the map shown in Minimap.UpdatePins, and Cartur's Map Pins then adjusts them in a
    /// postfix of its own. This mod's postfix runs before Cartur's (Patches): it switches off the icons (and names) the
    /// rules hide and the names they hide, and shrinks the icons the rules shrink. Cartur's then sees those icons as not
    /// drawn and its names make room only for names that show.
    /// An icon shrinks by its height alone, its picture keeping its shape (the Image's preserveAspect, and the same for its
    /// tick mark), so it is drawn smaller while its width stays the game's: Cartur's merging and crowd-shrinking measure
    /// the narrowest icon on screen for every pin (its Crowding.Measure), and a narrower one would shrink their cells
    /// everywhere. Never through the icon's scale, which Cartur's sets every frame; and only when a size changes, since
    /// the minimap redraws its pins nearly every frame while the player moves.
    /// After every frame, names this mod hides stay hidden (the game switches a new name on a second after making it), and
    /// the pin under the pointer shows its name.
    ///
    /// A name the rules show without its icon keeps the icon, unseen: its pictures are drawn fully transparent (each
    /// Graphic's CanvasRenderer alpha, which neither the game nor Cartur's touches: Cartur's fades pins with a CanvasGroup
    /// and switches each icon's Image on and off every frame), not the icon itself switched off, so the game and Cartur's
    /// place the name as always and the pin can be clicked at its place, under its name. Only while that name shows: a pin
    /// with no name, past the game's own name zoom, or whose name the game or Cartur's hides at the moment, has its icon
    /// switched off, as a hidden pin, so an invisible pin is never clicked or deleted. The friends' map (FriendsMap) can
    /// leave a friend's pin undrawn, as a rule that hides it would.
    ///
    /// The game never switches a pin's icon back on, it only makes a new one, so an icon switched off stays off until this
    /// mod switches it on, or the game replaces it. A switched-off icon can't be clicked, ticked or deleted: the game
    /// only finds pins whose icon is active. This mod switches on only icons it switched off itself.
    /// </summary>
    internal static class Drawing
    {
        // What this mod did to the map it last drew on, so that it undoes exactly that and nothing else. A pass fills the
        // "Next" sets and swaps them in at its end; an error in between leaves both, and RestoreAll undoes both.
        private static HashSet<Minimap.PinData> iconsOff = new HashSet<Minimap.PinData>();
        private static HashSet<Minimap.PinData> iconsOffNext = new HashSet<Minimap.PinData>();
        private static Dictionary<Minimap.PinData, Shrunk> sized = new Dictionary<Minimap.PinData, Shrunk>();
        private static Dictionary<Minimap.PinData, Shrunk> sizedNext = new Dictionary<Minimap.PinData, Shrunk>();
        private static readonly List<Minimap.PinData> namesOff = new List<Minimap.PinData>();
        private static Dictionary<Minimap.PinData, Unseen> unseen = new Dictionary<Minimap.PinData, Unseen>();
        private static Dictionary<Minimap.PinData, Unseen> unseenNext = new Dictionary<Minimap.PinData, Unseen>();

        // The pins drawn as a name alone at the last pass, and those of them whose icon AfterFrame switched off because
        // their name wasn't showing.
        private static readonly List<Minimap.PinData> namesAlone = new List<Minimap.PinData>();
        private static readonly HashSet<Minimap.PinData> offForName = new HashSet<Minimap.PinData>();

        // The pins whose icon shows while the rules hide their name, where their icon sits and how far from it the pointer
        // still counts as over it: worked out at each drawing pass, so the pointer is checked every frame by arithmetic
        // alone. Their icons don't move between passes: the map redraws its pins whenever it moves.
        private static readonly List<PointerTarget> pointerTargets = new List<PointerTarget>();
        private static Vector2 lastPointer = new Vector2(float.NaN, float.NaN);
        private static int lastPointerPass = -1;
        private static Minimap.PinData lastUnder;

        private static Minimap drawnOn;
        private static int pass;
        private static bool redraw;
        private static bool wasOn;
        private static bool toldSorted;
        private static bool toldCartur;
        private static Canvas pinCanvas;

        /// <summary>Asks the map to draw its pins again: a setting changed, or the record was read.</summary>
        internal static void RequestRedraw() => redraw = true;

        /// <summary>Called every frame (the module's Update): follows the Enabled switch, reads the record when it
        /// changes, and asks the map to draw again when something changed.</summary>
        internal static void Follow()
        {
            if (!Module.IsOn)
            {
                if (wasOn)
                {
                    wasOn = false;
                    RestoreAll();
                }
                return;
            }
            if (!wasOn)
            {
                wasOn = true;
                redraw = true;
            }
            Settings.Tick();
            Minimap map = Minimap.instance;
            if (map == null)
            {
                return;
            }
            if (map != drawnOn)
            {
                Forget(map);
            }
            Record.Tick(map);
            if (redraw)
            {
                redraw = false;
                GameHooks.RequestPinUpdate(map);
                // Once a section shrinks pins or hides names, Cartur's own doing the same for every pin would do it twice.
                if (!toldCartur && Sections.AnyShrinkOrNames())
                {
                    string still = Cartur.StillShrinks();
                    if (still != null)
                    {
                        toldCartur = true;
                        Module.Log.LogWarning(still);
                    }
                }
            }
        }

        /// <summary>The postfix on UpdatePins, before Cartur's: icons and names off by the rules, and sizes.</summary>
        internal static void Draw(Minimap map)
        {
            if (map != drawnOn)
            {
                Forget(map);
            }
            List<Minimap.PinData> pins = GameHooks.Pins(map);
            bool large = map.m_mode == Minimap.MapMode.Large;
            if (pins == null || (!large && map.m_mode != Minimap.MapMode.Small))
            {
                return;
            }
            float level = large ? ZoomLevel.Large(map) : ZoomLevel.Small(map);
            RecordIndex record = Record.Index;
            bool recordPending = Record.Pending;
            pass++;
            Sorter.Prune(pins);

            iconsOffNext.Clear();
            sizedNext.Clear();
            unseenNext.Clear();
            namesOff.Clear();
            namesAlone.Clear();
            offForName.Clear();
            pointerTargets.Clear();
            bool namesDrawn = large && map.LargeZoom < map.m_showNamesZoom;   // the game draws no name past this zoom
            foreach (Minimap.PinData pin in pins)
            {
                RectTransform icon = pin?.m_uiElement;
                if (icon == null)
                {
                    continue;   // not drawn now
                }
                GameObject iconObject = icon.gameObject;
                Chain chain = Sorter.ChainFor(pin, record, recordPending);
                Decision decision = chain != null ? chain.Decide(large, level, pass) : new Decision { Size = 1f };
                if (FriendsMap.HidesPin(pin, record, recordPending) ||
                    (decision.NameAlone && (!namesDrawn || pin.m_NamePinData == null || string.IsNullOrWhiteSpace(pin.m_name))))
                {
                    // Not drawn: a friend's pin the friends' map leaves out, or a name alone with no name to show.
                    decision = new Decision { HideIcon = true, HideName = large, Size = 1f };
                }

                if (decision.HideIcon)
                {
                    if (iconObject.activeSelf)
                    {
                        iconObject.SetActive(false);
                        iconsOffNext.Add(pin);
                    }
                    else if (iconsOff.Contains(pin))
                    {
                        iconsOffNext.Add(pin);   // still off, and still this mod's doing
                    }
                }
                else if (iconsOff.Contains(pin) && !iconObject.activeSelf)
                {
                    iconObject.SetActive(true);
                }

                if (decision.NameAlone && !decision.HideIcon && pin.m_iconElement != null)
                {
                    if (!unseen.TryGetValue(pin, out Unseen hid) || !hid.Holds(pin))
                    {
                        hid = new Unseen(pin);   // a new icon (the game makes them anew): its pictures are on
                    }
                    hid.Apply();
                    unseenNext[pin] = hid;
                    namesAlone.Add(pin);
                }
                else if (unseen.TryGetValue(pin, out Unseen seenAgain))
                {
                    seenAgain.Undo(pin);
                }

                bool sizable = !pin.m_animate && !(pin.m_worldSize > 0f);
                if (large && (decision.HideIcon || decision.HideName))
                {
                    GameObject name = pin.m_NamePinData?.PinNameGameObject;
                    if (name != null)
                    {
                        if (name.activeSelf)
                        {
                            name.SetActive(false);
                        }
                        namesOff.Add(pin);
                        if (!decision.HideIcon && icon.parent == map.m_pinRootLarge)
                        {
                            float drawn = BaseSize(map, pin) * (sizable ? decision.Size : 1f);
                            Vector3 at = icon.localPosition;
                            pointerTargets.Add(new PointerTarget(pin, new Vector2(at.x, at.y), Mathf.Max(12f, drawn * 0.5f)));
                        }
                    }
                }

                if (!decision.HideIcon && !decision.NameAlone && decision.Size < 0.999f && sizable && pin.m_iconElement != null)
                {
                    if (!sized.TryGetValue(pin, out Shrunk shrunk) || !shrunk.Holds(pin))
                    {
                        shrunk = new Shrunk(pin);   // a new icon (the game makes them anew): its pictures' own shapes
                    }
                    float baseSize = BaseSize(map, pin);
                    shrunk.Apply(icon, baseSize, baseSize * decision.Size);
                    sizedNext[pin] = shrunk;
                }
                else if (sized.TryGetValue(pin, out Shrunk was))
                {
                    was.Undo(pin, icon, BaseSize(map, pin));   // no longer shrunk, or hidden now: its own size back
                }
            }
            (iconsOff, iconsOffNext) = (iconsOffNext, iconsOff);
            (sized, sizedNext) = (sizedNext, sized);
            (unseen, unseenNext) = (unseenNext, unseen);
            iconsOffNext.Clear();
            sizedNext.Clear();
            unseenNext.Clear();

            if (large && !toldSorted && !recordPending)
            {
                toldSorted = true;
                TellSorted(pins, record);
            }
        }

        /// <summary>
        /// After every frame (the module's LateUpdate), on the large map: names this mod hides are switched off again if
        /// something switched them on, and the pin under the pointer shows its name if that setting is on. A pin drawn as
        /// its name alone keeps its icon only while that name shows (the game shows a new name a second after making it,
        /// and Cartur's hides names by zoom, by crowding and by its search): otherwise it would be an invisible pin that a
        /// click could still tick or delete.
        /// </summary>
        internal static void AfterFrame()
        {
            Minimap map = Minimap.instance;
            if (!Module.IsOn || map == null || map != drawnOn || map.m_mode != Minimap.MapMode.Large ||
                (namesOff.Count == 0 && namesAlone.Count == 0))
            {
                return;
            }
            foreach (Minimap.PinData pin in namesAlone)
            {
                RectTransform icon = pin.m_uiElement;
                GameObject name = pin.m_NamePinData?.PinNameGameObject;
                if (icon == null || !unseen.TryGetValue(pin, out Unseen hid) || !hid.Holds(pin))
                {
                    continue;   // the game has made its icon anew since: the next pass decides for it
                }
                bool nameShows = name != null && name.activeInHierarchy;
                if (!nameShows && icon.gameObject.activeSelf)
                {
                    icon.gameObject.SetActive(false);
                    iconsOff.Add(pin);   // the next pass, or RestoreAll, switches it on again
                    offForName.Add(pin);
                }
                else if (nameShows && !icon.gameObject.activeSelf && offForName.Remove(pin))
                {
                    icon.gameObject.SetActive(true);
                    iconsOff.Remove(pin);
                }
            }
            if (namesOff.Count == 0)
            {
                return;
            }
            Minimap.PinData under = Settings.NameUnderPointer.Value ? PinUnderPointer(map) : null;
            foreach (Minimap.PinData pin in namesOff)
            {
                GameObject name = pin.m_NamePinData?.PinNameGameObject;
                if (name == null)
                {
                    continue;
                }
                bool show = pin == under;
                if (name.activeSelf != show)
                {
                    name.SetActive(show);
                }
            }
        }

        /// <summary>
        /// Takes back everything this mod did to the map: the icons it switched off go back on, the icons it sized get
        /// their normal size, the pictures it made unseen are seen again, and the map draws its pins again, which sets every
        /// name as the game and Cartur's would.
        /// Also after an error, mid-pass included (both the last pass's sets and the unfinished one's): each pin is
        /// restored even if another fails.
        /// </summary>
        internal static void RestoreAll()
        {
            foreach (HashSet<Minimap.PinData> set in new[] { iconsOff, iconsOffNext })
            {
                foreach (Minimap.PinData pin in set)
                {
                    try
                    {
                        RectTransform icon = pin.m_uiElement;
                        if (icon != null && !icon.gameObject.activeSelf)
                        {
                            icon.gameObject.SetActive(true);
                        }
                    }
                    catch (System.Exception)
                    {
                        // One icon gone wrong must not keep the others off.
                    }
                }
            }
            foreach (Dictionary<Minimap.PinData, Shrunk> sizes in new[] { sized, sizedNext })
            {
                foreach (KeyValuePair<Minimap.PinData, Shrunk> each in sizes)
                {
                    try
                    {
                        RectTransform icon = each.Key.m_uiElement;
                        if (icon != null && drawnOn != null)
                        {
                            each.Value.Undo(each.Key, icon, BaseSize(drawnOn, each.Key));
                        }
                    }
                    catch (System.Exception)
                    {
                        // As above.
                    }
                }
            }
            foreach (Dictionary<Minimap.PinData, Unseen> hidden in new[] { unseen, unseenNext })
            {
                foreach (KeyValuePair<Minimap.PinData, Unseen> each in hidden)
                {
                    try
                    {
                        each.Value.Undo(each.Key);
                    }
                    catch (System.Exception)
                    {
                        // As above.
                    }
                }
            }
            iconsOff.Clear();
            iconsOffNext.Clear();
            sized.Clear();
            sizedNext.Clear();
            unseen.Clear();
            unseenNext.Clear();
            namesOff.Clear();
            namesAlone.Clear();
            offForName.Clear();
            pointerTargets.Clear();
            lastUnder = null;
            lastPointerPass = -1;
            if (drawnOn != null)
            {
                GameHooks.RequestPinUpdate(drawnOn);
            }
        }

        // A new map (a new world): its pins are new, and what was done to the old map's is gone with it.
        private static void Forget(Minimap map)
        {
            iconsOff.Clear();
            iconsOffNext.Clear();
            sized.Clear();
            sizedNext.Clear();
            unseen.Clear();
            unseenNext.Clear();
            namesOff.Clear();
            namesAlone.Clear();
            offForName.Clear();
            pointerTargets.Clear();
            lastUnder = null;
            lastPointerPass = -1;
            drawnOn = map;
            pinCanvas = null;
            toldSorted = false;
            Sorter.Forget();
            Record.Forget();
        }

        // A pin's size as the game gives it when it makes its icon: its map's pin size, twice that for a double-size pin.
        private static float BaseSize(Minimap map, Minimap.PinData pin)
        {
            float size = map.m_mode == Minimap.MapMode.Large ? map.m_pinSizeLarge : map.m_pinSizeSmall;
            return pin.m_doubleSize ? size * 2f : size;
        }

        // Sets an icon's width and height as the game sets its size, only if they differ: one change to its size when its
        // anchors are a point, as the game's pin icons' are, else the game's own two calls.
        private static void SetShape(RectTransform icon, float width, float height)
        {
            if (icon.anchorMin == icon.anchorMax)
            {
                Vector2 now = icon.sizeDelta;
                if (Mathf.Abs(now.x - width) > 0.01f || Mathf.Abs(now.y - height) > 0.01f)
                {
                    icon.sizeDelta = new Vector2(width, height);
                }
                return;
            }
            Rect rect = icon.rect;
            if (Mathf.Abs(rect.width - width) > 0.01f || Mathf.Abs(rect.height - height) > 0.01f)
            {
                icon.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                icon.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }

        /// <summary>
        /// An icon this mod has shrunk, and how. Where its pictures can keep their shape, it keeps its width and shrinks
        /// its height, and each picture that stretches with it keeps its shape (Image.preserveAspect), so it is drawn
        /// smaller while Cartur's merging still measures the game's width; Unity then draws it where a whole shrink would
        /// have (it places a shape-kept picture by its pivot, the icon's). Where they can't (a picture that isn't a Simple
        /// or Filled Image with a sprite, or a part placed off the icon's pivot line or under another part), the icon
        /// shrinks whole, as the game sizes it. A picture that isn't square is drawn at its own shape while shrunk (the
        /// game stretches it square): the lesser evil, since one icon shrunk whole makes Cartur's merging measure every
        /// pin on screen smaller. It remembers each picture's own shape setting, so exactly that is put back. The game
        /// makes a pin's icon anew whenever it comes into view or the map switches, and a new one starts as the game made it.
        /// </summary>
        private sealed class Shrunk
        {
            private static bool toldShape;
            private static bool toldWhole;

            private readonly Image icon;
            private readonly Image[] kept;
            private readonly bool[] keptBefore;
            private readonly bool whole;

            internal Shrunk(Minimap.PinData pin)
            {
                icon = pin.m_iconElement;
                var pictures = new List<Image>();
                whole = !CanKeepShape(icon, pictures, out string why);
                kept = pictures.ToArray();
                keptBefore = new bool[kept.Length];
                for (int i = 0; i < kept.Length; i++)
                {
                    keptBefore[i] = kept[i].preserveAspect;
                }
                if (!whole && !toldShape)
                {
                    toldShape = true;
                    Module.Log.LogInfo($"Shrinks pin icons by drawing their pictures smaller within their width, so Cartur's Map Pins' merging " +
                                       $"measures them as before: {Describe(icon)}.");
                }
                else if (whole && !toldWhole)
                {
                    toldWhole = true;
                    Module.Log.LogInfo($"Shrinks the icon of pins like \"{pin.m_name}\" whole, as the game sizes it, since {why}; while such a " +
                                       $"pin shows shrunk, Cartur's Map Pins' merging measures every pin on screen smaller. {Describe(icon)}.");
                }
            }

            /// <summary>Still the pin's own icon, not one the game has since replaced.</summary>
            internal bool Holds(Minimap.PinData pin) => icon != null && icon == pin.m_iconElement;

            /// <summary>Draws the icon smaller: as wide as the game made it and this high, its pictures keeping their
            /// shape; or, where they can't, this size all round.</summary>
            internal void Apply(RectTransform rect, float width, float height)
            {
                if (whole)
                {
                    SetShape(rect, height, height);
                    return;
                }
                SetShape(rect, width, height);
                foreach (Image picture in kept)
                {
                    if (picture != null && !picture.preserveAspect)
                    {
                        picture.preserveAspect = true;
                    }
                }
            }

            /// <summary>The icon's own size back, and its pictures' own shape settings, if it is still the icon this mod
            /// shrank.</summary>
            internal void Undo(Minimap.PinData pin, RectTransform rect, float size)
            {
                if (!Holds(pin))
                {
                    return;   // the game has made a new icon, at its own size
                }
                SetShape(rect, size, size);
                for (int i = 0; i < kept.Length; i++)
                {
                    if (kept[i] != null)
                    {
                        kept[i].preserveAspect = keptBefore[i];
                    }
                }
            }

            // Whether shrinking only the height draws the icon just as a whole shrink would: its own picture keeps its
            // shape, and every other part hangs from the icon or from a part already accepted, and either keeps its shape
            // too (stretched over its parent, with its pivot on the icon's pivot line) or doesn't follow the icon's width
            // at all (a fixed size on its parent's pivot line). Fills in the pictures to keep the shape of; none if not.
            private static bool CanKeepShape(Image icon, List<Image> pictures, out string why)
            {
                if (!KeepsShape(icon, out why))
                {
                    why = "its picture " + why;
                    return false;
                }
                pictures.Add(icon);
                float pivotLine = icon.rectTransform.pivot.x;
                var accepted = new HashSet<Transform> { icon.transform };
                foreach (Graphic part in icon.GetComponentsInChildren<Graphic>(true))   // parents come before their parts
                {
                    if (part == icon)
                    {
                        continue;
                    }
                    RectTransform rect = part.rectTransform;
                    var parent = rect.parent as RectTransform;
                    if (parent == null || !accepted.Contains(parent))
                    {
                        why = $"its part \"{part.name}\" hangs from \"{rect.parent?.name}\", which isn't checked";
                        pictures.Clear();
                        return false;
                    }
                    bool point = rect.anchorMin == rect.anchorMax;
                    if (point && Mathf.Approximately(rect.anchorMin.x, parent.pivot.x))
                    {
                        accepted.Add(rect);   // a fixed size on the pivot's line: it moves and sizes as with a whole shrink
                        continue;
                    }
                    bool stretched = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                                     rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
                    if (stretched && Mathf.Approximately(rect.pivot.x, pivotLine) && part is Image image && KeepsShape(image, out _))
                    {
                        pictures.Add(image);   // drawn by its own pivot, the icon's: where a whole shrink would put it
                        accepted.Add(rect);
                        continue;
                    }
                    why = $"its part \"{part.name}\" ({part.GetType().Name}) would lose its shape or its place";
                    pictures.Clear();
                    return false;
                }
                why = null;
                return true;
            }

            // A picture Unity draws keeping its shape: preserveAspect honours only Simple and Filled Images, with a sprite.
            private static bool KeepsShape(Image image, out string why)
            {
                if (image.type != Image.Type.Simple && image.type != Image.Type.Filled)
                {
                    why = $"is a {image.type} image";
                    return false;
                }
                if (image.sprite == null)
                {
                    why = "has no sprite";
                    return false;
                }
                why = null;
                return true;
            }

            // What the icon is made of, for the log: its picture, and every part under it with its placing.
            private static string Describe(Image icon)
            {
                var parts = new List<string>();
                foreach (Graphic part in icon.GetComponentsInChildren<Graphic>(true))
                {
                    RectTransform rect = part.rectTransform;
                    string kind = part is Image image ? $"{image.type} Image" : part.GetType().Name;
                    parts.Add($"{(part == icon ? "the icon" : $"\"{part.name}\"")}: {kind}, anchors {rect.anchorMin}-{rect.anchorMax}, pivot {rect.pivot}");
                }
                return string.Join("; ", parts);
            }
        }

        /// <summary>
        /// An icon this mod draws unseen, so its name shows alone: the icon's own Image and every Graphic under it (its tick
        /// mark) drawn fully transparent through its CanvasRenderer's alpha, which only this mod sets, remembering each
        /// one's own alpha to put back. Only while it is still the pin's icon: the game makes a pin's icon anew whenever it
        /// comes into view or the map switches.
        /// </summary>
        private sealed class Unseen
        {
            private readonly Image icon;
            private readonly Dictionary<CanvasRenderer, float> hidden = new Dictionary<CanvasRenderer, float>();

            internal Unseen(Minimap.PinData pin)
            {
                icon = pin.m_iconElement;
            }

            /// <summary>Still the pin's own icon, not one the game has since replaced.</summary>
            internal bool Holds(Minimap.PinData pin) => icon != null && icon == pin.m_iconElement;

            /// <summary>Draws every picture of the icon fully transparent (again, if something reset one).</summary>
            internal void Apply()
            {
                if (icon == null)
                {
                    return;
                }
                foreach (Graphic picture in icon.GetComponentsInChildren<Graphic>(true))
                {
                    CanvasRenderer renderer = picture.canvasRenderer;
                    if (renderer == null)
                    {
                        continue;
                    }
                    float alpha = renderer.GetAlpha();
                    if (alpha > 0f)
                    {
                        if (!hidden.ContainsKey(renderer))
                        {
                            hidden[renderer] = alpha;
                        }
                        renderer.SetAlpha(0f);
                    }
                }
            }

            /// <summary>Gives the pictures their own alpha back, if it is still the pin's icon.</summary>
            internal void Undo(Minimap.PinData pin)
            {
                if (Holds(pin))
                {
                    foreach (KeyValuePair<CanvasRenderer, float> each in hidden)
                    {
                        if (each.Key != null)
                        {
                            each.Key.SetAlpha(each.Value);
                        }
                    }
                }
                hidden.Clear();
            }
        }

        // The pin whose hidden name this mod would show: the nearest one with a drawn icon under the pointer. Worked out
        // again only when the pointer moved or the pins were drawn again.
        private static Minimap.PinData PinUnderPointer(Minimap map)
        {
            RectTransform root = map.m_pinRootLarge;
            if (root == null || pointerTargets.Count == 0)
            {
                return null;
            }
            if (pinCanvas == null)
            {
                pinCanvas = root.GetComponentInParent<Canvas>();
            }
            Canvas canvas = pinCanvas != null ? pinCanvas.rootCanvas : null;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3 pointer = ZInput.pointerPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, camera, out Vector2 local))
            {
                return null;
            }
            if (local == lastPointer && pass == lastPointerPass)
            {
                return lastUnder;
            }
            Minimap.PinData best = null;
            float bestDistance = float.MaxValue;
            foreach (PointerTarget target in pointerTargets)
            {
                float distance = (target.At - local).sqrMagnitude;
                if (distance <= target.Radius * target.Radius && distance < bestDistance)
                {
                    best = target.Pin;
                    bestDistance = distance;
                }
            }
            lastPointer = local;
            lastPointerPass = pass;
            lastUnder = best;
            return best;
        }

        private static void TellSorted(List<Minimap.PinData> pins, RecordIndex record)
        {
            var counts = new int[System.Enum.GetValues(typeof(Sorter.Source)).Length];
            foreach (Minimap.PinData pin in pins)
            {
                if (pin != null)
                {
                    counts[(int)Sorter.SourceOf(pin, record)]++;
                }
            }
            string portalPart = counts[(int)Sorter.Source.Portal] > 0 ? $"{counts[(int)Sorter.Source.Portal]} portals, " : string.Empty;
            string recordPart = Cartur.Installed
                ? $"{counts[(int)Sorter.Source.Record]} by Cartur's Map Pins' record, {counts[(int)Sorter.Source.Name]} by their name, " +
                  $"{counts[(int)Sorter.Source.Icon]} friends' by their icon, {counts[(int)Sorter.Source.Moving]} on boats, carts and tames, "
                : string.Empty;
            Module.Log.LogInfo($"Sorted the map's {pins.Count} pins: {recordPart}{counts[(int)Sorter.Source.HandPlaced]} placed by hand, {portalPart}" +
                               $"{counts[(int)Sorter.Source.GameType]} death markers and the game's markers, {counts[(int)Sorter.Source.Other]} other; " +
                               $"{counts[(int)Sorter.Source.Untouched]} players, pings and events are left as the game draws them.");
        }

        /// <summary>A pin whose name shows while the pointer is within Radius of its icon, At, in the pins' layer.</summary>
        private readonly struct PointerTarget
        {
            internal readonly Minimap.PinData Pin;
            internal readonly Vector2 At;
            internal readonly float Radius;

            internal PointerTarget(Minimap.PinData pin, Vector2 at, float radius)
            {
                Pin = pin;
                At = at;
                Radius = radius;
            }
        }
    }
}
