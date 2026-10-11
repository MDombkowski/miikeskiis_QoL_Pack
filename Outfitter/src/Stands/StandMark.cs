using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// A stand's mark, in its own record under the pack's keys (as Reserve Stock marks a chest): which loadout it holds (its
    /// Id), who saved it (player ID and name), the loadout's name, and the loadout itself as a share code (a list, never an
    /// item: for a friend's game, and for his own when the loadout has left his list). The unmodded server keeps it; a game
    /// without the pack ignores it. Written only by the stand's owner, in a claim's frame; taken off by writing "" and 0, never
    /// a removal (a removal reaches no other player). Text another game wrote is read through Outfitter's plain filter.
    /// </summary>
    internal sealed class StandMark
    {
        internal const string IdKey = "modprojects.outfitter.stand";
        internal const string ByKey = "modprojects.outfitter.standby";
        internal const string ByNameKey = "modprojects.outfitter.standbyname";
        internal const string NameKey = "modprojects.outfitter.standname";
        internal const string CodeKey = "modprojects.outfitter.standcode";

        internal string Id = string.Empty;
        internal long By;
        internal string ByName = string.Empty;
        internal string Name = string.Empty;
        internal string Code = string.Empty;

        /// <summary>The stand holds a loadout (its Id is one, as Outfitter makes them).</summary>
        internal bool Marked => Loadout.IsId(Id);

        /// <summary>Whether this is the same mark as <paramref name="other"/>: both unmarked, or the same loadout saved by
        /// the same player (its name and code may have been brought up to date meanwhile).</summary>
        internal bool SameAs(StandMark other) => other != null && Marked == other.Marked && (!Marked || (Id == other.Id && By == other.By));

        /// <summary>The mark in a stand's record; an unmarked one for anything that isn't a mark.</summary>
        internal static StandMark Read(IStand stand)
        {
            var mark = new StandMark();
            if (stand == null || !stand.Valid)
            {
                return mark;
            }
            string id = stand.GetString(IdKey);
            if (!Loadout.IsId(id))
            {
                return mark;
            }
            mark.Id = id;
            mark.By = stand.GetLong(ByKey);
            mark.ByName = Text.Plain(stand.GetString(ByNameKey), Loadout.MaxName);
            mark.Name = Text.Plain(stand.GetString(NameKey), Loadout.MaxName);
            string code = stand.GetString(CodeKey) ?? string.Empty;
            mark.Code = code.Length <= ShareCode.MaxCode ? code : string.Empty;
            return mark;
        }

        /// <summary>The mark for one of his loadouts: its Id, him, its name, and its code (left out when longer than a code
        /// may be read).</summary>
        internal static StandMark For(Loadout loadout, long by, string byName)
        {
            string code = ShareCode.Write(loadout, places: true);
            return new StandMark
            {
                Id = loadout.Id,
                By = by,
                ByName = Text.Plain(byName, Loadout.MaxName),
                Name = Text.Plain(loadout.Name, Loadout.MaxName),
                Code = code.Length <= ShareCode.MaxCode ? code : string.Empty,
            };
        }

        /// <summary>Writes it into the stand's record (as its owner: the caller's claim); true when anything changed.</summary>
        internal static bool Write(IStand stand, StandMark mark)
        {
            StandMark now = Read(stand);
            if (now.Id == mark.Id && now.By == mark.By && now.ByName == mark.ByName && now.Name == mark.Name && now.Code == mark.Code &&
                stand.GetString(IdKey) == mark.Id)
            {
                return false;
            }
            stand.SetString(IdKey, mark.Id);
            stand.SetLong(ByKey, mark.By);
            stand.SetString(ByNameKey, mark.ByName);
            stand.SetString(NameKey, mark.Name);
            stand.SetString(CodeKey, mark.Code);
            return true;
        }

        /// <summary>Takes the mark off: an ordinary stand again, whatever it holds staying on it.</summary>
        internal static bool Clear(IStand stand) => Write(stand, new StandMark());
    }
}
