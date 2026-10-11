using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>What the maps draw of the friends' map a cartography table gave the player.</summary>
    internal enum FriendsMapShows
    {
        Everything,
        ExploredGroundOnly,
        PinsOnly,
    }

    /// <summary>
    /// The friends' map, as the player chooses to see it (his ask, 2026-09-28, A39). Reading a table keeps the friends'
    /// explored ground apart from the player's own (the game's m_exploredOthers, the fog texture's green channel), faded in
    /// by the map shader's _SharedFade, and adds their pins with the friend as owner (m_ownerID). The game's own "shared
    /// map" checkbox on the large map hides both at once; this setting hides one of them:
    /// - explored ground only: a friend's pin isn't drawn (Drawing), unless Cartur's Map Pins made it (Sorter.MadeByCartur);
    /// - pins only: the shader's fade is held at 0, so the friends' ground stays under the fog, while the game's own fade,
    ///   which also draws the friends' pins, stays as the checkbox has it.
    /// Only drawing changes: nothing is deleted, and a table shares as before. A pin the player has clicked to keep is his
    /// (the game sets its owner to none), as the game's checkbox counts it.
    /// </summary>
    internal static class FriendsMap
    {
        private const string FadeProperty = "_SharedFade";

        private static AccessTools.FieldRef<Minimap, Material> smallShader;
        private static AccessTools.FieldRef<Minimap, Material> largeShader;
        private static AccessTools.FieldRef<Minimap, float> gameFade;
        private static int fadeId;

        // The map whose shaders this mod holds at 0, or null: it gives them back the game's own fade when it lets go.
        private static Minimap held;

        /// <summary>Whether the fog half of "pins only" can work: the game's shaders and fade were found.</summary>
        private static bool CanHoldFog => smallShader != null && largeShader != null && gameFade != null;

        internal static void Resolve(List<string> missingOptional)
        {
            smallShader = Field<Material>("m_mapSmallShader");
            largeShader = Field<Material>("m_mapLargeShader");
            gameFade = Field<float>("m_sharedMapDataFade");
            fadeId = Shader.PropertyToID(FadeProperty);
            if (!CanHoldFog)
            {
                smallShader = null;
                largeShader = null;
                gameFade = null;
                missingOptional.Add("Minimap.m_mapSmallShader, m_mapLargeShader or m_sharedMapDataFade (\"Friends' map: Pins only\" " +
                                    "then leaves the friends' explored ground as the game draws it)");
            }
        }

        /// <summary>Whether the rules leave a friend's pin undrawn: explored ground only, and a pin a friend placed by hand.</summary>
        internal static bool HidesPin(Minimap.PinData pin, RecordIndex record, bool recordPending) =>
            Settings.FriendsMap.Value == FriendsMapShows.ExploredGroundOnly && pin.m_ownerID != 0L &&
            !Sorter.MadeByCartur(pin, record, recordPending);

        /// <summary>
        /// After every frame (the module's LateUpdate), once the game's Update has set its fade: holds the friends' ground
        /// under the fog while "pins only" asks it, and gives the game its own fade back once it doesn't.
        /// </summary>
        internal static void Tick()
        {
            Minimap map = Minimap.instance;
            bool hold = Module.IsOn && CanHoldFog && map != null && Settings.FriendsMap.Value == FriendsMapShows.PinsOnly;
            if (held != null && (!hold || held != map))
            {
                Release();
            }
            if (!hold)
            {
                return;
            }
            Material small = smallShader(map);
            Material large = largeShader(map);
            if (small == null || large == null)
            {
                return;   // not set up yet (the map's Start)
            }
            // The game sets the fade only while it changes (its checkbox), so a value this mod set stays until then.
            if (small.GetFloat(fadeId) != 0f)
            {
                small.SetFloat(fadeId, 0f);
            }
            if (large.GetFloat(fadeId) != 0f)
            {
                large.SetFloat(fadeId, 0f);
            }
            held = map;
        }

        /// <summary>Gives the map's shaders the game's own fade back (switched off, another choice, or after an error).</summary>
        internal static void Release()
        {
            Minimap map = held;
            held = null;
            if (map == null || !CanHoldFog)
            {
                return;
            }
            float fade = gameFade(map);
            Material small = smallShader(map);
            Material large = largeShader(map);
            if (small != null)
            {
                small.SetFloat(fadeId, fade);
            }
            if (large != null)
            {
                large.SetFloat(fadeId, fade);
            }
        }

        private static AccessTools.FieldRef<Minimap, T> Field<T>(string name)
        {
            FieldInfo field = typeof(Minimap).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null && field.FieldType == typeof(T) ? AccessTools.FieldRefAccess<Minimap, T>(field) : null;
        }
    }
}
