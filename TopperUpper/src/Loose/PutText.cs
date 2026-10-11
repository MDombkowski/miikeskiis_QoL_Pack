using System;
using System.Reflection;
using UnityEngine;

namespace TopperUpper.Loose
{
    /// <summary>
    /// The loose-item pickup's line over a chest ("Stone x87"): the game's own floating world text (the kind damage numbers
    /// and ServersideQoL's "added" lines use), made on this computer only. The game's public way to show one
    /// (DamageText.ShowText) sends it to every player as a routed message; its private AddInworldText, which that message
    /// ends in, shows it here alone, so it's called by name, looked up at the module's start. Like the game's own, nothing shows past its
    /// text distance (30 m) or while the HUD is hidden. If the game has changed the method, or it fails, the caller writes a
    /// line at the top left instead, and the log says so once.
    /// </summary>
    internal static class PutText
    {
        private static MethodInfo add;
        private static bool looked;
        private static bool broken;

        /// <summary>Shows the text floating at <paramref name="at"/>; false when it can't (too far, or the game's method is
        /// gone), so the caller says it another way.</summary>
        internal static bool OverTheChest(Vector3 at, string text)
        {
            DamageText texts = DamageText.instance;
            Camera camera = Utils.GetMainCamera();
            if (broken || texts == null || camera == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            if (Hud.IsUserHidden())
            {
                return true;   // as the game's own texts: nothing while the HUD is hidden
            }
            float distance = Vector3.Distance(camera.transform.position, at);
            if (distance > texts.m_maxTextDistance)
            {
                return false;
            }
            MethodInfo method = Method();
            if (method == null)
            {
                return false;
            }
            try
            {
                method.Invoke(texts, new object[] { DamageText.TextType.Normal, at, distance, text, false });
                return true;
            }
            catch (Exception error)
            {
                broken = true;
                Module.Warn($"The loose-item pickup's line over a chest failed, so it writes its lines at the top left from now on: {error}");
                return false;
            }
        }

        /// <summary>At the module's start: finds the game's method, or says in the start log that the lines go to the top
        /// left instead.</summary>
        internal static void Resolve(System.Collections.Generic.List<string> missingOptional)
        {
            looked = true;
            add = typeof(DamageText).GetMethod("AddInworldText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                new[] { typeof(DamageText.TextType), typeof(Vector3), typeof(float), typeof(string), typeof(bool) }, null);
            if (add == null)
            {
                missingOptional.Add("the loose-item pickup's line over a chest (the game has changed: DamageText.AddInworldText not " +
                                    "found; its lines go to the top left)");
            }
        }

        private static MethodInfo Method() => looked ? add : null;
    }
}
