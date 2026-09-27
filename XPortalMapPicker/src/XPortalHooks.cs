using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// Everything this mod reads or calls inside XPortal. XPortal keeps these members internal or
    /// private, so they are looked up by name once, at startup. If any has gone because XPortal
    /// changed, the mod stays off and XPortal is left untouched. Checked against XPortal 1.2.25.
    /// </summary>
    internal static class XPortalHooks
    {
        internal const string Guid = "yay.spikehimself.xportal";

        /// <summary>What Resolve reports missing when XPortal isn't installed (possible inside the bundle, where XPortal is optional).</summary>
        internal const string NotInstalled = "the XPortal plugin itself";

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // XPortal.UI.PortalConfigurationPanel: the "Hail, traveller!" panel, a single instance.
        private static PropertyInfo panelInstance;
        private static FieldInfo mainPanel;
        private static FieldInfo destinationList;
        private static FieldInfo pingButton;
        private static FieldInfo rowPortals;
        private static MethodInfo setPanelActive;

        // XPortal.KnownPortalsManager holds every portal this machine knows; XPortal.KnownPortal is one of them.
        private static PropertyInfo managerInstance;
        private static MethodInfo listPortals;
        private static PropertyInfo portalId;
        private static PropertyInfo portalName;
        private static PropertyInfo portalLocation;

        // XPortal.XPortalConfig: its copy of the server's settings. Optional.
        private static PropertyInfo configInstance;
        private static PropertyInfo serverSettings;
        private static FieldInfo pingMapDisabled;

        /// <summary>A portal the destination list offers.</summary>
        internal readonly struct Portal
        {
            internal readonly ZDOID Id;
            internal readonly string Name;
            internal readonly Vector3 Location;

            internal Portal(ZDOID id, string name, Vector3 location)
            {
                Id = id;
                Name = name;
                Location = location;
            }
        }

        /// <summary>PortalConfigurationPanel.InitialiseUI: builds the panel, once per world session.</summary>
        internal static MethodInfo BuildPanel { get; private set; }

        /// <summary>PortalConfigurationPanel.SetPingMapButtonActive: shows or hides the Ping button and
        /// re-sizes the destination list to match. XPortal runs it whenever the list is filled or changed.</summary>
        internal static MethodInfo ArrangeDestinationRow { get; private set; }

        internal static string Version { get; private set; } = "(version unknown)";

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo plugin) || plugin.Instance == null)
            {
                missing.Add(NotInstalled);
                return;
            }
            Version = plugin.Metadata.Version.ToString();
            Assembly xportal = plugin.Instance.GetType().Assembly;

            Type panel = FindType(xportal, "XPortal.UI.PortalConfigurationPanel", missing);
            if (panel != null)
            {
                panelInstance = FindProperty(panel, "Instance", AnyStatic, panel, missing);
                mainPanel = FindField(panel, "mainPanel", typeof(GameObject), missing);
                destinationList = FindField(panel, "targetPortalDropdown", typeof(Dropdown), missing);
                pingButton = FindField(panel, "pingMapButtonObject", typeof(GameObject), missing);
                rowPortals = FindField(panel, "dropdownIndexToZDOIDMapping", typeof(Dictionary<int, ZDOID>), missing);
                setPanelActive = FindMethod(panel, "SetActive", new[] { typeof(bool) }, null, missing);
                BuildPanel = FindMethod(panel, "InitialiseUI", Type.EmptyTypes, null, missing);
                ArrangeDestinationRow = FindMethod(panel, "SetPingMapButtonActive", new[] { typeof(bool) }, null, missing);
            }

            Type manager = FindType(xportal, "XPortal.KnownPortalsManager", missing);
            if (manager != null)
            {
                managerInstance = FindProperty(manager, "Instance", AnyStatic, manager, missing);
                listPortals = FindMethod(manager, "GetList", Type.EmptyTypes, typeof(IEnumerable), missing);
            }

            Type portal = FindType(xportal, "XPortal.KnownPortal", missing);
            if (portal != null)
            {
                portalId = FindProperty(portal, "Id", AnyInstance, typeof(ZDOID), missing);
                portalName = FindProperty(portal, "Name", AnyInstance, typeof(string), missing);
                portalLocation = FindProperty(portal, "Location", AnyInstance, typeof(Vector3), missing);
            }

            Type config = xportal.GetType("XPortal.XPortalConfig", throwOnError: false);
            configInstance = config?.GetProperty("Instance", AnyStatic);
            serverSettings = config?.GetProperty("Server", AnyInstance);
            pingMapDisabled = serverSettings?.PropertyType.GetField("PingMapDisabled", AnyInstance);
            if (configInstance == null || serverSettings == null || pingMapDisabled == null || pingMapDisabled.FieldType != typeof(bool))
            {
                pingMapDisabled = null;
                missingOptional.Add("XPortal's copy of the server setting PingMapDisabled " +
                                    "(the Map button will show even where the server has turned XPortal's Ping button off)");
            }
        }

        internal static object Panel => panelInstance.GetValue(null);

        /// <summary>The panel's root object; null until XPortal has built it, and again once it is destroyed.</summary>
        internal static GameObject MainPanel(object panel) => (GameObject)mainPanel.GetValue(panel);

        internal static Dropdown DestinationList(object panel) => (Dropdown)destinationList.GetValue(panel);

        internal static GameObject PingButton(object panel) => (GameObject)pingButton.GetValue(panel);

        /// <summary>Shows or hides the panel the way XPortal itself does: showing it takes Jötunn's input
        /// block (the mouse is freed and the game ignores keys), hiding it releases the block.</summary>
        internal static void SetPanelActive(object panel, bool active) => setPanelActive.Invoke(panel, new object[] { active });

        /// <summary>The portals the destination list offers right now, so XPortal's own rules decide what
        /// can be chosen (every known portal except the one being set up).</summary>
        internal static List<Portal> ChoosablePortals(object panel)
        {
            var known = new Dictionary<ZDOID, Portal>();
            object manager = managerInstance.GetValue(null);
            foreach (object item in (IEnumerable)listPortals.Invoke(manager, null))
            {
                var portal = new Portal(
                    (ZDOID)portalId.GetValue(item),
                    (string)portalName.GetValue(item),
                    (Vector3)portalLocation.GetValue(item));
                known[portal.Id] = portal;
            }

            var choosable = new List<Portal>();
            foreach (ZDOID id in RowPortals(panel).Values)
            {
                // Row 0 is "(None)". A row can also outlive its portal if XPortal dropped the portal after filling the list.
                if (id != ZDOID.None && known.TryGetValue(id, out Portal portal))
                {
                    choosable.Add(portal);
                }
            }
            return choosable;
        }

        /// <summary>Selects the given portal's row in the destination list. Setting the list's value fires
        /// XPortal's own OnDropdownValueChanged, which stores the choice exactly as a pick from the list does.
        /// False if the portal is no longer in the list.</summary>
        internal static bool SelectDestination(object panel, ZDOID portal)
        {
            foreach (KeyValuePair<int, ZDOID> row in RowPortals(panel))
            {
                if (row.Value == portal)
                {
                    DestinationList(panel).value = row.Key;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The server's PingMapDisabled as XPortal received it. The setting's own description:
        /// "For players who wish to play without a map."</summary>
        internal static bool ServerTurnedOffMapPing()
        {
            if (pingMapDisabled == null)
            {
                return false;
            }
            object server = serverSettings.GetValue(configInstance.GetValue(null));
            return server != null && (bool)pingMapDisabled.GetValue(server);
        }

        private static Dictionary<int, ZDOID> RowPortals(object panel) => (Dictionary<int, ZDOID>)rowPortals.GetValue(panel);

        private static Type FindType(Assembly assembly, string fullName, List<string> missing)
        {
            Type type = assembly.GetType(fullName, throwOnError: false);
            if (type == null)
            {
                missing.Add(fullName);
            }
            return type;
        }

        private static FieldInfo FindField(Type owner, string name, Type fieldType, List<string> missing)
        {
            FieldInfo field = owner.GetField(name, AnyInstance);
            if (field != null && fieldType.IsAssignableFrom(field.FieldType))
            {
                return field;
            }
            missing.Add($"{owner.Name}.{name}");
            return null;
        }

        private static PropertyInfo FindProperty(Type owner, string name, BindingFlags flags, Type propertyType, List<string> missing)
        {
            PropertyInfo property = owner.GetProperty(name, flags);
            if (property != null && property.GetGetMethod(nonPublic: true) != null && propertyType.IsAssignableFrom(property.PropertyType))
            {
                return property;
            }
            missing.Add($"{owner.Name}.{name}");
            return null;
        }

        /// <param name="returns">The type the result must be usable as, or null when the result is not used.</param>
        private static MethodInfo FindMethod(Type owner, string name, Type[] parameters, Type returns, List<string> missing)
        {
            MethodInfo method = owner.GetMethod(name, AnyInstance, null, parameters, null);
            if (method != null && (returns == null || returns.IsAssignableFrom(method.ReturnType)))
            {
                return method;
            }
            missing.Add($"{owner.Name}.{name}");
            return null;
        }
    }
}
