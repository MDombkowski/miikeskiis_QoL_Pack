using System.Collections.Generic;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// Which sections of rules each pin follows. A pin is sorted once, and the answer kept until its name, type,
    /// position or owner changes, or the record, the override list or the sections change (Invalidate).
    ///
    /// The order a pin is sorted in:
    /// 0. a portal's pin (XPortal Map Picker's: type None, the game's portal pin picture): Portals;
    /// 1. its line in Cartur's Map Pins' record, found by where the pin stands (a saved pin only, as Cartur's own);
    /// 2. a pin with one of Cartur's icons and no line:
    ///    - unsaved: Cartur's pin on a boat, cart or tame (or another mod's temporary pin);
    ///    - while the record hasn't been read yet: left as the game draws it, until it has;
    ///    - saved: the key its name always had in the record; failing that, a friend's pin (from a table, by its owner or
    ///      author) takes the key its icon always had, and a pin of the player's own was placed by hand with one of
    ///      Cartur's icons (its icon picker, on by default, or its editor): hand-placed; without a record, other;
    /// 3. the game's own types: the five pin shapes are hand-placed (saved) or other (unsaved), death markers, and the
    ///    bed, boss, Hildir, memorial and location markers are the game's markers; players, pings, shouts and events are
    ///    never touched;
    /// 4. anything else follows Other pins.
    /// The override list is tried last, against the pin's kind, its whole key and its name.
    /// </summary>
    internal static class Sorter
    {
        private static readonly Dictionary<Minimap.PinData, Sorted> sorted = new Dictionary<Minimap.PinData, Sorted>();

        // The words of a kind, a key or a pin's name as the map shows it, worked out once per text: a record read sorts
        // every drawn pin again, and there are only a few hundred different names.
        private static readonly Dictionary<string, string> signatures = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private static readonly Dictionary<string, string> shownSignatures = new Dictionary<string, string>(System.StringComparer.Ordinal);

        private static int generation;

        /// <summary>Whether a pin is a friend's (from a cartography table): only such a pin is sorted by its icon when its
        /// name says nothing. The game sets it to Players.ByAnother (owner or author); by default, the owner alone.</summary>
        internal static System.Func<Minimap.PinData, bool> ByAnotherPlayer = pin => pin.m_ownerID != 0L;

        /// <summary>Where a pin's sections came from, for the log.</summary>
        internal enum Source
        {
            Untouched,
            Pending,
            Record,
            Name,
            Icon,
            Moving,
            Portal,
            HandPlaced,
            GameType,
            Other,
        }

        /// <summary>Every pin is sorted again at its next drawing.</summary>
        internal static void Invalidate() => generation++;

        /// <summary>Forgets every pin: a new world's map has other pins.</summary>
        internal static void Forget()
        {
            sorted.Clear();
            shownSignatures.Clear();   // a name's translation may differ (another language) by the next world
            generation++;
        }

        /// <summary>The chain of sections a pin follows, or null for a pin this mod leaves as the game draws it.</summary>
        /// <param name="recordPending">Cartur's Map Pins is installed and its record for this world is still being read:
        /// a pin with one of its icons and no line is left alone until the record is there (then Invalidate).</param>
        internal static Chain ChainFor(Minimap.PinData pin, RecordIndex record, bool recordPending = false) =>
            Get(pin, record, recordPending).Chain;

        /// <summary>Where a pin's sections came from.</summary>
        internal static Source SourceOf(Minimap.PinData pin, RecordIndex record, bool recordPending = false) =>
            Get(pin, record, recordPending).From;

        /// <summary>
        /// Whether Cartur's Map Pins made a pin, for the friends' map (FriendsMap): it is drawn with one of Cartur's own
        /// icons (a friend's Cartur's pins are, also those of kinds this player's record has never seen), it is sorted into
        /// one of Cartur's categories (by the record or its name), or it is one Cartur's keeps on a boat, cart or tame.
        /// True also while the record is still being read, and for live information, which is never hidden.
        /// </summary>
        internal static bool MadeByCartur(Minimap.PinData pin, RecordIndex record, bool recordPending = false)
        {
            if ((int)pin.m_type >= RecordIndex.FirstCustomType)
            {
                return true;
            }
            Sorted known = Get(pin, record, recordPending);
            return known.Cartur || known.From == Source.Pending || known.From == Source.Untouched;
        }

        /// <summary>Drops what it knows of pins no longer on the map, once there are many more of them than pins.</summary>
        internal static void Prune(List<Minimap.PinData> pins)
        {
            if (sorted.Count <= pins.Count * 2 + 256)
            {
                return;
            }
            var keep = new HashSet<Minimap.PinData>(pins);
            var gone = new List<Minimap.PinData>();
            foreach (Minimap.PinData pin in sorted.Keys)
            {
                if (!keep.Contains(pin))
                {
                    gone.Add(pin);
                }
            }
            foreach (Minimap.PinData pin in gone)
            {
                sorted.Remove(pin);
            }
        }

        /// <summary>The section of a pin sorted by its type alone.</summary>
        internal static RuleSection SectionOfType(Minimap.PinType type, bool save)
        {
            switch (type)
            {
                case Minimap.PinType.Icon0:
                case Minimap.PinType.Icon1:
                case Minimap.PinType.Icon2:
                case Minimap.PinType.Icon3:
                case Minimap.PinType.Icon4:
                    // The game saves every pin placed by hand; an unsaved one is another mod's (Cartur's pins on boats
                    // and tames, when its icons are switched off).
                    return save ? Sections.HandPlaced : Sections.Others;
                case Minimap.PinType.Death:
                    return Sections.Deaths;
                case Minimap.PinType.Bed:
                case Minimap.PinType.Boss:
                case Minimap.PinType.None:       // the game's location markers (UpdateLocationPins)
                case Minimap.PinType.Hildir1:
                case Minimap.PinType.Hildir2:
                case Minimap.PinType.Hildir3:
                case Minimap.PinType.Memorial:
                    return Sections.GameMarkers;
                default:
                    return Sections.Others;
            }
        }

        private static Sorted Get(Minimap.PinData pin, RecordIndex record, bool recordPending)
        {
            if (sorted.TryGetValue(pin, out Sorted known) && known.Fits(pin, generation))
            {
                return known;
            }
            known = known ?? new Sorted();
            known.Remember(pin, generation);
            Sort(pin, record, recordPending, known);
            sorted[pin] = known;
            return known;
        }

        private static void Sort(Minimap.PinData pin, RecordIndex record, bool recordPending, Sorted into)
        {
            into.Chain = null;
            into.From = Source.Untouched;
            into.Cartur = false;
            Minimap.PinType type = pin.m_type;
            switch (type)
            {
                case Minimap.PinType.Shout:
                case Minimap.PinType.Player:
                case Minimap.PinType.RandomEvent:
                case Minimap.PinType.Ping:
                case Minimap.PinType.EventArea:
                    return;   // live information: always drawn as the game draws it
            }

            if (PortalPictures.Is(pin))
            {
                RuleSection portal = Sections.OverrideCount == 0
                    ? null
                    : Sections.OverrideFor(string.Empty, string.Empty, SignatureOf(pin.m_name, shown: true));
                into.Chain = Chains.Get(portal, null, Sections.Portals);
                into.From = Source.Portal;
                return;
            }

            string key = null;
            RuleSection own = null;
            Source from = Source.Other;
            if (record != null && pin.m_save)
            {
                key = record.KeyAt(pin.m_pos);
                from = Source.Record;
            }
            if (key == null && (int)type >= RecordIndex.FirstCustomType)
            {
                if (!pin.m_save)
                {
                    own = Sections.Moving;
                    from = Source.Moving;
                }
                else if (recordPending)
                {
                    into.From = Source.Pending;
                    return;   // left as drawn until the record is read, which sorts every pin again
                }
                else
                {
                    bool theirs = ByAnotherPlayer(pin);
                    key = record?.KeyForName(pin.m_name);
                    from = Source.Name;
                    if (key == null && theirs)
                    {
                        key = record?.KeyForIcon((int)type);
                        from = Source.Icon;
                    }
                    if (key == null)
                    {
                        // One of his own, not in a record that was read: he placed it by hand with one of Cartur's icons.
                        // Without a record (none for this world, or unreadable), nothing tells his hand-placed pins from
                        // Cartur's own, so they follow Other pins.
                        bool placedByHand = !theirs && record != null;
                        own = placedByHand ? Sections.HandPlaced : Sections.Others;
                        from = placedByHand ? Source.HandPlaced : Source.Other;
                    }
                }
            }

            string kind = null;
            RuleSection group = null;
            if (key != null)
            {
                Kinds.Split(key, out string category, out kind);
                own = Sections.ForCategory(category) ?? Sections.Others;
                if (own == Sections.Pickables)
                {
                    group = Sections.ForGroup(Kinds.GroupOf(kind));
                }
            }
            else if (own == null)
            {
                own = SectionOfType(type, pin.m_save);
                from = own == Sections.HandPlaced ? Source.HandPlaced : own == Sections.Others ? Source.Other : Source.GameType;
            }

            RuleSection over = Sections.OverrideCount == 0
                ? null
                : Sections.OverrideFor(SignatureOf(kind), SignatureOf(key), SignatureOf(pin.m_name, shown: true));
            into.Chain = Chains.Get(over, group, own);
            into.From = from;
            into.Cartur = key != null || own == Sections.Moving;
        }

        private static string SignatureOf(string text, bool shown = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            Dictionary<string, string> known = shown ? shownSignatures : signatures;
            if (!known.TryGetValue(text, out string words))
            {
                if (known.Count > 4096)
                {
                    known.Clear();   // names that come and go (shouts, renamed pins): never let it grow without end
                }
                words = Words.Signature(shown ? AsShown(text) : text);
                known[text] = words;
            }
            return words;
        }

        // A pin's name as the map shows it: the game translates a name that holds $tokens when it draws it.
        private static string AsShown(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('$') < 0 || Localization.instance == null)
            {
                return name;
            }
            return Localization.instance.Localize(name);
        }

        private sealed class Sorted
        {
            private string name;
            private Sprite icon;
            private Minimap.PinType type;
            private Vector3 position;
            private bool save;
            private long owner;
            private int sortedIn;

            internal Chain Chain;
            internal Source From;
            internal bool Cartur;

            internal bool Fits(Minimap.PinData pin, int generation) =>
                sortedIn == generation && ReferenceEquals(name, pin.m_name) && ReferenceEquals(icon, pin.m_icon) &&
                type == pin.m_type && save == pin.m_save &&
                owner == pin.m_ownerID && position == pin.m_pos;

            internal void Remember(Minimap.PinData pin, int generation)
            {
                name = pin.m_name;
                icon = pin.m_icon;
                type = pin.m_type;
                position = pin.m_pos;
                save = pin.m_save;
                owner = pin.m_ownerID;
                sortedIn = generation;
            }
        }
    }
}
