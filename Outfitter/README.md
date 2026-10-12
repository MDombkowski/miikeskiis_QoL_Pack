# Outfitter

Saved loadouts for Valheim: pick the cells of your inventory you want, save them under a name, or build a loadout by dragging in what your chests hold, and put a loadout on in one go. Its items come from the chests near you, what you wore goes away into the chests your setups name, and every item lands in the cell it was saved in. Three modes, top-ups, share codes for friends, with or without EquipmentAndQuickSlots. And **Loadout Stands**: the game's own armour stand as a loadout's stand, wearing its gear whenever you don't, one key to put it on and hang it back, and a window to name stands and move loadouts between them. Client-side: only the players who want it install it.

A mod of **miikeskii's QoL Pack**, and released on its own too. Version 0.6.0.

## How to use it

- **Pick the cells to save:** open your inventory, hold **Right Alt** and left-click the cells you want (an orange frame marks each; click again to unpick). Or, in Outfitter's window, press **Show my inventory**, then **Highlight cells**, and click the cells of your inventory there.
- **Save:** press **L** to open Outfitter's window, then **Save new** and give it a name. With cells of the loadout picked highlighted instead (Highlight cells, then click them in the loadout), **Save new** makes a new loadout of just those cells, each item where it sits. **Save over** replaces the loadout you've chosen with what's in the highlighted cells now; its food slots stay, with their rules, and of the foods you carry that the loadout doesn't name, the one each slot would take stays that slot's, not saved as an item (as an item it would keep every slot from it: you eat one of each); every other food is saved as usual. Anything else lying in a slot's cell is saved, and the slot moves to a free cell; it says so.
- **Equip:** choose a loadout and press **Equip now**. If some of it isn't in the chests nearby, a pop-up lists what, each item by its icon with how many and why (point at an icon for its name), and offers to equip what's here.
- **Order:** the loadout picked in the list shows **↑** and **↓**: each moves it one place up or down.
- **The same loadout again tops it up:** 10 carrot soups saved and 3 carried, so 7 come from the chests.
- **Share:** **Copy code** puts the loadout on the clipboard as text to paste to a friend (it's too long for the game's chat, so paste it in Discord or Steam). **Paste code** makes a loadout from a friend's code. A code is only a list: it never makes an item, and anything your game doesn't have (another mod's item) shows as "not in your game".
- **Rename** and **Delete** change only the loadout, never any item.

The window shows the chosen loadout in its places: what isn't nearby in red, what it puts on in blue, a food slot in green, and what you'll carry afterwards (in red when it's over your carry weight). A button that would do nothing just now is faded almost away; one that acts on the cells you highlighted is lit orange; point at any button for a moment to read what it does (*Hover help*, a setting).

### Copying cells

- **Highlight cells** turns highlighting on (the button stays lit while it is): a click on a cell of the loadout, or of your inventory beside the window, highlights it, or takes its highlight off. Press it again to go back to clicking a cell to change it. Highlights live in one grid at a time.
- **Copy cells** copies what's in the highlighted cells (the loadout's, or your inventory's: the very items, as Save takes them) to Outfitter's own clipboard. **Paste cells** puts them into the loadout picked: each into the cell it was copied from when that's free, else the first free cell; gear stays worn only where the loadout wears nothing there. All or nothing: without room for all of them, nothing changes and it says how many more free cells it needs. **Undo** takes a paste back. So you can copy cells from one loadout into another, or from your inventory into a loadout.
- **Clear highlights** takes them all off.

### Building a loadout from your chests

- **Beside the window:** **Show nearby items** and **Show my inventory**, at its top right, show one panel or the other beside it (each button again hides its panel). **My inventory** is your own inventory as the game has it, live: drag a cell into the loadout to put that very item in (its maker and other mods' data too), or highlight cells to copy them.
- **The nearby items:** press **Show nearby items** in the window. A panel slides out beside it with everything in the chests near you (and what you carry), browsed as Add to Cart's Take tab: the list on the left, Group, Sort and the filter. It counts what putting a loadout on could take: with *Leave one of each item in chests* (on unless AzuCraftyBoxes says otherwise) one of each stays in every chest, and with *Take the loadout's items from: SetUpChestsOnly* only what a chest is set up for counts. Something your chests keep only one of each still shows, greyed, so you can put it in a loadout to stock later.
- **Drag an item onto a cell** of the loadout to put it there, or click it to put it in the first free cell. A stackable comes as one full stack. Gear goes on as worn when the loadout wears nothing there yet, by the game's own rules for hands (a torch beside a one-handed weapon, a shield on the left). Settings change each of these.
- **New (empty)** starts a loadout from nothing, to fill from the panel beside the window. An empty loadout can't be put on (Equip waits until it holds something): putting nothing on would only take things off you.
- **Change a cell:**
  - Drag it to another cell to move it (what's there swaps), or drag it out of the grid, or right-click it, to take it out.
  - Click a cell to change it: its count (the wheel over it, Shift for ten at a time, or − and + and the box), **Worn** and **Displayed**, or **Remove** (red).
  - **Worn** and **Displayed** are each a switch of their own, lit when on: Worn puts the item on you when you equip the loadout (off: it's carried in its cell); Displayed shows it on a loadout stand, worn or not. So a build hammer can be Displayed, not Worn: the stand shows it, and putting the loadout on carries it without putting it in your hands.
  - Edits save as you make them; **Undo** takes back the last ones while the window is open (a run of wheel turns on one cell is one). Rename, Save over and Delete can't be undone, and Undo forgets that loadout's edits before them.
- **The grid is your inventory as it is now:** the hotbar on top, your rows (4 to 6 as you buy Haldor's pockets), and EquipmentAndQuickSlots' quick slots and gear slots when you have it. A gear slot takes only its own kind, and what's in it is worn.
- **Only a list changes.** Building a loadout never moves an item: the items come from the chests when you put it on.

### Food slots

A cell of a loadout can hold a **food slot**: a rule instead of one food, filled at each equip with a food nearby that meets it.

- **Add a food slot** (beside the grid, when no cell is picked) puts one in the first free cell from the bottom; **Make a food slot**, with a food's cell picked, turns that food into one (that food ticked to start; not while another cell of the loadout holds that food too, as the slot could then never take it: the button is faded then, and says why). **Edit food slot**, or a click on the slot's cell, opens its rule beside the window (a click on it again closes it).
- **By its stats:** a from and a to for Health, Stamina and Eitr (empty for any). A food fills the slot when every range you gave holds: three slots for foods of 20 to 50 Stamina, say, and one for 0 to 10 Stamina and 30 to 70 Health. The list below shows the game's foods, in green the ones that meet the ranges.
- **From a list:** tick the foods it may take (up to 64). A food is what the game eats as one: something giving Health.
- **The list** shows the foods you have nearby (with you, or in the chests Outfitter takes from) and the ones whose recipe you know, plus any the slot ticks already; never one you haven't found yet. **Sort** it by biome (the order you reach them), or by Health, Stamina or Eitr: the most first, and the same button again for the least first.
- **Prefer:** the **Most** or the **Least** of Health, Stamina, Eitr, or all three together.
- **First:** **Its stat** (every slot, out of the box): the food it prefers comes first as long as at least 3 are nearby (*Food slots by stat: at least*, Equipping), even when another would fill its count: for a slot of 10 wanting the most Health, 3 Honey glazed chickens before 10 Cooked deer meats. **Amount**: a food that fills the whole count first, then the preference (0.4's way).
- **How many:** its count (the wheel over it, or the box beside the grid: 1 to 100), at most one stack of the food it takes.
- **At each equip:** each slot gets one kind of food from what you carry and what the chests nearby could give (the same chests and rules as any item). **No food fills two slots, nor a slot and a food the loadout names itself** (you can eat only one of each). The most slots are filled first, then the most filled to their whole count (for a slot putting its stat first: to that setting's few), then each slot by its preference, in the loadout's order; a tie goes to food you carry, then by name, so the same chests always give the same choice and a second press changes nothing. A slot nothing meets is missing ("no food nearby meets it"). The window shows each slot with the food it would take now.
- **Modes:** Swap gear only leaves food slots alone; Fully replace keeps what a slot takes, and puts away food no slot or item takes (a food you carry that loses to a better one nearby goes away while the better one comes).
- At most 16 food slots a loadout. A loadout or share code holding one is kept as a new version: an older Outfitter keeps it untouched and refuses such a code (and a friend on an older Outfitter can't read a stand whose loadout holds one). Since 0.5.0, so is a loadout using a favourites switch or Displayed, and a code holding an Amount slot.

## The three modes

- **Fully replace:** everything you carry that the loadout doesn't hold goes into the chests nearby (quest items stay, your favourites go too unless you switch it off below, and EquipmentAndQuickSlots' quick slots stay by a setting), and the loadout comes in its places.
- **Swap gear only:** only your gear changes: what you wore goes away, the loadout's gear comes; the rest of your inventory, and the loadout's food and such, are left as they are.
- **Swap gear and add the rest:** the gear changes, and the loadout's food and such come too, topped up to its counts; your own stay.

What counts as gear: armour and weapons, and (each a setting, all on) arrows and bolts, tools, and torches.

The three buttons sit under the heading **Equip mode** beside Equip: the one chosen is lit, the others faded until you point at them, and each says what it does when you point at it.

**Your favourites** (StoreStash's, Left Shift + right-click) go away like any other item, in all three modes, while the loadout's **Favourites go too** (beside the mode buttons, lit) is on: Fully replace puts away every favourite the loadout doesn't hold, the swap modes your favourite gear. Each loadout keeps its own switch; one you haven't touched follows *Equip modes put away my favourites* (Equipping, on). With it off, your favourite **gear** follows *My favourite gear in an equip* (Swaps: it goes; StaysWithMe: it stays, 0.3.0's way), and your other favourites stay with you while *Leave my favourites alone* is on.

## Where things go

- **What comes:** from the chests near you (150 m by default), the chests set up for an item first (StoreStash's chest setups). For gear, the best copy: the saved quality or better, the best first, then the least worn.
- **What goes away:** where StoreStash's quick stack would send it: the chests set up for it first, then chests already holding it, then any chest with room, nearest first. What no chest can take stays with you, never on the ground, and you're told. What you wear that the loadout doesn't comes off either way, arrows too (the game keeps them equipped with no bow in hand), so only the loadout's gear is on; a belt or trinket stays on unless a chest takes it (a setting), and where the loadout's own piece can't be had, *Keep wearing mine* keeps yours on. Quest items never go away; your favourites go as the loadout's Favourites go too says (above).
- **More than the loadout's count** (a second stack, a spare copy): put away in Fully replace and kept in the swap modes, by a setting.
- **Never touched:** Reserve Stock's reserved chests, a chest someone else has open (a friend's open chest can be taken from through their game, if they have the pack), gravestones, and (by a setting) carts' and ships' chests.
- **Never lost or copied:** every item moves through the pack's chest service, which asks each chest's owner first and moves exactly what arrived, as Add to Cart and StoreStash do.
- **Chests far away, on a server:** the game hands a chest only to a player within about 64 to 96 m of it; a chest farther out that no game can answer for is claimed by your own game when it's needed (since 0.1.1, with the setting "Chests beyond the server's reach" in Chests switched on: off out of the box), as the game's portals do, and used once the server's newest copy is in, between the server's two-second rounds: so it can take a second or two longer than a near chest. Chests your game hasn't loaded (beyond about 128 m on a server at the usual "Classic" simulation distance) can't be used.

## Places

Each item is saved in its place: the hotbar stays the hotbar, and every other row is counted up from the bottom, so after Haldor's Pockets add a row at the bottom your loadouts still put things where you had them. With EquipmentAndQuickSlots its quick slots and equipment slots are places too, and worn armour sits in its slot; without it, an item saved there goes to a free cell. An item whose saved cell holds something else swaps places with it (a setting). With EquipmentAndQuickSlots, a stack you keep in one of its quick slots stays there when the loadout counts only part of it for a cell elsewhere: a stack of your rows takes that cell, topped up from the quick slot's share, so your quick slot keeps the rest (unless the loadout saves something else in that quick slot).

**When your inventory is full:** the pieces you wear come first (one replacing a piece of yours that goes into a chest takes its cell), then your other gear, then food and materials; what doesn't fit stays in its chest and is said missing, and the missing-items question says how many cells you're short. With EquipmentAndQuickSlots, worn armour sits in its own slot, whose cell nothing else may use: with your bag full, a piece replacing yours finds no room, and you keep yours on (*Keep wearing mine*).

## Instant

Equipping is instant by default: a whole set of armour changes at once, where the game's own way takes about 12.5 s. It waits while you attack, dodge, swim or eat (the game won't change gear then), up to 10 s. A setting puts each piece on the game's own way instead.

## Loadout Stands

The game's own armour stand as a loadout's stand: it wears the loadout's gear whenever you don't.

- **Save to a stand:** point at an armour stand and press **Left Alt + Left Shift + E**. What you carry and wear becomes a loadout in Outfitter's list (give it a name, or keep the one filled in), and the stand becomes its stand. Nothing is taken off you: your gear goes onto the stand the first time you hang it back (or at once, by a setting).
- **Put it on:** point at the stand and press **Left Alt + E**. The stand's gear comes to you and the loadout goes on, the rest from the chests nearby (the ones right next to the stand first), as Outfitter's window does it. What you wore goes **home**: a piece of another of your stands' loadouts goes back onto that stand, when it's within 30 m; everything else goes where Outfitter sends gear. Or, by a setting, onto this stand: a plain swap.
- **Hang it back:** the same key again, once you wear it, puts the loadout's pieces back on the stand. What a stand can't hold (a trinket, arrows) stays on you.
- **Pointing at a marked stand** shows the loadout's name, what the key does there now, and anything of it that's neither on the stand nor on you.
- **On a stand that already holds your loadout,** the save key asks to save over it (its food slots stay), or else to make it an ordinary stand again (its gear stays on it, and the loadout stays in your list).
- **A stand shows** chest, legs, helmet, a shield, one weapon (or tool, or torch), a cape and a belt: the game's own seven places. The rest of a loadout (food, arrows, a trinket, a second weapon) comes from the chests.
- **What a stand shows** is what the loadout marks **Displayed**: out of the box what it wears. A piece Displayed and not Worn (his build hammer) hangs on the stand, comes to you carried when you put the loadout on, and goes back on with the rest; one Worn and not Displayed is never hung on the stand. Two items for one place: the one marked Displayed by hand, else the first in the loadout. **Stand slots** (under the list) shows beside the window each place of a stand and what of the loadout picked it shows, and what doesn't fit. Put another stand's loadout on in Fully replace while carrying such a piece, and it goes home onto its stand rather than into a chest. Displayed is kept in your own list: a friend (or your other list of loadouts) sees the stand show what its loadout wears.
- **The stand window:** press **L** while pointing at an armour stand you may use (at one you may not, the window opens without the bar and says why). Outfitter's window opens with a bar for that stand:
  - its name: **Rename stand** gives it one, shown in place of "Armor stand" when you point at it (friends with the pack see it too); empty the box to give it the game's name back. A stand holding another player's loadout is theirs to rename.
  - what it holds, with **Unassign**;
  - **Personal** (below);
  - **Assign to stand**, for the loadout picked on the left.
- **Moving loadouts between stands:** each loadout in the list shows the stand it's on. **Move to stand…** (under the list) opens the stands near you (within 60 m) for the loadout picked; pick one. With more than fit, the wheel scrolls the list. A click on a loadout in the list only picks it.
  - The loadout moves there with its gear, each piece straight across into its place.
  - Onto a stand that holds another of your loadouts, the two swap. The other's gear passes through your inventory, so it needs room: without it, nothing moves and you're told.
  - A loadout on a stand you may no longer use (a friend's ward went up over it) stays there with its gear, and goes onto the new stand too; you're told.
  - Stands farther than 60 m aren't seen: a loadout on one of those, assigned here, is on both.
  - A loadout on more than one stand shows the nearest you may use, with "(+1)" for each other; moving it, or taking it off its stand, goes by that one.

**Personal stands:** **Left Alt + H** pointing at a stand (or **Personal** on the stand bar) makes it your personal stand, and again makes it an ordinary stand. A stand you save to, assign a loadout to or move one onto becomes your personal stand too (*Stands I save to become personal*, on). On someone else's personal stand, every Outfitter key and button of a friend with the pack does nothing but say whose it is: save, put on, hang back, the pose, rename, assign, unassign, personal, and moving a loadout onto it. It's a mark of whose stand it is, not a lock: the game's own E works as for any stand, and the ward still decides who may touch it at all; a friend without the pack sees an ordinary stand. Only its person takes it off; a stand holding another player's loadout is theirs to make personal. Pointing at it says "Personal: yours", or whose.

**Stands share gear:** putting a stand's loadout on, a piece of it that's neither on that stand, nor with you, nor in the chests nearby comes off another of your stands within 30 m of it (your own marked stands you may use, the nearest first), and you're told which. One Lox Cape serves all three of your stands that hold it. Cancel at the missing-items question, or end up not wearing it, and it goes back onto its own stand; hang the loadout back and it goes onto the stand you hang it on. Never into a chest.

**Every stand shows its loadout:** a marked stand of yours is drawn wearing every piece of its loadout it lacks at the moment (worn by you, lent to another stand, in a chest), so it looks like its loadout from the moment you save it, and three stands can all show one cape. It's drawn in your game only, and nothing is on the stand in truth: E takes only what's really there, the hover says "Shown, not on it: …" (what its "Not here" line doesn't name already), and a friend sees the stand as it is. *Show what a stand's loadout lacks on it*: OnMyStands (the default), OnEveryMarkedStand, or Off.

**Equip from the window:** on a loadout that's on one of your stands near you (within 60 m), **Equip now** does what Left Alt + E at that stand does (pointing at the button names the stand), without walking there: the stand's gear comes to you, the rest from the chests around the stand (its own first), what you wore goes home, in the mode chosen in the window (with the window's mode buttons hidden, the stands' own *Equip mode at a stand*). It only ever puts on, never hangs back. The window's preview counts the stand's pieces as there, and its weight line says how many come from your stands. A setting turns it off.

**Never lost or copied:** the stand is made yours first, as the game hands a stand to whoever asks; everything moves in one go, added to you before it leaves the stand and onto the stand before it leaves you, even if another mod's code fails in the middle; for a few seconds after a swap another player's E does nothing (their next press works) and anything they hang on it by hand goes on once their game gets the stand, so their game sees your change first. Without room in your inventory for everything on the stand, nothing moves and you're told. If you cancel at the missing-items pop-up, the stand's gear goes back on it. A piece you can't wear (broken, say) goes back on the stand rather than into a chest, and doesn't stop the key from hanging the rest back.

**With friends:** a friend without the mod sees an ordinary stand wearing the gear, and can take it off with E as always. Your keys leave stands other players saved alone, unless a setting lets you put their loadout on (the stand keeps a copy of its loadout's list for that, never any item). Only stands you may use count (wards), or only ones you built, by a setting. An unmarked stand works exactly as the game's, except that E does nothing there while you hold a stand key (Left Alt + E tells you it holds no loadout).

Its settings are their own section, **Loadout Stands**: the keys (the personal key too), what saving takes, whether stands you save to become personal, what you wore goes, lending (on, within 30 m), the stands' own equip mode, chests near the stand, whose stands, the stand window (L at a stand, Equip in the window at a stand, how far stands are listed, whether a moved loadout takes its gear, swap or replace, a stand's name in place of the game's, what a stand shows of its loadout), the hover lines (whose personal stand too), how long a stand is held after a swap, and changing a marked stand's pose (off; Left Alt + Backspace when on: the game itself offers no way).

## Settings

In a configuration manager (F1), under **Outfitter**, folded into sections: Window and keys, Equipping, Chests, Which copy, Places, Saving and codes, Loadout Stands. Every setting works at once. **Window size** (Window and keys) draws the window Small (as 0.2.0 did), Medium (a quarter bigger, the default) or Large (half as big again), as Add to Cart's sizes, each kept to what fits your screen with the text sharp; the drawer and what dragging puts in have settings there too. Out of the box nothing you already see changes: Outfitter adds a window behind L and a pick with Right Alt, and a stand changes only once someone saves a loadout to it.

## Works with

- **EquipmentAndQuickSlots** (RandyKnapp): found at start and used only through its own interface; everything works without it too. Outfitter's window draws its slots as three rows below yours, the cells a little smaller to fit; with ten or more rows of your own (its Extra Inventory Rows) the window shows the first nine.
- **The pack's own mods:** StoreStash's chest setups say where what you take off goes, and its favourites never go away; Reserve Stock's reserved chests are never taken from or put into.
- **Use Case** shows its panel beside the game's own item tooltips; Outfitter's window draws its own cells, so the panel doesn't show there.
- **Other loadout mods** (Armory, NoVikingLeftBehind's loadouts): if you prefer one of them, switch Outfitter off in its settings (F1, Outfitter, Enabled).
- **Other armour stand mods** (Wardrobe and QuickswapArmorstand use Left Alt + E on stands too; ZenItemStands and RunicDisplayStands change how stands work): use one or the other. To keep theirs, switch Loadout Stands off (F1, Outfitter, Loadout Stands, the first setting) or give it other keys; Outfitter's window works either way.
- **MyLittleUI** rewrites the armour stand's hover text; a marked stand's lines are added after it, so both show.
- **StoreStash**'s Left Alt + H marks chests and does nothing at a stand, so the personal stand key shares it.
- **Other mods that draw armour stands** may draw over the pieces a stand shows; if so, set *Show what a stand's loadout lacks on it* to Off.
- **Mods that give your inventory rows or slots of their own** (AzuExtendedPlayerInventory, ExtraSlots, Extended Player Inventory, ComfyQuickSlots) aren't supported: Outfitter only knows the game's own rows and EquipmentAndQuickSlots' slots, so it stays off while one of them is installed, and says so in the log.

## Switched off

Its window, keys and picks do nothing, its frames go, and no chest is touched. Your saved loadouts stay saved with your character (in a part of its file the game itself never reads), ready for when you switch it back on. Marked stands are ordinary stands again, their gear still on them, personal stands are ordinary stands, and stands show only what they hold (Loadout Stands' own switch does the same for the stands alone; switched off in the middle of putting a stand's loadout on, Outfitter finishes that equip on its own and nothing more goes onto a stand: what Outfitter doesn't put away stays in your inventory).

## Building it

`dotnet build -c Release` in this folder (the .NET SDK and the .NET Framework 4.7.2 Developer Pack). `tools\package.ps1` makes its zip. It compiles against the game's and your mod profile's DLLs where they already are; see `Outfitter.csproj` to point it elsewhere.

## Made with AI

Its code and these files were written by Claude (Anthropic) agents for miikeskii.
