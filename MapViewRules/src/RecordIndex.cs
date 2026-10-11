using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// Cartur's Map Pins' record of the pins it placed, as this mod reads it: which pin is what, looked up by where the pin
    /// stands, and what its record teaches about the names and icons of pins that aren't in it.
    ///
    /// The record is a text file Cartur's rewrites whole within a third of a second of any change (PinRecord.Save in its
    /// 1.6.0): one line per pin, "key|x|y|z|label|written" (older lines have only the first four), with the key
    /// "Category" or "Category:Kind", the position in invariant culture, the label before translation and the exact text
    /// Cartur's last wrote on the pin. Cartur's matches a line to its pin within 0.5 m (PinRecord.FindPinAt); so does this.
    /// This class only reads: nothing here ever writes the file.
    /// </summary>
    internal sealed class RecordIndex
    {
        /// <summary>How close a pin must stand to a record line to be that line's pin, in metres (Cartur's own).</summary>
        internal const float MatchRadius = 0.5f;

        /// <summary>The first pin type the game doesn't define: Cartur's own icons start above it (100 and up).</summary>
        internal const int FirstCustomType = 18;

        // A learned name or icon must mean one key in at least this share of the pins that have it.
        private const float Dominance = 0.9f;

        // An icon is learned only from at least this many pins: several of Cartur's kinds share one picture.
        private const int FewestForIcon = 3;

        private readonly Dictionary<long, List<Line>> grid = new Dictionary<long, List<Line>>();
        private readonly Dictionary<string, string> keyOfName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> keyOfIcon = new Dictionary<int, string>();

        private RecordIndex()
        {
        }

        /// <summary>How many lines the record holds.</summary>
        internal int Count { get; private set; }

        /// <summary>How many names and icons were learned, for the log.</summary>
        internal int LearnedNames => keyOfName.Count;

        internal int LearnedIcons => keyOfIcon.Count;

        /// <summary>
        /// Reads the record's text. False when it looks half-written: Cartur's rewrites the whole file, so a read can meet
        /// it cut short, and a complete file ends every line, the last included, with a line break. An empty text is a
        /// complete, empty record. Lines this can't read are skipped, as Cartur's itself skips them.
        /// </summary>
        /// <param name="lenient">Reads a text that ends without a line break as it is: the file has stopped changing
        /// that way, so it isn't being written (Record).</param>
        internal static bool TryParse(string text, out List<Line> lines, bool lenient = false)
        {
            lines = new List<Line>();
            if (text == null)
            {
                return false;
            }
            if (!lenient && text.Length > 0 && text[text.Length - 1] != '\n')
            {
                return false;
            }
            foreach (string row in text.Split('\n'))
            {
                string[] fields = row.TrimEnd('\r').Split('|');
                if ((fields.Length != 4 && fields.Length != 6) || fields[0].Length == 0)
                {
                    continue;
                }
                if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float _) ||
                    !float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    continue;
                }
                lines.Add(new Line(fields[0], x, z, fields.Length == 6 ? fields[5] : null));
            }
            return true;
        }

        /// <summary>
        /// Indexes the record's lines by position, then learns from them and from the pins on the map now: the text
        /// Cartur's wrote on each pin, and the name and icon of each pin that stands on a line, tell which key a pin
        /// with that name or icon but no line of its own (a friend's, from a table) most likely has.
        /// </summary>
        /// <remarks>Touches no Unity object and no pin, only the facts copied from them, so it can run away from the
        /// game's main thread (Record reads the file there).</remarks>
        internal static RecordIndex Build(List<Line> lines, IList<PinFacts> pins)
        {
            var index = new RecordIndex { Count = lines.Count };
            var names = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var icons = new Dictionary<int, Dictionary<string, int>>();
            foreach (Line line in lines)
            {
                long cell = Cell(Mathf.FloorToInt(line.X), Mathf.FloorToInt(line.Z));
                if (!index.grid.TryGetValue(cell, out List<Line> inCell))
                {
                    index.grid[cell] = inCell = new List<Line>(1);
                }
                inCell.Add(line);
                Tally(names, line.Written, line.Key);
            }
            if (pins != null)
            {
                foreach (PinFacts pin in pins)
                {
                    if (!pin.Save)
                    {
                        continue;
                    }
                    string key = index.KeyAt(pin.Position);
                    if (key == null)
                    {
                        continue;
                    }
                    Tally(names, pin.Name, key);
                    if (pin.Type >= FirstCustomType)
                    {
                        Tally(icons, pin.Type, key);
                    }
                }
            }
            Learn(names, index.keyOfName, 1);
            Learn(icons, index.keyOfIcon, FewestForIcon);
            return index;
        }

        /// <summary>The key of the record line nearest the position, within MatchRadius, or null.</summary>
        internal string KeyAt(Vector3 position)
        {
            int cellX = Mathf.FloorToInt(position.x);
            int cellZ = Mathf.FloorToInt(position.z);
            string best = null;
            float bestDistance = MatchRadius * MatchRadius;
            for (int x = cellX - 1; x <= cellX + 1; x++)
            {
                for (int z = cellZ - 1; z <= cellZ + 1; z++)
                {
                    if (!grid.TryGetValue(Cell(x, z), out List<Line> inCell))
                    {
                        continue;
                    }
                    foreach (Line line in inCell)
                    {
                        float dx = line.X - position.x;
                        float dz = line.Z - position.z;
                        float distance = dx * dx + dz * dz;
                        if (distance < bestDistance)   // strictly within, as Cartur's; a tie keeps the first line found
                        {
                            best = line.Key;
                            bestDistance = distance;
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>The key a pin of this name has, learned from the record, or null.</summary>
        internal string KeyForName(string name) =>
            !string.IsNullOrEmpty(name) && keyOfName.TryGetValue(name, out string key) ? key : null;

        /// <summary>The key a pin with this icon has, learned from the record, or null.</summary>
        internal string KeyForIcon(int type) => keyOfIcon.TryGetValue(type, out string key) ? key : null;

        private static long Cell(int x, int z) => ((long)x << 32) | (uint)z;

        private static void Tally<T>(Dictionary<T, Dictionary<string, int>> counts, T what, string key)
        {
            if (what == null || (what is string text && text.Length == 0))
            {
                return;
            }
            if (!counts.TryGetValue(what, out Dictionary<string, int> keys))
            {
                counts[what] = keys = new Dictionary<string, int>(StringComparer.Ordinal);
            }
            keys.TryGetValue(key, out int count);
            keys[key] = count + 1;
        }

        // Keeps each name or icon whose most common key holds at least Dominance of its pins.
        private static void Learn<T>(Dictionary<T, Dictionary<string, int>> counts, Dictionary<T, string> learned, int fewest)
        {
            foreach (KeyValuePair<T, Dictionary<string, int>> each in counts)
            {
                int total = 0;
                int bestCount = 0;
                string best = null;
                foreach (KeyValuePair<string, int> key in each.Value)
                {
                    total += key.Value;
                    if (key.Value > bestCount || (key.Value == bestCount && string.CompareOrdinal(key.Key, best) < 0))
                    {
                        best = key.Key;
                        bestCount = key.Value;
                    }
                }
                if (total >= fewest && bestCount >= Dominance * total)
                {
                    learned[each.Key] = best;
                }
            }
        }

        /// <summary>One line of the record: its key, where its pin stands, and the text Cartur's last wrote on it.</summary>
        internal readonly struct Line
        {
            internal readonly string Key;
            internal readonly float X;
            internal readonly float Z;
            internal readonly string Written;

            internal Line(string key, float x, float z, string written)
            {
                Key = key;
                X = x;
                Z = z;
                Written = written;
            }
        }

        /// <summary>What learning needs of a pin, copied on the game's main thread so the learning can run elsewhere.</summary>
        internal readonly struct PinFacts
        {
            internal readonly string Name;
            internal readonly int Type;
            internal readonly Vector3 Position;
            internal readonly bool Save;

            internal PinFacts(string name, int type, Vector3 position, bool save)
            {
                Name = name;
                Type = type;
                Position = position;
                Save = save;
            }

            /// <summary>The facts of every pin in the list, or none.</summary>
            internal static PinFacts[] Of(List<Minimap.PinData> pins)
            {
                if (pins == null)
                {
                    return new PinFacts[0];
                }
                var facts = new PinFacts[pins.Count];
                for (int i = 0; i < pins.Count; i++)
                {
                    Minimap.PinData pin = pins[i];
                    facts[i] = pin == null ? default : new PinFacts(pin.m_name, (int)pin.m_type, pin.m_pos, pin.m_save);
                }
                return facts;
            }
        }
    }
}
