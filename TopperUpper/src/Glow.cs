using System.Collections.Generic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// A station's linked chests glow while he points at the station, and while he links it (the setting "Linked chests
    /// glow"): inputs in one colour, outputs in another, the way the game tints a piece (the colour and glow of the chest's
    /// own renderers, through the game's MaterialMan, as StoreStash lights up the chests a quick stack filled), put back
    /// as soon as he looks away. Only on his screen.
    /// </summary>
    internal static class Glow
    {
        private static readonly Dictionary<GameObject, Color> Lit = new Dictionary<GameObject, Color>();
        private static readonly Dictionary<GameObject, Color> Wanted = new Dictionary<GameObject, Color>();
        private static Fed shownFor;
        private static float resolvedAt;

        /// <summary>Every frame: the glows for the station pointed at (or being linked), others put out.</summary>
        internal static void Update()
        {
            Wanted.Clear();
            Fed station = Module.IsOn && Settings.Glow.Value ? Shown() : null;
            if (station != null)
            {
                // Resolved at most every few frames, or at once for another station.
                if (station != shownFor || Time.time - resolvedAt > 0.25f)
                {
                    shownFor = station;
                    resolvedAt = Time.time;
                    Links.Resolve(station, out List<Container> inputs, out List<Container> outputs);
                    Remembered.Clear();
                    foreach (Container chest in inputs)
                    {
                        Remembered[chest.gameObject] = Settings.InputColour.Value;
                    }
                    foreach (Container chest in outputs)
                    {
                        // A chest both input and output (card B11) glows its own colour.
                        Remembered[chest.gameObject] = inputs.Contains(chest) ? Settings.BothColour.Value : Settings.OutputColour.Value;
                    }
                }
                foreach (KeyValuePair<GameObject, Color> each in Remembered)
                {
                    if (each.Key != null)
                    {
                        Wanted[each.Key] = each.Value;
                    }
                }
            }
            else
            {
                shownFor = null;
                Remembered.Clear();
            }
            List<GameObject> off = null;
            foreach (KeyValuePair<GameObject, Color> each in Lit)
            {
                if (each.Key == null || !Wanted.TryGetValue(each.Key, out Color colour) || colour != each.Value)
                {
                    (off = off ?? new List<GameObject>()).Add(each.Key);
                }
            }
            if (off != null)
            {
                foreach (GameObject shown in off)
                {
                    PutOut(shown);
                    Lit.Remove(shown);
                }
            }
            foreach (KeyValuePair<GameObject, Color> each in Wanted)
            {
                if (!Lit.ContainsKey(each.Key) && MaterialMan.instance != null)
                {
                    MaterialMan.instance.SetValue(each.Key, ShaderProps._EmissionColor, each.Value * 0.4f);
                    MaterialMan.instance.SetValue(each.Key, ShaderProps._Color, each.Value);
                    Lit[each.Key] = each.Value;
                }
            }
        }

        private static readonly Dictionary<GameObject, Color> Remembered = new Dictionary<GameObject, Color>();

        // The station being linked, else the station he points at.
        private static Fed Shown()
        {
            Fed linking = Links.Linking;
            if (linking != null)
            {
                return linking;
            }
            Player player = Player.m_localPlayer;
            Fed pointed = Registry.Pointed(player != null ? player.GetHoverObject() : null);
            return pointed != null && !pointed.IsLight ? pointed : null;
        }

        /// <summary>The undo step, and switching off: every glow goes out at once.</summary>
        internal static void PutOutAll()
        {
            foreach (GameObject shown in Lit.Keys)
            {
                PutOut(shown);
            }
            Lit.Clear();
            Remembered.Clear();
            shownFor = null;
        }

        private static void PutOut(GameObject shown)
        {
            if (shown == null || MaterialMan.instance == null)
            {
                return;
            }
            MaterialMan.instance.ResetValue(shown, ShaderProps._Color);
            MaterialMan.instance.ResetValue(shown, ShaderProps._EmissionColor);
        }
    }
}
