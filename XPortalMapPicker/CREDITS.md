# Credits: XPortal Map Picker

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences were read from each project's `LICENSE` file on GitHub on 2026-09-25.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The Map button is built the way Jötunn's `GUIManager.CreateButton` builds XPortal's own Ping button, and styled with Jötunn's `ApplyButtonStyle` | [Jötunn](https://github.com/Valheim-Modding/Jotunn), by the JotunnLib Team | MIT | `src\MapButton.cs` (calls Jötunn; nothing copied) |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [XPortal](https://github.com/SpikeHimself/XPortal) 1.2.25 | SpikeHimself | GPL-3.0 | Required. This mod reads XPortal's portal list and panel, and sets its destination list, by reflection at run time. It doesn't reference or ship XPortal's DLL. The mod builds against XPortal's behaviour, so GPL-3.0 is the safe licence for any public release, and miikeskii's QoL Pack, which carries it, is released under GPL-3.0 (from 0.6.3). |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its GUI manager, and its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's map clicks and XPortal's panel. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- `icon.png` is drawn by this mod's own `tools\make-icon.ps1`.
- The portal markers reuse the game's own portal map icon, taken from the running game; nothing is shipped.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
