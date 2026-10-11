namespace Grounded
{
    /// <summary>What a piece is to Grounded.</summary>
    internal enum PieceKind
    {
        /// <summary>Neither a listed rug nor a listed floor.</summary>
        Other,

        /// <summary>Looked through, never landed on.</summary>
        Rug,

        /// <summary>Landed on.</summary>
        Floor,
    }

    /// <summary>The rules that say what a piece is, as the settings stand: read again for every question, so a change
    /// in F1 counts at once.</summary>
    internal sealed class KindRules
    {
        internal NameList Rugs;
        internal bool RugGroupCounts;
        internal NameList Floors;
        internal bool FloorTagCounts;
        internal bool PlansCount;
    }

    /// <summary>
    /// Names and kinds of pieces, from plain facts (an object's name, its comfort group, its Floor tag), so the scratch
    /// harness can run every rule without the game.
    /// </summary>
    internal static class Kinds
    {
        /// <summary>The end PlanBuild gives the name of a planned piece's prefab ("rug_straw_planned").</summary>
        internal const string PlanSuffix = "_planned";

        /// <summary>Where a prefab's name ends in its object's name ("rug_straw(Clone)", "rug_straw (1)").</summary>
        private static readonly char[] NameEnds = { '(', ' ' };

        /// <summary>
        /// The prefab name of a piece from its object's name, in lower case: cut at the first "(" or space, as the game's
        /// Utils.GetPrefabName does ("rug_straw(Clone)" is rug_straw). A PlanBuild plan ("rug_straw_planned") is named as
        /// its piece while plans count as their pieces, and says it is a plan either way. Null for no name.
        /// </summary>
        internal static string PrefabName(string objectName, bool plansCount, out bool isPlan)
        {
            isPlan = false;
            if (objectName == null)
            {
                return null;
            }
            int cut = objectName.IndexOfAny(NameEnds);
            string name = (cut >= 0 ? objectName.Substring(0, cut) : objectName).ToLowerInvariant();
            if (name.Length > PlanSuffix.Length && name.EndsWith(PlanSuffix, System.StringComparison.Ordinal))
            {
                isPlan = true;
                if (plansCount)
                {
                    name = name.Substring(0, name.Length - PlanSuffix.Length);
                }
            }
            return name;
        }

        /// <summary>
        /// What a piece is: a rug if its name is on the rugs list, or it is in the game's rug comfort group and that
        /// counts; else a floor if its name is on the floors list, or the build menu files it under Floor and that counts;
        /// else other. A rug is never a floor. A PlanBuild plan is other while plans don't count as their pieces.
        /// </summary>
        internal static PieceKind Classify(string objectName, bool carpetGroup, bool floorTag, KindRules rules)
        {
            string name = PrefabName(objectName, rules.PlansCount, out bool isPlan);
            if (name == null || (isPlan && !rules.PlansCount))
            {
                return PieceKind.Other;
            }
            if (rules.Rugs.HasName(name) || (rules.RugGroupCounts && carpetGroup))
            {
                return PieceKind.Rug;
            }
            if (rules.Floors.HasName(name) || (rules.FloorTagCounts && floorTag))
            {
                return PieceKind.Floor;
            }
            return PieceKind.Other;
        }
    }
}
