# Changelog

## 0.1.13 (2026-10-11)

- The chest service it carries changed: its settings and messages name no mod it doesn't work with or make room for (a chest the server holds is "the server (a mod running on the server)"), and in **Chests (every mod)**, the list of chest distances says when one of Topper-Upper's distances is 0 and takes the range above. Add to Cart 0.5.3, Craft from Chests 0.5.1, Outfitter 0.6.1, StoreStash 0.7.3, Topper-Upper 0.4.0 carry it too. Its README names no server mod either. Nothing else changes here.

## 0.1.12 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.1.11 (2026-10-10)

- The chest service it carries talks to miikeskii's Server-Side QoL Pack on the server: it says hello to it, and when the server pack runs Far Fetched, your game learns from it which chests it hands you (a server running Far Fetched alone works as before). Add to Cart 0.5.1, Craft from Chests 0.4.1, Outfitter 0.5.4, StoreStash 0.7.1 and Topper-Upper 0.2.0 carry it too. Nothing else changes here.

## 0.1.10 (2026-10-09)

- No padlock at a friend's chest you may only look at, and the key refuses there; HexQuickStackStorage's quick stack and Place stacks keep out of a friend's marked chest as its owner asks.
- The Obliterator can't be reserved; a reserve from before comes off with the padlock or the key.
- The chest service it carries changed (StoreStash 0.7.0, Add to Cart 0.5.0).

## 0.1.9 (2026-10-08)

- The chest service it carries changed: it switches off the "nearest first" sending order of Skarif's Valheim Performance Overhaul, which left chests stuck on a server, with its setting in a new section, **Performance mods (every mod)**, as in Craft from Chests 0.3.7. Add to Cart 0.4.10, StoreStash 0.6.3 and Outfitter 0.5.2 carry it too.

## 0.1.8 (2026-10-08)

- **The chest service it carries changed**: a chest your game can't get at all is left out instead of being asked for and timing out; Far Fetched on the server (a separate server mod) is recognised, and chests it hands you are used at once; a far chest is never claimed where the server would hand it out itself. Add to Cart 0.4.9, Craft from Chests 0.3.6, StoreStash 0.6.2 and Outfitter 0.5.1 carry it too.

## 0.1.7 (2026-10-06)

- **The chest service it carries changed** (Add to Cart 0.4.8, Craft from Chests 0.3.5 and StoreStash 0.6.1 carry it too): **Chests beyond the server's hand-out work now**: on a server, the game hands a chest only to a player within about 64 to 96 m of it, so a chest loaded farther out with nobody near it never answered ("owners didn't answer in time"). The chest service it carries now claims such a chest itself, as the game's own portals do: only one no game can answer for (nobody's, its owner gone from the game, or a dedicated server's far from the world's centre), never a cart's or a ship's; it changes it only once the server's newest copy has come in and between the server's two-second rounds, so such a chest can take a second or two longer than a near one, and it lets it go again right after. The modules that take from chests far away have a setting for it, "Chests beyond the server's reach", off out of the box. Alone or hosting, and within your own area, nothing changes; on a very slow connection such chests are left out, as before. One rare case stays, written in the chest service: with ServersideQoL on the server, that mod writing the same far chest in the instant your change is late reaching the server could copy or lose that one change. Nothing depends on a performance mod: with or without Skarif's ValheimPerformanceOverhaul (which never touches chests) it works the same.

## 0.1.6 (2026-10-05)

- **The chest service it carries changed** (with StoreStash 0.5.0's "Which chest first"; Add to Cart 0.4.7 and Craft from Chests 0.3.4 carry it too): a quick stack that asks orders the chests that may take an item by a list of rules (chests set up for it, the most specific setup, the one holding the most of it, personal chests), each rule's first chest filling before the next, and a set-up chest's room goes to what its setup takes before what it merely holds. Nothing changes in how it reserves: a reserved chest gets nothing from any quick stack.

## 0.1.5 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.4.0's chest setups; Add to Cart 0.4.5 and Craft from Chests 0.3.3 carry it too): a chest can carry a setup, and a quick stack that asks follows it, set-up chests first; a chest reserved while a quick stack's claim waited gets that stack another round without it. Nothing changes in how it reserves: a reserved chest gets nothing from any quick stack, set up or not.

## 0.1.4 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.2.0's personal chests; Add to Cart 0.4.4 and Craft from Chests 0.3.2 carry it too): it knows the "mine" mark a player can set on a chest with StoreStash's Left Alt + H. Nothing changes in how it reserves: a reserved chest gets nothing from any quick stack, a personal one included.

## 0.1.3 (2026-09-30)

- **The pack's own quick stack, StoreStash, leaves reserved chests alone**, as HexQuickStackStorage's did with this mod: a reserved chest never gets a quick stack. Its descriptions say so, and that switching Reserve Stock off gives reserved chests back to HexQuickStackStorage's quick stack only (StoreStash, Craft from Chests and Add to Cart still leave them alone). Its setting "Place stacks leaves reserved chests alone too" works as before; StoreStash has one of the same name for its own Place stacks and holding E, and either one on leaves reserved chests alone.
- **The chest service it carries changed** (with StoreStash 0.1.0): it can now put into chests and move between them; nothing changes in how it reserves.

## 0.1.2 (2026-09-29)

- **The chest service it carries changed** (with Craft from Chests 0.3.0 and Add to Cart 0.4.0, which share it): a chest you have open is shared with a friend whose Craft from Chests or Add to Cart asks for what's in it: your game takes it out of the chest in your open window and hands it over, never from a reserved chest unless their Craft from Chests uses reserved chests (its own setting). With MultiUserChest installed, reserving a chest someone has open says so after a second and a half (not 5). While every chest mod of the pack is switched off, nothing is shared. And if another mod moves a stack out of a chest in the middle of a take, what had arrived goes back, so nothing is copied. Reserve Stock itself is unchanged.

## 0.1.1 (2026-09-29)

- **The padlock button in the chest window is on by default**, as miikeskii asked after his test of 0.1.0. A settings file that already holds the setting keeps its value: switch it in the settings (Button in the chest window) if yours is off.

## 0.1.0 (2026-09-28)

- **First version**, as a mod of miikeskii's QoL Pack and on its own.
- **Reserve a chest** with Left Alt + E while pointing at it (a setting), or a padlock button beside Place stacks in its window (a setting, off at first so nobody's chest window changes unasked). The mark is saved on the chest, in the world, so it lasts and every friend running the pack sees it. Anyone who could open the chest can reserve or free it; not while someone has it open.
- **HexQuickStackStorage's quick stack** to nearby chests (P and Q) leaves reserved chests alone. Checked against HexQuickStackStorage 1.4.3; if a later version changes, that part switches itself off and says so in the log.
- **Craft from Chests and Add to Cart** never take from a reserved chest (the pack's chest service honours the mark); Craft from Chests' menus show a padlock with how many are reserved (its own setting).
- **A line, Reserved,** in a reserved chest's hover text; the key's hint on every chest, a setting (off).
- **Place stacks by hand** still works at a reserved chest; a setting makes Place stacks and holding E leave it alone too.
