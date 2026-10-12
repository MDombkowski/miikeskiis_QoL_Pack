using QoLMods.Naps;
using QoLMods.Serving;

namespace NapTime
{
    /// <summary>
    /// Naps in a world this game runs itself, alone or with the game's Start Server (card B29, his pick of 2026-10-12: the
    /// QoL Pack does it). There the game decides sleeping in this game, so the nap's skip runs here (the part's NapSkip, the
    /// same code Nap Time (Server) runs), by the "Your own world" settings, and friends who join with the pack hear it as
    /// they would hear Nap Time (Server) (the part's HostPack answers them under its id). With a plugin of the server
    /// pack's kind in this game, that plugin decides instead (Server), and this serves nothing.
    /// </summary>
    internal static class Own
    {
        /// <summary>Whether Nap Time serves the naps itself now: on, in a world this player's game serves (never a dedicated
        /// server), with no plugin of the server pack's kind here, "Naps in your own world" on, and the part's patch in place.</summary>
        internal static bool Serving =>
            Module.IsOn && ServerHalves.OwnWorld && !ServerHalves.KitHere && Settings.OwnWorld.Value && NapSkip.Ready;

        /// <summary>What the nap's skip serves now (NapSkip.Rules), or null.</summary>
        internal static Served Rules() => Serving ? Settings.OwnServed() : null;

        /// <summary>The words under the Enabled switch in the settings window: what applies right now (Where).</summary>
        internal static string Line()
        {
            if (!Module.Running)
            {
                return "Not running (the game's log says why): your bed is the game's own everywhere.";
            }
            if (!Module.IsOn)
            {
                return "Switched off: your bed is the game's own everywhere.";
            }
            Place place = Where.Now(NapRules.ServerModuleId);
            switch (place)
            {
                case Place.NoWorld:
                    return "In a world, this says whether you can nap there: in a world your own game runs (alone, or the " +
                           "game's Start Server) by \"Your own world\" below, and on someone else's server by that server.";
                case Place.Own:
                case Place.OwnUnheard:
                    return OwnLine(place == Place.OwnUnheard);
                case Place.OwnServerHalf:
                    string here = $"Your own world: {ServerHalves.Names} {ServerHalves.Are} in your game and decide{(ServerHalves.Are == "is" ? "s" : string.Empty)} here";
                    if (ServerHalves.Find("NapTimeServer.Module") == null)
                    {
                        return here + ", without Nap Time (Server), so your bed is the game's own (\"Your own world\" below waits).";
                    }
                    Served half = Server.Now;
                    if (half == null)
                    {
                        return here + ", with its Nap Time (Server) off, so your bed is the game's own (\"Your own world\" below waits).";
                    }
                    return CanNap(half)
                        ? here + $": its Nap Time (Server) runs the naps ({Hours(half)}) by its own settings, not \"Your own world\" below."
                        : here + ": its Nap Time (Server)'s hours allow no nap, so your bed is the game's own (\"Your own world\" below waits).";
                case Place.Asking:
                    return "Asking this server whether it runs naps...";
                case Place.ServerRuns:
                    Served served = Server.Now;
                    if (served == null)
                    {
                        return "This server runs naps: you can nap here.";
                    }
                    return CanNap(served)
                        ? $"This server runs naps: you can nap here ({Hours(served)})."
                        : "This server runs naps, but its hours allow none: your bed is the game's own here.";
                case Place.ServerWithout:
                    return "This server answers without naps, or with them off: your bed is the game's own here.";
                default:
                    return "This server doesn't run naps (no answer from miikeskii's Server-Side QoL Pack): your bed is the " +
                           "game's own here.";
            }
        }

        private static string OwnLine(bool unheard)
        {
            if (!Settings.OwnWorld.Value)
            {
                return "Your own world (your game runs it): \"Naps in your own world\" below is off, so your bed is the game's own here.";
            }
            if (!NapSkip.Ready)
            {
                return "Your own world: the game has changed and a nap can't start here (the game's log says what's missing), " +
                       "so your bed is the game's own.";
            }
            Served served = Settings.OwnServed();
            if (!CanNap(served))
            {
                return "Your own world: the hours and the afternoon below allow no nap, so your bed is the game's own here.";
            }
            return unheard
                ? $"Your own world (your game runs it): you can nap here ({Hours(served)}), by \"Your own world\" below. Friends " +
                  "who join can't lie down by day: another of the pack's mods installed on its own answers their games. " +
                  "Install miikeskii's QoL Pack in place of the separate mods."
                : $"Your own world (your game runs it): you can nap here ({Hours(served)}), by \"Your own world\" below. Friends " +
                  "who join with the pack can too.";
        }

        /// <summary>Whether a nap can ever start by these rules: a window that isn't empty, with a morning in it, or an
        /// afternoon whose rule naps (review, nit 5: a window wholly in the afternoon with NextMorning allows none).</summary>
        internal static bool CanNap(Served served) =>
            served.From < served.Until && (served.From < NapRules.Noon || served.Afternoon != AfternoonRule.NextMorning);

        // "from 6:00 until 18:00" (a window that allows a nap).
        private static string Hours(Served served) => $"from {NapRules.HourText(served.From)} until {NapRules.HourText(served.Until)}";
    }
}
