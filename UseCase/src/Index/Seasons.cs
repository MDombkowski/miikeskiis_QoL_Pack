using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// The game's seasons (Halloween, Yule …): which recipes and pieces each switches on, and which is on now. Every season
    /// is a SeasonalItemGroup listed on the Player prefab (its private m_seasonalItemGroups); the one on now is the
    /// player's CurrentSeason, the first in season by date (Player.UpdateCurrentSeason). A season has no name of its own
    /// but its asset's, made readable here ("Halloween"). Read once per index.
    /// </summary>
    internal sealed class Seasons
    {
        private static FieldInfo groupsField;

        private readonly List<SeasonalItemGroup> groups = new List<SeasonalItemGroup>();
        private readonly SeasonalItemGroup current;

        private Seasons(SeasonalItemGroup current)
        {
            this.current = current;
        }

        /// <summary>Looks the private list up; without it seasonal things are left out as the game leaves them out.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            groupsField = typeof(Player).GetField("m_seasonalItemGroups", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (groupsField == null || groupsField.FieldType != typeof(List<SeasonalItemGroup>))
            {
                groupsField = null;
                missingOptional.Add("Player.m_seasonalItemGroups (seasonal recipes and pieces are then listed only while in season)");
            }
        }

        internal static Seasons Read()
        {
            Player player = Player.m_localPlayer;
            var seasons = new Seasons(player != null ? player.CurrentSeason : null);
            Player prefab = player;
            if (prefab == null && Game.instance != null && Game.instance.m_playerPrefab != null)
            {
                prefab = Game.instance.m_playerPrefab.GetComponent<Player>();
            }
            if (groupsField != null && prefab != null && groupsField.GetValue(prefab) is List<SeasonalItemGroup> list)
            {
                foreach (SeasonalItemGroup group in list)
                {
                    if (group != null)
                    {
                        seasons.groups.Add(group);
                    }
                }
            }
            return seasons;
        }

        /// <summary>The season a recipe belongs to, readable, or null for none.</summary>
        internal string Of(Recipe recipe)
        {
            foreach (SeasonalItemGroup group in groups)
            {
                if (group.Recipes != null && group.Recipes.Contains(recipe))
                {
                    return Label(group.name);
                }
            }
            return null;
        }

        /// <summary>The season a piece belongs to, readable, or null for none.</summary>
        internal string Of(GameObject piece)
        {
            foreach (SeasonalItemGroup group in groups)
            {
                if (group.Pieces != null && group.Pieces.Contains(piece))
                {
                    return Label(group.name);
                }
            }
            return null;
        }

        internal bool OnNow(Recipe recipe) => current != null && current.Recipes != null && current.Recipes.Contains(recipe);

        internal bool OnNow(GameObject piece) => current != null && current.Pieces != null && current.Pieces.Contains(piece);

        /// <summary>A season asset's name as words: "Halloween" from "SeasonalItemGroup_Halloween" or
        /// "Seasonal_Halloween"; words split at capitals ("MidSummer" → "Mid Summer").</summary>
        internal static string Label(string asset)
        {
            if (string.IsNullOrEmpty(asset))
            {
                return "Seasonal";
            }
            string name = asset;
            foreach (string noise in new[] { "SeasonalItemGroup", "SeasonalItems", "SeasonalGroup", "Seasonal", "Season", "ItemGroup", "Items", "Group" })
            {
                name = name.Replace(noise, " ");
            }
            name = name.Replace('_', ' ').Replace('-', ' ');
            var words = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
                {
                    words.Append(' ');
                }
                words.Append(c);
            }
            string label = string.Join(" ", words.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return label.Length > 0 ? label : "Seasonal";
        }
    }
}
