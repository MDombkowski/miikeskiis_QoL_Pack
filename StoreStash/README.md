# StoreStash

Made with AI. Quick stack: one key puts what you carry into the chests nearby that already hold it, safely with friends at the same chests. It also sorts your inventory and chests, deletes what you mark as trash, and keeps chests set up alike tidy.

## What it does

- **Press P** (or the button beside your inventory, between the armour and weight boxes) and what you carry goes into the chests nearby that already hold some of it, filling their part-stacks first and then empty cells. Up to 150 m away, with the inventory open or closed. A line top left says what went where: "34 items into 5 chests".
- **Trophies** go to any chest nearby that holds a trophy of any kind.
- **Place stacks** in a chest's window, and **holding E** at a chest, do the same into that one chest: only what it already holds (trophies as set), as the game's own do.
- **Your personal chests: press H,** and what you carry goes into your personal chests nearby, and nowhere else: the game's Personal Chests you built, and any chest you marked as yours. Everything else works as P does (the radius, your favorites, the hotbar, trophies, the line top left: "20 items into 2 of your chests"). **Left Alt + H,** pointing at a chest or with its window open, marks it as yours, or takes your mark off; pointing at it then shows "Yours". Friends running StoreStash leave a chest you marked alone when they quick stack, and you leave theirs. A button for it, below P's, is on at first (a setting).
- **Favorites stay with you.** Hold Left Shift and right-click an item in your inventory to make its kind a favorite: a gold border shows it, and quick stack, Place stacks and holding E leave it with you. Every item of that kind is a favorite, whatever its quality, now and later.
- **The hotbar** (your top row): what isn't a favorite goes like the rest, by default (the first time it takes something from there, a note says so, once). A setting keeps the whole hotbar, or lets everything on it go but what you're wearing.
- **Never:** what you wear or hold (also a weapon or tool the game has put away for a moment: at a crafting station, in bed, while you swim or eat, or with R), the stack in your hand, a quest item, rows below the inventory's own (EquipmentAndQuickSlots' equipment and quick slots, and its extra inventory rows if you use them), the world's own chests, gravestones, a chest someone else has open, or a ship someone else is aboard.
- **Reserved chests** (Reserve Stock): the key and the button leave them alone. Place stacks and holding E still work at one, as a choice made at that chest, unless a setting says otherwise.
- **Which chest first** (a list in the settings, under What goes where): when several chests may take an item, the first rule that tells two apart decides, and that chest gets all it can take before the next: chests set up for it (the most specific setup first, then the one holding the most of it) and, if you tick it, your personal chests. Drag the rules into the order you want.
- **Its keys never fire while you type** in chat, a sign, a map pin's name, or the search boxes of the game and most mods, or while a configuration manager's window (F1) is open.
- **Chest setups:** set a chest up to take only the kinds of things you pick (Hides and Pelts; Creature Parts; only from the Plains…), and quick stack sends those things there, even when the chest holds none yet, and keeps everything else out. Copy a chest's setup onto another with Ctrl+C and Ctrl+V. See *Chest setups* below.
- **Stack Overflow:** chests set up alike act as one chest. Quick stack fills the one already holding the most of a thing, then the next when it's full; **Left Alt + N** (pointing at a set-up chest, or with its window open) tidies them, moving things between them so each kind sits in as few of them as possible. See *Stack Overflow* below.

## Chest setups

- **Open a chest's setup** by pointing at it and pressing **Left Alt + J**: the chest opens, with its setup window beside it, over the crafting panel. With a chest's window already open, Left Alt + J opens or closes its setup window. A button for it in the chest window, under Reserve Stock's padlock, is on at first ("Setup button in the chest window"). The window's title shows the chest's name, or its kind while it has none: click it, type, and Enter renames the chest (kept where MyLittleUI keeps chest names, so both show it; Esc keeps the old name).
- **Pick what it takes** from the pack's catalogue, as Add to Cart's Take tab shows it: the list on the left picks a family (Hides and Fabrics, Creature Parts, Gear…) or one of its members, and the **search box** over it finds things as you type (every word must match a thing's name, family, member, kind or biome: "swamp troph"); each heading has a **Take all** button ("Take" while biomes narrow it; a family, a member, one of Gear's kinds such as Arrows or Axes, or in Trophies and Creature Parts a biome: Swamp Trophies); a click on a thing takes that one thing, or, inside a family the chest takes, leaves it out ("Creature Parts, except Needle"), and a second click undoes it. Everything the chest takes has a **green check** in its corner: point at it and it turns into a red X, and a click on the X takes the thing out; a **right-click** on a thumbnail, or on a heading's button, takes it out too. A thing left out has a red minus, which takes it back. In "This chest's setup" a left click on a thumbnail does nothing: its mark, a right-click or a heading's button changes it. A green cell is taken, a red one left out, a dim one not taken.
- **Biomes** (the row "From:") narrow the families, members and kinds you take to what comes from them (each item's biome of origin: "pelts from the Plains" is Lox Pelt), in either order: Trophies, then the Swamp and the Plains, or the biomes first and then Trophies' **Only this**, both make "Takes Trophies, only from the Swamp and Plains biomes". Pick several; none means every biome. With no family, member or kind taken, the biomes take everything from them ("Takes everything from the Plains biome"). A single thing you pick by name comes from any biome, on top of the rest. "Other" is the few items with no biome. The line at the top says what the chest takes, in words.
- **"This chest's setup"** in the list on the left shows what it takes. Every change is saved into the chest at once: there's nothing to save, and closing the chest loses nothing. **Clear** makes it an ordinary chest again.
- **Copy and paste:** the Copy and Paste buttons, or **Ctrl+C and Ctrl+V while a chest's window is open** (never while pointing at a chest in the world, where Left Ctrl crouches, C walks and V picks up). Pasting replaces the chest's setup and says what it now takes. A setting adds it instead: the copy's picks, left-out things and biomes join the chest's own, so the copy's biomes narrow the chest's families too, the copy's left-out things are left out of the chest too (unless the chest picked that very thing), and what the copy picks is taken even if the chest left it out. With "Copy to the clipboard too" on, a setup is also a line of text you can send a friend, who pastes it the same way.
- **How quick stack follows setups:** a set-up chest takes what its setup takes, even when it holds none yet, and nothing else, even something already inside it (a setting lets it also take more of what it holds). By default set-up chests come first: what one takes goes there before any chest without a setup, the one set up most specifically first (a single thing, then a Gear kind, a member, a family, biomes alone), then the one already holding the most of it, then the nearest; "Which chest first" changes the order, or puts your personal chests first. What the set-up chests can't fit goes to chests that already hold it, and what no set-up chest takes goes to chests without a setup, as before (each a setting). A set-up chest someone has open, or whose owner's game doesn't answer, is still the home of what it takes: with "What set-up chests can't fit goes on" off, that stays with you until it's free. Place stacks and holding E at a set-up chest follow its setup too (a setting).
- **Pointing at a set-up chest** shows what it takes under its name: "Takes: Hides and Pelts from Plains".
- **Say where things went** (a setting, on at first): after a quick stack, a line for each set-up chest says what it took, in place of the usual line ("Into set-up chests: 12 Wolf Pelt → Pelts", a chest called by its name if it has one); when other chests got things too, "15 items into 2 other chests" comes first, in the same message. Each line after the first keeps the message up a second longer (two settings).
- **With friends:** the setup is saved in the chest itself, with the world. Anyone who can open a chest can change its setup, and friends on the pack follow it. A friend without the pack (or with Chest setups off) sees an ordinary chest, and their quick stack only ever adds what a chest already holds, so a set-up chest stays strict unless something stray is already inside it.
- **Carts and ships** can be set up too (a setting); their setup stays with the cart or ship.
- **Switched off** ("Chest setups", or StoreStash itself): quick stack ignores setups, and the window, button, keys and hover line are gone. The setups stay in the chests for when it's on again.

## Stack Overflow

Chests set up alike (*Chest setups*, above) act as partitions of one chest.

- **At quick stack,** a thing goes first to the set-up chest already holding the most of it, and when that one is full, on into the next: "Which chest first"'s rule "...the one holding the most of it" (on by default) does that, so nothing else needs switching on.
- **A tidy** gathers what's already stored: **press Left Alt + N** pointing at a set-up chest, or with its window open, and the chests set up like it move things between them so each kind sits in as few of them as possible. A kind already all in one chest stays there, unless a bigger kind needs its room and splitting it leaves your kinds in fewer chests in all; one spread over several goes to the chest that keeps it (by default the one already holding the most of it), and one too big for any chest fills the roomiest first, then as few others as it needs. Other things move out of a chest's way where they must: 8 stacks of Leather Scraps in one chest and 3 in another set up the same, the 3 go into the chest with 8, and what was taking their room there goes to the other. When every chest is full, one stack waits in any chest of the group with a free cell while the rest swap; with no free cell anywhere, it stops and says the chests are full.
- **A button** in the chest window, under the setup button, does the same (off until you switch it on: "Tidy button in the chest window").
- **"Set up alike"** means exactly the same setup; a setting counts setups alike once their biomes are left out ("Hides from the Plains" and "Hides from the Meadows"). A thing still goes only into a chest whose own setup takes it, and things no setup in the group takes stay where they are (a setting gathers them too).
- **Which chests:** the chests quick stack may use within its search radius (or the tidy's own radius), set up alike; never a reserved chest (Reserve Stock), a chest someone else has open (left out that time, and said), a chest Craft from Chests is holding for a craft under way, or a chest someone else marked as theirs (a setting); carts' and ships' chests only with their settings.
- **What it says:** "Tidied 3 chests: 14 stacks moved" (a setting adds a line for each chest a thing went into, or says nothing), "already tidy", or why it couldn't, and what it couldn't put right because the chests are full: a kind still in more than one chest, or one left in a chest whose setup doesn't take it. At most 500 stacks move per press (a setting); a bigger tidy says to press again. A setting asks first when a tidy would move more than so many stacks; a tidy by itself never asks, and moves no more than that many.
- **Afterwards** each chest it changed is sorted as StoreStash sorts a chest as it opens (a setting), so part-stacks inside one chest merge too.
- **By itself** (both off at first): after each quick stack, the groups of the set-up chests it put things into; or every so many minutes, every group in reach. Each tidy claims the chests it changes from their owners and holds them a few seconds, so friends nearby may find them busy for a moment.
- **With friends:** every chest a tidy changes is first asked of the friend whose game holds it, as quick stack's are, read again once handed over, and each move goes into the target first, measured, then out of the source, so nothing is lost or copied. At your own chests it's instant.
- **Switched off** ("Stack Overflow", "Chest setups", or StoreStash itself): no key, button or tidy by itself; quick stack's order is "Which chest first"'s either way.

## Sorting

- **Press S** with your inventory open (or the button below the weight box, the lowest of the buttons beside your inventory) and your inventory is sorted: by the pack's own order, the one you browse in Add to Cart (tools, weapons, armour and ammo first, then wood, stone, metals, hides, creature parts, trophies, fuels, nature, food, meads, magic, boss items, valuables; each by its own groups, then from the Meadows to the Deep North). Part-stacks of the same item come together.
- **What stays where it is:** your hotbar (the top row), what you wear or hold, and your favorites, each a setting (what you wear or hold is never merged into another stack, kept in place or not). Rows below the inventory's own (EquipmentAndQuickSlots' equipment and quick slots) are never touched, and neither is the stack you have in the split window.
- **A chest is sorted as it opens**, once it's yours (the game hands a chest to whoever opens it): every chest you open, or only chests you built (a setting); a chest someone else marked as theirs (Left Alt + H, above) isn't, unless a setting says so. A **Sort button** left of Place stacks sorts the open chest too, any chest you could sort. Never a grave: taking everything back from one puts each item in its old cell.
- **Other orders** (a setting): by type, by name, heaviest first, most valuable first; and "Sorted items start" at the bottom instead, leaving the empty cells at the top.
- **Nothing is saved when nothing moves,** so a sorted chest opened again isn't written again.

## The trash can

- **Mark what you never want:** hold Left Alt and right-click an item in your inventory to mark its kind as trash: a red border shows it (just inside a favorite's gold one). Every item of that kind is marked, whatever its quality, now and later.
- **Click the trash can** (above the armour box, the top one of the buttons beside your inventory): it says what it would delete and waits for your Delete. It deletes every marked stack in your inventory, **never** what's on your hotbar, what you wear or hold, a favorite or a quest item, whatever is marked; those are named as kept.
- **Carrying an item** (picked up with a click, or split off with Shift): click the can to delete just what you carry, marked or not, from your inventory or the chest you have open. A favorite, or something from your hotbar or quick slots, is kept unless a setting allows it; what you wear or hold, and quest items, never.
- **"Don't ask again"** in the question sets "Ask before deleting" to Never; F1 brings it back. Esc closes only the question; Delete needs a click (Enter does nothing, so no stray key deletes).
- A line top left says what went: "Deleted 3 kinds, 57 items".

## Safe with friends

Every chest it puts into is first asked of the friend whose game holds it, the way the game's own chest window asks, and it waits a moment for the answer. Then the chest is read again, what goes in is measured, and only that much leaves you. So nothing is lost or copied when a friend uses the same chests at the same moment. A chest you're the owner of already needs no asking, so at your own base it's instant. Marking a chest as yours asks the same way, and the mark is sent to everyone nearby at once and kept with the world.

## Good to know

- **Chests far away.** On a server, the game hands a chest only to a player within about 64 to 96 m of it; a chest farther out that no game can answer for is claimed by your own game when it's needed (since this version, with the setting "Chests beyond the server's reach" switched on: off out of the box), as the game's portals do, and used once the server's newest copy is in, between the server's two-second rounds: so it can take a second or two longer than a near chest. Chests your game hasn't loaded can't be used: at the usual "Classic" simulation distance that's beyond about 128 m (up to 160 m, more toward the corners of the area).
- **It replaces HexQuickStackStorage's quick stack and favorites,** written from scratch. While HexQuickStackStorage is installed, StoreStash stays off and says so in the log: uninstall HexQuickStackStorage (or switch it off in the mod manager) to use it. The first time it runs, your HexQuickStackStorage favorites are taken in (its file is only read, and only once). Its sorting and trash can are replaced too (0.3.0), and your HexQuickStackStorage trash marks are taken in the first time, only read.
- **Your favorites live in** `BepInEx\config\modprojects.marks.favorites.txt`, and **your trash marks** in `BepInEx\config\modprojects.marks.trash.txt`, one item per line, for your whole mod profile (every character).
- **A chest is only ever sorted while your game holds it** (as it opens, or with its window open), so a sort never touches a chest someone else is using. On a server whose own mod re-sorts chests after they close, the two orders meet only in the order of the cells.
- **A near miss beside the trash can** while you carry an item drops it on the ground, as a click anywhere outside the window does.
- **Only you need it.** It's client-side: neither the server nor the other players need it. A friend without it can still put things into your chests the old way.
- **Rare cases, written down:** if the game crashes right after a quick stack, before your character is saved, the items can be in both the chests and your inventory, as with the game's own Place stacks. A friend still running HexQuickStackStorage writes into chests without asking first, so within about a second of your quick stack his P could put a chest back as it was.
- **An item from a mod the server doesn't have** (such as AdvancedTerrainModifiers' Shovel) is deleted from a chest when a mod on the server re-sorts that chest, however it got there. With "Only into chests that already hold the item" on (the default), quick stack only ever puts such an item into a chest that already holds one; keep it with you, or make it a favorite.
- **A mark belongs to the character** who set it, as the game's Personal Chests do: your other characters see it as someone else's. Only whoever marked a chest can take the mark off. Anyone may mark a chest they could open by hand. A friend without the pack sees an ordinary chest, and their quick stack isn't affected; nor is a friend's on an older version of the pack, from before marks, or a friend's who still runs HexQuickStackStorage (StoreStash stays off for them, and Hex doesn't read marks).
- **"Use my personal chests" off** keeps P and its button out of your Personal Chests and the chests you marked, each kind while H counts it, so only H fills them; never out of every chest you built.
- **Keyboard and mouse:** the keys and the favorite and trash marking have no gamepad control (the buttons work with a pointer); a gamepad's B says Keep to the trash can's question.
- **Switched off,** the keys and the buttons go, Place stacks and holding E are the game's own again, and the borders and the "Yours" lines go; in your game, marked chests are like any other. Your favorites and trash marks stay in their files, and the marks on the chests, for when it's on again. Sorting and the trash can each switch off on their own too (their settings).
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Works with, and what to switch off instead

- **EquipmentAndQuickSlots:** its equipment and quick slots, and its extra inventory rows, are never touched.
- **Reserve Stock** (in the pack): a reserved chest never gets a quick stack, from P or from H, personal or not. By hand, Place stacks and holding E still work there unless a setting says otherwise (StoreStash's or Reserve Stock's own, either one).
- **SeidrChest:** its bound Personal Chest is an ordinary Personal Chest here; nothing of it changes.
- **Handyman** (in the pack): its "Own key" is H too, but only used if you switch its "Key" to OwnKey; then change one of the two.
- **Craft from Chests** (in the pack): its "leave one of each item" keeps chests findable for quick stack.
- **MyLittleUI:** a chest's name shows in its setup window's title, and a name given there shows in MyLittleUI's hover (both keep it in the chest); its hover text is otherwise unaffected.
- **A mod on the server that re-sorts chests:** only the order of the cells changes.
- **MultiUserChest:** while it's installed, chests aren't sorted (two players can then have one chest open, each with a copy); your inventory still is.
- **MyLittleUI:** its Left Alt + left-click posts an item in chat; the trash mark is Left Alt + right-click (a setting), a slip away.
- **Another sorting mod:** switch off StoreStash's Sorting (its setting "Sorting"). **Another trash mod:** switch off its Trash ("Trash"). The rest of StoreStash keeps working.
- **Another quick-stack mod** (QuickStackPlus, Quick Stack Store Sort Trash Restock, AutoStash, AzuAutoStore): switch StoreStash off (its Enabled setting), and the other mod's quick stack, Place stacks and hold-E work alone. HexQuickStackStorage needs nothing: StoreStash stands aside by itself.
- **Another mod that sets chests up** (RunicStorage, AutoStash's filters, QuickStackPlus' Smart Storage, BestAutoSort): switch "Chest setups" off, and StoreStash's quick stack ignores its setups.
- **Another mod that gathers or consolidates chests** (ChestButler's Organize, Hearthkeeper's Consolidate, OdinStorage's Consolidate, RunicStorage's SmartSort): switch "Stack Overflow" off, and StoreStash never tidies; its quick stack and setups keep working.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-StoreStash-<version>.zip`. Keep the author `drumcowski` and the name `StoreStash` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `StoreStash.dll` into a folder named `drumcowski-StoreStash` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.storestash.cfg`, which appears after the first start; in the bundle, in the section "StoreStash" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: no keys or buttons, no sorting or trash can, no chest setups or tidying, the game's own Place stacks and holding E, no borders or "Yours" lines. Your favorites and marks are kept. |
| Quick stack key | P | Quick stacks to the chests nearby. |
| Quick stack button beside the inventory | on | The button between the armour and weight boxes. |
| Button position (offset) | 0, 0 | Moves the button, if another mod puts something there. |
| Search radius (metres) | 150 | How far a chest may be (5 to 150). |
| Which chests | any chest I can open | Or only chests, carts and ships you built. |
| Chests beyond the server's reach | off | On a server, the game hands a chest only to a player within about 64 to 96 m of it; on: a chest farther out that no game can answer for (never a cart's or a ship's) is claimed by your game when it's needed, as the game's portals do, and used once the server's newest copy is in, between the server's rounds (a second or two longer than a near chest). Off: such chests are left out, as before 0.31.0. Alone or hosting, it changes nothing. |
| Use my personal chests | on | Off: P and its button leave your personal chests alone (your Personal Chests and chests you marked, each while H counts it), so only H fills them. |
| Carts' chests | on | Use the chests of carts nearby. |
| Ships' chests | on | Use ships' chests, never with someone else aboard. |
| Only into chests that already hold the item | on | Off: into any chest, those holding it first, then the nearest (the key and the button only). |
| Trophies | any chest holding a trophy | Or like any other item (only a chest holding that trophy), or never. |
| The hotbar (top row) | unfavorited only | Or off (the hotbar stays), or everything but what you wear. |
| Top up part-stacks before empty cells | on | Off: chest by chest, nearest first. |
| Which chest first | chests set up for it, the most specific setup, the one holding the most | A list you drag (or Up and Down), each rule with a box: Chests set up for it, ...the most specific setup, ...the one holding the most of it, My personal chests (off). The first rule that tells two chests apart decides, and that chest fills first. |
| Quick stack while a chest is open | on | Off: the key and the button do nothing while a chest's window is open. |
| Place stacks follows these rules | on | Off: the game's own Place stacks. |
| Holding E follows these rules | on | Off: the game's own. |
| Place stacks and holding E leave reserved chests alone too | off | On: they do nothing at a reserved chest, and say why. |
| Message after a quick stack | top left | Or in the middle of the screen, or none (with "Say where things went" on, its lines still show, top left). |
| Seconds per extra line | 1 | Each line after the first keeps a message after a quick stack fully shown this much longer (0 to 5). A one-line message fades as the game's do. |
| Longest on screen (seconds) | 10 | The most a message after a quick stack stays on screen, its fade included (4 to 30). |
| Sound after a quick stack | on | The game's own sound of moving items. |
| Light up the chests that received items | on | They glow for two seconds. |
| Light-up colour | gold | |
| Favorite: key held with a right-click | Left Shift | None switches marking off (your favorites are kept). |
| Favorite borders | on | A border around every favorite in your inventory. |
| Favorite border colour | gold | |
| Favorites in chest windows too | off | Borders, and the right-click marking, in a chest's window as well. |
| Take in my HexQuickStackStorage favorites (once) | on | Reads `HexQuickStackStorage.favorites.txt` once, if it's there. |
| Personal quick stack key | H | Quick stacks into your personal chests nearby only, by the settings above. (Handyman's own key is H too, used only if you pick it there: then change one.) |
| Personal quick stack button beside the inventory | on | A button that does what H does. With P's button on too, the two share the space between the armour and weight boxes, a little smaller, this one below; alone, it takes P's place. |
| Personal quick stack: what goes in | as the quick stack | As "Only into chests that already hold the item" says; or only what your chests already hold; or anything, into your chests holding it first, then the nearest (a Personal Chest has 6 cells). |
| Personal chests: the game's Personal Chests I built | on | Your own Personal Chests count as personal chests. |
| Personal chests: chests I marked as mine | on | Chests you marked count, whoever built them. |
| Personal chests: every chest I built | off | Every chest, cart and ship you built with this character counts, unless someone else marked it. |
| Mark a chest as mine: key | Left Alt + H | Pointing at a chest, or with its window open: marks it as yours, or takes your mark off. |
| Quick stack leaves chests others marked alone | on | P puts nothing into a chest someone else marked (H never counts one as yours). Place stacks and holding E still work there. |
| Show whose a marked chest is | on | Pointing at a marked chest shows "Yours", or whose it is. |
| Colour of "Yours" | gold | |
| The mark key in every chest's hover text | off | Pointing at a chest you could mark shows the key and what it would do. |
| Personal button position (offset) | 0, 0 | Moves the personal button. |

**Sorting** (in the single release's file, the section "Sorting"):

| Setting | Default | What it does |
|---|---|---|
| Sorting | on | Off: no sort key or buttons, and no chest sorted as it opens. |
| Sort key (inventory open) | S | Sorts your inventory while it's open. |
| Sort key with a chest open | my inventory | Or the chest, or both. |
| Sort button beside the inventory | on | The button below the weight box. |
| Sort button position (offset) | 0, 0 | Moves it, if another mod puts something there. |
| Sort order | the pack's order | Or by type, by name, by weight (heaviest first), by value (most valuable first). |
| The pack's order: families | his order | The families in the order the pack's order sorts them, each family's groups in brackets. |
| Keep the hotbar in place | on | Off: the hotbar is sorted with the rest. |
| Keep what you wear in place | on | Off: what you wear moves with the rest (you still wear it); it's never merged either way. |
| Keep favorites in place | on | Off: favorites are sorted with the rest. |
| Merge part-stacks | on | Off: every stack keeps its amount. |
| Sorted items start | top | Or bottom: the empty cells are left at the top. |
| Sort button in a chest's window | on | The button left of Place stacks. |
| Chest sort button position (offset) | 0, 0 | Moves it. |
| Sort a chest as it opens | every chest | Or only chests you built, or off. |
| Sort as it opens: chests others marked too | off | Off: a chest someone else marked as theirs isn't sorted as it opens (its Sort button still sorts it). Your own marked chests are sorted either way. |
| Sound after sorting | on | The game's own sound of moving items, after the key or a button sorted something. |

**Trash** (in the single release's file, the section "Trash"):

| Setting | Default | What it does |
|---|---|---|
| Trash | on | Off: no trash marking, borders or can (your marks are kept). |
| Trash: key held with a right-click | Left Alt | None switches marking off (your marks are kept). |
| Trash borders | on | A red border around every item marked as trash. |
| Trash border colour | red | |
| Trash marks in chest windows too | off | Borders, and the right-click marking, in a chest's window as well. |
| Trash can beside the inventory | on | The can above the armour box. |
| Trash can position (offset) | 0, 0 | Moves it. |
| Ask before deleting | always | Or only for more than one stack, or never. |
| The can deletes an item you're carrying | on | Off: the can asks you to put it down. |
| ...a carried favorite or hotbar item too | off | On: a carried favorite, or one from your hotbar or quick slots, can be deleted. What you wear or hold, and quest items, never. |
| Message after deleting | on | "Deleted 3 kinds, 57 items", top left. |
| Take in my HexQuickStackStorage trash marks (once) | on | Reads `HexQuickStackStorage.junk.txt` once, if it's there. |

Two more lines in the file aren't choices and aren't listed in F1: `HotbarNoteShown` only remembers that the note about the hotbar was shown, and `SettingsVersion` that the settings from before 0.5.0 were carried over.

In F1 the settings sit under StoreStash's one heading: the quick stack's own at the top, and each feature after them (Favorites, Personal Stacker, Sorting, Trash, Chest setups, Stack Overflow) behind a row that opens and closes it ("[+] Chest setups"), and inside each the settings sit in small groups, each behind a row of its own ("[+] Copy and paste"; the quick stack's own first few stay open, the rest in four groups), with shudnal's ConfigurationManager 1.1.22 or later; with another manager they all show open. The three switches 0.4.0 had for the order of set-up chests are carried into "Which chest first" once, and taken out of the file.

### Chest setups

| Setting | Default | What it does |
|---|---|---|
| Chest setups | on | Off: quick stack ignores every setup; no setup window, button, keys or hover line. Setups stay in the chests. |
| Setup button in the chest window | on | A button under Reserve Stock's padlock that opens the chest's setup window. |
| Setup button position (offset) | 0, 0 | Moves the button, if another mod puts something there. |
| Setup key | Left Alt + J | Pointing at a chest: opens it with its setup window. With a chest's window open: opens or closes its setup window. |
| Copy and Paste buttons | on | In the setup window. |
| Copy key / Paste key | Left Ctrl + C / Left Ctrl + V | With a chest's window open only. |
| Pasting over a setup | replace | Or add: the copy's picks, left-out things and biomes join the chest's own. |
| Copy to the clipboard too | off | Copy also puts the setup on your computer's clipboard as text, and Paste reads one from there. |
| A set-up chest also takes what it holds | off | Off: strict, only what its setup takes. |
| What set-up chests can't fit goes on | on | Off: it stays with you (also while its set-up chest is open elsewhere). |
| What no setup takes goes to other chests | on | Off: quick stack only puts into set-up chests. |
| Place stacks and holding E follow a setup | on | Off: they put in only what the chest holds. |
| Say what a set-up chest takes | on | The line under its name when you point at it. |
| Setup key hint on every chest | off | The key under every chest you could open and set up. |
| Carts' and ships' chests can be set up | on | Off: their setups are ignored and can't be changed. |
| Hide what you haven't found yet | on | The setup window shows only what this character has found. |
| Rename the chest in the setup window | on | Click the name in the window's title to rename the chest. Off: the name only shows. |
| Say where things went | on | After a quick stack, what each set-up chest took for its setup, in place of the usual line (with "N items into M other chests" first when other chests got things too), in one message where the quick stack's own line goes (top left when that one is off). |

### Stack Overflow

| Setting | Default | What it does |
|---|---|---|
| Stack Overflow | on | Off: no tidying (no key, button or tidy by itself). Quick stack's order is "Which chest first"'s either way. |
| Tidy key | Left Alt + N | Pointing at a set-up chest, or with its window open: tidies the chests set up like it. |
| Tidy button in the chest window | off | A button under the setup button that does what the key does. |
| Tidy button position (offset) | 0, 0 | Moves the button, if another mod puts something there. |
| What a tidy covers | the chest's group | Or every group of chests set up alike within reach, wherever you point. |
| Ask first when a tidy would move more than (stacks) | 0 | A bigger tidy says how many first, and a second press within 10 seconds tidies (0: never ask; up to 2000). A tidy by itself never asks: it moves no more than this many, and the rest waits for a press. |
| Chests set up alike means | identical | Or the same once their biomes are left out. |
| Which chest keeps a kind | the one holding the most | Or the biggest (most cells), or the nearest to you. A kind already all in one chest stays there whichever you pick, unless a bigger kind needs its room. |
| Move other things out of the way | on | Off: a kind only moves into a preferred chest's free room. |
| Merge part-stacks between chests | on | Part-stacks of one thing in two of its chests are topped up into the one that keeps it. |
| Leave free cells in each chest | 0 | A tidy keeps this many cells empty in each chest where it can (0 to 10): a kind goes into a chest that keeps them if one can hold all of it, and uses them rather than be split over more chests. |
| Keep free cells even if a kind splits | off | On: the free cells are kept while the group has room for them, a kind split over two chests if it must. |
| Things no setup in the group takes | left where they are | Or gathered too, among the group's chests. |
| Sort the chests a tidy changed | like opening | As "Sort a chest as it opens" says (Sorting); or always; or never. |
| Most stacks moved per tidy | 500 | Then it stops, and a press again goes on (50 to 2000). |
| Tidy radius (metres) | 0 | 0: quick stack's search radius; else up to 150. |
| Carts' chests take part | off | A set-up cart's chest is tidied with the chests set up like it. |
| Ships' chests take part | off | A set-up ship's chest too, never one with someone else aboard. |
| Leave chests others marked alone | on | A chest someone else marked as theirs is never tidied. |
| Tidy after each quick stack | off | The groups of the set-up chests a quick stack put things into, a moment after it. |
| Tidy every so many minutes | 0 | Every group within reach this often (0: never; up to 60). |
| Say what moved | one line | Or one line and a line per chest things went into, or nothing (it still says why it couldn't tidy). |
| Sound after a tidy | on | The game's own sound of moving items. |
| Light up the chests a tidy changed | off | They glow for two seconds, in the light-up colour. |
| Tidy key hint on set-up chests | off | The tidy key under every set-up chest you point at. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "StoreStash". If HexQuickStackStorage is installed, it says so and stays off. If your favorites file can't be read, quick stack, Place stacks and holding E do nothing and say so, rather than put your favorites away; the file is left as it is. The same for sorting (with favorites kept in place) and the trash can (which then deletes nothing, if either marks file can't be read). If only sorting's or the trash can's parts of the game can't be found, those alone stay off, and the log says so. If the game has changed so that the mod can't find what it needs, it switches itself off and says why; if only Place stacks or holding E can't be found, those stay the game's own and the log says so. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\StoreStash.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-StoreStash-<version>.zip`. Until StoreStash is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, `parts\Chests\` a copy of the QoL Mods chest service (which does the quick stack itself: finding, asking, putting, and following chests' setups), `parts\ItemMarks\` a copy of the pack's item marks (the favorites and trash marks), and `parts\Catalogue\` a copy of the pack's catalogue (the order sorting follows, and what each item is, for chest setups and their window); how a module and a part are made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`.
