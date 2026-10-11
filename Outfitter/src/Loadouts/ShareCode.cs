using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Outfitter.Loadouts
{
    /// <summary>
    /// A loadout as a share code (card A34; research section 6): "OF1:" and then, deflated and written in base64's
    /// URL-safe letters, the loadout's name and its items (prefab name, quality, style, count, place, worn), never who
    /// made an item, how worn it is or other mods' data, which belong to his copy of the item. About 300 characters for a
    /// typical loadout: too long for the game's chat box, so it goes by the clipboard. Reading one makes a loadout that
    /// lists what it holds, never an item, and refuses anything malformed or oversized, changing nothing. Pure, so the
    /// scratch harness reaches it; the game's own checks (is the prefab in this game, its qualities and styles) come
    /// after, in Codes. A food slot (card A34, decision 72) is a line of its own, starting "*" (never a prefab name): its
    /// place, its count and its rule, its foods by prefab name (a list: a food this game lacks stays a name). A code holding
    /// one starts "OF2:", which an older Outfitter refuses whole rather than read half of; a code without any is written
    /// as before, "OF1:". Both are read; a slot's line only in an "OF2:" code.
    /// </summary>
    internal static class ShareCode
    {
        internal const string Prefix = "OF1:";

        /// <summary>The start of a code holding a food slot (Outfitter 0.4.0 on).</summary>
        internal const string FoodPrefix = "OF2:";

        /// <summary>The first field of a food slot's line.</summary>
        internal const string SlotMark = "*";

        /// <summary>The longest code read, in characters.</summary>
        internal const int MaxCode = 4000;

        /// <summary>The most a code may hold once inflated, in bytes: far beyond a full loadout (about 8 KB).</summary>
        internal const int MaxInflated = 65536;

        /// <summary>The most items a code may hold: as many as a loadout (R1, finding 26: a full capture with
        /// EquipmentAndQuickSlots' extra rows could pass 96).</summary>
        internal const int MaxItems = Loadout.MaxItems;

        internal const int MaxCount = 9999;

        internal const int MaxQuality = 10;

        internal const int MaxVariant = 63;

        /// <summary>The code for a loadout; <paramref name="places"/> false leaves each item's cell out (its worn place
        /// stays).</summary>
        internal static string Write(Loadout loadout, bool places)
        {
            var text = new StringBuilder();
            LoadoutText.Join(text, loadout.Name);
            int written = 0;
            int slots = 0;
            foreach (LoadoutItem item in loadout.Items)
            {
                if (written++ >= MaxItems)
                {
                    break;
                }
                CellKind cell = places ? item.Cell : CellKind.None;
                if (item.Food != null)
                {
                    if (slots++ >= Loadout.MaxFoodSlots)
                    {
                        continue;   // never more than a code may hold (a loadout never has more)
                    }
                    text.Append('\n');
                    WriteSlot(text, item, cell, places);
                    continue;
                }
                text.Append('\n');
                LoadoutText.Join(text, item.Prefab, LoadoutText.Number(item.Quality), LoadoutText.Number(item.Variant),
                                 LoadoutText.Number(item.Count), CellLetter(cell),
                                 LoadoutText.Number(places ? item.Column : 0), LoadoutText.Number(places ? item.RowsUp : 0),
                                 cell == CellKind.Slot ? item.SlotId : string.Empty, LoadoutText.Number((int)item.Worn));
            }
            byte[] plain = new UTF8Encoding(false).GetBytes(text.ToString());
            using (var packed = new MemoryStream())
            {
                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(plain, 0, plain.Length);
                }
                return (slots > 0 ? FoodPrefix : Prefix) + ToBase64Url(packed.ToArray());
            }
        }

        // A food slot's line: "*", its place (cell letter, column, rows up, slot), its count, then its rule: the mode (S by
        // stats, L from a list), the three ranges' ends (Health, Stamina, Eitr; an open end empty), what it prefers (M most,
        // L least; in lower case, m or l, for a slot putting a whole count first, 0.5.0's Amount, which 0.4 refuses whole) and
        // of what (H, S, E, A for all three), and the foods ticked, one a field.
        private static void WriteSlot(StringBuilder text, LoadoutItem item, CellKind cell, bool places)
        {
            FoodRule rule = item.Food;
            int column = places ? item.Column : 0;
            int rowsUp = places ? item.RowsUp : 0;
            var fields = new List<string>
            {
                SlotMark, CellLetter(cell), LoadoutText.Number(column), LoadoutText.Number(rowsUp),
                cell == CellKind.Slot ? item.SlotId : string.Empty, LoadoutText.Number(item.Count),
                rule.Mode == FoodMode.FromList ? "L" : "S",
                LoadoutText.End(rule.Health.From), LoadoutText.End(rule.Health.To), LoadoutText.End(rule.Stamina.From),
                LoadoutText.End(rule.Stamina.To), LoadoutText.End(rule.Eitr.From), LoadoutText.End(rule.Eitr.To),
                PreferLetter(rule), StatLetter(rule.Of),
            };
            fields.AddRange(rule.Foods);
            LoadoutText.Join(text, fields.ToArray());
        }

        // What a slot prefers, as a letter: M or L, in lower case when it puts a whole count first (0.5.0).
        private static string PreferLetter(FoodRule rule)
        {
            string letter = rule.Prefer == FoodPrefer.Least ? "L" : "M";
            return rule.First == FoodFirst.Amount ? letter.ToLowerInvariant() : letter;
        }

        private static string StatLetter(FoodStat stat)
        {
            switch (stat)
            {
                case FoodStat.Health:
                    return "H";
                case FoodStat.Stamina:
                    return "S";
                case FoodStat.Eitr:
                    return "E";
                default:
                    return "A";
            }
        }

        private static bool TryStat(string letter, out FoodStat stat)
        {
            switch (letter)
            {
                case "H":
                    stat = FoodStat.Health;
                    return true;
                case "S":
                    stat = FoodStat.Stamina;
                    return true;
                case "E":
                    stat = FoodStat.Eitr;
                    return true;
                case "A":
                    stat = FoodStat.All;
                    return true;
                default:
                    stat = FoodStat.All;
                    return false;
            }
        }

        /// <summary>Reads a code into a new loadout (a new ID, saved by no one), or says why it can't: null and
        /// <paramref name="why"/>.</summary>
        internal static Loadout Read(string code, Random random, out string why)
        {
            why = null;
            if (code == null)
            {
                why = "there's no code on the clipboard";
                return null;
            }
            code = code.Trim();
            bool food = code.StartsWith(FoodPrefix, StringComparison.Ordinal);   // only "OF2:" holds food slots
            if (!code.StartsWith(Prefix, StringComparison.Ordinal) && !food)
            {
                why = "that isn't an Outfitter code (it starts with \"" + Prefix + "\")";
                return null;
            }
            if (code.Length > MaxCode)
            {
                why = "the code is too long";
                return null;
            }
            byte[] packed = FromBase64Url(code.Substring(Prefix.Length));
            if (packed == null || packed.Length == 0)
            {
                why = "the code is damaged (some of it is missing or changed)";
                return null;
            }
            string text = Inflate(packed);
            if (text == null)
            {
                why = "the code is damaged (some of it is missing or changed)";
                return null;
            }
            string[] lines = text.Split('\n');
            if (lines.Length - 1 > MaxItems)
            {
                why = $"the code holds more than {MaxItems} items";
                return null;
            }
            List<string> head = LoadoutText.Split(lines[0]);
            string name = Text.Plain(head[0], Loadout.MaxName);
            var loadout = new Loadout { Id = Loadout.NewId(random), Name = name.Length > 0 ? name : "Shared loadout" };
            int slots = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                List<string> fields = LoadoutText.Split(lines[i]);
                bool slot = food && fields[0] == SlotMark;
                if (slot && ++slots > Loadout.MaxFoodSlots)
                {
                    why = $"the code holds more than {Loadout.MaxFoodSlots} food slots";
                    return null;
                }
                LoadoutItem item = slot ? ReadSlot(fields) : ReadItem(fields);
                if (item == null)
                {
                    why = $"item {i} of the code can't be read";
                    return null;
                }
                loadout.Items.Add(item);
            }
            if (loadout.Items.Count == 0)
            {
                why = "the code holds no items";
                return null;
            }
            return loadout;
        }

        private static LoadoutItem ReadItem(List<string> f)
        {
            if (f.Count != 9)
            {
                return null;
            }
            string prefab = Text.PrefabName(f[0]);
            if (prefab == null || !LoadoutText.TryInt(f[1], out int quality) || !LoadoutText.TryInt(f[2], out int variant) ||
                !LoadoutText.TryInt(f[3], out int count) || !TryCell(f[4], out CellKind cell) ||
                !LoadoutText.TryInt(f[5], out int column) || !LoadoutText.TryInt(f[6], out int rowsUp) ||
                !LoadoutText.TryInt(f[8], out int worn))
            {
                return null;
            }
            if (quality < 1 || quality > MaxQuality || variant < 0 || variant > MaxVariant || count < 1 || count > MaxCount ||
                column < 0 || column > LoadoutText.MaxPlace || rowsUp < 0 || rowsUp > LoadoutText.MaxPlace || worn < 0 ||
                !Enum.IsDefined(typeof(WornAs), worn))
            {
                return null;
            }
            if (cell == CellKind.Slot ? !LoadoutText.IsSlotId(f[7]) : f[7].Length != 0)
            {
                return null;
            }
            return new LoadoutItem
            {
                Prefab = prefab,
                Quality = quality,
                Variant = variant,
                Count = count,
                Cell = cell,
                Column = cell == CellKind.Hotbar || cell == CellKind.FromBottom ? column : 0,
                RowsUp = cell == CellKind.FromBottom ? rowsUp : 0,
                SlotId = cell == CellKind.Slot ? f[7] : string.Empty,
                Worn = (WornAs)worn,
            };
        }

        // A food slot's line (WriteSlot), or null for anything out of bounds: its place as an item's, its count 1 to
        // FoodRule.MaxCount, its rule as FoodRule.WhyNotValid allows.
        private static LoadoutItem ReadSlot(List<string> f)
        {
            if (f.Count < 15 || !TryCell(f[1], out CellKind cell) || !LoadoutText.TryInt(f[2], out int column) ||
                !LoadoutText.TryInt(f[3], out int rowsUp) || !LoadoutText.TryInt(f[5], out int count) || (f[6] != "S" && f[6] != "L") ||
                !LoadoutText.TryEnd(f[7], out int? healthFrom) || !LoadoutText.TryEnd(f[8], out int? healthTo) ||
                !LoadoutText.TryEnd(f[9], out int? staminaFrom) || !LoadoutText.TryEnd(f[10], out int? staminaTo) ||
                !LoadoutText.TryEnd(f[11], out int? eitrFrom) || !LoadoutText.TryEnd(f[12], out int? eitrTo) || (f[13] != "M" && f[13] != "L" && f[13] != "m" && f[13] != "l") ||
                !TryStat(f[14], out FoodStat of))
            {
                return null;
            }
            if (count < 1 || count > FoodRule.MaxCount || column < 0 || column > LoadoutText.MaxPlace || rowsUp < 0 || rowsUp > LoadoutText.MaxPlace)
            {
                return null;
            }
            if (cell == CellKind.Slot ? !LoadoutText.IsSlotId(f[4]) : f[4].Length != 0)
            {
                return null;
            }
            var rule = new FoodRule
            {
                Mode = f[6] == "L" ? FoodMode.FromList : FoodMode.ByStats,
                Health = new FoodRange(healthFrom, healthTo),
                Stamina = new FoodRange(staminaFrom, staminaTo),
                Eitr = new FoodRange(eitrFrom, eitrTo),
                Prefer = f[13] == "L" || f[13] == "l" ? FoodPrefer.Least : FoodPrefer.Most,
                Of = of,
                First = f[13] == "m" || f[13] == "l" ? FoodFirst.Amount : FoodFirst.Stat,
            };
            for (int i = 15; i < f.Count; i++)
            {
                rule.Foods.Add(f[i]);
            }
            if (rule.WhyNotValid() != null)
            {
                return null;
            }
            var slot = new LoadoutItem { Count = count, Cell = cell, SlotId = cell == CellKind.Slot ? f[4] : string.Empty, Food = rule };
            if (cell == CellKind.Hotbar || cell == CellKind.FromBottom)
            {
                slot.Column = column;   // a column only where the place has one, as an item's (ReadItem)
            }
            if (cell == CellKind.FromBottom)
            {
                slot.RowsUp = rowsUp;
            }
            return slot;
        }

        private static string CellLetter(CellKind cell)
        {
            switch (cell)
            {
                case CellKind.Hotbar:
                    return "H";
                case CellKind.FromBottom:
                    return "B";
                case CellKind.Slot:
                    return "S";
                default:
                    return "N";
            }
        }

        private static bool TryCell(string letter, out CellKind cell)
        {
            switch (letter)
            {
                case "H":
                    cell = CellKind.Hotbar;
                    return true;
                case "B":
                    cell = CellKind.FromBottom;
                    return true;
                case "S":
                    cell = CellKind.Slot;
                    return true;
                case "N":
                    cell = CellKind.None;
                    return true;
                default:
                    cell = CellKind.None;
                    return false;
            }
        }

        // Inflates at most MaxInflated bytes of strict UTF-8; null for anything else (a damaged code, or one built to
        // inflate to a great size).
        private static string Inflate(byte[] packed)
        {
            try
            {
                using (var input = new MemoryStream(packed))
                using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;
                    while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > MaxInflated)
                        {
                            return null;
                        }
                        output.Write(buffer, 0, read);
                    }
                    return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(output.ToArray());
                }
            }
            catch (Exception)
            {
                return null;   // InvalidDataException, a bad UTF-8 byte, or anything else a damaged code makes
            }
        }

        internal static string ToBase64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>The bytes of base64 written with URL-safe letters and no padding; null for anything else.</summary>
        internal static byte[] FromBase64Url(string text)
        {
            if (text.Length == 0 || text.Length % 4 == 1)
            {
                return null;
            }
            foreach (char c in text)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                {
                    return null;
                }
            }
            string standard = text.Replace('-', '+').Replace('_', '/');
            standard += new string('=', (4 - standard.Length % 4) % 4);
            try
            {
                return Convert.FromBase64String(standard);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
