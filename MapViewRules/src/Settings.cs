using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>A corner of a map, for the zoom readout.</summary>
    internal enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.mapviewrules.cfg, one section per
    /// part; inside miikeskii's QoL Pack, in the bundle's file, in the section "Map View Rules" (General) and one section
    /// "Map View Rules - &lt;part&gt;" for each section of rules (Sections). A configuration manager's window lists them
    /// all under the one heading, each section of rules as a fold-out (Folds). Every one takes effect at once, also when
    /// changed in game with a configuration manager.
    /// </summary>
    internal static class Settings
    {
        // A configuration manager saves the override list at every keystroke, and a half-typed name would otherwise get a
        // section of its own. So the list is read once the manager's window has closed (shudnal's and BepInEx's both say
        // whether it is open, DisplayingWindow), and without one once it has stayed unchanged this long.
        private const float OverridesSettle = 2.5f;

        private static readonly string[] ManagerGuids = { "_shudnal.ConfigurationManager", "com.bepis.bepinex.configurationmanager" };

        private static float overridesChangedAt = -1f;
        private static bool lookedForManager;
        private static BaseUnityPlugin manager;
        private static PropertyInfo managerWindowOpen;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> ZoomReadout { get; private set; }

        internal static ConfigEntry<Corner> ReadoutCornerLarge { get; private set; }

        internal static ConfigEntry<Corner> ReadoutCornerMinimap { get; private set; }

        internal static ConfigEntry<bool> NameUnderPointer { get; private set; }

        internal static ConfigEntry<FriendsMapShows> FriendsMap { get; private set; }

        internal static ConfigEntry<string> Overrides { get; private set; }

        /// <summary>The override list out of the box, his own (card A63): each of these has defaults of its own
        /// (SectionDefaults).</summary>
        internal const string DefaultOverrides = "Cloudberry,Lox,Boar,Wolf";

        // Which version's rules the settings file holds: 1 (0.1.0, or a file without it) or 2 (0.2.0 on, icons and names apart).
        private const int RulesNow = 2;

        private static ModuleSettings bound;
        private static ConfigEntry<int> rulesVersion;

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            ZoomReadout = settings.Bind(ModuleSettings.General, "ZoomReadout", false, "Zoom readout", 90,
                "Shows the map's zoom in a corner of the large map and of the minimap, as you zoom: 0 fully zoomed in, " +
                "100 fully out, the same numbers every zoom setting of this mod takes. Find the zoom you like, then type its " +
                "number in, or press the button beside the setting.");
            ReadoutCornerLarge = settings.Bind(ModuleSettings.General, "ReadoutCornerLargeMap", Corner.TopLeft, "Readout corner, large map", 89,
                "Which corner of the large map the zoom readout sits in.");
            ReadoutCornerMinimap = settings.Bind(ModuleSettings.General, "ReadoutCornerMinimap", Corner.BottomLeft, "Readout corner, minimap", 88,
                "Which corner of the minimap the zoom readout sits in.");
            NameUnderPointer = settings.Bind(ModuleSettings.General, "NameUnderPointer", true, "Name under the pointer", 80,
                "On the large map, a pin whose name these rules hide shows its name while the pointer is over it, so no pin " +
                "is ever unnamed. Off: hidden names stay hidden.");
            FriendsMap = settings.Bind(ModuleSettings.General, "FriendsMap", FriendsMapShows.Everything, "Friends' map", 75,
                "What both maps draw of the map your friends gave you through a cartography table. Everything: as the game " +
                "draws it. ExploredGroundOnly: their explored ground, and none of the pins they placed by hand (every pin " +
                "drawn with one of Cartur's Map Pins' icons still shows). PinsOnly: their pins, and none of their explored ground. Only what is " +
                "drawn changes: nothing is deleted, and a table shares everything as before. A pin you clicked to keep is " +
                "yours. The game's own shared-map checkbox on the large map still hides both.");
            // His own list out of the box since card A63 (2026-10-06; empty before): a file still holding the empty list
            // moves once, after BindSettings, so its sections are bound at the module's Resolve (ApplyMovedOverrides).
            Overrides = settings.Bind(ModuleSettings.General, "Overrides", DefaultOverrides, "Pins with their own rules", 70,
                "Kinds or pin names that get a section of rules of their own, apart from their group or category, " +
                "separated by commas: a kind as Cartur's Map Pins names it (Cloudberry, Raspberry, Tin) or a pin's name as " +
                "the map shows it (Draugr Elite Spawner), in any word order and case. The list is read when this window " +
                "closes: a section \"Override - <its words>\" is there, last under this heading, the next time it opens. The " +
                "first name in the list " +
                "that fits a pin wins. A name taken off the list stops applying, and its section goes at the next start; " +
                "added back, it has its values again. Out of the box: " + DefaultOverrides + ".");
            settings.MovedDefault(Overrides, string.Empty, 1);

            ZoomReadout.SettingChanged += (sender, args) => Drawing.RequestRedraw();
            NameUnderPointer.SettingChanged += (sender, args) => Drawing.RequestRedraw();
            FriendsMap.SettingChanged += (sender, args) => Drawing.RequestRedraw();
            Overrides.SettingChanged += (sender, args) => overridesChangedAt = Time.realtimeSinceStartup;

            Sections.Bind(settings);

            // Read at the module's Resolve (CarryOverOnce), after the bundle has taken a single release's settings over.
            bound = settings;
            rulesVersion = settings.Bind(ModuleSettings.General, "RulesVersion", 1, "Rules version", 71,
                "Which version of this mod's rules the settings file holds (2: icons and names apart). Kept by the mod; " +
                "not shown in the settings window.");
            ModuleSettings.HintsOf(rulesVersion).Browsable = false;
        }

        /// <summary>
        /// At the module's Resolve, once: a settings file from 0.1.0 has no "Large map: names", which would follow All
        /// pins (shown) and bring back the names of every section its "show" hid. So every section's names take the value
        /// its "show", now its icons, has, and so do the sections of names no longer in the override list, which the file
        /// still keeps (BepInEx's orphaned entries), for when a name is listed again. On a new file every "show" is at its
        /// default, so nothing changes. Resolve, not the binding: the bundle takes a single release's settings over
        /// (0.1.0's included, without this setting) right after the module has bound its own, and before its Resolve;
        /// and Resolve runs even when the module then can't start, so names set meanwhile are never overwritten later. The
        /// version is written last: every write saves the file, and a save that fails leaves it to be done again.
        /// </summary>
        internal static void CarryOverOnce()
        {
            if (rulesVersion == null || rulesVersion.Value >= RulesNow)
            {
                return;
            }
            Sections.CarryOverShow();
            int kept = CarryOverUnlisted();
            rulesVersion.Value = RulesNow;
            Module.Log.LogInfo("Settings from before Map View Rules 0.2.0: each section's \"Large map: names\" takes the value of " +
                               "its \"Large map: show\", now \"Large map: icons\", so the maps look as they did" +
                               (kept > 0 ? $" ({kept} of them for names no longer in the override list)." : "."));
        }

        // The same for the override sections the file keeps unbound: a "LargeMapNames" beside each "LargeMapShow" there,
        // with its value, which a section bound later reads. The file's unbound entries are BepInEx's own (ConfigFile's
        // private OrphanedEntries); without them, nothing is done.
        private static int CarryOverUnlisted()
        {
            PropertyInfo orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (orphans == null || !(orphans.GetValue(rulesVersion.ConfigFile, null) is System.Collections.Generic.Dictionary<ConfigDefinition, string> unbound))
            {
                return 0;
            }
            string overrides = bound.SectionOf("Override - ");
            var add = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<ConfigDefinition, string>>();
            foreach (System.Collections.Generic.KeyValuePair<ConfigDefinition, string> each in unbound)
            {
                if (each.Key.Key == "LargeMapShow" && each.Key.Section.StartsWith(overrides, System.StringComparison.Ordinal))
                {
                    var names = new ConfigDefinition(each.Key.Section, "LargeMapNames");
                    if (!unbound.ContainsKey(names))
                    {
                        add.Add(new System.Collections.Generic.KeyValuePair<ConfigDefinition, string>(names, each.Value));
                    }
                }
            }
            foreach (System.Collections.Generic.KeyValuePair<ConfigDefinition, string> each in add)
            {
                unbound[each.Key] = each.Value;
            }
            if (add.Count > 0)
            {
                rulesVersion.ConfigFile.Save();
            }
            return add.Count;
        }

        /// <summary>
        /// At the module's Resolve, once: an override list changed while the module started (the host moved an old file's
        /// empty list to the new default, or the bundle took a single release's list over) is read at once, so its sections
        /// are bound before anything is drawn, rather than once the list has settled (Tick).
        /// </summary>
        internal static void ApplyMovedOverrides()
        {
            if (overridesChangedAt < 0f)
            {
                return;
            }
            overridesChangedAt = -1f;
            Sections.ApplyOverrides();
        }

        /// <summary>Called every frame: reads the override list once it has stopped changing and the configuration
        /// manager's window, if there is one, has closed.</summary>
        internal static void Tick()
        {
            if (overridesChangedAt >= 0f && Time.realtimeSinceStartup - overridesChangedAt >= OverridesSettle && !ManagerWindowOpen())
            {
                overridesChangedAt = -1f;
                Sections.ApplyOverrides();
                Drawing.RequestRedraw();
            }
        }

        // Whether a configuration manager's window is open now; false without one.
        private static bool ManagerWindowOpen()
        {
            if (!lookedForManager)
            {
                lookedForManager = true;
                foreach (string guid in ManagerGuids)
                {
                    if (!Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) || info.Instance == null)
                    {
                        continue;
                    }
                    PropertyInfo open = info.Instance.GetType().GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public);
                    if (open != null && open.PropertyType == typeof(bool) && open.GetIndexParameters().Length == 0)
                    {
                        manager = info.Instance;
                        managerWindowOpen = open;
                        break;
                    }
                }
            }
            if (manager == null)
            {
                return false;
            }
            try
            {
                return (bool)managerWindowOpen.GetValue(manager, null);
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
