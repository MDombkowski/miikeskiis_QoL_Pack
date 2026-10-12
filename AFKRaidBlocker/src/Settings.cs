using BepInEx.Configuration;
using QoLMods.Kit;

namespace AFKRaidBlocker
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.afkraidblocker.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "AFK Raid Blocker". Every one takes
    /// effect at once, also when changed in game with a configuration manager: the mod reads them every frame. Their keys
    /// never change once released.
    /// </summary>
    internal static class Settings
    {
        internal const int MostMinutes = 60;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<int> AwayAfterMinutes { get; private set; }

        internal static ConfigEntry<InputCounts> InputCounts { get; private set; }

        internal static ConfigEntry<bool> EmotesCount { get; private set; }

        internal static ConfigEntry<string> Emotes { get; private set; }

        internal static ConfigEntry<bool> ChairsCount { get; private set; }

        internal static ConfigEntry<bool> BedsCount { get; private set; }

        internal static ConfigEntry<SeatTiming> SeatTiming { get; private set; }

        internal static ConfigEntry<HeldOff> HeldOff { get; private set; }

        internal static ConfigEntry<int> NearbyMetres { get; private set; }

        internal static ConfigEntry<bool> ShowIcon { get; private set; }

        internal static ConfigEntry<bool> ShowMessages { get; private set; }

        internal static ConfigEntry<MessagePlace> Message { get; private set; }

        internal static ConfigEntry<bool> ShowMyMark { get; private set; }

        internal static ConfigEntry<bool> SeeMarks { get; private set; }

        internal static ConfigEntry<string> MarkText { get; private set; }

        internal static ConfigEntry<bool> Log { get; private set; }

        /// <summary>The away rules as the settings say right now.</summary>
        internal static Rules Rules => new Rules
        {
            Minutes = AwayAfterMinutes.Value,
            Input = InputCounts.Value,
            Emotes = EmotesCount.Value,
            Chairs = ChairsCount.Value,
            Beds = BedsCount.Value,
            Timing = SeatTiming.Value,
        };

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            AwayAfterMinutes = settings.Bind(ModuleSettings.General, "AwayAfterMinutes", 5,
                "Away after this many minutes", 22,
                "After this many minutes without moving (and, with the next setting, without a key, the mouse or a " +
                "gamepad either), you count as away from the keyboard, and raids no longer start on you while nobody " +
                "near you is playing. 0 for never: then only sitting or lying down makes you away.",
                new AcceptableValueRange<int>(0, MostMinutes));

            InputCounts = settings.Bind(ModuleSettings.General, "InputCounts", AFKRaidBlocker.InputCounts.MovementKeysAndMouse,
                "What counts as being there", 21,
                "Movement, keys and the mouse: pressing a key, a mouse button or a gamepad button, moving the mouse or a " +
                "stick, or the wheel keeps you there (only while the game's window has the focus; a key left held down " +
                "counts once, as it's pressed). Movement only: only moving your character does; you can then look " +
                "around, open the map or type in the console and still go away.");

            EmotesCount = settings.Bind(ModuleSettings.General, "EmotesCount", true,
                "Sitting or lying down by an emote counts", 20,
                "On: sitting or lying down by an emote (the Sit key, /sit, /rest or /relax) makes you away, your way of " +
                "saying you've stepped away. You're back when you stand up.");

            Emotes = settings.Bind(ModuleSettings.General, "Emotes", "sit, rest, relax",
                "Which emotes count", 19,
                "The emotes that make you away, by their chat names, separated by commas: sit (sitting on the ground), " +
                "rest and relax (lying down). An emote counts while you hold its pose: sit until you get up, rest and " +
                "relax until you get up or a hit knocks you up; kneel can be added too; a short one such as wave would " +
                "count only while it plays.");

            ChairsCount = settings.Bind(ModuleSettings.General, "ChairsCount", false,
                "Sitting in a chair counts", 18,
                "On: sitting in a chair, on a bench (a ship's too) or a throne makes you away, as an emote does.");

            BedsCount = settings.Bind(ModuleSettings.General, "BedsCount", false,
                "Lying in a bed counts", 17,
                "On: lying in a bed makes you away, as an emote does.");

            SeatTiming = settings.Bind(ModuleSettings.General, "SeatTiming", AFKRaidBlocker.SeatTiming.AtOnce,
                "Sitting or lying makes you away", 16,
                "At once: you're away as soon as you sit or lie down by a way that counts. After the same minutes: " +
                "only once you've sat or lain there for the minutes above, whatever you press meanwhile (with 0 " +
                "minutes, at once).");

            HeldOff = settings.Bind(ModuleSettings.General, "HeldOff", AFKRaidBlocker.HeldOff.RaidsThatNeedABase,
                "What's held off while you're away", 15,
                "Raids that need a base: the game's raids on a base (trolls, the Fuling army, the Elder's greydwarfs " +
                "and the rest) don't start on you. Every random event: no random event starts on you, the wolves of " +
                "the mountains and plains included (they come without a base). A raid already under way goes on.");

            NearbyMetres = settings.Bind(ModuleSettings.General, "NearbyMetres", Nearby.RaidReach,
                "Someone still playing within (m) lets raids come", 14,
                "Raids are held off only while everyone near you is away too: while a player who isn't away (or who " +
                "doesn't have this mod) is within this many metres of you, raids can start on you as usual, and reach " +
                "them. 96 is a raid's reach. It counts the players your game has loaded around you (at the game's " +
                "usual simulation distance, everyone within 128 m), and anyone farther who shares their position on " +
                "the map. 0: held off whoever is near.",
                new AcceptableValueRange<int>(0, Nearby.MostMetres));

            ShowIcon = settings.Bind(ModuleSettings.General, "ShowIcon", true,
                "Show the AFK sign", 13,
                "On: the whole time you're away, a small \"AFK\" sign shows at the top of your screen, over the " +
                "inventory, chests, the crafting and build menus, the map and every other window, and it goes the " +
                "moment you're back. It hides while you hide the game's HUD (Ctrl+F3).");

            ShowMessages = settings.Bind(ModuleSettings.General, "ShowMessages", true,
                "Show the away and back messages", 12,
                "On: a short line says that raids are held off as you go away, and that they can come again as you " +
                "come back, where the next setting says. Off: no line.");

            Message = settings.Bind(ModuleSettings.General, "Message", MessagePlace.Centre,
                "Where the away and back messages show", 11,
                "Where the line going away and coming back shows: in the middle of the screen, top left, or nowhere.");

            ShowMyMark = settings.Bind(ModuleSettings.General, "ShowMyMark", false,
                "Others see you're away", 10,
                "On: while you're away, players who have this mod see a mark beside your name over your head. Players " +
                "without it see nothing. Off or on, your game tells other players' copies of this mod whether you're " +
                "away (for \"Someone still playing within\"); this only decides whether the mark shows.");

            SeeMarks = settings.Bind(ModuleSettings.General, "SeeMarks", true,
                "Show other players' away mark", 9,
                "On: the mark of a player who is away and lets others see it shows beside their name over their head.");

            MarkText = settings.Bind(ModuleSettings.General, "MarkText", "AFK",
                "The away mark's text", 8,
                "What the mark beside a name says, in brackets and grey. Empty for no mark.");

            Log = settings.Bind(ModuleSettings.General, "Log", false,
                "Write to the log going away and coming back", 7,
                "On: a line in BepInEx's log each time you go away or come back, and each time a player still playing " +
                "comes near or leaves while you're away, saying why and what's held off.");
        }
    }
}
