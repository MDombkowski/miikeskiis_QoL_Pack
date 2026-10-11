using System;
using System.Collections.Generic;
using System.Linq;

namespace UseCase
{
    /// <summary>
    /// Every use of every item in one world, keyed by the item's $ name (as the pack keys items: MODULES.md, "Items are
    /// named by a key"). Built from GameReader's facts by UseBuilder; the two "anything" rules and selling, which would
    /// put a line on nearly every item, are added only for the item asked about, and only when their settings ask. It
    /// knows nothing of the panel or the settings but what For is given, so it could move into the catalogue part later
    /// (the card's decision 9).
    /// </summary>
    internal sealed class UseIndex
    {
        private readonly Dictionary<string, List<Use>> byResource;
        private readonly AnythingFacts anything;
        private readonly string sellerToken;
        private readonly string sellerPrefab;
        private readonly Dictionary<string, List<Use>> learned = new Dictionary<string, List<Use>>(StringComparer.Ordinal);

        internal UseIndex(GameFactsRead facts)
        {
            byResource = UseBuilder.Build(facts);
            anything = facts.Anything;
            TraderFact buyer = facts.Traders.FirstOrDefault(trader => trader.Prefab == "Haldor") ?? facts.Traders.FirstOrDefault();
            sellerToken = buyer?.Token ?? "Haldor";
            sellerPrefab = buyer?.Prefab ?? "Haldor";
            Count = byResource.Values.Sum(list => list.Count);
        }

        /// <summary>How many uses it holds, for the log.</summary>
        internal int Count { get; }

        internal int Resources => byResource.Count;

        /// <summary>Goes up when a place learned in the world adds a use.</summary>
        internal int Version { get; private set; }

        /// <summary>The uses of one item. <paramref name="value"/> is what Haldor pays for one (0: nothing), for
        /// selling.</summary>
        internal List<Use> For(string resource, bool selling, bool anythingRules, int value)
        {
            var uses = new List<Use>();
            if (string.IsNullOrEmpty(resource))
            {
                return uses;
            }
            if (byResource.TryGetValue(resource, out List<Use> known))
            {
                uses.AddRange(known);
            }
            if (learned.TryGetValue(resource, out List<Use> places))
            {
                uses.AddRange(places);
            }
            if (selling && value > 0)
            {
                uses.Add(new Use { Kind = UseKind.Selling, Resource = resource, Token = sellerToken, Prefab = sellerPrefab, Amount = value, KnownBy = KnownBy.Always });
            }
            if (anythingRules)
            {
                PieceFact obliterator = anything.Obliterator;
                if (obliterator != null && !anything.ObliteratorListed.Contains(resource) && resource != anything.ObliteratorResult)
                {
                    uses.Add(new Use { Kind = UseKind.Processing, Resource = resource, Token = obliterator.Token, Prefab = obliterator.Prefab,
                        Amount = anything.ObliteratorCost, PerProduct = anything.ObliteratorCost, KnownBy = KnownBy.Piece, KnownKey = obliterator.Token,
                        Sort = obliterator.Sort, Mod = obliterator.Mod });
                }
                PieceFact catapult = anything.Catapult;
                if (catapult != null && anything.CatapultTakes.Contains(resource))
                {
                    uses.Add(new Use { Kind = UseKind.Ammo, Resource = resource, Token = catapult.Token, Prefab = catapult.Prefab, Amount = anything.CatapultHolds,
                        KnownBy = KnownBy.Piece, KnownKey = catapult.Token, Sort = catapult.Sort, Mod = catapult.Mod });
                }
            }
            return uses;
        }

        /// <summary>Adds a place learned as it loaded near him (another mod's altar or door); true when it is new.</summary>
        internal bool Learn(PlaceFact place)
        {
            if (place == null || place.Item.Length == 0 || byResource.TryGetValue(place.Item, out List<Use> known) && known.Any(use => use.KnownKey == place.SeenKey && use.Kind == place.Kind))
            {
                return false;
            }
            if (!learned.TryGetValue(place.Item, out List<Use> list))
            {
                learned[place.Item] = list = new List<Use>();
            }
            if (list.Any(use => use.KnownKey == place.SeenKey))
            {
                return false;
            }
            list.Add(new Use { Kind = place.Kind, Resource = place.Item, Token = place.Token, Prefab = place.Prefab, Amount = place.Count > 0 ? place.Count : -1,
                Note = place.Note, KnownBy = KnownBy.Seen, KnownKey = place.SeenKey, BeatenKey = place.BeatenKey, Sort = place.Sort, GameOrder = 1000 + list.Count });
            Version++;
            return true;
        }
    }
}
