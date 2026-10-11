using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MapViewRules
{
    /// <summary>
    /// The game method this mod hooks, and the private members it uses, looked up once at startup.
    /// Checked against the game's Minimap of 2026-09-25 (UpdatePins, m_pins, m_pinUpdateRequired).
    /// </summary>
    internal static class GameHooks
    {
        private static AccessTools.FieldRef<Minimap, List<Minimap.PinData>> pins;
        private static AccessTools.FieldRef<Minimap, bool> pinUpdateRequired;

        /// <summary>Minimap.UpdatePins(): where the game (re)draws the pins of the map shown, and Cartur's Map Pins after it.</summary>
        internal static MethodInfo UpdatePins { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            UpdatePins = typeof(Minimap).GetMethod("UpdatePins", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (UpdatePins == null)
            {
                missing.Add("Minimap.UpdatePins");
            }
            pins = Field<List<Minimap.PinData>>("m_pins", missing);
            pinUpdateRequired = Field<bool>("m_pinUpdateRequired", missing);
        }

        /// <summary>Every pin of the map: the game's own list, only ever read here.</summary>
        internal static List<Minimap.PinData> Pins(Minimap map) => map != null ? pins(map) : null;

        /// <summary>Asks the map to draw its pins again this frame or the next, as the game itself does when they change.</summary>
        internal static void RequestPinUpdate(Minimap map)
        {
            if (map != null)
            {
                pinUpdateRequired(map) = true;
            }
        }

        private static AccessTools.FieldRef<Minimap, T> Field<T>(string name, List<string> missing)
        {
            FieldInfo field = typeof(Minimap).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(T))
            {
                missing.Add($"Minimap.{name}");
                return null;
            }
            return AccessTools.FieldRefAccess<Minimap, T>(field);
        }
    }
}
