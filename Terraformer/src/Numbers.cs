using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Terraformer
{
    /// <summary>
    /// The values a stroke is made with. A round tool uses its radius, and its sharpness if it changes height; a raising
    /// tool its lift (Lower ground's is negative: how deep it digs); a square tool the points along its facing, and across
    /// it for a rectangle (B02); Slope its plane and how it moves its points. Which ones were chosen (rather than left at
    /// the tool's own) is in <see cref="Chosen"/>, so a stroke never changes what its player didn't.
    /// </summary>
    internal struct Numbers
    {
        internal float Radius;
        internal float Sharpness;
        internal float Lift;

        /// <summary>A square tool's points along its facing (and across it, unless <see cref="Chosen.Across"/>).</summary>
        internal int Points;

        /// <summary>A rectangle's points across its facing, with <see cref="Chosen.Across"/>.</summary>
        internal int Width;

        /// <summary>Slope's plane, with <see cref="Chosen.Plane"/>.</summary>
        internal SlopePlane Plane;

        internal SlopeFlags Slope;

        internal Chosen Chosen;

        /// <summary>The points across a square tool's facing: its width for a rectangle, else as many as along it.</summary>
        internal int Across => (Chosen & Chosen.Across) != 0 ? Width : Points;
    }

    [Flags]
    internal enum Chosen : byte
    {
        None = 0,
        Radius = 1,
        Sharpness = 2,
        Lift = 4,
        Points = 8,

        /// <summary>The stroke only paints: its raise, level and smooth steps are left out wherever it's applied (A99).</summary>
        PaintOnly = 16,

        /// <summary>A square tool's width differs from its length: a rectangle (B02).</summary>
        Across = 32,

        /// <summary>A Slope stroke's plane and its way of moving points travel with it (B02).</summary>
        Plane = 64,
    }

    /// <summary>
    /// A terrain tool's own values, read from its settings: the ones it has in the game, before anyone resizes it.
    /// Sharpness uses AdvancedTerrainModifiers' scales, so his percentages mean what they meant there: a smoothing
    /// strength from 1 to 30 and a raising strength from 1 down to 0.05 both read as 0 to 100 %.
    /// </summary>
    internal readonly struct Facts
    {
        internal const float SoftestSmooth = 1f;
        internal const float SharpestSmooth = 30f;
        internal const float SoftestRaise = 1f;
        internal const float SharpestRaise = 0.05f;

        /// <summary>A square tool's own size, in points along an edge: 4 m (his word, 2026-09-30; 3 until 0.2.0).</summary>
        internal const int StartPoints = 5;

        /// <summary>The shallowest Lower ground (precision) can dig a click, in metres.</summary>
        internal const float ShallowestDepth = 0.01f;

        /// <param name="digDepth">Lower ground's depth a click, from its setting ("Lower ground depth").</param>
        internal Facts(TerrainOp.Settings settings, Tool tool, float digDepth)
        {
            Square = tool != null && tool.Square;
            DigsOnly = tool != null && tool.IsPrecisionLower;
            Slopes = tool != null && tool.IsSlope;
            Radius = settings.GetRadius();
            float height = 0f;
            if (settings.m_level)
            {
                height = Math.Max(height, settings.m_levelRadius);
            }
            if (settings.m_raise)
            {
                height = Math.Max(height, settings.m_raiseRadius);
            }
            if (settings.m_smooth)
            {
                height = Math.Max(height, settings.m_smoothRadius);
            }
            HeightRadius = height > 0f ? Math.Min(height, Radius) : Radius;
            Smooths = settings.m_smooth;
            Raises = settings.m_raise;
            if (settings.m_smooth)
            {
                Sharpness = SharpnessOfSmooth(settings.m_smoothPower);
            }
            else if (settings.m_raise)
            {
                Sharpness = SharpnessOfRaise(settings.m_raisePower);
            }
            else
            {
                Sharpness = 0f;
            }
            if (Slopes)
            {
                // Slope's sharpness is its edge, its own from the setting "Edge" (50 %, soft, out of the box).
                Sharpness = SlopeTool.StartingEdge;
            }
            Lift = settings.m_raise ? settings.m_raiseDelta : 0f;
            if (tool != null && tool.IsLowerGround)
            {
                Lift = -Math.Abs(digDepth);
            }
            if (DigsOnly)
            {
                Lift = -Math.Max(ShallowestDepth, Math.Abs(settings.m_raiseDelta));
            }
            Points = StartPoints;
        }

        internal bool Square { get; }

        /// <summary>Lower ground (precision): its lift is a depth, always below zero.</summary>
        internal bool DigsOnly { get; }

        /// <summary>Slope (B02): its sharpness is its edge, round or square.</summary>
        internal bool Slopes { get; }

        /// <summary>Its reach: the largest radius among its steps that are on. A stroke's radius travels as its reach.</summary>
        internal float Radius { get; }

        /// <summary>The radius of the ground it moves up or down (the paint's for a tool that only paints): the radius he
        /// sees and sets (since 0.2.0; his first test). At most its reach.</summary>
        internal float HeightRadius { get; }

        /// <summary>How far its paint reaches past the ground it moves (Raise ground's 0.5 m, the dig's 1 m): kept as it
        /// is whatever size he sets.</summary>
        internal float Rim => Radius - HeightRadius;

        internal bool Smooths { get; }

        internal bool Raises { get; }

        /// <summary>Whether sharpness means anything for it: a round tool that changes height, and Slope (its edge).</summary>
        internal bool CanSharpen => Slopes || (!Square && (Smooths || Raises));

        internal float Sharpness { get; }

        internal float Lift { get; }

        internal int Points { get; }

        internal static float SharpnessOfSmooth(float power) =>
            Clamp((power - SoftestSmooth) / (SharpestSmooth - SoftestSmooth) * 100f, 0f, 100f);

        internal static float SharpnessOfRaise(float power) =>
            Clamp((SoftestRaise - power) / (SoftestRaise - SharpestRaise) * 100f, 0f, 100f);

        internal static float SmoothPowerOf(float sharpness) =>
            SoftestSmooth + (SharpestSmooth - SoftestSmooth) * Clamp(sharpness, 0f, 100f) / 100f;

        internal static float RaisePowerOf(float sharpness) =>
            SoftestRaise - (SoftestRaise - SharpestRaise) * Clamp(sharpness, 0f, 100f) / 100f;

        internal static float Clamp(float value, float low, float high) => value < low ? low : value > high ? high : value;
    }

    /// <summary>
    /// The bounds the settings put on a tool's values now. Remembered values are held to them whenever they're used, so
    /// lowering a limit, or switching "Square tools resize" or "Precision raise can dig" off, takes effect at once, and
    /// raising it again brings the remembered value back (review round 1).
    /// </summary>
    internal readonly struct Limits
    {
        internal Limits(float largestRadius, bool squaresResize, int largestSquare, float largestLift, bool liftCanDig,
                        RectanglesFor rectangles = RectanglesFor.EverySquareTool)
        {
            LargestRadius = Math.Max(Settings.SmallestRadius, largestRadius);
            SquaresResize = squaresResize;
            LargestSquare = Footprint.OddWithin(largestSquare);
            LargestLift = Math.Abs(largestLift);
            LiftCanDig = liftCanDig;
            Rectangles = rectangles;
        }

        /// <summary>Which square tools can be rectangles ("Rectangles for").</summary>
        internal RectanglesFor Rectangles { get; }

        /// <summary>Whether a square tool with these facts can be a rectangle now.</summary>
        internal bool AllowsRectangle(Facts facts) =>
            facts.Square && SquaresResize && (facts.Slopes || Rectangles == RectanglesFor.EverySquareTool);

        internal float LargestRadius { get; }

        internal bool SquaresResize { get; }

        internal int LargestSquare { get; }

        internal float LargestLift { get; }

        internal bool LiftCanDig { get; }

        /// <summary>The limits as the settings say now.</summary>
        internal static Limits Now => new Limits(Settings.LargestRadius.Value, Settings.SquaresResize.Value, Settings.LargestSquare.Value,
                                                 Settings.LargestLift.Value, Settings.LiftCanDig.Value, SlopeTool.RectanglesNow);
    }

    /// <summary>
    /// Each terrain tool's remembered values, by its prefab's name: what he last chose for it. Kept while the game runs,
    /// and between game sessions in a hidden setting ("Remembered tools") when "Keep them after quitting" is on, written
    /// a few seconds after a change rather than at every notch (every write saves the whole settings file).
    /// </summary>
    internal static class Memory
    {
        internal sealed class Entry
        {
            internal float? Radius;
            internal float? Sharpness;
            internal float? Lift;
            internal int? Points;

            /// <summary>A rectangle's width, set with the width key (B02).</summary>
            internal int? Width;

            internal bool IsEmpty => Radius == null && Sharpness == null && Lift == null && Points == null && Width == null;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        /// <summary>Seconds after the last change before it's written to the settings file.</summary>
        internal const float WriteDelay = 3f;

        private static bool dirty;
        private static float changedAt;
        private static bool loaded;
        private static bool unsaved;
        private static bool warnedUnsaved;

        /// <summary>The values a stroke of this piece would use now: the tool's own, with what he chose over them, held to
        /// the settings' limits as they are now.</summary>
        internal static Numbers For(string prefab, Facts facts) => For(prefab, facts, Limits.Now);

        internal static Numbers For(string prefab, Facts facts, Limits limits)
        {
            var numbers = new Numbers { Radius = facts.Radius, Sharpness = facts.Sharpness, Lift = facts.Lift, Points = facts.Points };
            if (facts.Square)
            {
                numbers.Points = Footprint.OddWithin(Math.Min(facts.Points, limits.LargestSquare));
            }
            Entry entry = null;
            if (prefab != null && Entries.TryGetValue(prefab, out entry))
            {
                if (entry.Radius.HasValue && !facts.Square)
                {
                    // A radius is kept as the stroke's reach, and held to the limits by the ground it moves (the radius he
                    // sets and sees since 0.2.0); the painted rim rides on top, as the game's.
                    float height = Facts.Clamp(entry.Radius.Value - facts.Rim, Settings.SmallestRadius, limits.LargestRadius);
                    numbers.Radius = height + facts.Rim;
                    numbers.Chosen |= Chosen.Radius;
                }
                if (entry.Sharpness.HasValue && facts.CanSharpen)
                {
                    numbers.Sharpness = Facts.Clamp(entry.Sharpness.Value, 0f, 100f);
                    numbers.Chosen |= Chosen.Sharpness;
                }
                // Only the precision tools' lift is set with the keys; Lower ground's depth is its own setting (review 2).
                // Lower ground (precision)'s lift is its depth, always below zero.
                if (entry.Lift.HasValue && facts.Raises && facts.Square)
                {
                    float lowest = facts.DigsOnly || limits.LiftCanDig ? -limits.LargestLift : 0f;
                    float highest = facts.DigsOnly ? -Facts.ShallowestDepth : limits.LargestLift;
                    numbers.Lift = Facts.Clamp(entry.Lift.Value, lowest, highest);
                    numbers.Chosen |= Chosen.Lift;
                }
                if (entry.Points.HasValue && facts.Square && limits.SquaresResize)
                {
                    numbers.Points = Footprint.OddWithin(Math.Min(entry.Points.Value, limits.LargestSquare));
                    numbers.Chosen |= Chosen.Points;
                }
            }
            // A square's width is its length until the width key sets one of its own (B02), held to the same limits, and
            // only for the tools "Rectangles for" names; a width equal to the length is a square again.
            numbers.Width = numbers.Points;
            if (entry != null && entry.Width.HasValue && limits.AllowsRectangle(facts))
            {
                int width = Footprint.OddWithin(Math.Min(entry.Width.Value, limits.LargestSquare));
                if (width != numbers.Points)
                {
                    numbers.Width = width;
                    numbers.Chosen |= Chosen.Across;
                }
            }
            return numbers;
        }

        internal static void SetRadius(string prefab, float value, float now) => Change(prefab, now, entry => entry.Radius = value);

        internal static void SetSharpness(string prefab, float value, float now) => Change(prefab, now, entry => entry.Sharpness = value);

        internal static void SetLift(string prefab, float value, float now) => Change(prefab, now, entry => entry.Lift = value);

        internal static void SetPoints(string prefab, int value, float now) => Change(prefab, now, entry => entry.Points = value);

        /// <summary>A rectangle's width; null (a width equal to its length) makes it a square again, which then grows on
        /// both sides alike.</summary>
        internal static void SetWidth(string prefab, int? value, float now) => Change(prefab, now, entry => entry.Width = value);

        /// <summary>Forgets what he chose for this tool: it's back to its own values. True if there was anything.</summary>
        internal static bool Reset(string prefab, float now)
        {
            if (prefab == null || !Entries.Remove(prefab))
            {
                return false;
            }
            dirty = true;
            changedAt = now;
            return true;
        }

        /// <summary>Forgets every tool's values ("Remember each tool's settings" off, on leaving building).</summary>
        internal static void ForgetAll(float now)
        {
            if (Entries.Count == 0)
            {
                return;
            }
            Entries.Clear();
            dirty = true;
            changedAt = now;
        }

        private static void Change(string prefab, float now, Action<Entry> change)
        {
            if (prefab == null)
            {
                return;
            }
            if (!Entries.TryGetValue(prefab, out Entry entry))
            {
                entry = new Entry();
                Entries[prefab] = entry;
            }
            change(entry);
            dirty = true;
            changedAt = now;
        }

        /// <summary>Reads the remembered values from the hidden setting, once, if they're kept between sessions.</summary>
        internal static void LoadOnce()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            if (!Settings.RememberAfterQuitting.Value)
            {
                return;
            }
            foreach (KeyValuePair<string, Entry> pair in Parse(Settings.Remembered.Value))
            {
                Entries[pair.Key] = pair.Value;
            }
        }

        /// <summary>Writes the values to the hidden setting when they changed a while ago, or at once when told to. A
        /// settings file that can't be written just now (held by an antivirus scan or a cloud sync) is tried again a few
        /// seconds later, rather than stopping the mod (review round 1).</summary>
        internal static void WriteIfDue(float now, bool atOnce = false)
        {
            if (!dirty || (!atOnce && now - changedAt < WriteDelay))
            {
                return;
            }
            dirty = false;
            if (!Settings.RememberAfterQuitting.Value)
            {
                return;
            }
            string text = Format(Entries);
            try
            {
                if (Settings.Remembered.Value != text)
                {
                    Settings.Remembered.Value = text;   // BepInEx saves the file as it's set
                }
                else if (unsaved)
                {
                    Settings.Remembered.ConfigFile.Save();
                }
                unsaved = false;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                unsaved = true;
                dirty = true;
                changedAt = now;
                if (!warnedUnsaved)
                {
                    warnedUnsaved = true;
                    Module.Log.LogWarning($"The remembered tool sizes couldn't be saved just now ({e.Message}); trying again.");
                }
            }
        }

        // "prefab:r=4.5,s=60,l=0.35,p=5;prefab2:…", with a full stop for decimals whatever the computer's language.
        internal static string Format(Dictionary<string, Entry> entries)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                if (pair.Value.IsEmpty || pair.Key.IndexOfAny(new[] { ':', ';', ',', '=' }) >= 0)
                {
                    continue;
                }
                var parts = new List<string>();
                if (pair.Value.Radius.HasValue)
                {
                    parts.Add("r=" + pair.Value.Radius.Value.ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (pair.Value.Sharpness.HasValue)
                {
                    parts.Add("s=" + pair.Value.Sharpness.Value.ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (pair.Value.Lift.HasValue)
                {
                    parts.Add("l=" + pair.Value.Lift.Value.ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (pair.Value.Points.HasValue)
                {
                    parts.Add("p=" + pair.Value.Points.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (pair.Value.Width.HasValue)
                {
                    parts.Add("w=" + pair.Value.Width.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.Append(pair.Key).Append(':').Append(string.Join(",", parts));
            }
            return text.ToString();
        }

        internal static Dictionary<string, Entry> Parse(string text)
        {
            var entries = new Dictionary<string, Entry>();
            if (string.IsNullOrEmpty(text))
            {
                return entries;
            }
            foreach (string item in text.Split(';'))
            {
                int colon = item.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                string prefab = item.Substring(0, colon).Trim();
                var entry = new Entry();
                foreach (string part in item.Substring(colon + 1).Split(','))
                {
                    int equals = part.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }
                    string key = part.Substring(0, equals).Trim();
                    string value = part.Substring(equals + 1).Trim();
                    if (key == "p" || key == "w")
                    {
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                        {
                            if (key == "p")
                            {
                                entry.Points = points;
                            }
                            else
                            {
                                entry.Width = points;
                            }
                        }
                        continue;
                    }
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                        float.IsNaN(number) || float.IsInfinity(number))
                    {
                        continue;
                    }
                    switch (key)
                    {
                        case "r": entry.Radius = number; break;
                        case "s": entry.Sharpness = number; break;
                        case "l": entry.Lift = number; break;
                    }
                }
                if (prefab.Length > 0 && !entry.IsEmpty)
                {
                    entries[prefab] = entry;
                }
            }
            return entries;
        }

        /// <summary>For the harness: the values remembered for a prefab, as the file would hold them.</summary>
        internal static string Describe(string prefab) =>
            prefab != null && Entries.TryGetValue(prefab, out Entry entry) ? Format(new Dictionary<string, Entry> { [prefab] = entry }) : string.Empty;
    }
}
