# Credits: Z-Fighter

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-10-11.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| How the game builds and draws a piece: its wear states (`WearNTear.m_new`, `m_worn`, `m_broken`), its snow (`m_snow`), its snap points (`Piece.GetSnapPoints`), its usage tags (`Piece.m_usage`), its creator (`Piece.GetCreator`), when a piece sets itself up (`WearNTear.Awake`) | Valheim, by Iron Gate | proprietary | `src\Tracker.cs` reads them; nothing copied. |
| The idea of nudging only the drawn copy of overlapping building pieces, never the piece itself | "No Z Fighting" for Satisfactory, by SirDigby (studied for the idea; another engine, nothing taken) | not stated | the whole mod's approach; its rule for which piece moves and by how much is this mod's own. |
| The separating-axis test for two turned boxes | the standard method of computational geometry | — | `src\Logic\Obb.cs`, written for this mod. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| Grounded | miikeskii's QoL Pack | GPL-3.0 | Its rugs are left to it: they are on "Pieces left alone" by default. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
