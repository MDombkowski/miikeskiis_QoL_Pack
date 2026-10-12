using System;
using System.Collections.Generic;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Chests linked to a station (card A77; the research's 4.1): Left Alt + J pointing at a station starts linking it;
    /// then E on a chest makes it an input (the station takes only from its inputs) or no longer one, Left Shift + E the
    /// same for an output (what it makes goes there), each role on its own, so a chest may be both (card B11); the key
    /// again at the station (or pointing at nothing), opening a menu or the inventory (Escape does), walking 30 m away or
    /// a minute without a change ends it. While linking, the game's own E doesn't open a chest (Interact), and a chest's
    /// hover text says what E and Left Shift + E will do to it (Hover). Every press while linking, and why linking ended,
    /// is written to the log, so a test that shows nothing says why (card B11). The links live on the station, as one line
    /// of text naming each chest by its prefab and its place (LinkText), never its network ID, which the game renumbers at
    /// every load; a chest found within 5 cm of the place is the one. Only a chest of the game's own kind that a player
    /// built, standing still (never a cart's or a ship's), may be linked.
    /// </summary>
    internal static class Links
    {
        /// <summary>The links' name in a station's record. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.topperupper.links";

        /// <summary>Linking ends after this long without a change, in seconds.</summary>
        internal const float EndAfter = 60f;

        /// <summary>Linking ends once he is this far from the station, in metres.</summary>
        internal const float EndBeyond = 30f;

        private static Fed linking;
        private static float changedAt;

        private static readonly Dictionary<string, List<Link>> Parsed = new Dictionary<string, List<Link>>(StringComparer.Ordinal);
        private static readonly Dictionary<Container, string> PrefabOf = new Dictionary<Container, string>();

        /// <summary>The station being linked, or null.</summary>
        internal static Fed Linking => linking != null && linking.Alive ? linking : null;

        /// <summary>A station's links, read (cached by text).</summary>
        internal static List<Link> Of(Fed fed)
        {
            string text = fed.LinkText;
            if (string.IsNullOrEmpty(text))
            {
                return Empty;
            }
            if (!Parsed.TryGetValue(text, out List<Link> links))
            {
                if (Parsed.Count > 256)
                {
                    Parsed.Clear();
                }
                links = LinkText.Parse(text);
                Parsed[text] = links;
            }
            return links;
        }

        private static readonly List<Link> Empty = new List<Link>();

        /// <summary>
        /// The chests a station's links name, loaded on this computer and still linkable, by role, nearest the station first.
        /// A chest of the link's prefab within 5 cm of its place is the one; one moved by a building mod is lost.
        /// </summary>
        internal static void Resolve(Fed fed, out List<Container> inputs, out List<Container> outputs)
        {
            inputs = new List<Container>();
            outputs = new List<Container>();
            List<Link> links = Of(fed);
            if (links.Count == 0)
            {
                return;
            }
            foreach (Container chest in ChestFinder.Loaded())
            {
                if (chest == null)
                {
                    continue;
                }
                Vector3 at = chest.transform.position;
                bool? linkable = null;
                foreach (Link link in links)
                {
                    // The place first (cheap), then the prefab. Every link is looked at: a chest may be both (card B11).
                    if (Math.Abs(at.x * 100.0 - link.X) > LinkText.Near || Math.Abs(at.y * 100.0 - link.Y) > LinkText.Near ||
                        Math.Abs(at.z * 100.0 - link.Z) > LinkText.Near || !link.Is(Prefab(chest), at.x, at.y, at.z))
                    {
                        continue;
                    }
                    linkable = linkable ?? WhyNotLinkable(chest) == null;
                    if (linkable == true)
                    {
                        List<Container> into = link.Output ? outputs : inputs;
                        if (!into.Contains(chest))
                        {
                            into.Add(chest);
                        }
                    }
                }
            }
            Vector3 station = fed.Position;
            inputs.Sort((a, b) => (a.transform.position - station).sqrMagnitude.CompareTo((b.transform.position - station).sqrMagnitude));
            outputs.Sort((a, b) => (a.transform.position - station).sqrMagnitude.CompareTo((b.transform.position - station).sqrMagnitude));
        }

        /// <summary>The stations (in the registry) a chest is linked to, each with what the chest is to it ("its input",
        /// "its output", or both), for its hover lines.</summary>
        internal static List<KeyValuePair<Fed, string>> LinkedTo(Container chest)
        {
            var found = new List<KeyValuePair<Fed, string>>();
            Vector3 at = chest.transform.position;
            string prefab = null;
            foreach (Fed fed in Registry.All)
            {
                if (fed.IsLight || !fed.Alive)
                {
                    continue;
                }
                List<Link> links = Of(fed);
                if (links.Count == 0)
                {
                    continue;
                }
                prefab = prefab ?? Prefab(chest);
                LinkText.RolesOf(links, prefab, at.x, at.y, at.z, out bool input, out bool output);
                string role = LinkWords.Role(input, output);
                if (role != null)
                {
                    found.Add(new KeyValuePair<Fed, string>(fed, role));
                }
            }
            return found;
        }

        /// <summary>What a chest is to a station: its roles among the station's links.</summary>
        internal static void RolesOf(Fed fed, Container chest, out bool input, out bool output)
        {
            Vector3 at = chest.transform.position;
            LinkText.RolesOf(Of(fed), Prefab(chest), at.x, at.y, at.z, out input, out output);
        }

        /// <summary>Why a chest can't be linked, as words after "the chest is"; null if it can: the game's own kind of chest, a
        /// player built it, standing still (not on a cart or a ship), and the chest rules let him use it.</summary>
        internal static string WhyNotLinkable(Container chest)
        {
            if (chest == null || chest.GetType() != typeof(Container))
            {
                return "another mod's kind of container";
            }
            if (Obliterator.Is(chest))
            {
                return "the Obliterator (what goes in is destroyed)";
            }
            if (chest.GetComponentInParent<Ship>() != null || chest.GetComponentInParent<Vagon>() != null)
            {
                return "on a cart or a ship (it moves)";
            }
            ZNetView view = ChestHooks.NetView(chest);
            if (view == null || view.gameObject != chest.gameObject)
            {
                return "part of something that moves";
            }
            string why = ChestFinder.WhyNot(chest, new ChestRules { OnlyBuiltByMe = Settings.Chests.Value == WhichChests.OnlyChestsIBuilt },
                                            ChestClaims.PlayerId());
            return why == ChestFinder.WaitedOnWhy ? null : why;
        }

        /// <summary>Every frame: the link key, the E presses on chests while linking, and the ways linking ends.</summary>
        internal static void Update()
        {
            if (!Module.IsOn)
            {
                Cancel();
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Cancel();
                return;
            }
            float now = Time.time;
            Fed current = Linking;
            if (linking != null)
            {
                if (current == null)
                {
                    End("Linking ended: the station is gone.");
                    return;
                }
                if ((player.transform.position - current.Position).sqrMagnitude > EndBeyond * EndBeyond)
                {
                    End("Linking ended: you walked away.");
                    return;
                }
                if (now - changedAt > EndAfter)
                {
                    End("Linking ended: a minute without a change.");
                    return;
                }
                if (Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen())
                {
                    End(null, "a menu, the inventory or the map opened");
                    return;
                }
            }
            if (!Keys.TakesKeys())
            {
                return;
            }
            GameObject hover = player.GetHoverObject();
            if (Keys.Pressed(Settings.LinkKey.Value))
            {
                Fed pointed = Registry.Pointed(hover);
                bool station = pointed != null && !pointed.IsLight && Stations.Feedable(pointed);
                if (current != null && (pointed == null || pointed == current))
                {
                    if (hover == null || hover.GetComponentInParent<Container>() == null)
                    {
                        End("Linking done.");
                    }
                    else
                    {
                        // At a chest, the key is StoreStash's chest setup: linking goes on.
                        Module.Log.LogInfo($"Linking the {Names.Of(current)}: the link key at a chest does nothing to its links " +
                                           "(E and Left Shift + E link it); linking goes on.");
                    }
                    return;
                }
                if (station)
                {
                    Start(pointed);
                }
                return;
            }
            if (current == null || !Keys.UseDown())
            {
                return;
            }
            Container chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            if (chest == null)
            {
                return;   // E on something else: the game's own use, as ever
            }
            // E links an input, the game's own "alt" with it (Left Shift, or the gamepad's) an output; never a press with Alt
            // or Ctrl held, which is another key's (Reserve Stock's Left Alt + E at a chest: code review m3).
            bool alt = Keys.AltHeld();
            LinkPress press = LinkPresses.Of(true, alt, Keys.CtrlHeld(), Keys.GameAlt());
            string where = $"{Prefab(chest)} at {Place(chest.transform.position)}";
            if (press == LinkPress.None)
            {
                Module.Log.LogInfo($"Linking the {Names.Of(current)}: E on {where} with {(alt ? "Alt" : "Ctrl")} held is another key's, " +
                                   "so it links nothing.");
                return;
            }
            bool output = press == LinkPress.Output;
            string why = WhyNotLinkable(chest);
            if (why != null)
            {
                Marks.Show($"Can't link it: the chest is {why}.");
                Module.Log.LogInfo($"Linking the {Names.Of(current)}: {where} can't be linked: it is {why}.");
                return;
            }
            Vector3 at = chest.transform.position;
            var links = new List<Link>(LinkText.Parse(current.LinkText, out bool readable));
            if (!readable)
            {
                links.Clear();   // another version's links: replaced by his new ones
            }
            LinkChange change = LinkText.Toggle(links, LinkText.At(output, Prefab(chest), at.x, at.y, at.z));
            if (change == LinkChange.Full)
            {
                Marks.Show($"The {Names.Of(current)} has as many {(output ? "outputs" : "inputs")}\nas it can keep ({LinkText.MostEach}).");
                Module.Log.LogInfo($"Linking the {Names.Of(current)}: {where} not linked as {(output ? "an output" : "an input")}: " +
                                   $"it has {LinkText.MostEach} already.");
                return;
            }
            string how = Marks.SetLinks(current, LinkText.Write(links));
            changedAt = now;
            LinkText.RolesOf(links, Prefab(chest), at.x, at.y, at.z, out bool isInput, out bool isOutput);
            string said = LinkWords.After(Names.Of(current), output, change, isInput, isOutput, Summary(current));
            if (how.Length > 0)
            {
                said += "\n" + how.Trim();
            }
            Marks.Show(said);
            Module.Log.LogInfo($"Linking the {Names.Of(current)}: {(output ? "Left Shift + E" : "E")} on {where}: " +
                               $"{(change == LinkChange.Unlinked ? "unlinked as" : "linked as")} {(output ? "an output" : "an input")}; " +
                               $"the chest is now {LinkWords.Role(isInput, isOutput) ?? "not linked"}{how}.");
            current.Due = 0f;
        }

        // A place as the log names it: "x 120.3, z -45.1".
        private static string Place(Vector3 at) => $"x {at.x:0.0}, z {at.z:0.0}";

        /// <summary>"Takes from 2 chests · Puts into 1", or "" with none linked.</summary>
        internal static string Summary(Fed fed)
        {
            List<Link> links = Of(fed);
            int ins = LinkText.CountOf(links, output: false);
            int outs = LinkText.CountOf(links, output: true);
            if (ins == 0 && outs == 0)
            {
                return string.Empty;
            }
            var parts = new List<string>();
            if (ins > 0)
            {
                parts.Add($"Takes from {ins} chest{(ins == 1 ? string.Empty : "s")}");
            }
            if (outs > 0)
            {
                parts.Add($"Puts into {outs}");
            }
            return string.Join(" · ", parts);
        }

        private static void Start(Fed fed)
        {
            linking = fed;
            changedAt = Time.time;
            // Short lines, one thing each (his test of 0.1.0: the one long line ran off the screen, card B11); the chest's
            // hover text says the same while linking.
            Marks.Show(LinkWords.Start(Names.Of(fed), Keys.UseName(), Keys.AltName(), Keys.Text(Settings.LinkKey.Value)));
            Module.Log.LogInfo($"Linking the {Names.Of(fed)}'s chests (at {Place(fed.Position)}): {Summary(fed).ToLowerInvariant()}" +
                               $"{(Of(fed).Count == 0 ? "none linked yet" : string.Empty)}.");
        }

        private static void End(string said, string why = null)
        {
            if (linking != null)
            {
                Module.Log.LogInfo(why != null ? $"Linking ended: {why}." : said);   // said: "Linking ended: ..." or "Linking done."
            }
            linking = null;
            if (said != null)
            {
                Marks.Show(said);
            }
        }

        /// <summary>The undo step, and switching off: linking ends at once.</summary>
        internal static void Cancel() => linking = null;

        /// <summary>A chest's prefab name, kept per chest.</summary>
        internal static string Prefab(Container chest)
        {
            if (!PrefabOf.TryGetValue(chest, out string prefab))
            {
                if (PrefabOf.Count > 2048)
                {
                    PrefabOf.Clear();
                }
                prefab = Utils.GetPrefabName(chest.gameObject);
                PrefabOf[chest] = prefab;
            }
            return prefab;
        }
    }
}
