using System.Collections.Generic;
using System.Text;
using Outfitter.Equip;
using Outfitter.Loadouts;

namespace Outfitter.Stands
{
    /// <summary>
    /// The lines a marked stand adds to its hover text (card A46's "Hover lines on a marked stand", each a switch): the
    /// loadout's name (and whose, when not his), what the key does there now (put it on, or hang it back), and what it lacks
    /// (the loadout's pieces neither on the stand nor on him). Since 0.4.0 (job 30, Session 3): whose personal stand it is
    /// ("Personal: yours"), and what it's drawn wearing that isn't on it ("Shown, not on it: ..."); someone else's personal
    /// stand offers none of his keys. An unmarked stand adds nothing but whose personal stand it is, unless "Show the save key
    /// on every stand" is on. Added after every other mod's hover text (MyLittleUI rewrites the stand's), worked out at most a
    /// few times a second per stand.
    /// </summary>
    internal static class StandHover
    {
        private const float Keep = 0.3f;

        private sealed class Lines
        {
            internal string Text;
            internal float Until;
        }

        private static readonly Dictionary<object, Lines> Made = new Dictionary<object, Lines>();

        /// <summary>The hover text with the stand's lines added, and, with "A stand's name replaces "Armor stand"", its own
        /// name in place of the game's, where the text holds it: <paramref name="shownName"/> (what the hover shows for it),
        /// else the text of <paramref name="slot"/> (the slot pointed at, localized only for a stand with a name of its
        /// own), else the stand's game name.</summary>
        internal static string Add(IStand stand, string text, string shownName = null, Switch slot = null)
        {
            if (stand == null || !stand.Valid)
            {
                return text;
            }
            float now = Stands.Clock();
            if (!Made.TryGetValue(stand.Key, out Lines lines) || now > lines.Until)
            {
                if (Made.Count > 64)
                {
                    Made.Clear();
                }
                Made[stand.Key] = lines = new Lines { Text = Make(stand), Until = now + Keep };
            }
            text = Renamed(stand, text, shownName, slot);
            return lines.Text.Length == 0 ? text : text + lines.Text;
        }

        // The stand's own name in place of the game's, the first time the text holds it; the text as it is otherwise. Its
        // name is read at every look (a friend's rename shows at once); only a named stand's slot text is localized.
        private static string Renamed(IStand stand, string text, string shownName, Switch slot)
        {
            if (!StandSettings.ShowName.Value || string.IsNullOrEmpty(text))
            {
                return text;
            }
            string own = StandTitle.Read(stand);
            if (own.Length == 0)
            {
                return text;
            }
            if (shownName == null && slot != null && Localization.instance != null && !string.IsNullOrEmpty(slot.m_hoverText))
            {
                shownName = Localization.instance.Localize(slot.m_hoverText);
            }
            return Swapped(text, shownName, own) ?? Swapped(text, Stands.GameName(stand), own) ?? text;
        }

        // The text with the first "name" in it replaced by "own"; null when it doesn't hold the name.
        private static string Swapped(string text, string name, string own)
        {
            int at = string.IsNullOrEmpty(name) ? -1 : text.IndexOf(name, System.StringComparison.Ordinal);
            return at < 0 ? null : text.Substring(0, at) + own + text.Substring(at + name.Length);
        }

        /// <summary>The lines for a stand now, each starting with a line break; empty for none.</summary>
        internal static string Make(IStand stand)
        {
            var text = new StringBuilder();
            StandMark mark = StandMark.Read(stand);
            bool blocked = StandPersonal.Blocks(stand);
            if (!mark.Marked)
            {
                Personal(stand, text);
                // Never on someone else's personal stand: his save key leaves it alone (job 30, Session 3).
                if (StandSettings.HoverEveryStand.Value && StandSettings.SaveKey.Value.MainKey != UnityEngine.KeyCode.None && !blocked &&
                    stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: false))
                {
                    text.Append($"\n[<color=yellow><b>{StandInput.Text(StandSettings.SaveKey.Value)}</b></color>] Save what you carry and wear to it");
                }
                return text.ToString();
            }
            Loadout loadout = StandLoadouts.Of(mark, Stands.Me(), out bool mine);
            string name = loadout != null ? loadout.Name : mark.Name;
            if (StandSettings.HoverName.Value)
            {
                string whose = mine ? string.Empty : mark.ByName.Length > 0 ? $" ({mark.ByName}'s)" : " (another player's)";
                text.Append($"\n<color=orange>Loadout: {name}</color>{whose}");
            }
            Personal(stand, text);
            IWearer wearer = Outfit.WearerOf();
            if (loadout == null || wearer == null || (!mine && StandSettings.Others.Value == OthersStands.LeaveThemAlone) || blocked ||
                !stand.MayUse(StandSettings.Which.Value == WhichStands.OnlyOnesIBuilt, flash: false))
            {
                Shown(stand, text, new HashSet<string>());
                return text.ToString();   // nothing his key could do here: only the name (review round 1, n1)
            }
            if (loadout.Items.Count == 0)
            {
                // An empty loadout: the key refuses (review round 1 of Session 2, m2), so the hover offers nothing and says
                // why (the harness's S3-hover-offers-empty-put-on).
                if (StandSettings.HoverKeys.Value)
                {
                    text.Append($"\nEmpty: fill it in Outfitter's window ({StandInput.Text(Settings.WindowKey.Value)})");
                }
                return text.ToString();
            }
            EquipRules rules = StandJob.Rules();
            List<StandPiece> pieces = StandPlan.Pieces(loadout, stand);
            if (StandSettings.HoverKeys.Value && StandSettings.EquipKey.Value.MainKey != UnityEngine.KeyCode.None)
            {
                bool back = StandPlan.Decide(stand, wearer, pieces, rules) == StandAction.HangBack;
                text.Append($"\n[<color=yellow><b>{StandInput.Text(StandSettings.EquipKey.Value)}</b></color>] {(back ? "Hang it back" : "Put it on")}");
            }
            var notHere = new HashSet<string>();
            if (StandSettings.HoverLacks.Value)
            {
                List<StandPiece> lacks = StandPlan.Lacks(stand, wearer, pieces, rules);
                for (int i = 0; i < lacks.Count && i < 3; i++)
                {
                    notHere.Add(Messages.NameOf(lacks[i].Item));   // the names it shows: one in its "and N more" is named on the Shown line (round 2, n6)
                }
                if (lacks.Count > 0)
                {
                    var names = new List<string>();
                    for (int i = 0; i < lacks.Count && i < 3; i++)
                    {
                        names.Add(Messages.NameOf(lacks[i].Item));
                    }
                    if (lacks.Count > 3)
                    {
                        names.Add($"and {lacks.Count - 3} more");
                    }
                    text.Append($"\nNot here: {string.Join(", ", names)}");
                }
            }
            Shown(stand, text, notHere);
            return text.ToString();
        }

        // "Personal: yours" or whose ("Hover: whose personal stand", job 30's Session 3).
        private static void Personal(IStand stand, StringBuilder text)
        {
            if (!StandSettings.HoverPersonal.Value)
            {
                return;
            }
            (long whose, string name) = StandPersonal.Read(stand);
            if (whose == 0L)
            {
                return;
            }
            text.Append(whose == Stands.Me() ? "\nPersonal: yours" : $"\nPersonal: {(name.Length > 0 ? name + "'s" : "another player's")}");
        }

        // What the stand is drawn wearing that isn't on it ("Show what a stand's loadout lacks on it", StandShow), up to
        // three names: so a shown piece is never mistaken for one E would take. A piece "Not here" names already isn't
        // named again (review round 1 of Session 3, j4).
        private static void Shown(IStand stand, StringBuilder text, HashSet<string> notHere)
        {
            Dictionary<int, LoadoutItem> shown = StandShow.Missing(stand);
            if (shown.Count == 0)
            {
                return;
            }
            var places = new List<int>();
            foreach (KeyValuePair<int, LoadoutItem> place in shown)
            {
                if (!notHere.Contains(Messages.NameOf(place.Value)))
                {
                    places.Add(place.Key);
                }
            }
            if (places.Count == 0)
            {
                return;
            }
            places.Sort();
            var names = new List<string>();
            for (int i = 0; i < places.Count && i < 3; i++)
            {
                names.Add(Messages.NameOf(shown[places[i]]));
            }
            if (places.Count > 3)
            {
                names.Add($"and {places.Count - 3} more");
            }
            text.Append($"\nShown, not on it: {string.Join(", ", names)}");
        }

        /// <summary>Forgets the lines made (after a change, and the undo step).</summary>
        internal static void Forget() => Made.Clear();
    }
}
