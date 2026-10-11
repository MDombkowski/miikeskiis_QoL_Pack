// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// What the pack changes in other mods made for speed, in a section of its own, "Performance mods (every mod)" (since
    /// 2026-10-08, card A69; his ask: "Should there be a section of our config dedicated to any modifications we make to
    /// this (and other) performance mods?"). Each mod that uses the chest service binds it (ChestSettings.Bind), so in the
    /// bundle it is one entry.
    ///
    /// Skarif's ValheimPerformanceOverhaul (8.2.4) re-sorts everything this game sends the server by distance from the
    /// player, nearest first, every time (its NetworkOptimizer: a postfix on ZDOMan.CreateSyncList, always on, with no
    /// setting of its own). The game sends what changed oldest first, with what was handed to another game first of all,
    /// so everything gets its turn. Sorted by distance, in a busy base (tames moving, fires burning, pieces placed) there is
    /// always something nearer waiting, so the record of a chest 50 m away handed to the server, or taken from, can wait
    /// for minutes. Meanwhile his game thinks the server has the chest and the server thinks his game has it: nobody
    /// answers for it, ServersideQoL asks for it every second, and building, crafting and fetching from it fail. His log
    /// of 2026-10-08 with it on: 1,360 requests from the server for chests in 24 minutes, 1,280 of them for chests his
    /// game had already handed over; with it off, 33 in 17 minutes and none. Its switch is a public static field,
    /// `ValheimEngineOptimizer.Modules.NetworkOptimizer.PriorityZDOSorting`, which nothing in it sets (read in 8.2.4's
    /// code): with "Skarif's mod: send in the game's own order" on, this copy sets it false while the game runs, and back
    /// to true when the setting goes off (only if it was this copy that set it false); a copy whose module didn't bind the
    /// setting never touches it. The rest of Skarif's mod is untouched.
    /// It changes nothing where Skarif's mod isn't loaded; where its switch can't be found (a later version that changed
    /// it), it says so once in the log.
    /// </summary>
    internal static class PerformanceMods
    {
        /// <summary>The section's name, the same in the bundle and in every single release.</summary>
        internal const string Section = "Performance mods (every mod)";

        /// <summary>Skarif's plugin's GUID starts so (it changed once, _NEW to _WATER).</summary>
        private const string SkarifGuidStart = "com.Skarif.ValheimPerformanceOverhaul";

        private const string SkarifSorterType = "ValheimEngineOptimizer.Modules.NetworkOptimizer";
        private const string SkarifSorterField = "PriorityZDOSorting";

        /// <summary>How often the switch is looked at again while nothing changed, in seconds (another copy of this part,
        /// or another mod, may set it).</summary>
        private const float LookAgainEvery = 2f;

        internal static ConfigEntry<bool> SkarifSendOrder { get; private set; }

        // Skarif's switch once found; whether the plugins were looked through for it (once every plugin has started).
        private static FieldInfo skarifSorting;
        private static bool lookedForSkarif;

        // Whether this copy set Skarif's switch false (so it may set it back), and what it wanted when it last looked.
        private static bool switchedOffHere;
        private static bool wantedBefore;
        private static float lookedAt = float.NegativeInfinity;
        private static bool failed;

        /// <summary>Binds the section (from ChestSettings.Bind, so every module that uses the service binds it).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            SkarifSendOrder = settings.BindShared(Section, "SkarifSendOrder", true,
                "Skarif's mod: send in the game's own order", 10,
                "Only matters while Skarif's Valheim Performance Overhaul is installed. That mod makes your game send its " +
                "updates to the server nearest first, every time (it has no setting for this). In a busy base something " +
                "nearer always has an update waiting, so the news that your game handed a chest to the server, or took " +
                "from it, can wait for minutes: your game and the server each think the other has the chest, nobody can " +
                "use it, and building, crafting and fetching from chests a few dozen metres away fail on a server. On: " +
                "the pack switches off just that part of Skarif's mod while the game runs, so your game sends in the " +
                "game's own order again (and, when you host, what your game sends your friends); the rest of Skarif's " +
                "mod is untouched. Off: Skarif's mod as it comes.");
        }

        /// <summary>Every frame, from ChestClaims.Tick (which runs once a frame while any module using the service runs):
        /// the first time, finds Skarif's switch (every plugin has started by then); then, whenever the setting changed and
        /// every few seconds besides, sets the switch to match.</summary>
        internal static void Tick()
        {
            if (failed)
            {
                return;
            }
            try
            {
                if (!lookedForSkarif)
                {
                    lookedForSkarif = true;
                    skarifSorting = FindSkarifSwitch();
                }
                if (skarifSorting == null)
                {
                    return;
                }
                // A copy whose module never bound the setting leaves Skarif's switch alone (0.36.1's review, M1).
                bool wanted = SkarifSendOrder != null && SkarifSendOrder.Value;
                float now = Time.realtimeSinceStartup;
                if (wanted == wantedBefore && now - lookedAt < LookAgainEvery)
                {
                    return;
                }
                lookedAt = now;
                wantedBefore = wanted;
                bool sorting = (bool)skarifSorting.GetValue(null);
                if (wanted && sorting)
                {
                    skarifSorting.SetValue(null, false);
                    if (!switchedOffHere)
                    {
                        switchedOffHere = true;
                        ZLog.Log("[QoL Mods chest service] Skarif's Valheim Performance Overhaul is loaded: its \"nearest first\" " +
                                 "order for what your game sends the server is switched off, so chests handed over and taken " +
                                 "from reach the server in their turn (\"Skarif's mod: send in the game's own order\", under \"" +
                                 Section + "\"). The rest of it is untouched.");
                    }
                }
                else if (!wanted && switchedOffHere)
                {
                    switchedOffHere = false;
                    if (!sorting)
                    {
                        skarifSorting.SetValue(null, true);
                    }
                    ZLog.Log("[QoL Mods chest service] \"Skarif's mod: send in the game's own order\" is off: this mod no " +
                             "longer switches Skarif's \"nearest first\" order off.");
                }
            }
            catch (Exception error)
            {
                failed = true;
                ZLog.LogWarning("[QoL Mods chest service] Skarif's mod's sending order couldn't be changed, and is left as it is: " +
                                error.Message);
            }
        }

        // Skarif's switch, when his plugin is loaded and has it as 8.2.4 had it; null otherwise (said once in the log when
        // his plugin is there but the switch isn't).
        private static FieldInfo FindSkarifSwitch()
        {
            foreach (BepInEx.PluginInfo info in Chainloader.PluginInfos.Values)
            {
                if (info?.Metadata == null || info.Instance == null ||
                    !info.Metadata.GUID.StartsWith(SkarifGuidStart, StringComparison.Ordinal))
                {
                    continue;
                }
                Type sorter = info.Instance.GetType().Assembly.GetType(SkarifSorterType);
                FieldInfo field = sorter?.GetField(SkarifSorterField, BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly && !field.IsLiteral)
                {
                    return field;
                }
                // Not its version in the message: 8.2.4 calls itself 8.0.0 (0.36.1's review).
                ZLog.LogWarning("[QoL Mods chest service] Skarif's Valheim Performance Overhaul is loaded, but its switch " +
                                "for the \"nearest first\" sending order isn't where version 8.2.4 " +
                                "had it, so it's left on. On a server, chests a few dozen metres away may then fail to come " +
                                "for minutes at a time; switching Skarif's mod off cures it.");
                return null;
            }
            return null;
        }
    }
}
