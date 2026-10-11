namespace Outfitter.Stands
{
    /// <summary>
    /// A stand's own name (his words, 2026-10-06: "we should be able to rename armor stands"), apart from the loadout's: a
    /// key of the pack's in the stand's record, as the mark's are, so friends with the pack see it too and a game without it
    /// ignores it. Plain text, at most <see cref="MaxName"/> characters; empty for a stand never named (the game's own name
    /// then). Written only by the stand's owner, in a claim's frame; taken off by writing "".
    /// </summary>
    internal static class StandTitle
    {
        internal const string Key = "modprojects.outfitter.standtitle";

        internal const int MaxName = 40;

        /// <summary>The stand's name, plain; empty when it has none.</summary>
        internal static string Read(IStand stand) =>
            stand == null || !stand.Valid ? string.Empty : Text.Plain(stand.GetString(Key) ?? string.Empty, MaxName);

        /// <summary>Names the stand (empty: no name); false when it was already so (nothing written).</summary>
        internal static bool Write(IStand stand, string name)
        {
            string plain = Text.Plain(name ?? string.Empty, MaxName);
            if (stand == null || !stand.Valid || Read(stand) == plain)
            {
                return false;
            }
            stand.SetString(Key, plain);
            return true;
        }

        /// <summary>The name to show for a stand: its own, or <paramref name="gameName"/> (the game's).</summary>
        internal static string Shown(IStand stand, string gameName)
        {
            string name = Read(stand);
            return name.Length > 0 ? name : gameName;
        }
    }
}
