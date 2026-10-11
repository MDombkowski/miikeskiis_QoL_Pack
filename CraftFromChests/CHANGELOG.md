# Changelog

## 0.4.0 (2026-10-09)

- **My personal chests first** (card A80, miikeskii's feedback of 2026-10-09; new setting, on): crafting, building and filling stations take from your personal chests first (the game's Personal Chests you built and chests marked as yours, as StoreStash counts them).
- Each chest now gives exactly what was planned from it (a take could draw on different chests than its plan, with the right totals).
- Leaves the Obliterator alone, and a friend's marked chest whose owner keeps other players' mods out.
- The chest service it carries changed (StoreStash 0.7.0, Add to Cart 0.5.0).

## 0.3.7 (2026-10-08)

- **Skarif's Valheim Performance Overhaul no longer leaves chests stuck** (card A69, miikeskii's report of 2026-10-08: building from chests 50 to 60 m away kept failing on his friend's server, and much less with Skarif's mod off). That mod makes your game send its updates to the server nearest first, every time, so in a busy base the news that a chest changed hands could wait for minutes, and your game and the server each thought the other had it. The chest service now switches off just that part of Skarif's mod while the game runs: a new section in F1, **Performance mods (every mod)**, with **Skarif's mod: send in the game's own order** (on). Nothing changes without Skarif's mod.
- **Each item says how far its own chest is:** "Getting Wood (54 m) and Stone (56 m) from chests: place it again in a moment." (miikeskii's format), instead of one distance for them all. Crafting and stations say it so too.
- **"Beyond the server's reach" only when it is:** a chest your game claimed itself because the server hadn't handed it over (55 m away) was called beyond the server's reach.

## 0.3.6 (2026-10-08)

- **One place for the chest settings every chest mod must agree on** (card A69): a new section in F1, **Chests (every mod)**, holds the chest range (up to 250 m now, 150 out of the box), **Chests beyond the server's reach** (now **on** out of the box, one switch for Add to Cart, Craft from Chests, StoreStash and Outfitter instead of four) and **Write chest problems to the log** (on). This mod's own "Chest range" is 0 out of the box, which follows the shared one; set it to give this mod a range of its own. A settings file still at the old 150 moves to 0 once.
- **Building no longer says "Getting the materials from a chest nearby: place it again in a moment" for a chest that can't come.** It says which item, how far its chest is, and why: on its way (place it again), beyond the server's reach (it can take a few tries), someone has it open, or your game can't get it (with what to do). Crafting and stations say the same when they come up short.
- **Honest messages:** when the chests can't give something, it says which item, how far its chest is and why (another player's game has it, the server hasn't handed it over yet, it's beyond the server's reach, or your game can't get it at all), and with the log setting on, writes each chest and its situation to the log.
- **The chest service it carries changed** (card A69): a chest your game can't get at all is left out instead of being asked for and timing out; Far Fetched on the server (card A70, a separate server mod) is recognised, and chests it hands you are used at once; a far chest is never claimed where the server would hand it out itself.

## 0.3.5 (2026-10-06)

- **miikeskii's own setting is the default now** (card A63): "Use my personal chests" is on out of the box.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.
- **Chests beyond the server's hand-out work now** (card A64): on a server, the game hands a chest only to a player within about 64 to 96 m of it, so a chest loaded farther out with nobody near it never answered ("owners didn't answer in time"). The chest service it carries now claims such a chest itself, as the game's own portals do: only one no game can answer for (nobody's, its owner gone from the game, or a dedicated server's far from the world's centre), never a cart's or a ship's; it changes it only once the server's newest copy has come in and between the server's two-second rounds, so such a chest can take a second or two longer than a near one, and it lets it go again right after. It is **off out of the box**: switch on the new setting "Chests beyond the server's reach" to use it (on your own server first). Alone or hosting, and within your own area, nothing changes; on a very slow connection such chests are left out, as before. One rare case stays, written in the chest service: with ServersideQoL on the server, that mod writing the same far chest in the instant your change is late reaching the server could copy or lose that one change. Nothing depends on a performance mod: with or without Skarif's ValheimPerformanceOverhaul (which never touches chests) it works the same. A craft's bar waits just short of its end for such a chest when it must (a second at most), and a station's rounds keep it.

## 0.3.4 (2026-10-05)

- **The chest service it carries changed** (with StoreStash 0.5.0's "Which chest first", card A41; Add to Cart 0.4.7 and Reserve Stock 0.1.6 carry it too): a quick stack that asks orders the chests that may take an item by a list of rules (chests set up for it, the most specific setup, the one holding the most of it, personal chests), each rule's first chest filling before the next, and a set-up chest's room goes to what its setup takes before what it merely holds. Crafting, building and stations take as before.

## 0.3.3 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.4.0's chest setups, card A41; Add to Cart 0.4.5 and Reserve Stock 0.1.5 carry it too): a chest can carry a setup, and a quick stack that asks follows it, set-up chests first; a chest reserved while a quick stack's claim waited gets that stack another round without it. Crafting, building and stations take as before: a setup doesn't keep a chest out.

## 0.3.2 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.2.0's personal chests, card A43; Add to Cart 0.4.4 and Reserve Stock 0.1.4 carry it too): it knows the "mine" mark a player can set on a chest, and a quick stack can leave chests someone else marked alone. Crafting, building and stations take as before: a mark doesn't keep a chest out (Reserve Stock's reserve does).

## 0.3.1 (2026-09-30)

- **The chest service it carries changed** (with StoreStash 0.1.0, the pack's own quick stack, which uses it; Add to Cart 0.4.2 and Reserve Stock 0.1.3 carry it too): it can now put into chests and move items between them, as safely as it takes (a chest asked of its owner first, added to before anything leaves, sent and held after), and it holds the quick stack itself. Craft from Chests itself is unchanged: crafting, building and stations take exactly as before.

## 0.3.0 (2026-09-29)

- **A chest a friend has open is used too, when their game runs miikeskii's QoL Pack** (0.10.0 or later; any of Craft from Chests, Add to Cart or Reserve Stock will do). Your game asks theirs for what you need; their game takes it out of the real chest, so their open window shows it leave at once, and hands it over. It lands in your inventory, and is used from there: during the crafting bar, before a station is fed, and, when building, at your next click (the first click says "Getting the materials from a chest nearby: place it again in a moment."). Only what fits in your inventory is asked for. The menus count such a chest like any other, and follow what your friend moves in or out of it within a second. **A friend without the pack, or with an older one, keeps their open chest to themselves, as before:** it's left out of the counts until they close it.
- Your game says hello to the other players' games when you join, to learn which of them share; nothing is added to the world, and nothing is needed on the server.
- **If a friend's game doesn't answer within 4 seconds,** the craft, click or station goes on without that chest; if the items still come, they land in your inventory, with a word. What doesn't fit in your inventory by then is dropped at your feet, with a word. The rare cases: a player whose game crashes or leaves in the instant between the take and its arrival loses what was on its way; and if your friend's game crashes in the instant right after its take, their chest may still hold what you received.
- **Your own open chests** are shared the same way with friends who ask: what they take leaves your window as you watch. The stack you're dragging or splitting is left alone. Switch every chest mod of the pack off (Craft from Chests, Add to Cart and Reserve Stock, each Enabled off, or Left Alt + O where there's no other) and your open chests aren't shared; your friends' games are told.
- **With MultiUserChest installed** (a mod that lets several players open one chest): a chest someone has open is said to be open after a second and a half, instead of after 5 seconds as "a chest's owner didn't answer in time", and the three seconds after a take, during which a friend's E on that chest waits, still hold.
- **The chest service:** if another mod moves a stack out of a chest in the middle of a take (re-sorting the chest as your inventory changes), what had arrived goes back out of your inventory, so nothing is copied (older than this version).
- Stacks alike in every way are taken in a fixed order (the lower world level, then the first cell), so a friend's game and yours pick stacks the same way.

## 0.2.1 (2026-09-29)

- **No more warning in the log for the padlock's number** ("The LiberationSans SDF Font Asset was not found … CraftFromChests_Reserved_Amount", once for each padlock made): the number is now given the menus' own font before it starts. What shows is unchanged.

## 0.2.0 (2026-09-28)

- **Reserved chests** (Reserve Stock, new in miikeskii's QoL Pack 0.8.0): a new setting, "Reserved chests", says how chests reserved with Reserve Stock count. **LeftOutWithLock**, the default: they're never used, and an amount in the crafting and build menus shows a small orange padlock with how many more the reserved chests nearby hold. **LeftOut**: never used, nothing shown. **UsedAsUsual**: crafting, building and stations use them like any other chest (quick stack and Add to Cart still leave them alone).
- **A chest reserved while a craft's bar runs** is left out of that craft too, unless reserved chests are used as usual: if the other chests can't make up for it, the craft is refused and says why, rather than coming up short.
- The chest service (the pack's `parts\Chests`) carries the setting's third choice, and the padlock is drawn by it, the same as Reserve Stock's.
- **The chest service:** renaming a chest with MyLittleUI (Shift + E) while the pack is taking from it no longer opens the chest's window under the rename box (its check now runs after MyLittleUI's).

## 0.1.1 (2026-09-28)

- **A run of crafts no longer stops on a server that sorts chests.** On a server that sorts chests or feeds stations from them (the server mod ServersideQoL does both, asking for each chest the way a player's E key does), every craft that took from a chest made the server ask for that chest, and the mod mistook the server for a player opening it: it left the chest out of the counts for up to five seconds, so an ingredient showed 0 and the next craft in the run was refused (miikeskii's test: meals at a cauldron, sets of arrows). Now the server's request still waits for the few seconds after a take, as every other computer's does, but only a player standing at the chest makes it drop out of the counts: a run of crafts keeps its chests, and the server gets them once you stop. And while a craft's bar runs, the chests it will take from stay yours against the server (a friend's E still gets one at once), so even one long bar, such as the game's own craft-several, finishes.

## 0.1.0 (2026-09-27)

- **First version**, as a mod of miikeskii's QoL Pack and on its own: our own craft-from-chests, to replace AzuCraftyBoxes.
- **Crafting** at any station and by hand uses what the chests nearby hold: what you carry first, the rest from the nearest chests as the crafting bar ends. Upgrades, the game's "craft five", MyLittleUI's multicraft and the Raw Fish recipe (any one fish) work too.
- **Building** with the hammer, hoe and cultivator uses the chests; the chests holding the picked piece's materials are asked for as soon as it's picked.
- **Stations** take from the chests: E with none on you takes one; Shift + E fills the station, from what you carry and then the chests. Smelters, the charcoal kiln (plain Wood only, a setting), blast furnace, spinning wheel, windmill, eitr refinery, fires and torches, cooking stations and the oven, the fermenter, the shield generator and the ballista.
- **The menus** show "have/need" counting the chests (an amount made up by the chests flashes yellow), and the piece in hand shows how many can be placed.
- **Safe with friends:** every take goes through the pack's chest service, which asks the chest's owner first, as the game's own chest window does. A chest a friend has open is never used; what you use is exactly what leaves the chests. Nobody is told a chest is in use because of it.
- **Left Alt + O** switches it on and off. Stays off, and says why, while AzuCraftyBoxes or CraftFromContainers is installed.
