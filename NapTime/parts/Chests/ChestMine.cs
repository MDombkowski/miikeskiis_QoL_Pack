// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>
    /// The "mine" mark of StoreStash's personal chests (card A43): a chest a player marked as theirs (Left Alt + H). It lives
    /// in the chest's own network record, like Reserve Stock's mark (ChestReserve), so the unmodded server keeps it with the
    /// world and every player nearby receives it: the character who marked it, by player ID (per character, as the game's
    /// own Personal Chest is), and that character's name, for other players' hover text. The chest service reads it where
    /// a quick stack routes (StackRouting.Rank): a chest someone else marked takes nothing from anyone's quick stack while
    /// the caller's rules say so (StackRules.LeaveOthersMarked), in every game on the pack. StoreStash writes it, always as
    /// the chest's owner (StoreStash\src\Mine.cs); an unmarked chest holds 0 and "" (a value removed from a record isn't sent
    /// to the other players, so it's never removed). What other players may do at a marked chest is ChestAccess (card A80).
    ///
    /// Which chests are a player's personal ones (card A80): the game's Personal Chests he built, chests marked as his, and
    /// (a choice) every chest he built, never one someone else marked (IsPersonal). StoreStash's personal quick stack fills
    /// them, as its settings "Which chests are yours" say, and Craft from Chests takes from them first; a module can't use
    /// StoreStash's code, so the rule is here, and in the bundle StoreStash hands its settings over (KindsFrom).
    /// </summary>
    internal static class ChestMine
    {
        /// <summary>The mark's name in the chest's record: the player ID of the character who marked the chest as theirs, 0
        /// when unmarked. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.qolmods.mine";

        /// <summary>The name of the character who marked it, "" when unmarked. Permanent, once written anywhere.</summary>
        internal const string NameKey = "modprojects.qolmods.minename";

        /// <summary>The longest name shown: what's left of another game's text after Plain.</summary>
        internal const int NameLength = 40;

        /// <summary>The player ID of the character who built a chest (its piece's creator: a cart's or ship's chest sits
        /// below the piece), 0 if none. Only the scratch test harness sets another: it has no engine to ask about pieces.</summary>
        internal static Func<Container, long> CreatorOf = chest =>
        {
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null ? piece.GetCreator() : 0L;
        };

        /// <summary>Which kinds of chest count as a player's personal ones, as the module that offers the choice says now
        /// (StoreStash's "Which chests are yours", which it hands over in the bundle, where every module shares this copy of
        /// the part); null: the defaults (PersonalKinds.Default), as in a single release of a module without StoreStash.
        /// Set only by StoreStash, so another mod's single release never sets it: the compiler's warning is silenced, as
        /// ChestRules' are.</summary>
#pragma warning disable CS0649
        internal static Func<PersonalKinds> KindsFrom;
#pragma warning restore CS0649

        /// <summary>The kinds of personal chest as the settings say now (KindsFrom, else the defaults).</summary>
        internal static PersonalKinds Kinds
        {
            get
            {
                Func<PersonalKinds> from = KindsFrom;
                if (from == null)
                {
                    return PersonalKinds.Default;
                }
                try
                {
                    return from();
                }
                catch (Exception)
                {
                    return PersonalKinds.Default;   // a module's settings not bound yet: the defaults
                }
            }
        }

        /// <summary>The player ID of the character who marked this chest as theirs, or 0: unmarked, or not loaded.</summary>
        internal static long MarkOf(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() ? view.GetZDO().GetLong(Key) : 0L;
        }

        /// <summary>The name of the character who marked this chest, as they were called then; "" if unmarked or not known.</summary>
        internal static string MarkerName(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() ? view.GetZDO().GetString(NameKey) : string.Empty;
        }

        /// <summary>Marked as his by this character (<paramref name="playerId"/>).</summary>
        internal static bool IsMine(Container chest, long playerId) => playerId != 0L && MarkOf(chest) == playerId;

        /// <summary>Marked as theirs by any other character (another player's, or another character of his).</summary>
        internal static bool MarkedByOther(Container chest, long playerId)
        {
            long mark = MarkOf(chest);
            return mark != 0L && mark != playerId;
        }

        /// <summary>
        /// Whether this chest is one of the personal chests of character <paramref name="me"/>, as <paramref name="kinds"/>
        /// say: marked as his; the game's own Personal Chest (private) he built; with Built, any chest he built. Never a
        /// chest someone else marked as theirs, whoever built it. <paramref name="creatorOf"/> says who built a chest (null:
        /// CreatorOf), asked only when the answer needs it.
        /// </summary>
        internal static bool IsPersonal(Container chest, long me, PersonalKinds kinds, Func<Container, long> creatorOf = null)
        {
            if (chest == null || me == 0L)
            {
                return false;
            }
            long mark = MarkOf(chest);
            if (mark != 0L && mark != me)
            {
                return false;
            }
            if (kinds.Marked && mark == me)
            {
                return true;
            }
            if (!kinds.Built && !kinds.GameChests)
            {
                return false;
            }
            if ((creatorOf ?? CreatorOf)(chest) != me)
            {
                return false;
            }
            return kinds.Built || chest.m_privacy == Container.PrivacySetting.Private;
        }

        /// <summary>The chests in the order given, those <paramref name="personal"/> says yes to first: a stable split, so
        /// each half keeps the order it had (nearest first). Craft from Chests' "My personal chests first" (card A80).</summary>
        internal static List<Container> PersonalFirst(IEnumerable<Container> chests, Func<Container, bool> personal)
        {
            var first = new List<Container>();
            var rest = new List<Container>();
            foreach (Container chest in chests)
            {
                (personal(chest) ? first : rest).Add(chest);
            }
            first.AddRange(rest);
            return first;
        }

        /// <summary>Whose a chest someone else marked is, as a hover line and a message say it: "Ada's", or "Someone
        /// else's" ("someone else's" inside a sentence) when no name came with the mark.</summary>
        internal static string Whose(Container chest, bool capital = true)
        {
            string name = Plain(MarkerName(chest));
            return name.Length > 0 ? $"{name}'s" : capital ? "Someone else's" : "someone else's";
        }

        /// <summary>
        /// A name as plain text, whatever another game wrote into the record: only letters, digits, spaces and ' - _ . are
        /// kept. So no rich-text tag of the game's labels ("&lt;" and "&gt;"), no "\" (its text turns "\&lt;" into "&lt;"
        /// before it reads tags, and "\n" into a line break), no "$" (the game's messages translate "$words"), no line or
        /// paragraph separator, no invisible formatting character (one turns the text after it right to left), no emoji half
        /// left by the cut to 40 characters.
        /// </summary>
        internal static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            var plain = new System.Text.StringBuilder(name.Length);
            foreach (char each in name)
            {
                if (char.IsLetterOrDigit(each) || each == ' ' || each == '\'' || each == '-' || each == '_' || each == '.')
                {
                    plain.Append(each);
                }
            }
            string text = plain.ToString().Trim();
            return text.Length > NameLength ? text.Substring(0, NameLength) : text;
        }
    }

    /// <summary>Which kinds of chest count as a player's personal ones (ChestMine.IsPersonal): StoreStash's "Which chests
    /// are yours".</summary>
    internal readonly struct PersonalKinds
    {
        /// <summary>The game's own Personal Chests he built (only their builder can open them).</summary>
        internal readonly bool GameChests;

        /// <summary>Chests marked as his (the "mine" mark).</summary>
        internal readonly bool Marked;

        /// <summary>Every chest, cart and ship he built, unless someone else marked it.</summary>
        internal readonly bool Built;

        internal PersonalKinds(bool gameChests, bool marked, bool built)
        {
            GameChests = gameChests;
            Marked = marked;
            Built = built;
        }

        /// <summary>StoreStash's defaults: his Personal Chests and the chests marked as his.</summary>
        internal static PersonalKinds Default => new PersonalKinds(true, true, false);
    }
}
