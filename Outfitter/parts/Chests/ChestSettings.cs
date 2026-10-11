// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using BepInEx.Configuration;
using QoLMods.Kit;

namespace QoLMods.Chests
{
    /// <summary>
    /// The chest settings every mod that uses chests must agree on, in one section of their own, "Chests (every mod)"
    /// (since 2026-10-08, card A69). He switched "Chests beyond the server's reach" on in three mods and it still failed:
    /// the fourth, Craft from Chests, builds and crafts, and he couldn't be expected to know that (his words: "I definitely
    /// wouldn't expect a player to know that they need to set all three of these configs appropriately for it to work as
    /// they want it to"). So the switch is one setting for the whole pack, and so is the range, which each mod may still
    /// set for itself (its own "Chest range": 0 follows this one). Each module binds them in its BindSettings
    /// (ModuleSettings.BindShared): in the bundle that is one entry, whichever module binds it first; in a single release,
    /// that mod's own. Read each value when it's needed, never cached (his rule 3).
    /// </summary>
    internal static class ChestSettings
    {
        /// <summary>The section's name, the same in the bundle and in every single release.</summary>
        internal const string Section = "Chests (every mod)";

        /// <summary>The farthest any range may reach, in metres.</summary>
        internal const float MostRange = 250f;

        /// <summary>The nearest a mod's own range may be set to, in metres (below it, 0 means "follow the shared one").</summary>
        internal const float LeastRange = 5f;

        /// <summary>The range out of the box: his own, as every chest mod had it (card A63).</summary>
        internal const float DefaultRange = 150f;

        /// <summary>The words every mod's own range setting ends with.</summary>
        internal const string OwnRangeNote = "0 (out of the box): the range in \"" + Section + "\". Another number: this mod's own range, in " +
                                             "metres (at least 5).";

        internal static ConfigEntry<float> Range { get; private set; }

        internal static ConfigEntry<bool> BeyondReach { get; private set; }

        internal static ConfigEntry<bool> LogProblems { get; private set; }

        /// <summary>Binds the shared settings (each module's BindSettings calls it, before its own).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            Range = settings.BindShared(Section, "Range", DefaultRange,
                "Chest range (metres)", 30,
                "How far from you a chest may be for the pack's mods to count, take from and put into it: Add to Cart, Craft " +
                "from Chests (crafting, building, filling stations), StoreStash's quick stack and Outfitter. Each of them can " +
                "have its own range instead (its own \"Chest range\"; 0 there follows this one). Only chests your game has " +
                "loaded can be reached at all: about 128 to 192 m around you at the game's default simulation distance, so a " +
                "larger number finds no more there. On a server, a chest farther than about 64 to 128 m from you (it depends " +
                "on where you stand) needs \"Chests beyond the server's reach\" below, or Far Fetched on the server.",
                new AcceptableValueRange<float>(LeastRange, MostRange));

            // The key the four mods' own switches had (card A64). Theirs lay in each mod's own section, so a single release's
            // file brings no value over to it; the new default is on in any case.
            BeyondReach = settings.BindShared(Section, "ClaimFarChests", true,
                "Chests beyond the server's reach", 20,
                "On a server, the game hands a chest only to a player within about 64 to 128 m of it (it depends on where " +
                "you stand), and only that player's game can open it for anyone, so a chest farther away with nobody near it " +
                "answers no one. On: your own game claims such a chest when a mod needs it, as the game's own portals do " +
                "(never a cart's or a ship's), and changes it only in the half second the server allows after each of its " +
                "two-second rounds: crafting and fetching wait for it by themselves, and building places only when your click " +
                "lands in that moment, so it can take a few clicks. Off: your game never claims such chests; the pack still " +
                "shows what they hold, and says so when you try to use it. With Far Fetched on the server, the server hands " +
                "such chests to you itself, and this setting hardly matters. Alone or hosting, it changes nothing. On a " +
                "server running ServersideQoL, a rare timing (that mod writing the same far chest in the same instant) could " +
                "copy or lose one take.");

            LogProblems = settings.BindShared(Section, "LogProblems", true,
                "Write chest problems to the log", 10,
                "Whenever building, crafting, a fetch or a quick stack can't get what it needs from the chests, writes a line " +
                "to the game's log (BepInEx\\LogOutput.log) naming each chest it needed, how far it is and why it couldn't be " +
                "used then (whose game has it, whether the server hands it to you). For finding out why something failed. " +
                "Off: no such lines.");

            // What the pack changes in other mods made for speed: a section of its own (PerformanceMods).
            PerformanceMods.Bind(settings);
        }

        /// <summary>A mod's range in metres: its own setting where that's at least LeastRange (a number between 0 and
        /// LeastRange counts as LeastRange), else the shared one.</summary>
        internal static float RangeOf(ConfigEntry<float> own)
        {
            float mine = own != null ? own.Value : 0f;
            if (mine >= LeastRange)
            {
                return mine > MostRange ? MostRange : mine;
            }
            if (mine > 0f)
            {
                return LeastRange;
            }
            float shared = Range != null ? Range.Value : DefaultRange;
            return shared < LeastRange ? LeastRange : shared > MostRange ? MostRange : shared;
        }

        /// <summary>Whether a chest beyond the server's reach may be claimed by this game (on until bound otherwise).</summary>
        internal static bool ClaimsFar => BeyondReach == null || BeyondReach.Value;

        /// <summary>Whether chest problems are written to the log (on until bound otherwise).</summary>
        internal static bool Logs => LogProblems == null || LogProblems.Value;
    }
}
