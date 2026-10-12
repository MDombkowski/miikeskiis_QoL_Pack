using System;
using System.Collections.Generic;

namespace TopperUpper.Loose
{
    /// <summary>
    /// The loose-item pickup (card A77, his "build it now, in this mod, as its own section, off at first"): ServersideQoL's
    /// AutoStore auto pickup done the pack's way, so breaking a big deposit beside a chest holding stone costs a few frames,
    /// never a freeze. Each frame, last in the module's Update: while the module and "Pick up loose items into chests" are
    /// on, the sweep's turn (Sweep: gather a few a frame, then put away one kind at a time in capped frames, one line per
    /// chest at the end), with GameWorld as the game. Only items this game owns, never an ownership request, never a claim:
    /// chests only when this game may change them now (ChestClaims.MayChange), as the products' put-away. Switched off, a
    /// new world, or the undo step: everything forgotten.
    /// Its own guard (code review n8; the card's decision 2): an error in the pickup loses only the pickup, for the session,
    /// said once in the log; lights and stations go on. That's safe because every put settles in its own finally (Stow,
    /// GroundPut): what moved, moved whole, and the sweep is forgotten.
    /// </summary>
    internal static class Pickup
    {
        private static readonly GameWorld World = new GameWorld();
        private static readonly Sweep Work = new Sweep(World);
        private static readonly Pace Pace = new Pace();
        private static ZNet seen;
        private static bool running;
        private static bool lost;

        /// <summary>An error lost the pickup for the session (what Topper-Upper tells the server pack: "topperupper.loose").</summary>
        internal static bool Lost => lost;

        /// <summary>At the module's start: the game's members the pickup and the log fix use, each missing one said in the
        /// start log with what that costs (the rest of the mod runs).</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            GameWorld.Resolve(missingOptional);
            PutText.Resolve(missingOptional);
            QuietRepeatAsk.Resolve(missingOptional);
        }

        internal static void Tick()
        {
            if (seen != ZNet.instance)
            {
                seen = ZNet.instance;
                Forget();   // record numbers are only good within one world's session
            }
            bool on = !lost && Module.IsOn && Settings.PickUpLoose != null && Settings.PickUpLoose.Value;
            if (!on)
            {
                if (running)
                {
                    Forget();
                }
                return;
            }
            if (Player.m_localPlayer == null || ZNet.instance == null || ZNetScene.instance == null || ZDOMan.instance == null ||
                ObjectDB.instance == null || Game.instance == null)
            {
                return;
            }
            running = true;
            try
            {
                Pace.Scan = Settings.LooseScanPerFrame.Value;
                Pace.Puts = Settings.LoosePutsPerFrame.Value;
                Pace.Chests = Settings.LooseChestsPerFrame.Value;
                Pace.Every = Settings.LooseEvery.Value;
                Pace.RetryAfter = Math.Max(10.0, 3.0 * Settings.LooseEvery.Value);
                Work.Tick(Pace);
            }
            catch (Exception error)
            {
                lost = true;
                try
                {
                    Forget();
                }
                catch (Exception)
                {
                    // Forgetting is clearing lists; nothing more to undo.
                }
                Module.Warn("Topper-Upper's loose-item pickup stopped after an error while picking up loose items; it stays off until " +
                            $"the game is started again. Lights and stations go on as before.\n{error}");
            }
        }

        /// <summary>The undo step, switching off, and a new world: the sweep and what it remembered forgotten.</summary>
        internal static void Forget()
        {
            running = false;
            Work.Forget();
            World.Forget();
        }
    }
}
