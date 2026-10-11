using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Outfitter.Loadouts
{
    /// <summary>
    /// Where his loadouts are kept (card A34, decision 6; the setting "Where loadouts are kept"). With the character: in
    /// Player.m_customData["modprojects.outfitter.loadouts"], which the game saves in the character's own file (at its
    /// saves, at logout, and before a respawn), so they go with him to every world and server. On this PC: in
    /// BepInEx\config\modprojects.outfitter.loadouts.txt, the same for every character, read again whenever the file
    /// changes. Each is its own list: switching the setting shows the other one, and switching back brings the first
    /// back. Text another version wrote is kept whole and never written over. Only this computer's own player is read.
    /// </summary>
    internal static class Store
    {
        internal const string Key = "modprojects.outfitter.loadouts";

        internal const string FileName = "modprojects.outfitter.loadouts.txt";

        /// <summary>The most loadouts kept, so the character's file never grows without end (the game moves a character
        /// out of Steam's cloud near 1 MiB: R1, finding 34).</summary>
        internal const int MaxLoadouts = 100;

        /// <summary>He has as many loadouts as are kept: a new one can't be added until one goes.</summary>
        internal static bool Full => All.Count >= MaxLoadouts;

        private static readonly List<Loadout> Loadouts = new List<Loadout>();
        private static readonly System.Random Random = new System.Random();

        // What the list was read from: the player object (or the file and its time), and how it was kept.
        private static Player readFor;
        private static KeptWhere readFrom;
        private static DateTime readFileTime;
        private static bool read;
        private static bool writable = true;
        private static string whyNot;
        private static float nextLook;

        /// <summary>Goes up at every change of the list (read again, saved, deleted, moved), for a window or a stand to
        /// notice it.</summary>
        internal static int Version { get; private set; }

        /// <summary>His loadouts, in his order. Read it again after <see cref="Version"/> changes; never change it.</summary>
        internal static IReadOnlyList<Loadout> All
        {
            get
            {
                Refresh();
                return Loadouts;
            }
        }

        /// <summary>False when the kept text can't be written over (another version wrote it), or there's nowhere to
        /// keep loadouts yet (no character): <see cref="WhyNotWritable"/> says why.</summary>
        internal static bool Writable
        {
            get
            {
                Refresh();
                return writable && Where() != null;
            }
        }

        internal static string WhyNotWritable => whyNot ?? "There's no character to keep loadouts with yet.";

        /// <summary>Why the last <see cref="Save"/> kept nothing, in words: the kept text can't be written
        /// (<see cref="WhyNotWritable"/>), or it was a new loadout with <see cref="MaxLoadouts"/> kept already.</summary>
        internal static string WhyNotSaved => lastSaveFull ? FullWords : WhyNotWritable;

        /// <summary>The words for a full list.</summary>
        internal static string FullWords => $"You have {MaxLoadouts} loadouts, as many as are kept: delete one first.";

        private static bool lastSaveFull;

        /// <summary>The loadout with this ID, or null.</summary>
        internal static Loadout Find(string id)
        {
            foreach (Loadout loadout in All)
            {
                if (loadout.Id == id)
                {
                    return loadout;
                }
            }
            return null;
        }

        /// <summary>A new loadout ID, not used by any of his loadouts.</summary>
        internal static string NewId()
        {
            while (true)
            {
                string id = Loadout.NewId(Random);
                if (Find(id) == null)
                {
                    return id;
                }
            }
        }

        /// <summary>Keeps a loadout: replaces the one with its ID, or adds it at the end. False, keeping nothing, when the
        /// kept text can't be written (<see cref="Writable"/>), or when it's a new one and <see cref="MaxLoadouts"/> are kept
        /// already (<see cref="Full"/>): <see cref="WhyNotSaved"/> says which.</summary>
        internal static bool Save(Loadout loadout)
        {
            lastSaveFull = false;
            if (loadout == null || !Loadout.IsId(loadout.Id) || !Writable)
            {
                return false;
            }
            Loadout copy = loadout.Clone();
            copy.Name = Text.Plain(copy.Name, Loadout.MaxName);
            if (copy.Name.Length == 0)
            {
                copy.Name = "Loadout";
            }
            if (copy.Items.Count > Loadout.MaxItems)
            {
                copy.Items.RemoveRange(Loadout.MaxItems, copy.Items.Count - Loadout.MaxItems);
            }
            int at = Loadouts.FindIndex(each => each.Id == copy.Id);
            if (at >= 0)
            {
                Loadouts[at] = copy;
            }
            else if (Loadouts.Count >= MaxLoadouts)
            {
                lastSaveFull = true;
                return false;   // Full: the window says so before it asks
            }
            else
            {
                Loadouts.Add(copy);
            }
            return Write();
        }

        /// <summary>Forgets a loadout (never any item). False when there's none with that ID or nothing can be written.</summary>
        internal static bool Delete(string id)
        {
            if (!Writable)
            {
                return false;
            }
            int at = Loadouts.FindIndex(each => each.Id == id);
            if (at < 0)
            {
                return false;
            }
            Loadouts.RemoveAt(at);
            return Write();
        }

        /// <summary>Moves a loadout up or down his list.</summary>
        internal static bool Move(string id, int to)
        {
            if (!Writable)
            {
                return false;
            }
            int at = Loadouts.FindIndex(each => each.Id == id);
            if (at < 0)
            {
                return false;
            }
            to = Mathf.Clamp(to, 0, Loadouts.Count - 1);
            Loadout moving = Loadouts[at];
            Loadouts.RemoveAt(at);
            Loadouts.Insert(to, moving);
            return Write();
        }

        /// <summary>Every frame: notices, about once a second, another character or a change to the file on this PC.</summary>
        internal static void Tick()
        {
            if (Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 1f;
            if (read && (Settings.Kept.Value != readFrom || (readFrom == KeptWhere.WithTheCharacter && Player.m_localPlayer != readFor) ||
                         (readFrom == KeptWhere.OnThisPC && FileTime() != readFileTime)))
            {
                read = false;   // read again at the next use
                Version++;
            }
        }

        // Reads the list if it hasn't been read for where it's kept now.
        private static void Refresh()
        {
            KeptWhere where = Settings.Kept.Value;
            if (read && where == readFrom && (where != KeptWhere.WithTheCharacter || Player.m_localPlayer == readFor))
            {
                return;
            }
            read = true;
            readFrom = where;
            readFor = Player.m_localPlayer;
            Loadouts.Clear();
            writable = true;
            whyNot = null;
            string text = null;
            if (where == KeptWhere.WithTheCharacter)
            {
                if (readFor != null)
                {
                    readFor.m_customData.TryGetValue(Key, out text);
                }
            }
            else
            {
                readFileTime = FileTime();
                try
                {
                    if (File.Exists(FilePath()))
                    {
                        text = File.ReadAllText(FilePath(), Encoding.UTF8);
                    }
                }
                catch (Exception error)
                {
                    writable = false;
                    whyNot = $"The loadouts file couldn't be read ({error.Message}), so it isn't written over.";
                    Module.Warn(whyNot);
                }
            }
            ReadLoadouts parsed = LoadoutText.Read(text);
            Loadouts.AddRange(parsed.Loadouts);
            if (!parsed.Readable)
            {
                writable = false;
                whyNot = parsed.NewerVersion
                    ? "These loadouts were saved by a newer Outfitter: this one shows none of them and never writes over them."
                    : "The loadouts kept here aren't in a form this Outfitter knows, so it never writes over them.";
                Module.Warn(whyNot);
            }
            else if (parsed.Skipped > 0)
            {
                Module.Warn($"{parsed.Skipped} line(s) of the kept loadouts couldn't be read and were left out.");
            }
            Version++;
        }

        // The list written where it's kept now.
        private static bool Write()
        {
            string text = LoadoutText.Write(Loadouts);
            Version++;
            if (readFrom == KeptWhere.WithTheCharacter)
            {
                if (readFor == null)
                {
                    return false;
                }
                if (Loadouts.Count == 0)
                {
                    readFor.m_customData.Remove(Key);   // nothing left behind once he has none
                }
                else
                {
                    readFor.m_customData[Key] = text;
                }
                return true;
            }
            try
            {
                string path = FilePath();
                string temporary = path + ".new";
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
                readFileTime = FileTime();
                return true;
            }
            catch (Exception error)
            {
                Module.Warn($"The loadouts file couldn't be written ({error.Message}).");
                return false;
            }
        }

        private static KeptWhere? Where() =>
            Settings.Kept.Value == KeptWhere.OnThisPC ? KeptWhere.OnThisPC : Player.m_localPlayer != null ? KeptWhere.WithTheCharacter : (KeptWhere?)null;

        private static string FilePath() => Path.Combine(BepInEx.Paths.ConfigPath, FileName);

        private static DateTime FileTime()
        {
            try
            {
                string path = FilePath();
                return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }
    }
}
