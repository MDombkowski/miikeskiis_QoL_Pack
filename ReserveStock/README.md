# Reserve Stock

Made with AI. Reserve a chest, and quick stack and crafting from chests leave it alone: the stock you keep for later stays where you put it.

## What it does

- **Reserve a chest** with the padlock button beside Place stacks in its window, or press **Left Alt + E** while pointing at it, without opening it. The same again frees it. The button shows a closed orange padlock while the chest is reserved, an open grey one while it isn't.
- **Quick stack leaves it alone.** The pack's own quick stack, StoreStash, never stacks into a reserved chest, and neither does HexQuickStackStorage's (P, and its Q button), for as long as you still run it.
- **Crafting from chests leaves it alone.** Craft from Chests never takes from a reserved chest for crafting, building or stations, and Add to Cart never fetches from one. In Craft from Chests' menus, an amount a reserved chest holds more of shows a small padlock with how many are reserved; its setting "Reserved chests" can hide the padlock, or let crafting use reserved chests after all.
- **Pointing at a reserved chest** shows a line, Reserved, in its hover text.
- **By hand, nothing is blocked.** Taking, adding, Place stacks in its window and holding E on it all work as usual, since they're a choice made at that one chest. A setting makes Place stacks and holding E leave reserved chests alone too.

## Good to know

- **Chests far away** (the chest service it carries): On a server, the game hands a chest only to a player within about 64 to 96 m of it; a chest farther out that no game can answer for is claimed by your own game when it's needed (since this version, with the setting "Chests beyond the server's reach" switched on: off out of the box), as the game's portals do, and used once the server's newest copy is in, between the server's two-second rounds: so it can take a second or two longer than a near chest. Chests your game hasn't loaded can't be used: at the usual "Classic" simulation distance that's beyond about 128 m (up to 160 m, more toward the corners of the area).
- **The mark is saved on the chest,** in the world, so it lasts through restarts, and every friend running miikeskii's QoL Pack (or this mod, or Craft from Chests) leaves the chest alone too. It works on carts and ships' chests as well, never on a gravestone or a chest the world made.
- **Anyone who could open a chest can reserve or free it,** as the game's own rules for opening it say (a personal chest only its builder, a warded one only those on the ward). Not while someone has it open: Left Alt + E then says so.
- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **Keyboard and mouse only:** the key and the button have no gamepad control.
- **What it can't reach:** a friend without the pack (or still on AzuCraftyBoxes) can still quick stack into, and craft from, a reserved chest. So can a mod on the server that puts items lying on the ground into chests, stacks a player's items into chests or feeds smelters from chests nearby (miikeskii's Server-Side QoL Pack leaves reserved chests alone). And two mods write to a chest without asking its owner first, HexQuickStackStorage's quick stack and MyLittleUI's rename: in the second or so before a new mark reaches a friend's game, a friend's quick stack or rename of that chest could, rarely, put the chest back as it was before the mark.
- **Switched off,** its button, key and hover line go, and HexQuickStackStorage's quick stack uses reserved chests again. The chests keep their mark, and StoreStash, Craft from Chests and Add to Cart still leave them alone.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library
- HexQuickStackStorage is optional: without it, its patch there has nothing to do, and the rest works as usual.

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-ReserveStock-<version>.zip`. Keep the author `drumcowski` and the name `ReserveStock` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `ReserveStock.dll` into a folder named `drumcowski-ReserveStock` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.reservestock.cfg`, which appears after the first start; in the bundle, in the section "Reserve Stock" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: no button, key or hover line, and HexQuickStackStorage's quick stack uses reserved chests again. The chests keep their mark, and the pack's own mods still leave them alone. |
| Key to reserve the chest you point at | Left Alt + E | Reserves the chest you point at, or frees it. Left Shift + E is MyLittleUI's rename. |
| Button in the chest window | on | The padlock button beside Place stacks. |
| Say "Reserved" when pointing at one | on | A line in a reserved chest's hover text. |
| Show the key when pointing at a chest | off | The key and what it does, in every chest's hover text. |
| Place stacks leaves reserved chests alone too | off | On: Place stacks and holding E do nothing at a reserved chest, and say why. StoreStash has the same setting for its own; either one on does it. |

How Craft from Chests' counts show reserved chests is its own setting, "Reserved chests": a padlock with how many (the default), nothing, or used like any other chest.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Reserve Stock". Its first line says whether it found HexQuickStackStorage. If HexQuickStackStorage has changed so that the mod can't find its quick stack any more, that part stays off and the log says so; the rest keeps working. If the game has changed so that the mod can't find what it needs, it switches itself off and says why. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\ReserveStock.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-ReserveStock-<version>.zip`. Until Reserve Stock is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\Chests\` a copy of the QoL Mods chest service, which reads the mark and claims a chest from its owner for the key; how a module and a part are made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`.
