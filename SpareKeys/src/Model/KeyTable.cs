using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>"A plain key counts as used under any modifier": whether a key, or a combination, still counts when more
    /// modifiers are held than it names.</summary>
    internal enum PlainRule
    {
        /// <summary>Always (safe): the game's buttons, and many mods, fire whatever else is held.</summary>
        Always,

        /// <summary>Only the game's buttons, KeyCode settings, console binds and the pack's own keys (all read loosely);
        /// other mods' shortcuts follow BepInEx's own check, which refuses any extra key.</summary>
        GameAndLooseReadersOnly,

        /// <summary>Never: every key counts only with exactly its own modifiers held.</summary>
        Never,
    }

    /// <summary>"Look inside mods' code".</summary>
    internal enum CodeRule
    {
        Off,
        MaybeUsed,
        CountAsUsed,
    }

    /// <summary>"Debug-mode keys".</summary>
    internal enum DebugRule
    {
        Never,
        OnlyWhileDebugModeIsOn,
        Always,
    }

    /// <summary>"Keys shared by places never true at once": a key two things use whose whens never meet (Left Alt + E at a
    /// chest and at an armour stand).</summary>
    internal enum SharedRule
    {
        /// <summary>Its own colour: shared, not a clash (B04).</summary>
        ShownAsShared,

        /// <summary>Simply used, as 0.1.0 showed a key whose places never met.</summary>
        ShownAsUsed,

        /// <summary>A clash, as if the whens weren't known.</summary>
        CountedAsClashes,
    }

    /// <summary>"A key whose when isn't known counts as".</summary>
    internal enum UnknownRule
    {
        /// <summary>Working anywhere: a clash with anything else on its key.</summary>
        Anywhere,

        /// <summary>Maybe a clash (amber), unless its key clashes anyway.</summary>
        Maybe,
    }

    /// <summary>A key's state with some modifiers held.</summary>
    internal enum Status
    {
        /// <summary>Nothing uses it, with these modifiers or any others.</summary>
        Free,

        /// <summary>Free with these modifiers, but used with others (J alone is free; Left Alt + J is StoreStash's).</summary>
        WithModifier,

        /// <summary>One user (or several of one owner that never meet).</summary>
        Used,

        /// <summary>Two or more users whose whens never meet (at a chest and at an armour stand): shared, no clash.</summary>
        Shared,

        /// <summary>Two or more users that fire at once.</summary>
        Clash,

        /// <summary>Two or more users that would fire at once if one whose when isn't known works there ("A key whose when
        /// isn't known counts as: Maybe").</summary>
        MaybeClash,

        /// <summary>Only a mod's code reads it, as far as can be seen.</summary>
        Maybe,

        /// <summary>The system's (Windows keys, Print Screen, Caps Lock, Alt+Tab …): never offered.</summary>
        NotOffered,

        /// <summary>One of the modifiers held right now.</summary>
        Held,
    }

    /// <summary>What counts, from the settings: plain values, so the harness sets them as it likes.</summary>
    internal sealed class Options
    {
        internal PlainRule Plain = PlainRule.Always;
        internal CodeRule Code = CodeRule.MaybeUsed;
        internal bool MenusTypingWindows;
        internal DebugRule Debug = DebugRule.OnlyWhileDebugModeIsOn;
        internal bool DebugModeOn;
        internal bool ConsoleBinds = true;
        internal bool SwitchedOffModules;
        internal bool Gamepad;
        internal SharedRule Shared = SharedRule.ShownAsShared;
        internal UnknownRule Unknown = UnknownRule.Anywhere;

        /// <summary>Whether a use counts at all under these settings.</summary>
        internal bool Counts(Use use)
        {
            if (use.Kind == UseKind.Code && Code == CodeRule.Off)
            {
                return false;
            }
            if (use.Place.OnlyInMenusTypingOrWindow && !MenusTypingWindows)
            {
                return false;
            }
            if (use.DebugMode && (Debug == DebugRule.Never || (Debug == DebugRule.OnlyWhileDebugModeIsOn && !DebugModeOn)))
            {
                return false;
            }
            if (use.Kind == UseKind.ConsoleBind && !ConsoleBinds)
            {
                return false;
            }
            if (use.SwitchedOff && !SwitchedOffModules)
            {
                return false;
            }
            return Gamepad || !use.IsGamepad;
        }

        /// <summary>A use that may be in use, shown amber rather than used: a key found only in a mod's code, a mod's
        /// button with no setting behind it, a text setting that reads as a key. Only keys found in code follow "Look inside
        /// mods' code: CountAsUsed"; the others stay amber whatever it says.</summary>
        internal bool IsMaybe(Use use) => use.Maybe && (use.Kind != UseKind.Code || Code != CodeRule.CountAsUsed);
    }

    /// <summary>A key with some modifiers held, and everything that uses it.</summary>
    internal sealed class KeyState
    {
        internal int Key;
        internal int[] Layer;
        internal Status Status;

        /// <summary>Why it isn't offered (NotOffered).</summary>
        internal string Reason;

        /// <summary>The users that fire with exactly these modifiers held, or carried from fewer (a plain key under Alt).</summary>
        internal readonly List<Use> Fires = new List<Use>();

        /// <summary>Users that fire here, found only in a mod's code.</summary>
        internal readonly List<Use> Maybe = new List<Use>();

        /// <summary>Users that need other modifiers than these (Left Alt + J, seen with nothing held).</summary>
        internal readonly List<Use> Elsewhere = new List<Use>();

        /// <summary>Whether a user fires here only because its plain key (or a smaller combination) is carried under the
        /// modifiers held.</summary>
        internal bool IsCarried(Use use) => !Combo.Same(use.Mods, Layer);

        /// <summary>For a clash, two users that fire at once (for a maybe clash, two that may; with "Keys shared by places
        /// never true at once" counted as clashes, two that never meet); for a key judged for one setting being picked,
        /// that setting and one that fires with it. Null otherwise.</summary>
        internal Use[] Pair;

        /// <summary>A carried user that gives way here to a more specific key of its own owner (StoreStash's Left Alt + H
        /// wins over its own H: it reads the one with the most modifiers).</summary>
        internal bool IsShadowed(Use use) =>
            IsCarried(use) && Fires.Any(other => other.Mods.Length > use.Mods.Length && GivesWay(use, other));

        /// <summary>
        /// Whether <paramref name="carried"/>, a key carried under more modifiers than its own, gives way to
        /// <paramref name="specific"/>, a more specific key of the same owner: where the owner picks one of a group of its
        /// keys per press, the one with the most modifiers, and both are in that group (StoreStash's keys; Outfitter's
        /// stand keys: their code, 58-B), or where the carried key is read strictly (BepInEx's own check refuses the extra
        /// modifier). Anywhere else both fire. The game's own keys (all read loosely, in no group) never give way: they never
        /// clash with each other when one is carried (its only combinations of its own are debug mode's, Judge), but hide
        /// from no one else.
        /// </summary>
        internal static bool GivesWay(Use carried, Use specific)
        {
            if (carried.OwnerId != specific.OwnerId)
            {
                return false;
            }
            if (!carried.Loose)
            {
                return true;
            }
            return OnePerPress.TryGetValue(carried.OwnerId, out HashSet<string> group) && group.Contains(carried.Detail) && group.Contains(specific.Detail);
        }

        // The keys each owner picks one of per press, the one with the most modifiers (StoreStash's Keys.Pressed; Outfitter's
        // StandInput.Pressed, its stand keys only: its window key is read on its own).
        private static readonly Dictionary<string, HashSet<string>> OnePerPress = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["pack:StoreStash"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "QuickStackKey", "PersonalKey", "MineKey", "OneStackKey", "SortKey", "SetupKey", "SetupCopyKey", "SetupPasteKey", "OverflowKey",
            },
            ["pack:Outfitter"] = new HashSet<string>(StringComparer.Ordinal) { "EquipKey", "SaveKey", "PersonalKey", "PoseKey" },
        };

        /// <summary>The modifiers its other users need, as the corner mark on its picture (Ctrl, Alt, Shift: "C", "A",
        /// "S"), in that order.</summary>
        internal string Corner
        {
            get
            {
                bool ctrl = false, alt = false, shift = false;
                foreach (Use use in Elsewhere)
                {
                    foreach (int mod in use.Mods)
                    {
                        KeyCode code = (KeyCode)mod;
                        ctrl |= code == KeyCode.LeftControl || code == KeyCode.RightControl;
                        alt |= KeyCatalog.IsAlt(mod);
                        shift |= code == KeyCode.LeftShift || code == KeyCode.RightShift;
                    }
                }
                return (ctrl ? "C" : string.Empty) + (alt ? "A" : string.Empty) + (shift ? "S" : string.Empty);
            }
        }
    }

    /// <summary>
    /// The key map: every use that counts under the settings, and each key's state with any modifiers held. The rules,
    /// in one place:
    /// - a user fires with exactly its own modifiers held, and with more held when the setting "A plain key counts as
    ///   used under any modifier" carries it (Always: everyone; GameAndLooseReadersOnly: the loose readers);
    /// - a user that only holds the key with something else (Shift-click) uses it too: an action put on that key would
    ///   fire each time it is held;
    /// - two users clash when both their whens can be true at the same moment (Place.Overlap: an anywhere key meets
    ///   everything, a key of the world meets one that needs a tool in hand or a thing pointed at, a tool in hand and a
    ///   thing pointed at hold together, a chest and an armour stand never do), whoever they belong to (his words: "two
    ///   mods (or the same mod)"), unless the key is Shift, Ctrl or Alt (shared on purpose), one is the game's Use carried
    ///   under a pack key made to take it, one is carried under a more specific key of its own owner that it gives way to
    ///   (KeyState.GivesWay: StoreStash's keys, Outfitter's stand keys, a key read strictly), or both are the game's and
    ///   one is carried (its debug combinations);
    /// - two users of different owners whose whens never meet share the key: shown as shared, not a clash (B04);
    /// - a key whose only users are found in mods' code is "maybe used".
    /// Plain code over plain data: the harness checks every rule.
    /// </summary>
    internal sealed class KeyTable
    {
        private readonly Dictionary<int, List<Use>> byMain = new Dictionary<int, List<Use>>();

        internal KeyTable(IEnumerable<Use> uses, Options options)
        {
            Options = options;
            Uses = uses.Where(use => use != null && !use.Combo.IsNone && options.Counts(use)).ToList();
            foreach (Use use in Uses)
            {
                if (!byMain.TryGetValue(use.Main, out List<Use> list))
                {
                    byMain[use.Main] = list = new List<Use>();
                }
                list.Add(use);
            }
        }

        internal Options Options { get; }

        /// <summary>Every use that counts, in the order they were given.</summary>
        internal List<Use> Uses { get; }

        /// <summary>The uses of one key (as the main key), at any layer.</summary>
        internal IReadOnlyList<Use> UsesOf(int key) => byMain.TryGetValue(key, out List<Use> list) ? (IReadOnlyList<Use>)list : Array.Empty<Use>();

        /// <summary>Whether a user still fires with more modifiers held than its own.</summary>
        internal bool Carries(Use use)
        {
            switch (Options.Plain)
            {
                case PlainRule.Always:
                    return true;
                case PlainRule.GameAndLooseReadersOnly:
                    return use.Loose;
                default:
                    return false;
            }
        }

        /// <summary>A key's state with the modifiers of <paramref name="layer"/> held (a sorted id set; empty for none).</summary>
        internal KeyState StateOf(int key, int[] layer)
        {
            layer = layer ?? Combo.NoMods;
            var state = new KeyState { Key = key, Layer = layer };
            if (Array.IndexOf(layer, key) >= 0)
            {
                state.Status = Status.Held;
                Collect(state);
                return state;
            }
            state.Reason = KeyCatalog.NotOfferedWith(key, layer);
            Collect(state);
            if (state.Reason != null)
            {
                state.Status = Status.NotOffered;
                return state;
            }
            if (state.Fires.Count == 0)
            {
                state.Status = state.Maybe.Count > 0 ? Status.Maybe : state.Elsewhere.Count > 0 ? Status.WithModifier : Status.Free;
                return state;
            }
            state.Status = Judge(state);
            return state;
        }

        /// <summary>
        /// A key's state for one setting while a key is being picked for it (the panel beside F1 and the Controls menu):
        /// the setting itself left out, a key some other user fires at the same time as the setting's when is taken (Used),
        /// one whose users never meet that when is Shared (free for it), and the rest as the map has them.
        /// <paramref name="self"/>: the setting, as a use (its combination needn't be set); null for the plain map.
        /// </summary>
        internal KeyState StateFor(int key, int[] layer, Use self)
        {
            KeyState whole = StateOf(key, layer);
            if (self == null || whole.Status == Status.NotOffered || whole.Status == Status.Held)
            {
                return whole;
            }
            var state = new KeyState { Key = whole.Key, Layer = whole.Layer, Reason = whole.Reason };
            state.Fires.AddRange(whole.Fires.Where(use => !IsSelf(use, self)));
            state.Maybe.AddRange(whole.Maybe.Where(use => !IsSelf(use, self)));
            state.Elsewhere.AddRange(whole.Elsewhere.Where(use => !IsSelf(use, self)));
            List<Use> fires = state.Fires.Where(use => !state.IsShadowed(use)).ToList();
            if (fires.Count == 0)
            {
                state.Status = state.Maybe.Count > 0 ? Status.Maybe : state.Elsewhere.Count > 0 ? Status.WithModifier : Status.Free;
                return state;
            }
            if (KeyCatalog.IsModifier(state.Key))
            {
                state.Status = Status.Used;
                return state;
            }
            bool maybe = false;
            bool apart = false;
            foreach (Use use in fires)
            {
                // The game's Use this setting takes over, or its own key that gives way to it: neither fires with it.
                if (GivesWayToSelf(state, use, self))
                {
                    continue;
                }
                if (!Place.Overlap(use.Place, self.Place))
                {
                    apart = true;
                    continue;
                }
                if (Options.Unknown == UnknownRule.Maybe && (!use.Known || !self.Known) && !(use.Known && use.Place.Everywhere) && !(self.Known && self.Place.Everywhere))
                {
                    maybe = true;
                    state.Pair = state.Pair ?? new[] { self, use };
                    continue;
                }
                state.Pair = new[] { self, use };
                state.Status = Status.Used;
                return state;
            }
            if (!maybe && !apart)
            {
                state.Status = state.Maybe.Count > 0 ? Status.Maybe : state.Elsewhere.Count > 0 ? Status.WithModifier : Status.Free;
                return state;
            }
            state.Status = maybe ? Status.MaybeClash : Options.Shared == SharedRule.ShownAsShared ? Status.Shared : Status.Used;
            return state;
        }

        /// <summary>Whether a use that fires here never fires together with the setting being picked: the game's Use that
        /// setting takes over at its things, its own key that gives way to it, or the game's own carried key beside a game
        /// button being rebound.</summary>
        internal static bool GivesWayToSelf(KeyState state, Use use, Use self) =>
            self != null && state.IsCarried(use) &&
            ((IsGamesUse(use) && self.OverGame) || KeyState.GivesWay(use, self) || (use.IsGame && self.IsGame));

        /// <summary>Whether a use is the setting being picked for (its owner, its section and its key in the file, or the
        /// game's button).</summary>
        internal static bool IsSelf(Use use, Use self) =>
            self != null && use.OwnerId == self.OwnerId && use.Detail == self.Detail && use.Section == self.Section && use.Kind == self.Kind;

        private void Collect(KeyState state)
        {
            foreach (Use use in UsesOf(state.Key))
            {
                if (Combo.Same(use.Mods, state.Layer) || (Combo.Within(use.Mods, state.Layer) && Carries(use)))
                {
                    (Options.IsMaybe(use) ? state.Maybe : state.Fires).Add(use);
                }
                else if (!Options.IsMaybe(use))
                {
                    state.Elsewhere.Add(use);
                }
            }
        }

        // Used, Shared, Clash or MaybeClash, for a key something fires at (the rules above).
        private Status Judge(KeyState state)
        {
            if (KeyCatalog.IsModifier(state.Key))
            {
                return Status.Used;   // Shift, Ctrl and Alt are held with other things on purpose: shared, never a clash
            }
            // A user carried under the modifiers held gives way to a more specific key of its own owner that fires here
            // (StoreStash's Left Alt + H wins over its own H: the one with the most modifiers).
            List<Use> fires = state.Fires.Where(use => !state.IsShadowed(use)).ToList();
            Use[] apart = null;
            Use[] maybe = null;
            for (int i = 0; i < fires.Count; i++)
            {
                for (int j = i + 1; j < fires.Count; j++)
                {
                    Use a = fires[i];
                    Use b = fires[j];
                    bool sameOwner = a.OwnerId == b.OwnerId;
                    if (OverGame(state, a, b) || OverGame(state, b, a) ||
                        (sameOwner && a.IsGame && (state.IsCarried(a) || state.IsCarried(b))))
                    {
                        continue;
                    }
                    if (!Place.Overlap(a.Place, b.Place))
                    {
                        if (!sameOwner)
                        {
                            apart = apart ?? new[] { a, b };   // two owners whose whens never meet: shared
                        }
                        continue;
                    }
                    if (Options.Unknown == UnknownRule.Maybe && (!a.Known || !b.Known) && !(a.Known && a.Place.Everywhere) && !(b.Known && b.Place.Everywhere))
                    {
                        maybe = maybe ?? new[] { a, b };
                        continue;
                    }
                    state.Pair = new[] { a, b };
                    return Status.Clash;
                }
            }
            if (apart != null && Options.Shared == SharedRule.CountedAsClashes)
            {
                state.Pair = apart;
                return Status.Clash;
            }
            if (maybe != null)
            {
                state.Pair = maybe;
                return Status.MaybeClash;
            }
            return apart != null && Options.Shared == SharedRule.ShownAsShared ? Status.Shared : Status.Used;
        }

        // The game's key, carried under the modifiers of a pack key made to take it there whenever it fires (Reserve
        // Stock's Left Alt + E at a chest over the game's E).
        private static bool OverGame(KeyState state, Use game, Use pack) =>
            IsGamesUse(game) && state.IsCarried(game) && pack.OverGame;

        /// <summary>The game's Use (E), the one key of the game's a pack key takes over on purpose at its own things.</summary>
        internal static bool IsGamesUse(Use use) => use.IsGame && use.Kind == UseKind.GameButton && use.Detail == GameKeys.UseButton;

        /// <summary>Every modifier set some user names exactly, and none: the layers a clash can be at.</summary>
        internal List<int[]> Layers()
        {
            var layers = new List<int[]> { Combo.NoMods };
            foreach (Use use in Uses)
            {
                if (!layers.Any(layer => Combo.Same(layer, use.Mods)))
                {
                    layers.Add(use.Mods);
                }
            }
            return layers;
        }

        /// <summary>Every clash (and maybe clash), at every layer: each key with each set of modifiers some user names, in
        /// keyboard order (fewer modifiers first).</summary>
        internal List<KeyState> Clashes() => Every(status => status == Status.Clash || status == Status.MaybeClash);

        /// <summary>Every shared key (users whose whens never meet), at every layer, in the same order.</summary>
        internal List<KeyState> SharedKeys() => Every(status => status == Status.Shared);

        private List<KeyState> Every(Func<Status, bool> wanted)
        {
            var found = new List<KeyState>();
            List<int[]> layers = Layers().OrderBy(layer => layer.Length).ToList();
            foreach (int key in byMain.Keys.OrderBy(Order))
            {
                foreach (int[] layer in layers)
                {
                    KeyState state = StateOf(key, layer);
                    if (wanted(state.Status) && !SameCarried(found, state))
                    {
                        found.Add(state);
                    }
                }
            }
            return found;
        }

        // A clash seen with more modifiers held only because the same users clash with fewer (F8 under Left Alt, when F8
        // itself clashes) is the same clash: listed once, at the fewest modifiers. The same for a shared key.
        private static bool SameCarried(List<KeyState> found, KeyState state) =>
            found.Any(earlier => earlier.Key == state.Key && earlier.Status == state.Status && Combo.Within(earlier.Layer, state.Layer) &&
                                 state.Fires.All(use => earlier.Fires.Contains(use)));

        /// <summary>A key's place in keyboard order (unknown keys last).</summary>
        internal static int Order(int key) => KeyCatalog.Get(key)?.Order ?? 100000 + key;

        /// <summary>
        /// Free keys to try instead of one in use, at most <paramref name="count"/>: first the same key with one modifier
        /// (Left Ctrl, Left Alt, Left Shift) where that combination is free, then the free keys nearest it on the picture,
        /// with the same modifiers held. Never a modifier or a key the system uses.
        /// </summary>
        internal List<Combo> Suggest(int key, int[] layer, Layout layout, int count = 3)
        {
            var found = new List<Combo>();
            layer = layer ?? Combo.NoMods;
            if (!KeyCatalog.IsModifier(key) && KeyCatalog.Get(key)?.IsGamepad != true)
            {
                foreach (KeyCode mod in new[] { KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftShift })
                {
                    if (found.Count >= count || Array.IndexOf(layer, (int)mod) >= 0)
                    {
                        continue;
                    }
                    int[] with = Combo.Sorted(layer.Concat(new[] { (int)mod }));
                    if (IsFree(StateOf(key, with).Status))
                    {
                        found.Add(new Combo(key, with));
                    }
                }
            }
            if (layout == null || !layout.TryGet(key, out KeySpot from))
            {
                return found;
            }
            var near = new List<KeyValuePair<float, int>>();
            foreach (KeySpot spot in layout.Keys)
            {
                if (spot.Id == key || KeyCatalog.IsModifier(spot.Id) || !IsFree(StateOf(spot.Id, layer).Status))
                {
                    continue;
                }
                float dx = spot.MiddleX - from.MiddleX;
                float dy = (spot.MiddleY - from.MiddleY) * 1.2f;   // a row away is a little farther than a key across
                near.Add(new KeyValuePair<float, int>(dx * dx + dy * dy, spot.Id));
            }
            foreach (KeyValuePair<float, int> each in near.OrderBy(pair => pair.Key).ThenBy(pair => Order(pair.Value)))
            {
                if (found.Count >= count)
                {
                    break;
                }
                found.Add(new Combo(each.Value, layer));
            }
            return found;
        }

        /// <summary>Free to take with these modifiers: nothing fires there (used with others only is still free).</summary>
        internal static bool IsFree(Status status) => status == Status.Free || status == Status.WithModifier;

        /// <summary>For a key judged for one setting (StateFor): free for it, since nothing that fires there meets its
        /// when (Shared, while shared keys are shown as shared), or nothing fires there at all.</summary>
        internal static bool IsFreeFor(Status status) => IsFree(status) || status == Status.Shared;
    }
}
