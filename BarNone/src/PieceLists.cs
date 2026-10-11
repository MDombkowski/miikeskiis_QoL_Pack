using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarNone
{
    /// <summary>
    /// One list of prefab names (a part's pieces, or the items the items part leaves alone) from a comma-separated
    /// setting, compared in lower case. The text is parsed again only when it changes, so a list edited in a
    /// configuration manager counts at once.
    /// </summary>
    internal sealed class PieceList
    {
        private readonly Func<string> source;
        private string parsedFrom;
        private HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        internal PieceList(Func<string> source)
        {
            this.source = source;
        }

        /// <summary>Whether this prefab name (lower case, as PieceNames gives it) is on the list.</summary>
        internal bool HasName(string lowerName)
        {
            if (lowerName == null)
            {
                return false;
            }
            string text = source() ?? string.Empty;
            if (!string.Equals(text, parsedFrom, StringComparison.Ordinal))
            {
                names = Parse(text);
                parsedFrom = text;
            }
            return names.Contains(lowerName);
        }

        /// <summary>The names in a list's text: split at commas, spaces around each trimmed, lower case, empty ones left
        /// out.</summary>
        internal static HashSet<string> Parse(string text)
        {
            var parsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string part in (text ?? string.Empty).Split(','))
            {
                string name = part.Trim().ToLowerInvariant();
                if (name.Length > 0)
                {
                    parsed.Add(name);
                }
            }
            return parsed;
        }
    }

    /// <summary>The five parts' lists, each read from its own setting, and the items part's exceptions.</summary>
    internal static class PieceLists
    {
        internal static readonly PieceList Reach = new PieceList(() => Settings.ReachPieces?.Value);
        internal static readonly PieceList Smoke = new PieceList(() => Settings.SmokePieces?.Value);
        internal static readonly PieceList Projectiles = new PieceList(() => Settings.ProjectilePieces?.Value);
        internal static readonly PieceList Sky = new PieceList(() => Settings.SkyPieces?.Value);
        internal static readonly PieceList Items = new PieceList(() => Settings.ItemPieces?.Value);

        /// <summary>The items that never fall through (item prefab names, not pieces).</summary>
        internal static readonly PieceList ExceptItems = new PieceList(() => Settings.ExceptItems?.Value);

        /// <summary>Whether this collider belongs to a piece on the list.</summary>
        internal static bool Has(this PieceList list, Collider collider) => collider != null && list.HasName(PieceNames.Of(collider));
    }

    /// <summary>
    /// What a hit collider (or a hovered object) belongs to, worked out once per object and kept: the prefab name of its
    /// piece (the nearest Piece at or above it, so a cage wall built on a ship is the wall; lower case, or null for no
    /// piece), and whether it can be used itself (an Interactable at or above it: every gate and the wood shutter are
    /// doors). Something that only shows a text when pointed at (a Hoverable alone) can't be used, and is passed: since
    /// 2026-10-08 (card A50), JereKuusela's ESP gives every building piece a hover text of its own, whether it shows
    /// anything or not, and the cage floors stopped being reached through. An object never changes piece; the cache is
    /// emptied once it passes its limit.
    /// </summary>
    internal static class PieceNames
    {
        internal const int Limit = 50000;

        private static readonly Dictionary<int, Facts> known = new Dictionary<int, Facts>();

        /// <summary>The lower-case prefab name of the piece this collider belongs to, or null.</summary>
        internal static string Of(Component part) => part == null ? null : FactsOf(part.gameObject).Name;

        /// <summary>The lower-case prefab name of the piece this object belongs to, or null.</summary>
        internal static string Of(GameObject thing) => thing == null ? null : FactsOf(thing).Name;

        /// <summary>Whether this collider's object, or anything above it, can be used (an Interactable).</summary>
        internal static bool IsUsable(Component part) => part != null && FactsOf(part.gameObject).Usable;

        /// <summary>Whether this object, or anything above it, can be used (an Interactable).</summary>
        internal static bool IsUsable(GameObject thing) => thing != null && FactsOf(thing).Usable;

        internal static int Count => known.Count;

        internal static void Forget() => known.Clear();

        private static Facts FactsOf(GameObject thing)
        {
            int id = thing.GetInstanceID();
            if (known.TryGetValue(id, out Facts facts))
            {
                return facts;
            }
            if (known.Count >= Limit)
            {
                known.Clear();
            }
            Piece piece = thing.GetComponentInParent<Piece>();
            facts = new Facts(piece != null ? Utils.GetPrefabName(piece.gameObject).ToLowerInvariant() : null,
                              thing.GetComponentInParent<Interactable>() != null);
            known[id] = facts;
            return facts;
        }

        private readonly struct Facts
        {
            internal readonly string Name;
            internal readonly bool Usable;

            internal Facts(string name, bool usable)
            {
                Name = name;
                Usable = usable;
            }
        }
    }
}
