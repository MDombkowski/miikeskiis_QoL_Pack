using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Hoarstone.Shapes;

namespace Hoarstone
{
    /// <summary>What one round of working out gives: every settlement found (with its shape where it counts), and the
    /// shapes spread into zone tiles.</summary>
    internal sealed class WorkResult
    {
        internal List<ShapePoint> Points;
        internal ShapeOptions Options;
        internal List<Settlement> Settlements;
        internal Dictionary<ZoneId, Tile> Tiles;
        internal Dictionary<string, ShapeGrid> Cache;
        internal int ShapesWorked;
        internal int TooLarge;
        internal long SquaresWorked;
        internal double Milliseconds;
        internal Exception Error;
        internal int Generation;

        /// <summary>What this game held whole when the round took its pieces.</summary>
        internal SeenSnapshot Seen;

        /// <summary>The pieces', land's and shape settings' versions the round was taken at (Coverage refreshes what's
        /// held while all three still match).</summary>
        internal int RecordsVersion, LandVersion, ShapeVersion;
    }

    /// <summary>
    /// Works the settlements and their shapes out on a worker thread (decision 1 of the plan): a 400 m settlement is
    /// about 60,000 squares, and that must never stall a frame. The main thread copies the inputs out (the pieces, the
    /// options, the exact land), the worker does pure arithmetic (and the world's own heights, as the game's heightmap
    /// builder does on its own thread), and the main thread takes the result at a later frame. A settlement whose pieces,
    /// options and land are unchanged keeps its shape from the round before.
    /// </summary>
    internal static class Work
    {
        private static WorkResult done;
        private static Dictionary<string, ShapeGrid> cache = new Dictionary<string, ShapeGrid>();
        private static int generation;

        internal static bool Running { get; private set; }

        /// <summary>The most squares one settlement's grid may have (about 2 km across at 2 m squares): a larger one,
        /// which only pieces chained across a huge area make, isn't worked out, and the log says so.</summary>
        internal const long MostSquares = 1000000;

        internal static void Start(PieceCopy[] pieces, ShapeOptions options, Dictionary<ZoneId, LandTile> land, WorldGenerator world, SeenSnapshot seen,
                                   int recordsVersion, int landVersion, int shapeVersion)
        {
            Running = true;
            Dictionary<string, ShapeGrid> before = cache;
            int startedIn = generation;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = new WorkResult
                {
                    Options = options,
                    Generation = startedIn,
                    Seen = seen,
                    RecordsVersion = recordsVersion,
                    LandVersion = landVersion,
                    ShapeVersion = shapeVersion,
                };
                try
                {
                    result.Points = Records.Sorted(pieces);
                    Run(result, land, world, before);
                }
                catch (Exception error)
                {
                    result.Error = error;
                }
                if (startedIn == Volatile.Read(ref generation))
                {
                    Volatile.Write(ref done, result);   // a round from before a Forget never lands on a newer one
                }
            });
        }

        /// <summary>The finished round, once; null while none is waiting.</summary>
        internal static WorkResult Take()
        {
            WorkResult result = Volatile.Read(ref done);
            if (result == null)
            {
                return null;
            }
            Volatile.Write(ref done, null);
            if (result.Generation != generation)
            {
                return null;   // started before a Forget: its world is gone (Forget already let a new round start)
            }
            Running = false;
            if (result.Error == null)
            {
                cache = result.Cache;
            }
            return result;
        }

        private static void Run(WorkResult result, Dictionary<ZoneId, LandTile> land, WorldGenerator world, Dictionary<string, ShapeGrid> before)
        {
            var clock = Stopwatch.StartNew();
            ShapeOptions o = result.Options;
            List<Settlement> found = Settlements.Find(result.Points, o);
            Func<double, double, double> ground = Land.GroundFor(land, world, o.Cell);
            double pad = Settlements.Pad(o);
            var next = new Dictionary<string, ShapeGrid>();
            foreach (Settlement s in found)
            {
                if (!s.Kept)
                {
                    continue;
                }
                s.Signature = Settlements.PiecesSignature(s, result.Points, o) + "|" +
                              Land.Stamp(land, s.MinX - pad, s.MinZ - pad, s.MaxX + pad, s.MaxZ + pad);
                double across = (s.MaxX - s.MinX + 2 * pad) / o.Cell + 2, deep = (s.MaxZ - s.MinZ + 2 * pad) / o.Cell + 2;
                if (across * deep > MostSquares)
                {
                    s.Kept = false;
                    result.TooLarge++;
                    continue;
                }
                if (!before.TryGetValue(s.Signature, out ShapeGrid shape) && !next.TryGetValue(s.Signature, out shape))
                {
                    shape = ShapeMath.ShapeOf(Settlements.PointsOf(s, result.Points), o, ground);
                    result.ShapesWorked++;
                    result.SquaresWorked += (long)shape.W * shape.H;
                }
                s.Shape = shape;
                next[s.Signature] = shape;
            }
            result.Settlements = found;
            result.Tiles = Tiles.Spread(found, o.Cell);
            result.Cache = next;
            result.Milliseconds = clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Forgets the shapes kept between rounds. A round still running finishes into nothing.</summary>
        internal static void Forget()
        {
            generation++;
            cache = new Dictionary<string, ShapeGrid>();
            Volatile.Write(ref done, null);
            Running = false;
        }
    }
}
