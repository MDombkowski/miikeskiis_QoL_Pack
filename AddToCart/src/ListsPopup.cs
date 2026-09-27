using System;
using Jotunn.Managers;
using QoLMods.Catalogue;
using UnityEngine;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>
    /// His favorites and lists, the game's gesture: a middle-click on a thumbnail stars it, then opens a small checklist
    /// of his lists, with Add List and Remove from Favorites (the game's own build menu does the same). The list on the
    /// left's "Add List…" and a right-click on one of his lists (to rename it) open the same small window. His lists are
    /// the game's favorite categories (QoLMods.Catalogue.GameLists); deleting one is left to the game's build menu, which
    /// asks first.
    /// </summary>
    internal static class ListsPopup
    {
        private const float Width = 300f;
        private const float RowHeight = 26f;
        private const int MostRows = 8;

        private enum Mode
        {
            Favorite,
            AddList,
            Rename,
        }

        private static RectTransform window;
        private static GameObject blocker;
        private static Mode mode;
        private static Entry entry;
        private static int renaming = -1;
        private static InputField nameField;
        private static Text errorText;
        private static string typed = string.Empty;
        private static bool submitNextFrame;
        private static Vector2 at;

        internal static bool IsOpen => blocker != null && blocker.activeSelf;

        /// <summary>The checklist's scroll content while it is open (for the mouse wheel), else null.</summary>
        internal static RectTransform Checklist => IsOpen ? checklist : null;

        private static RectTransform checklist;

        /// <summary>The window it opens over, once built (a new window after a change of scene).</summary>
        internal static void Built(RectTransform root)
        {
            window = root;
            blocker = null;
        }

        /// <summary>The middle-click: stars it (unless it is starred already) and opens the checklist of his lists.</summary>
        internal static void Open(Entry thumbnail)
        {
            FavoritePieceList store = Favorites.Store;
            if (store == null)
            {
                CartWindow.Status("Favorites work once your character is in a world.");
                return;
            }
            if (thumbnail.FavoriteKey == null)
            {
                CartWindow.Status($"The game doesn't know {thumbnail.Name} as an item (another mod's, perhaps), so it can't be starred.");
                return;
            }
            GameLists.Star(store, thumbnail.FavoriteKey);
            entry = thumbnail;
            mode = Mode.Favorite;
            Show(PointerAt());
            Browser.Render();
        }

        /// <summary>"Add List…": a name for a new list.</summary>
        internal static void AddList(Entry thumbnail)
        {
            if (Favorites.Store == null)
            {
                return;
            }
            entry = thumbnail;
            mode = Mode.AddList;
            Show(PointerAt());
        }

        /// <summary>A right-click on one of his lists: a new name for it.</summary>
        internal static void Rename(int list)
        {
            FavoritePieceList store = Favorites.Store;
            if (store == null || list < 0 || list >= GameLists.ListCount(store))
            {
                return;
            }
            if (!GameLists.CanRename)
            {
                CartWindow.Status("This version of the game doesn't let the mod rename a list.");
                return;
            }
            renaming = list;
            mode = Mode.Rename;
            Show(PointerAt());
        }

        internal static void Close()
        {
            if (blocker != null)
            {
                blocker.SetActive(false);   // gone from the screen this frame; destroyed at the end of it
                UnityEngine.Object.Destroy(blocker);
            }
            blocker = null;
            nameField = null;
            errorText = null;
            submitNextFrame = false;
        }

        /// <summary>Every frame while the window is open: Enter in the name field adds or renames, a frame later (never
        /// from inside the field's own handler, which also runs as the field is taken down).</summary>
        internal static void Update()
        {
            if (submitNextFrame && IsOpen)
            {
                submitNextFrame = false;
                Submit();
            }
        }

        private static Vector2 PointerAt()
        {
            if (window == null)
            {
                return Vector2.zero;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(window, ZInput.pointerPosition, null, out Vector2 local);
            Rect rect = window.rect;
            return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        }

        // ---- Drawing

        private static void Show(Vector2 near)
        {
            Close();
            if (window == null)
            {
                return;
            }
            at = near;
            typed = mode == Mode.Rename ? GameLists.ListName(Favorites.Store, renaming) : string.Empty;
            Rect rect = window.rect;
            blocker = Ui.Box("AddToCart_Lists", window, 0f, 0f, rect.width, rect.height, new Color(0f, 0f, 0f, 0.3f));
            Clicks outside = blocker.AddComponent<Clicks>();
            outside.Left = Close;
            outside.Right = Close;
            outside.Middle = Close;

            FavoritePieceList store = Favorites.Store;
            int lists = GameLists.ListCount(store);
            int shownRows = mode == Mode.Favorite ? Math.Min(lists, MostRows) : 0;
            float height = 16f + 30f + 34f + shownRows * RowHeight + (mode == Mode.Favorite && lists == 0 ? 24f : 0f) + 40f + 20f + 44f + 12f;
            float x = Mathf.Clamp(near.x + 8f, 8f, rect.width - Width - 8f);
            float y = Mathf.Clamp(near.y - 20f, 8f, rect.height - height - 8f);
            GameObject panel = GUIManager.Instance.CreateWoodpanel(blocker.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Width, height, false);
            Ui.Place(panel, x, y, Width, height);
            // Clicks inside the panel stay inside it.
            panel.AddComponent<Clicks>();
            Transform p = panel.transform;

            float top = 14f;
            string title = mode == Mode.Favorite ? entry.Name : mode == Mode.AddList ? "Add List" : "Rename List";
            Ui.Label(p, title, 16f, top, Width - 32f, 28f, 18, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            top += 30f;
            string hint = mode == Mode.Favorite ? "Your lists are the game's own favorite categories: they show in the build menu too."
                : mode == Mode.AddList ? "Up to 20 characters. It shows in the build menu's Favorites too."
                : "Up to 20 characters. To delete a list, middle-click it in the game's build menu.";
            Ui.Label(p, hint, 16f, top, Width - 32f, 32f, 12, TextAnchor.UpperLeft, Ui.Quiet);
            top += 34f;

            if (mode == Mode.Favorite)
            {
                if (lists == 0)
                {
                    Ui.Label(p, "No lists yet: name one below.", 16f, top, Width - 32f, 22f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
                    top += 24f;
                }
                else
                {
                    checklist = Ui.Scroll("Lists", p, 12f, top, Width - 24f, shownRows * RowHeight);
                    Ui.List(checklist, 0f);
                    for (int list = 0; list < lists; list++)
                    {
                        CheckRow(checklist, store, list);
                    }
                    top += shownRows * RowHeight;
                }
            }

            // The name field, with Add List (or Rename) beside it.
            GameObject field = GUIManager.Instance.CreateInputField(p, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                    InputField.ContentType.Standard, mode == Mode.Rename ? "List name" : "New list name", 15,
                                                                    Width - 132f, 30f);
            Ui.Place(field, 14f, top + 4f, Width - 132f, 30f);
            nameField = field.GetComponent<InputField>();
            nameField.characterLimit = GameLists.MaxNameLength;
            nameField.SetTextWithoutNotify(typed);
            nameField.onValueChanged.AddListener(text => typed = text ?? string.Empty);
            nameField.onEndEdit.AddListener(text => Module.Guard("naming a list", () =>
            {
                // Only note it: this also runs as the field is taken down. Enter adds, next frame.
                typed = text ?? string.Empty;
                if (ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false))
                {
                    submitNextFrame = true;
                }
            }));
            string act = mode == Mode.Rename ? "Rename" : mode == Mode.AddList ? "Add" : "Add List";
            Ui.Button(p, act, Width - 112f, top + 4f, 98f, 30f, () => Module.Guard("naming a list", Submit));
            top += 40f;
            errorText = Ui.Label(p, string.Empty, 16f, top, Width - 32f, 18f, 12, TextAnchor.MiddleLeft, Ui.Bad);
            top += 20f;

            if (mode == Mode.Favorite)
            {
                Ui.Button(p, "Remove from Favorites", 14f, top + 4f, 180f, 34f, () => Module.Guard("removing a favorite", Unstar));
                Ui.Button(p, "Done", Width - 104f, top + 4f, 90f, 34f, () => Module.Guard("closing the lists", Close));
            }
            else
            {
                Ui.Button(p, "Cancel", Width - 104f, top + 4f, 90f, 34f, () => Module.Guard("closing the lists", Close));
            }
            if (mode != Mode.Favorite)
            {
                nameField.Select();
                nameField.ActivateInputField();
            }
        }

        private static void CheckRow(RectTransform checklist, FavoritePieceList store, int list)
        {
            GameObject row = Ui.Row(checklist, RowHeight);
            Toggle box = Ui.Checkbox(row.transform, 4f, 3f, 20f);
            box.SetIsOnWithoutNotify(GameLists.InList(store, entry.FavoriteKey, list));
            box.onValueChanged.AddListener(on => Module.Guard("changing a list", () =>
            {
                GameLists.SetInList(Favorites.Store, entry.FavoriteKey, list, on);
                Browser.Render();
            }));
            Text name = Ui.Label(row.transform, GameLists.ListName(store, list), 32f, 0f, Width - 80f, RowHeight, 15, TextAnchor.MiddleLeft);
            name.raycastTarget = true;
            name.gameObject.AddComponent<Clicks>().Left = () => box.isOn = !box.isOn;
        }

        // ---- Acting

        private static void Submit()
        {
            FavoritePieceList store = Favorites.Store;
            if (store == null || !IsOpen)
            {
                Close();
                return;
            }
            string name = (nameField != null ? nameField.text : typed) ?? string.Empty;
            string refused = GameLists.Refuse(name);
            if (refused != null)
            {
                if (errorText != null)
                {
                    errorText.text = refused;
                }
                return;
            }
            switch (mode)
            {
                case Mode.Rename:
                    if (GameLists.Rename(store, renaming, name))
                    {
                        Browser.ListRenamed(renaming, name.Trim());
                        CartWindow.Status($"Renamed the list to {name.Trim()}.");
                    }
                    Close();
                    break;
                case Mode.AddList:
                    GameLists.AddList(store, name);
                    Close();
                    CartWindow.Status($"New list {name.Trim()}: middle-click a thumbnail to put it in.");
                    break;
                default:
                    int list = GameLists.AddList(store, name);
                    if (list >= 0)
                    {
                        GameLists.SetInList(store, entry.FavoriteKey, list, true);
                    }
                    Show(at);   // again, with the new list ticked
                    break;
            }
            Browser.Render();
        }

        private static void Unstar()
        {
            GameLists.Unstar(Favorites.Store, entry.FavoriteKey);
            CartWindow.Status($"{entry.Name} is no longer a favorite, nor in any list.");
            Close();
            Browser.Render();
        }
    }
}
