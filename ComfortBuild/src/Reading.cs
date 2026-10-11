using QoLMods.Catalogue;

namespace ComfortBuild
{
    /// <summary>
    /// The comfort line beside the build panel (Line): a piece's comfort and group, what it would add where he stands,
    /// and why not all of it. The game fills that panel every frame while he builds (Hud.SetupPieceInfo, for the piece
    /// under the pointer, else the one in hand), so the line is kept until the piece, the look around him or the
    /// settings change.
    /// </summary>
    internal static class Reading
    {
        private static Piece linePiece;
        private static int lineVersion = -1;
        private static int lineSettings = -1;
        private static string line;

        /// <summary>The line for this piece, or null for a piece without comfort, or where the mod shows nothing.</summary>
        internal static string LineFor(Piece piece)
        {
            if (!Module.IsOn || piece == null || !ComfortTab.IsComfort(piece) || !(Menu.OnOurTab || Settings.EveryTab.Value))
            {
                return null;
            }
            int settings = Settings.Version;
            if (piece == linePiece && lineVersion == Nearby.Version && settings == lineSettings)
            {
                return line;
            }
            linePiece = piece;
            lineVersion = Nearby.Version;
            lineSettings = settings;
            line = Make(piece);
            return line;
        }

        private static string Make(Piece piece)
        {
            Thing candidate = Nearby.AsCandidate(piece);
            int gain = ComfortMath.Gain(Nearby.Things, candidate);
            string inTheWay = null;
            int inTheWayGives = 0;
            if (gain < candidate.Comfort)
            {
                int at = ComfortMath.InTheWay(Nearby.Things, candidate);
                if (at >= 0 && at < Nearby.Pieces.Count && Nearby.Pieces[at] != null)
                {
                    inTheWay = GameFacts.Localize(Nearby.Pieces[at].m_name);
                    inTheWayGives = Nearby.Things[at].Comfort;
                }
            }
            // A fire near him that is out, and would give at least as much once lit: worth lighting before building.
            string unlit = null;
            int unlitGives = 0;
            string lit = "lit";
            if (gain > 0 && !Settings.UnlitAsLit.Value)
            {
                for (int i = 0; i < Nearby.Pieces.Count; i++)
                {
                    Piece near = Nearby.Pieces[i];
                    if (near == null || Nearby.Things[i].Comfort > 0 || near.m_comfort < candidate.Comfort)
                    {
                        continue;
                    }
                    bool sameKind = candidate.Group != ComfortMath.NoGroup ? (int)near.m_comfortGroup == candidate.Group : near.m_name == piece.m_name;
                    if (sameKind && near.m_comfort > unlitGives)
                    {
                        unlit = GameFacts.Localize(near.m_name);
                        unlitGives = near.m_comfort;
                        lit = near.m_comfortGroup == Piece.ComfortGroup.Leisure ? "heated" : "lit";
                    }
                }
            }
            return Words.PieceLine(candidate.Comfort, Taxonomy.ComfortKind(((Piece.ComfortGroup)candidate.Group).ToString()),
                                   candidate.Group != ComfortMath.NoGroup, gain, inTheWay, inTheWayGives, unlit, unlitGives, lit,
                                   Nearby.Sheltered, Looks.Hex(Settings.NumberColor.Value));
        }
    }
}
