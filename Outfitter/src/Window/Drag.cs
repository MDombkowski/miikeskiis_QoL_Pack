using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outfitter.Window
{
    /// <summary>
    /// What is being dragged in Outfitter's window: an item from the drawer (a kind found in the chests nearby, or one he
    /// carries), or one of the loadout's items from its cell. Only a description: dragging never moves a real item.
    /// </summary>
    internal sealed class DragItem
    {
        /// <summary>The item's prefab name (the loadout's key for it).</summary>
        internal string Prefab;

        internal int Quality = 1;

        /// <summary>Its style (the game's m_variant).</summary>
        internal int Variant;

        /// <summary>From the drawer: how many the chests and he hold of it (shown, and the most a stack dragged in may
        /// take with "what the chests hold"). From a cell: the cell's count.</summary>
        internal int Count = 1;

        /// <summary>A copy found (its data, maker and style), when the drawer has one; may be null.</summary>
        internal ItemDrop.ItemData Sample;

        /// <summary>The loadout cell it's dragged from (x, y as his inventory counts them), or null from the drawer.</summary>
        internal Vector2i? FromCell;

        /// <summary>From his own inventory beside the window (job 30, Session 3): that very item as Save takes it (its
        /// maker and other mods' data, worn as he wears it), put in as it is; null from the drawer or a loadout's cell.</summary>
        internal Loadouts.LoadoutItem Exact;

        internal Sprite Icon;
    }

    /// <summary>
    /// Dragging in Outfitter's window (job 30, Session 2): one drag at a time, its picture following the pointer on
    /// Outfitter's canvas. A source makes the item (<see cref="DragSource"/>); a loadout cell takes it (the window's
    /// <see cref="DroppedOnCell"/>); a drag let go anywhere else calls <see cref="DroppedElsewhere"/> (a cell dragged out
    /// of the grid: taken out of the loadout). Clicks on a drawer item go to <see cref="Clicked"/>.
    /// </summary>
    internal static class Drag
    {
        private static GameObject ghost;
        private static Image ghostImage;
        private static bool dropped;

        /// <summary>What is being dragged now, or null.</summary>
        internal static DragItem Current { get; private set; }

        /// <summary>A drag let go on a loadout cell (x, y as his inventory counts them). Set by the window.</summary>
        internal static Action<DragItem, Vector2i> DroppedOnCell;

        /// <summary>A drag let go anywhere but a loadout cell. Set by the window.</summary>
        internal static Action<DragItem> DroppedElsewhere;

        /// <summary>A drawer item clicked (left: <paramref name="right"/> false). Set by the window.</summary>
        internal static Action<DragItem, bool> Clicked;

        internal static void Begin(DragItem item, PointerEventData data)
        {
            Cancel();
            if (item == null)
            {
                return;
            }
            Current = item;
            dropped = false;
            if (ghost == null)
            {
                ghost = Ui.Box("Outfitter_Drag", Ui.CanvasRoot(), 0f, 0f, 48f, 48f);
                ghostImage = ghost.AddComponent<Image>();
                ghostImage.preserveAspect = true;
                ghostImage.raycastTarget = false;
                CanvasGroup group = ghost.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            ghostImage.sprite = item.Icon;
            ghostImage.enabled = item.Icon != null;
            ghost.SetActive(true);
            ghost.transform.SetAsLastSibling();
            Move(data);
        }

        internal static void Move(PointerEventData data)
        {
            if (ghost == null || Current == null || data == null)
            {
                return;
            }
            var canvasRect = (RectTransform)Ui.CanvasRoot();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, data.position, null, out Vector2 local))
            {
                var rect = (RectTransform)ghost.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = local;
            }
        }

        /// <summary>A loadout cell's drop: called by the cell under the pointer as the drag is let go (before the source's
        /// end).</summary>
        internal static void DropOn(Vector2i cell)
        {
            DragItem item = Current;
            if (item == null)
            {
                return;
            }
            dropped = true;
            Module.Guard("dropping an item on a loadout's cell", () => DroppedOnCell?.Invoke(item, cell));
        }

        /// <summary>The source's end of the drag: let go anywhere but a cell, it's dropped elsewhere.</summary>
        internal static void End()
        {
            DragItem item = Current;
            bool onCell = dropped;
            Cancel();
            if (item != null && !onCell)
            {
                Module.Guard("dropping an item outside the loadout", () => DroppedElsewhere?.Invoke(item));
            }
        }

        /// <summary>Ends a drag without a drop (the window closed, an undo step).</summary>
        internal static void Cancel()
        {
            Current = null;
            dropped = false;
            if (ghost != null)
            {
                ghost.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Makes a UI element a drag source: <see cref="Make"/> says what it drags (null: nothing, the drag doesn't start).
    /// Each step runs inside the module's guard, so an error switches Outfitter off rather than reaching Unity's events
    /// (review round 1 of Session 2, n5).
    /// </summary>
    internal sealed class DragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal Func<DragItem> Make;

        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || Make == null)
            {
                return;
            }
            Module.Guard("starting a drag", () =>
            {
                DragItem item = Make();
                if (item != null)
                {
                    Drag.Begin(item, data);
                }
            });
        }

        public void OnDrag(PointerEventData data)
        {
            if (Drag.Current != null)
            {
                Module.Guard("dragging", () => Drag.Move(data));
            }
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (Drag.Current != null)
            {
                Module.Guard("ending a drag", Drag.End);
            }
        }

        private void OnDisable()
        {
            if (Drag.Current != null)
            {
                Module.Guard("ending a drag", Drag.Cancel);
            }
        }
    }

    /// <summary>Makes a UI element a drop target for loadout cells.</summary>
    internal sealed class DropTarget : MonoBehaviour, IDropHandler
    {
        internal Vector2i Cell;

        public void OnDrop(PointerEventData data)
        {
            if (Drag.Current != null)
            {
                Drag.DropOn(Cell);
            }
        }
    }

    /// <summary>The scroll wheel over a UI element, as notches (+1 up, -1 down).</summary>
    internal sealed class WheelCatcher : MonoBehaviour, IScrollHandler
    {
        internal Action<int> Turned;

        public void OnScroll(PointerEventData data)
        {
            int notches = data.scrollDelta.y > 0f ? 1 : data.scrollDelta.y < 0f ? -1 : 0;
            if (notches != 0 && Turned != null)
            {
                Module.Guard("turning the wheel over a loadout's cell", () => Turned(notches));
            }
        }
    }
}
