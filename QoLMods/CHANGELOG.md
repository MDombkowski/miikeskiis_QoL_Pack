# Changelog

Each mod inside keeps its own version and changelog; this one lists the bundle's releases and the versions inside each.

## 0.6.4 (2026-09-27)

- **On Thunderstore as miikeskiis QoL Pack:** the package is `miikeskii-miikeskiis_QoL_Pack` (it was `miikeskii-QoL_Pack`, whose 0.6.3 listing was rejected), so that miikeskii's name is in the title; a Thunderstore title can't hold an apostrophe. A mod manager takes it for a new mod: remove `miikeskii-QoL_Pack` or `drumcowski-QoL_Mods` before installing this one. The plugin ID, `modprojects.qolmods`, is unchanged, so the settings file carries over.
- **The source left the package**, at Thunderstore's request (0.6.3 was rejected: "Please remove the source files"). It's at https://github.com/MDombkowski/miikeskiis_QoL_Pack, linked from the README and the package's website.
- **Made with AI, declared** where Thunderstore asks of AI-made mods: at the top of the README, and in the DLL's AssemblyMetadata (`AI_Assisted_Creation`, `AI_Model_Vendor`, `AI_Model`), set in every project file.
- **The README** says the pack is still in the works and listed only under the AI Generated category for now. The description names the pack and says it's made with AI.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0**, **Trader Circles 0.1.0** and **Add to Cart 0.3.3** are unchanged, and so are the parts.

## 0.6.3 (2026-09-27)

- **Renamed: miikeskii's QoL Pack** (was drumcowski's QoL Mods), to publish it. Its package is `miikeskii-QoL_Pack` (was `drumcowski-QoL_Mods`): a mod manager takes it for a new mod, so remove `drumcowski-QoL_Mods` before installing this one. The plugin ID, `modprojects.qolmods`, is unchanged, so the settings file carries over.
- **The kit** says the new name (`Bundle.Name`), which the settings window and the log show.
- **Licence: GPL-3.0**, at miikeskii's pick: `LICENSE` in the package, and the source of every mod in it as `source.zip`. The project files find the mod profile through `%APPDATA%` instead of one user's own path.
- **Ready for Thunderstore:** the README says the pack is made for miikeskii's friends and not currently intended for broad use, and names the other mods it needs, changes or reads. The description says so too, and names the mods inside without their versions. The credits are brought up to date (how the chests are found) and no longer point to private notes.
- **Add to Cart 0.3.3:** thumbnails the chests can't cover fade to 0.1 by default.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** are unchanged, but for the new name in their texts; so are the parts.

## 0.6.2 (2026-09-27)

- **Add to Cart 0.3.2:** the thumbnails the chests can't cover really turn grey now (0.3.1's grey never happened), with a slider for how faded they are; Large is the default window size.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** are unchanged, and so are the parts.

## 0.6.1 (2026-09-27)

- **Add to Cart 0.3.1:** drumcowski's seven points from his test of 0.3.0.
  - A window size setting (Normal, Large, Extra large), with sharp text at every size.
  - Thumbnails the chests can't cover are grey, the build menu's way, and faded (a setting picks the look).
  - The list on the left's headings are orange and fold away with a click; the line above the results says only what is shown and how many; no coloured squares by the biome headings.
  - Right-click a cart line to take it out; the hover line shows the thumbnail.
  - When a Take search finds nothing, it says where a chest near you holds something of that name and why the cart leaves it out.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** are unchanged.
- **The chest service** (`parts\Chests`) can say why it leaves a chest or an item out (`ChestFinder.WhyNot`, `ChestStock.Explain`), for any mod that wants to tell the player; what it counts and takes is unchanged.
- **The catalogue** (`parts\Catalogue`) is unchanged.

## 0.6.0 (2026-09-27)

- **Add to Cart 0.3.0:** one way to browse, as agreed with drumcowski on 2026-09-27.
  - A list on the left picks what shows: the game's categories and materials on Build, kinds and stations on Craft, families on Take, each family opening to its members.
  - Headings follow the pick (Group's Auto), in progression order; Group offers only choices that would change the view, with Equipment Slot for gear and Preparation for food; food sorts by Health, Stamina, Eitr or Duration.
  - Your favorites and lists are the game's own favorite categories, shared both ways with the build menu, for items and recipes too.
  - A filter over the whole tab; seasonal pieces in season; Locate can clear every label at once; each tab remembers its group and sort; the orders of the lists are settings.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** are unchanged.
- **Parts, new: the catalogue** (`parts\Catalogue`): where each thing sits (the Take families and members, the Craft kinds, the build categories, what a piece is made of, each item's biome), the orders, and the browsing rules (the list on the left, Auto headings, which Group and Sort choices appear), with the player's lists on the game's own favorites. Every mod that lists things will use it, so they all sort and group the same way. Each item's biome (`Biomes.cs`) moved here from Add to Cart, and now counts food burnt on the cooking stations: coal is a Meadows item.
- **The chest service** (`parts\Chests`) is unchanged.

## 0.5.0 (2026-09-26)

- **Add to Cart 0.2.0:**
  - A Take tab.
  - The processing stations in Craft.
  - "X/Y" costs on hover.
  - A faster mouse wheel.
  - Sort by name, count, biome, recent or frequent.
  - Locate labels over the chests holding an item.
  - Favorites, shared with the build menu.
  - Comfort numbers.
  - Reserved chests left alone.
- **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** are unchanged.
- **The chest service** (`parts\Chests`) names items by key (any quality, or one quality of gear), counts the chests in one pass, keeps back only stackable items with leave-one, and leaves alone chests carrying Reserve Stock's mark (`ChestReserve`).

## 0.4.1 (2026-09-26)

- **Add to Cart 0.1.1**: its window no longer lists the hoe's terrain actions, the serving tray's food placements or (unless a new setting says so) the cultivator's plantings. **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** unchanged.

## 0.4.0 (2026-09-25)

- **Add to Cart 0.1.0 joins**, beside **XPortal Map Picker 0.4.0**, **Map Zoom 0.1.0** and **Trader Circles 0.1.0** (all unchanged). A window (key K) of everything you can build or craft; fill a cart, see what it needs, and one Fetch takes what you're short from the chests near you, asking each chest's owner first.
- **Parts, new:** code several mods share lives in `parts\` and is compiled once into the bundle. The first is the chest service (`parts\Chests`), which Add to Cart uses and later mods will too. `tools\sync-kit.ps1` now keeps each mod's copy of a part identical too.
- The bundle now also compiles against Unity's `UnityEngine.PhysicsModule` (finding chests) and `UnityEngine.InputLegacyModule` (the key).
- Not yet tested in game.

## 0.3.0 (2026-09-25)

- **Trader Circles 0.1.0 joins**, beside **XPortal Map Picker 0.4.0** and **Map Zoom 0.1.0** (both unchanged). See-through circles on the big map around every place each trader could still be, gone once the trader is found; one switch per trader, all off at first.
- The bundle now also compiles against Unity's `UnityEngine.UIModule` (for Trader Circles' drawing).
- The kit: a module can keep its patches in place after an error switches it off (`KeepPatchesAfterError`), so Trader Circles' catch for the server's answers stays. The other modules are unchanged.
- Not yet tested in game.

## 0.2.0 (2026-09-25)

- **Map Zoom 0.1.0 joins**, beside **XPortal Map Picker 0.4.0** (unchanged). The mouse wheel zooms the big map in 18 notches end to end instead of about 44, a setting that works at once, and toward the mouse pointer.
- The bundle now also compiles against the game's `assembly_utils` (for its input).
- Not yet tested in game.

## 0.1.0 (2026-09-25)

- **First version**, holding **XPortal Map Picker 0.4.0**.
- One entry in the settings window, drumcowski's QoL Mods, with a section per mod, each starting with its own Enabled switch that works at once.
- Each mod runs on its own: its own game patches, and an error in one switches off only that one.
- With a mod's separate release also installed, that copy stands aside. A separate copy too old to know how (XPortal Map Picker 0.3.0 and before) keeps running, and the bundle's copy of that mod stays off instead, with a line in the log. The bundle takes a mod's settings over from its separate release once, the first time it runs it.
- Not yet tested in game.
