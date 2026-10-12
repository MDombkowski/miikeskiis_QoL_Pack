using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.Kit;
using TopperUpper.Logic;

namespace TopperUpper
{
    /// <summary>
    /// Topper-Upper as a module of miikeskii's QoL Pack (card A77, his name): lights and fires near him fuelled, and
    /// processing stations fed, from the chests nearby by his rules (reserves, maximums, priorities, the kiln's woods,
    /// leave-one); a key to leave one light or station alone; chests linked to a station for its input and output; and what
    /// stations make put away. Client-side: only the game that looks after a light or station (its owner) feeds it, taking
    /// only from chests that game already holds, so nothing is ever claimed and no ownership is ever asked for. It carries
    /// the chest service and the catalogue in its own parts\ folder. The same code runs in the bundle and in its single
    /// release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    ///
    /// The loose-item pickup (built on top, in its own files) hooks in through the partial methods TickLooseItems and
    /// ForgetLooseItems here and Settings.BindLooseItems, uses Module.Chests as its chest user, and GroundPut and Stow for
    /// its puts. The catch-up (0.2.0, card A93: stations make up the time players were on while nobody was near them) is
    /// in src\CatchUp\, its plain rules in src\Logic\ (CatchUpSim, PlayClock, WindMath, CatchUpChoice); it also tells
    /// miikeskii's Server-Side QoL Pack what this game feeds and picks up, and follows its settings for everyone.
    /// </summary>
    internal sealed partial class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.topperupper";

        internal const string Name = "Topper-Upper";

        internal const string Version = "0.6.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "TopperUpper";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("Topper-Upper - Lights and fires" …), listed
        /// under the one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: nothing is fed, topped up or put away, and lights and stations work as in the game. " +
            "On brings back each part switched on below: out of the box lights and fires, stations and loose items.";

        internal override string SwitchedOffNote => "Lights and stations work as in the game.";

        internal override string UpdateTask => "feeding lights and stations";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>A warning in the module's log, or nowhere while it has no host yet.</summary>
        internal static void Warn(string text) => current?.Host?.Log?.LogWarning(text);

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it: "Chests beyond the server's reach" is the pack's shared
        /// setting (card A69), and its range is what it asks of Far Fetched on the server (card A70). Topper-Upper itself
        /// never claims a chest (it takes only from chests this game holds, ChestClaims.MayChange), but the service still
        /// needs every user registered, and the loose-item pickup uses this same user.</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn,
            reach: () => Settings.Range == null ? 0f : Settings.ChestRange);

        /// <summary>The loose-item pickup's turn each frame, after the feeding (Pickup's own file). Left out: nothing.</summary>
        static partial void TickLooseItems();

        /// <summary>The loose-item pickup's undo step after an error (Pickup's own file). Left out: nothing.</summary>
        static partial void ForgetLooseItems();

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            ChestHooks.Resolve(missing, Chests);
            GameHooks.Resolve(missing, missingOptional);
            Rivals.Resolve();
            Loose.Pickup.Resolve(missingOptional);   // the game's members the loose-item pickup and the log fix use
            Winds.Resolve(missingOptional);   // the windmill's cover, for the catch-up's wind
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
            QoLMods.ListPicker.Lists.Resolve(missingOptional);
            TellTheServerPack();
        }

        /// <summary>
        /// What Topper-Upper tells miikeskii's Server-Side QoL Pack on the server (card A91, A93; ServerPack's hello), each
        /// "1" or "0", read at each hello: "topperupper.on" (the mod is on here), "topperupper.stations" (and feeds the
        /// stations this game looks after), "topperupper.loose" (and picks up the loose items this game looks after, its
        /// pickup not lost to an error), "topperupper.catchup" (this version catches up and follows the server pack's
        /// catch-up settings: on, and "Follow the server pack's settings" on). The server pack may then hand this game
        /// items and stations of friends without the pack (it makes this game their owner); Topper-Upper stores and feeds
        /// them as anything else this game owns: the pickup takes only what this game owns, a few seconds after it was made
        /// (by its record's spawn time, long past for such an item), lying still and away from players; the loop feeds any
        /// station this game owns; and a station handed over this way was run by the friend's game, so it doesn't catch up.
        /// </summary>
        private static void TellTheServerPack()
        {
            ServerPack.Say("topperupper.on", () => CatchUpChoice.Flag(IsOn));
            // Not while another feeding mod runs (Rivals: then Topper-Upper feeds nothing). Not per kind: a friend's station of
            // a kind switched off here is still handed over, and goes unfed (said in the README).
            ServerPack.Say("topperupper.stations", () => CatchUpChoice.Flag(IsOn && Settings.FeedStations.Value && !Rivals.Any));
            ServerPack.Say("topperupper.loose", () => CatchUpChoice.Flag(IsOn && Settings.PickUpLoose != null && Settings.PickUpLoose.Value && !Loose.Pickup.Lost));
            ServerPack.Say("topperupper.catchup", () => CatchUpChoice.Flag(IsOn && !CatchUps.Lost && Settings.FollowServerPack.Value));
        }

        /// <summary>Every frame while the module runs, also while switched off: the chest service's turn (the server pack's
        /// hello with it), the fold-outs, the hello, the time online (the catch-up's clock), the keys, linking, the
        /// fermenters' catch-up, the loop, the glow, and the loose-item pickup.</summary>
        internal override void Update()
        {
            ChestClaims.Tick();
            Folds.Tick();
            Rivals.AtFirstTurn();
            Hello.Tick();
            PlayTime.Tick();
            Marks.Update();
            Links.Update();
            CatchUps.Tick();
            Loop.Tick();
            Glow.Update();
            TickLooseItems();
        }

        /// <summary>After an error, each step on its own (MODULES.md rule 7): the loop's lines and wants forgotten, linking
        /// ended, every glow out, the hello's last word, the chest service's holds left to end by the clock, the catch-up's
        /// fermenters forgotten, and the loose-item pickup's own.</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Loop.Forget,
            Links.Cancel,
            Glow.PutOutAll,
            Hello.Forget,
            Marks.Forget,
            ChestClaims.ReleaseHolds,
            CatchUps.Forget,
            UndoLooseItems,
        };

        // An Action can't point at a partial method that may not exist; this one always does.
        private static void UndoLooseItems() => ForgetLooseItems();
    }
}
