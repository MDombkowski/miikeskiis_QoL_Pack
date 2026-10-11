using System.Collections.Generic;
using System.Text;
using Outfitter.Loadouts;

namespace Outfitter.Equip
{
    /// <summary>
    /// What Outfitter says about an equip, in plain words: one line at the top left ("Result message": one line, or a line
    /// for each part, also in the log), the missing-items question, and the confirm question. Item names are the game's
    /// own, through its localisation; a line read from a code whose item this game lacks says its prefab, plain.
    /// </summary>
    internal static class Messages
    {
        /// <summary>The game's name for an item ($ token localised) in the player's language; the harness sets another.</summary>
        internal static System.Func<string, string> Localize = token => Localization.instance != null ? Localization.instance.Localize(token) : token;

        /// <summary>A loadout item's name as shown; a food slot's, its rule in few words ("Food: Stamina 20 to 50, most
        /// Stamina").</summary>
        internal static string NameOf(LoadoutItem item)
        {
            if (item.Food != null)
            {
                return "Food: " + item.Food.Describe(FoodName);
            }
            ItemDrop.ItemData template = Codes.Template(item.Prefab);
            if (template == null)
            {
                return Text.Plain(item.Prefab, 64);
            }
            string name = Localize(template.m_shared.m_name);
            return template.m_shared.m_maxQuality > 1 && Matching.Single(template) ? $"{name} (quality {item.Quality})" : name;
        }

        /// <summary>A food's name by its prefab, as shown: the game's own, or the prefab, plain, for one this game lacks.</summary>
        internal static string FoodName(string prefab)
        {
            ItemDrop.ItemData template = Codes.Template(prefab);
            return template != null ? Localize(template.m_shared.m_name) : Text.Plain(prefab, 64);
        }

        internal static string Line(EquipRequest request, EquipResult result)
        {
            string loadout = request.Loadout != null ? request.Loadout.Name : "the loadout";
            if (result.Cancelled)
            {
                return $"{loadout}: not put on, {result.WhyCancelled}.";
            }
            var parts = new List<string>();
            if (result.Fetched > 0)
            {
                parts.Add($"{result.Fetched} from chests");
            }
            if (result.PutAway > 0)
            {
                parts.Add($"{result.PutAway} put away");
            }
            int missing = 0;
            foreach (MissingItem item in result.Missing)
            {
                missing += item.Count;
            }
            if (missing > 0)
            {
                parts.Add($"{missing} missing ({Few(result.Missing, 2)})");
            }
            if (result.StayedWithHim.Count > 0)
            {
                parts.Add($"{result.StayedWithHim.Count} stayed with you (no chest took them)");
            }
            if (result.NotPutOn.Count > 0)
            {
                parts.Add($"{result.NotPutOn.Count} not put on");
            }
            if (result.ChestsInUse > 0)
            {
                parts.Add($"{result.ChestsInUse} chest{(result.ChestsInUse == 1 ? " was" : "s were")} in use");
            }
            if (result.ChestsNoAnswer > 0)
            {
                parts.Add($"{result.ChestsNoAnswer} chest{(result.ChestsNoAnswer == 1 ? "" : "s")} didn't answer");
            }
            return parts.Count == 0 ? $"{loadout} on." : $"{loadout} on: {string.Join(", ", parts)}.";
        }

        /// <summary>A few missing items by name, "and n more" for the rest.</summary>
        internal static string Few(List<MissingItem> missing, int show)
        {
            var names = new List<string>();
            for (int i = 0; i < missing.Count && i < show; i++)
            {
                names.Add($"{NameOf(missing[i].Item)}{(missing[i].Count > 1 ? " x" + missing[i].Count : string.Empty)}: {missing[i].Why}");
            }
            if (missing.Count > show)
            {
                names.Add($"and {missing.Count - show} more");
            }
            return string.Join("; ", names);
        }

        /// <summary>What a plan lacks, in words.</summary>
        internal static string MissingWords(EquipPlan plan, int show)
        {
            var missing = new List<MissingItem>();
            foreach (Want want in plan.Wants)
            {
                if (want.Missing > 0)
                {
                    missing.Add(new MissingItem { Item = want.First, Count = want.Missing, Why = want.Why });
                }
            }
            return missing.Count == 0 ? "nothing is missing" : "missing " + Few(missing, show);
        }

        internal static string MissingQuestion(Loadout loadout, EquipPlan plan, EquipMode mode)
        {
            var text = new StringBuilder();
            text.Append($"Some of {loadout.Name} isn't in the chests nearby:\n");
            int away = plan.Aways.Count;
            int shown = 0;
            foreach (Want want in plan.Wants)
            {
                if (want.Missing <= 0)
                {
                    continue;
                }
                if (shown++ == 8)
                {
                    text.Append("...\n");
                    break;
                }
                text.Append($"- {NameOf(want.First)}{(want.Missing > 1 ? " x" + want.Missing : string.Empty)}: {want.Why}\n");
            }
            if (mode == EquipMode.FullyReplace && away > 0)
            {
                // One yes puts away what isn't in the loadout: say how much (R1, finding 24; R2, finding 11).
                text.Append($"{Stacks(plan, away)} of what you carry go{(away == 1 ? "es" : "")} into the chests (what none has room for stays with you).\n");
            }
            if (plan.CellsShort > 0)
            {
                text.Append($"Your inventory is {plan.CellsShort} cell{(plan.CellsShort == 1 ? "" : "s")} short of room for all of it.\n");
            }
            text.Append("Equip what's here? (Never ask: F1, Outfitter, Equipping, \"Missing items\": EquipWhatsHere.)");
            return text.ToString();
        }

        internal static string ConfirmQuestion(Loadout loadout, EquipPlan plan)
        {
            int away = plan.Aways.Count;
            int fetch = plan.FetchCount;
            return $"Put {loadout.Name} on? {fetch} item{(fetch == 1 ? " comes" : "s come")} from chests, " +
                   $"{Stacks(plan, away)} of yours go{(away == 1 ? "es" : "")} away.";
        }

        // What goes away, counted: items when each is a single one (gear), else stacks.
        private static string Stacks(EquipPlan plan, int count)
        {
            bool single = plan.Aways.TrueForAll(goes => Matching.Single(goes.Item));
            string what = single ? "item" : "stack";
            return count == 1 ? $"1 {what}" : $"{count} {what}s";
        }

        /// <summary>Shows a result: one line at the top left, or, with "Result message" Detailed, a line for each part,
        /// also in the log.</summary>
        internal static void Show(EquipResult result)
        {
            Player player = Player.m_localPlayer;
            if (player == null || string.IsNullOrEmpty(result.Line))
            {
                return;
            }
            player.Message(MessageHud.MessageType.TopLeft, result.Line);
            if (Settings.Result.Value != ResultMessage.Detailed)
            {
                return;
            }
            Module.Log.LogInfo(result.Line);
            foreach (MissingItem item in result.Missing)
            {
                string line = $"Missing: {NameOf(item.Item)}{(item.Count > 1 ? " x" + item.Count : string.Empty)} ({item.Why}).";
                player.Message(MessageHud.MessageType.TopLeft, line);
                Module.Log.LogInfo(line);
            }
            foreach (string line in result.NotPutOn)
            {
                player.Message(MessageHud.MessageType.TopLeft, "Not put on: " + line);
                Module.Log.LogInfo("Not put on: " + line);
            }
        }
    }
}
