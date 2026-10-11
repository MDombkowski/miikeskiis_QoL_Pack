using BepInEx.Configuration;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>What saving to a stand takes from him.</summary>
    internal enum StandSaves
    {
        EverythingICarryAndWear,
        WhatIWear,
        TheCellsIPicked,
    }

    /// <summary>How a new stand's loadout gets its name.</summary>
    internal enum StandNaming
    {
        Ask,
        UseTheNameFilledIn,
    }

    /// <summary>What saving to a stand does besides recording the loadout there.</summary>
    internal enum StandSaving
    {
        RecordsIt,
        RecordsItAndHangsMyGearNow,
    }

    /// <summary>Where what he wore goes when he puts a stand's loadout on.</summary>
    internal enum WhatIWoreGoes
    {
        Home,
        OntoThisStand,
    }

    /// <summary>The equip mode at a stand.</summary>
    internal enum StandMode
    {
        AsOutfittersSetting,
        FullyReplace,
        SwapGearOnly,
        SwapGearAndAddTheRest,
    }

    /// <summary>Which armour stands he may make loadout stands.</summary>
    internal enum WhichStands
    {
        AnyICanUse,
        OnlyOnesIBuilt,
    }

    /// <summary>What his keys do at a stand someone else saved.</summary>
    internal enum OthersStands
    {
        LeaveThemAlone,
        IMayPutTheirLoadoutOn,
    }

    /// <summary>What moving a loadout onto a stand that holds another loadout of his does.</summary>
    internal enum OntoAnother
    {
        SwapThem,
        AskThenReplace,
    }

    /// <summary>Which marked stands show, for this game only, the pieces of their loadout that aren't on them (job 30,
    /// Session 3: A46's decision 48).</summary>
    internal enum ShowLacks
    {
        OnMyStands,
        OnEveryMarkedStand,
        Off,
    }

    /// <summary>
    /// Loadout Stands' settings (card A46's table, and what the build added; his rule 3), one folded section of Outfitter's,
    /// "Loadout Stands", kept in "Outfitter - Loadout Stands" inside the pack (alone, its own section of
    /// modprojects.outfitter.cfg), with the orders 299 down kept for it. Keys and part are permanent once released. Every one
    /// is read when it's used, so a change works at once.
    /// </summary>
    internal static class StandSettings
    {
        internal const string Part = "Loadout Stands";

        internal static ConfigEntry<bool> On { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> EquipKey { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> SaveKey { get; private set; }
        internal static ConfigEntry<StandSaves> Saves { get; private set; }
        internal static ConfigEntry<StandNaming> Naming { get; private set; }
        internal static ConfigEntry<StandSaving> Saving { get; private set; }
        internal static ConfigEntry<WhatIWoreGoes> WhatIWore { get; private set; }
        internal static ConfigEntry<float> HomeRange { get; private set; }
        internal static ConfigEntry<StandMode> Mode { get; private set; }
        internal static ConfigEntry<bool> NearFirst { get; private set; }
        internal static ConfigEntry<float> NearRange { get; private set; }
        internal static ConfigEntry<WhichStands> Which { get; private set; }
        internal static ConfigEntry<OthersStands> Others { get; private set; }
        internal static ConfigEntry<bool> PanelKey { get; private set; }
        internal static ConfigEntry<float> ListRange { get; private set; }
        internal static ConfigEntry<bool> MoveGear { get; private set; }
        internal static ConfigEntry<OntoAnother> Onto { get; private set; }
        internal static ConfigEntry<bool> ShowName { get; private set; }
        internal static ConfigEntry<bool> HoverName { get; private set; }
        internal static ConfigEntry<bool> HoverKeys { get; private set; }
        internal static ConfigEntry<bool> HoverLacks { get; private set; }
        internal static ConfigEntry<bool> HoverEveryStand { get; private set; }
        internal static ConfigEntry<int> HoldSeconds { get; private set; }
        internal static ConfigEntry<bool> ChangePose { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> PoseKey { get; private set; }

        // Job 30, Session 3 (0.4.0): personal stands, stands lending gear, the window's Equip through a stand, and a stand
        // showing what its loadout lacks.
        internal static ConfigEntry<KeyboardShortcut> PersonalKey { get; private set; }
        internal static ConfigEntry<bool> BecomePersonal { get; private set; }
        internal static ConfigEntry<bool> Lend { get; private set; }
        internal static ConfigEntry<float> LendRange { get; private set; }
        internal static ConfigEntry<bool> WindowEquip { get; private set; }
        internal static ConfigEntry<ShowLacks> Show { get; private set; }
        internal static ConfigEntry<bool> HoverPersonal { get; private set; }

        internal static void Bind(SettingsBinder b)
        {
            b.BeginFold(Part);
            On = b.Add(Part, "Stands", true, "Loadout Stands",
                "The game's own armour stand as a loadout's stand: marked with Save (below), it wears that loadout's gear while " +
                "you don't, and its key puts the loadout on (the rest from the chests nearby) and hangs it back. Nothing changes " +
                "on a stand nobody marked. Off: marked stands are ordinary stands again, their gear still on them; the keys do " +
                "nothing and E works as in the game.");
            EquipKey = b.Add(Part, "EquipKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), "Put on a stand's loadout (again: hang it back)",
                "Pointing at a marked stand: its gear comes to you and the loadout goes on, the rest from the chests nearby (as " +
                "Outfitter's window does), and what you wore goes home (below). The same key again, once it's on you, hangs it " +
                "back on the stand. While this key is held, E doesn't also take everything off the stand. Left Alt + E as " +
                "Reserve Stock's key on chests; Wardrobe and QuickswapArmorstand use it on stands too (switch one off).");
            SaveKey = b.Add(Part, "SaveKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt, KeyCode.LeftShift), "Save what I carry and wear to a stand",
                "Pointing at an armour stand: what you carry and wear becomes a loadout in Outfitter's list, and the stand its " +
                "stand. On a stand that already holds your loadout it asks to save over it (or to make it an ordinary stand " +
                "again); on a stand someone else saved it asks first.");
            PersonalKey = b.Add(Part, "PersonalKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt), "Personal stand key",
                "Pointing at an armour stand you may use: makes it your personal stand, or an ordinary stand again (the stand " +
                "bar's Personal does the same). Other players' Outfitter keys and buttons leave your personal stand alone: " +
                "their save, put on, hang back, pose, rename, assign and moving a loadout onto it. The game's own E isn't " +
                "stopped (the ward decides who may touch a stand). Only you can take it off. Left Alt + H as StoreStash's key " +
                "that marks a chest yours (it does nothing at a stand); the game doesn't use H.");
            Saves = b.Add(Part, "Saves", StandSaves.EverythingICarryAndWear, "Saving to a stand takes",
                "EverythingICarryAndWear (your words: \"save current inventory to that stand\"). WhatIWear: only the pieces you " +
                "wear (pair it with an equip mode for stands of SwapGearOnly or SwapGearAndAddTheRest: with FullyReplace " +
                "everything else you carry goes into the chests at each put-on). TheCellsIPicked: the cells you picked with " +
                "Right Alt (as Outfitter's Save does), else everything.");
            Naming = b.Add(Part, "Naming", StandNaming.Ask, "Name a new stand's loadout",
                "Ask: a name box opens with a name filled in (Enter keeps it). UseTheNameFilledIn: that name, without asking. " +
                "Either way it's a loadout like any other: rename it in Outfitter's window.");
            Saving = b.Add(Part, "Saving", StandSaving.RecordsIt, "Saving to a stand",
                "RecordsIt: the loadout is recorded there and nothing is taken off you; its gear goes onto the stand the next " +
                "time you hang it back (the key above). RecordsItAndHangsMyGearNow: the pieces you wear go onto the stand at once.");
            BecomePersonal = b.Add(Part, "BecomePersonal", true, "Stands I save to become personal",
                "On: saving to a stand, assigning a loadout to it or moving one onto it also makes it your personal stand (as " +
                "the personal stand key does), unless it's someone else's. Off: only the key and the stand bar's Personal do.");
            WhatIWore = b.Add(Part, "WhatIWoreGoes", WhatIWoreGoes.Home, "What I wore goes",
                "When you put a stand's loadout on. Home: a piece of the loadout of another of your own stands goes back onto that " +
                "stand, when it's near (below) and you may use it; everything else goes where Outfitter sends gear (the chests " +
                "nearby). OntoThisStand: what you wore goes onto this stand, a plain swap (what the stand can't hold goes where " +
                "Outfitter sends gear).");
            HomeRange = b.Add(Part, "HomeRange", 30f, "Its own stand counts within (metres)",
                "How near another loadout's stand must be for its pieces to go back onto it. Farther, they go where Outfitter " +
                "sends gear.", new AcceptableValueRange<float>(5f, 60f));
            Lend = b.Add(Part, "Lend", true, "Take missing pieces from my other stands",
                "On: putting a stand's loadout on, a piece of it that is neither on the stand, nor with you, nor in the chests " +
                "nearby comes off another of your own stands nearby (below) that holds one, the nearest first: one Lox cape " +
                "can serve three stands. If you cancel, it goes back onto the stand it came from; a piece you don't end up " +
                "wearing goes back there too. Hung back later, it goes onto the stand you hang it on. Off: such a piece is " +
                "missing, at the stand and in the window's Equip through it.");
            LendRange = b.Add(Part, "LendRange", 30f, "Other stands lend within (metres)",
                "How near the stand you put on at another of your stands must be to lend it a piece.",
                new AcceptableValueRange<float>(5f, 60f));
            Mode = b.Add(Part, "Mode", StandMode.AsOutfittersSetting, "Equip mode at a stand",
                "AsOutfittersSetting: Outfitter's own \"Equip mode\" (Equipping). Or one for stands: FullyReplace, SwapGearOnly, " +
                "SwapGearAndAddTheRest (as Outfitter's setting describes them).");
            NearFirst = b.Add(Part, "NearFirst", true, "Chests near the stand first",
                "On: what the loadout needs beyond the stand's own gear comes from the chests next to the stand first (below), " +
                "then from Outfitter's chests. Off: from Outfitter's chests alike.");
            NearRange = b.Add(Part, "NearRange", 3f, "Near the stand (metres)",
                "How near a chest must be to the stand to come first.", new AcceptableValueRange<float>(0f, 10f));
            Which = b.Add(Part, "WhichStands", WhichStands.AnyICanUse, "Which stands can be loadout stands",
                "AnyICanUse: any armour stand you may use (the game's ward check). OnlyOnesIBuilt: only stands you built.");
            Others = b.Add(Part, "OthersStands", OthersStands.LeaveThemAlone, "Stands someone else saved",
                "LeaveThemAlone: your key does nothing at a stand another player (or another of your characters) saved. " +
                "IMayPutTheirLoadoutOn: you may put it on (their stand's gear, and the rest from your chests) and hang it back. " +
                "The game's own E takes a stand's gear either way, as for any stand.");
            PanelKey = b.Add(Part, "PanelKey", true, "Outfitter's key at a stand opens it on the stand",
                "On: Outfitter's window key (L), pointing at an armour stand you may use, opens the window with a bar for that " +
                "stand: its name (Rename), the loadout it holds (Unassign), whether it's personal (Personal), and Assign to " +
                "stand for the loadout picked on the left. The loadout list shows each loadout's stand; Move to stand... moves " +
                "the loadout to another stand.");
            WindowEquip = b.Add(Part, "WindowEquip", true, "Equip in the window puts a stand's loadout on at its stand",
                "On: Equip in Outfitter's window, for a loadout on one of your stands within \"Stands listed within\" (below), " +
                "does what the put-on key does at that stand, without walking there: the stand's pieces come to you (and what " +
                "your other stands lend), the rest comes from the chests around the stand (those next to it first), and what " +
                "you wore goes home. It only ever puts on, never hangs back; the window's mode buttons choose the mode. Off: " +
                "the window's Equip takes from the chests around you only, and a stand's pieces count as missing.");
            ListRange = b.Add(Part, "ListRange", 60f, "Stands listed within (metres)",
                "The stands the window lists and moves loadouts between: those within this distance of you (only stands near " +
                "you can be changed).", new AcceptableValueRange<float>(10f, 150f));
            MoveGear = b.Add(Part, "MoveGear", true, "Moving a loadout moves its gear",
                "On: moving a loadout to another stand moves its pieces on the old stand straight onto the new one, each into " +
                "its place (a piece whose place there is taken stays, and you're told). Off: only the loadout moves; its gear " +
                "stays where it is.");
            Onto = b.Add(Part, "OntoAnother", OntoAnother.SwapThem, "Moving onto a stand that holds another loadout",
                "SwapThem: the two loadouts trade stands, their gear too (the other's gear comes through your inventory, so " +
                "it needs room for it: all or nothing). AskThenReplace: asks first, then the other loadout leaves the stand " +
                "(its gear comes to you; the loadout stays in your list).");
            ShowName = b.Add(Part, "ShowName", true, "A stand's name replaces \"Armor stand\"",
                "On: pointing at a stand you renamed (the stand bar's Rename) shows its name where the game shows \"Armor " +
                "stand\". The name is kept on the stand: friends with the pack see it too; a game without it shows the game's.");
            Show = b.Add(Part, "ShowLacks", ShowLacks.OnMyStands, "Show what a stand's loadout lacks on it",
                "A marked stand draws, in your game only, each piece of its loadout that isn't on it now (you wear it, another " +
                "stand has it, it's in a chest) in its place, so it looks like its loadout from the moment it's saved, and " +
                "three stands can all show one Lox cape. Nothing is on the stand in truth: E takes only what's really there, " +
                "a friend sees the stand as it is, and pointing at it says \"Shown, not on it\". OnMyStands: the stands you " +
                "saved. OnEveryMarkedStand: friends' too. Off: every stand shows only what it holds.");
            HoverName = b.Add(Part, "HoverName", true, "Hover: the loadout's name",
                "Pointing at a marked stand: a line with its loadout's name (and whose, when it isn't yours).");
            HoverKeys = b.Add(Part, "HoverKeys", true, "Hover: what the key does",
                "Pointing at a marked stand: the key, and whether it puts the loadout on or hangs it back.");
            HoverLacks = b.Add(Part, "HoverLacks", true, "Hover: what it lacks",
                "Pointing at a marked stand: the loadout's pieces neither on the stand nor on you (taken by hand, say).");
            HoverPersonal = b.Add(Part, "HoverPersonal", true, "Hover: whose personal stand",
                "Pointing at a personal stand: \"Personal: yours\", or whose it is (\"Personal: Astrid's\").");
            HoverEveryStand = b.Add(Part, "HoverEveryStand", false, "Show the save key on every stand",
                "On: pointing at any armour stand you may use shows the save key. Off: only marked stands show lines.");
            HoldSeconds = b.Add(Part, "HoldSeconds", 3, "Hold the stand after a swap (seconds)",
                "After a swap the stand stays yours this long, so another player's game has your change first: their E does " +
                "nothing until then (their next press works), and an item they hang on it by hand goes on once their game " +
                "gets the stand (the game rechecks the item and the place then, so nothing is lost).", new AcceptableValueRange<int>(1, 10));
            ChangePose = b.Add(Part, "ChangePose", false, "Change a marked stand's pose",
                "On: the key below, pointing at a marked stand you may use, turns it to its next pose (the game's armour stand " +
                "has 15; the game itself offers no way).");
            PoseKey = b.Add(Part, "PoseKey", new KeyboardShortcut(KeyCode.Backspace, KeyCode.LeftAlt), "Pose key",
                "With \"Change a marked stand's pose\" on. Neither the game, nor EquipmentAndQuickSlots (its quick slots are on Z, " +
                "V, B, Y, U and I), nor the pack's other mods use Backspace with Left Alt (Terraformer's reset is Right Ctrl + " +
                "Backspace, with the hoe out).");
            b.EndFold();
        }
    }
}
