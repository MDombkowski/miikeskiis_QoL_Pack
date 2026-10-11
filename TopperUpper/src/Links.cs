using System;
using System.Collections.Generic;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Chests linked to a station (card A77; the research's 4.1): Left Alt + J pointing at a station starts linking it;
    /// then E on a chest makes it an input (the station takes only from its inputs), Left Shift + E an output (what it
    /// makes goes there), and the same again unlinks it; the key again at the station (or pointing at nothing), opening
    /// a menu or the inventory (Escape does), walking 30 m away or a minute without a change ends it. While linking, the
    /// game's own E doesn't open a chest (Interact). The links live on the station, as one line of text naming each chest
    /// by its prefab and its place (LinkText), never its network ID, which the game renumbers at every load; a chest found
    /// within 5 cm of the place is the one. Only a chest of the game's own kind that a player built, standing still (never
    /// a cart's or a ship's), may be linked.
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
                foreach (Link link in links)
                {
                    // The place first (cheap), then the prefab.
                    if (Math.Abs(at.x * 100.0 - link.X) > LinkText.Near || Math.Abs(at.y * 100.0 - link.Y) > LinkText.Near ||
                        Math.Abs(at.z * 100.0 - link.Z) > LinkText.Near || !link.Is(Prefab(chest), at.x, at.y, at.z))
                    {
                        continue;
                    }
                    if (WhyNotLinkable(chest) == null)
                    {
                        List<Container> into = link.Output ? outputs : inputs;
                        if (!into.Contains(chest))
                        {
                            into.Add(chest);
                        }
                    }
                    break;
                }
            }
            Vector3 station = fed.Position;
            inputs.Sort((a, b) => (a.transform.position - station).sqrMagnitude.CompareTo((b.transform.position - station).sqrMagnitude));
            outputs.Sort((a, b) => (a.transform.position - station).sqrMagnitude.CompareTo((b.transform.position - station).sqrMagnitude));
        }

        /// <summary>The station (in the registry) and role a chest is linked to, for its hover line; null role when none.</summary>
        internal static List<KeyValuePair<Fed, bool>> LinkedTo(Container chest)
        {
            var found = new List<KeyValuePair<Fed, bool>>();
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
                Link link = LinkText.Find(links, prefab, at.x, at.y, at.z);
                if (link != null)
                {
                    found.Add(new KeyValuePair<Fed, bool>(fed, link.Output));
                }
            }
            return found;
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
                    End(null);
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
                    return;   // at a chest, the key is StoreStash's chest setup: linking goes on
                }
                if (station)
                {
                    Start(pointed);
                }
                return;
            }
            if (current == null)
            {
                return;
            }
            // E links an input, the game's own "alt" with it (Left Shift, or the gamepad's) an output; never a press with Alt
            // or Ctrl held, which is another key's (Reserve Stock's Left Alt + E at a chest: code review m3).
            LinkPress press = LinkPresses.Of(Keys.UseDown(), Keys.AltHeld(), Keys.CtrlHeld(), Keys.GameAlt());
            if (press == LinkPress.None)
            {
                return;
            }
            Container chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            if (chest == null)
            {
                return;
            }
            bool output = press == LinkPress.Output;
            string why = WhyNotLinkable(chest);
            if (why != null)
            {
                Marks.Show($"Can't link it: the chest is {why}.");
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
                Marks.Show($"The {Names.Of(current)} has as many {(output ? "outputs" : "inputs")} as it can keep ({LinkText.MostEach}).");
                return;
            }
            string how = Marks.SetLinks(current, LinkText.Write(links));
            changedAt = now;
            string role = output ? "an output" : "an input";
            string said = change == LinkChange.Unlinked ? $"Unlinked from the {Names.Of(current)}" :
                          change == LinkChange.Switched ? $"Now {role} of the {Names.Of(current)}" : $"Linked: {role} of the {Names.Of(current)}";
            Marks.Show($"{said}{how}. {Summary(current)}");
            current.Due = 0f;
        }

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
            string done = Keys.Text(Settings.LinkKey.Value);
            Marks.Show($"Linking the {Names.Of(fed)}'s chests. E on a chest: it takes from that chest. Left Shift + E: it puts what it " +
                       $"makes there. The same again unlinks it. {done} here, or away from chests, when you're done.");
        }

        private static void End(string said)
        {
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
