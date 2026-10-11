using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Outfitter.Loadouts;

namespace Outfitter
{
    /// <summary>
    /// RandyKnapp's EquipmentAndQuickSlots (3.1.3, read 2026-10-05), found at start and used only through its own
    /// interface, by reflection, so Outfitter needs nothing of it to build or run (his words: "Must be compatible
    /// with/without"). It keeps its slots as three hidden rows of the same inventory, under the rows he sees; its static
    /// class EquipmentAndQuickSlots.API (looked up by its full name in its own assembly, which also holds Auga.API) says
    /// where the visible rows end and which cells are which slots. Slots that aren't switched on in its settings are left
    /// out ("active" in GetSlotInfoJson). If it's installed but its interface can't be read, Outfitter never touches its
    /// rows: it counts the visible rows from the game's own row count, as the chest service does.
    /// </summary>
    internal static class Eaqs
    {
        internal const string Guid = "randyknapp.mods.equipmentandquickslots";

        private static MethodInfo getVisibleRows;
        private static MethodInfo getSlotIdsJson;
        private static MethodInfo getSlotInfoJson;
        private static bool warned;

        /// <summary>EquipmentAndQuickSlots is installed (whether or not its interface could be read).</summary>
        internal static bool Present { get; private set; }

        /// <summary>Its interface was found: its rows and slots can be used.</summary>
        internal static bool Usable { get; private set; }

        /// <summary>Looks for it once, at start.</summary>
        internal static void Find(ManualLogSource log)
        {
            Present = false;
            Usable = false;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out BepInEx.PluginInfo info) || info?.Instance == null)
            {
                return;
            }
            Present = true;
            try
            {
                Type api = info.Instance.GetType().Assembly.GetType("EquipmentAndQuickSlots.API", throwOnError: false);
                const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
                getVisibleRows = api?.GetMethod("GetVisibleRows", Static, null, Type.EmptyTypes, null);
                getSlotIdsJson = api?.GetMethod("GetSlotIdsJson", Static, null, Type.EmptyTypes, null);
                getSlotInfoJson = api?.GetMethod("GetSlotInfoJson", Static, null, new[] { typeof(string) }, null);
                Usable = getVisibleRows != null && getVisibleRows.ReturnType == typeof(int) &&
                         getSlotIdsJson != null && getSlotIdsJson.ReturnType == typeof(string) &&
                         getSlotInfoJson != null && getSlotInfoJson.ReturnType == typeof(string);
            }
            catch (Exception error)
            {
                Usable = false;
                log?.LogWarning($"EquipmentAndQuickSlots' interface couldn't be looked up ({error.Message}).");
            }
            if (Usable)
            {
                log?.LogInfo($"Found EquipmentAndQuickSlots {info.Metadata?.Version}: loadouts use its slots.");
            }
            else
            {
                log?.LogWarning($"EquipmentAndQuickSlots {info.Metadata?.Version} is installed, but its interface wasn't found: " +
                                "Outfitter leaves its rows alone (items saved in its slots go to free cells).");
            }
        }

        /// <summary>The rows he sees, from its interface; -1 when it can't be read.</summary>
        internal static int VisibleRows()
        {
            if (!Usable)
            {
                return -1;
            }
            try
            {
                return (int)getVisibleRows.Invoke(null, null);
            }
            catch (Exception error)
            {
                Broke(error);
                return -1;
            }
        }

        /// <summary>Its active slots by name, with their cells; empty when it can't be read.</summary>
        internal static void AddSlots(GridShape grid)
        {
            if (!Usable)
            {
                return;
            }
            try
            {
                foreach (string id in Json.Strings((string)getSlotIdsJson.Invoke(null, null)))
                {
                    if (!LoadoutText.IsSlotId(id))
                    {
                        continue;
                    }
                    Dictionary<string, string> info = Json.Object((string)getSlotInfoJson.Invoke(null, new object[] { id }));
                    if (info == null || !info.TryGetValue("active", out string active) || active != "true" ||
                        !info.TryGetValue("gridX", out string gx) || !LoadoutText.TryInt(gx, out int x) ||
                        !info.TryGetValue("gridY", out string gy) || !LoadoutText.TryInt(gy, out int y) ||
                        x < 0 || x >= grid.Width || y < grid.Rows || y >= grid.Height)
                    {
                        continue;
                    }
                    grid.Slots[id] = new SlotCell
                    {
                        X = x,
                        Y = y,
                        Quick = info.TryGetValue("isQuickSlot", out string quick) && quick == "true",
                        Equipment = info.TryGetValue("isEquipmentSlot", out string equipment) && equipment == "true",
                    };
                }
            }
            catch (Exception error)
            {
                grid.Slots.Clear();
                Broke(error);
            }
        }

        private static void Broke(Exception error)
        {
            Usable = false;
            if (!warned)
            {
                warned = true;
                Module.Warn($"EquipmentAndQuickSlots' interface stopped answering ({error.GetBaseException().Message}); " +
                            "Outfitter leaves its rows alone for the rest of the session.");
            }
        }

        /// <summary>
        /// Reads the two kinds of JSON its interface returns: a list of strings, and one flat object of strings, numbers
        /// and true/false (its own writer escapes only "\" and the quote). Anything else is read as nothing.
        /// </summary>
        internal static class Json
        {
            internal static List<string> Strings(string json)
            {
                var strings = new List<string>();
                if (string.IsNullOrEmpty(json))
                {
                    return strings;
                }
                int i = 0;
                Skip(json, ref i);
                if (i >= json.Length || json[i] != '[')
                {
                    return strings;
                }
                i++;
                while (true)
                {
                    Skip(json, ref i);
                    if (i >= json.Length || json[i] == ']')
                    {
                        return strings;
                    }
                    string value = ReadString(json, ref i);
                    if (value == null)
                    {
                        return strings;
                    }
                    strings.Add(value);
                    Skip(json, ref i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i++;
                    }
                }
            }

            internal static Dictionary<string, string> Object(string json)
            {
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                var values = new Dictionary<string, string>();
                int i = 0;
                Skip(json, ref i);
                if (i >= json.Length || json[i] != '{')
                {
                    return null;
                }
                i++;
                while (true)
                {
                    Skip(json, ref i);
                    if (i >= json.Length)
                    {
                        return null;
                    }
                    if (json[i] == '}')
                    {
                        return values;
                    }
                    string key = ReadString(json, ref i);
                    Skip(json, ref i);
                    if (key == null || i >= json.Length || json[i] != ':')
                    {
                        return null;
                    }
                    i++;
                    Skip(json, ref i);
                    if (i >= json.Length)
                    {
                        return null;
                    }
                    string value;
                    if (json[i] == '"')
                    {
                        value = ReadString(json, ref i);
                    }
                    else
                    {
                        int start = i;
                        while (i < json.Length && json[i] != ',' && json[i] != '}' && !char.IsWhiteSpace(json[i]))
                        {
                            i++;
                        }
                        value = json.Substring(start, i - start);
                    }
                    if (value == null)
                    {
                        return null;
                    }
                    values[key] = value;
                    Skip(json, ref i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i++;
                    }
                }
            }

            private static void Skip(string json, ref int i)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
            }

            private static string ReadString(string json, ref int i)
            {
                if (i >= json.Length || json[i] != '"')
                {
                    return null;
                }
                var text = new StringBuilder();
                for (i++; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '"')
                    {
                        i++;
                        return text.ToString();
                    }
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        c = json[++i];
                    }
                    text.Append(c);
                }
                return null;
            }
        }
    }
}
