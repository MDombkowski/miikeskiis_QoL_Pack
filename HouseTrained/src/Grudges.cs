using System.Collections.Generic;

namespace HouseTrained
{
    /// <summary>
    /// Fighting back (decision 20): a creature hurt by a friend holds a grudge against it for a while, and the two count
    /// as enemies of each other until it lasts out, either way round, so the hurt one can take its attacker as a target
    /// and its attacks can hit it. Kept on the hurt creature's owner's game, where its AI runs. A short list scanned in
    /// full: there are rarely more than a handful, and with none, a check costs one count test (BaseAI.IsEnemy is asked
    /// constantly). Creatures are kept by reference, compared by reference: plain objects, so the harness checks it.
    /// </summary>
    internal sealed class Grudges
    {
        /// <summary>At most this many at once; the oldest goes first.</summary>
        internal const int Most = 64;

        private readonly List<Grudge> list = new List<Grudge>();

        internal int Count => list.Count;

        /// <summary>A grudge between the two until <paramref name="until"/> (a time on the module's clock); a grudge they
        /// already hold, either way round, is renewed.</summary>
        internal void Hold(object hurt, object by, float until)
        {
            if (hurt == null || by == null || ReferenceEquals(hurt, by))
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (Pairs(list[i], hurt, by))
                {
                    list[i] = new Grudge(list[i].A, list[i].B, until > list[i].Until ? until : list[i].Until);
                    return;
                }
            }
            if (list.Count >= Most)
            {
                list.RemoveAt(0);
            }
            list.Add(new Grudge(hurt, by, until));
        }

        /// <summary>Whether the two hold a live grudge, either way round. Grudges that have lasted out are dropped as they
        /// are met.</summary>
        internal bool Between(object a, object b, float now)
        {
            if (list.Count == 0 || a == null || b == null)
            {
                return false;
            }
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Grudge grudge = list[i];
                if (grudge.Until <= now)
                {
                    list.RemoveAt(i);
                    continue;
                }
                if (Pairs(grudge, a, b))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every grudge that has lasted out dropped (once a second, so the count really reaches nothing and the
        /// enemy test costs one count test again: review N2).</summary>
        internal void Prune(float now)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Until <= now)
                {
                    list.RemoveAt(i);
                }
            }
        }

        /// <summary>Every grudge forgotten: switched off, or a new world.</summary>
        internal void Clear() => list.Clear();

        private static bool Pairs(Grudge grudge, object a, object b) =>
            (ReferenceEquals(grudge.A, a) && ReferenceEquals(grudge.B, b)) || (ReferenceEquals(grudge.A, b) && ReferenceEquals(grudge.B, a));

        private readonly struct Grudge
        {
            internal readonly object A;
            internal readonly object B;
            internal readonly float Until;

            internal Grudge(object a, object b, float until)
            {
                A = a;
                B = b;
                Until = until;
            }
        }
    }
}
