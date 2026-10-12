// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;

namespace QoLMods.Catalogue
{
    /// <summary>One thing a piece costs, as the rules need it.</summary>
    internal sealed class PieceCost
    {
        /// <summary>The item's prefab name (canonical), which names the structural materials.</summary>
        internal string Prefab;

        /// <summary>The item's name as the player reads it: a member of the "Other" materials when it is what the piece
        /// is mostly made of.</summary>
        internal string Name;

        internal int Amount;

        /// <summary>The item's biome of origin (a label), which decides what a piece is made of.</summary>
        internal string Biome = Taxonomy.OtherBiome;

        /// <summary>The item's Take family, which sends a piece of hide or cloth to Hides and Fabrics.</summary>
        internal string Family = string.Empty;
    }

    /// <summary>What the rules need to know about one building piece.</summary>
    internal sealed class PieceFacts
    {
        internal string Prefab;

        /// <summary>Its build-menu categories (Piece.m_usage), the flags by name, in the game's order.</summary>
        internal readonly List<string> Flags = new List<string>();

        /// <summary>What it costs, in the game's order.</summary>
        internal readonly List<PieceCost> Costs = new List<PieceCost>();
    }

    /// <summary>Where a piece sits in the Build tab.</summary>
    internal sealed class PieceHome
    {
        /// <summary>Its categories (labels), in the game's order: it shows under each.</summary>
        internal readonly List<string> Categories = new List<string>();

        /// <summary>Its most specific category: its heading under "All".</summary>
        internal string Primary;

        /// <summary>What it is made of: the wood, stone or metal it costs that comes latest in the game; for a piece
        /// costing none of those, the name of its biggest cost.</summary>
        internal string Material = string.Empty;

        /// <summary>Wood, Stone or Metal for a piece of those; else Hides and Fabrics or Other.</summary>
        internal string MaterialFamily = "Other";

        /// <summary>The families of all the wood, stone and metal it costs (a piece can cost wood and iron).</summary>
        internal readonly HashSet<string> StructuralFamilies = new HashSet<string>();

        /// <summary>The labels of all the wood, stone and metal it costs: Materials › Finewood lists every piece
        /// costing finewood, as the game's own Materials tab does.</summary>
        internal readonly HashSet<string> StructuralMaterials = new HashSet<string>();

        internal bool InBuilding
        {
            get
            {
                foreach (string category in Categories)
                {
                    if (category == Taxonomy.BuildingParent || System.Array.IndexOf(Taxonomy.BuildingSubs, category) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>The Build tab's rules (the mock-up's build section, ported).</summary>
    internal static class Pieces
    {
        internal static PieceHome Home(PieceFacts piece)
        {
            var home = new PieceHome();
            foreach (string flag in piece.Flags)
            {
                home.Categories.Add(Taxonomy.CategoryOf.TryGetValue(flag, out string label) ? label : flag);
            }
            home.Primary = null;
            foreach (string category in Taxonomy.PrimaryPriority)
            {
                if (home.Categories.Contains(category))
                {
                    home.Primary = category;
                    break;
                }
            }
            if (home.Primary == null)
            {
                home.Primary = home.Categories.Count > 0 ? home.Categories[0] : "Misc.";
            }

            // Made of: the structural material that comes latest in the game (its biome, then its place in the unlock
            // order), so a Wood Iron Beam counts as Iron and a Darkwood piece as Finewood.
            PieceCost latest = null;
            int latestBiome = -1;
            int latestIndex = -1;
            foreach (PieceCost cost in piece.Costs)
            {
                if (!Taxonomy.StructuralByPrefab.TryGetValue(cost.Prefab ?? string.Empty, out (string Label, string Family) material))
                {
                    continue;
                }
                home.StructuralFamilies.Add(material.Family);
                home.StructuralMaterials.Add(material.Label);
                int biome = Taxonomy.BiomeRank(cost.Biome);
                int index = IndexOfMaterial(cost.Prefab);
                if (latest == null || biome > latestBiome || (biome == latestBiome && index > latestIndex))
                {
                    latest = cost;
                    latestBiome = biome;
                    latestIndex = index;
                }
            }
            if (latest != null)
            {
                (string label, string family) = Taxonomy.StructuralByPrefab[latest.Prefab];
                home.Material = label;
                home.MaterialFamily = family;
            }
            else if (piece.Costs.Count > 0)
            {
                // Pieces costing none of those fall under their biggest cost (the first, on a tie).
                PieceCost biggest = piece.Costs[0];
                foreach (PieceCost cost in piece.Costs)
                {
                    if (cost.Amount > biggest.Amount)
                    {
                        biggest = cost;
                    }
                }
                home.Material = biggest.Name ?? string.Empty;
                home.MaterialFamily = biggest.Family == "Hides and Fabrics" ? "Hides and Fabrics" : "Other";
            }
            return home;
        }

        private static int IndexOfMaterial(string prefab)
        {
            for (int i = 0; i < Taxonomy.StructuralMaterials.Length; i++)
            {
                if (Taxonomy.StructuralMaterials[i].Prefab == prefab)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
