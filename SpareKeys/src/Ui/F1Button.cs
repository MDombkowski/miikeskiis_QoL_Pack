using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// "A button in F1": the setting's row in a configuration manager's window, drawn by the mod (the kit's customDrawer):
    /// its switch, and while it is on a "Show the key map" button. The click only asks; the window opens at the next frame,
    /// outside the manager's drawing, and the manager's window closes so the map can be seen. An error here never breaks
    /// the manager's window: said once, and the row is drawn as a plain switch from then on.
    /// </summary>
    internal static class F1Button
    {
        private static bool broken;

        internal static void Draw(ConfigEntryBase entry)
        {
            if (broken)
            {
                bool plain = GUILayout.Toggle(entry.BoxedValue is bool value && value, "On");
                if (!(entry.BoxedValue is bool before) || before != plain)
                {
                    entry.BoxedValue = plain;
                }
                return;
            }
            bool horizontal = false;
            try
            {
                bool on = entry.BoxedValue is bool current && current;
                // The key map opens only in a world, with the mod on: elsewhere the button shows, greyed, and says why.
                bool usable = Module.IsOn && KeyMapWindow.InWorld;
                GUILayout.BeginHorizontal();
                horizontal = true;
                bool wanted = GUILayout.Toggle(on, on ? "On" : "Off", GUILayout.Width(60f));
                if (wanted != on)
                {
                    entry.BoxedValue = wanted;
                }
                // The same controls in both of Unity's passes (Layout, then the event's): drawn from the values read above.
                if (on)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && usable;
                    string label = usable ? "Show the key map" : Module.IsOn ? "Show the key map (in a world)" : "Show the key map (Spare Keys is off)";
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(true)) && usable)
                    {
                        KeyMapWindow.RequestOpen();
                    }
                    GUI.enabled = enabled;
                }
                horizontal = false;
                GUILayout.EndHorizontal();
            }
            catch (ExitGUIException)
            {
                throw;   // Unity's own way out of a GUI pass: it puts its groups right itself
            }
            catch (Exception error)
            {
                broken = true;
                if (horizontal)
                {
                    try
                    {
                        GUILayout.EndHorizontal();
                    }
                    catch (Exception)
                    {
                        // Closing what the error left open is all that's wanted.
                    }
                }
                Module.Log?.LogWarning($"The button in F1 failed ({error.GetType().Name}: {error.Message}); it shows as a plain switch from now on. " +
                                       "The key to open the key map still works.");
            }
        }
    }
}
