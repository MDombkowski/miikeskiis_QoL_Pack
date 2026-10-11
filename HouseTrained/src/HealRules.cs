using System;

namespace HouseTrained
{
    /// <summary>
    /// Healing in pens (card A84, his words of 2026-10-09: tames "regenerate health while in pens at a significantly higher
    /// rate", needing nothing but being tamed and in a pen), as plain rules the harness checks: how much a tame heals, over
    /// what time, who heals it, and what counts as a pen. The game's own healing (BaseAI.UpdateRegeneration, every 2 s on
    /// the creature's owner) gives a creature its full health back in m_regenAllHPTime seconds of world time (3600 for
    /// every tame in 1.0.17), and a tame only while it isn't hungry; this comes on top of it, hungry or not.
    /// </summary>
    internal static class HealRules
    {
        /// <summary>How often a tame in a pen is healed, in seconds (the game's own regeneration's beat).</summary>
        internal const float Beat = 2f;

        /// <summary>A gap longer than this between two beats starts the clock again rather than healing for it (the tame
        /// was unloaded, another game looked after it meanwhile, or the part was off), in seconds.</summary>
        internal const float LongestGap = 5f;

        /// <summary>How long a tame's "in a pen" answer is kept before it is looked at again, in seconds.</summary>
        internal const float PenCheckEvery = 5f;

        /// <summary>The directions looked along from a tame for its pen's walls, evenly round it.</summary>
        internal const int Directions = 16;

        /// <summary>How far each stretch of a look along the ground reaches, in metres: on a slope each stretch follows the
        /// ground, so a fence up or down the hill isn't passed over or under.</summary>
        internal const float Stretch = 4f;

        /// <summary>A tame whose feet are this close to the terrain stands on it, and its looks follow the ground; one
        /// standing higher (a barn's floor) looks level, in metres.</summary>
        internal const float OnTerrain = 0.5f;

        /// <summary>At most this many pen tests in one look over the tames (a quarter of a second): the rest wait for the
        /// next, so a big pen of hurt tames never makes a frame stutter (code review m3).</summary>
        internal const int MostPenTests = 4;

        /// <summary>The heights above the ground a pen's walls are looked for at, in metres, the same for every tame (a
        /// hen's own height would leave a fence's rails above its looks: code review m4). From the game's data (research
        /// 2026-09-30, tames in pens, report C): the Roundpole fence is one solid slab from at most 0.28 m up to at least
        /// 0.97 m, the stone fence 0 to 1 m, a half wall 1 m, the sharp stakes 0.86 m; every one is met by two looks.</summary>
        internal static readonly float[] LookHeights = { 0.3f, 0.6f, 0.9f };

        // Piece.UsageTagFlags (the build menu's categories), by value.
        private const int Wall = 0x10;
        private const int Architecture = 0x40;
        private const int Defense = 0x8000;
        private const int Doors = 0x40000;

        /// <summary>
        /// Whether a piece fences a pen in, by the build menu's categories it carries (Piece.m_usage): walls (wood and
        /// stone walls, the Roundpole and stone fences, stake walls), doors and gates, defences (sharp stakes), and
        /// architecture (poles and beams), and nothing else: not a floor or a roof, chests, beds, torches, stations or decor,
        /// so an open yard or a floored porch isn't a pen (code review m5). A piece that is a wall as well as a floor (the
        /// black marble and ice blocks, built into walls as often as floors) counts. Another mod's piece without any
        /// category counts, as it did before.
        /// </summary>
        internal static bool Fences(int usage) => usage == 0 || (usage & (Wall | Doors | Defense | Architecture)) != 0;

        /// <summary>A tame's first beat, at a random point of its first <see cref="Beat"/>, so tames loaded together don't
        /// all beat in one look (code review m3); <paramref name="random"/> from 0 to 1.</summary>
        internal static float FirstBeat(float now, float random) => now + Clamp01(random) * Beat;

        /// <summary>When a tame's pen is looked for again: after about <see cref="PenCheckEvery"/> (0.75 to 1.25 of it, at
        /// random), so the tests spread out.</summary>
        internal static float NextPenCheck(float now, float random) => now + PenCheckEvery * (0.75f + 0.5f * Clamp01(random));

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));

        /// <summary>How much health a tame gets back over <paramref name="seconds"/> at <paramref name="percentPerMinute"/>
        /// of its full health a minute; nothing for a share or time of zero or less.</summary>
        internal static float Amount(float maxHealth, float percentPerMinute, float seconds)
        {
            if (maxHealth <= 0f || percentPerMinute <= 0f || seconds <= 0f || float.IsNaN(seconds))
            {
                return 0f;
            }
            return maxHealth * (percentPerMinute / 100f) * (seconds / 60f);
        }

        /// <summary>The time a beat heals for: since the last beat (<paramref name="last"/>, NaN when there was none), or
        /// nothing when the gap is too long or time went backwards; the clock is set to now either way.</summary>
        internal static float Since(float now, ref float last, float longest)
        {
            float before = last;
            last = now;
            if (float.IsNaN(before) || now < before || now - before > longest)
            {
                return 0f;
            }
            return now - before;
        }

        /// <summary>Whether this game heals the creature now: the part on, this game looking after the creature (the owner
        /// of its record: only that game heals it, so two friends on the pack never heal it twice), tamed, alive, hurt and in
        /// a pen.</summary>
        internal static bool Heals(bool partOn, bool owner, bool tamed, bool dead, bool hurt, bool inPen) =>
            partOn && owner && tamed && !dead && hurt && inPen;

        /// <summary>
        /// One beat of one tame on one game: how much it heals now. A game that doesn't look after it (or a dead one)
        /// heals nothing and sets its clock back, so it never heals for time another game had it; otherwise the time
        /// since its last beat here, at the share a minute, only while it's hurt and in a pen (asked last, and only then),
        /// never more than it lacks.
        /// </summary>
        internal static float BeatAmount(bool partOn, bool owner, bool tamed, bool dead, float health, float most, float percentPerMinute,
                                         float now, ref float last, Func<bool> inPen)
        {
            if (!owner || dead)
            {
                last = float.NaN;
                return 0f;
            }
            float seconds = Since(now, ref last, LongestGap);
            bool hurt = health > 0f && health < most;
            if (!hurt || !Heals(partOn, owner, tamed, dead, hurt, inPen()))
            {
                return 0f;
            }
            return Math.Min(Amount(most, percentPerMinute, seconds), most - health);
        }

        /// <summary>Whether walls close a tame in: no more than <paramref name="gapsAllowed"/> of the directions looked along
        /// are open (an open gate, a gap between two fences).</summary>
        internal static bool Enclosed(int closed, int directions, int gapsAllowed) =>
            directions > 0 && directions - closed <= Math.Max(0, gapsAllowed);

        /// <summary>Whether walls close a tame in: each direction asked in turn whether a wall stands that way
        /// (<paramref name="walledAt"/>), stopping as soon as too many are open; how many were found walled.</summary>
        internal static bool Walled(Func<int, bool> walledAt, int directions, int gapsAllowed, out int closed)
        {
            closed = 0;
            int open = 0;
            for (int i = 0; i < directions; i++)
            {
                if (walledAt(i))
                {
                    closed++;
                }
                else if (TooManyOpen(++open, gapsAllowed))
                {
                    break;   // open on too many sides already: no pen, whatever the rest find
                }
            }
            return Enclosed(closed, directions, gapsAllowed);
        }

        /// <summary>A look along one direction in stretches: each stretch (from, to: metres from the tame) asked in turn,
        /// from the tame outward, until one finds a wall; the last ends at the reach exactly.</summary>
        internal static bool March(float reach, float stretch, Func<float, float, bool> walledBetween)
        {
            if (reach <= 0f || stretch <= 0f)
            {
                return false;
            }
            float done = 0f;
            while (done < reach - 1e-3f)
            {
                float next = Math.Min(reach, done + stretch);
                if (walledBetween(done, next))
                {
                    return true;
                }
                done = next;
            }
            return false;
        }

        /// <summary>Whether the pen test can stop early: more directions are open already than the gaps allowed, so the
        /// answer is no whatever the rest find.</summary>
        internal static bool TooManyOpen(int open, int gapsAllowed) => open > Math.Max(0, gapsAllowed);

        /// <summary>The <paramref name="index"/>th of <paramref name="count"/> directions round a tame, on the level (x, z),
        /// the first along +z.</summary>
        internal static void Direction(int index, int count, out float x, out float z)
        {
            double angle = 2.0 * Math.PI * index / Math.Max(1, count);
            x = (float)Math.Sin(angle);
            z = (float)Math.Cos(angle);
        }
    }
}
