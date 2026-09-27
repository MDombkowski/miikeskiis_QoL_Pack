using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// One portal's marker on the big map while picking: the portal picture, which is a button (a left click chooses
    /// the portal), with the portal's name below it. This component follows the mouse over the marker, for the name's
    /// highlight and the names-on-hover setting, and makes a right click on the marker cancel, as it does anywhere
    /// else on the map.
    /// </summary>
    internal sealed class PortalMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        internal ZDOID PortalId { get; private set; }

        internal Vector3 Location { get; private set; }

        // The portal's place on the map texture. The portal never moves, so this is worked out once.
        internal float MapX { get; private set; }

        internal float MapY { get; private set; }

        internal Text Label { get; private set; }

        internal bool PointerOver { get; private set; }

        internal void Setup(ZDOID portalId, Vector3 location, float mapX, float mapY, Text label)
        {
            PortalId = portalId;
            Location = location;
            MapX = mapX;
            MapY = mapY;
            Label = label;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetPointerOver(true);

        public void OnPointerExit(PointerEventData eventData) => SetPointerOver(false);

        // The button beside this component handles the left click.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                PickMode.OnRightClick();
            }
        }

        // A marker that is hidden while under the mouse (panned off the map) never hears the mouse leave.
        private void OnDisable() => SetPointerOver(false);

        private void SetPointerOver(bool over)
        {
            PointerOver = over;
            if (Label != null)
            {
                Label.color = over ? GUIManager.Instance.ValheimOrange : Color.white;
            }
        }
    }
}
