using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>
    /// Whose turn it is to be looked at (the research's 2.6): a cursor goes round the list of lights and stations, a few
    /// entries a frame, and hands back those whose turn has come (each entry's own due time; the caller sets the next one
    /// when it looks). At most so many entries are passed over in one frame, and at most so many looked at, so a base of a
    /// thousand torches costs the same each frame as one of ten; every entry is reached once per round, none twice in one
    /// frame, and entries removed keep the others' places. Plain values, for the harness.
    /// </summary>
    internal sealed class Turns
    {
        /// <summary>The next entry to pass over.</summary>
        internal int Cursor { get; private set; }

        /// <summary>How many times the cursor has gone round: a round that began after something was wanted has seen
        /// everything due with it.</summary>
        internal int Pass { get; private set; }

        /// <summary>
        /// The entries (by index, below <paramref name="count"/>) whose turn has come (<paramref name="dueOf"/> at or before
        /// <paramref name="now"/>), passing over at most <paramref name="scanCap"/> entries and handing back at most
        /// <paramref name="lookBudget"/>, into <paramref name="into"/> (cleared first); and into <paramref name="passes"/>
        /// (if given, cleared first), for each, the round it was found in: the cursor can go round in the middle of a frame,
        /// so what was found before that belongs to the round that just ended.
        /// </summary>
        internal void Next(int count, Func<int, float> dueOf, float now, int scanCap, int lookBudget, List<int> into, List<int> passes = null)
        {
            into.Clear();
            passes?.Clear();
            if (count <= 0)
            {
                Cursor = 0;
                return;
            }
            int scanned = 0;
            while (scanned < scanCap && scanned < count && into.Count < lookBudget)
            {
                if (Cursor >= count)
                {
                    Cursor = 0;
                    Pass++;
                }
                if (dueOf(Cursor) <= now)
                {
                    into.Add(Cursor);
                    passes?.Add(Pass);
                }
                Cursor++;
                scanned++;
            }
            if (Cursor >= count)
            {
                Cursor = 0;
                Pass++;
            }
        }

        /// <summary>The entry at <paramref name="index"/> was taken out of the list (the rest moved up one): the cursor stays
        /// on the entry it was about to pass over.</summary>
        internal void Removed(int index)
        {
            if (index < Cursor)
            {
                Cursor--;
            }
        }

        internal void Reset()
        {
            Cursor = 0;
            Pass = 0;
        }
    }

    /// <summary>
    /// The lights and stations wanting a feed, and which goes next (the research's 2.6 and 3.1): one is taken at a time (a
    /// few a frame, a setting), the best by its key first (the emptiest light, or the nearest, as "When fuel is short,
    /// first" says), but only once the round that found it has ended, or it has waited <c>maxWait</c>, so the emptiest
    /// of all those due together goes first when fuel is short. Plain values, for the harness.
    /// </summary>
    internal sealed class Wants<T> where T : class
    {
        private readonly List<Want> wants = new List<Want>();
        private long added;

        internal int Count => wants.Count;

        internal bool Has(T item) => wants.FindIndex(each => ReferenceEquals(each.Item, item)) >= 0;

        /// <summary>Adds a want found in round <paramref name="pass"/> at <paramref name="now"/>, with its key (lower goes
        /// first); one already there keeps its place in line and takes the new key.</summary>
        internal void Add(T item, int pass, float now, float key)
        {
            int found = wants.FindIndex(each => ReferenceEquals(each.Item, item));
            if (found >= 0)
            {
                wants[found].Key = key;
                return;
            }
            wants.Add(new Want { Item = item, Pass = pass, At = now, Key = key, Order = added++ });
        }

        /// <summary>The next to feed, taken out of the line: among those ready (found in an earlier round than
        /// <paramref name="pass"/>, or waiting at least <paramref name="maxWait"/>), the lowest key, then the first in line;
        /// null if none is ready.</summary>
        internal T Take(int pass, float now, float maxWait)
        {
            int best = -1;
            for (int i = 0; i < wants.Count; i++)
            {
                Want want = wants[i];
                if (want.Pass >= pass && now - want.At < maxWait)
                {
                    continue;
                }
                if (best < 0 || want.Key < wants[best].Key || (want.Key == wants[best].Key && want.Order < wants[best].Order))
                {
                    best = i;
                }
            }
            if (best < 0)
            {
                return null;
            }
            T item = wants[best].Item;
            wants.RemoveAt(best);
            return item;
        }

        internal void Remove(T item) => wants.RemoveAll(each => ReferenceEquals(each.Item, item));

        /// <summary>Drops every want <paramref name="gone"/> says is gone (destroyed, unloaded).</summary>
        internal void RemoveWhere(Func<T, bool> gone) => wants.RemoveAll(each => gone(each.Item));

        internal void Clear() => wants.Clear();

        private sealed class Want
        {
            internal T Item;
            internal int Pass;
            internal float At;
            internal float Key;
            internal long Order;
        }
    }
}
