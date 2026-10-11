using System.Collections.Generic;

namespace Grounded
{
    /// <summary>What one hit of the aiming ray is to Grounded.</summary>
    internal enum HitKind
    {
        /// <summary>Not counted at all: a loose thing with a body of its own (an item, a cart), as the game skips it.</summary>
        Skip,

        /// <summary>A listed rug: looked through.</summary>
        Rug,

        /// <summary>A listed floor.</summary>
        Floor,

        /// <summary>The ground itself.</summary>
        Ground,

        /// <summary>Anything else: a table, a wall, an unlisted piece, a rock, water.</summary>
        Other,
    }

    /// <summary>What the walk along the ray decided.</summary>
    internal enum Outcome
    {
        /// <summary>Leave the game's own placement.</summary>
        GameOwn,

        /// <summary>Lie on the floor (or the ground, where it counts) at Index.</summary>
        Floor,

        /// <summary>Land on whatever is at Index, as the game would have if the rugs weren't there.</summary>
        Behind,
    }

    internal readonly struct Choice
    {
        internal readonly Outcome Outcome;
        internal readonly int Index;

        internal Choice(Outcome outcome, int index)
        {
            Outcome = outcome;
            Index = index;
        }

        internal static Choice GameOwn => new Choice(Outcome.GameOwn, -1);
    }

    /// <summary>
    /// The walk along the aiming ray, nearest hit first: every listed rug is passed, and the first thing behind them
    /// decides. A listed floor (or the ground, while it counts) is landed on; anything else leaves the game's own
    /// placement, or by the setting is landed on as it is. Nothing at all behind the rugs leaves the game's own
    /// placement. (Reach is judged by the caller, on the point it hands back.) Plain code: the scratch harness runs it.
    /// </summary>
    internal static class HitWalk
    {
        internal static Choice Choose(IList<HitKind> hits, bool groundCounts, NoFloor noFloor)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                switch (hits[i])
                {
                    case HitKind.Skip:
                    case HitKind.Rug:
                        continue;
                    case HitKind.Floor:
                        return new Choice(Outcome.Floor, i);
                    case HitKind.Ground:
                        if (groundCounts)
                        {
                            return new Choice(Outcome.Floor, i);
                        }
                        return Behind(i, noFloor);
                    default:
                        return Behind(i, noFloor);
                }
            }
            return Choice.GameOwn;
        }

        private static Choice Behind(int index, NoFloor noFloor) =>
            noFloor == NoFloor.BehindRugs ? new Choice(Outcome.Behind, index) : Choice.GameOwn;
    }
}
