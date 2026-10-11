using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XPortalMapPicker
{
    /// <summary>
    /// Every portal in XPortal's list as a pin on the big map, on the minimap, or on both, as the settings ask. XPortal
    /// keeps its list current on every computer (the server sends each portal that is built or renamed, and the whole
    /// list again after one is destroyed), so reading it once a second is enough for the pins to follow it live.
    ///
    /// The pins are the game's own, made the way the game makes its pins for the unique locations it reveals (the
    /// trader, the bosses' altars): PinType.None with a picture of their own, and never saved (m_save false). So they are
    /// never written to the map in the character's save, never handed on at a cartography table (both take only saved
    /// pins), and a right click can't delete them (the game deletes only saved pins). Their picture is the game's own
    /// white portal pin, the fifth of the pins a player can place (Icon4), as the picking map's markers are, so they look
    /// like every other pin (miikeskii's word, 2026-09-29: 0.5.0's full-colour build-menu icon didn't). Map View Rules
    /// knows a portal pin by exactly that: a pin of type None drawn with the game's portal pin picture.
    ///
    /// The game keeps one list of pins and draws it on whichever map is open, so a pin shows on only one of the two maps
    /// by being in the list only while that map is open: the pins are added when a map whose setting is on opens, and
    /// taken away when it closes. That needs no patch of the game. Its one cost: when the map changes, the game may draw
    /// the pins on the other map for a single frame, if it redraws its pins before this runs.
    ///
    /// On the big map, the name of the portal pin under the pointer (or the gamepad's crosshair, as the game reads it)
    /// shows at any zoom, where Cartur's Map Pins (its name zoom, or crowded names), Map View Rules or the names setting
    /// would leave it unnamed: a name of the game's own look, made from the game's name prefab, which only this mod
    /// shows and hides. Only a pin whose picture is drawn counts: Cartur's hides a merged or far pin by switching its
    /// picture off (Image.enabled, its PinFade), not its icon.
    /// </summary>
    internal static class PortalPins
    {
        // How often XPortal's list is read while pins show.
        private const float PollSeconds = 1f;

        /// <summary>One portal's pin, and what it was made from, so a change to the portal is noticed.</summary>
        private sealed class Shown
        {
            internal Minimap.PinData Pin;
            internal string Name;       // as the pin carries it: empty while names are off
            internal string Title;      // the portal's name, for the name under the pointer
            internal Vector3 Location;
        }

        private static readonly Dictionary<ZDOID, Shown> shown = new Dictionary<ZDOID, Shown>();

        private static readonly HashSet<Minimap.PinData> onMap = new HashSet<Minimap.PinData>();
        private static readonly HashSet<ZDOID> listed = new HashSet<ZDOID>();
        private static readonly List<ZDOID> gone = new List<ZDOID>();

        // The world's map the pins were added to. A new world brings a new map, and the old one takes its pins with it.
        private static Minimap map;

        private static bool syncNow = true;
        private static float nextSync;
        private static bool namesApplied;
        private static bool logged;
        private static bool warnedNoIcon;
        private static bool warnedNoHover;

        // The name under the pointer: one object, made once per map, shown for one pin at a time.
        private static GameObject hover;
        private static TMP_Text hoverText;
        private static Shown hovered;

        // The camera the big map's pins are drawn with (null for an overlay canvas), found once per map.
        private static Camera pinCamera;
        private static bool pinCameraFound;

        /// <summary>Runs every frame, also while the mod is switched off: adds, changes and takes away the pins so they
        /// match XPortal's list, the settings and the map that is open.</summary>
        internal static void Tick()
        {
            Minimap current = Minimap.instance;
            if (!ReferenceEquals(current, map))
            {
                // Another world, or none. The pins, if any, went with the old map, which may be destroyed already, so
                // it is left alone; so did the name under the pointer, which hung under its pins' names.
                shown.Clear();
                hover = null;
                hoverText = null;
                hovered = null;
                pinCameraFound = false;
                logged = false;
                syncNow = true;
                map = current;
            }

            if (!Wanted(current))
            {
                Remove();
                return;
            }
            if (syncNow || Time.time >= nextSync || namesApplied != Settings.PortalNamesOnLargeMap.Value)
            {
                Sync(current);
            }
        }

        /// <summary>
        /// After every frame (the module's LateUpdate), once the map has drawn its pins and every other mod has touched
        /// their names: on the big map, shows the name of the portal pin under the pointer, unless its own name already
        /// shows there.
        /// </summary>
        internal static void AfterFrame()
        {
            Minimap current = map;
            if (current == null || shown.Count == 0 || !Settings.PortalNamesOnHover.Value ||
                current.m_mode != Minimap.MapMode.Large)
            {
                HideHover();
                return;
            }
            Shown over = PinUnderPointer(current);
            if (over == null || NameShows(over.Pin))
            {
                HideHover();
                return;
            }
            ShowHover(current, over);
        }

        /// <summary>Takes every portal pin off the map: when they are not wanted, when the mod is switched off, and
        /// after an error. Safe to call at any time.</summary>
        internal static void Remove()
        {
            if (shown.Count > 0 && map != null)
            {
                foreach (Shown pin in shown.Values)
                {
                    map.RemovePin(pin.Pin);
                }
            }
            shown.Clear();
            HideHover();
            syncNow = true;   // so they come back at once when they are wanted again
        }

        // Whether the pins belong on the map that is open right now.
        private static bool Wanted(Minimap current)
        {
            if (!Settings.Enabled.Value || current == null || Player.m_localPlayer == null)
            {
                return false;
            }

            // While picking, the mod's own markers stand exactly where these pins would, and a pin could take the click
            // meant for a marker. The clean map switches the pin layers off anyway, but "Clean map while picking" can be
            // off, so the pins step aside while picking either way, and come back when picking ends.
            if (PickMode.IsPicking)
            {
                return false;
            }

            // A world without a map keeps the map mode at None, so no pins are added there either.
            bool onThisMap = current.m_mode == Minimap.MapMode.Large ? Settings.PortalsOnLargeMap.Value
                           : current.m_mode == Minimap.MapMode.Small && Settings.PortalsOnMinimap.Value;

            // XPortal's own rule for its Ping button, which the Map button follows too. Read once a second, not every frame:
            // it is three reflection reads.
            if (onThisMap && Time.time >= nextPingCheck)
            {
                nextPingCheck = Time.time + PollSeconds;
                pingOff = XPortalHooks.ServerTurnedOffMapPing();
            }
            return onThisMap && !pingOff;
        }

        // The server's PingMapDisabled as last read, and when to read it again.
        private static bool pingOff;
        private static float nextPingCheck;

        // A portal's name as a pin shows it: the game draws a pin's name as rich text and translates its $words, and it
        // strips both from the names players type for their own pins; a portal's name is typed by any player.
        private static string AsPinName(string name) => name.Replace('$', ' ').Replace('<', ' ');

        // Makes the pins match XPortal's list: a pin for each new portal, a new pin for a renamed one or one whose pin
        // is no longer on the map, and none for a portal that is gone.
        private static void Sync(Minimap current)
        {
            syncNow = false;
            nextSync = Time.time + PollSeconds;
            namesApplied = Settings.PortalNamesOnLargeMap.Value;

            Sprite icon = PortalIcon(current);
            if (icon == null)
            {
                // Without the portal pin's picture a pin would be drawn as a plain white square, and Map View Rules
                // wouldn't know it for a portal, so no pins are shown at all.
                if (!warnedNoIcon)
                {
                    warnedNoIcon = true;
                    Module.Log.LogWarning("The game's portal pin (Icon4) has no picture, so the portals aren't shown as pins " +
                                          "on the map. The game may have changed.");
                }
                Remove();
                syncNow = false;   // tried again at the next poll, not every frame
                return;
            }

            // The pins the game has now. Another mod can take a pin off the map; it is put back here.
            onMap.Clear();
            List<Minimap.PinData> pins = GameHooks.Pins(current);
            if (pins != null)
            {
                onMap.UnionWith(pins);
            }

            listed.Clear();
            foreach (XPortalHooks.Portal portal in XPortalHooks.KnownPortals())
            {
                listed.Add(portal.Id);
                string title = string.IsNullOrEmpty(portal.Name) ? Localization.instance.Localize("$piece_portal_tag_none")
                             : AsPinName(portal.Name);
                string name = namesApplied ? title : string.Empty;

                if (shown.TryGetValue(portal.Id, out Shown pin))
                {
                    if (pin.Name == name && pin.Location == portal.Location && (pins == null || onMap.Contains(pin.Pin)))
                    {
                        pin.Title = title;
                        continue;
                    }

                    // The game makes a pin's name when the pin is added, so a changed pin is replaced rather than edited.
                    current.RemovePin(pin.Pin);
                    if (ReferenceEquals(hovered, pin))
                    {
                        HideHover();
                    }
                }
                shown[portal.Id] = new Shown
                {
                    Pin = AddPin(current, portal.Location, name, icon),
                    Name = name,
                    Title = title,
                    Location = portal.Location,
                };
            }

            gone.Clear();
            foreach (KeyValuePair<ZDOID, Shown> pin in shown)
            {
                if (!listed.Contains(pin.Key))
                {
                    current.RemovePin(pin.Value.Pin);
                    gone.Add(pin.Key);
                    if (ReferenceEquals(hovered, pin.Value))
                    {
                        HideHover();
                    }
                }
            }
            foreach (ZDOID id in gone)
            {
                shown.Remove(id);
            }
            onMap.Clear();

            if (!logged && shown.Count > 0)
            {
                logged = true;
                Module.Log.LogInfo($"Showing the {shown.Count} portals in XPortal's list as pins on the map");
            }
        }

        // A pin the way the game adds its unique locations' pins: no type of its own, its own picture, never saved. The
        // game gives a pin of type None no picture, and draws the one set here the next time it draws its pins. An empty
        // name makes no name under the pin.
        private static Minimap.PinData AddPin(Minimap current, Vector3 location, string name, Sprite icon)
        {
            Minimap.PinData pin = current.AddPin(location, Minimap.PinType.None, name, save: false, isChecked: false);
            pin.m_icon = icon;
            return pin;
        }

        // The game's own white portal pin: the picture of Icon4, the fifth of the pins a player can place, the same one
        // the picking map's markers use (MarkerLayer).
        private static Sprite PortalIcon(Minimap current)
        {
            foreach (Minimap.SpriteData icon in current.m_icons)
            {
                if (icon.m_name == Minimap.PinType.Icon4)
                {
                    return icon.m_icon;
                }
            }
            return null;
        }

        // The portal pin whose drawn picture the pointer is over, the nearest if several; null if none. The pointer is the
        // mouse, or with a gamepad the crosshair in the middle of the screen, as the game reads it on the big map.
        private static Shown PinUnderPointer(Minimap current)
        {
            Vector3 pointer = ZInput.IsMouseActive() || ZInput.IsTouchPressedDown()
                ? ZInput.pointerPosition
                : new Vector3(Screen.width / 2f, Screen.height / 2f);
            Camera camera = PinCamera(current);
            Shown best = null;
            float bestDistance = float.MaxValue;
            foreach (Shown each in shown.Values)
            {
                RectTransform icon = each.Pin?.m_uiElement;
                if (icon == null || !icon.gameObject.activeInHierarchy || each.Pin.m_iconElement == null || !each.Pin.m_iconElement.enabled)
                {
                    continue;   // not drawn now: off screen, hidden by Map View Rules, or merged or faded out by Cartur's
                }
                if (!RectTransformUtility.RectangleContainsScreenPoint(icon, pointer, camera))
                {
                    continue;
                }
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, icon.position);
                float distance = ((Vector2)pointer - centre).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = each;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // The camera of the canvas the big map's pins are on: null for an overlay canvas (the game's), as Unity's pointer
        // tests take it.
        private static Camera PinCamera(Minimap current)
        {
            if (!pinCameraFound && current.m_pinRootLarge != null)
            {
                Canvas canvas = current.m_pinRootLarge.GetComponentInParent<Canvas>();
                Canvas root = canvas != null ? canvas.rootCanvas : null;
                pinCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
                pinCameraFound = root != null;
            }
            return pinCamera;
        }

        // Whether the pin's own name shows now (its names setting on, zoomed in far enough, and not hidden by another mod).
        private static bool NameShows(Minimap.PinData pin)
        {
            GameObject name = pin.m_NamePinData?.PinNameGameObject;
            return name != null && name.activeInHierarchy;
        }

        // Shows the portal's name where the game puts a pin's name, in the game's own look.
        private static void ShowHover(Minimap current, Shown over)
        {
            if (hover == null && !MakeHover(current))
            {
                return;
            }
            if (!ReferenceEquals(hovered, over) || hoverText.text != over.Title)
            {
                hoverText.text = over.Title;
                hovered = over;
            }
            var box = (RectTransform)hover.transform;
            Vector2 at = over.Pin.m_uiElement.anchoredPosition;   // the game places a pin's name as it places its icon
            if (box.anchoredPosition != at)
            {
                box.anchoredPosition = at;
            }
            if (!hover.activeSelf)
            {
                hover.SetActive(true);
                box.SetAsLastSibling();   // over the other names
            }
        }

        private static void HideHover()
        {
            if (hover != null && hover.activeSelf)
            {
                hover.SetActive(false);
            }
            hovered = null;
        }

        // The name object, made from the game's own pin name prefab under the big map's names; false if the game no
        // longer has what it needs (said once).
        private static bool MakeHover(Minimap current)
        {
            GameObject prefab = GameHooks.PinNamePrefab(current);
            RectTransform root = current.m_pinNameRootLarge;
            if (prefab == null || root == null)
            {
                if (!warnedNoHover)
                {
                    warnedNoHover = true;
                    Module.Log.LogWarning("The game's pin name prefab wasn't found, so a portal's name doesn't show under the " +
                                          "pointer. The game may have changed.");
                }
                return false;
            }
            hover = Object.Instantiate(prefab, root);
            hover.name = "XPortalMapPicker_PortalHoverName";
            hoverText = hover.GetComponentInChildren<TMP_Text>(true);
            if (hoverText == null)
            {
                Object.Destroy(hover);
                hover = null;
                return false;
            }
            hoverText.color = Color.white;
            hover.SetActive(false);
            return true;
        }

        /// <summary>Takes the name under the pointer away for good (after an error, or when the mod is removed).</summary>
        internal static void RemoveHover()
        {
            if (hover != null)
            {
                Object.Destroy(hover);
            }
            hover = null;
            hoverText = null;
            hovered = null;
        }
    }
}
