# Changelog

## 0.3.3 (2026-09-27)

- **Thumbnails the chests can't cover fade to 0.1 by default** (was 0.3), miikeskii's call. A settings file that has the setting already keeps its value.
- **The bundle it belongs to is renamed miikeskii's QoL Pack**, and so are the mentions of him as its maker. This mod's own package stays `drumcowski-AddToCart` until it is published on its own.

## 0.3.2 (2026-09-27)

drumcowski's two points on 0.3.1:

- **Thumbnails the chests can't cover really are grey now.** 0.3.1's grey never happened: it looked for the game's icon material by the wrong shader name and found nothing, so they only faded, in colour. The mod now makes its own grey copy of each picture, a few a frame, the first time it's needed.
- **A slider for how faded they are:** "Opacity of thumbnails the chests can't cover", 0.05 to 0.9, 0.3 by default (as 0.3.0 faded them). It applies to GreyAndFaded and Faded; Grey stays opaque.
- **Large is the default window size.** Normal and Extra large stay as settings. A settings file written by 0.3.1 keeps the size it has: pick Large there once.
- The thumbnails are drawn with Unity's own material again, as in 0.3.0.
- **The Shovel** that Take didn't list was vanishing from its chest when the chest closed: not Add to Cart's doing (drumcowski's own finding).

## 0.3.1 (2026-09-27)

drumcowski's seven points from his test of 0.3.0:

- **Window size**, a new setting: Normal, Large (a quarter bigger) or Extra large (half as big again, or as big as fits your screen). The text stays sharp at every size: the window now has a canvas of its own, drawn at the game's GUI scale times the size.
- **Thumbnails the chests can't cover were to be grey and faded**, the build menu's own grey (the game's icon material) under a fade. A new setting picks the look: grey and faded, grey only, or faded in colour (fainter than 0.3.0's). *(The grey didn't happen: see 0.3.2.)*
- **No coloured squares** beside the biome headings.
- **The line above the results** is bigger and says only what is shown and how many ("All  229 recipes"); Group and Sort say the rest.
- **The headings of the list on the left** (Your Lists, Kinds, Made At …) are orange, and a click folds a whole section away or brings it back. Folded headings are kept with your character, per tab.
- **Right-click anywhere on a cart line** takes it out of the cart, as × does.
- **The hover line shows the thumbnail** of what is under the pointer beside its description.
- **The Take tab says why** when a search finds nothing but a chest loaded near you holds something of that name: the chest is out of range, someone else has it open, it is someone else's private chest, the last one of an item stays with "leave one", and so on. It is written to the log too. (A Shovel from another mod, in a chest, wasn't listed on Take in drumcowski's test; this names the reason next time.)

## 0.3.0 (2026-09-27)

drumcowski's feedback on sorting, filtering and grouping, as agreed on 2026-09-27: one way to browse, the same in every mod.

- **A list on the left picks what shows:** All, your favorites and lists, then each tab's own. Build: the game's categories (Building Structures opening to its six) and materials (Wood, Stone, Metal, Hides and Fabrics, Other, each opening to its members). Craft: the kinds, and the stations (Made At) in the order you can first build them. Take: the families (Gear, Wood, Stone and Minerals, Metals …), each opening to its members. Only what has something in it shows, with how many.
- **Headings follow the pick.** Group's default, Auto, uses the next level down and says so, "Auto (Material)": walls by what they're made of, weapons by type, food by Ready to Eat and Cooking Ingredients. Group offers only choices that would change the view: Equipment Slot where gear shows, Preparation (Simple Foods, Prepared Dishes, Cooking Ingredients) where food shows, never the one the pick already fixes.
- **Progression is the default order:** Meadows to the Deep North, then the game's own order. Sort also offers Name; Health, Stamina, Eitr and Duration where food shows (the number shows in each tile's corner); Count, Recent and Frequent where they would change the order.
- **A line above the results** says what is shown, how many, how it is grouped and sorted, with one click back to everything and one to clear the filter.
- **Your lists are the game's own favorite categories.** Middle-click stars a thumbnail and opens a checklist of your lists, with Add List and Remove from Favorites; a list made here shows in the build menu's Favorites, and the other way round. One star per thing: Lox Meat Pie starred on Craft is starred on Take. Right-click a list to rename it; delete it in the game's build menu, which asks first. 0.2.0's own favorites (recipes and Take items) move across once.
- **The filter** (F focuses it) searches the whole tab: names, what things cost, categories and families, biomes and stations; every word must match ("stone floor"). The list on the left shows where the hits are, and a pick there narrows the search.
- **New settings:** expandable families; each tab remembering its group and sort (kept with your character); the default group and sort; the order of Build's categories and materials, Craft's kinds and stations, and Take's families.
- **Seasonal pieces and recipes** show while in season, as in the game's build menu.
- **Locate** has a setting, off by default: opening any one of the chests clears every label.
- **Coal counts as a Meadows item:** the cooking stations and the oven burn food into coal. What's made from coal moved with it (the Sign, two banners, the Cape and Hood of Oden).
- **The Group and Sort lists** light up the choice under the pointer and keep long names on one line.
- **Retired:** the setting "Sort by" (each tab now keeps its own Group and Sort, and Biome became a way to group). Its line may stay in an old settings file, where it does nothing.
- **The classification moved into the bundle's catalogue part** (`parts\Catalogue`: families, kinds, biomes, orders and the browsing rules), so every mod of the bundle sorts and groups the same way.

## 0.2.0 (2026-09-26)

drumcowski's eight points on 0.1.1:

- **A Take tab:** every item in the chests in range, each shown once with how many there are. The cart takes the item itself, exactly as many as it says. A count can be typed into a cart line.
- **Craft lists what the processing stations make:** the Fermenter's meads and potions, the smelters', kilns' and blast furnace's output (with the smelter's coal), the windmill, the spinning wheel, the eitr refinery, the cooking stations and the oven.
- **Hover shows each cost as "X/Y"** (what one takes, what the chests hold); X is red when that's what the chests can't cover.
- **The mouse wheel scrolls two rows a notch** (a setting), instead of a few pixels.
- **Sort by** Name, Count, Biome (with a heading per biome), Recent or Frequent. Each item's biome is worked out from the game's own data: where the world puts what gives it, then through what it's made from and at which station. It is written to the log once per world, with how many items it could place.
- **Locate**, on Take: a label over every chest holding the item, seen through walls, gone when that chest is opened; Stop locating clears them.
- **Favorites:** middle-click, and a "Favorites first" switch. A build piece's favorite is the game's own, both ways.
- **Comfort:** a comfort piece shows its comfort in the thumbnail's corner, and its group on hover.
- **Ready for Reserve Stock:** chests carrying its mark are never taken from, and are counted apart.
- **Also new:** Shift and Ctrl add more at once; leave-one now keeps back only stackable items; Take and the chest service tell gear's levels apart.

## 0.1.1 (2026-09-26)

- **The window lists only what you build or craft.** Left out now: the hoe's terrain actions (Raise Ground, Paved Road), the serving tray's food placements, and the cultivator's plantings, unless the new setting "List plantings" is on (off by default). The hammer's pieces and every recipe stay as they were.
- A thumbnail of a recipe that makes more than one shows how many (×5, ×20), so Bronze and Bronze ×5 are told apart.
- The log says once what the window lists and what it leaves out, and why.
- 0.1.0 worked in drumcowski's game, alone; the test with a friend is still to come.

## 0.1.0 (2026-09-25)

- **First version**, as a mod of drumcowski's QoL Mods and on its own.
- A window (key K) of every building piece and recipe your character knows, with Build and Craft tabs, groups and a search box.
- A cart with −, + and ×; a running preview of each material (have/need, from the chests, short), the weight and the free slots after a fetch.
- A thumbnail greys out when the chests can't cover one more of it after the rest of the cart; clicking it asks "Add all available?".
- Fetch takes only what you're short (a setting) from the chests within 150 m (a setting) that you could open by hand (or only those you built, a setting), leaving one of each item in each chest as AzuCraftyBoxes does (a setting).
- Each chest is asked from its owner with the game's own open request before anything is taken; a chest a friend has open is skipped.
- Uses the chest service shared by the bundle's mods (`parts\Chests`), in its first version.
- Not yet tested in game.
