# How a mod becomes a module of miikeskii's QoL Pack

**Every Valheim mod of miikeskii's is written once, as a module, and released two ways:** inside the bundle, *miikeskii's QoL Pack* (one plugin, one section of its settings per mod), and on its own, as its single release. Set up by session 6-B on 2026-09-25 (card A10). The design and why it was chosen: card A10 and the research it links, in the vault.

This page is the convention. Follow it from a mod's first line; it is what keeps "one bundle" and "each mod on its own" both possible without reworking anything.

## The shape

```
Games\Valheim\
├─ QoLMods\                the bundle, and only the bundle
│  ├─ QoLMods.csproj       compiles its own src\, kit\ and parts\, plus every module's src\ (one Compile line each)
│  ├─ src\Plugin.cs        the bundle's plugin: lists the modules, runs each one with the kit
│  ├─ kit\                 THE KIT, the one copy to edit: runs a module (QoLModule, ModuleHost, ModuleSettings …)
│  ├─ parts\<Part>\        THE PARTS, the one copy to edit of each: code several modules share (see "Parts")
│  ├─ tools\               package.ps1 (both zips) · sync-kit.ps1 · make-icon.ps1
│  └─ MODULES.md · README.md · CHANGELOG.md · icon.png
└─ <Mod>\                  one folder per mod, self-contained, so it can be published on its own with its history
   ├─ <Mod>.csproj         builds the single release from everything in the folder
   ├─ src\                 the module: all the mod's own code (the bundle compiles this folder from here)
   │  └─ Module.cs         its entry: class Module : QoLModule
   ├─ standalone\Plugin.cs its single release's plugin (never compiled into the bundle)
   ├─ kit\                 a copy of the kit, kept identical by QoLMods\tools\sync-kit.ps1; never edited here
   ├─ parts\<Part>\        only if it uses a part: a copy, kept identical the same way; never edited here
   ├─ tools\package.ps1    its own zip
   └─ README.md · CHANGELOG.md · CREDITS.md · (icon.png only once it is released alone)
```

XPortalMapPicker is the worked example of every rule below.

## What a module must do

1. **Live in its own namespace**, named exactly as its folder (`XPortalMapPicker`), with every one of its classes in it or below it. The bundle holds every module in one DLL, and the kit finds a module's patches by its namespace.
2. **Have one entry class, `src\Module.cs`: `internal sealed class Module : QoLModule`,** with three constants its single release's plugin attribute uses, `Guid`, `Name` and `Version`, and the overrides the kit asks for (`Id`, `DisplayName`, `ModuleVersion`, `SingleGuid`, `EnabledDescription`, `BindSettings`, `Resolve`). Optional overrides: `Update`, `LateUpdate`, `UndoSteps`, `KeepPatchesAfterError`, `SwitchedOffNote`, `ReadyMessage`, `DescribeMissing`, `UpdateTask`, `LateUpdateTask`, `OneSectionInBundle`, `SingleStandsAsideFrom`. Each is explained in `kit\QoLModule.cs`. **Its `Name` becomes a section name in the bundle**, so it avoids the characters BepInEx refuses there (`= ' " [ ] \` and tabs); a module named "Tim's Tweaks" would run alone and fail in the bundle.
3. **Never reach for its host directly.** No `BaseUnityPlugin`, no `ConfigFile`, no `new Harmony(…)`, no `PatchAll`, no BepInEx `Logger`. Log with `Module.Log`, and run any work that could fail inside `Module.Guard("doing what", () => …)`. Module.cs gives both as static members that forward to its host (copy them from XPortalMapPicker).
4. **Bind settings only through `ModuleSettings`,** in `BindSettings`: `settings.Bind(part, key, default, displayName, order, description[, range])`.
   - A **part** is `ModuleSettings.General` or a name of the module's own, such as `"Map"`. In its single release each part is a section of its own file; in the bundle, all parts share one section named after the module (a large module can override `OneSectionInBundle` to get `"<Name> - <part>"` sections instead).
   - **Keys never change** once a version has been released. The bundle takes a single release's settings over by part and key.
   - **Orders are unique across the whole module and below 1000** (the kit refuses 1000 and above). A higher order is listed first.
   - **The Enabled switch is bound by the kit**, first in General, with the module's `EnabledDescription`. Read it through the entry the kit hands over (`settings.Enabled`).
   - Every setting takes effect at once: read its value when it's needed, never cache it at start (his rule 3: a setting for every preference).
5. **Make Enabled work live, both ways.** The kit calls the module's `Update` every frame while the module is active, also while it is switched off, so the module can follow the switch. Off takes back at once everything the module shows or changes; on puts it back without a restart. The kit applies a module's patches once, at start, and leaves them in place while it's off, so **every patch checks the switch first and changes nothing while it's off** (a prefix that could skip the game's own code returns true). The one exception is a patch that keeps the game from acting on the module's own messages, which must keep doing that while the module is off: Trader Circles' catch for the server's answers, which would otherwise become pins. XPortalMapPicker reads its `Settings.Enabled`; the host's `IsOn` says "active and switched on", and Module.cs can forward it as a static like `Log`.
6. **Patch only with `[HarmonyPatch]` classes in its own namespace.** The kit applies them under the module's own Harmony ID (the single release's plugin ID when alone, `modprojects.qolmods.<id in lower case>` in the bundle), and removes them at the next frame if the module fails. A patch body that does real work runs inside `Module.Guard`.
7. **Keep its errors to itself.** The first error inside `Module.Guard` (or `Update`/`LateUpdate`) switches that module off for the rest of the session. `UndoSteps` must then take back everything it shows or changed; each step runs even if the one before it fails. The kit then removes the module's patches at the next frame, unless the module sets `KeepPatchesAfterError` (Trader Circles does, for its catch); every patch of such a module must be safe to leave in place while it is off.
8. **Find other mods at start, never require them in the bundle.** In `Resolve`, look them up in `Chainloader.PluginInfos` and add a plain name to `missing` for anything it can't work without; the kit then leaves only that module off, with the module's `SwitchedOffNote`. In the bundle, add a soft-dependency line for that mod on `QoLMods\src\Plugin.cs`, so it starts before the bundle. The single release may declare it as a hard dependency, as XPortalMapPicker does with XPortal.
9. **Stay client-side** (his rule 6): Jötunn's `NetworkCompatibility(NotEnforced)` is on both plugins, and one level covers the whole bundle. A mod that needs the server is discussed with him first, and ships separately.
10. **Name its Unity objects `<Id>_…`** (`XPortalMapPicker_MapButton`), so modules never find each other's objects.
11. **Keep its credits in `CREDITS.md`** in its own folder, in XPortalMapPicker's shape: code copied in, code or ideas adapted, what it works with at run time, assets, and the note on AI-written code (his rule 5). The bundle's package gathers them all. **Its project file declares that it's made with AI**, as Thunderstore asks of AI-made mods: copy XPortalMapPicker's `AssemblyMetadata` items (`AI_Assisted_Creation`, `AI_Model_Vendor`, `AI_Model`), and say so in its README before it's released on its own.
12. **One version, three places:** `Version` in `src\Module.cs`, `<Version>` in its project, and a `## <version>` entry in its CHANGELOG. Both package scripts refuse a mismatch.

## Its single release

`standalone\Plugin.cs` is the whole of it; copy XPortalMapPicker's and change the names and dependencies:

```csharp
[BepInPlugin(Module.Guid, Module.Name, Module.Version)]
[BepInDependency(Jotunn.Main.ModGuid)]
[BepInDependency(Bundle.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // so the bundle starts first, if it's there
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public sealed class Plugin : BaseUnityPlugin
{
    private ModuleHost host;
    private void Awake() => host = ModuleHost.RunAlone(this, new Module(), Logger);
    private void Update() => host?.Update();
    private void LateUpdate() => host?.LateUpdate();
}
```

- **Its plugin ID is `modprojects.<id in lower case>`, and permanent:** it names the settings file, which the bundle reads once to take the settings over.
- **When the bundle is installed and runs the module,** `RunAlone` stands aside: it logs why and returns null, so the game is never patched twice. It asks the bundle through the bundle plugin's public `ModuleVersion(id)`, which answers only for a module the bundle started.
- **A single release from before the mod was a module doesn't know how to stand aside.** Such a module sets `SingleStandsAsideFrom` to its first single release that does (XPortalMapPicker: 0.4.0). At the first frame, when every plugin has started, the bundle checks: with an older copy running, the bundle's copy of that module steps back instead, and its log line says to update or remove the old copy. A mod that was a module from its first release leaves it null.
- **No icon of its own until it is released alone** (his rule, 2026-09-25). The icon is only the package's picture in the mod manager, never seen in game, and while a mod ships inside the bundle only the bundle's icon is seen. So a new module has no `icon.png` and no `make-icon.ps1`; its `tools\package.ps1` uses its own `icon.png` if there is one, else the bundle's `..\QoLMods\icon.png` (copy Map Zoom's). XPortal Map Picker keeps the icon it had from its own releases before the bundle.
- **Its package** is `drumcowski-<Folder>-<version>.zip`. Author and name never change once a friend has imported it (LESSONS_LEARNED, in the vault). The bundle's is `miikeskii-miikeskiis_QoL_Pack-<version>.zip` since 0.6.4, renamed at his word (card A30) so that his name is in the title Thunderstore shows (`miikeskii-QoL_Pack` for 0.6.3, whose Thunderstore listing was rejected; `drumcowski-QoL_Mods` before). The single releases follow when they are published.

## Adding a module to the bundle

1. **Create the mod's folder in its final place,** `Games\Valheim\<Mod>\`, before its first commit: it is never moved or renamed after (PROJECT.md). Start from Map Zoom's project file, `src\Module.cs`, `standalone\Plugin.cs`, `tools\package.ps1`, README, CHANGELOG and CREDITS.md: Map Zoom (`..\MapZoom\`, 2026-09-25) was a module from its first line, so it is the plainest starting point; XPortalMapPicker shows the extras a mod released before the bundle needs.
2. **`QoLMods\QoLMods.csproj`:** add one Compile line for its `src\`, and any reference it needs that the bundle lacks (to its own project too).
3. **Run `QoLMods\tools\sync-kit.ps1`.** It copies the kit into the new folder's `kit\`, reading the module folders from those Compile lines, and each part the mod uses into its `parts\<Part>\` (create that folder first; see "Parts").
4. **`QoLMods\src\Plugin.cs`:** add `new <Mod>.Module(),` to `Modules()` (the list's order is the order of the sections), and a soft-dependency line for every other author's mod it works with.
5. **The bundle's release:** raise its version (project file, `src\Plugin.cs`, CHANGELOG, whose entry names the modules' versions), and add the mod's row to its README's *What's inside*.
6. **Build both:** `QoLMods\tools\package.ps1` checks the kit copies and the versions, and writes the bundle's zip and every single release's zip.
7. **`Games\Valheim\README.md`:** a row for the mod.

## Parts: code several modules share

**A module never uses another module's code** (each single release must build and run alone). Code that several modules need, such as the chest service, is a **part** instead: shared source, compiled into each plugin that needs it, the way the kit is. Decided by session 9-B on 2026-09-25 (card A11), for the chest service that Add to Cart, our own craft-from-chests (A27) and Reserve Stock (A12) all use.

```
QoLMods\parts\<Part>\      the one copy to edit; the bundle compiles it once, whichever modules use it
<Mod>\parts\<Part>\        a copy in each mod that uses it, kept identical by tools\sync-kit.ps1; never edited there
```

1. **A mod uses a part by having its folder:** create `<Mod>\parts\<Part>\` and run `QoLMods\tools\sync-kit.ps1`, which fills it. The mod's own project compiles it (everything in the folder is compiled); the bundle compiles only its own `parts\`, never a module's copy. `sync-kit.ps1 -Check` (run by the package script) fails if a copy differs, or if a mod's `parts\` names a part that doesn't exist.
2. **A part lives in `namespace QoLMods.<Part>`** (the chest service: `QoLMods.Chests`), with every class `internal`, like the kit. Each plugin holds its own copy of each type, so two plugins carrying the same part never share state or clash.
3. **A part has no settings and no switch of its own.** Each module that uses it passes what its own settings say, each time it asks (the chest service: a `ChestRules`), and hands itself over as a user (`ChestUser`: its name, log and `Guard`), so the part's work for a module runs under that module's rules: an error while serving it switches off only that module (rule 7).
4. **A part's patches are its own, applied once per plugin, at the game's start, and never removed by a host.** A part patches with an explicit `Harmony.Patch` under its own Harmony ID, `modprojects.<plugin assembly>.<part>` (the chest service in the bundle: `modprojects.qolmods.chests`; in Add to Cart's single release: `modprojects.addtocart.chests`), from a `Resolve` that every using module calls in its own `Resolve`; the first call patches, and each call adds to `missing` what the part can't work without. It never uses `[HarmonyPatch]` classes, so no module's host picks them up. Its patches must do nothing unless the part itself is waiting for something, and must be safe to keep for the whole session: a host removes only its own module's patches after an error, and the part's patch may still be needed by another module, or by an answer still on its way.
5. **A part that needs a turn every frame** gets it from each using module's `Update` (the chest service: `ChestClaims.Tick()`), and does its work once a frame however many modules call it. The kit calls a module's `Update` also while it is switched off, so a part keeps turning while any user of it runs.
6. **Two plugins with the same part installed at once** (the bundle and an older single release that doesn't stand aside, or two single releases): each copy is separate, with its own patches, and each must act only on what it started. The chest service catches only answers to its own requests, by chest; if two copies ask the same chest in the same instant, the owner answers only the first (it no longer owns the chest when the second arrives), and the second copy's claim just times out.
7. **Changing a part** is like changing the kit: edit only `QoLMods\parts\<Part>\`, run `tools\sync-kit.ps1`, rebuild the bundle and every mod that uses it, and note it in the bundle's CHANGELOG and in each using mod's CHANGELOG. Keep its members working for every mod that uses it.

**The chest service** (`parts\Chests\`), the first part: finds the chests he may use (`ChestFinder`, from a list its patch on `Container.Awake` keeps), counts what they hold from this computer's copies in one pass (`ChestStock.Survey`), and takes from them safely (`ChestFetch.Start`), claiming each one from its owner through the game's own open request first, and holding it "in use" for a few seconds after (`ChestClaims`).
- **Items are named by a key:** the item's shared name for any quality ("$item_wood"), or `name@quality` for one quality of gear (`ChestStock.KeyOf`, `Parse`).
- **Leave-one keeps back one of each stackable item only**, never a lone piece of gear.
- **Why something is left out,** for telling the player (since bundle 0.6.1): `ChestFinder.WhyNot` says why the rules refuse a chest (the one place those rules are written: `MayUse` asks it), and `ChestStock.Explain` walks every chest loaded on this computer for the items a module asks about, saying for each why a count leaves it out (out of range, someone has the chest open, reserved, a lower world level, the last one kept back …). Add to Cart's Take tab uses it when a search finds nothing. It explains; it never takes.
- **A chest carrying Reserve Stock's mark** (`ChestReserve.Key`, a bool in the chest's record; card A12) is never taken from, and its contents are counted apart in the survey. So Reserve Stock only has to write the mark, and every module that uses the service honours it. How it keeps a friend's chest safe is written at the top of `ChestClaims.cs`. A module that uses it calls `ChestHooks.Resolve(missing)` in its `Resolve`, `ChestClaims.Tick()` in its `Update`, and lists `ChestClaims.ReleaseHolds` among its `UndoSteps` (Add to Cart's `src\Module.cs`). Its tests: a scratch console harness (runbook job 6's Issues and insights), and the two-player test on card A11.

**The catalogue** (`parts\Catalogue\`, namespace `QoLMods.Catalogue`), the second part, added with Add to Cart 0.3.0 (session 11-B, 2026-09-27; card A11): how every mod of the bundle classifies, orders and browses things, so a window in any mod sorts and groups exactly as Add to Cart does (the browsing philosophy, PROJECT.md rule 7). It has no patches, and nothing that runs by itself.
- **Where each thing sits,** as data (`Taxonomy`: a rule change is one line): each item's Take family and member, Gear's sub-headings, its equipment slot, food's preparation and its Craft kind (`Items.Home`); a piece's build categories, its most specific one and what it is made of (`Pieces.Home`). Things are named by the game's prefab names, item types and skills, never by English text, so the rules hold in every language; another mod's things fall back by their game type.
- **Each item's biome of origin** (`Biomes`, worked out once per world from the game's own data; moved here from Add to Cart).
- **Reading the game** (`GameFacts`): the prefab that stands for every prefab sharing a `$` name, each item's facts and home (cached per world and shared, so read-only), a piece's facts, a station's label by its prefab. Every lookup prepares itself; call `Biomes.Build(log)` first if you want its log line (otherwise it runs silently).
- **The things a window lists** (`Listings.Piece`, `Listings.Item`): a piece, a recipe's or station's product, or an item, made into a `Listing` the one way every mod makes them (where it sits, its biome, its order, its name in the game's favorites, and what the filter box searches, as `Browse.SearchText`). A module gives each its own key.
- **The orders** (`Orders`): his orders by default; a module offers each as a one-line setting and passes the lines to `Orders.From` (names left out go last, unknown names are ignored).
- **The browsing rules** (`Browse`): a module asks for the list on the left (`Tree`, then `Shown`: no empty rows), what a pick shows (`InPick`; a picked list is kept by name as well as number, `ListPick` and `Resolve`), the filter (`Matches`), the Group choices with Auto (`GroupOptions`), the Sort choices (`SortOptions`), and the headings in order (`Sections`, a stable sort).
- **His lists** (`GameLists`): favorites and lists on the game's own `FavoritePieceList`, pieces under their prefab name and items under `item:` and theirs; it keeps the three cautions of the build-menu research (check before adding, only valid list numbers, never the save's bytes). A module that uses it calls `GameLists.Resolve(missingOptional)` in its `Resolve`.
- **Its tests:** the scratch harness checks the port item by item against the mock-up's own data script and its page's script on every pick (runbook job 6's Issues and insights, 2026-09-27).

## Changing the kit

Edit only `QoLMods\kit\`. Then run `tools\sync-kit.ps1`, rebuild the bundle and every single release, and note the change in the bundle's CHANGELOG. Keep the kit's members working for the modules that already use them: every module compiles against it. `tools\sync-kit.ps1 -Check` (run by the package script) fails if any mod's copy differs.

## Testing

The bundle is what he tests every time. A single release is tested in game when a friend wants one. Every test follows PROJECT.md's *How to run*: back up first, pack the zips, and he imports them.

## Building and packing the bundle

The bundle's README is its page on Thunderstore, so how to build it is here. The pack is GPL-3.0 (`LICENSE`, his pick, 2026-09-27), and its source is public at https://github.com/MDombkowski/miikeskiis_QoL_Pack (the README and the package's website point there). The package itself never carries the source: Thunderstore's moderators rejected 0.6.3 for it. `tools\package.ps1 -SourceTo <that repository's folder>` mirrors this folder and every module's into it, side by side, without `bin\`, `obj\` or `dist\`, with `LICENSE` and a README at its root. It refuses a source file that names this computer's user, user folder or git email, so keep machine paths out of the source (`$(APPDATA)` in a project file, `$env:APPDATA` in a script).

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\QoLMods.dll`.
- **What's where:** this folder holds only the bundle: its plugin (`src\Plugin.cs`), the kit (`kit\`), which runs every mod as a module, and the parts several mods share (`parts\`: the chest service, and the catalogue, which says where each thing sits and how every mod browses). Each mod's code stays in its own folder beside this one and is compiled from there, so the bundle and each mod's own release come from the same files. How a module and a part are written and added: the sections above.
- **Package:** `tools\package.ps1` checks that every mod's copy of the kit and of each part it uses matches this one (`tools\sync-kit.ps1 -Check`), that the project file, `src\Plugin.cs` and `CHANGELOG.md` agree on the version, that `README.md` and `CHANGELOG.md` are what Thunderstore takes (no byte-order mark, under 100,000 characters), builds the bundle, and writes `dist\miikeskii-miikeskiis_QoL_Pack-<version>.zip`. Its `CREDITS.md` gathers every mod's credits. It then builds each mod's own zip with that mod's `tools\package.ps1`; `-BundleOnly` skips that. The icon is drawn by `tools\make-icon.ps1`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside the mod folders. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.
