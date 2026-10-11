// The QoL Mods item marks, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemMarks\. Every mod that uses it
// carries a copy in its own parts\ItemMarks\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace QoLMods.ItemMarks
{
    /// <summary>
    /// One kind of mark on item types (card A52): favourites, and trash marks later (card A53). A mark belongs to every item
    /// of a type, by the game's own name for it ("$item_wood"), whatever its quality, so an item picked up later is marked
    /// too. The list lives in a plain file in the mod profile's BepInEx\config folder, one name per line, as
    /// HexQuickStackStorage kept its own: per mod profile, not per character. The bundle and any single release carrying
    /// the part read the same file, so it is read again whenever it changes on disk, and written on every change, never
    /// after a failed read (which would lose his marks).
    /// </summary>
    internal sealed class MarkList
    {
        /// <summary>Where the lists live: the mod profile's BepInEx\config. Only the scratch test harness sets another.</summary>
        internal static Func<string> Folder = () => Paths.ConfigPath;

        /// <summary>The clock for how often the file is looked at. Only the scratch test harness sets another.</summary>
        internal static Func<float> Clock = () => Time.realtimeSinceStartup;

        /// <summary>Reads a list's file. Only the scratch test harness sets another (to make a read fail).</summary>
        internal static Func<string, string[]> ReadLines = File.ReadAllLines;

        /// <summary>Reads a list's file's date. Only the scratch test harness sets another (to make it fail).</summary>
        internal static Func<string, DateTime> LastWrite = File.GetLastWriteTimeUtc;

        /// <summary>How often, at most, the file's date is looked at for a change made elsewhere, in seconds.</summary>
        internal const float CheckEvery = 1f;

        private const string ImportedNote = "# imported: ";

        private readonly string about;
        private HashSet<string> names = new HashSet<string>();
        private readonly List<string> imported = new List<string>();
        private bool loaded;
        private bool readFailed;
        private DateTime stamp;
        private float checkedAt = float.NegativeInfinity;

        /// <param name="kind">The kind, in the file's name: modprojects.marks.&lt;kind&gt;.txt. Permanent.</param>
        /// <param name="about">One line for the file's head, saying what the list is for.</param>
        internal MarkList(string kind, string about)
        {
            Kind = kind;
            this.about = about;
        }

        internal string Kind { get; }

        internal string FilePath => Path.Combine(Folder(), $"modprojects.marks.{Kind}.txt");

        /// <summary>
        /// False while the list's file can't be read: the list is kept as it was (empty, if it was never read), is read
        /// again every second, and is never written meanwhile. A module that acts on an item not being marked checks this
        /// first, or it would treat his favourites as ordinary items (StoreStash stacks nothing then; trash must delete
        /// nothing).
        /// </summary>
        internal bool Readable
        {
            get
            {
                Refresh();
                return !readFailed;
            }
        }

        /// <summary>How many item types carry the mark.</summary>
        internal int Count
        {
            get
            {
                Refresh();
                return names.Count;
            }
        }

        /// <summary>The marked item types, by name.</summary>
        internal IEnumerable<string> Names
        {
            get
            {
                Refresh();
                return names.ToList();
            }
        }

        internal bool Has(ItemDrop.ItemData item) => item?.m_shared != null && Has(item.m_shared.m_name);

        internal bool Has(string name)
        {
            Refresh();
            return name != null && names.Contains(name);
        }

        /// <summary>Marks the type if it isn't, unmarks it if it is, and saves; returns whether it's marked now. If the
        /// list can't be saved (its file couldn't be read, so writing would lose marks), nothing changes, and the log says
        /// why.</summary>
        internal bool Toggle(string name) => Toggle(name, out _);

        /// <summary>As Toggle(name), and <paramref name="saved"/> says whether the change was saved (false: nothing
        /// changed, so the result is the mark as it was).</summary>
        internal bool Toggle(string name, out bool saved)
        {
            Refresh(force: true);
            if (readFailed)
            {
                // Not applied to a list that wasn't read: were the file readable a moment later, the save would load it
                // without this change, write that, and call it saved.
                ZLog.LogWarning($"[QoL Mods item marks] {Path.GetFileName(FilePath)} couldn't be read, so the change wasn't saved (it would have lost the marks in it).");
                saved = false;
                return names.Contains(name);
            }
            bool marked = !names.Contains(name);
            if (marked)
            {
                names.Add(name);
            }
            else
            {
                names.Remove(name);
            }
            saved = Save();
            if (!saved)
            {
                if (marked)
                {
                    names.Remove(name);
                }
                else
                {
                    names.Add(name);
                }
                return !marked;
            }
            return marked;
        }

        /// <summary>
        /// Adds the names in another mod's list (one per line), once: a line in this list's file remembers each list taken
        /// in, by <paramref name="source"/>, so it is never read again. The other file is only read. Returns how many names
        /// were new, or -1 if it was taken in before, isn't there, or this list can't be saved.
        /// </summary>
        internal int Import(string otherFile, string source)
        {
            Refresh(force: true);
            if (imported.Contains(source) || !File.Exists(otherFile) || readFailed)
            {
                return -1;
            }
            string[] lines;
            try
            {
                lines = File.ReadAllLines(otherFile);
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods item marks] {Path.GetFileName(otherFile)} couldn't be read, so it wasn't taken in: {error.Message}");
                return -1;
            }
            int added = 0;
            foreach (string raw in lines)
            {
                string name = raw.Trim();
                if (name.Length > 0 && !name.StartsWith("#") && names.Add(name))
                {
                    added++;
                }
            }
            imported.Add(source);
            if (!Save())
            {
                imported.Remove(source);
                return -1;
            }
            return added;
        }

        /// <summary>Whether a list was taken in already (see Import).</summary>
        internal bool Imported(string source)
        {
            Refresh();
            return imported.Contains(source);
        }

        // Reads the file when first needed, and again whenever its date has changed (another plugin carrying the part
        // saved it, or he edited it), looked at no more than once a second.
        private void Refresh(bool force = false)
        {
            float now = Clock();
            if (loaded && !force && now - checkedAt < CheckEvery)
            {
                return;
            }
            checkedAt = now;
            string path = FilePath;
            DateTime date;
            try
            {
                date = File.Exists(path) ? LastWrite(path) : default;
            }
            catch (Exception error)
            {
                // Looked at again in a second. A list never read yet counts as unreadable meanwhile.
                if (!loaded)
                {
                    Failed(path, error);
                }
                return;
            }
            if (loaded && !readFailed && date == stamp)
            {
                return;
            }
            Load(path, date);
        }

        private void Load(string path, DateTime date)
        {
            if (!File.Exists(path))
            {
                names = new HashSet<string>();
                imported.Clear();
                stamp = default;
                loaded = true;
                readFailed = false;
                return;
            }
            try
            {
                var read = new HashSet<string>();
                var notes = new List<string>();
                foreach (string raw in ReadLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith(ImportedNote.Trim()))
                    {
                        notes.Add(line.Substring(ImportedNote.Trim().Length).Trim());
                    }
                    else if (line.Length > 0 && !line.StartsWith("#"))
                    {
                        read.Add(line);
                    }
                }
                names = read;
                imported.Clear();
                imported.AddRange(notes);
                stamp = date;
                loaded = true;
                readFailed = false;
            }
            catch (Exception error)
            {
                Failed(path, error);
            }
        }

        // The file couldn't be read: the list is kept as it was and counts as unreadable (Readable), never written until
        // it's read again.
        private void Failed(string path, Exception error)
        {
            if (!readFailed)
            {
                ZLog.LogWarning($"[QoL Mods item marks] {Path.GetFileName(path)} couldn't be read ({error.Message}); it is left as it is and read again in a moment.");
            }
            readFailed = true;
            loaded = true;
        }

        // Writes the whole list: a note on what it is, the lists taken in, then the names in order. Written to a new file
        // first and then swapped in, so a crash mid-write can't leave half a list.
        private bool Save()
        {
            if (readFailed)
            {
                Refresh(force: true);
                if (readFailed)
                {
                    ZLog.LogWarning($"[QoL Mods item marks] {Path.GetFileName(FilePath)} couldn't be read, so the change wasn't saved (it would have lost the marks in it).");
                    return false;
                }
            }
            string path = FilePath;
            var lines = new List<string>
            {
                "# miikeskii's QoL Pack: " + about,
                "# One item per line, by the game's own name for it (such as $item_wood). Lines starting with # are notes.",
            };
            foreach (string source in imported)
            {
                lines.Add(ImportedNote + source);
            }
            lines.AddRange(names.OrderBy(name => name, StringComparer.Ordinal));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string fresh = path + ".new";
                File.WriteAllLines(fresh, lines);
                if (File.Exists(path))
                {
                    File.Replace(fresh, path, null);
                }
                else
                {
                    File.Move(fresh, path);
                }
                stamp = File.GetLastWriteTimeUtc(path);
                checkedAt = Clock();
                return true;
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods item marks] {Path.GetFileName(path)} couldn't be saved: {error.Message}");
                return false;
            }
        }
    }
}
