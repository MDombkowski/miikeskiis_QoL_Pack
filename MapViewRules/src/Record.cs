using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// Keeps Cartur's Map Pins' record of the world being played at hand: finds the file by Cartur's own rule, looks
    /// every two seconds whether it changed, and reads it again when it did. It only reads: the file is opened so that
    /// Cartur's can always write it meanwhile, and nothing here ever writes to it.
    ///
    /// Cartur's rewrites the whole file within a third of a second of each new pin, so while he explores it changes often,
    /// and his holds some 2,500 lines. Reading, parsing and indexing it takes a few milliseconds, so that runs on a worker
    /// thread: the game's main thread only looks at the file's date and size, copies the facts learning needs from the
    /// pins, and takes the finished index when it is ready. The worker touches no Unity object and no pin.
    /// </summary>
    internal static class Record
    {
        private const float CheckEvery = 2f;
        private const float AgainSoon = 0.5f;

        // A file that keeps ending without a line break, unchanged this many reads in a row, isn't being written: it is
        // read as it is.
        private const int HalfWrittenTries = 3;

        private static string path;
        private static DateTime writtenAt;
        private static long length = -1;
        private static int pinsAtRead = -1;
        private static float nextCheck;
        private static DateTime halfWrittenAt;
        private static long halfWrittenLength = -1;
        private static int halfWrittenCount;
        private static bool toldMissing;
        private static bool toldRead;
        private static bool toldUnreadable;
        private static bool toldLenient;
        private static int failures;
        private static Read running;

        /// <summary>The record as last read, or null when there is none for this world yet.</summary>
        internal static RecordIndex Index { get; private set; }

        /// <summary>True once it has looked for this world's record and found none.</summary>
        internal static bool Missing { get; private set; }

        /// <summary>True while the file exists but can't be opened (after a failed read, until one succeeds).</summary>
        internal static bool Unreadable { get; private set; }

        /// <summary>Cartur's Map Pins is installed and this world's record hasn't been read yet, nor found missing or
        /// unreadable: its pins without a line are left as drawn until it has (Sorter).</summary>
        internal static bool Pending => Cartur.Installed && Index == null && !Missing && !Unreadable;

        /// <summary>A new world: forget the last one's record and look for this one's at once. A read still running
        /// for the last world is left to finish and then ignored.</summary>
        internal static void Forget()
        {
            Index = null;
            path = null;
            writtenAt = default;
            length = -1;
            pinsAtRead = -1;
            nextCheck = 0f;
            halfWrittenAt = default;
            halfWrittenLength = -1;
            halfWrittenCount = 0;
            running = null;
            failures = 0;
            Missing = false;
            Unreadable = false;
            toldMissing = toldRead = toldUnreadable = toldLenient = false;
        }

        /// <summary>Called every frame: takes a finished read, and starts a new one when the file has changed, or the map
        /// holds many more or fewer pins than when it was last read (learning needs the pins), looking at most every two
        /// seconds.</summary>
        internal static void Tick(Minimap map)
        {
            if (running != null)
            {
                if (!running.Done)
                {
                    return;   // one read at a time
                }
                Take(running);
                running = null;
            }
            // The map loads its saved pins before the game lets the player in, so waiting for the player means learning
            // from the pins, not from an empty list; and the first look comes the moment the player is there.
            if (!Cartur.Installed || Player.m_localPlayer == null || Time.realtimeSinceStartup < nextCheck)
            {
                return;
            }
            nextCheck = Time.realtimeSinceStartup + CheckEvery;

            string wanted = PathNow();
            if (wanted == null)
            {
                return;   // no world or character yet
            }
            if (wanted != path)
            {
                Forget();
                path = wanted;
                nextCheck = Time.realtimeSinceStartup + CheckEvery;
            }

            var file = new FileInfo(path);
            if (!file.Exists)
            {
                writtenAt = default;
                length = -1;
                if (Index != null || !Missing)
                {
                    Index = null;
                    Missing = true;
                    Sorter.Invalidate();
                    Drawing.RequestRedraw();
                }
                if (!toldMissing)
                {
                    toldMissing = true;
                    Module.Log.LogInfo($"Cartur's Map Pins has no record for this world and character yet ({Path.GetFileName(path)}): " +
                                       "pins with its icons follow the Other pins section until it has one.");
                }
                return;
            }
            Missing = false;
            int pinCount = GameHooks.Pins(map)?.Count ?? 0;
            bool pinsChanged = pinsAtRead >= 0 && Math.Abs(pinCount - pinsAtRead) > Math.Max(50, pinsAtRead / 10);
            if (file.LastWriteTimeUtc == writtenAt && file.Length == length && !pinsChanged)
            {
                return;
            }

            bool lenient = halfWrittenCount >= HalfWrittenTries && file.LastWriteTimeUtc == halfWrittenAt && file.Length == halfWrittenLength;
            pinsAtRead = pinCount;
            var read = new Read(path, file.LastWriteTimeUtc, file.Length, RecordIndex.PinFacts.Of(GameHooks.Pins(map)), lenient);
            running = read;
            ThreadPool.QueueUserWorkItem(_ => read.Run());
        }

        // A finished read, on the main thread: its index replaces the last one, unless it was for another world or the
        // file was being written meanwhile (then it is read again in a moment).
        private static void Take(Read read)
        {
            if (read.Path != path)
            {
                return;
            }
            if (read.Result == null)
            {
                nextCheck = Time.realtimeSinceStartup + AgainSoon;
                if (read.Problem != null)
                {
                    // Tried again less and less often (0.5 s, 1, 2, 4, 8, then every 10 s): each try reads the whole file.
                    failures++;
                    nextCheck = Time.realtimeSinceStartup + Mathf.Min(10f, AgainSoon * (1 << Math.Min(failures - 1, 5)));
                    if (!Unreadable)
                    {
                        Unreadable = true;
                        Sorter.Invalidate();   // pins left alone while it was pending are sorted without it now
                        Drawing.RequestRedraw();
                    }
                    if (!toldUnreadable)
                    {
                        toldUnreadable = true;
                        Module.Log.LogWarning($"Could not read Cartur's Map Pins' record ({Path.GetFileName(path)}): {read.Problem}; " +
                                              "pins with its icons follow the Other pins section until it can. Trying again, less and less often.");
                    }
                }
                else if (read.HalfWritten)
                {
                    if (read.WrittenAt == halfWrittenAt && read.Length == halfWrittenLength)
                    {
                        halfWrittenCount++;
                    }
                    else
                    {
                        halfWrittenAt = read.WrittenAt;
                        halfWrittenLength = read.Length;
                        halfWrittenCount = 1;
                    }
                }
                return;
            }
            writtenAt = read.WrittenAt;
            length = read.Length;
            halfWrittenCount = 0;
            failures = 0;
            Unreadable = false;
            Index = read.Result;
            Sorter.Invalidate();
            Drawing.RequestRedraw();
            if (read.Lenient && !toldLenient)
            {
                toldLenient = true;
                Module.Log.LogWarning($"Cartur's Map Pins' record ({Path.GetFileName(path)}) ends without a line break and hasn't changed " +
                                      "for a while: read as it is.");
            }
            if (!toldRead)
            {
                toldRead = true;
                Module.Log.LogInfo($"Read Cartur's Map Pins' record for this world: {Index.Count} pins, and {Index.LearnedNames} names " +
                                   $"and {Index.LearnedIcons} icons learned for sorting pins it doesn't list (a friend's, from a table).");
            }
        }

        // The file Cartur's Map Pins keeps for this world and character (its PinRecord.EnsureLoaded in 1.6.0), in
        // BepInEx's config folder: com.jekkle.valheim.carturmappins.<world>-<world id>.<character file>.pins.txt, with
        // every character but letters and digits of the names turned into "_".
        private static string PathNow()
        {
            World world = ZNet.World;
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (world == null || profile == null || string.IsNullOrEmpty(profile.m_filename))
            {
                return null;
            }
            string name = $"{Sanitize(world.m_name)}-{world.m_uid}.{Sanitize(profile.m_filename)}";
            return Path.Combine(Paths.ConfigPath, "com.jekkle.valheim.carturmappins." + name + ".pins.txt");
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unnamed";
            }
            var text = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                text.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return text.Length > 0 ? text.ToString() : "unnamed";
        }

        /// <summary>One read of the file, run on a worker thread.</summary>
        private sealed class Read
        {
            private readonly RecordIndex.PinFacts[] pins;
            private volatile bool done;

            internal Read(string path, DateTime writtenAt, long length, RecordIndex.PinFacts[] pins, bool lenient)
            {
                Path = path;
                WrittenAt = writtenAt;
                Length = length;
                this.pins = pins;
                Lenient = lenient;
            }

            internal string Path { get; }

            internal DateTime WrittenAt { get; }

            internal long Length { get; }

            /// <summary>Reads a file that ends without a line break as it is (it has stopped changing).</summary>
            internal bool Lenient { get; }

            /// <summary>The index, or null if the file couldn't be read whole this time.</summary>
            internal RecordIndex Result { get; private set; }

            /// <summary>Why the file couldn't be opened or read at all, or null.</summary>
            internal string Problem { get; private set; }

            /// <summary>The file ended without a line break: Cartur's may have been writing it.</summary>
            internal bool HalfWritten { get; private set; }

            internal bool Done => done;

            internal void Run()
            {
                try
                {
                    string text = ReadShared(Path, out string problem);
                    if (text == null)
                    {
                        Problem = problem;
                        return;
                    }
                    // Cartur's was writing it while it was read: its date or size moved (read again soon).
                    var after = new FileInfo(Path);
                    if (!after.Exists || after.LastWriteTimeUtc != WrittenAt || after.Length != Length)
                    {
                        return;
                    }
                    if (!RecordIndex.TryParse(text, out var lines, Lenient))
                    {
                        HalfWritten = true;
                        return;
                    }
                    Result = RecordIndex.Build(lines, pins);
                }
                catch (Exception error)
                {
                    Problem = error.Message;
                    Result = null;
                }
                finally
                {
                    done = true;
                }
            }

            // Reads the whole file, sharing it for reading, writing and deleting so that Cartur's can rewrite it at any
            // moment meanwhile; null if it can't be read right now.
            private static string ReadShared(string file, out string problem)
            {
                problem = null;
                try
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (IOException error)
                {
                    problem = error.Message;
                    return null;
                }
                catch (UnauthorizedAccessException error)
                {
                    problem = error.Message;
                    return null;
                }
            }
        }
    }
}
