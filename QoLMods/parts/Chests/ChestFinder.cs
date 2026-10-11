// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// Finds the chests near the player that he may use, and reads what they hold from this computer's copy of each
    /// (kept current by the game about once a second, so up to a second old: good for counting, never for taking).
    /// </summary>
    internal static class ChestFinder
    {
        // Every chest that has come into being on this computer (the patch on Container.Awake), as AzuCraftyBoxes keeps
        // its list; unloaded ones drop out as they are met. Cheaper than a physics search of 150 m once a second.
        private static readonly HashSet<Container> Known = new HashSet<Container>();

        private static int sincePruned;

        /// <summary>Where a thing is (a chest, or him). Only the scratch test harness sets another: it has no engine to
        /// ask.</summary>
        internal static Func<Component, Vector3> Where = thing => thing.transform.position;

        /// <summary>The rules' word on a chest (WhyNot). Only the scratch test harness sets another: the rules ask the
        /// engine about the chest's pieces.</summary>
        internal static Func<Container, ChestRules, long, string> Judge = WhyNot;

        internal static void Register(Container chest)
        {
            if (chest == null)
            {
                return;
            }
            Known.Add(chest);
            // Placement previews, dungeon chests and gravestones join too, and go again: prune now and then, so the list
            // doesn't grow while the window stays closed.
            if (++sincePruned >= 256)
            {
                sincePruned = 0;
                Known.RemoveWhere(each => each == null);
            }
        }

        /// <summary>The chests within the rules' range that the rules allow, nearest first. Only loaded chests exist on
        /// this computer, so only they can be found. A chest carrying Reserve Stock's mark is left out, unless
        /// <paramref name="withReserved"/> asks for it too (to count it apart, or to point at it, never to take), or the
        /// rules use reserved chests like any other (ChestRules.UseReserved).</summary>
        internal static List<Container> Find(Vector3 centre, ChestRules rules, bool withReserved = false)
        {
            Known.RemoveWhere(chest => chest == null);
            var found = new List<Container>();
            var away = new Dictionary<Container, float>();
            long playerId = ChestClaims.PlayerId();
            float most = rules.Range * rules.Range;
            foreach (Container chest in Known)
            {
                float distance = (Where(chest) - centre).sqrMagnitude;
                if (distance <= most && Judge(chest, rules, playerId) == null &&
                    (withReserved || rules.UseReserved || !ChestReserve.IsReserved(chest)))
                {
                    found.Add(chest);
                    away[chest] = distance;
                }
            }
            found.Sort((a, b) => away[a].CompareTo(away[b]));
            return found;
        }

        /// <summary>
        /// Whether the rules let him use this chest: one a player built (never a world's own chest, a gravestone or the
        /// Obliterator), that
        /// the game would let him open by hand (its privacy, and the ward over it), within "only chests I built" if that
        /// is on, not on a ship with anyone else aboard, not another mod's kind of chest, not another player's personal
        /// chest closed to others' mods (ChestAccess), and not left alone by the rules' Skip.
        /// </summary>
        internal static bool MayUse(Container chest, ChestRules rules, long playerId) => WhyNot(chest, rules, playerId) == null;

        /// <summary>Why the rules don't let him use this chest, as words that follow "the chest is" for him to read; null if
        /// they do. The one place the rules are written (MayUse asks it).</summary>
        internal static string WhyNot(Container chest, ChestRules rules, long playerId)
        {
            // Only the game's own chests: another mod's kind of chest (PlanBuild's plan totem, which holds what his
            // plans are to be built from) has its own purpose.
            if (chest.GetType() != typeof(Container))
            {
                return "another mod's kind of container";
            }
            ZNetView view = ChestHooks.NetView(chest);
            if (view == null || !view.IsValid() || !chest.isActiveAndEnabled)
            {
                return "not loaded";
            }
            if (chest.GetComponentInParent<TombStone>() != null)
            {
                return "a gravestone";
            }
            // The Obliterator: a plain Container on its piece's child "container" (Incinerator.m_container), whose contents
            // its lever destroys, ten of anything becoming one coal. Never a chest to the pack, for takes and puts alike, or a
            // quick stack would put coal into one still holding coal (45-B's fuel research, 2026-10-09; card A80). The game's
            // only player-built piece holding a Container that isn't storage (its classes with a Container: Container,
            // Corpse, Incinerator, TombStone, Vagon's cart storage).
            if (IsObliterator(chest))
            {
                return ObliteratorWhy;
            }
            // A cart's or a ship's chest sits below the piece the player built, so the piece is looked for upwards.
            Piece piece = chest.GetComponentInParent<Piece>();
            if (piece == null || !piece.IsPlacedByPlayer())
            {
                return "not one a player built";
            }
            if (rules.OnlyBuiltByMe && piece.GetCreator() != playerId)
            {
                return "one someone else built (the setting \"Which chests\")";
            }
            // A ship's chest belongs to the ship's record, so claiming it would hand him the ship, and its steering,
            // while someone else sails it.
            Ship ship = chest.GetComponentInParent<Ship>();
            if (ship != null && ChestHooks.OthersAboard(ship))
            {
                return "on a ship someone else is aboard";
            }
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, flash: false))
            {
                return "behind a ward you aren't on";
            }
            if (!ChestHooks.CheckAccess(chest, playerId))
            {
                return "someone else's private chest";
            }
            // A chest another player marked as theirs and keeps closed to others, or lets them only look at, unless they let
            // other players' mods in (card A80): every module's reaching in stays out, as his hands do.
            string kept = ChestAccess.WhyNot(chest, playerId);
            if (kept != null)
            {
                return kept;
            }
            if (rules.Skip != null && rules.Skip(chest))
            {
                return "one the mod leaves alone";
            }
            // Held after a take, and asked for by another player meanwhile (ChestClaims): left alone until the hold ends, so
            // taking again can't keep him out. Asked last, so WaitedOn can tell a chest left alone for this reason only.
            if (ChestClaims.IsWaitedOn(chest))
            {
                return WaitedOnWhy;
            }
            return null;
        }

        /// <summary>WhyNot's words for a chest another player is about to open.</summary>
        internal const string WaitedOnWhy = "about to be opened by another player";

        /// <summary>WhyNot's words for the Obliterator's container.</summary>
        internal const string ObliteratorWhy = "an obliterator, not a chest";

        /// <summary>True for the Obliterator's container (an Incinerator on its piece, above it): what it holds is destroyed
        /// at its lever's pull, so no module of the pack takes from it or puts into it, and it can't be marked as anyone's.</summary>
        internal static bool IsObliterator(Container chest) => chest != null && chest.GetComponentInParent<Incinerator>() != null;

        /// <summary>The chests within the rules' range that the rules would allow but for one thing: another player asked to
        /// open them while this computer held them (ChestClaims.IsWaitedOn), so Find leaves them alone for a moment. A quick
        /// stack counts a set-up one among them as the home of what it takes, as one someone has open.</summary>
        internal static List<Container> WaitedOn(Vector3 centre, ChestRules rules)
        {
            var found = new List<Container>();
            long playerId = ChestClaims.PlayerId();
            float most = rules.Range * rules.Range;
            foreach (Container chest in Known)
            {
                if (chest != null && (Where(chest) - centre).sqrMagnitude <= most && Judge(chest, rules, playerId) == WaitedOnWhy &&
                    (rules.UseReserved || !ChestReserve.IsReserved(chest)))
                {
                    found.Add(chest);
                }
            }
            return found;
        }

        /// <summary>Every chest loaded on this computer, whatever the rules say: for explaining what a count left out.</summary>
        internal static List<Container> Loaded()
        {
            Known.RemoveWhere(chest => chest == null);
            return Known.ToList();
        }

        /// <summary>True if another player has this chest open, as far as this computer's copy of its record says. Used
        /// for the preview only; a fetch asks the owner, whose answer is the truth.</summary>
        internal static bool OpenElsewhere(Container chest)
        {
            ZNetView view = ChestHooks.NetView(chest);
            return view != null && view.IsValid() && !view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
        }
    }
}
