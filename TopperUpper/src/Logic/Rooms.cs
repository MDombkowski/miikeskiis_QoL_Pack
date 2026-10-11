using System;

namespace TopperUpper.Logic
{
    /// <summary>When a light is topped up.</summary>
    internal enum LightTopUp
    {
        /// <summary>As soon as it has room for one more.</summary>
        RoomForOne,

        /// <summary>Once it's half empty.</summary>
        HalfEmpty,

        /// <summary>Only once it's empty.</summary>
        Empty,
    }

    /// <summary>When a station is topped up.</summary>
    internal enum StationTopUp
    {
        RoomForOne,
        HalfEmpty,
    }

    /// <summary>
    /// How much a light or station has room for, worked out as the game's own hand does it (the owner's own add never
    /// checks: the research's 1.1), and when it's due a top-up. Plain values, for the harness.
    /// </summary>
    internal static class Rooms
    {
        /// <summary>A fire's room (Fireplace.Interact and RPC_AddFuel): one more goes in while its fuel, rounded up, is
        /// below its capacity; the owner throws away the rest.</summary>
        internal static int Fire(float fuel, float maxFuel)
        {
            int room = 0;
            float level = Math.Max(0f, fuel);
            while (Math.Ceiling(level) < maxFuel && room < 1000)
            {
                level = Math.Min(level + 1f, maxFuel);
                room++;
            }
            return room;
        }

        /// <summary>A smelter's, cooking station's or shield generator's fuel room (their OnAddFuel): one more goes in while
        /// the fuel is at most its capacity less one.</summary>
        internal static int Fuel(float fuel, int maxFuel) => fuel > maxFuel - 1 ? 0 : (int)Math.Floor(maxFuel - Math.Max(0f, fuel));

        /// <summary>A smelter's queue room (OnAddOre): its size less what's queued.</summary>
        internal static int Queue(int queued, int maxOre) => Math.Max(0, maxOre - Math.Max(0, queued));

        /// <summary>Whether a light is due a top-up: room for one at least, and, as the setting says, any room, half empty,
        /// or empty.</summary>
        internal static bool LightDue(float fuel, float maxFuel, int room, LightTopUp at)
        {
            if (room <= 0)
            {
                return false;
            }
            switch (at)
            {
                case LightTopUp.HalfEmpty:
                    return fuel <= maxFuel * 0.5f;
                case LightTopUp.Empty:
                    return fuel <= 0f;
                default:
                    return true;
            }
        }

        /// <summary>Whether a station's queue (or fuel) is due a top-up: room for one at least, and with half empty, half
        /// its capacity free.</summary>
        internal static bool StationDue(int room, int capacity, StationTopUp at)
        {
            if (room <= 0)
            {
                return false;
            }
            return at != StationTopUp.HalfEmpty || room * 2 >= capacity;
        }
    }
}
