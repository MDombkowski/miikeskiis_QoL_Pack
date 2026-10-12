# Changelog

## 0.6.1 (2026-10-11)

- Settings name no other mod unless Outfitter works with it, stands aside for it or makes room for it: "Put on a stand's loadout" no longer names two armour-stand mods; what it does is unchanged.
- The chest service it carries changed: its settings and messages name no mod it doesn't work with or make room for (a chest the server holds is "the server (a mod running on the server)"), and in **Chests (every mod)**, the list of chest distances says when one of Topper-Upper's distances is 0 and takes the range above. Add to Cart 0.5.3, Craft from Chests 0.5.1, Reserve Stock 0.1.13, StoreStash 0.7.3, Topper-Upper 0.4.0 carry it too.

## 0.6.0 (2026-10-11)

- **Switching loadouts takes off what the new one doesn't wear** (miikeskii's word: the game kept what he had equipped, arrows above all, which stay equipped with no bow in hand). Whatever you wear that the loadout doesn't wear now comes off even when no chest nearby takes it, and stays in your inventory, so only the loadout's gear is on. A loadout saved with a bow but no arrows now takes your equipped arrows off too. A new setting in Equipping, **What I wear that the loadout doesn't**: ComesOff (the default), or ComesOffOnlyIntoAChest, where a piece comes off only into a chest or for something in its place, as before.
- **Belts and trinkets keep their place:** one you wear that the loadout doesn't name still comes off only into a chest (or for one the loadout puts on), so a Megingjord doesn't take your carry weight away from you out in the field. A new setting in Equipping, **A belt or trinket the loadout doesn't name**: KeepWearing (the default) or ComesOff.
- **The window's weight line counts the carry weight you'd lose** (or gain) with the pieces that come off and go on.
- **Keep wearing mine** now also keeps the weapon in your other hand when the loadout's bow or two-handed weapon can't be had (that bow would have taken both hands).
- **The missing-items pop-up is easier to read** (miikeskii's word): a blank line between its parts, a bullet for each item, and each item's icon with how many and why; point at an icon for its name. The long settings note at its end is gone. A new setting in Equipping, **Missing items in the pop-up**: IconNameOnHover (the default) or IconAndName; with a gamepad the names always show beside the icons. A long list scrolls (the mouse wheel, or a gamepad's right stick).

## 0.5.5 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.
- **"Use this range in every mod"** in the shared section **Chests (every mod)**: while it's on, Outfitter's chest range is the shared **Chest range**, whatever its own says (its own is kept for when it's off). The section also has a button that sets every mod's own range to 0 for good, and lists every chest distance in the pack with what each reaches now.

## 0.5.4 (2026-10-10)

- The chest service it carries talks to miikeskii's Server-Side QoL Pack on the server: it says hello to it, and when the server pack runs Far Fetched, your game learns from it which chests it hands you (a server running Far Fetched alone works as before). Add to Cart 0.5.1, Craft from Chests 0.4.1, Reserve Stock 0.1.11, StoreStash 0.7.1 and Topper-Upper 0.2.0 carry it too. Nothing else changes here.

## 0.5.3 (2026-10-09)

- The chest service and the catalogue it carries changed: a friend's marked chest whose owner keeps other players' mods out is left alone, the Obliterator is never a chest, and the put-away follows chest setups' quick picks and a group's own biomes (StoreStash 0.7.0).

## 0.5.2 (2026-10-08)

- **Skarif's Valheim Performance Overhaul no longer leaves chests stuck** (miikeskii's report of 2026-10-08: building from chests 50 to 60 m away kept failing on his friend's server, and much less with Skarif's mod off). That mod makes your game send its updates to the server nearest first, every time, so in a busy base the news that a chest changed hands could wait for minutes, and your game and the server each thought the other had it. The chest service now switches off just that part of Skarif's mod while the game runs: a new section in F1, **Performance mods (every mod)**, with **Skarif's mod: send in the game's own order** (on). Nothing changes without Skarif's mod.
- **"Beyond the server's reach" only when it is**, in the log's line for a chest that couldn't be had: a chest your game claimed itself because the server hadn't handed it over (55 m away) was called beyond the server's reach.

## 0.5.1 (2026-10-08)

- **One place for the chest settings every chest mod must agree on**: a new section in F1, **Chests (every mod)**, holds the chest range (up to 250 m now, 150 out of the box), **Chests beyond the server's reach** (now **on** out of the box, one switch for Add to Cart, Craft from Chests, StoreStash and Outfitter instead of four) and **Write chest problems to the log** (on). This mod's own "Chest range" is 0 out of the box, which follows the shared one; set it to give this mod a range of its own. A settings file still at the old 150 moves to 0 once.
- **The chest service it carries changed**: a chest your game can't get at all is left out instead of being asked for and timing out; Far Fetched on the server (a separate server mod) is recognised, and chests it hands you are used at once; a far chest is never claimed where the server would hand it out itself.

## 0.5.0 (2026-10-07)

At miikeskii's word on 0.4.1:

- **Favourites go too:** a switch per loadout beside the mode buttons, on out of the box (*Equip modes put away my favourites*, Equipping): your favourites go away like any other item in all three modes. Off, they follow the 0.4 settings.
- **Food slots by stat:** each slot puts **Its stat** first (every slot, old ones too), as long as at least 3 are nearby (*Food slots by stat: at least*), or the **Amount** first (0.4's way). A click on a food slot opens its rule.
- **Displayed and Worn,** each its own switch on a cell: a piece can show on its stand and stay out of your hands when you put the loadout on (carried, and hung back with the rest), or be worn and never shown. **Stand slots** beside the window shows a stand's places and what fills them.
- **The window:** **↑** and **↓** on the loadout picked reorder the list; **Save new** saves the loadout's highlighted cells as a new loadout; **Take out** is a red **Remove**; Equip reads **Equip now** (never "Equip at" a stand's name); the missing-items question says where to turn it off.
- The kept text is written as version 3 when a loadout uses any of this or holds a food slot (0.4 keeps it untouched); codes carry a slot's Amount.
- **At the stands, too:** a carried Displayed piece goes home onto its stand when another stand's loadout goes on in Fully replace; a stand's copy goes back when your own fills the loadout; your everyday hammer no longer makes the key hang back at an empty stand.

## 0.4.1 (2026-10-07)

- **The food slot's list shows only foods you know** (miikeskii's word: it showed foods he hadn't discovered yet): the ones you have nearby, with you or in the chests Outfitter takes from (read every two seconds while the panel shows), the ones whose recipe you know, and any the slot ticks already, so they can be unticked.
- **Sort the list** by biome (as before), or by Health, Stamina or Eitr, the most first; the same button again sorts the least first.

## 0.4.0 (2026-10-07)

- **The window** (miikeskii's notes on 0.3.0):
  - **Show nearby items** / **Show my inventory**, side by side at the top right: the panel beside the window shows one or the other. **My inventory** is your own grid as the game has it, live: drag a cell into the loadout to put that very item in (its maker and other mods' data kept), or highlight cells.
  - **Highlight cells** (in place of Choose cells) is a switch: while it's on, a click on a cell of the loadout or of your inventory highlights it, or takes its highlight off. The loadout's grid always shows the loadout. Highlights live in one grid at a time; Right Alt + click in the game's own inventory still highlights your inventory's cells.
  - **Copy cells** copies the highlighted cells' items to Outfitter's own clipboard; **Paste cells** puts them into the loadout picked, each into the cell it came from when that's free, else the first free cell; all or nothing; one Undo takes a paste back.
  - A button that would do nothing now is faded almost away; one that acts on the highlighted cells is lit orange. Pointing at a button for a moment says what it does (**Hover help**, a setting).
  - The three modes sit under the heading **Equip mode**: the chosen one lit, the other two faded until you point at them, each with its help.
  - **Move to stand…** (under the list) moves the loadout picked; a click on a loadout in the list only picks it. **Assign to stand** on the stand bar.
  - The window is 60 pixels taller, for the cell tools and the heading.
  - The window's red marks for what's missing, and their "Missing: …" lines, work: they had never shown (its preview planned a copy of the loadout).
- **Food slots**: a cell of a loadout can hold a rule instead of one food, **by its stats** (a range for any of Health, Stamina and Eitr) or **from a list** of foods you tick, preferring the most or the least of one stat or of all three, with how many to bring (at most one stack). At each equip each slot is filled from what you carry and the chests nearby could give: one food per slot, never two slots with one food nor a slot with a food the loadout names itself (you eat one of each); the most slots filled first, then the most filled to their count, then each slot's preference in the loadout's order; ties to food you carry, then by name. Swap gear only leaves them alone; Fully replace puts away the food no slot takes. At most 16 a loadout. Edited in a panel beside the window (**Add a food slot**, **Make a food slot**, **Edit food slot**); a slot's cell is green with the food it would take now and how many. A food is what the game eats as one: something giving Health. **Save over**, in the window and at a stand, keeps a loadout's food slots. Loadouts and codes holding one are kept as version 2 ("outfitter 2", "OF2:"): an older Outfitter keeps them untouched and refuses such codes.
- **Your favourite gear swaps** (his note: "none of them get swapped"): **My favourite gear in an equip** (Equipping), Swaps out of the box: favourite gear comes off and goes away like any other gear, at the window and at a stand. StaysWithMe is 0.3.0's way. "Leave my favourites alone" still keeps your other favourites.
- **Loadout Stands**:
  - **Personal stands** (his yes): Left Alt + H, or **Personal** on the stand bar, makes a stand your personal stand. Other players' Outfitter keys and buttons leave it alone (save, put on, hang back, pose, rename, assign, unassign, moving a loadout onto it); the game's E isn't stopped. Stands you save to, assign or move a loadout onto become personal (a setting, on). The hover says whose.
  - **Stands share gear:** a piece of a stand's loadout that's not on the stand, not with you and not in the chests nearby comes off another of your stands within 30 m; it goes back onto its own stand on a cancel, or when you don't end up wearing it.
  - **Equip in the window** on a loadout that's on one of your stands nearby does what Left Alt + E at that stand does (the button says **Equip at** and the stand's name, or its loadout's: "Equip at Raid's stand"): the chests around the stand, the window's mode (with its mode buttons hidden, the stands' own); the preview counts the stand's pieces.
  - **A stand shows its loadout:** drawn wearing the pieces it lacks, in your game only ("Shown, not on it: …"). OnMyStands, OnEveryMarkedStand, Off.
- **Quick slots kept** (with EquipmentAndQuickSlots): a stack in one of its quick slots that a loadout counts only part of for a cell elsewhere stays in the quick slot, topped up from rather than moved whole (the next press used to put its surplus away).
- **Two fixes in putting a loadout on:** a stack that lands in the cell another item is saved in is now moved to its own cell at the first press (a second press used to); topping up next to a favourite stack counts the favourite first, so a second press moves nothing.
- New settings: My inventory panel, Hover help (Window and keys); My favourite gear in an equip (Equipping); Food slots: count at first (Saving and codes); Personal stand key, Stands I save to become personal, Take missing pieces from my other stands, Other stands lend within (metres), Equip in the window puts a stand's loadout on at its stand, Show what a stand's loadout lacks on it, Hover: whose personal stand (Loadout Stands). "The drawer opens with the window" is now "The panel beside the window opens with", with AlwaysMyInventory.

## 0.3.0 (2026-10-06)

- **Build a loadout from your chests** (miikeskii's feedback on 0.2.0):
  - **Nearby items** opens a drawer beside the window, with everything in the chests near you and what you carry, browsed as Add to Cart's Take tab.
  - Drag an item onto a loadout's cell to put it there, or click it into the first free cell. A stackable comes as one full stack; gear goes on as worn when nothing is worn there yet, by the game's rules for hands.
  - **New (empty)** starts a loadout from nothing. An empty loadout can't be put on, from the window or from a stand.
  - A cell drags to another (a swap) or out of the grid (taken out). Click a cell to change its count (the wheel, − and +, or the box), wear or carry it, or take it out; **Undo** takes back the last edits (Rename, Save over and Delete can't be undone).
  - The grid is your inventory as it is: the hotbar, your rows, EquipmentAndQuickSlots' quick and gear slots, a gear slot taking only its own kind.
  - The drawer counts what putting a loadout on could take: one of each left in every chest by "Leave one of each item in chests", and only what a chest is set up for with "SetUpChestsOnly". What the chests keep only one of each still shows, greyed.
- **The stand window** (his feedback):
  - **L**, pointing at an armour stand you may use, opens the window with a bar for that stand: **Rename stand** (the name shown in place of "Armor stand"; empty, the game's name again; not on a stand holding another player's loadout), **Unassign**, **Assign to armor stand**.
  - The loadout list shows each loadout's stand. A click there moves it to another stand near you, with its gear: each piece is written onto its new place before it leaves the old one. The list scrolls when there are more stands than fit.
  - Onto a stand holding another of your loadouts, the two swap.
  - A loadout on a stand you may no longer use stays there, and goes onto the new stand too. One on more than one stand shows the nearest you may use, "(+1)" for each other, and moves from that one.
- **Window size:** Small (as 0.2.0), Medium (a quarter bigger, the default), Large (half as big again), as Add to Cart's sizes, kept to what fits the screen.
- Twelve new settings, in Window and keys and in Loadout Stands.

## 0.2.0 (2026-10-06)

- **Loadout Stands**: the game's own armour stand as a loadout's stand. Left Alt + Left Shift + E saves what you carry and wear to a stand (a loadout in Outfitter's list, the stand marked with it); Left Alt + E puts the stand's loadout on (its gear from the stand, the rest from the chests near it first) and sends what you wore home (a piece of another of your stands' loadouts back onto that stand when it's near, else where Outfitter sends gear), and the same key again hangs it back. Hover lines on marked stands; a stand made yours before anything moves, held a few seconds after; nothing moves without room for all of it; a cancel puts the stand's gear back; a friend without the mod sees an ordinary stand. Its own section of settings, Loadout Stands (20 settings). Nothing changes on a stand nobody marked.
- Outfitter's equip gained two things for the stands, which change nothing for the window: a request can keep chosen pieces with him as they come off (`EquipRequest.KeepsTakenOff`), and keep chosen items from ever going away (`EquipRequest.Stays`).

## 0.1.1 (2026-10-06)

- **The chest service it carries changed** (Add to Cart 0.4.8, Craft from Chests 0.3.5, Reserve Stock 0.1.7 and StoreStash 0.6.1 carry it too): on a server, the game hands a chest only to a player within about 64 to 96 m of it, so a chest loaded farther out with nobody near it never answered ("owners didn't answer in time"); the service now claims such a chest itself, as the game's own portals do, only when no game can answer for it, and changes it only once the server's newest copy is in and between the server's rounds. It is off out of the box: switch on the new setting in Chests, "Chests beyond the server's reach", to use it. Alone or hosting, and within your own area, nothing changes.
- The kit it carries gained `ModuleSettings.MovedDefault` and `QoLModule.EnabledByDefault`; none of Outfitter's defaults changed.

## 0.1.0 (2026-10-06)

- **First version**, as a mod of miikeskii's QoL Pack (0.30.0) and on its own: saved loadouts of the cells you pick (Right Alt + click, or Choose cells), kept in their places counted from the bottom of your inventory, put on in one go from a window of its own (L), their items fetched from the chests nearby and what you wore put away into the chests your setups name, in one of three modes (Fully replace, Swap gear only, Swap gear and add the rest), instantly or the game's own way; the same loadout again tops it up; share codes; Rename, Delete, Save over; with or without EquipmentAndQuickSlots.
