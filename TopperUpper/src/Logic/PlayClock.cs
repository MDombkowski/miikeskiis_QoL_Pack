using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TopperUpper.Logic
{
    /// <summary>A span of world time, in seconds (ZNet's time: the world's own clock, which a client shares with the
    /// server).</summary>
    internal readonly struct Span
    {
        internal readonly double Start;
        internal readonly double End;

        internal Span(double start, double end)
        {
            Start = start;
            End = end;
        }

        internal double Length => Math.Max(0.0, End - Start);
    }

    /// <summary>
    /// The catch-up's clock (card A93, his addition 1: only time with players on the server counts, never time it stood
    /// empty), on plain values. Three ways to know it, in order (CatchUps picks): the server pack's (the spans of world
    /// time the server stood empty, complete from a world time on: time with players on is the time less those gaps); in
    /// single player or hosting, the world's own time (it runs only while the world does); on a server without the server
    /// pack, this game's own time online in that world (spans kept with the character), which errs toward less: time when
    /// only others were on goes uncounted. Spans are written "start:end;start:end" in world seconds, the invariant
    /// culture, oldest first.
    /// </summary>
    internal static class PlayClock
    {
        /// <summary>
        /// Reads "start:end;start:end…" (the server pack's "clock.gaps", or the spans kept with the character). An empty
        /// text is no spans. False, and no spans, for any entry that isn't two numbers with the end not before the start.
        /// </summary>
        internal static bool TryParse(string text, out List<Span> spans)
        {
            spans = new List<Span>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            foreach (string raw in text.Split(';'))
            {
                string entry = raw.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }
                int at = entry.IndexOf(':');
                if (at <= 0 || !TryNumber(entry.Substring(0, at), out double start) || !TryNumber(entry.Substring(at + 1), out double end) || end < start)
                {
                    spans.Clear();
                    return false;
                }
                spans.Add(new Span(start, end));
            }
            return true;
        }

        /// <summary>A world time as the server pack writes it: a finite number in the invariant culture.</summary>
        internal static bool TryNumber(string text, out double value) =>
            double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>Spans written back ("start:end;…", to the millisecond).</summary>
        internal static string Format(IList<Span> spans)
        {
            var text = new StringBuilder();
            foreach (Span span in spans)
            {
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.Append(span.Start.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(span.End.ToString("0.###", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>Spans sorted, and those that overlap or touch made one.</summary>
        internal static List<Span> Merge(IEnumerable<Span> spans)
        {
            var sorted = new List<Span>();
            foreach (Span span in spans)
            {
                if (span.End >= span.Start)
                {
                    sorted.Add(span);
                }
            }
            sorted.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
            var merged = new List<Span>();
            foreach (Span span in sorted)
            {
                if (merged.Count > 0 && span.Start <= merged[merged.Count - 1].End)
                {
                    Span last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new Span(last.Start, Math.Max(last.End, span.End));
                }
                else
                {
                    merged.Add(span);
                }
            }
            return merged;
        }

        /// <summary>How much of [<paramref name="from"/>, <paramref name="to"/>] the spans cover (each moment once, however
        /// the spans overlap).</summary>
        internal static double Overlap(double from, double to, IEnumerable<Span> spans)
        {
            if (!(to > from) || spans == null)
            {
                return 0.0;
            }
            double sum = 0.0;
            foreach (Span span in Merge(spans))
            {
                double start = Math.Max(from, span.Start);
                double end = Math.Min(to, span.End);
                if (end > start)
                {
                    sum += end - start;
                }
            }
            return sum;
        }

        /// <summary>
        /// The time with players on between two world times, by the server pack's clock: from <paramref name="since"/> on,
        /// the time less the gaps (when the server stood empty); before <paramref name="since"/>, where its list isn't
        /// complete, only what <paramref name="ownSpans"/> (this game's own time online) covers. Never negative.
        /// </summary>
        internal static double WithGaps(double from, double to, IList<Span> gaps, double since, IList<Span> ownSpans)
        {
            if (!(to > from))
            {
                return 0.0;
            }
            double known = Math.Max(from, since);
            double played = 0.0;
            if (to > known)
            {
                played += (to - known) - Overlap(known, to, gaps);
            }
            if (known > from)
            {
                played += Overlap(from, Math.Min(known, to), ownSpans);
            }
            return Math.Max(0.0, played);
        }

        /// <summary>
        /// This game's own spans online in one world, after a moment online now: the session's span
        /// [<paramref name="sessionStart"/>, <paramref name="now"/>] added to those kept, merged; a span starting after now
        /// (the world's time went back: a restore from a backup) dropped, one reaching past now cut at now; then only the
        /// newest kept, while their total stays within <paramref name="keepPlayed"/> seconds (enough for the longest cap)
        /// and at most <paramref name="mostSpans"/> of them.
        /// </summary>
        internal static List<Span> Online(IEnumerable<Span> kept, double sessionStart, double now, double keepPlayed, int mostSpans)
        {
            var all = new List<Span>();
            if (kept != null)
            {
                foreach (Span span in kept)
                {
                    if (span.Start <= now)
                    {
                        all.Add(new Span(span.Start, Math.Min(span.End, now)));
                    }
                }
            }
            if (sessionStart <= now)
            {
                all.Add(new Span(sessionStart, now));
            }
            List<Span> merged = Merge(all);
            var newest = new List<Span>();
            double total = 0.0;
            for (int i = merged.Count - 1; i >= 0 && newest.Count < Math.Max(1, mostSpans); i--)
            {
                newest.Add(merged[i]);
                total += merged[i].Length;
                if (total >= keepPlayed)
                {
                    break;
                }
            }
            newest.Reverse();
            return newest;
        }
    }
}
