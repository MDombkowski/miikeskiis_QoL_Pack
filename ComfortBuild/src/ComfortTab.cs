using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;
using UnityEngine;

namespace ComfortBuild
{
    /// <summary>
    /// The Comfort tab's list, one more of the build menu's own tabs (the game's IPieceList, like By Usage): the pieces
    /// of the tool in hand that give comfort, with their comfort groups down the left. A group shows only when he can
    /// build something in it, in the order of the setting "Order of the groups". The pieces keep the order the setting
    /// "Order" asks for (progression by default, through the catalogue part, as Add to Cart's Build tab), and under
    /// "Show All" they follow one another group by group. The repair and remove tools come along, as on By Usage, so the
    /// repair button beside the search box stays where it is.
    /// </summary>
    internal sealed class ComfortTab : IPieceList
    {
        /// <summary>The tab's name: the game's own word, in the player's language.</summary>
        internal const string Token = "$se_rested_comfort";

        private readonly List<int> groups = new List<int>();
        private readonly Dictionary<int, int> most = new Dictionary<int, int>();

        // The catalogue's listing of each piece, for its progression order: per world (the biomes are), per tool (the
        // game's order is the table's).
        private readonly Dictionary<Piece, Listing> listings = new Dictionary<Piece, Listing>();
        private object listingsFor;
        private PieceTable listingsTool;

        // The order of the groups, from the setting, read again when the line changes.
        private string orderLine;
        private Dictionary<string, int> orderOf = new Dictionary<string, int>();

        public string DisplayName => Token;

        public bool ShowTags => true;

        public bool CanCustomizeTags => false;

        public int TagCount => groups.Count;

        public int TagSeparatorIndex => -1;

        public string GetTagDisplayName(int index) => Label(groups[index]);

        public int GetTagIdByIndex(int index) => groups[index];

        /// <summary>A group's label as the list shows it: "Chairs 2/3" (what it gives here, the most he can build).</summary>
        internal string Label(int group) =>
            Words.GroupLabel(GroupLabel(group), ComfortMath.SumOfGroup(Nearby.Things, group), most.TryGetValue(group, out int best) ? best : 0,
                             Settings.GroupValues.Value, Looks.Hex(Settings.NumberColor.Value));

        internal static string GroupLabel(int group) => Taxonomy.ComfortLabel(((Piece.ComfortGroup)group).ToString());

        internal static bool IsComfort(Piece piece) => piece != null && piece.m_comfort > 0 && !piece.m_repairPiece && !piece.m_removePiece;

        /// <summary>Whether this tool has any comfort piece he can build now: the tab shows only then.</summary>
        internal static bool HasAny(PieceTable table)
        {
            if (table == null)
            {
                return false;
            }
            foreach (Piece piece in table.m_availablePieces)
            {
                if (IsComfort(piece))
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateAvailableTags(PieceTable pieceTable)
        {
            groups.Clear();
            most.Clear();
            bool done = false;
            Module.Guard("listing the comfort groups", () =>
            {
                FillGroups(pieceTable);
                done = true;
            });
            if (!done)
            {
                // An error switched the mod off (Guard): no half-filled list for the game.
                groups.Clear();
                most.Clear();
            }
        }

        private void FillGroups(PieceTable pieceTable)
        {
            groups.Clear();
            most.Clear();
            var byGroup = new Dictionary<int, List<Thing>>();
            foreach (Piece piece in pieceTable.m_availablePieces)
            {
                if (!IsComfort(piece))
                {
                    continue;
                }
                int group = (int)piece.m_comfortGroup;
                if (!byGroup.TryGetValue(group, out List<Thing> things))
                {
                    byGroup[group] = things = new List<Thing>();
                }
                things.Add(Nearby.AsCandidate(piece));
            }
            foreach (KeyValuePair<int, List<Thing>> group in byGroup)
            {
                most[group.Key] = ComfortMath.SumOfGroup(group.Value, group.Key);
                groups.Add(group.Key);
            }
            groups.Sort((a, b) =>
            {
                int byPlace = GroupPlace(a).CompareTo(GroupPlace(b));
                return byPlace != 0 ? byPlace : a.CompareTo(b);
            });
        }

        public void GetAvailablePiecesWithTag(int tagId, PieceTable pieceTable, IList<Piece> resultOut)
        {
            var repair = new List<Piece>();
            var comfort = new List<Piece>();
            var ordered = new List<Piece>();
            Module.Guard("listing the comfort pieces", () =>
            {
                Pick(tagId, pieceTable, repair, comfort);
                ordered.AddRange(Ordered(comfort, pieceTable));
            });
            foreach (Piece piece in repair)
            {
                resultOut.Add(piece);
            }
            foreach (Piece piece in ordered)
            {
                resultOut.Add(piece);
            }
        }

        private static void Pick(int tagId, PieceTable pieceTable, List<Piece> repair, List<Piece> comfort)
        {
            foreach (Piece piece in pieceTable.m_availablePieces)
            {
                if (piece.m_repairPiece || piece.m_removePiece)
                {
                    repair.Add(piece);
                }
                else if (IsComfort(piece) && (tagId == -1 || (int)piece.m_comfortGroup == tagId))
                {
                    comfort.Add(piece);
                }
            }
        }

        /// <summary>The pieces group by group (the list's order), each group in the order the setting asks for.</summary>
        private IEnumerable<Piece> Ordered(List<Piece> pieces, PieceTable table)
        {
            ListingsFor(table);
            var byListing = new Dictionary<Listing, Piece>();
            Comparison<Listing> inGroup;
            switch (Settings.Order.Value)
            {
                case PieceOrder.Name:
                    inGroup = (a, b) =>
                    {
                        int order = Browse.CompareNames(a, b);
                        return order != 0 ? order : Browse.CompareProgression(a, b);
                    };
                    break;
                case PieceOrder.ComfortHighestFirst:
                    inGroup = (a, b) =>
                    {
                        int order = ComfortOf(b).CompareTo(ComfortOf(a));
                        return order != 0 ? order : Browse.CompareProgression(a, b);
                    };
                    break;
                default:
                    inGroup = Browse.CompareProgression;
                    break;
            }
            var rows = new List<Listing>();
            foreach (Piece piece in pieces)
            {
                Listing listing = ListingOf(piece, table);
                byListing[listing] = piece;
                rows.Add(listing);
            }
            // OrderBy is stable: pieces the orders call equal keep the game's own order.
            return rows.OrderBy(row => GroupPlace((int)byListing[row].m_comfortGroup))
                       .ThenBy(row => (int)byListing[row].m_comfortGroup)
                       .ThenBy(row => row, Comparer<Listing>.Create(inGroup))
                       .Select(row => byListing[row]);

            int ComfortOf(Listing listing) => byListing.TryGetValue(listing, out Piece piece) ? piece.m_comfort : 0;
        }

        private void ListingsFor(PieceTable table)
        {
            // A new world makes new prefabs and new biomes; another tool another order.
            object world = ZNetScene.instance;
            if (!ReferenceEquals(world, listingsFor) || table != listingsTool)
            {
                listings.Clear();
                listingsFor = world;
                listingsTool = table;
            }
        }

        private Listing ListingOf(Piece piece, PieceTable table)
        {
            if (listings.TryGetValue(piece, out Listing listing))
            {
                return listing;
            }
            int gameOrder = table.m_pieces.IndexOf(piece.gameObject);
            if (gameOrder < 0)
            {
                gameOrder = table.m_pieces.Count;
            }
            try
            {
                listing = Listings.Piece("piece:" + piece.gameObject.name, piece.gameObject, piece, gameOrder);
            }
            catch (Exception error)
            {
                // A piece the catalogue can't read (another mod's) still lists, in the game's own order.
                Module.Log.LogWarning($"The catalogue couldn't place {piece.gameObject.name} ({error.Message}); it lists in the game's order.");
                listing = new Listing { Shelf = Shelf.Build, Key = "piece:" + piece.gameObject.name, GameOrder = gameOrder, Name = GameFacts.Localize(piece.m_name) };
            }
            // Before the world's biomes are worked out, every piece is "Other": list them, but place them again later.
            if (Biomes.Built)
            {
                listings[piece] = listing;
            }
            return listing;
        }

        /// <summary>A group's place in the list, from the setting "Order of the groups"; unknown groups after the rest.</summary>
        private int GroupPlace(int group)
        {
            string line = Settings.GroupOrder.Value ?? string.Empty;
            if (line != orderLine)
            {
                orderLine = line;
                var labels = Taxonomy.ComfortGroups.Select(each => each.Label).ToList();
                (List<string> order, _) = Orders.Apply(line, labels, new Dictionary<string, List<string>>());
                orderOf = new Dictionary<string, int>();
                for (int i = 0; i < order.Count; i++)
                {
                    orderOf[order[i]] = i;
                }
            }
            return orderOf.TryGetValue(GroupLabel(group), out int place) ? place : orderOf.Count;
        }
    }
}
