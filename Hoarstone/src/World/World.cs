using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// The settlements' side of the mod, run every frame: the records, the zones seen, the land, a round of working out
    /// when something changed (a moment after the changes settle, and at least every few seconds while they keep coming),
    /// and the shapes and saved tiles. While the mod is switched off it holds nothing: switched on again, it starts from a
    /// fresh pass over the records.
    /// </summary>
    internal static class World
    {
        private const float SettleFor = 1f;
        private const float AtMostWait = 5f;

        private static bool wasOn;
        private static ZDOMan forWorld;
        private static bool warnedTooLarge;
        private static int doneRecords = -1, doneLand = -1, doneShape = -1, doneSeen = -1;
        private static float firstChange = -1f, lastChange = -1f;
        private static int seenRecords = -1, seenLand = -1, seenShape = -1, seenSeen = -1;

        internal static void Tick(float now)
        {
            if (!Module.IsOn)
            {
                if (wasOn)
                {
                    Forget();
                }
                wasOn = false;
                return;
            }
            wasOn = true;
            if (ZDOMan.instance != forWorld)
            {
                Forget();   // another world, or none: start afresh
                forWorld = ZDOMan.instance;
            }
            if (ZNet.instance == null || ZDOMan.instance == null || Player.m_localPlayer == null)
            {
                return;   // no world, or a dedicated server: nothing spawns here
            }
            Records.Tick(now);
            SeenZones.Tick(now);
            Land.Tick(now);

            WorkResult result = Work.Take();
            if (result != null)
            {
                if (result.Error != null)
                {
                    throw new System.InvalidOperationException("Working out the settlements failed.", result.Error);
                }
                Coverage.Apply(result);
                if (result.TooLarge > 0 && !warnedTooLarge)
                {
                    warnedTooLarge = true;
                    Module.Log.LogWarning($"{result.TooLarge} settlement(s) spread over more than about 2 km aren't worked out; " +
                                          "a smaller \"Pieces this close belong together\" or a larger \"Neighbours a piece needs to pass that on\" splits them.");
                }
                if (Settings.LogWork.Value)
                {
                    int kept = 0;
                    foreach (Settlement s in result.Settlements)
                    {
                        if (s.Kept)
                        {
                            kept++;
                        }
                    }
                    Module.Log.LogInfo($"Worked out {kept} settlements from {result.Points.Count} pieces: {result.ShapesWorked} shapes " +
                                       $"new ({result.SquaresWorked} squares) in {result.Milliseconds:0.0} ms, off the main thread.");
                }
            }
            Coverage.Tick(now);

            // A round also follows what this game holds whole (review round 3: its shapes are judged by that moment's).
            int records = Records.Version, land = Land.Version, shape = Settings.ShapeVersion, held = SeenZones.Version + (SeenZones.Everything ? 1 << 30 : 0);
            if (records != seenRecords || land != seenLand || shape != seenShape || held != seenSeen)
            {
                seenRecords = records;
                seenLand = land;
                seenShape = shape;
                seenSeen = held;
                if (firstChange < 0f)
                {
                    firstChange = now;
                }
                lastChange = now;
            }
            bool wanted = records != doneRecords || land != doneLand || shape != doneShape || held != doneSeen;
            if (wanted && !Work.Running && (now - lastChange >= SettleFor || now - firstChange >= AtMostWait))
            {
                doneRecords = records;
                doneLand = land;
                doneShape = shape;
                doneSeen = held;
                firstChange = -1f;
                Work.Start(Records.Snapshot(), Settings.Options(), Land.Snapshot(), WorldGenerator.instance, SeenZones.Snapshot(),
                           records, land, shape);
            }
        }

        /// <summary>Forgets everything worked out and stops listening (switched off, or after an error).</summary>
        internal static void Forget()
        {
            Work.Forget();
            Records.Forget();
            SeenZones.Forget();
            Land.Forget();
            Coverage.Forget();
            doneRecords = doneLand = doneShape = doneSeen = -1;
            seenRecords = seenLand = seenShape = seenSeen = -1;
            firstChange = lastChange = -1f;
            forWorld = null;
        }
    }
}
