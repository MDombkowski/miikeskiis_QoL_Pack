using System.Collections.Generic;
using Outfitter.Equip;
using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>What the stand's key does at a marked stand.</summary>
    internal enum StandAction
    {
        PutOn,
        HangBack,
    }

    /// <summary>One of a loadout's pieces a stand can show, and the place it hangs in there.</summary>
    internal sealed class StandPiece
    {
        internal LoadoutItem Item;
        internal int Place;
    }

    /// <summary>
    /// The stands' decisions, on what the stand's record and his inventory say now (the plan's "Put it on" and "Hang it
    /// back"): which of a loadout's pieces a stand can show, which of them are on the stand and which on him, what the key
    /// does, and which of his pieces go to which place of a stand. Each of his items answers one piece at most, and each
    /// place takes one item, the first that comes, as the game's attach takes the first place that fits. Pure apart from the
    /// game's plain item classes, so the scratch harness reaches it.
    /// </summary>
    internal static class StandPlan
    {
        /// <summary>The loadout's pieces the stand can show: what it shows (LoadoutItem.Displayed: what it wears, unless
        /// marked otherwise; 0.5.0, his "Displayed" and "Worn"), that fits a place of the stand, each place once: the first
        /// piece for it marked Displayed by hand, else the first shown by being worn.</summary>
        internal static List<StandPiece> Pieces(Loadout loadout, IStand stand) => PiecesBy(loadout, stand != null ? stand.FitForPrefab : (System.Func<string, int>)null);

        /// <summary><see cref="Pieces(Loadout, IStand)"/> by a stand's fit alone (the window's stand panel, a stand that
        /// isn't there: StandPrototype).</summary>
        internal static List<StandPiece> PiecesBy(Loadout loadout, System.Func<string, int> fitFor)
        {
            var pieces = new List<StandPiece>();
            if (loadout == null || fitFor == null)
            {
                return pieces;
            }
            var taken = new HashSet<int>();
            foreach (bool byHand in new[] { true, false })
            {
                foreach (LoadoutItem item in loadout.Items)
                {
                    if (!item.Displayed || (item.Shown == true) != byHand)
                    {
                        continue;   // not shown (carried, hidden, or a food slot: a stand's loadout fetches its food like the rest)
                    }
                    int place = fitFor(item.Prefab);
                    if (place < 0 || !taken.Add(place))
                    {
                        continue;
                    }
                    pieces.Add(new StandPiece { Item = item, Place = place });
                }
            }
            pieces.Sort((a, b) => loadout.Items.IndexOf(a.Item).CompareTo(loadout.Items.IndexOf(b.Item)));   // the loadout's order, as before
            return pieces;
        }

        /// <summary>The loadout's shown items that want a place of the stand another shown item has (they don't show), and
        /// those no place takes, for the window's stand panel.</summary>
        internal static List<LoadoutItem> NotShown(Loadout loadout, List<StandPiece> pieces, System.Func<string, int> fitFor, out List<LoadoutItem> noPlace)
        {
            var crowded = new List<LoadoutItem>();
            noPlace = new List<LoadoutItem>();
            if (loadout == null || fitFor == null)
            {
                return crowded;
            }
            foreach (LoadoutItem item in loadout.Items)
            {
                if (!item.Displayed || pieces.Exists(piece => piece.Item == item))
                {
                    continue;
                }
                (fitFor(item.Prefab) < 0 ? noPlace : crowded).Add(item);
            }
            return crowded;
        }

        /// <summary>Whether an item answers a loadout's piece: the same prefab, and what an equip asks of it (the quality,
        /// and the style, maker and other mods' data when the settings ask: Matching.Answers).</summary>
        internal static bool Answers(ItemDrop.ItemData item, LoadoutItem piece, EquipRules rules) =>
            item != null && piece != null && Capture.PrefabOf(item) == piece.Prefab && Matching.Answers(item, piece, rules);

        /// <summary>The pieces the stand holds an answer to, by place: the piece its item answers (each piece once). Only an
        /// item the stands move counts (StandMoves.OneOf): another mod's stack left there is nobody's piece, so it never
        /// makes the key put a loadout on for ever (review round 2, m3).</summary>
        internal static Dictionary<int, StandPiece> OnStand(IStand stand, List<StandPiece> pieces, EquipRules rules)
        {
            var found = new Dictionary<int, StandPiece>();
            var answered = new HashSet<StandPiece>();
            for (int place = 0; place < stand.Places; place++)
            {
                if (!stand.Has(place))
                {
                    continue;
                }
                ItemDrop.ItemData item = stand.Read(place);
                if (!StandMoves.OneOf(item))
                {
                    continue;
                }
                foreach (StandPiece piece in pieces)
                {
                    if (!answered.Contains(piece) && Answers(item, piece.Item, rules))
                    {
                        answered.Add(piece);
                        found[place] = piece;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>His worn items (a weapon the game has put away for a moment counts) answering the pieces, each piece
        /// and each item once, in his inventory's order; a piece the loadout shows but doesn't wear (0.5.0: "Displayed",
        /// not "Worn") answered by an item he carries too, as putting it on carries it.</summary>
        internal static Dictionary<ItemDrop.ItemData, StandPiece> OnHim(IWearer wearer, List<StandPiece> pieces, EquipRules rules,
                                                                        ICollection<StandPiece> skip = null, bool carried = true)
        {
            var found = new Dictionary<ItemDrop.ItemData, StandPiece>();
            var answered = new HashSet<StandPiece>();
            var worn = new HashSet<ItemDrop.ItemData>(Worn(wearer));
            foreach (ItemDrop.ItemData item in wearer.Inventory.GetAllItemsInGridOrder())
            {
                if (item == null || item.m_stack <= 0)
                {
                    continue;
                }
                bool wears = worn.Contains(item);
                if (!wears && !carried)
                {
                    continue;
                }
                foreach (StandPiece piece in pieces)
                {
                    if (!answered.Contains(piece) && (skip == null || !skip.Contains(piece)) && (wears || piece.Item.Worn == WornAs.None) &&
                        Answers(item, piece.Item, rules))
                    {
                        answered.Add(piece);
                        found[item] = piece;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>The items he carries or wears answering the pieces, each piece and each item once, in his inventory's
        /// order, as an equip would take them for a piece to wear: never a broken one (the game won't put it on, so the equip
        /// looks further). For lending (job 30, Session 3): a piece with him needs no other stand's.</summary>
        internal static Dictionary<ItemDrop.ItemData, StandPiece> WithHim(IWearer wearer, List<StandPiece> pieces, EquipRules rules,
                                                                          ICollection<StandPiece> skip = null)
        {
            var found = new Dictionary<ItemDrop.ItemData, StandPiece>();
            var answered = new HashSet<StandPiece>();
            foreach (ItemDrop.ItemData item in wearer.Inventory.GetAllItemsInGridOrder())
            {
                if (item == null || item.m_stack <= 0 || Matching.Broken(item))
                {
                    continue;
                }
                foreach (StandPiece piece in pieces)
                {
                    if (!answered.Contains(piece) && (skip == null || !skip.Contains(piece)) && Answers(item, piece.Item, rules))
                    {
                        answered.Add(piece);
                        found[item] = piece;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>What he wears now, in his inventory's order (a hand item the game has put away for a moment counts).</summary>
        internal static List<ItemDrop.ItemData> Worn(IWearer wearer)
        {
            var worn = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in wearer.Inventory.GetAllItemsInGridOrder())
            {
                if (wearer.WornAs(item) != WornAs.None || ReferenceEquals(item, wearer.HiddenRight) || ReferenceEquals(item, wearer.HiddenLeft))
                {
                    worn.Add(item);
                }
            }
            return worn;
        }

        /// <summary>What the key does (decision 8), piece by piece: a piece on the stand and not on him, that he could
        /// wear (<see cref="Wearable"/>), put it on; else a piece on him, hang it back (a piece both on the stand and on him,
        /// a second copy, counts for neither, and so does one on the stand he couldn't wear: a broken helmet would otherwise
        /// make the key put it on for ever, review round 1's M2); neither, put it on (everything from the chests).</summary>
        internal static StandAction Decide(IStand stand, IWearer wearer, List<StandPiece> pieces, EquipRules rules)
        {
            var onHim = new HashSet<StandPiece>(OnHim(wearer, pieces, rules).Values);
            var onStand = new HashSet<StandPiece>();
            foreach (KeyValuePair<int, StandPiece> each in OnStand(stand, pieces, rules))
            {
                onStand.Add(each.Value);
                if (!onHim.Contains(each.Value) && Wearable(stand.Read(each.Key), rules))
                {
                    return StandAction.PutOn;
                }
            }
            // Only a piece on him and not on the stand can be hung back (a second copy stays with him: HangNow skips it), so
            // only such a piece makes the key hang back (the harness, area A's first defect). A piece the loadout only shows
            // (Displayed, not Worn) answered by what he carries does so only for a loadout that wears none of the stand's
            // pieces: else his everyday hammer would hang back alone instead of the loadout going on (review 1's m4).
            var wornOnHim = new HashSet<StandPiece>(OnHim(wearer, pieces, rules, carried: false).Values);
            bool wearsAny = pieces.Exists(piece => piece.Item.Worn != WornAs.None);
            foreach (StandPiece piece in onHim)
            {
                if (!onStand.Contains(piece) && (wornOnHim.Contains(piece) || !wearsAny))
                {
                    return StandAction.HangBack;
                }
            }
            return StandAction.PutOn;
        }

        /// <summary>Whether the DLC an item needs is installed (DLCMan in play); the harness sets another.</summary>
        internal static System.Func<string, bool> HasDlc = dlc => DLCMan.instance == null || DLCMan.instance.IsDLCInstalled(dlc);

        /// <summary>Whether an equip could put an item on him: the game's own refusals (Humanoid.EquipItem, 1.0.17: a broken
        /// item, a DLC not installed, a belt or trinket below the world's level), and, in Swap gear only, only what counts as
        /// gear (the planner leaves the rest out).</summary>
        internal static bool Wearable(ItemDrop.ItemData item, EquipRules rules)
        {
            if (item == null || Matching.Broken(item))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(item.m_shared.m_dlc) && !HasDlc(item.m_shared.m_dlc))
            {
                return false;
            }
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            if (Game.m_worldLevel > 0 && item.m_worldLevel < Game.m_worldLevel &&
                (type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Trinket))
            {
                return false;
            }
            return rules.Mode != EquipMode.SwapGearOnly || Matching.IsGear(type, rules);
        }

        /// <summary>Whether a place can take one of his items: empty, or holding an item the stands can move (a stray, which
        /// comes to him first: one of it, this game's: StandMoves.OneOf) that isn't kept there (<paramref name="keep"/>: a
        /// piece of the stand's own loadout).</summary>
        internal static bool Open(IStand stand, int place, ICollection<int> keep)
        {
            if (!stand.Has(place))
            {
                return true;
            }
            return (keep == null || !keep.Contains(place)) && StandMoves.OneOf(stand.Read(place));
        }

        /// <summary>
        /// Which of these items of his go onto the stand, and where: each into the place the game would hang it in, while
        /// that place is open (<see cref="Open"/>; <paramref name="emptied"/>: places that will be empty by then) and no
        /// earlier item took it. Items in the order given.
        /// </summary>
        internal static Dictionary<ItemDrop.ItemData, int> Assign(IStand stand, IEnumerable<ItemDrop.ItemData> items, ICollection<int> keep,
                                                                  ICollection<int> emptied = null)
        {
            var places = new Dictionary<ItemDrop.ItemData, int>();
            var taken = new HashSet<int>();
            foreach (ItemDrop.ItemData item in items)
            {
                int place = stand.FitFor(item);
                if (place < 0 || taken.Contains(place) || !((emptied != null && emptied.Contains(place)) || Open(stand, place, keep)))
                {
                    continue;
                }
                taken.Add(place);
                places[item] = place;
            }
            return places;
        }

        /// <summary>The loadout's pieces neither on the stand nor on him (taken by hand, say), for the hover's "lacks".</summary>
        internal static List<StandPiece> Lacks(IStand stand, IWearer wearer, List<StandPiece> pieces, EquipRules rules)
        {
            var onStand = new HashSet<StandPiece>(OnStand(stand, pieces, rules).Values);
            var onHim = new HashSet<StandPiece>(OnHim(wearer, pieces, rules, onStand).Values);
            var lacks = new List<StandPiece>();
            foreach (StandPiece piece in pieces)
            {
                if (!onStand.Contains(piece) && !onHim.Contains(piece))
                {
                    lacks.Add(piece);
                }
            }
            return lacks;
        }
    }
}
