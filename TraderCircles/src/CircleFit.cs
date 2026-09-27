using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// One circle and the part of it that is drawn: the circle cut to the ground where its trader can appear. Kept in
    /// the world's metres (x and z) and worked out once; the drawing only moves and scales it onto the map.
    /// The shape is described along lines straight out from the world's centre, at angles close together: on each, the
    /// stretch from Lo to Hi (metres from the world's centre) is drawn, none where Lo is above Hi.
    /// </summary>
    internal sealed class Circle
    {
        internal Circle(Vector2 centre, float radius, float[] angles, float[] lo, float[] hi, bool fullTurn, bool cut, bool placeOnGround)
        {
            Centre = centre;
            Radius = radius;
            Angles = angles;
            Lo = lo;
            Hi = hi;
            FullTurn = fullTurn;
            Cut = cut;
            PlaceOnGround = placeOnGround;
        }

        internal Vector2 Centre { get; }

        internal float Radius { get; }

        internal float[] Angles { get; }

        internal float[] Lo { get; }

        internal float[] Hi { get; }

        /// <summary>The circle holds the world's centre, so its lines go all the way round and the last joins the first.</summary>
        internal bool FullTurn { get; }

        /// <summary>Some of the circle lay where its trader can't appear, and isn't drawn.</summary>
        internal bool Cut { get; }

        /// <summary>The place lies on the ground as this mod works it out, so it lies in the drawn part. When not (the
        /// game's rules must differ from this mod's), the whole circle is drawn instead, uncut.</summary>
        internal bool PlaceOnGround { get; }

        /// <summary>The self-check: the place is inside the circle, and (as PlaceOnGround says) in the drawn part.</summary>
        internal bool Holds(Vector3 place) => (new Vector2(place.x, place.z) - Centre).magnitude <= Radius + 0.01f;
    }

    /// <summary>
    /// Places the circle of one size around one place a trader could be. The circle is never centred on the place: its
    /// centre lies anywhere within one radius of it, drawn at random, evenly, and stays there. That "random" comes from
    /// the world's seed and ID, the trader, the place and the size, so it is the same every session and for every player
    /// in the world, and laying one session's circle over another's narrows nothing down.
    /// Whatever part of the circle lies where the trader can't appear is simply not drawn. The circle is never moved to
    /// fit the ground: moving it would make places near the ground's edge likelier than others inside it. So the place is
    /// equally likely anywhere in the drawn shape.
    /// Each of the few sizes has circles of its own: circles of different sizes grown or shrunk around the place would
    /// point at it. Overlapping the circles of several sizes does narrow the search (his choice of five sizes, 2026-09-25).
    /// </summary>
    internal static class CircleFit
    {
        /// <summary>Places the circle of the given radius around one place and cuts it to the ground. The place is inside
        /// the circle by construction.</summary>
        internal static Circle Place(Ground ground, Trader trader, Vector3 place, float radius, int worldSeed, long worldId)
        {
            Vector2 spot = new Vector2(place.x, place.z);
            // The place to a tenth of a metre (the server sends every player the same numbers) and the size, written the
            // same way on every machine, whatever its language settings.
            var random = new Stream(Hash(string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}",
                worldSeed, worldId, trader.LocationName, (long)Math.Round(spot.x * 10.0), (long)Math.Round(spot.y * 10.0),
                (long)Math.Round(radius * 10.0))));
            Vector2 centre = spot + radius * random.PointInDisc();
            return Cut(ground, centre, radius, ground.Allows(spot));
        }

        // The circle cut to the ground, along lines from the world's centre a few metres apart at its far edge. With
        // cutting false, the whole circle.
        private static Circle Cut(Ground ground, Vector2 centre, float radius, bool cutting)
        {
            float distance = centre.magnitude;
            bool fullTurn = distance <= radius;
            float middle = Mathf.Atan2(centre.y, centre.x);
            float half = fullTurn ? Mathf.PI : Mathf.Asin(radius / distance);
            // About one line every 6 m along the circle's far edge; a full turn's last line is not the first again.
            int lines = Mathf.Clamp(Mathf.CeilToInt(2f * half * (distance + radius) / 6f), 32, 720);
            int count = fullTurn ? lines : lines + 1;
            var angles = new float[count];
            var lo = new float[count];
            var hi = new float[count];
            bool cut = false;
            for (int i = 0; i < count; i++)
            {
                // Closer together towards the circle's two sides, where its edge turns fastest across the lines, so the
                // straight pieces between lines cut off no more than a hair of the circle there.
                float angle = fullTurn ? 2f * Mathf.PI * i / lines : middle + half * Mathf.Sin(Mathf.PI * ((float)i / lines - 0.5f));
                angles[i] = angle;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Where this line crosses the circle: from near to far (metres from the world's centre).
                float along = Vector2.Dot(direction, centre);
                float square = along * along - distance * distance + radius * radius;
                float reach = Mathf.Sqrt(Mathf.Max(0f, square));
                float near = Mathf.Max(0f, along - reach);
                float far = Mathf.Max(near, along + reach);
                if (!cutting)
                {
                    lo[i] = near;
                    hi[i] = far;
                }
                else if (ground.Clip(direction, near, far, out float from, out float to))
                {
                    lo[i] = from;
                    hi[i] = to;
                    cut |= from > near + 0.5f || to < far - 0.5f;
                }
                else
                {
                    lo[i] = 1f;   // nothing on this line: Lo above Hi
                    hi[i] = 0f;
                    cut |= far - near > 0.5f;
                }
            }
            return new Circle(centre, radius, angles, lo, hi, fullTurn, cut, cutting);
        }

        // FNV-1a over the text's UTF-8 bytes, then SplitMix64's finaliser so every bit depends on every byte. Written
        // here rather than taken from .NET or Unity, whose hashes may differ between machines or versions.
        private static ulong Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return Mix(hash);
        }

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xbf58476d1ce4e5b9UL;
            value ^= value >> 27;
            value *= 0x94d049bb133111ebUL;
            value ^= value >> 31;
            return value;
        }

        /// <summary>A stream of fixed "random" numbers from one starting value (SplitMix64): the same on every machine.</summary>
        private sealed class Stream
        {
            private ulong state;

            internal Stream(ulong seed)
            {
                state = seed;
            }

            /// <summary>A point inside a circle of radius 1, spread evenly over it.</summary>
            internal Vector2 PointInDisc()
            {
                ulong value = Next();
                double turn = (uint)value / 4294967296.0;
                double reach = Math.Sqrt((uint)(value >> 32) / 4294967296.0);
                double angle = 2.0 * Math.PI * turn;
                return new Vector2((float)(reach * Math.Cos(angle)), (float)(reach * Math.Sin(angle)));
            }

            private ulong Next()
            {
                state += 0x9e3779b97f4a7c15UL;
                return Mix(state);
            }
        }
    }
}
