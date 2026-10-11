using System;
using System.Collections.Generic;
using QoLMods.Catalogue;
using QoLMods.Chests;

namespace StoreStash
{
    /// <summary>
    /// Chests' setups (card A41): which chests can carry one, reading a chest's (its record's text, read into the
    /// catalogue's ItemPicks, each text once), how the quick stack follows it (Reading, through the chest service's
    /// SetupRules), writing one as the chest's owner (the chest service's ChestSetup.Write), and its words.
    /// </summary>
    internal static class Setups
    {
        /// <summary>How many setups' texts are kept read at once: far more than any base holds; past it, all are read
        /// again as they're met.</summary>
        private const int Kept = 512;

        private static readonly Dictionary<string, ItemPicks> Read = new Dictionary<string, ItemPicks>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Described = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>What the engine says of a chest: a gravestone, a cart's or ship's, built by a player. Only the scratch
        /// test harness sets another (the engine isn't there).</summary>
        internal static Func<Container, (bool Grave, bool Vehicle, bool Built)> Facts = chest =>
        {
            Piece piece = chest.GetComponentInParent<Piece>();
            return (chest.GetComponentInParent<TombStone>() != null, IsVehicle(chest), piece != null && piece.IsPlacedByPlayer());
        };

        /// <summary>
        /// Whether a chest can carry a setup: the game's own kind of chest (carts' and ships' too, while their setting
        /// is on), built by a player; never a gravestone, a chest the world made, or another mod's container.
        /// </summary>
        internal static bool Settable(Container chest)
        {
            if (chest == null || chest.GetType() != typeof(Container))
            {
                return false;
            }
            (bool grave, bool vehicle, bool built) = Facts(chest);
            return !grave && built && (!vehicle || SetupSettings.Vehicles.Value);
        }

        /// <summary>A cart's or ship's chest: its setup is kept in the vehicle's record.</summary>
        internal static bool IsVehicle(Container chest) =>
            chest != null && (chest.GetComponentInParent<Vagon>() != null || chest.GetComponentInParent<Ship>() != null);

        /// <summary>A setup from its text, read once and kept; null for no setup (an empty text, or picks with nothing in
        /// them).</summary>
        internal static ItemPicks Of(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            if (!Read.TryGetValue(text, out ItemPicks picks))
            {
                if (Read.Count >= Kept)
                {
                    Read.Clear();
                    Described.Clear();
                }
                picks = ItemPicks.Parse(text);
                Read[text] = picks;
            }
            return picks == null || picks.IsEmpty ? null : picks;
        }

        /// <summary>A chest's setup, or null (none, or a chest that can't carry one). Never changed in place: a copy is
        /// changed and written (Write).</summary>
        internal static ItemPicks Of(Container chest) => Settable(chest) ? Of(ChestSetup.Read(chest)) : null;

        /// <summary>
        /// The quick stack's reading of a chest's setup (SetupRules.Of): null for a chest without one, else how strongly
        /// it takes an item (ItemPicks.Fit, from the catalogue's home and biome for the item; 0: not at all).
        /// </summary>
        internal static Func<ItemDrop.ItemData, int> Reading(Container chest)
        {
            ItemPicks picks = Of(chest);
            if (picks == null)
            {
                return null;
            }
            return item => item?.m_shared == null ? 0 : picks.Fit(item.m_shared.m_name);
        }

        /// <summary>
        /// Gives a chest this computer owns a setup (null or empty: none), and sends its record at once. False, changing
        /// nothing, when the chest isn't his (or can't carry a setup), or the setup can't be written.
        /// </summary>
        internal static bool Write(Container chest, ItemPicks picks)
        {
            if (!Settable(chest) || (picks != null && !picks.Readable))
            {
                return false;
            }
            return ChestSetup.Write(chest, picks == null ? string.Empty : picks.Text());
        }

        /// <summary>An item's name in his language, by its shared name ("$item_wolfpelt" as "Wolf Pelt"). Only the scratch
        /// test harness sets another (the game's Localization loads its texts through the engine).</summary>
        internal static Func<string, string> ItemName = sharedName => GameFacts.Localize(sharedName);

        // ---- Its words (card A41's second round, decision 23): StoreStash's own, so the catalogue part's are left as
        // they were (ItemPicks.Describe).

        /// <summary>
        /// What a setup takes, in a few words, kept per text: the hover line, the setup button's tooltip, the copy and
        /// paste messages and a where-line's chest. Its parts apart by semicolons: what the biomes narrow and the biomes
        /// ("Trophies from Swamp, Plains"; biomes alone, "Everything from Swamp, Plains"), then the things picked by name,
        /// from any biome ("Wolf Fang, Needle"), then what's left out ("except Leech Trophy"). At most
        /// <paramref name="most"/> of each are named, then "and 3 more". The "Other" biome reads "or no biome" ("with no
        /// biome" alone). A new language shows after the game starts again (or once more than Kept setups have been put in
        /// words).
        /// </summary>
        internal static string Describe(ItemPicks picks, int most = 6)
        {
            if (picks == null)
            {
                return string.Empty;
            }
            string key = most + "\n" + picks.Text();
            if (!Described.TryGetValue(key, out string words))
            {
                if (Described.Count >= Kept)
                {
                    Described.Clear();   // every change in the setup window is a new text
                }
                words = Shown(Short(picks, most));
                Described[key] = words;
            }
            return words;
        }

        /// <summary>
        /// What a setup takes, as the setup window's top line says it, in a sentence: "Takes Trophies, only from the Swamp
        /// and Plains biomes, and nothing else." Biomes alone, "Takes everything from the Swamp and Plains biomes, and
        /// nothing else."; things picked by name come after, "plus Wolf Fang and Needle (from any biome)" (that last only
        /// when biomes narrow the rest), then "except Leech Trophy"; with <paramref name="keepHeld"/> (the setting "A
        /// set-up chest also takes what it holds") it ends "and more of what it already holds." instead. Things of no
        /// biome (the "Other" biome) are said so. At most <paramref name="most"/> of each are named, then "and 3 more".
        /// </summary>
        internal static string Sentence(ItemPicks picks, bool keepHeld, int most = 4)
        {
            if (picks == null || (picks.Readable && picks.IsEmpty))
            {
                return "Nothing picked yet. Until you pick something, this is an ordinary chest: quick stack puts in what it already holds.";
            }
            if (!picks.Readable)
            {
                return picks.TooLong
                    ? "Its setup is longer than a setup can be (another mod's, or another version's): quick stack puts nothing in it. Clear gives it a new one."
                    : "Set up by a newer version of the pack: update StoreStash to see or change it. Until then quick stack puts nothing in it.";
            }
            Split(picks, out List<string> groups, out List<string> narrowed, out List<string> byName, out List<string> excepts, out List<string> known,
                  out bool other);
            bool biomes = picks.InBiomes.Count > 0;
            string text = null;
            if (narrowed.Count > 0)
            {
                // Groups with biomes of their own (card A81) first, by those ("Mistlands Weapons"), then the rest: with the
                // From: row's biomes apart ("Takes Mistlands Weapons, and Food only from the Plains biome"), else in one list
                // ("Takes Mistlands Weapons and Food").
                if (groups.Count > 0 && biomes)
                {
                    text = "Takes " + AndList(narrowed, most) + ", and " + AndList(groups, most) + " " +
                           (known.Count == 0 ? "only with no biome" : "only " + FromTheBiomes(known, other));
                }
                else
                {
                    var all = new List<string>(narrowed);
                    all.AddRange(groups);
                    text = "Takes " + AndList(all, most);
                }
            }
            else if (groups.Count > 0)
            {
                text = "Takes " + AndList(groups, most);
                if (biomes)
                {
                    text += ", " + (known.Count == 0 ? "only those with no biome" : "only " + FromTheBiomes(known, other));
                }
            }
            else if (biomes)
            {
                text = "Takes " + EverythingFrom(picks.InBiomes);
            }
            if (byName.Count > 0)
            {
                string names = AndList(byName, most);
                text = text == null ? "Takes " + names : text + ", plus " + names + (biomes || narrowed.Count > 0 ? " (from any biome)" : string.Empty);
            }
            if (text == null)
            {
                // Only parts a newer version wrote, none of them a pick: it takes nothing of its own.
                return keepHeld ? "Takes nothing new, only more of what it already holds." : "Takes nothing.";
            }
            if (excepts.Count > 0)
            {
                text += ", except " + AndList(excepts, most);
            }
            text += keepHeld ? ", and more of what it already holds." : ", and nothing else.";
            if (narrowed.Count > 0 && groups.Count == 0 && biomes)
            {
                // Every group has biomes of its own: the From: row has nothing left to narrow (card A81).
                text += " Its From: row narrows nothing: every group has biomes of its own.";
            }
            return Shown(text);
        }

        /// <summary>Biomes in words (card A81): "the Swamp and Plains biomes", "the Plains biome, or no biome", "no biome";
        /// shown as words only (Shown: a record's labels may be another game's, review m4).</summary>
        internal static string BiomesInWords(IEnumerable<string> biomes)
        {
            SplitBiomes(biomes, out List<string> known, out bool other);
            if (known.Count == 0)
            {
                return "no biome";
            }
            return Shown("the " + AndList(known, int.MaxValue) + (known.Count == 1 ? " biome" : " biomes") + (other ? ", or no biome" : string.Empty));
        }

        /// <summary>A group with biomes of its own, by them (card A81): "Mistlands Weapons", "Swamp and Plains Trophies",
        /// "Weapons with no biome", "Plains Weapons (or with no biome)".</summary>
        internal static string NarrowedLabel(ItemPick pick, IEnumerable<string> ownBiomes)
        {
            SplitBiomes(ownBiomes, out List<string> known, out bool other);
            string label = GroupLabel(pick);
            if (known.Count == 0)
            {
                return label + " with no biome";
            }
            return AndList(known, int.MaxValue) + " " + label + (other ? " (or with no biome)" : string.Empty);
        }

        /// <summary>"everything from the Swamp and Plains biomes", "everything from the Plains biome, or with no biome",
        /// "everything with no biome": what biomes alone take (the top line, and the window's note when a setup widens to
        /// them).</summary>
        internal static string EverythingFrom(IEnumerable<string> biomes)
        {
            SplitBiomes(biomes, out List<string> known, out bool other);
            return Shown(known.Count == 0 ? "everything with no biome" : "everything " + FromTheBiomes(known, other));
        }

        /// <summary>
        /// Words made from a chest's record (a setup's labels, biomes and names, as another version or another game may have
        /// written them; a chest's name, SetupName.Plain) as they may be shown (the last look and its review rounds 2 to 5;
        /// LESSONS_LEARNED, text another player's game wrote): no "&lt;" at all, so neither TextMeshPro nor Unity's Text can
        /// read a tag (TextMeshPro reads one without its "&gt;": "&lt;br " breaks the line). A run that looks like a tag (an
        /// ASCII letter, "/" or "#" right after its "&lt;", as every tag of theirs has) is left out whole: "&lt;color=red&gt;Ores"
        /// shows as "Ores"; any other "&lt;" goes alone: "Mead &lt;3" shows "Mead 3", "Chest &lt;1&gt;" shows "Chest 1&gt;". No
        /// "\" (the game's labels turn "\n" into a line break) or "$" (its messages translate "$words"); a line
        /// break or a tab as a space; no space at the start and none right after another; no invisible formatting character,
        /// of any plane (one turns the text after it right to left), but the two joiners an emoji or a script needs; and no
        /// half an emoji. Everything else is kept, so this version's own words never change. Pure, and as long to run as the
        /// text is long.
        /// </summary>
        internal static string Shown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var shown = new System.Text.StringBuilder(text.Length);
            var open = new List<int>();   // where each "<" not yet closed sits in what's shown, in order of place
            for (int i = 0; i < text.Length; i++)
            {
                char each = text[i];
                if (char.IsHighSurrogate(each) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // A whole character past the first plane: an emoji kept, an invisible formatting one (a tag letter) not.
                    if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text, i) != System.Globalization.UnicodeCategory.Format)
                    {
                        shown.Append(each).Append(text[i + 1]);
                    }
                    i++;
                    continue;
                }
                if (each == '<')
                {
                    open.Add(shown.Length);
                    shown.Append(each);
                    continue;
                }
                if (each == '>' && open.Count > 0 && LooksLikeTag(shown, open[open.Count - 1]))
                {
                    // A tag, whole: back to the nearest "<" still open, judged by what follows it now (so "<<b>b>" goes too).
                    shown.Length = open[open.Count - 1];
                    open.RemoveAt(open.Count - 1);
                    continue;
                }
                System.Globalization.UnicodeCategory kind = char.GetUnicodeCategory(each);
                if (char.IsControl(each) || kind == System.Globalization.UnicodeCategory.LineSeparator ||
                    kind == System.Globalization.UnicodeCategory.ParagraphSeparator)
                {
                    shown.Append(' ');   // a line break or a tab: a space ("Wood\nChest" reads "Wood Chest")
                    continue;
                }
                if (each == '\\' || each == '$' || char.IsSurrogate(each) ||
                    (kind == System.Globalization.UnicodeCategory.Format && each != (char)0x200C && each != (char)0x200D))
                {
                    continue;
                }
                shown.Append(each);
            }
            // Then, in one pass (review round 5: as long as the text, however many are left): every "<" left (review rounds 3
            // and 4: TextMeshPro reads a tag without its ">", "<br " is a line break, so none stays), no space at the start and
            // none right after another ("Mead < 3" reads "Mead 3", "Wood\n Chest" reads "Wood Chest").
            var plain = new System.Text.StringBuilder(shown.Length);
            int left = 0;   // the next "<" in `open` to leave out
            for (int c = 0; c < shown.Length; c++)
            {
                if (left < open.Count && open[left] == c)
                {
                    left++;
                    continue;
                }
                char kept = shown[c];
                if (kept == ' ' && (plain.Length == 0 || plain[plain.Length - 1] == ' '))
                {
                    continue;
                }
                plain.Append(kept);
            }
            return plain.ToString();
        }

        // Whether the "<" at <paramref name="at"/> starts what looks like a tag: an ASCII letter, "/" or "#" right after it
        // in what's shown (TextMeshPro's and Unity's tags all do; review round 5: no other letter, so "<Eda>" in Cyrillic
        // keeps its word); "<3", "<-" and "<<" don't.
        private static bool LooksLikeTag(System.Text.StringBuilder shown, int at)
        {
            if (at + 1 >= shown.Length)
            {
                return false;
            }
            char next = shown[at + 1];
            return (next >= 'a' && next <= 'z') || (next >= 'A' && next <= 'Z') || next == '/' || next == '#';
        }

        /// <summary>The families whose members are biomes: the catalogue files each trophy and each creature part under its
        /// biome of origin (Items.Classify, ItemHome.Section), so the setup window's headings for them are members to take.</summary>
        internal static bool IsBiomeFamily(string family) => family == "Trophies" || family == "Creature Parts";

        /// <summary>A family's, member's or kind's label (an item's name in his language); a member of a biome family says
        /// its family too, its own label being only a biome: "Swamp Trophies", "Trophies with no biome".</summary>
        internal static string GroupLabel(ItemPick pick)
        {
            if (pick.Level == PickLevel.Member && IsBiomeFamily(pick.Family))
            {
                return pick.Member == Taxonomy.OtherBiome ? $"{pick.Family} with no biome" : $"{pick.Member} {pick.Family}";
            }
            return pick.Label(ItemName);
        }

        // The short words: its parts apart by semicolons.
        private static string Short(ItemPicks picks, int most)
        {
            if (!picks.Readable)
            {
                return picks.TooLong ? "a setup longer than this version can follow" : "a setup from a newer version of the pack";
            }
            Split(picks, out List<string> groups, out List<string> narrowed, out List<string> byName, out List<string> excepts, out List<string> known,
                  out bool other);
            var parts = new List<string>();
            string head = CommaList(groups, most);
            if (head.Length == 0 && picks.InBiomes.Count > 0 && narrowed.Count == 0)
            {
                head = "Everything";
            }
            if (picks.InBiomes.Count > 0 && head.Length > 0)
            {
                head += known.Count == 0 ? " with no biome" : " from " + string.Join(", ", known) + (other ? " or no biome" : string.Empty);
            }
            // Groups with biomes of their own (card A81), each by them ("Mistlands Weapons"), apart from the rest.
            if (narrowed.Count > 0)
            {
                parts.Add(CommaList(narrowed, most));
            }
            if (head.Length > 0)
            {
                parts.Add(head);
            }
            if (byName.Count > 0)
            {
                parts.Add(CommaList(byName, most));
            }
            if (parts.Count == 0)
            {
                parts.Add("Nothing");
            }
            if (excepts.Count > 0)
            {
                parts.Add("except " + CommaList(excepts, most));
            }
            return string.Join("; ", parts);
        }

        // A readable setup in words' pieces, each in the record's fixed order: the families, members, kinds and quick picks
        // it takes that follow its biomes (and "what a newer version picked" for a pick this version doesn't know: it
        // narrows the biomes as a family does, ItemPicks.TakesGroups), those with biomes of their own, by them ("Mistlands
        // Weapons", card A81), the things it takes by name, what it leaves out, and its biomes in the order a player meets
        // them (Other apart).
        private static void Split(ItemPicks picks, out List<string> groups, out List<string> narrowed, out List<string> byName,
                                  out List<string> excepts, out List<string> known, out bool other)
        {
            var takes = new List<ItemPick>(picks.Takes);
            takes.Sort(ItemPick.Compare);
            groups = new List<string>();
            narrowed = new List<string>();
            byName = new List<string>();
            foreach (ItemPick pick in takes)
            {
                IReadOnlyList<string> ownBiomes = picks.OwnBiomesOf(pick);
                if (pick.Level == PickLevel.Item)
                {
                    byName.Add(GroupLabel(pick));
                }
                else if (ownBiomes.Count > 0 && !SetupWindow.TakenWholeByBroader(picks, pick))
                {
                    // (A group a broader one takes whole from every biome is named plain: its own biomes change nothing,
                    // review m1.)
                    narrowed.Add(NarrowedLabel(pick, ownBiomes));
                }
                else
                {
                    groups.Add(GroupLabel(pick));
                }
            }
            if (picks.NewerPicks)
            {
                groups.Add("what a newer version picked");
            }
            var left = new List<ItemPick>(picks.Excepts);
            left.Sort(ItemPick.Compare);
            excepts = new List<string>();
            foreach (ItemPick pick in left)
            {
                excepts.Add(GroupLabel(pick));
            }
            SplitBiomes(picks.InBiomes, out known, out other);
        }

        // The biomes in the order a player meets them, a record's own labels (another version's) after them by name, and
        // whether "Other" (things of no biome) is among them.
        private static void SplitBiomes(IEnumerable<string> biomes, out List<string> known, out bool other)
        {
            known = new List<string>();
            other = false;
            foreach (string biome in biomes)
            {
                if (biome == Taxonomy.OtherBiome)
                {
                    other = true;
                }
                else
                {
                    known.Add(biome);
                }
            }
            known.Sort((a, b) =>
            {
                int order = Taxonomy.BiomeRank(a).CompareTo(Taxonomy.BiomeRank(b));
                return order != 0 ? order : string.CompareOrdinal(a, b);
            });
        }

        // "from the Swamp and Plains biomes", "from the Plains biome, or with no biome".
        private static string FromTheBiomes(List<string> known, bool other) =>
            "from the " + AndList(known, int.MaxValue) + (known.Count == 1 ? " biome" : " biomes") + (other ? ", or with no biome" : string.Empty);

        /// <summary>A list in a sentence: "A", "A and B", "A, B and C"; past <paramref name="most"/>, "A, B and 3 more".</summary>
        internal static string AndList(IList<string> items, int most)
        {
            if (items.Count == 0)
            {
                return string.Empty;
            }
            if (items.Count > most)
            {
                return string.Join(", ", Take(items, most)) + $" and {items.Count - most} more";
            }
            return items.Count == 1 ? items[0] : string.Join(", ", Take(items, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        // A list in the short words: "A, B, C"; past most, "A, B and 3 more".
        private static string CommaList(IList<string> items, int most) =>
            string.Join(", ", Take(items, Math.Min(most, items.Count))) + (items.Count > most ? $" and {items.Count - most} more" : string.Empty);

        private static IEnumerable<string> Take(IList<string> items, int count)
        {
            for (int i = 0; i < count && i < items.Count; i++)
            {
                yield return items[i];
            }
        }
    }
}
