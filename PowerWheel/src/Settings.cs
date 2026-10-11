using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace PowerWheel
{
    /// <summary>
    /// The player's settings (card A75's table). Alone, the mod keeps them in BepInEx\config\modprojects.powerwheel.cfg,
    /// in the section General; inside miikeskii's QoL Pack, in the bundle's file, in the section "PowerWheel". Every one
    /// takes effect at once: the mod reads them at each press, each opening of the wheel and each frame it shows. Their
    /// keys never change once released (MODULES.md rule 4).
    /// </summary>
    internal static class Settings
    {
        internal const float ShortestHold = 0.15f;
        internal const float LongestHold = 1f;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyChoice> Key { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> OwnKey { get; private set; }

        internal static ConfigEntry<float> HoldSeconds { get; private set; }

        internal static ConfigEntry<TapChoice> Tap { get; private set; }

        internal static ConfigEntry<LetGoChoice> LetGo { get; private set; }

        internal static ConfigEntry<LeftClickChoice> LeftClick { get; private set; }

        internal static ConfigEntry<RightClickChoice> RightClick { get; private set; }

        internal static ConfigEntry<NamesChoice> Names { get; private set; }

        internal static ConfigEntry<ShowChoice> WhichPowers { get; private set; }

        internal static ConfigEntry<EarnedChoice> Earned { get; private set; }

        internal static ConfigEntry<LockedLookChoice> LockedLook { get; private set; }

        internal static ConfigEntry<bool> PickWhileCooling { get; private set; }

        internal static ConfigEntry<bool> ShowCooldown { get; private set; }

        internal static ConfigEntry<bool> ShowWhatItDoes { get; private set; }

        internal static ConfigEntry<bool> MarkCurrent { get; private set; }

        internal static ConfigEntry<CursorChoice> CursorStarts { get; private set; }

        internal static ConfigEntry<MessageChoice> Message { get; private set; }

        internal static ConfigEntry<bool> Gamepad { get; private set; }

        internal static ConfigEntry<bool> LogKeys { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists a section's settings by their
            // Order, highest first.
            Enabled = settings.Enabled;

            Key = settings.Bind(ModuleSettings.General, "Key", KeyChoice.GamePowerKey,
                "Key", 41,
                "GamePowerKey: the game's \"Forsaken power\" key, F unless you rebound it in the game's controls (a rebind " +
                "carries over). Tap it to use your power, hold it for the wheel. OwnKey: the key in \"Own key\" opens the " +
                "wheel instead, and F works exactly as in the game.");

            OwnKey = settings.Bind(ModuleSettings.General, "OwnKey", new KeyboardShortcut(KeyCode.None),
                "Own key", 40,
                "The key that opens the wheel when \"Key\" is OwnKey (none at first). Choose one the game doesn't use: " +
                "W, A, S, D, E, R, Q, C, X, F, V, G, T, M, Tab, Space, Enter, F5, 1 to 8, Left Shift and Left Ctrl " +
                "are the game's own. A tap of it does what \"A tap of the key\" says.");

            HoldSeconds = settings.Bind(ModuleSettings.General, "HoldSeconds", 0.33f,
                "How long is a hold (seconds)", 39,
                "How long the key must be held for the wheel; let go sooner and it's a tap. 0.33 is the game's own hold " +
                "for its gamepad's wheel. With \"A tap of the key\" at UsesPower, a tap uses your power when you let go, " +
                "rather than as you press.",
                new AcceptableValueRange<float>(ShortestHold, LongestHold));

            Tap = settings.Bind(ModuleSettings.General, "Tap", TapChoice.UsesPower,
                "A tap of the key", 38,
                "UsesPower: a tap uses your forsaken power, as F does in the game. Nothing: the key only opens the wheel.");

            LetGo = settings.Bind(ModuleSettings.General, "LetGo", LetGoChoice.Picks,
                "Letting go of the key", 37,
                "With the wheel open. Picks: the power under the pointer becomes yours. PicksAndUses: it becomes yours and " +
                "is used at once. Closes: the wheel closes without a change; click a power while still holding the key to " +
                "pick it. Letting go on the power you already have, in the middle of the wheel, or without moving the " +
                "mouse never changes or uses anything, with any of the three: holding the key just to look is safe (a tap " +
                "does what \"A tap of the key\" says).");

            LeftClick = settings.Bind(ModuleSettings.General, "LeftClick", LeftClickChoice.Picks,
                "Left click", 36,
                "With the wheel open. Picks: the power clicked becomes yours. PicksAndUses: it becomes yours and is used at " +
                "once. The gamepad's confirm button does the same.");

            RightClick = settings.Bind(ModuleSettings.General, "RightClick", RightClickChoice.Closes,
                "Right click", 35,
                "With the wheel open. Closes: the wheel closes without a change, as in the game's own wheel. PicksAndUses: " +
                "the power under the pointer becomes yours and is used at once. Escape always closes without a change.");

            Names = settings.Bind(ModuleSettings.General, "Names", NamesChoice.OnHover,
                "Names", 34,
                "OnHover: a power's name shows in the middle of the wheel while the pointer is on it. Always: under every " +
                "icon as well.");

            WhichPowers = settings.Bind(ModuleSettings.General, "WhichPowers", ShowChoice.AllUnearnedLocked,
                "Which powers show", 33,
                "AllUnearnedLocked: every forsaken power; those you haven't earned are locked (how they look: \"How a locked " +
                "power looks\") and can't be picked. EarnedOnly: only the powers you have earned. AllAnyPickable: every " +
                "power, and any can be picked; the game then counts a power picked as yours for good, as if taken at its stone.");

            // A padlock alone, out of the box: his ask, 2026-10-10 (card B06).
            LockedLook = settings.Bind(ModuleSettings.General, "LockedLook", LockedLookChoice.PadlockOnly,
                "How a locked power looks", 32,
                "PadlockOnly: a padlock in its place, with no boss icon and no name; the middle of the wheel says \"Locked\" " +
                "and why. DimmedWithPadlock: its icon dimmed with a small padlock at its corner, and its name.");

            // Its trophy on its stone, out of the box: his ask, 2026-10-10 (card B06); 0.1.x's default was TakenAtStone.
            Earned = settings.Bind(ModuleSettings.General, "Earned", EarnedChoice.TrophyAtStone,
                "What counts as earned", 31,
                "TrophyAtStone: its boss's trophy is on its sacrificial stone in this world, whoever placed it; the game " +
                "keeps no record of that away from the stones, so the mod remembers each stone it has seen with its " +
                "trophy (on your character, for each world), and a power you took at its stone counts too. TakenAtStone: " +
                "you took the power at its stone with this character (the game's own record, kept on the character, so it " +
                "counts in every world). BossBeatenInWorld: its boss has been beaten in this world. Either: " +
                "taken at its stone or its boss beaten. The power you have always counts. Under BossBeatenInWorld, Either " +
                "or TrophyAtStone, a power you pick that you never took at its stone becomes this character's own for good, " +
                "as if taken there (the game records every power picked), and stays so whatever this setting says later. " +
                "Another mod's power, whose boss isn't known, counts under BossBeatenInWorld once taken at its stone.");
            settings.MovedDefault(Earned, EarnedChoice.TakenAtStone, 1);

            PickWhileCooling = settings.Bind(ModuleSettings.General, "PickWhileCooling", true,
                "Picking while it cools down", 30,
                "On: you can switch powers while your power cools down; the new one waits for the same time (one timer " +
                "for every power, as at the stones). Off: the wheel won't switch until your power is ready.");

            ShowCooldown = settings.Bind(ModuleSettings.General, "ShowCooldown", true,
                "Show the cooldown", 29,
                "On: \"Ready\", or the time left, under the name in the middle of the wheel.");

            ShowWhatItDoes = settings.Bind(ModuleSettings.General, "ShowWhatItDoes", false,
                "Show what it does", 28,
                "On: beside the wheel, what the power under the pointer does, as its stone tells it.");

            MarkCurrent = settings.Bind(ModuleSettings.General, "MarkCurrent", true,
                "Mark the power you have", 27,
                "On: the power you have is marked in the wheel the way the game's own wheel marks the hammer in your hand.");

            CursorStarts = settings.Bind(ModuleSettings.General, "CursorStarts", CursorChoice.OnCurrentPower,
                "Pointer starts", 26,
                "OnCurrentPower: the mouse pointer starts on the power you have. InTheCentre: in the middle of the wheel. " +
                "Either way, letting go without moving the mouse keeps the power you have and never uses it.");

            Message = settings.Bind(ModuleSettings.General, "Message", MessageChoice.Centre,
                "Message", 25,
                "Where a message says which power you picked, or why a power can't be picked: Centre, TopLeft (with the " +
                "power's icon, except for a power shown as a padlock) or None.");

            Gamepad = settings.Bind(ModuleSettings.General, "Gamepad", true,
                "Gamepad: hold the power button", 24,
                "On: with a gamepad in the classic layout, hold D-pad down for the wheel and aim with the right stick; " +
                "let go of D-pad down while the stick still points at a power (a stick at rest picks nothing), or press " +
                "RT. A tap does what \"A tap of the key\" says. The wheel waits while LT is held. Off: the gamepad's power " +
                "button works as in the game. In the other layouts the power stays on both sticks pressed together, as in " +
                "the game.");

            LogKeys = settings.Bind(ModuleSettings.General, "LogKeys", false,
                "Write what it sees of the key to the log", 23,
                "For testing: a line in BepInEx\\LogOutput.log each time the key goes down, is held long enough or is let " +
                "go, saying what the mod made of it: a tap, the wheel opening, or why it left the key to the game or " +
                "didn't open the wheel. Off: no lines.");
        }

        /// <summary>The hold time, kept within its range whatever the file says.</summary>
        internal static float Hold => Mathf.Clamp(HoldSeconds.Value, ShortestHold, LongestHold);
    }
}
