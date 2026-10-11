using System.Collections.Generic;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The chests a quick stack put something into glow for a moment (the setting "Light up the chests that received
    /// items", on by default since card A63), the way the game lights up a piece the hammer points at (WearNTear.Highlight): the colour
    /// and glow of the chest's own renderers, set through the game's MaterialMan and put back after. Only on his screen.
    /// </summary>
    internal static class LightUp
    {
        /// <summary>How long a chest glows, in seconds.</summary>
        internal const float For = 2f;

        private static readonly Dictionary<GameObject, float> Lit = new Dictionary<GameObject, float>();

        /// <summary>Lights a chest up, if the setting is on.</summary>
        internal static void Light(Container chest)
        {
            if (Settings.LightUp.Value)
            {
                Glow(chest);
            }
        }

        /// <summary>Lights a chest up whatever the quick stack's setting says: for a caller with a setting of its own (Stack
        /// Overflow's "Light up the chests a tidy changed").</summary>
        internal static void Glow(Container chest)
        {
            if (chest == null || MaterialMan.instance == null)
            {
                return;
            }
            GameObject shown = chest.gameObject;
            Color colour = Settings.LightUpColor.Value;
            MaterialMan.instance.SetValue(shown, ShaderProps._EmissionColor, colour * 0.4f);
            MaterialMan.instance.SetValue(shown, ShaderProps._Color, colour);
            Lit[shown] = Time.time + For;
        }

        /// <summary>Every frame: the glows that are over go out.</summary>
        internal static void Update()
        {
            if (Lit.Count == 0)
            {
                return;
            }
            List<GameObject> over = null;
            foreach (KeyValuePair<GameObject, float> each in Lit)
            {
                if (each.Key == null || Time.time > each.Value || !Module.IsOn)
                {
                    (over = over ?? new List<GameObject>()).Add(each.Key);
                }
            }
            if (over != null)
            {
                foreach (GameObject shown in over)
                {
                    PutOut(shown);
                    Lit.Remove(shown);
                }
            }
        }

        /// <summary>The undo step after an error: every glow goes out at once.</summary>
        internal static void PutOutAll()
        {
            foreach (GameObject shown in Lit.Keys)
            {
                PutOut(shown);
            }
            Lit.Clear();
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
