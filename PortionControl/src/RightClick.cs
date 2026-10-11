using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PortionControl
{
    /// <summary>
    /// A right-click on one of this mod's buttons (a Unity button answers the left button only): the split window's
    /// amounts split the stack into stacks of them, and the chest window's Max weight takes its right click's way. Only
    /// while the button can be clicked. The work runs inside the module's Guard (the caller's action).
    /// </summary>
    internal sealed class RightClick : MonoBehaviour, IPointerClickHandler
    {
        internal Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right || Clicked == null)
            {
                return;
            }
            Button button = GetComponent<Button>();
            if (button != null && (!button.IsInteractable() || !button.gameObject.activeInHierarchy))
            {
                return;
            }
            Clicked();
        }

        /// <summary>Gives a button a right-click.</summary>
        internal static void On(Button button, Action clicked)
        {
            if (button == null)
            {
                return;
            }
            RightClick handler = button.GetComponent<RightClick>();
            if (handler == null)   // Unity's own test
            {
                handler = button.gameObject.AddComponent<RightClick>();
            }
            handler.Clicked = clicked;
        }
    }
}
