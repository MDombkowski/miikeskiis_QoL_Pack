using System;
using Outfitter.Loadouts;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// The grid in Outfitter's window: his inventory's shape (the rows he sees, the hotbar on top, then
    /// EquipmentAndQuickSlots' slots, a little apart), each cell with an icon, its count, its quality and a marker for
    /// what's worn. It shows either a loadout in its places (what isn't nearby in red) or his inventory as it is (while
    /// he chooses cells, picked cells in orange). Made once with room for the biggest inventory; cells not in his grid
    /// are hidden; the cells shrink when his inventory is taller than the room the window gives the grid (R1, finding 20).
    /// </summary>
    internal sealed class GridView
    {
        internal const float CellSize = 48f;
        internal const float GapShare = 4f / 48f;
        internal const int MaxColumns = 8;
        internal const int MaxRows = 9;
        internal const int SlotRows = 3;
        private const float SlotGap = 10f;

        private readonly CellView[,] cells = new CellView[MaxColumns, MaxRows + SlotRows];
        private readonly float left;
        private readonly float top;

        /// <summary>A cell was clicked (x, y as his inventory counts them).</summary>
        internal Action<Vector2i> Clicked;

        /// <summary>A cell was right-clicked.</summary>
        internal Action<Vector2i> RightClicked;

        /// <summary>The pointer is over a cell, or left it (null).</summary>
        internal Action<Vector2i?> Hovered;

        /// <summary>What dragging a cell drags (null: nothing: an empty cell, or the window not editing).</summary>
        internal Func<Vector2i, DragItem> DragFrom;

        /// <summary>The scroll wheel over a cell: one notch up is +1.</summary>
        internal Action<Vector2i, int> Scrolled;

        /// <summary>Whether its cells take a drag let go over them (a loadout's grid); his own inventory's grid beside the
        /// window (job 30, Session 3) only gives.</summary>
        private readonly bool takesDrops;

        internal GridView(Transform parent, float left, float top, bool takesDrops = true)
        {
            this.left = left;
            this.top = top;
            this.takesDrops = takesDrops;
            for (int x = 0; x < MaxColumns; x++)
            {
                for (int y = 0; y < MaxRows + SlotRows; y++)
                {
                    cells[x, y] = new CellView(parent, x, y, this);
                }
            }
        }

        /// <summary>How many rows of cells, and slot rows, a shape shows.</summary>
        private static void Rows(GridShape grid, out int rows, out int hidden)
        {
            rows = Mathf.Min(grid.Rows, MaxRows);
            hidden = grid.Slots.Count > 0 ? Mathf.Min(grid.Height - grid.Rows, SlotRows) : 0;
        }

        /// <summary>The cell size (with its gap) that fits this shape into <paramref name="room"/> pixels of height.</summary>
        internal static float StepFor(GridShape grid, float room)
        {
            Rows(grid, out int rows, out int hidden);
            float step = CellSize * (1f + GapShare);
            float needed = (rows + hidden) * step + (hidden > 0 ? SlotGap : 0f);
            if (needed > room && rows + hidden > 0)
            {
                step = Mathf.Max(20f, (room - (hidden > 0 ? SlotGap : 0f)) / (rows + hidden));
            }
            return step;
        }

        /// <summary>Lays the cells out for this shape inside <paramref name="room"/> pixels of height: the visible rows, then
        /// (with EquipmentAndQuickSlots) its active slots; everything else hidden. Clears every cell.</summary>
        internal void Shape(GridShape grid, float room)
        {
            Rows(grid, out int rows, out int _);
            float step = StepFor(grid, room);
            float size = step / (1f + GapShare);
            for (int x = 0; x < MaxColumns; x++)
            {
                for (int y = 0; y < MaxRows + SlotRows; y++)
                {
                    CellView cell = cells[x, y];
                    bool shown = x < grid.Width && grid.Usable(x, y) && (y < rows || (y >= grid.Rows && y - grid.Rows < SlotRows));
                    if (shown)
                    {
                        float py = y < grid.Rows ? top + y * step : top + rows * step + SlotGap + (y - grid.Rows) * step;
                        cell.Move(left + x * step, py, size);
                    }
                    cell.Show(shown);
                    cell.Clear();
                }
            }
        }

        /// <summary>Fills a cell. <paramref name="food"/>: a food slot (job 30, Session 3), its cell tinted green.</summary>
        internal void Set(int x, int y, Sprite icon, int count, int quality, bool worn, bool missing, bool picked, string slotLabel, bool food = false)
        {
            if (x < 0 || x >= MaxColumns || y < 0 || y >= MaxRows + SlotRows)
            {
                return;
            }
            cells[x, y].Set(icon, count, quality, worn, missing, picked, slotLabel, food);
        }

        internal void Label(int x, int y, string slotLabel)
        {
            if (x >= 0 && x < MaxColumns && y >= 0 && y < MaxRows + SlotRows)
            {
                cells[x, y].SetLabel(slotLabel);
            }
        }

        internal void Pick(int x, int y, bool picked)
        {
            if (x >= 0 && x < MaxColumns && y >= 0 && y < MaxRows + SlotRows)
            {
                cells[x, y].SetPicked(picked);
            }
        }

        /// <summary>Marks the one cell being edited (null: none), with a frame of its own.</summary>
        internal void Select(Vector2i? cell)
        {
            for (int x = 0; x < MaxColumns; x++)
            {
                for (int y = 0; y < MaxRows + SlotRows; y++)
                {
                    cells[x, y].SetSelected(cell.HasValue && cell.Value.x == x && cell.Value.y == y);
                }
            }
        }

        private sealed class CellView
        {
            private readonly GameObject root;
            private readonly Image background;
            private readonly Image icon;
            private readonly Text count;
            private readonly Text quality;
            private readonly Text label;
            private readonly Outline frame;
            private bool worn;
            private bool missing;
            private bool picked;
            private bool food;

            internal CellView(Transform parent, int x, int y, GridView owner)
            {
                root = Ui.Box($"Outfitter_Cell_{x}_{y}", parent, 0f, 0f, CellSize, CellSize, Ui.Cell);
                background = root.GetComponent<Image>();
                background.raycastTarget = true;
                label = Ui.Label(root.transform, string.Empty, 0f, 0f, CellSize, CellSize, 10, TextAnchor.MiddleCenter, Ui.Quiet);
                Stretch(label.gameObject, 2f, 2f, 2f, 2f);
                icon = Ui.Icon(root.transform, 0f, 0f, CellSize);
                Stretch(icon.gameObject, 4f, 4f, 4f, 4f);
                count = Ui.Label(root.transform, string.Empty, 0f, 0f, CellSize, 16f, 12, TextAnchor.LowerRight);
                Stretch(count.gameObject, 2f, 1f, 3f, 0f);
                quality = Ui.Label(root.transform, string.Empty, 0f, 0f, 20f, 16f, 12, TextAnchor.UpperLeft, Ui.Orange);
                Stretch(quality.gameObject, 3f, 0f, 2f, 1f);
                Clicks clicks = root.AddComponent<Clicks>();
                clicks.Lights = false;
                var cell = new Vector2i(x, y);
                clicks.OnLeft = () => owner.Clicked?.Invoke(cell);
                clicks.OnRight = () => owner.RightClicked?.Invoke(cell);
                clicks.OnEnter = () => owner.Hovered?.Invoke(cell);
                clicks.OnExit = () => owner.Hovered?.Invoke(null);
                // A loadout's cell is dragged to move its item, takes a drag let go over it, and its count follows the wheel.
                DragSource source = root.AddComponent<DragSource>();
                source.Make = () => owner.DragFrom?.Invoke(cell);
                if (owner.takesDrops)
                {
                    root.AddComponent<DropTarget>().Cell = cell;
                }
                root.AddComponent<WheelCatcher>().Turned = notches => owner.Scrolled?.Invoke(cell, notches);
                frame = root.AddComponent<Outline>();
                frame.effectColor = new Color(1f, 0.8f, 0.3f, 1f);
                frame.effectDistance = new Vector2(2f, -2f);
                frame.enabled = false;
                root.SetActive(false);
            }

            internal void SetSelected(bool selected)
            {
                if (frame.enabled != selected)
                {
                    frame.enabled = selected;
                }
            }

            // Fills the cell, inset by the given pixels (left, bottom, right, top), so it follows the cell's size.
            private static void Stretch(GameObject thing, float l, float b, float r, float t)
            {
                var rect = (RectTransform)thing.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(l, b);
                rect.offsetMax = new Vector2(-r, -t);
            }

            internal void Move(float x, float y, float size) => Ui.Place(root, x, y, size, size);

            internal void Show(bool shown)
            {
                if (root.activeSelf != shown)
                {
                    root.SetActive(shown);
                }
            }

            internal void Clear()
            {
                icon.enabled = false;
                icon.sprite = null;
                count.text = string.Empty;
                quality.text = string.Empty;
                label.text = string.Empty;
                worn = missing = picked = food = false;
                Paint();
            }

            internal void Set(Sprite sprite, int amount, int level, bool isWorn, bool isMissing, bool isPicked, string slotLabel, bool isFood = false)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = isMissing ? Ui.Missing : Color.white;
                count.text = amount > 1 ? amount.ToString() : string.Empty;
                quality.text = level > 1 ? level.ToString() : string.Empty;
                label.text = sprite == null ? slotLabel ?? string.Empty : string.Empty;
                worn = isWorn;
                missing = isMissing;
                picked = isPicked;
                food = isFood;
                Paint();
            }

            internal void SetLabel(string slotLabel)
            {
                if (!icon.enabled)
                {
                    label.text = slotLabel ?? string.Empty;
                }
            }

            internal void SetPicked(bool isPicked)
            {
                picked = isPicked;
                Paint();
            }

            private void Paint()
            {
                background.color = picked ? Ui.CellPicked : missing ? new Color(0.35f, 0.08f, 0.06f, 0.9f) : worn ? Ui.CellWorn : food ? Ui.CellFood : Ui.Cell;
            }
        }
    }
}
