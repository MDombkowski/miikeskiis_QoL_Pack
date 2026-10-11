using System.Collections.Generic;

namespace AFKRaidBlocker
{
    /// <summary>
    /// What a player's report to the server says while he is away, and how it's put back.
    ///
    /// Each player's game keeps a small report for the server (ZNet.m_serverSyncedPlayerData): among other things its
    /// base value ("baseValue": how many base pieces such as workbenches, fires and beds are within 20 m of him) and the
    /// raids he is ready for ("possibleEvents"). The server reads it before every raid roll and only centres a raid that
    /// needs a base on a player whose base value is 3 or more; a player whose report lacks either value is left out of
    /// every random event (RandEventSystem.RetrievePlayerEventData). So, only while the report is sent or read:
    /// - raids that need a base: the base value reads 0;
    /// - every random event: the list of raids he is ready for is left out.
    /// The report itself is changed only for that moment and put back at once, so nothing else sees a difference: his
    /// game's own base value (Player.m_baseValue, which "safe in home" reads) and the copy on his character's record
    /// that other games read are never touched.
    /// </summary>
    internal sealed class ReportMask
    {
        internal const string BaseValueKey = "baseValue";       // Player.BaseValueKey
        internal const string ReadyEventsKey = "possibleEvents"; // RandEventSystem.PossibleEventsKey

        /// <summary>The value the base value reads while held off.</summary>
        internal const string NoBase = "0";

        private readonly List<KeyValuePair<string, string>> before = new List<KeyValuePair<string, string>>();

        /// <summary>Whether anything was changed (and so will be put back).</summary>
        internal bool Changed => before.Count > 0;

        /// <summary>Changes the report for what's held off, remembering each value before it changes it, so that
        /// Restore can put back exactly what was there even if this stops part way.</summary>
        internal void Apply(IDictionary<string, string> report, HeldOff what)
        {
            if (what == HeldOff.EveryRandomEvent)
            {
                if (report.TryGetValue(ReadyEventsKey, out string ready))
                {
                    before.Add(new KeyValuePair<string, string>(ReadyEventsKey, ready));
                    report.Remove(ReadyEventsKey);
                }
                return;
            }
            if (report.TryGetValue(BaseValueKey, out string value) && value != NoBase)
            {
                before.Add(new KeyValuePair<string, string>(BaseValueKey, value));
                report[BaseValueKey] = NoBase;
            }
        }

        /// <summary>Puts back every value Apply changed, last first, and forgets them.</summary>
        internal void Restore(IDictionary<string, string> report)
        {
            for (int i = before.Count - 1; i >= 0; i--)
            {
                report[before[i].Key] = before[i].Value;
            }
            before.Clear();
        }

        /// <summary>The base value and ready list as the server would take them from a report, after the hold: the
        /// game's own test (RetrievePlayerEventData), for the host's own report. False: left out of every event.</summary>
        internal static bool Held(bool found, ref int baseValue, HeldOff what)
        {
            if (!found)
            {
                return false;
            }
            if (what == HeldOff.EveryRandomEvent)
            {
                return false;
            }
            baseValue = 0;
            return true;
        }
    }
}
