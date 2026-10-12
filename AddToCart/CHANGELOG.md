# Changelog

## 0.5.3 (2026-10-11)

- The chest service it carries changed: its settings and messages name no mod it doesn't work with or make room for (a chest the server holds is "the server (a mod running on the server)"), and in **Chests (every mod)**, the list of chest distances says when one of Topper-Upper's distances is 0 and takes the range above. Craft from Chests 0.5.1, Outfitter 0.6.1, Reserve Stock 0.1.13, StoreStash 0.7.3, Topper-Upper 0.4.0 carry it too. Nothing else changes here.

## 0.5.2 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.
- **"Use this range in every mod"** in the shared section **Chests (every mod)**: while it's on, the cart's chest range is the shared **Chest range**, whatever its own says (its own is kept for when it's off). The section also has a button that sets every mod's own range to 0 for good, and lists every chest distance in the pack with what each reaches now.

## 0.5.1 (2026-10-10)

- The chest service it carries talks to miikeskii's Server-Side QoL Pack on the server: it says hello to it, and when the server pack runs Far Fetched, your game learns from it which chests it hands you (a server running Far Fetched alone works as before). Craft from Chests 0.4.1, Outfitter 0.5.4, Reserve Stock 0.1.11, StoreStash 0.7.1 and Topper-Upper 0.2.0 carry it too. Nothing else changes here.

## 0.5.0 (2026-10-09)

- **Leave one, and gear** (miikeskii's feedback of 2026-10-09): "Leave one of each item in chests" says what it always did: it keeps back only things that stack, never your only helmet. A new setting, **Leave one of gear too** (off), keeps one of each piece of gear back as well.
- **True counts**: a switch on the Take tab (and a setting) shows every item's whole count, the one kept back in each chest included, everywhere in the window. A fetch that would then take the last one of something from a chest asks first: **Take it anyway** (emptying as few chests as it can), **Take all but one**, or **Cancel**.
- **The open key is read strictly**: K no longer opens the cart while Ctrl, Alt or the Windows key is held, so another mod's Left Alt + K doesn't open it too. Shift still works, so it opens while you sprint.
- Fixed: "Fetching..." could stay up after a fetch that finished at once; the "Add all available?" question no longer comes back after closing the window with K; F no longer jumps to the filter behind a question.
- The chest service and the catalogue it carries changed: a friend's marked chest whose owner keeps other players' mods out is left alone, the Obliterator is never a chest (StoreStash 0.7.0); quick picks (nothing changes here).

## 0.4.10 (2026-10-08)

- **Skarif's Valheim Performance Overhaul no longer leaves chests stuck** (miikeskii's report of 2026-10-08: building from chests 50 to 60 m away kept failing on his friend's server, and much less with Skarif's mod off). That mod makes your game send its updates to the server nearest first, every time, so in a busy base the news that a chest changed hands could wait for minutes, and your game and the server each thought the other had it. The chest service now switches off just that part of Skarif's mod while the game runs: a new section in F1, **Performance mods (every mod)**, with **Skarif's mod: send in the game's own order** (on). Nothing changes without Skarif's mod.
- **Each item says how far its own chest is:** "Getting Wood (54 m) and Stone (56 m) from chests: place it again in a moment." (miikeskii's format), instead of one distance for them all. A fetch that came up short names each item so.
- **"Beyond the server's reach" only when it is:** a chest your game claimed itself because the server hadn't handed it over (55 m away) was called beyond the server's reach.

## 0.4.9 (2026-10-08)

- **One place for the chest settings every chest mod must agree on**: a new section in F1, **Chests (every mod)**, holds the chest range (up to 250 m now, 150 out of the box), **Chests beyond the server's reach** (now **on** out of the box, one switch for Add to Cart, Craft from Chests, StoreStash and Outfitter instead of four) and **Write chest problems to the log** (on). This mod's own "Chest range" is 0 out of the box, which follows the shared one; set it to give this mod a range of its own. A settings file still at the old 150 moves to 0 once.
- **Honest messages:** when the chests can't give something, it says which item, how far its chest is and why (another player's game has it, the server hasn't handed it over yet, it's beyond the server's reach, or your game can't get it at all), and with the log setting on, writes each chest and its situation to the log. A fetch that came up short names the item and its chest.
- **The chest service it carries changed**: a chest your game can't get at all is left out instead of being asked for and timing out; Far Fetched on the server (a separate server mod) is recognised, and chests it hands you are used at once; a far chest is never claimed where the server would hand it out itself.

## 0.4.8 (2026-10-06)

- **miikeskii's own settings are the defaults now**: "Take only what I'm short" off (a fetch takes the full amounts), the opacity of thumbnails the chests can't cover 0.075 (was 0.1), and "Locate: opening one chest clears every label" on.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.
- **Chests beyond the server's hand-out work now**: on a server, the game hands a chest only to a player within about 64 to 96 m of it, so a chest loaded farther out with nobody near it never answered ("owners didn't answer in time"). The chest service it carries now claims such a chest itself, as the game's own portals do: only one no game can answer for (nobody's, its owner gone from the game, or a dedicated server's far from the world's centre), never a cart's or a ship's; it changes it only once the server's newest copy has come in and between the server's two-second rounds, so such a chest can take a second or two longer than a near one, and it lets it go again right after. It is **off out of the box**: switch on the new setting "Chests beyond the server's reach" to use it (on your own server first). Alone or hosting, and within your own area, nothing changes; on a very slow connection such chests are left out, as before. One rare case stays, written in the chest service: with ServersideQoL on the server, that mod writing the same far chest in the instant your change is late reaching the server could copy or lose that one change. Nothing depends on a performance mod: with or without Skarif's ValheimPerformanceOverhaul (which never touches chests) it works the same.

## 0.4.7 (2026-10-05)

- **The chest service it carries changed** (with StoreStash 0.5.0's "Which chest first"; Craft from Chests 0.3.4 and Reserve Stock 0.1.6 carry it too): a quick stack that asks orders the chests that may take an item by a list of rules (chests set up for it, the most specific setup, the one holding the most of it, personal chests), each rule's first chest filling before the next, and a set-up chest's room goes to what its setup takes before what it merely holds. Add to Cart doesn't quick stack, and its taking is unchanged.

## 0.4.6 (2026-09-30)

- **Item stand filter** (a setting, off out of the box; miikeskii's ask after his test of Item Stand Icons 0.1.0, "browse all standable items together in one filter or tab"): on Craft and Take, the list on the left gains a heading, Fits a Stand, with a row for everything that fits a stand, then the wall item stand, the flat one and the armor stand, each of those three with its mark; a row is picked like any other, the line above the results says what's shown, and Show All × or typing in the filter goes back to everything. A filter rather than a fourth tab: each tab is what a click puts in the cart (a piece, a recipe, an item to take), and standable things are recipes on Craft and items on Take. By Item Stand Icons' rule when it's installed, else the game's own.
- **The armor stand's mannequin** on the thumbnails of items the armor stand takes, beside the item stand arrows, with "Item stand marks" as before (the item stands part's 0.2.0, with Item Stand Icons 0.2.0).

## 0.4.5 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.4.0's chest setups; Craft from Chests 0.3.3 and Reserve Stock 0.1.5 carry it too): a chest can carry a setup, and a quick stack that asks follows it, set-up chests first; a chest reserved while a quick stack's claim waited gets that stack another round without it. Add to Cart doesn't quick stack, and its taking is unchanged. Its catalogue part gains setups (`ItemPicks`), which it doesn't use.

## 0.4.4 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.2.0's personal chests; Craft from Chests 0.3.2 and Reserve Stock 0.1.4 carry it too): it knows the "mine" mark a player can set on a chest, and a quick stack can leave chests someone else marked alone. Add to Cart doesn't quick stack, and its taking is unchanged.

## 0.4.3 (2026-09-30)

- **Item stand marks on the thumbnails**: an item that fits an item stand carries a small mark on its thumbnail, in the middle of its left edge, as the new Item Stand Icons draws on item icons: an up arrow for the wall stand, a right arrow for the flat one, both arrows for both. On the Craft and Take tabs and what a station makes; never on a piece.
- **A new setting, "Item stand marks":** AsItemStandIcons (the default) shows them whenever Item Stand Icons shows its marks, in its look and by its rule, so nothing changes until its "Show the marks" is on; Always shows them whatever Item Stand Icons does (in a plain look without it); Off never.
- The rule, the arrows and the mark come from a part the pack shares, `QoLMods\parts\ItemStands`, which Add to Cart now carries in its own `parts\` folder.

## 0.4.2 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.1.0, the pack's own quick stack, which uses it; Craft from Chests 0.3.1 and Reserve Stock 0.1.3 carry it too): it can now put into chests and move items between them, as safely as it takes (a chest asked of its owner first, added to before anything leaves, sent and held after), and it holds the quick stack itself. Add to Cart itself is unchanged: Fetch and the counts work exactly as before.

## 0.4.1 (2026-09-30)

- **Comfort, as Comfort Build shows it:** a comfort piece's hover now reads "Comfort 2 (Chairs)" (was "comfort 2, in the chair group"), and a piece with no group "Comfort 1 (each kind counts once)". The number in the thumbnail's top-left corner, light blue, is where and how Comfort Build shows it in the build menu too, so it stays.
- **Its grey copies of pictures** (for thumbnails the chests can't cover) now come from a part the pack shares, `QoLMods\parts\Pictures`, since Comfort Build greys pictures the same way; they look and work as before. In miikeskii's QoL Pack, the pictures are redrawn as soon as a grey copy is made, whichever mod made it.

## 0.4.0 (2026-09-29)

- **Fetch takes from a chest a friend has open too, when their game runs miikeskii's QoL Pack** (0.10.0 or later; any of Craft from Chests, Add to Cart or Reserve Stock will do). Your game asks theirs for what you're short; their game takes it out of the real chest, so their open window shows it leave at once, and it lands in your inventory like the rest of the fetch. Only what fits in your inventory is asked for. The counts (the cart's "from chests", the Take tab) include such a chest, and follow what your friend moves in or out of it within a second. **A friend without the pack, or with an older one, keeps their open chest to themselves, as before.**
- The line after a fetch says how many of the chests were a friend's open chest, and if anything didn't fit in your inventory by the time it came (it's dropped at your feet, with a word; this shouldn't happen, since only what fits is asked for).
- If a friend's game doesn't answer within 4 seconds, the fetch goes on without that chest; if the items still come, they land in your inventory, with a word. The rare cases: a player whose game crashes or leaves in the instant between the take and its arrival loses what was on its way; and if your friend's game crashes in the instant right after its take, their chest may still hold what you received.
- **Your own open chests** are shared the same way with friends who fetch from them. The stack you're dragging or splitting is left alone. While every chest mod of the pack is switched off, nothing is shared, and your friends' games are told.
- **The chest service:** if another mod moves a stack out of a chest in the middle of a take (re-sorting the chest as your inventory changes), what had arrived goes back out of your inventory, so nothing is copied (older than this version).
- The Take tab's reason for a chest someone has open now reads "someone else has the chest open, and their game doesn't share it".
- **With MultiUserChest installed:** a chest someone has open is skipped after a second and a half instead of after 5 seconds, and the three seconds after a fetch, during which a friend's E on that chest waits, still hold.

## 0.3.6 (2026-09-28)

- **The chest service it carries changed** (with Craft from Chests and the new Reserve Stock, which share it): a chest reserved with Reserve Stock after a fetch planned it gives nothing when the take comes, and "(and N in reserved chests)" counts what reserved chests hold whole, without "leave one" taken off. Nothing else changes for Add to Cart.
- **The chest service:** renaming a chest with MyLittleUI (Shift + E) while the pack is taking from it no longer opens the chest's window under the rename box (its check now runs after MyLittleUI's).

## 0.3.5 (2026-09-28)

- **The chest service it carries was fixed** (with Craft from Chests, which shares it): on a server that sorts chests or feeds stations from them (the server mod ServersideQoL does both, asking for each chest the way a player's E key does), a chest just fetched from was left out of the counts for up to five seconds, as if a player were about to open it. Now only a player standing at the chest makes it drop out of the counts; the server's request still waits for the few seconds after a take, as every other computer's does.

## 0.3.4 (2026-09-27)

- **The chest service it carries changed** (with Craft from Chests, which shares it): for three seconds after a fetch takes from a chest, a friend's press of E on that chest does nothing, and his next press opens it, where he used to be told it was in use. It still keeps him from seeing, and saving back, his copy from before the fetch. A chest he has pressed E on meanwhile is left out of fetches until two seconds after, so his next press gets it.
- The Take tab's reasons gain one: a chest "about to be opened by another player".
- If another mod fails in the middle of a fetch, the chests already taken from are still sent to your friends and held.

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
