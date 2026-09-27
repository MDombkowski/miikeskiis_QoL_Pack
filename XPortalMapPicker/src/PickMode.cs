using System.Collections.Generic;
using UnityEngine;

namespace XPortalMapPicker
{
    /// <summary>
    /// Pick mode. XPortal's panel steps aside and the big map opens with a marker on every portal the panel's
    /// destination list offers, on an otherwise clean map. Clicking a marker chooses that portal; the panel then
    /// comes back with it selected, and the player presses OK as usual. Right click, Esc or the map key cancel.
    /// However picking ends, Stop() takes the markers away, brings the map's pins back and restores the zoom.
    /// </summary>
    internal static class PickMode
    {
        private enum Stage
        {
            Idle,
            Picking,

            // The map has closed; XPortal's panel comes back at returnFrame.
            Returning,
        }

        // Room left around the outermost portals when the map zooms out to show them all.
        private const float FitMargin = 1.25f;

        private static Stage stage;
        private static float zoomBefore;

        // Clicks are noted here and acted on at the next Tick, outside the game's and Unity's click handling.
        private static ZDOID? clickedPortal;
        private static bool cancelClicked;

        private static ZDOID? chosenPortal;
        private static int returnFrame;

        internal static bool IsPicking => stage == Stage.Picking;

        /// <summary>The Map button was clicked.</summary>
        internal static void Start()
        {
            Minimap map = Minimap.instance;
            Player player = Player.m_localPlayer;
            if (stage != Stage.Idle || !Settings.Enabled.Value || map == null || player == null || player.IsDead() || Game.m_noMap)
            {
                return;
            }

            object panel = XPortalHooks.Panel;
            List<XPortalHooks.Portal> portals = XPortalHooks.ChoosablePortals(panel);
            if (portals.Count == 0)
            {
                Notify("There are no other portals to choose from.");
                return;
            }
            List<XPortalHooks.Portal> visible = Settings.ShowPortalsInUnexploredAreas.Value
                ? portals
                : portals.FindAll(portal => GameHooks.IsExplored(map, portal.Location));
            if (visible.Count == 0)
            {
                Notify("All the other portals stand where your map is still dark. The setting \"Show portals in unexplored areas\" shows them.");
                return;
            }

            // The box around the portals that will show, and the player, who stands at the portal being set up.
            Vector3 min = player.transform.position;
            Vector3 max = min;
            foreach (XPortalHooks.Portal portal in visible)
            {
                min = Vector3.Min(min, portal.Location);
                max = Vector3.Max(max, portal.Location);
            }

            zoomBefore = map.LargeZoom;
            bool fit = Settings.ZoomToFitPortals.Value;
            if (fit)
            {
                Vector3 centre = (min + max) * 0.5f;
                centre.y = player.transform.position.y;
                map.ShowPointOnMap(centre);   // opens the big map, centred there
            }
            else
            {
                map.SetMapMode(Minimap.MapMode.Large);   // the usual way: centred on the player
            }
            if (map.m_mode != Minimap.MapMode.Large)
            {
                return;   // the game kept the big map shut; nothing has changed
            }

            // From here on, Stop() undoes everything.
            stage = Stage.Picking;
            clickedPortal = null;
            cancelClicked = false;
            if (fit)
            {
                map.LargeZoom = Mathf.Max(zoomBefore, ZoomToFit(map, max - min));
            }
            PlayerLayer.Create(map);   // first, so the portal markers are drawn over the players
            MarkerLayer.Create(map, portals);
            if (Settings.CleanMapWhilePicking.Value)
            {
                CleanMap.Apply(map);
            }

            // Only now that the big map is open: hiding the panel releases XPortal's input block, which the map needs to
            // take clicks and keys, and with the map already open the game leaves the mouse pointer free.
            XPortalHooks.SetPanelActive(panel, false);
            Module.Log.LogDebug($"Picking a destination among {portals.Count} portals");
        }

        /// <summary>A portal marker was clicked.</summary>
        internal static void OnMarkerClicked(ZDOID portalId)
        {
            if (stage == Stage.Picking && !clickedPortal.HasValue && !cancelClicked)
            {
                clickedPortal = portalId;
            }
        }

        /// <summary>A right click on the big map, or on a marker, while picking.</summary>
        internal static void OnRightClick()
        {
            if (stage == Stage.Picking)
            {
                cancelClicked = true;
            }
        }

        /// <summary>Runs every frame.</summary>
        internal static void Tick()
        {
            if (stage == Stage.Picking)
            {
                WatchPicking();
            }
            else if (stage == Stage.Returning && Time.frameCount >= returnFrame)
            {
                ReturnToPanel();
            }
        }

        /// <summary>Runs every frame after all the Update calls, once the map has moved for this frame.</summary>
        internal static void PlaceMarkers()
        {
            Minimap map = Minimap.instance;
            if (stage == Stage.Picking && map != null)
            {
                PlayerLayer.Refresh(map);
                MarkerLayer.Refresh(map);
            }
        }

        /// <summary>Logout or quit.</summary>
        internal static void OnWorldClosing()
        {
            if (stage == Stage.Picking)
            {
                Stop(returnToPanel: false);
            }
            stage = Stage.Idle;
        }

        /// <summary>After an error: take the markers away, bring the pins back, close the map and give XPortal its
        /// panel back, as far as that still works.</summary>
        internal static void Abandon()
        {
            if (stage == Stage.Idle)
            {
                return;
            }
            try
            {
                if (stage == Stage.Picking)
                {
                    Stop(returnToPanel: false);
                }
            }
            finally
            {
                stage = Stage.Idle;
            }
            ShowPanel(XPortalHooks.Panel);
        }

        private static void WatchPicking()
        {
            Minimap map = Minimap.instance;
            if (map == null)
            {
                // The world was left without Game.Shutdown being seen: the map, the markers and the pin layers are gone.
                MarkerLayer.Remove();
                PlayerLayer.Remove();
                CleanMap.Restore();
                stage = Stage.Idle;
                return;
            }

            GameObject panel = XPortalHooks.MainPanel(XPortalHooks.Panel);
            if (panel == null || panel.activeSelf)
            {
                // The panel was destroyed, or XPortal opened it again by itself: there is nothing to go back to.
                Stop(returnToPanel: false);
            }
            else if (!Settings.Enabled.Value)
            {
                // Switched off in the settings: give the panel back unchanged.
                Stop(returnToPanel: true);
            }
            else if (clickedPortal.HasValue)
            {
                Stop(returnToPanel: true, clickedPortal);
            }
            else if (cancelClicked || map.m_mode != Minimap.MapMode.Large)
            {
                // A right click, or the map was closed another way: Esc, the map key, or dying.
                Stop(returnToPanel: true);
            }
            else if (Settings.CleanMapWhilePicking.Value)
            {
                CleanMap.Apply(map);
            }
            else
            {
                CleanMap.Restore();
            }
        }

        private static void Stop(bool returnToPanel, ZDOID? chosen = null)
        {
            MarkerLayer.Remove();
            PlayerLayer.Remove();
            CleanMap.Restore();
            Minimap map = Minimap.instance;
            if (map != null)
            {
                map.LargeZoom = zoomBefore;
                if (map.m_mode == Minimap.MapMode.Large)
                {
                    map.SetMapMode(Minimap.MapMode.Small);
                }
            }
            clickedPortal = null;
            cancelClicked = false;
            chosenPortal = chosen;
            stage = returnToPanel ? Stage.Returning : Stage.Idle;

            // The panel comes back a frame later: XPortal's Cancel button answers to Esc as soon as the panel shows,
            // and the Esc press that closed the map would otherwise cancel the panel too.
            returnFrame = Time.frameCount + 1;
            Module.Log.LogDebug(chosen.HasValue ? $"Picked portal {chosen.Value}" : "Stopped picking without a choice");
        }

        private static void ReturnToPanel()
        {
            stage = Stage.Idle;
            ZDOID? chosen = chosenPortal;
            chosenPortal = null;

            object panel = XPortalHooks.Panel;
            if (ShowPanel(panel) && chosen.HasValue && !XPortalHooks.SelectDestination(panel, chosen.Value))
            {
                Notify("That portal is no longer in XPortal's list, so the destination is unchanged.");
            }
        }

        // Shows XPortal's panel again, unless it is gone or already showing, or the player is dead or gone.
        private static bool ShowPanel(object panel)
        {
            Player player = Player.m_localPlayer;
            GameObject panelObject = XPortalHooks.MainPanel(panel);
            if (player == null || player.IsDead() || panelObject == null || panelObject.activeSelf)
            {
                return false;
            }
            XPortalHooks.SetPanelActive(panel, true);
            return true;
        }

        // The big map shows LargeZoom of the map texture's height, and the texture spans m_textureSize × m_pixelSize
        // metres, so this is the zoom at which a box of the given size just fits, with a margin.
        private static float ZoomToFit(Minimap map, Vector3 span)
        {
            Rect view = map.m_mapImageLarge.rectTransform.rect;
            float mapMetres = map.m_textureSize * map.m_pixelSize;
            if (view.width <= 0f || view.height <= 0f || mapMetres <= 0f)
            {
                return 0f;
            }
            return Mathf.Max(span.z, span.x * view.height / view.width) * FitMargin / mapMetres;
        }

        private static void Notify(string text)
        {
            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
            }
        }
    }
}
