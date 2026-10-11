# Credits: Map View Rules

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-24 (the Cartur's Map Pins research) and from the other modules' credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Which pin is what: the record file's name, place and line format, and its 0.5 m match | [Cartur's Map Pins](https://github.com/jekkle/cartur-map-pins) 1.6.0, by Cartur (jekkle) | none found (all rights reserved) | `src\Record.cs` and `src\RecordIndex.cs` read the file by the same rules; no code taken, and the file is never written. |
| Its categories, pickable groups and kind names, and the words that sort a pickable into a group | Cartur's Map Pins 1.6.0 | none found | `src\Kinds.cs`: the names, and a table of which group each kind falls in; no code taken. |
| Hiding names within reach of the pointer: Cartur's shows the name of a pin near the cursor when it hides names | Cartur's Map Pins | none found | The idea only; `src\Drawing.cs` does it its own way. |
| Measuring zoom between the map's own limits | Cartur's Map Pins (`Crowding.ZoomOutPercent`) | none found | The idea only; this mod measures the logarithm, so every notch counts the same. |
| The display-hint class for configuration managers: its class name and field names (DispName, Order, CustomDrawer) are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (three field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Cartur's Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/) 1.6.0 | Cartur | none found | Optional. Its record file is read; four of its settings are read (never changed) to know whether it still shrinks pins and measures crowding; its drawing is left alone, and this mod's patches run on either side of its own. |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's drawing of map pins. |
| A configuration manager, such as [shudnal's ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) | shudnal | LGPL-3.0 | Optional: draws the settings, with this mod's "Use map zoom" buttons. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.
- The readout's font is the game's own, taken from the map at run time; nothing ships here.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
