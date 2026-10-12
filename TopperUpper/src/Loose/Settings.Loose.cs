using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace TopperUpper
{
    /// <summary>How the loose-item pickup says what it put away.</summary>
    internal enum LooseSay
    {
        Off,
        OverTheChest,
        TopLeft,
    }

    /// <summary>
    /// The loose-item pickup's settings (card A77's "Loose items"): the part "Loose items", its own band of orders (249 down
    /// to 200), bound between "Linking and products" and "Friends and the server". Keys are permanent from 0.1.0. The
    /// pickup is on out of the box since 0.4.0 (card B05; his "off at first" held until the server's own pickup was gone,
    /// and a file still holding the old off moves once); the log fix is its own switch, on.
    /// </summary>
    internal static partial class Settings
    {
        internal static ConfigEntry<bool> PickUpLoose { get; private set; }
        internal static ConfigEntry<float> LooseRange { get; private set; }
        internal static ConfigEntry<float> LooseWait { get; private set; }
        internal static ConfigEntry<float> LoosePlayerDistance { get; private set; }
        internal static ConfigEntry<string> LooseNever { get; private set; }
        internal static ConfigEntry<bool> LooseDropped { get; private set; }
        internal static ConfigEntry<bool> LooseFodder { get; private set; }
        internal static ConfigEntry<bool> LooseEggs { get; private set; }
        internal static ConfigEntry<bool> LooseOnlyWhereHeld { get; private set; }
        internal static ConfigEntry<bool> LooseFollowSetups { get; private set; }
        internal static ConfigEntry<bool> LooseLeaveOthersMarked { get; private set; }
        internal static ConfigEntry<LooseSay> LooseSayPut { get; private set; }
        internal static ConfigEntry<float> LooseEvery { get; private set; }
        internal static ConfigEntry<int> LooseScanPerFrame { get; private set; }
        internal static ConfigEntry<int> LoosePutsPerFrame { get; private set; }
        internal static ConfigEntry<int> LooseChestsPerFrame { get; private set; }
        internal static ConfigEntry<bool> SkipRepeatAskLog { get; private set; }

        static partial void BindLooseItems(ModuleSettings settings)
        {
            BeginFold(LoosePart, LooseOrderTop);
            PickUpLoose = Add(LoosePart, "PickUpLoose", true, "Pick up loose items into chests",
                "Items lying on the ground within Topper-Upper's own \"Chest range\" (at the top) of you go into chests within \"Range from the item " +
                "to the chest\" (below) that already hold them, in batches: one put and one line per chest (\"Stone x87\"), a " +
                "few frames' work for a whole stone deposit, and never a request to another player's game. Only what your own " +
                "game looks after (what you mined, chopped or killed, what fell near you first); a few seconds after it's made, " +
                "once it lies still on something or floats in water; never near a player, nor what you dropped on purpose " +
                "(until the area loads again: the game forgets that mark). With miikeskii's Server-Side QoL Pack on the server, " +
                "friends' drops near you can be handed to your game to put away too. If a mod on the server picks up loose " +
                "items itself, it asks for every item first and takes them before this does; Topper-Upper then says so once, in " +
                "the log and on screen. Keep this off while another mod in your game picks up loose items.");
            // On since 0.4.0 (card B05): a file written while it was off out of the box moves once (MODULES.md rule 4).
            settings.MovedDefault(PickUpLoose, false, 1);
            LooseRange = Add(LoosePart, "LooseRange", 64f, "Range from the item to the chest (metres)",
                "How far from an item a chest may be to take it. " + ZeroFollows("from the item"),
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            ChestSettings.Elsewhere("Topper-Upper, " + LoosePart, "Range from the item to the chest (metres)", LooseRange,
                                    "from each loose item");   // card B13
            LooseWait = Add(LoosePart, "LooseWait", 3f, "Wait after it's made (seconds)",
                "How long after it's made an item is left before it's put away, so a falling deposit's stones have landed and " +
                "the game's own pickup gets its turn. An item still rolling or hanging in mid-air waits on after that; one " +
                "floating in water counts as lying still.",
                new AcceptableValueRange<float>(0.5f, 60f));
            LoosePlayerDistance = Add(LoosePart, "LoosePlayerDistance", 4f, "Leave items near a player (metres)",
                "Items within this distance of any player stay on the ground, so they can be picked up by hand; once everyone " +
                "has walked away they're put away. 0: no such rule.",
                new AcceptableValueRange<float>(0f, 30f));
            LooseNever = Add(LoosePart, "LooseNever", string.Empty, "Never pick up",
                "Kinds left on the ground, separated by commas: item types as the game names them (Trophy, Ammo, Fish, " +
                "Consumable, Material …) or items by prefab name (BoneFragments, Coins).");
            // Its Pick button in F1 (card B03): items and the game's item types picked from pictures, the text written as
            // NeverList reads it (commas or semicolons, any case).
            Lists.Attach(LooseNever, new ListSpec
            {
                Module = Module.Name,
                Kinds = new[] { ThingKind.Items, ThingKind.ItemTypes },
                Format = new ListFormat { Separators = ",;\n\r", Joiner = ", " },
                Says = "Kinds of item left on the ground, and single items.",
                Warn = Module.Warn,
            });
            LooseDropped = Add(LoosePart, "LooseDropped", false, "Also items dropped on purpose",
                "An item you drop from your inventory is one the game won't pick up by itself either; it's left alone. The game " +
                "keeps that mark only on the computer of the player who dropped it, and forgets it when the area loads again, " +
                "so a friend's drop that later passes to your game, or your own after a reload, is put away like any other. " +
                "On: what you drop is put away too.");
            LooseFodder = Add(LoosePart, "LooseFodder", true, "Leave food near a tame that eats it",
                "Food lying near an animal that eats it, tamed or one that can be tamed (so a pen's feed, or food put out to " +
                "tame one, stays), is left where it is: within the distance the animal looks for food, plus 5 m.");
            LooseEggs = Add(LoosePart, "LooseEggs", true, "Leave eggs that could hatch",
                "Every egg lying alone (a stack of one) stays where it is, warm or not, since one can hatch where it lies if it's " +
                "warm and under a roof.");
            LooseOnlyWhereHeld = Add(LoosePart, "LooseOnlyWhereHeld", true, "Only into chests already holding it",
                "As the quick stack: an item goes only into a chest already holding some (or set up to take it). Off: any " +
                "chest in range with room.");
            LooseFollowSetups = Add(LoosePart, "LooseFollowSetups", true, "Follow chest setups",
                "Chests set up with StoreStash to take only some things get only those, and get them first.");
            LooseLeaveOthersMarked = Add(LoosePart, "LooseLeaveOthersMarked", true, "Leave chests others marked as theirs alone",
                "A chest another player marked as theirs (StoreStash's Left Alt + H) gets nothing put away.");
            LooseSayPut = Add(LoosePart, "LooseSay", LooseSay.OverTheChest, "Say what was put away",
                "One line per chest for each batch, never one per item. OverTheChest: floating over the chest (\"Stone x87\"), " +
                "only on your screen; a chest farther than the game shows such text gets a line at the top left instead. " +
                "TopLeft: a line at the top left. Off: quietly.");
            LooseEvery = Add(LoosePart, "LooseEvery", 2f, "Look for loose items every (seconds)",
                "How often the items near you are looked over again.",
                new AcceptableValueRange<float>(0.5f, 30f));
            LooseScanPerFrame = Add(LoosePart, "LooseScanPerFrame", 64, "Items looked at per frame",
                "How many items on the ground are looked at in one frame. Looking is cheap; more finds things sooner.",
                new AcceptableValueRange<int>(8, 512));
            LoosePutsPerFrame = Add(LoosePart, "LoosePutsPerFrame", 40, "Items put away per frame",
                "How many items on the ground (each a stack lying there) go into chests in one frame. A broken stone deposit's " +
                "hundred-odd stones take a few frames at 40, so no frame is long.",
                new AcceptableValueRange<int>(1, 200));
            LooseChestsPerFrame = Add(LoosePart, "LooseChestsPerFrame", 4, "Chests put into per frame",
                "How many chests one frame's items may go into; the next frame goes on to the next chests.",
                new AcceptableValueRange<int>(1, 16));
            SkipRepeatAskLog = Add(LoosePart, "SkipRepeatAskLog", true, "Skip the log line for a repeat ownership request",
                "When another game (a mod on the server that picks items up, mostly) asks yours for an item your game no longer looks after, " +
                "the game only writes a log line or two and does nothing else; most of those lines in your log are such repeats. " +
                "On: those asks are skipped without the lines, and one line a minute says how many were skipped and from how many " +
                "askers. An item your game does look after is still handed over exactly as the game does it, with its line. " +
                "Works whether the pickup above is on or off. Switch it off while testing the freeze when a big deposit breaks: " +
                "those tests count the game's own lines.");
            EndFold();
        }
    }
}
