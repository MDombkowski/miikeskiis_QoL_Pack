using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComfortBuild
{
    /// <summary>
    /// The build menu's private parts the mod reaches, looked up by name when the game starts (Valheim 1.0.16's BuildUi,
    /// decompiled 2026-09-30). Each lookup checks the member's type too, so a game update that renames or retypes one
    /// switches the mod off with a line saying which, instead of failing in the menu.
    /// </summary>
    internal static class Game
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static AccessTools.FieldRef<BuildUi, List<IPieceList>> PieceLists;
        internal static AccessTools.FieldRef<BuildUi, RectTransform> TabContainer;
        internal static AccessTools.FieldRef<BuildUi, TabHandler> Tabs;
        internal static AccessTools.FieldRef<BuildUi, int> CurrentList;
        internal static AccessTools.FieldRef<BuildUi, int> CurrentTag;
        internal static AccessTools.FieldRef<BuildUi, PieceTable> CurrentTool;
        internal static AccessTools.FieldRef<BuildUi, int> AvailableCount;
        internal static AccessTools.FieldRef<BuildUi, RectTransform> TagContent;
        internal static AccessTools.FieldRef<BuildUi, GameObject> TagButtonPrefab;
        internal static AccessTools.FieldRef<BuildUi, List<BuildUiTagButton>> TagButtons;
        internal static AccessTools.FieldRef<BuildUi, List<BuildUiPieceButton>> PieceButtons;
        internal static AccessTools.FieldRef<BuildUiPieceButton, Image> Icon;
        private static MethodInfo configureNavigation;

        // Optional: the game's tick and checkbox (the favourites dropdown's rows), for the mark and the switch.
        private static AccessTools.FieldRef<BuildUi, BuildUiFavoritesDropdown> favoritesDropdown;
        private static AccessTools.FieldRef<BuildUiFavoritesDropdown, GameObject> checkButtonPrefab;
        private static AccessTools.FieldRef<BuildUiFavoriteCategoryCheckButton, Image> check;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            PieceLists = Field<BuildUi, List<IPieceList>>("m_pieceLists", missing);
            TabContainer = Field<BuildUi, RectTransform>("m_tabContainer", missing);
            Tabs = Field<BuildUi, TabHandler>("m_tabHandler", missing);
            CurrentList = Field<BuildUi, int>("m_currentPieceList", missing);
            CurrentTag = Field<BuildUi, int>("m_currentTagId", missing);
            CurrentTool = Field<BuildUi, PieceTable>("m_currentBuildTool", missing);
            AvailableCount = Field<BuildUi, int>("m_availablePieceCount", missing);
            TagContent = Field<BuildUi, RectTransform>("m_tagListScrollContent", missing);
            TagButtonPrefab = Field<BuildUi, GameObject>("m_tagButtonPrefab", missing);
            TagButtons = Field<BuildUi, List<BuildUiTagButton>>("m_tagButtons", missing);
            PieceButtons = Field<BuildUi, List<BuildUiPieceButton>>("m_pieceButtons", missing);
            Icon = Field<BuildUiPieceButton, Image>("m_icon", missing);
            configureNavigation = typeof(BuildUi).GetMethod("ConfigureButtonNavigation", AnyInstance, null, System.Type.EmptyTypes, null);
            if (configureNavigation == null)
            {
                missing.Add("BuildUi.ConfigureButtonNavigation()");
            }

            var optional = new List<string>();
            favoritesDropdown = Field<BuildUi, BuildUiFavoritesDropdown>("m_favoritesDropdown", optional);
            checkButtonPrefab = Field<BuildUiFavoritesDropdown, GameObject>("checkButtonPrefab", optional);
            check = Field<BuildUiFavoriteCategoryCheckButton, Image>("m_check", optional);
            if (optional.Count > 0)
            {
                missingOptional.Add($"{string.Join(", ", optional)} (the mark and the switch draw a plain tick instead)");
            }
        }

        /// <summary>Sets the build menu's gamepad and keyboard paths again, after the mod added or moved a row.</summary>
        internal static void ConfigureNavigation(BuildUi ui) => configureNavigation?.Invoke(ui, null);

        /// <summary>The favourites dropdown's checkbox row (FavCategoryCheck), whose box and tick the mod borrows; or null.</summary>
        internal static BuildUiFavoriteCategoryCheckButton CheckRow(BuildUi ui)
        {
            if (favoritesDropdown == null || checkButtonPrefab == null || check == null)
            {
                return null;
            }
            BuildUiFavoritesDropdown dropdown = favoritesDropdown(ui);
            GameObject prefab = dropdown != null ? checkButtonPrefab(dropdown) : null;
            return prefab != null ? prefab.GetComponent<BuildUiFavoriteCategoryCheckButton>() : null;
        }

        /// <summary>The tick image of a checkbox row (its box is the tick's parent), or null.</summary>
        internal static Image TickOf(BuildUiFavoriteCategoryCheckButton row) => row != null && check != null ? check(row) : null;

        /// <summary>The font of the inventory's level number (a gear item's quality), which the comfort number uses; else
        /// null.</summary>
        internal static TMP_Text LevelNumber()
        {
            InventoryGui gui = InventoryGui.instance;
            GameObject element = gui != null && gui.m_playerGrid != null ? gui.m_playerGrid.m_elementPrefab : null;
            InventoryElement slot = element != null ? element.GetComponent<InventoryElement>() : null;
            return slot != null ? slot.m_quality : null;
        }

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name, List<string> missing)
        {
            FieldInfo field = typeof(T).GetField(name, AnyInstance);
            if (field == null || field.FieldType != typeof(F))
            {
                missing.Add($"{typeof(T).Name}.{name}");
                return null;
            }
            return AccessTools.FieldRefAccess<T, F>(field);
        }
    }
}
