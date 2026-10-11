using System.Collections.Generic;
using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// Which loadout a marked stand holds (the plan's "Which loadout a stand holds"): his own (the mark's saver is his
    /// character) by its Id in his list (Outfitter's Store), the newest; otherwise, and for a stand someone else saved, the
    /// share code kept in the stand's record, read as a friend's code is (Codes: a list, never an item; what this game lacks
    /// stays a line), with the mark's Id, name and saver. Read codes are kept by their text, so a stand pointed at every frame
    /// reads its code once.
    /// </summary>
    internal static class StandLoadouts
    {
        private const int CacheSize = 64;

        private static readonly Dictionary<string, Loadout> Read = new Dictionary<string, Loadout>();

        /// <summary>Reads a code into a loadout (Codes.Read in play; the harness sets another).</summary>
        internal static System.Func<string, Loadout> FromCode = code => Codes.Read(code, out _);

        /// <summary>The loadout a mark points at, or null; <paramref name="mine"/> says whether he saved it (this
        /// character).</summary>
        internal static Loadout Of(StandMark mark, long me, out bool mine)
        {
            mine = mark != null && mark.Marked && me != 0 && mark.By == me;
            if (mark == null || !mark.Marked)
            {
                return null;
            }
            if (mine)
            {
                Loadout own = Store.Find(mark.Id);
                if (own != null)
                {
                    return own;
                }
            }
            if (mark.Code.Length == 0)
            {
                return null;
            }
            string key = mark.Id + "|" + mark.By + "|" + mark.Name + "|" + mark.Code;
            if (!Read.TryGetValue(key, out Loadout loadout))
            {
                Loadout fromCode = FromCode(mark.Code);
                if (fromCode != null)
                {
                    fromCode.Id = mark.Id;
                    fromCode.Name = mark.Name.Length > 0 ? mark.Name : fromCode.Name;
                    fromCode.SavedBy = mark.By;
                    fromCode.SavedByName = mark.ByName;
                }
                if (Read.Count >= CacheSize)
                {
                    Read.Clear();
                }
                Read[key] = loadout = fromCode;
            }
            return loadout;
        }

        /// <summary>Forgets the codes read (the module's undo step, and the harness).</summary>
        internal static void Forget() => Read.Clear();
    }
}
