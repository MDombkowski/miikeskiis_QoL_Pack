using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UseCase
{
    /// <summary>
    /// The altars and locked doors each character has seen, for the spoiler rule (the game keeps no "known" for them;
    /// research section 4). One plain file in the profile's BepInEx\config, modprojects.usecase.seen.txt, one line per
    /// place seen: the character's name, a tab, the place's key. Nothing is written into the game's own saves. A file that
    /// can't be read is never written over (LESSONS_LEARNED: "couldn't read it" must never look like "empty"); the places
    /// seen this session are then kept only until the game closes.
    /// </summary>
    internal static class Seen
    {
        internal const string FileName = "modprojects.usecase.seen.txt";

        private static readonly HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
        private static string path;
        private static bool read;
        private static bool readFailed;

        /// <summary>Goes up whenever a place is learned, so the panel's words are made again.</summary>
        internal static int Version { get; private set; }

        /// <summary>Where the file is: the profile's config folder, set by the module (and by the harness).</summary>
        internal static string Folder;

        internal static bool HasSeen(string character, string key)
        {
            Load();
            return !string.IsNullOrEmpty(key) && lines.Contains(Line(character, key));
        }

        /// <summary>Remembers that this character has seen the place; true when it is new.</summary>
        internal static bool Learn(string character, string key, Action<string> warn)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(character))
            {
                return false;
            }
            Load();
            if (!lines.Add(Line(character, key)))
            {
                return false;
            }
            Version++;
            if (readFailed || path == null)
            {
                return true;
            }
            try
            {
                File.AppendAllText(path, Line(character, key) + Environment.NewLine);
            }
            catch (Exception e)
            {
                readFailed = true;   // written no more this session, so a half-written file is never written over
                warn?.Invoke($"Couldn't write {FileName}, so the altars and doors seen now are remembered only until the game closes: {e.Message}");
            }
            return true;
        }

        /// <summary>Forgets what was read, for the harness.</summary>
        internal static void Reset()
        {
            lines.Clear();
            read = false;
            readFailed = false;
            path = null;
            Version++;
        }

        private static void Load()
        {
            if (read)
            {
                return;
            }
            read = true;
            if (string.IsNullOrEmpty(Folder))
            {
                readFailed = true;
                return;
            }
            path = Path.Combine(Folder, FileName);
            try
            {
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path).Where(each => each.Contains('\t')))
                    {
                        lines.Add(line.Trim('\r', '\n'));
                    }
                }
            }
            catch (Exception)
            {
                readFailed = true;
            }
        }

        // The character's name can't hold a tab; the key is the place's own (Places).
        private static string Line(string character, string key) => character + "\t" + key;
    }
}
