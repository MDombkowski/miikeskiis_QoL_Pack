using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace UseCase
{
    /// <summary>
    /// What the local player knows, from the game's own lists (research section 4): one known list for recipes (by what
    /// they make) and pieces (by their $ name), the known materials, the world's global keys and his own unique keys (a
    /// boss beaten sets both), the no-cost cheat and the world's unlock-all keys. Also a cheap stamp that changes whenever
    /// any of it does, so a panel is made again as he learns things.
    /// </summary>
    internal sealed class Knowledge : IKnowledge
    {
        private static AccessTools.FieldRef<Player, HashSet<string>> knownRecipes;
        private static AccessTools.FieldRef<Player, HashSet<string>> knownMaterial;

        private readonly Player player;
        private readonly string character;

        private Knowledge(Player player)
        {
            this.player = player;
            character = player.GetPlayerName();
        }

        /// <summary>Looks up the two private lists, which only the stamp needs: without them a panel is made again at most
        /// once a second instead of the moment he learns something.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            knownRecipes = Field("m_knownRecipes", missingOptional);
            knownMaterial = Field("m_knownMaterial", missingOptional);
        }

        private static Knowledge thisFrame;
        private static int madeAt = -1;

        /// <summary>The local player's, made once a frame; null outside a world.</summary>
        internal static Knowledge ThisFrame()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return null;
            }
            if (thisFrame == null || madeAt != UnityEngine.Time.frameCount || thisFrame.player != player)
            {
                thisFrame = new Knowledge(player);
                madeAt = UnityEngine.Time.frameCount;
            }
            return thisFrame;
        }

        public bool KnowsRecipe(string name) => !string.IsNullOrEmpty(name) && player.IsRecipeKnown(name);

        public bool KnowsMaterial(string name) => !string.IsNullOrEmpty(name) && player.IsKnownMaterial(name);

        public bool HasSeen(string key) => Seen.HasSeen(character, key);

        public bool WorldHas(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            return (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key)) || player.HaveUniqueKey(key);
        }

        public bool GameShowsAllRecipes => player.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.AllRecipesUnlocked));

        public bool GameShowsAllPieces => player.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.AllPiecesUnlocked));

        /// <summary>The character's name, for the seen list.</summary>
        internal string Character => character;

        /// <summary>Changes whenever what he knows could have: his known lists grow, a place is seen, a key is set, the
        /// cheat or an unlock-all key flips. <paramref name="second"/> is the clock's second, for when the lists
        /// couldn't be looked up.</summary>
        internal int Stamp(int second)
        {
            unchecked
            {
                int stamp = Seen.Version * 31;
                stamp = stamp * 31 + (knownRecipes != null ? knownRecipes(player).Count : second);
                stamp = stamp * 31 + (knownMaterial != null ? knownMaterial(player).Count : second);
                stamp = stamp * 31 + (ZoneSystem.instance != null ? ZoneSystem.instance.GetGlobalKeys().Count : 0);
                stamp = stamp * 31 + (GameShowsAllRecipes ? 1 : 0) + (GameShowsAllPieces ? 2 : 0);
                return stamp;
            }
        }

        private static AccessTools.FieldRef<Player, HashSet<string>> Field(string name, List<string> missingOptional)
        {
            FieldInfo field = typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(HashSet<string>))
            {
                missingOptional.Add($"Player.{name} (a panel is then made again once a second)");
                return null;
            }
            return AccessTools.FieldRefAccess<Player, HashSet<string>>(field);
        }
    }
}
