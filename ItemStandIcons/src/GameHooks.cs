using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ItemStandIcons
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it reads, looked up once at startup. Each place the marks
    /// show needs its own few; a place whose members aren't found stays unmarked (the log says which), and the others work.
    /// Checked against the game's InventoryGrid (UpdateGui, m_inventory, m_elements), HotkeyBar (UpdateIcons, m_elements,
    /// ElementData.m_go, m_items), InventoryGui (AddRecipeToList, m_availableRecipes, RecipeDataPair.Recipe and
    /// InterfaceElement, UpdateRecipe, m_selectedRecipe, SetupRequirement, HideRequirement), Hud (SetupPieceInfo) and
    /// StoreGui (FillList, m_itemList, m_trader) of 2026-09-30 (Valheim 1.0.16).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // The inventory grids (his inventory, chests, EquipmentAndQuickSlots' slots).
        internal static MethodInfo GridUpdateGui { get; private set; }
        private static FieldInfo gridInventory;
        private static FieldInfo gridElements;

        // The hotbar (and EquipmentAndQuickSlots' quick-slot bar, a copy of it).
        internal static MethodInfo HotbarUpdateIcons { get; private set; }
        private static FieldInfo hotbarElements;
        private static FieldInfo hotbarItems;
        private static FieldInfo elementObject;

        // The crafting menu.
        internal static MethodInfo AddRecipeToList { get; private set; }
        internal static MethodInfo UpdateRecipe { get; private set; }
        private static FieldInfo availableRecipes;
        private static FieldInfo selectedRecipe;
        private static PropertyInfo pairRecipe;
        private static PropertyInfo pairElement;
        internal static MethodInfo SetupRequirement { get; private set; }
        internal static MethodInfo HideRequirement { get; private set; }
        internal static MethodInfo SetupPieceInfo { get; private set; }

        // The trader.
        internal static MethodInfo FillStoreList { get; private set; }
        private static FieldInfo storeRows;
        private static FieldInfo storeTrader;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            var lost = new List<string>();
            int places = 0;

            // His inventory and chests.
            GridUpdateGui = typeof(InventoryGrid).GetMethod("UpdateGui", AnyInstance, null, new[] { typeof(Player), typeof(ItemDrop.ItemData) }, null);
            gridInventory = Field(typeof(InventoryGrid), "m_inventory", typeof(Inventory));
            gridElements = Field(typeof(InventoryGrid), "m_elements", typeof(List<InventoryElement>));
            if (GridUpdateGui == null || gridInventory == null || gridElements == null)
            {
                GridUpdateGui = null;
                lost.Add("the inventory and chests (InventoryGrid.UpdateGui, m_inventory, m_elements)");
            }
            else
            {
                places++;
            }

            // The hotbar.
            HotbarUpdateIcons = typeof(HotkeyBar).GetMethod("UpdateIcons", AnyInstance, null, new[] { typeof(Player) }, null);
            Type elementData = typeof(HotkeyBar).GetNestedType("ElementData", BindingFlags.NonPublic | BindingFlags.Public);
            hotbarElements = elementData != null ? Field(typeof(HotkeyBar), "m_elements", typeof(List<>).MakeGenericType(elementData)) : null;
            hotbarItems = Field(typeof(HotkeyBar), "m_items", typeof(List<ItemDrop.ItemData>));
            elementObject = elementData != null ? Field(elementData, "m_go", typeof(GameObject)) : null;
            if (HotbarUpdateIcons == null || hotbarElements == null || hotbarItems == null || elementObject == null)
            {
                HotbarUpdateIcons = null;
                lost.Add("the hotbar (HotkeyBar.UpdateIcons, m_elements, m_items, ElementData.m_go)");
            }
            else
            {
                places++;
            }

            // The crafting list and the chosen recipe: both read the game's recipe pairs.
            Type pair = typeof(InventoryGui).GetNestedType("RecipeDataPair", BindingFlags.NonPublic | BindingFlags.Public);
            pairRecipe = pair?.GetProperty("Recipe", AnyInstance);
            pairElement = pair?.GetProperty("InterfaceElement", AnyInstance);
            if (pairRecipe?.PropertyType != typeof(Recipe) || !pairRecipe.CanRead)
            {
                pairRecipe = null;
            }
            if (pairElement?.PropertyType != typeof(GameObject) || !pairElement.CanRead)
            {
                pairElement = null;
            }
            AddRecipeToList = typeof(InventoryGui).GetMethod("AddRecipeToList", AnyInstance, null,
                new[] { typeof(Player), typeof(Recipe), typeof(ItemDrop.ItemData), typeof(bool) }, null);
            availableRecipes = pair != null ? Field(typeof(InventoryGui), "m_availableRecipes", typeof(List<>).MakeGenericType(pair)) : null;
            if (AddRecipeToList == null || availableRecipes == null || pairRecipe == null || pairElement == null)
            {
                AddRecipeToList = null;
                lost.Add("the crafting list (InventoryGui.AddRecipeToList, m_availableRecipes, RecipeDataPair)");
            }
            else
            {
                places++;
            }
            UpdateRecipe = typeof(InventoryGui).GetMethod("UpdateRecipe", AnyInstance, null, new[] { typeof(Player), typeof(float) }, null);
            selectedRecipe = pair != null ? Field(typeof(InventoryGui), "m_selectedRecipe", pair) : null;
            if (UpdateRecipe == null || selectedRecipe == null || pairRecipe == null)
            {
                UpdateRecipe = null;
                lost.Add("the chosen recipe (InventoryGui.UpdateRecipe, m_selectedRecipe)");
            }
            else
            {
                places++;
            }

            // The ingredients, and the build menu's costs (the game fills both with SetupRequirement).
            SetupRequirement = typeof(InventoryGui).GetMethod("SetupRequirement", AnyStatic, null,
                new[] { typeof(Transform), typeof(Piece.Requirement), typeof(Player), typeof(bool), typeof(int), typeof(int) }, null);
            HideRequirement = typeof(InventoryGui).GetMethod("HideRequirement", AnyStatic, null, new[] { typeof(Transform) }, null);
            SetupPieceInfo = typeof(Hud).GetMethod("SetupPieceInfo", AnyInstance, null, new[] { typeof(Piece) }, null);
            if (SetupRequirement == null || SetupRequirement.ReturnType != typeof(bool) || HideRequirement == null)
            {
                SetupRequirement = null;
                HideRequirement = null;
                lost.Add("the ingredients and the build menu's costs (InventoryGui.SetupRequirement, HideRequirement)");
            }
            else
            {
                places++;
                if (SetupPieceInfo == null)
                {
                    lost.Add("the build menu's costs (Hud.SetupPieceInfo)");
                }
            }

            // The trader.
            FillStoreList = typeof(StoreGui).GetMethod("FillList", AnyInstance, null, Type.EmptyTypes, null);
            storeRows = Field(typeof(StoreGui), "m_itemList", typeof(List<GameObject>));
            storeTrader = Field(typeof(StoreGui), "m_trader", typeof(Trader));
            if (FillStoreList == null || storeRows == null || storeTrader == null)
            {
                FillStoreList = null;
                lost.Add("the trader (StoreGui.FillList, m_itemList, m_trader)");
            }
            else
            {
                places++;
            }

            if (places == 0)
            {
                missing.Add("every place item icons are drawn (the game has changed)");
                return;
            }
            foreach (string place in lost)
            {
                missingOptional.Add(place + ": no marks there");
            }
        }

        private static FieldInfo Field(Type type, string name, Type fieldType)
        {
            FieldInfo field = type.GetField(name, AnyInstance);
            return field != null && field.FieldType == fieldType ? field : null;
        }

        internal static Inventory InventoryOf(InventoryGrid grid) => gridInventory.GetValue(grid) as Inventory;

        internal static List<InventoryElement> ElementsOf(InventoryGrid grid) => gridElements.GetValue(grid) as List<InventoryElement>;

        /// <summary>The hotbar's cells, in order (a cell per slot up to the last one bound).</summary>
        internal static IList HotbarElements(HotkeyBar bar) => hotbarElements.GetValue(bar) as IList;

        internal static GameObject ObjectOf(object element) => element != null ? elementObject.GetValue(element) as GameObject : null;

        internal static List<ItemDrop.ItemData> HotbarItems(HotkeyBar bar) => hotbarItems.GetValue(bar) as List<ItemDrop.ItemData>;

        /// <summary>The crafting list's rows, as the game keeps them (recipe and row object).</summary>
        internal static IList AvailableRecipes(InventoryGui gui) => availableRecipes.GetValue(gui) as IList;

        internal static Recipe RecipeOf(object pair) => pair != null ? pairRecipe.GetValue(pair) as Recipe : null;

        internal static GameObject RowOf(object pair) => pair != null ? pairElement.GetValue(pair) as GameObject : null;

        /// <summary>The recipe picked in the crafting menu, or null.</summary>
        internal static Recipe SelectedRecipe(InventoryGui gui) => RecipeOf(selectedRecipe.GetValue(gui));

        internal static List<GameObject> StoreRows(StoreGui store) => storeRows.GetValue(store) as List<GameObject>;

        internal static Trader StoreTrader(StoreGui store) => storeTrader.GetValue(store) as Trader;
    }
}
