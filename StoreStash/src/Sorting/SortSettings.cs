using BepInEx.Configuration;
using QoLMods.Catalogue;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>What the sort key does while a chest is open.</summary>
    internal enum SortKeyWithChest
    {
        MyInventory,
        TheChest,
        Both,
    }

    /// <summary>Where a sort starts laying stacks.</summary>
    internal enum SortFrom
    {
        /// <summary>From the top left: the empty cells end up at the bottom.</summary>
        Top,

        /// <summary>The same order, pushed down so it ends at the bottom right: the empty cells end up at the top.</summary>
        Bottom,
    }

    /// <summary>Which chests are sorted as they open.</summary>
    internal enum SortOnOpenChoice
    {
        EveryChest,
        ChestsIBuilt,
        Off,
    }

    /// <summary>
    /// Sorting's settings (card A53), the part "Sorting" of StoreStash's: a section of its own in the single release's
    /// file, and in the bundle's one "StoreStash" section. In F1 (Folds): its fold row at 700 over its own six (699 to 694),
    /// then its groups How it sorts (690; 689 to 685), What stays in place (680; 679 to 677) and Chests (670; 669 to 666),
    /// each row right above its settings (card A41's second round, decision 22; Settings has every part's orders). Keys start with
    /// Sort, so none meets another part's in the bundle. The defaults are his HexQuickStackStorage settings (S; a chest
    /// sorted as it opens) and the card's. Every one takes effect at once.
    /// </summary>
    internal static class SortSettings
    {
        internal const string Part = "Sorting";

        internal static ConfigEntry<bool> On { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<SortKeyWithChest> KeyWithChest { get; private set; }

        internal static ConfigEntry<bool> Button { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<SortOrderChoice> Order { get; private set; }

        internal static ConfigEntry<string> Families { get; private set; }

        internal static ConfigEntry<bool> KeepHotbar { get; private set; }

        internal static ConfigEntry<bool> KeepWorn { get; private set; }

        internal static ConfigEntry<bool> KeepFavorites { get; private set; }

        internal static ConfigEntry<bool> Merge { get; private set; }

        /// <summary>A favourite's stacks merge when his inventory is sorted (card A82).</summary>
        internal static ConfigEntry<bool> MergeFavorites { get; private set; }

        internal static ConfigEntry<SortFrom> From { get; private set; }

        internal static ConfigEntry<bool> ChestButton { get; private set; }

        internal static ConfigEntry<Vector2> ChestButtonOffset { get; private set; }

        internal static ConfigEntry<SortOnOpenChoice> OnOpen { get; private set; }

        internal static ConfigEntry<bool> OthersMarked { get; private set; }

        internal static ConfigEntry<bool> Sound { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            On = settings.Bind(Part, "SortOn", true,
                "Sorting", 699,
                "On: the sort key and buttons, and chests sorted as they open, by the settings below. Off: none of it (the " +
                "rest of StoreStash works as before).");

            Key = settings.Bind(Part, "SortKey", new KeyboardShortcut(KeyCode.S),
                "Sort key (inventory open)", 698,
                "Sorts your inventory while it's open (the rows the game gives you; EquipmentAndQuickSlots' slots are never " +
                "touched). Not while you type in chat, a sign or a search box, or while a configuration manager's window (F1) " +
                "is open. One press runs one of StoreStash's keys: if another has the same key with more modifiers held, that " +
                "one runs instead.");

            Key.SettingChanged += (sender, args) => SortButtons.KeyChanged();   // the button's tooltip names the key

            KeyWithChest = settings.Bind(Part, "SortKeyWithChest", SortKeyWithChest.MyInventory,
                "Sort key with a chest open", 697,
                "MyInventory: the key sorts your inventory, as without a chest. TheChest: it sorts the open chest instead. " +
                "Both: the chest and your inventory.");

            Button = settings.Bind(Part, "SortButton", true,
                "Sort button beside the inventory", 696,
                "A button below the weight box that sorts your inventory (the key can sort an open chest instead: the setting " +
                "above).");

            ButtonOffset = settings.Bind(Part, "SortButtonOffset", Vector2.zero,
                "Sort button position (offset)", 695,
                "Moves that button from its place below the weight box, in the inventory's own units: x to the right, y up. " +
                "For when another mod puts something there.");

            Order = settings.Bind(Part, "SortOrder", SortOrderChoice.PacksOrder,
                "Sort order", 689,
                "PacksOrder: the order you browse in Add to Cart: families (the line below), each family's own groups, then " +
                "progression, Meadows to the Deep North. ByType: by the game's own item type. ByName: by name. ByWeight: " +
                "heaviest stacks first. ByValue: what a trader pays for one, most first. Ties go by name, then the highest " +
                "quality, then the fullest stack.");

            Families = settings.Bind(Part, "SortFamilies", Orders.DefaultFamilyLine,
                "The pack's order: families", 688,
                "The families in the order PacksOrder sorts them, with each family's groups in brackets. Move names to change " +
                "the order; names left out go last in the usual order, and misspelt ones are ignored. Empty: the usual order.");

            KeepHotbar = settings.Bind(Part, "SortKeepHotbar", true,
                "Keep the hotbar in place", 679,
                "On: the top row isn't sorted, and nothing is sorted into it. Off: it's sorted with the rest, so the first " +
                "things in the order fill it.");

            KeepWorn = settings.Bind(Part, "SortKeepWorn", true,
                "Keep what you wear in place", 678,
                "On: what you wear or hold stays in its cell (a weapon the game has put away for a moment too). Off: it's " +
                "sorted with the rest, and you still wear it; it's never merged with another stack either way.");

            KeepFavorites = settings.Bind(Part, "SortKeepFavorites", true,
                "Keep favorites in place", 677,
                "On: your favorites (the gold border) stay in their cells (with \"Merge favorite stacks\" on, a favorite's " +
                "stacks still merge into the first of them). Off: they're sorted with the rest. In a chest nothing stays in " +
                "place.");

            Merge = settings.Bind(Part, "SortMerge", true,
                "Merge part-stacks", 687,
                "On: part-stacks of the same item (and quality) are put together into as few stacks as fit. Off: every stack " +
                "keeps its amount. Stacks that stay in place are never topped up, except a favorite's from its own other " +
                "stacks (\"Merge favorite stacks\").");

            // Card A82 (his word, 2026-10-09): favourite stacks merge when sorted, on out of the box; a new key, so no
            // moved default. In F1 right after "Merge part-stacks", in "How it sorts".
            MergeFavorites = settings.Bind(Part, "SortMergeFavorites", true,
                "Merge favorite stacks", 686,
                "On: when your inventory is sorted and part-stacks merge (\"Merge part-stacks\"), a favorite's stacks merge " +
                "too: while favorites are kept in place, into the first of them (top left first), which stays in its cell, " +
                "the emptied ones' cells going to the rest; otherwise as any other item's. Off: a favorite's stacks keep " +
                "their amounts, so a stack you keep apart stays apart. Never what you wear or hold, or a stack you're " +
                "carrying. Your inventory only: in a chest a favorite is like any other item.");

            From = settings.Bind(Part, "SortFrom", SortFrom.Top,
                "Sorted items start", 685,
                "Top: from the top left, the empty cells left at the bottom. Bottom: the same order, pushed down to end at the " +
                "bottom right, the empty cells left at the top.");

            ChestButton = settings.Bind(Part, "SortChestButton", true,
                "Sort button in a chest's window", 669,
                "A button left of Place stacks that sorts the open chest.");

            ChestButtonOffset = settings.Bind(Part, "SortChestButtonOffset", Vector2.zero,
                "Chest sort button position (offset)", 668,
                "Moves that button from its place left of Place stacks, in the window's own units: x to the right, y up.");

            OnOpen = settings.Bind(Part, "SortOnOpen", SortOnOpenChoice.EveryChest,
                "Sort a chest as it opens", 667,
                "EveryChest: every chest you open is sorted as its window opens, once the game has handed it to you (a " +
                "friend's too, while you have it open; never a grave). ChestsIBuilt: only chests, carts and ships you built. " +
                "Off: only by its button.");

            OthersMarked = settings.Bind(Part, "SortOnOpenOthersMarked", false,
                "Sort as it opens: chests others marked too", 666,
                "Off: a chest someone else marked as theirs (Personal chests' mark, Left Alt + H) isn't sorted as it opens, " +
                "as the quick stack leaves it alone; its Sort button still sorts it, and so does the key when it sorts the " +
                "chest. On: it's sorted as it opens like any other. Chests you marked as yours are sorted as they open either " +
                "way.");

            Sound = settings.Bind(Part, "SortSound", true,
                "Sound after sorting", 694,
                "The game's own sound of moving items, when the key or a button sorted something (not as a chest opens).");

            Folds.Fold sorting = Folds.Add(settings, Part, "SortUnfolded", "Sorting", 700, new ConfigEntryBase[]
            {
                On, Key, KeyWithChest, Button, ButtonOffset, Sound,
            });
            Folds.AddGroup(settings, Part, "SortHowUnfolded", "How it sorts", 690, new ConfigEntryBase[]
            {
                Order, Families, Merge, MergeFavorites, From,
            }, sorting);
            Folds.AddGroup(settings, Part, "SortStaysUnfolded", "What stays in place", 680, new ConfigEntryBase[]
            {
                KeepHotbar, KeepWorn, KeepFavorites,
            }, sorting);
            Folds.AddGroup(settings, Part, "SortChestsUnfolded", "Chests", 670, new ConfigEntryBase[]
            {
                ChestButton, ChestButtonOffset, OnOpen, OthersMarked,
            }, sorting);
        }
    }
}
