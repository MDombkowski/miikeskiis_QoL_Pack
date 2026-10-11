using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace Grounded
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.grounded.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Grounded". Every one takes effect at
    /// once, also when changed in game with a configuration manager: the mod reads them each time it places. The keys
    /// never change once released (MODULES.md rule 4); the defaults are card A74's.
    /// </summary>
    internal static class Settings
    {
        /// <summary>The game's eleven rugs (the research, section 3): the Straw rug is the "hay".</summary>
        internal const string RugsDefault =
            "rug_straw,rug_deer,rug_bjorn,rug_wolf,rug_fur,rug_hare,rug_asksvin,rug_seal,rug_moose,jute_carpet,jute_carpet_blue";

        /// <summary>The game's floor pieces (the Cage Floor 1x1 under both its names, the black marble blocks and the ice
        /// block, which the build menu files as floors too) and BentoG's MissingPieces' three (the research, section 3).</summary>
        internal const string FloorsDefault =
            "wood_floor,wood_floor_1x1,stone_floor_2x2,iron_floor_1x1,iron_floor_1x1_v2,iron_floor_2x2,blackmarble_floor," +
            "blackmarble_floor_triangle,blackmarble_1x1,blackmarble_2x1x1,blackmarble_2x2x2,ashwood_floor_1x1," +
            "ashwood_floor_2x2,ashwood_deco_floor,piece_grausten_floor_1x1,piece_grausten_floor_2x2," +
            "piece_grausten_floor_4x4,piece_icecube,stone_floor_1x1,wood_floor_triangular1,wood_floor_triangular2";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<KeyMode> HowKeyWorks { get; private set; }

        internal static ConfigEntry<WhichPieces> Pieces { get; private set; }

        internal static ConfigEntry<string> Rugs { get; private set; }

        internal static ConfigEntry<bool> RugGroup { get; private set; }

        internal static ConfigEntry<string> Floors { get; private set; }

        internal static ConfigEntry<bool> FloorCategory { get; private set; }

        internal static ConfigEntry<bool> GroundIsFloor { get; private set; }

        internal static ConfigEntry<NoFloor> WithoutFloor { get; private set; }

        internal static ConfigEntry<HowLow> Height { get; private set; }

        internal static ConfigEntry<bool> KeepPickable { get; private set; }

        internal static ConfigEntry<float> LowerBy { get; private set; }

        internal static ConfigEntry<float> Step { get; private set; }

        internal static ConfigEntry<Overlap> Overlapping { get; private set; }

        internal static ConfigEntry<LookUnder> Look { get; private set; }

        internal static ConfigEntry<bool> PlansCount { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first. All in General: one heading in F1, in the bundle and alone.
            Enabled = settings.Enabled;

            // Left Alt: no game button uses it (ZInput.ResetKBMButtons, 1.0.17); Left Alt + E is Outfitter's and Reserve
            // Stock's key, but while building E only picks a snap point, which rugs don't have (the card's decision 1).
            Key = settings.Bind(ModuleSettings.General, "Key", new KeyboardShortcut(KeyCode.LeftAlt),
                "Key", 99,
                "The key (or mouse button: Mouse 0 to Mouse 4; the game doesn't read Mouse 5 and 6) for laying pieces " +
                "on the floor. Left Alt by default: the game uses it for nothing. Empty it to have the game's own " +
                "placement always (unless \"How the key works\" is \"Always on\"). A key the game can't read counts " +
                "as never held, and the log says so.");

            // KeyState takes the key apart once per value (not every frame); this tells it a new one came.
            Key.SettingChanged += (sender, args) => KeyState.KeyChanged();
            KeyState.KeyChanged();

            HowKeyWorks = settings.Bind(ModuleSettings.General, "KeyMode", KeyMode.Hold,
                "How the key works", 98,
                "Hold it (the default): pieces lie on the floor while you hold the key. Press once on, once off: a " +
                "press and release while building switches it on, the next off, with a message in the middle of the " +
                "screen (leaving the game with Alt-Tab switches nothing). " +
                "Always on: no key needed, as for a gamepad.");

            Pieces = settings.Bind(ModuleSettings.General, "Pieces", WhichPieces.Rugs,
                "Which pieces", 97,
                "The rugs (the default): only a rug in your hand (one on the list below) lies on the floor. Any " +
                "piece: whatever you place with the hammer while the key works looks through the rugs to the floor, " +
                "a table or a chair too; the hoe's, the cultivator's and the serving tray's are left to the game.");

            Rugs = settings.Bind(ModuleSettings.General, "Rugs", RugsDefault,
                "Rugs", 96,
                "The rugs, by their prefab names (the names in the game's data), separated by commas: looked " +
                "through, never landed on, and laid on the floor when in your hand. The game's eleven by default " +
                "(rug_straw is the Straw rug).");

            RugGroup = settings.Bind(ModuleSettings.General, "RugComfortGroup", true,
                "Rugs: also the game's rug comfort group", 95,
                "On (the default): every piece that gives the game's rug comfort counts as a rug too, so another " +
                "mod's rugs work without naming them.");

            Floors = settings.Bind(ModuleSettings.General, "Floors", FloorsDefault,
                "Floors", 94,
                "The floors, by their prefab names, separated by commas: what a rug lands on. The game's floor " +
                "pieces by default, the black marble blocks and the ice block too, and BentoG's MissingPieces' three.");

            FloorCategory = settings.Bind(ModuleSettings.General, "FloorCategory", true,
                "Floors: also everything filed under Floor", 93,
                "On (the default): every piece the build menu files under Floor counts as a floor too, so another " +
                "mod's floors work without naming them.");

            GroundIsFloor = settings.Bind(ModuleSettings.General, "GroundIsFloor", false,
                "The ground counts as a floor", 92,
                "On: a rug over the bare ground lands on it the same way. Off (the default): only floor pieces count.");

            WithoutFloor = settings.Bind(ModuleSettings.General, "NoFloor", NoFloor.GameOwn,
                "With no listed floor behind the rugs", 91,
                "What happens when the first thing behind the rugs isn't a listed floor (a table, a wall, the ground). " +
                "The game's own placement (the default): as without the mod. Land on whatever is behind the rugs: on " +
                "it as it is, at the height the game gives it.");

            Height = settings.Bind(ModuleSettings.General, "HowLow", HowLow.WhatYouSee,
                "How low", 90,
                "Down to what you see (the default): the rug's lowest drawn part comes down to just above the floor's " +
                "planks or stones as you see them, which on wood floors is a little lower than the game's own surface. The " +
                "floor's surface as the game sees it: where the game puts a rug on a bare floor.");

            // Not on the card: added by the build (45-B) when the hammer's remove turned out to take the first collider its
            // ray meets (Player.RemovePiece), which a rug sunk into the floor's collider can't be.
            KeepPickable = settings.Bind(ModuleSettings.General, "KeepPickable", true,
                "Keep rugs where the hammer can point at them", 89,
                "On (the default): a rug never goes so low that it sinks into the floor's invisible box, so the " +
                "hammer can still point at it to remove or copy it. Thin rugs (wolf, lox, asksvin, moose) and the deer " +
                "rug then stay two or three centimetres above wood floors and about one above stone, about half as " +
                "high as without the mod. Off: rugs go all the way down, but most can then only be removed by " +
                "removing the floor under them first: the hammer points at the floor.");

            LowerBy = settings.Bind(ModuleSettings.General, "LowerByCm", 0f,
                "Lower by (cm)", 88,
                "Lowers every piece laid on the floor by this much more, in centimetres. 0 by default. While \"Keep " +
                "rugs where the hammer can point at them\" is on, a rug still stops where the hammer can point at it.",
                new AcceptableValueRange<float>(0f, 10f));

            Step = settings.Bind(ModuleSettings.General, "StepCm", 0.5f,
                "Step between overlapping rugs (cm)", 87,
                "A rug laid over rugs already on the floor is kept at least this far, in centimetres, from any of them " +
                "drawn at nearly its own height, so two rugs don't flicker where they overlap. Rugs drawn higher or " +
                "lower than that are left as they are. 0.5 by default; 0 keeps no distance.",
                new AcceptableValueRange<float>(0f, 5f));

            Overlapping = settings.Bind(ModuleSettings.General, "Overlap", Overlap.OnTop,
                "Overlapping rugs", 86,
                "A step above them (the default): a new rug lies a step above the rugs under it that are drawn at " +
                "nearly its height (the same kind of rug, laid the same way), so it shows on top of those. A rug drawn " +
                "higher (a fur rug under straw) stays above, as it looks anyway. As low as it can without touching " +
                "another: a row of rugs, each over the last, stays low instead of climbing a step each time, but a new " +
                "rug may show under an older one.");

            Look = settings.Bind(ModuleSettings.General, "LookUnder", LookUnder.Cursor,
                "Look for the floor", 85,
                "Under the cursor (the default): the floor where you point. Under the whole rug: also under its " +
                "corners, and the highest floor wins, so a rug across two floors at different heights doesn't sink " +
                "into the higher one.");

            PlansCount = settings.Bind(ModuleSettings.General, "PlansCount", true,
                "PlanBuild plans count as their pieces", 84,
                "On (the default): a PlanBuild plan of a rug or a floor counts as that rug or floor. Off: plans are " +
                "like any other piece.");
        }
    }
}
