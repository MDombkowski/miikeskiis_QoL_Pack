using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>What other players may do at the chests he marked as his (card A80), as the setting says.</summary>
    internal enum OthersAtMyChests
    {
        /// <summary>They open and use them as any chest: nothing changes.</summary>
        OpenAsUsual,

        /// <summary>They may open one and see what's inside, but not take, put in or move anything.</summary>
        LookDontTouch,

        /// <summary>They can't open them.</summary>
        KeepThemOut,
    }

    /// <summary>
    /// Personal Stacker's settings for his marked chests (card A80, his feedback of 2026-10-09): the button in a chest's
    /// window that marks it as his or takes his mark off (MineButton), and what other players may do at the chests he
    /// marked (MineAccess writes it into each). Two groups in the "Personal Stacker" part, under the "Personal chests" fold
    /// in F1 (orders 790 to 778: Settings has every part's).
    /// Settings.Bind calls Bind once Personal Stacker's own are bound. Every one is read when it's needed.
    /// </summary>
    internal static class MineSettings
    {
        private const string Part = "Personal Stacker";

        internal static ConfigEntry<bool> Button { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<OthersAtMyChests> Others { get; private set; }

        internal static ConfigEntry<bool> ModsFollow { get; private set; }

        internal static void Bind(ModuleSettings settings, Folds.Fold personal)
        {
            // On out of the box at his word ("We need a container button below the Reserve Stock lock icon"), as Reserve
            // Stock's padlock is: it only marks a chest when clicked, and the key did the same before it.
            Button = settings.Bind(Part, "MineButton", true,
                "Personal chest button in the chest window", 789,
                "A button in an open chest's window, under Reserve Stock's padlock (in the padlock's place without it): click " +
                "it to mark the chest as yours, or to take your mark off, as the key does. Lit gold while the chest is yours. " +
                "On a chest someone else marked, it shows that it's theirs and only says whose it is.");

            ButtonOffset = settings.Bind(Part, "MineButtonOffset", Vector2.zero,
                "Personal chest button position (offset)", 788,
                "Moves that button from its place under the padlock, in the window's own units: x to the right, y up. The " +
                "setup and tidy buttons keep their places below its own.");

            Folds.AddGroup(settings, Part, "MineButtonUnfolded", "Button in the chest window", 790, new ConfigEntryBase[]
            {
                Button, ButtonOffset,
            }, personal);

            // Open as usual out of the box (MODULES.md rule 4): nothing changes for a friend who already marked chests.
            Others = settings.Bind(Part, "OthersAtMyChests", OthersAtMyChests.OpenAsUsual,
                "Other players at my marked chests", 779,
                "What other players may do at the chests you marked as yours. OpenAsUsual: they open and use them as any chest. " +
                "LookDontTouch: they can open one and see what's inside, but not take, put in or move anything. KeepThemOut: " +
                "they can't open them. You're never kept out, and the game's own Personal Chests stay as the game has them " +
                "(only their builder opens one). Saved on each chest you marked, so friends on the pack follow it; a chest " +
                "takes a new choice the next time your game holds it (when you're the first one near it, or open it). A player " +
                "without the pack, or with an older version of it, isn't stopped: the pack runs only in each player's own " +
                "game. Your other characters count as other players, as with the game's Personal Chests.");

            ModsFollow = settings.Bind(Part, "OthersModsToo", true,
                "Their mods follow it too", 778,
                "On: with LookDontTouch or KeepThemOut, other players' mods leave your marked chests alone too: the pack's " +
                "quick stack, Craft from Chests, Add to Cart, Outfitter and tidying (and HexQuickStackStorage's quick stack, " +
                "where Reserve Stock is on). Off: only their hands are kept out, and those mods may still take from and put " +
                "into your marked chests. With OpenAsUsual it changes nothing.");

            Folds.AddGroup(settings, Part, "OthersUnfolded", "Other players at your marked chests", 780, new ConfigEntryBase[]
            {
                Others, ModsFollow,
            }, personal);

            Settings.MineKey.SettingChanged += (sender, args) => MineButton.TipChanged();   // the tooltip names the key
            Others.SettingChanged += (sender, args) => MineButton.TipChanged();             // ...and what others may do
            ModsFollow.SettingChanged += (sender, args) => MineButton.TipChanged();

            // Which chests are his personal ones, for Craft from Chests' "My personal chests first" (card A80): in the
            // bundle every module shares the chest service's one copy, so his "Which chests are yours" count there too.
            ChestMine.KindsFrom = Personal.Kinds;
        }

        /// <summary>What the part calls the choice.</summary>
        internal static OthersMay ToPart(OthersAtMyChests choice) =>
            choice == OthersAtMyChests.KeepThemOut ? OthersMay.KeepOut : choice == OthersAtMyChests.LookDontTouch ? OthersMay.LookOnly : OthersMay.Open;

        /// <summary>The number his marked chests should hold, as the settings say now (ChestAccess.Encode).</summary>
        internal static int Wanted() => ChestAccess.Encode(ToPart(Others.Value), ModsFollow.Value);
    }
}
