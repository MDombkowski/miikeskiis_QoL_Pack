// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
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
    /// Since card B13 (2026-10-11, his night helping a friend: raising this range changed nothing for building, because
    /// Craft from Chests' own range wasn't 0, and they had to hunt for the setting that did): "Use this range in every
    /// mod" makes every mod use it while on, their own values kept for when it's off; a button sets every mod's own range
    /// to 0 for good; and the list under them names every chest distance in the pack, each mod's registered by the mod
    /// itself (Reaches, ReachesThrough, Elsewhere: a part never calls a module's code), with its value now.
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
                                             "metres (at least 5), unless \"Use this range in every mod\" is on there (card B13).";

        internal static ConfigEntry<float> Range { get; private set; }

        /// <summary>"Use this range in every mod" (card B13): while on, every mod's chest search uses Range.</summary>
        internal static ConfigEntry<bool> ForEveryMod { get; private set; }

        internal static ConfigEntry<bool> BeyondReach { get; private set; }

        internal static ConfigEntry<bool> LogProblems { get; private set; }

        /// <summary>Binds the shared settings (each module's BindSettings calls it, before its own).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            Range = settings.BindShared(Section, "Range", DefaultRange,
                "Chest range (metres)", 30,
                "How far from you a chest may be for the pack's mods to count, take from and put into it: Add to Cart, Craft " +
                "from Chests (crafting, building, filling stations), StoreStash's quick stack, Outfitter and Topper-Upper. Each " +
                "of them can have its own range instead (its own \"Chest range\"; 0 there follows this one), unless \"Use this " +
                "range in every mod\" below is on; the list below it names each one and what it's set to. Only chests your game has " +
                "loaded can be reached at all: about 128 to 192 m around you at the game's default simulation distance, so a " +
                "larger number finds no more there. On a server, a chest farther than about 64 to 128 m from you (it depends " +
                "on where you stand) needs \"Chests beyond the server's reach\" below, or Far Fetched on the server.",
                new AcceptableValueRange<float>(LeastRange, MostRange));

            // Off out of the box (his words: changing the shared value "shouldn't just go override all of the other values"
            // someone fine-tuned). Drawn with its button and the list (ChestRangeDrawer), across the window's width.
            ForEveryMod = settings.BindShared(Section, "RangeForEveryMod", false,
                "Use this range in every mod", 29,
                "On: every mod of the pack looks for chests as far as \"Chest range\" above, whatever its own range says; " +
                "each mod's own range is kept, and rules again once this is off. Off (out of the box): a mod uses the range " +
                "above only where its own range is 0. Below it: a button that sets every mod's own range to 0, so they all " +
                "follow the range above for good, and the list of every chest distance in the pack with what each is set to, " +
                "so you can find any of them to change it.",
                null, ChestRangeDrawer.Draw);
            var hints = ModuleSettings.HintsOf(ForEveryMod);   // the kit's hints (Jötunn has a class of that name too)
            if (hints != null)
            {
                hints.HideSettingName = true;   // the drawer writes the name, and its list takes the window's width
                hints.HideDefaultButton = true;
            }

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
                "server with a mod of its own that writes into chests, a rare timing (that mod writing the same far chest in " +
                "the same instant) could copy or lose one take.");

            LogProblems = settings.BindShared(Section, "LogProblems", true,
                "Write chest problems to the log", 10,
                "Whenever building, crafting, a fetch or a quick stack can't get what it needs from the chests, writes a line " +
                "to the game's log (BepInEx\\LogOutput.log) naming each chest it needed, how far it is and why it couldn't be " +
                "used then (whose game has it, whether the server hands it to you). For finding out why something failed. " +
                "Off: no such lines.");

            // What the pack changes in other mods made for speed: a section of its own (PerformanceMods).
            PerformanceMods.Bind(settings);
        }

        /// <summary>A mod's range in metres: the shared one while "Use this range in every mod" is on; else its own setting
        /// where that's at least LeastRange (a number between 0 and LeastRange counts as LeastRange), else the shared
        /// one.</summary>
        internal static float RangeOf(ConfigEntry<float> own)
        {
            if (ForEveryModOn)
            {
                return SharedRange;
            }
            float mine = own != null ? own.Value : 0f;
            if (mine >= LeastRange)
            {
                return mine > MostRange ? MostRange : mine;
            }
            if (mine > 0f)
            {
                return LeastRange;
            }
            return SharedRange;
        }

        /// <summary>The shared range in metres, within LeastRange and MostRange.</summary>
        internal static float SharedRange
        {
            get
            {
                float shared = Range != null ? Range.Value : DefaultRange;
                return shared < LeastRange ? LeastRange : shared > MostRange ? MostRange : shared;
            }
        }

        /// <summary>Whether "Use this range in every mod" is on (off until bound).</summary>
        internal static bool ForEveryModOn => ForEveryMod != null && ForEveryMod.Value;

        // ------------------------------------------------------------------------------------------- the list (card B13)

        /// <summary>How a chest distance answers to the shared range.</summary>
        internal enum Follows
        {
            /// <summary>A mod's own range: 0 follows the shared one; the switch makes it follow whatever it says.</summary>
            Own,

            /// <summary>A range that falls back to another of its mod's (0 follows that one); the switch makes it follow the
            /// shared one too.</summary>
            Through,

            /// <summary>A distance measured from somewhere else (a light, a station, an item): the switch and the button
            /// leave it alone, and its line reads a 0 as the shared range's number, measured from that place (Topper-Upper's,
            /// card B05). Register one here only if its 0 means that, or it never takes 0.</summary>
            Elsewhere,
        }

        /// <summary>One chest distance in the list.</summary>
        internal sealed class Distance
        {
            internal string Where;
            internal string Name;
            internal ConfigEntry<float> Entry;
            internal Follows How;
            internal string Note;
        }

        private static readonly List<Distance> distances = new List<Distance>();

        /// <summary>Every chest distance registered, in the order the mods registered them.</summary>
        internal static IList<Distance> Distances => distances;

        /// <summary>A mod's own chest range, which the shared one reaches (0 follows it; the switch makes it follow).
        /// <paramref name="where"/> is where it is in the window ("Craft from Chests").</summary>
        internal static void Reaches(string where, string name, ConfigEntry<float> own) => Register(where, name, own, Follows.Own, null);

        /// <summary>A range that falls back to another of its mod's when 0 (<paramref name="zeroFollows"/> names that one);
        /// the switch makes it follow the shared one.</summary>
        internal static void ReachesThrough(string where, string name, ConfigEntry<float> entry, string zeroFollows) =>
            Register(where, name, entry, Follows.Through, zeroFollows);

        /// <summary>A chest distance measured from somewhere else (<paramref name="from"/>: "from the light"), which the
        /// switch and the button leave alone; its line reads a 0 as the shared range's number, so register one here only if
        /// its 0 means that, or it never takes 0 (card B05).</summary>
        internal static void Elsewhere(string where, string name, ConfigEntry<float> entry, string from) =>
            Register(where, name, entry, Follows.Elsewhere, from);

        private static void Register(string where, string name, ConfigEntry<float> entry, Follows how, string note)
        {
            if (entry == null)
            {
                return;
            }
            foreach (Distance known in distances)
            {
                if (known.Entry == entry)
                {
                    return;   // bound again (a section bound twice hands back the same entry)
                }
            }
            distances.Add(new Distance { Where = where, Name = name, Entry = entry, How = how, Note = note });
        }

        /// <summary>The button: every mod's own range, and every range that falls back through one, set to 0, so all follow
        /// the shared one for good. Returns how many changed.</summary>
        internal static int FollowEverywhere()
        {
            int changed = 0;
            foreach (Distance each in distances)
            {
                if (each.How != Follows.Elsewhere && each.Entry.Value != 0f)
                {
                    each.Entry.Value = 0f;
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>How many settings the button would change.</summary>
        internal static int NotFollowing()
        {
            int count = 0;
            foreach (Distance each in distances)
            {
                if (each.How != Follows.Elsewhere && each.Entry.Value != 0f)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>One distance's line in the list: "Craft from Chests: Chest range (metres) = 40, its own: 40 m now".
        /// Plain characters only (the window's font may lack arrows).</summary>
        internal static string Line(Distance each) => Line(each.Where, each.Name, each.Entry.Value, each.How, each.Note, ForEveryModOn, SharedRange);

        /// <summary>The line on plain values (for a scratch test harness too).</summary>
        internal static string Line(string where, string name, float value, Follows how, string note, bool forEveryMod, float shared)
        {
            string head = $"{where}: {name} = {Number(value)}";
            switch (how)
            {
                case Follows.Own:
                    if (forEveryMod)
                    {
                        return $"{head}; {Number(shared)} m now (the switch above)";
                    }
                    return value == 0f ? $"{head}, follows the range above: {Number(shared)} m now" : $"{head}, its own: {Number(Clamp(value))} m now";
                case Follows.Through:
                    if (forEveryMod)
                    {
                        return $"{head}; {Number(shared)} m now (the switch above)";
                    }
                    return value == 0f ? $"{head}, follows {note}" : $"{head}, its own";   // each mod reads it its own way (review nit)
                default:
                    // 0: the shared range's number, measured from that place (card B05).
                    return value == 0f ? $"{head}, {note}: the range above, {Number(shared)} m now" : $"{head} m, {note}";
            }
        }

        private static float Clamp(float own) => own < LeastRange ? LeastRange : own > MostRange ? MostRange : own;

        private static string Number(float value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whether a chest beyond the server's reach may be claimed by this game (on until bound otherwise).</summary>
        internal static bool ClaimsFar => BeyondReach == null || BeyondReach.Value;

        /// <summary>Whether chest problems are written to the log (on until bound otherwise).</summary>
        internal static bool Logs => LogProblems == null || LogProblems.Value;
    }
}
