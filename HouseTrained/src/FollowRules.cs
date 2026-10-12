namespace HouseTrained
{
    /// <summary>
    /// Following (card B25), the rules on plain values, so the harness checks them: which tames E commands as it commands a
    /// wolf, what its hover text says, and which tames are blood magic's summons.
    /// </summary>
    internal static class FollowRules
    {
        /// <summary>A blood magic summon (the Dead Raiser's skeletons, the spirit callers' animals): the game sends it away
        /// when it's left behind, when its summoner logs out, or it raises its summoner's skill (Tameable's
        /// m_unsummonDistance, m_unsummonOnOwnerLogoutSeconds, m_levelUpOwnerSkill; every summon has all three, no tame
        /// any, by the installed game's prefabs).</summary>
        internal static bool IsSummon(float unsummonDistance, float unsummonOnLogout, bool raisesSkill) =>
            unsummonDistance > 0f || unsummonOnLogout > 0f || raisesSkill;

        /// <summary>Whether this press of E commands the tame (follow or stay) where the game itself would only pet it:
        /// the part on, a tame with the game's monster AI that the game doesn't command (only wolves are commandable),
        /// chosen by "Which tames", and a summon only with "The staffs' summons too" on.</summary>
        internal static bool Commands(bool on, bool tamed, bool hasMonsterAi, bool commandable, bool chosen, bool summon, bool summonsToo) =>
            on && tamed && hasMonsterAi && !commandable && chosen && (!summon || summonsToo);

        /// <summary>Whether this press of E makes a tame that follows stay where the part wouldn't command it (switched off,
        /// or the tame not chosen): a tame the game doesn't command, left following, could otherwise never be told to stay
        /// (research A section 2). Never a summon: they follow by the staff's own command, and one made to stay here
        /// could be told to follow again only by the part, chosen (code review 1, F1; the last look).</summary>
        internal static bool Undoes(bool moduleOn, bool tamed, bool hasMonsterAi, bool commandable, bool following, bool summon) =>
            moduleOn && tamed && hasMonsterAi && !commandable && following && !summon;

        /// <summary>Whether a patrol point is a "stay" a player gave (kept, whatever its distance), rather than a spawner's
        /// (which "Ignore a far-away spawn point" may ignore): the game's commandable tames (wolves), and any tame this part
        /// told to stay (its mark, written where the game's command ran).</summary>
        internal static bool ToldToStay(bool commandable, bool stayMark) => commandable || stayMark;

        /// <summary>Whether E on this tame follows and stays, by the game or by this part: what its hover text then names.</summary>
        internal static bool Follows(bool commandedByGame, bool commandedHere) => commandedByGame || commandedHere;

        /// <summary>The word E's line of a tame's hover text shows: Stay while it follows someone, else Follow.</summary>
        internal static string Word(bool following) => following ? "Stay" : "Follow";

        /// <summary>
        /// A tame's hover text with E's word changed: the first "] Pet" (the game's "[E] Pet" line, its key in the game's
        /// own colour tags, its word localized) becomes "] Follow" or "] Stay". The text unchanged when the line isn't
        /// there (another mod rewrote it), or the word is empty.
        /// </summary>
        internal static string Reworded(string hover, string petWord, string word)
        {
            if (string.IsNullOrEmpty(hover) || string.IsNullOrEmpty(petWord) || string.IsNullOrEmpty(word))
            {
                return hover;
            }
            string find = "] " + petWord;
            int at = hover.IndexOf(find, System.StringComparison.Ordinal);
            if (at < 0)
            {
                return hover;
            }
            int end = at + find.Length;
            // Only the whole word: "] Pet" and not "] Petting" (another language's longer word).
            if (end < hover.Length && char.IsLetter(hover[end]))
            {
                return hover;
            }
            return hover.Substring(0, at) + "] " + word + hover.Substring(end);
        }

        /// <summary>A line of a hover text naming a key and what it does, in the game's own look: "[&lt;key&gt;] what".</summary>
        internal static string KeyLine(string key, string what) => $"\n[<color=yellow><b>{key}</b></color>] {what}";
    }
}
