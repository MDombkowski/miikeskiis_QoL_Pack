using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Outfitter.Loadouts
{
    /// <summary>What reading the kept text came to.</summary>
    internal sealed class ReadLoadouts
    {
        /// <summary>The loadouts read, in his order.</summary>
        internal readonly List<Loadout> Loadouts = new List<Loadout>();

        /// <summary>False when the text isn't this version's (a newer Outfitter's, or not Outfitter's at all): it is kept as
        /// it is, and never written over.</summary>
        internal bool Readable = true;

        /// <summary>Not readable because a newer Outfitter wrote it.</summary>
        internal bool NewerVersion;

        /// <summary>How many lines couldn't be read and were left out (only another program editing the text makes any).</summary>
        internal int Skipped;
    }

    /// <summary>
    /// His loadouts as one text, as kept with the character (Player.m_customData["modprojects.outfitter.loadouts"]) or on
    /// this PC: a version word, then one line per loadout and one per item, fields between tabs, each escaped
    /// ("\\", "\t", "\n", "\r"). Every number is written the invariant way. A food slot (card A34, decision 72) is a line of
    /// its own kind, "F", under its loadout. Version 3 ("outfitter 3", 0.5.0) adds a field to each kind of line: a loadout's
    /// favourites (FavouritesGo: empty, "Go" or "Stay"), an item's showing on a stand (Shown: empty, "Shown" or "Hidden"),
    /// and a food slot's choosing first (First, after its preference, before its foods). A text holding any of these, or a
    /// food slot, is written as version 3, which an older Outfitter keeps whole and never writes over rather than read
    /// half of it; a text without any is written as before ("outfitter 1"). Versions 1, 2 (0.4.x's, with food slots) and
    /// 3 are read. Pure, so the scratch harness reaches it.
    /// </summary>
    internal static class LoadoutText
    {
        /// <summary>The first line of a text without food slots (every Outfitter reads it).</summary>
        internal const string Header = "outfitter 1";

        /// <summary>The first line of a text holding a food slot, as Outfitter 0.4.x wrote it (read, never written now).</summary>
        internal const string FoodHeader = "outfitter 2";

        /// <summary>The first line of a text holding a food slot or any of 0.5.0's fields (Outfitter 0.5.0 on).</summary>
        internal const string Header3 = "outfitter 3";

        /// <summary>The highest column and rows-up a place may say, in the kept text and in a code alike: room for any
        /// grid, EquipmentAndQuickSlots' rows and Haldor's Pockets together.</summary>
        internal const int MaxPlace = 15;

        private const string HeaderWord = "outfitter ";

        internal static string Write(IEnumerable<Loadout> loadouts)
        {
            var all = new List<Loadout>(loadouts);
            bool three = all.Exists(NeedsThree);
            var text = new StringBuilder(three ? Header3 : Header);
            foreach (Loadout loadout in all)
            {
                text.Append('\n');
                if (three)
                {
                    Join(text, "L", loadout.Id, Number(loadout.SavedBy), loadout.Name, loadout.SavedByName, FavouritesField(loadout.FavouritesGo));
                }
                else
                {
                    Join(text, "L", loadout.Id, Number(loadout.SavedBy), loadout.Name, loadout.SavedByName);
                }
                foreach (LoadoutItem item in loadout.Items)
                {
                    text.Append('\n');
                    if (item.Food != null)
                    {
                        WriteSlot(text, item);
                        continue;
                    }
                    var fields = new List<string>
                    {
                        "I", item.Prefab, Number(item.Quality), Number(item.Variant), Number(item.Count), item.Cell.ToString(),
                        Number(item.Column), Number(item.RowsUp), item.SlotId, item.Worn.ToString(), Number(item.CrafterId),
                        item.CrafterName, item.Data == null ? "?" : item.Data,
                    };
                    if (three)
                    {
                        fields.Add(ShownField(item.Shown));
                    }
                    Join(text, fields.ToArray());
                }
            }
            return text.ToString();
        }

        // Whether a loadout needs version 3: a food slot, its favourites set, or an item's showing set.
        private static bool NeedsThree(Loadout loadout) =>
            loadout.FoodSlots > 0 || loadout.FavouritesGo.HasValue || loadout.Items.Exists(item => item.Shown.HasValue);

        private static string FavouritesField(bool? go) => go.HasValue ? (go.Value ? "Go" : "Stay") : string.Empty;

        private static string ShownField(bool? shown) => shown.HasValue ? (shown.Value ? "Shown" : "Hidden") : string.Empty;

        // A field of version 3 read: empty is null, the two words true and false; anything else unreadable.
        private static bool TryFlag(string text, string yes, string no, out bool? flag)
        {
            flag = null;
            if (text.Length == 0)
            {
                return true;
            }
            if (text == yes || text == no)
            {
                flag = text == yes;
                return true;
            }
            return false;
        }

        // A food slot's line (always version 3): "F", its place (cell, column, rows up, slot), its count, then its rule:
        // the mode, the three ranges' ends (Health, Stamina, Eitr; an open end empty), what it prefers and of what, what it
        // puts first, and the foods ticked, one a field.
        private static void WriteSlot(StringBuilder text, LoadoutItem item)
        {
            FoodRule rule = item.Food;
            var fields = new List<string>
            {
                "F", item.Cell.ToString(), Number(item.Column), Number(item.RowsUp), item.SlotId, Number(item.Count), rule.Mode.ToString(),
                End(rule.Health.From), End(rule.Health.To), End(rule.Stamina.From), End(rule.Stamina.To), End(rule.Eitr.From), End(rule.Eitr.To),
                rule.Prefer.ToString(), rule.Of.ToString(), rule.First.ToString(),
            };
            fields.AddRange(rule.Foods);
            Join(text, fields.ToArray());
        }

        /// <summary>A range's end as kept: its number, or empty when open.</summary>
        internal static string End(int? end) => end.HasValue ? Number(end.Value) : string.Empty;

        /// <summary>A range's end read: empty is open; else a whole number.</summary>
        internal static bool TryEnd(string text, out int? end)
        {
            end = null;
            if (text.Length == 0)
            {
                return true;
            }
            if (!TryInt(text, out int value))
            {
                return false;
            }
            end = value;
            return true;
        }

        /// <summary>One of an enum's names, exactly (never a number, never two names joined).</summary>
        internal static bool TryName<T>(string text, out T value) where T : struct
        {
            foreach (T each in (T[])Enum.GetValues(typeof(T)))
            {
                if (each.ToString() == text)
                {
                    value = each;
                    return true;
                }
            }
            value = default;
            return false;
        }

        internal static ReadLoadouts Read(string text)
        {
            var read = new ReadLoadouts();
            if (string.IsNullOrEmpty(text))
            {
                return read;
            }
            string[] lines = text.Split('\n');
            string first = lines[0].TrimEnd('\r');
            bool three = first == Header3;
            bool food = first == FoodHeader || three;   // versions 2 and 3 hold food slots
            if (first != Header && !food)
            {
                // A newer version's text (another version word), or anything else: kept whole, never written over.
                read.Readable = false;
                read.NewerVersion = first.StartsWith(HeaderWord, StringComparison.Ordinal);
                return read;
            }
            Loadout current = null;
            var ids = new HashSet<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                List<string> fields = Split(line);
                bool? favourites = null;
                if (fields[0] == "L" && fields.Count >= 5 && Loadout.IsId(fields[1]) && !ids.Contains(fields[1]) &&
                    TryLong(fields[2], out long savedBy) && (!three || fields.Count < 6 || TryFlag(fields[5], "Go", "Stay", out favourites)))
                {
                    current = new Loadout
                    {
                        Id = fields[1],
                        SavedBy = savedBy,
                        Name = Text.Plain(fields[3], Loadout.MaxName),
                        SavedByName = Text.Plain(fields[4], Loadout.MaxName),
                        FavouritesGo = favourites,
                    };
                    ids.Add(current.Id);
                    read.Loadouts.Add(current);
                }
                else if (fields[0] == "I" && current != null && fields.Count >= 12 && current.Items.Count < Loadout.MaxItems &&
                         TryItem(fields, three, out LoadoutItem item))
                {
                    current.Items.Add(item);
                }
                else if (fields[0] == "F" && food && current != null && fields.Count >= (three ? 16 : 15) && RoomForSlot(current) &&
                         TrySlot(fields, three, out LoadoutItem slot))
                {
                    current.Items.Add(slot);
                }
                else
                {
                    read.Skipped++;
                    if (fields[0] == "L")
                    {
                        current = null;   // its items go with it
                    }
                }
            }
            return read;
        }

        private static bool TryItem(List<string> f, bool three, out LoadoutItem item)
        {
            item = null;
            bool? shown = null;
            if (three && f.Count > 13 && !TryFlag(f[13], "Shown", "Hidden", out shown))
            {
                return false;
            }
            if (Text.PrefabName(f[1]) == null || !TryInt(f[2], out int quality) || !TryInt(f[3], out int variant) ||
                !TryInt(f[4], out int count) || !Enum.TryParse(f[5], out CellKind cell) || !Enum.IsDefined(typeof(CellKind), cell) ||
                !TryInt(f[6], out int column) || !TryInt(f[7], out int rowsUp) || !Enum.TryParse(f[9], out WornAs worn) ||
                !Enum.IsDefined(typeof(WornAs), worn) || !TryLong(f[10], out long crafterId))
            {
                return false;
            }
            if (quality < 1 || variant < 0 || count < 1 || column < 0 || column > MaxPlace || rowsUp < 0 || rowsUp > MaxPlace ||
                (cell == CellKind.Slot && !IsSlotId(f[8])))
            {
                return false;
            }
            item = new LoadoutItem
            {
                Prefab = f[1],
                Quality = quality,
                Variant = variant,
                Count = count,
                Cell = cell,
                Column = column,
                RowsUp = rowsUp,
                SlotId = cell == CellKind.Slot ? f[8] : string.Empty,
                Worn = worn,
                CrafterId = crafterId,
                CrafterName = f[11],
                Data = f.Count > 12 && f[12] != "?" ? DataField(f[12]) : null,   // a line without it: kept before it was saved
                Shown = shown,
            };
            return true;
        }

        // Whether a loadout read so far has room for one more food slot: fewer items than a loadout holds, and fewer food
        // slots (the rest are left out, as a 129th item is).
        private static bool RoomForSlot(Loadout loadout) => loadout.Items.Count < Loadout.MaxItems && loadout.FoodSlots < Loadout.MaxFoodSlots;

        // A food slot's line (WriteSlot; version 2's has no First, read as Stat), or false for anything out of its bounds: its
        // place as an item's, its count 1 to FoodRule.MaxCount, its rule as FoodRule.WhyNotValid allows.
        private static bool TrySlot(List<string> f, bool three, out LoadoutItem slot)
        {
            slot = null;
            FoodFirst firstBy = FoodFirst.Stat;
            if (three && !TryName(f[15], out firstBy))
            {
                return false;
            }
            if (!TryName(f[1], out CellKind cell) || !TryInt(f[2], out int column) || !TryInt(f[3], out int rowsUp) || !TryInt(f[5], out int count) ||
                !TryName(f[6], out FoodMode mode) || !TryEnd(f[7], out int? healthFrom) || !TryEnd(f[8], out int? healthTo) ||
                !TryEnd(f[9], out int? staminaFrom) || !TryEnd(f[10], out int? staminaTo) || !TryEnd(f[11], out int? eitrFrom) ||
                !TryEnd(f[12], out int? eitrTo) || !TryName(f[13], out FoodPrefer prefer) || !TryName(f[14], out FoodStat of))
            {
                return false;
            }
            if (count < 1 || count > FoodRule.MaxCount || column < 0 || column > MaxPlace || rowsUp < 0 || rowsUp > MaxPlace ||
                (cell == CellKind.Slot && !IsSlotId(f[4])))
            {
                return false;
            }
            var rule = new FoodRule
            {
                Mode = mode,
                Health = new FoodRange(healthFrom, healthTo),
                Stamina = new FoodRange(staminaFrom, staminaTo),
                Eitr = new FoodRange(eitrFrom, eitrTo),
                Prefer = prefer,
                Of = of,
                First = firstBy,
            };
            for (int i = three ? 16 : 15; i < f.Count; i++)
            {
                rule.Foods.Add(f[i]);
            }
            if (rule.WhyNotValid() != null)
            {
                return false;
            }
            slot = new LoadoutItem
            {
                Cell = cell,
                Column = column,
                RowsUp = rowsUp,
                SlotId = cell == CellKind.Slot ? f[4] : string.Empty,
                Count = count,
                Food = rule,
            };
            return true;
        }

        // Other mods' data's fingerprint as kept: empty, or 16 hex digits; anything else reads as unknown.
        private static string DataField(string text)
        {
            if (text.Length == 0)
            {
                return string.Empty;
            }
            if (text.Length != 16)
            {
                return null;
            }
            foreach (char c in text)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return null;
                }
            }
            return text;
        }

        /// <summary>An EquipmentAndQuickSlots slot name as Outfitter keeps it: English letters and digits, 1 to 24.</summary>
        internal static bool IsSlotId(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 24)
            {
                return false;
            }
            foreach (char c in text)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }

        internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        internal static bool TryLong(string text, out long value) =>
            long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        /// <summary>Appends the fields, escaped, with a tab between each two.</summary>
        internal static void Join(StringBuilder text, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    text.Append('\t');
                }
                foreach (char c in fields[i] ?? string.Empty)
                {
                    switch (c)
                    {
                        case '\\':
                            text.Append("\\\\");
                            break;
                        case '\t':
                            text.Append("\\t");
                            break;
                        case '\n':
                            text.Append("\\n");
                            break;
                        case '\r':
                            text.Append("\\r");
                            break;
                        default:
                            text.Append(c);
                            break;
                    }
                }
            }
        }

        /// <summary>The fields of one line, unescaped (an unknown escape keeps its letter).</summary>
        internal static List<string> Split(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\t')
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else if (c == '\\' && i + 1 < line.Length)
                {
                    char next = line[++i];
                    field.Append(next == 't' ? '\t' : next == 'n' ? '\n' : next == 'r' ? '\r' : next);
                }
                else
                {
                    field.Append(c);
                }
            }
            fields.Add(field.ToString());
            return fields;
        }
    }
}
