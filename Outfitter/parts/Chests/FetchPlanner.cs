// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>
    /// Plans a fetch before anything moves: which chest gives how much of what. Plain arithmetic, with no game objects,
    /// so it can be tested outside the game.
    /// </summary>
    internal static class FetchPlanner
    {
        internal readonly struct Take
        {
            internal readonly int Source;
            internal readonly string Item;
            internal readonly int Amount;

            internal Take(int source, string item, int amount)
            {
                Source = source;
                Item = item;
                Amount = amount;
            }
        }

        /// <summary>
        /// The plan for items named by key (ChestStock): an item wanted at one level ("name@quality") and the same item
        /// wanted by name (any level) draw on the same stacks, so the levels are planned first, and each name is then
        /// planned against what they leave in each source. The takes come in that order, levels first, so a take by name
        /// can't use up the level a Take line wanted.
        /// </summary>
        internal static List<Take> PlanLevelsFirst(IDictionary<string, int> wanted, IList<Dictionary<string, int>> sources)
        {
            var levels = new Dictionary<string, int>();
            var names = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> want in wanted)
            {
                if (want.Value > 0)
                {
                    ChestStock.Parse(want.Key, out _, out int quality);
                    (quality >= 0 ? levels : names)[want.Key] = want.Value;
                }
            }
            List<Take> takes = Plan(levels, sources);
            if (names.Count == 0 || takes.Count == 0)
            {
                takes.AddRange(Plan(names, sources));
                return takes;
            }
            var remaining = new List<Dictionary<string, int>>();
            foreach (Dictionary<string, int> source in sources)
            {
                remaining.Add(new Dictionary<string, int>(source));
            }
            foreach (Take take in takes)
            {
                ChestStock.Parse(take.Item, out string name, out _);
                if (remaining[take.Source].TryGetValue(name, out int has))
                {
                    remaining[take.Source][name] = has > take.Amount ? has - take.Amount : 0;
                }
            }
            takes.AddRange(Plan(names, remaining));
            return takes;
        }

        /// <summary>
        /// For each wanted item, takes from the sources in their order (nearest chest first) until it has enough or they
        /// run out. Never plans more than a source has, nor more than is wanted; a zero or negative amount is ignored.
        /// </summary>
        internal static List<Take> Plan(IDictionary<string, int> wanted, IList<Dictionary<string, int>> sources)
        {
            var takes = new List<Take>();
            foreach (KeyValuePair<string, int> want in wanted)
            {
                int left = want.Value;
                for (int source = 0; source < sources.Count && left > 0; source++)
                {
                    if (sources[source].TryGetValue(want.Key, out int has) && has > 0)
                    {
                        int amount = has < left ? has : left;
                        takes.Add(new Take(source, want.Key, amount));
                        left -= amount;
                    }
                }
            }
            return takes;
        }
    }
}
