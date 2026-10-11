using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>When the trash can asks before deleting.</summary>
    internal enum TrashAsk
    {
        Always,

        /// <summary>Only when more than one stack would go (a carried stack never asks).</summary>
        MoreThanOneStack,

        Never,
    }

    /// <summary>
    /// The trash's settings (card A53), the part "Trash" of StoreStash's: a section of its own in the single release's
    /// file, and in the bundle's one "StoreStash" section. In F1 (Folds): its fold row at 600 over its own three (599 to
    /// 597), then its groups Marks (590; 589 to 586) and The trash can (580; 579 to 575), each row right above its settings (card A41's
    /// second round, decision 22; Settings has every part's orders). Keys start with Trash. The defaults are his
    /// HexQuickStackStorage settings (Left Alt + right-click; asking first) and the card's. Every one takes effect at once.
    /// </summary>
    internal static class TrashSettings
    {
        internal const string Part = "Trash";

        /// <summary>The red of a trash mark's border.</summary>
        internal static readonly Color Red = new Color(0.9f, 0.18f, 0.12f, 1f);

        internal static ConfigEntry<bool> On { get; private set; }

        internal static ConfigEntry<KeyCode> Modifier { get; private set; }

        internal static ConfigEntry<bool> Borders { get; private set; }

        internal static ConfigEntry<Color> BorderColor { get; private set; }

        internal static ConfigEntry<bool> InChestWindows { get; private set; }

        internal static ConfigEntry<bool> Button { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<TrashAsk> Ask { get; private set; }

        internal static ConfigEntry<bool> Carried { get; private set; }

        internal static ConfigEntry<bool> CarriedKept { get; private set; }

        internal static ConfigEntry<bool> Message { get; private set; }

        internal static ConfigEntry<bool> ImportHex { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            On = settings.Bind(Part, "TrashOn", true,
                "Trash", 599,
                "On: trash marks and the trash can, by the settings below. Off: neither shows or works (your marks are kept).");

            Modifier = settings.Bind(Part, "TrashModifier", KeyCode.LeftAlt,
                "Trash: key held with a right-click", 598,
                "Hold it and right-click an item in your inventory to mark its kind as trash, or not: every item of that kind, " +
                "whatever its quality, now and later. None: marking is off (your marks are kept). If it's the same key as the " +
                "favorite key, the right-click marks a favorite.");

            Borders = settings.Bind(Part, "TrashBorders", true,
                "Trash borders", 589,
                "A border around every item marked as trash, just inside a favorite's.");

            BorderColor = settings.Bind(Part, "TrashBorderColor", Red,
                "Trash border colour", 588,
                "The colour of that border.");

            InChestWindows = settings.Bind(Part, "TrashInChestWindows", false,
                "Trash marks in chest windows too", 587,
                "On: the border shows on marked items in a chest's window as well, and the right-click marks there too. The " +
                "trash can only ever deletes from your inventory.");

            Button = settings.Bind(Part, "TrashButton", true,
                "Trash can beside the inventory", 579,
                "A trash can above the armour box. A click deletes every item marked as trash from your inventory, but never " +
                "what's on the hotbar, what you wear or hold, a favorite or a quest item, whatever is marked.");

            ButtonOffset = settings.Bind(Part, "TrashButtonOffset", Vector2.zero,
                "Trash can position (offset)", 578,
                "Moves the trash can from its place above the armour box, in the inventory's own units: x to the right, y up.");

            Ask = settings.Bind(Part, "TrashAsk", TrashAsk.Always,
                "Ask before deleting", 597,
                "Always: the can says what it would delete and waits for your yes. MoreThanOneStack: it asks only when more than " +
                "one stack would go. Never: it deletes at once. The question's \"Don't ask again\" box sets this to Never.");

            Carried = settings.Bind(Part, "TrashCarried", true,
                "The can deletes an item you're carrying", 577,
                "On: carrying an item (picked up with a click, or split off) and clicking the can deletes just what you carry, " +
                "whether it's marked or not. Off: the can asks you to put it down first.");

            CarriedKept = settings.Bind(Part, "TrashCarriedKept", false,
                "...a carried favorite or hotbar item too", 576,
                "Off: a favorite, or an item from your hotbar or EquipmentAndQuickSlots' quick slots, is never deleted, even " +
                "carried to the can. On: carried, it can be. What you wear or hold, and a quest item, never.");

            Message = settings.Bind(Part, "TrashMessage", true,
                "Message after deleting", 575,
                "A line top left, such as \"Deleted 3 kinds, 57 items\".");

            ImportHex = settings.Bind(Part, "TrashImportHex", true,
                "Take in my HexQuickStackStorage trash marks (once)", 586,
                "The first time the mod runs with this on, the items you marked as trash in HexQuickStackStorage " +
                "(BepInEx\\config\\HexQuickStackStorage.junk.txt, if it's there) are marked here too. That file is only read, " +
                "and only ever once. Your marks live in BepInEx\\config\\modprojects.marks.trash.txt.");

            Folds.Fold trash = Folds.Add(settings, Part, "TrashUnfolded", "Trash", 600, new ConfigEntryBase[]
            {
                On, Modifier, Ask,
            });
            Folds.AddGroup(settings, Part, "TrashMarksUnfolded", "Marks", 590, new ConfigEntryBase[]
            {
                Borders, BorderColor, InChestWindows, ImportHex,
            }, trash);
            Folds.AddGroup(settings, Part, "TrashCanUnfolded", "The trash can", 580, new ConfigEntryBase[]
            {
                Button, ButtonOffset, Carried, CarriedKept, Message,
            }, trash);
        }
    }
}
