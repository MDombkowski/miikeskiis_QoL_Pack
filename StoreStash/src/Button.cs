using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The quick stack button beside his inventory (a SquareButton, with a drawn chest and arrow). It sits just outside the
    /// inventory panel's right edge, between the armour tab and the weight tab, where the panel leaves 77 px free (read from
    /// the game's IngameGui_Inventory prefab: the tabs stick out 72 px past the panel's edge, the armour tab above the middle,
    /// the weight tab at the bottom). With the personal quick stack button on as well (card A43), the two share that space
    /// at 32 px each, this one above (the column agreed with the session adding sorting and trash, whose buttons sit above
    /// the armour tab and below the weight tab). EquipmentAndQuickSlots' own panel starts right of the tabs, so it stays clear. A
    /// setting moves it, for another mod's things there.
    /// </summary>
    internal static class Button
    {
        /// <summary>The button's width and height, as the pack's other square buttons (Reserve Stock's padlock).</summary>
        internal const float Size = 36f;

        /// <summary>Its width and height while the personal quick stack button shares its space: two fit, 4 px apart.</summary>
        internal const float SharedSize = 32f;

        /// <summary>Its middle, from the inventory panel's bottom right corner: the middle of the armour and weight tabs'
        /// column (32 px right of the edge), and midway up the free space between the weight tab's icon and the armour tab.</summary>
        internal static readonly Vector2 Place = new Vector2(32f, 145f);

        /// <summary>Its middle while the personal quick stack button shares the space, below it.</summary>
        internal static readonly Vector2 SharedPlace = new Vector2(32f, 162.75f);

        internal static readonly SquareButton Square = new SquareButton("StoreStash_Button", () => Icon.Sprite, Clicked,
            () => Settings.ShowButton.Value, Usable,
            () => (Shared ? SharedPlace : Place) + Settings.ButtonOffset.Value,
            () => Shared ? SharedSize : Size,
            () => $"Quick stack ({Settings.KeyText(Settings.Key.Value)}): what you carry goes into the chests nearby" +
                  (Settings.OnlyWhereHeld.Value ? " that already hold it" : string.Empty) + ". Your favorites stay with you.");

        /// <summary>Both buttons are on: they share the space between the tabs. With only one on, it has the space alone.</summary>
        internal static bool Shared => Settings.ShowButton.Value && Settings.PersonalButton.Value;

        /// <summary>A setting its tooltip names changed (the key, only into chests that already hold the item): the tooltip is
        /// written again.</summary>
        internal static void KeyChanged() => Square.TipChanged();

        /// <summary>Every frame: shown while his inventory is open, the mod and the setting on; greyed while a press would do
        /// nothing (one under way, or a chest open with "Quick stack while a chest is open" off).</summary>
        internal static void Update() => Square.Update();

        /// <summary>The undo step after an error: the button goes.</summary>
        internal static void Remove() => Square.Remove();

        /// <summary>Whether a press of either quick stack button would do anything now.</summary>
        internal static bool Usable(InventoryGui gui) =>
            !QuickStack.Running && (Settings.WithChestOpen.Value || GameHooks.OpenChest(gui) == null);

        private static void Clicked()
        {
            Module.Guard("quick stacking from the button", () => QuickStack.Ask());
        }
    }
}
