using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper.Loose
{
    /// <summary>
    /// The game as the loose-item pickup's sweep sees it (Sweep's ILooseWorld). At a sweep's start it notes every item
    /// loaded on this computer (the game's own list of them, ItemDrop.s_instances, read by name; failing that, a physics
    /// search around him), the players, the tame animals and what they eat, and the outputs of the stations whose products
    /// Topper-Upper handles. It looks at an item cheap questions first (Qualify), and puts one frame's items of one kind
    /// away through Stow, into the few chests ChestPick chooses, each item no farther than the range from its chest.
    /// Nothing here asks another game for anything or claims a chest.
    /// </summary>
    internal sealed class GameWorld : ILooseWorld
    {
        /// <summary>With the game's own list of items missing, how far around him the physics search reaches.</summary>
        internal const float FallbackReach = 64f;

        private static FieldInfo instancesField;
        private static readonly Dictionary<ItemDrop.ItemData.ItemType, string> TypeNames = new Dictionary<ItemDrop.ItemData.ItemType, string>();

        private readonly List<ItemDrop> items = new List<ItemDrop>();
        private readonly List<Spot> players = new List<Spot>();
        private readonly List<Eater> eaters = new List<Eater>();
        private readonly HashSet<string> eaten = new HashSet<string>(StringComparer.Ordinal);
        // The products lying where their station's put-away looks (by instance id: a destroyed item's own equality may match
        // another destroyed one).
        private readonly HashSet<int> atOutputs = new HashSet<int>();
        private readonly PickupRules rules = new PickupRules();
        private readonly Collider[] touching = new Collider[16];
        private readonly int[] masks = new int[32];
        private readonly bool[] masksKnown = new bool[32];
        private List<Container> loadedChests;
        private readonly List<string> cornerLines = new List<string>();
        private int cornerUnits;

        /// <summary>Past this many chests a sweep's top-left lines become one.</summary>
        internal const int MostCornerLines = 3;
        private NeverList never = new NeverList();
        private string neverText;
        private Spot you;

        public double Now => Time.time;

        public Spot You => you;

        public int Begin()
        {
            Forget();
            Player me = Player.m_localPlayer;
            if (me == null)
            {
                return 0;
            }
            you = SpotOf(me.transform.position);
            rules.Wait = Settings.LooseWait.Value;
            rules.PlayerDistance = Settings.LoosePlayerDistance.Value;
            rules.Reach = Settings.ChestRange;
            rules.TakeDropped = Settings.LooseDropped.Value;
            rules.Fodder = Settings.LooseFodder.Value;
            rules.Eggs = Settings.LooseEggs.Value;
            ReadNever();
            NotePlayers();
            if (rules.Fodder)
            {
                NoteEaters();
            }
            NoteOutputs();
            List<ItemDrop> all = Instances();
            if (all != null)
            {
                items.AddRange(all);
            }
            else
            {
                items.AddRange(Lying.At(me.transform.position, Mathf.Min(rules.Reach, FallbackReach)));
            }
            return items.Count;
        }

        public Left Look(int index, Waits waits, out Found found)
        {
            found = null;
            ItemDrop drop = index >= 0 && index < items.Count ? items[index] : null;
            Left why = Check(drop, waits, out ZNetView view);
            if (why != Left.None)
            {
                return why;
            }
            ZDOID id = view.GetZDO().m_uid;
            found = new Found
            {
                Index = index,
                Name = drop.m_itemData.m_shared.m_name,
                At = SpotOf(drop.transform.position),
                Key = new ItemKey(id.UserID, id.ID),
            };
            return Left.None;
        }

        public PutOutcome Put(Job job, IList<Found> chunk, int chestCap)
        {
            var outcome = new PutOutcome();
            var drops = new List<ItemDrop>();
            var still = new List<Found>();
            var spots = new List<Spot>();
            var where = new Dictionary<ItemDrop.ItemData, Spot>(SameItem.Instance);
            NotePlayers();   // where everyone is now, not when the sweep began
            foreach (Found each in chunk)
            {
                ItemDrop drop = each.Index >= 0 && each.Index < items.Count ? items[each.Index] : null;
                // Each looked at again in the frame of the put: gone, taken by hand, handed over or no longer allowed
                // drops out of the job.
                if (Check(drop, null, out ZNetView _) != Left.None || drop.m_itemData.m_shared.m_name != job.Name || where.ContainsKey(drop.m_itemData))
                {
                    continue;
                }
                Spot at = SpotOf(drop.transform.position);
                drops.Add(drop);
                still.Add(each);
                spots.Add(at);
                where[drop.m_itemData] = at;
            }
            if (drops.Count == 0)
            {
                return outcome;
            }
            float range = Settings.LooseReach;
            bool onlyWhereHeld = Settings.LooseOnlyWhereHeld.Value;
            bool followSetups = Settings.LooseFollowSetups.Value;
            bool leaveOthersMarked = Settings.LooseLeaveOthersMarked.Value;
            ChestRules chestRules = Settings.Rules(range);
            ItemDrop.ItemData like = drops[0].m_itemData;
            long me = ChestClaims.PlayerId();
            // Only the kind's chests within reach of this frame's items are read (code review n7).
            var near = new List<Container>();
            var views = new List<ChestView>();
            foreach (Container chest in ChestsFor(job, like, range, chestRules, onlyWhereHeld, followSetups))
            {
                if (chest != null && ChestPick.InRangeOfAny(SpotOf(chest.transform.position), spots, range))
                {
                    near.Add(chest);
                    views.Add(ViewOf(chest, like, chestRules.UseReserved, followSetups, me));
                }
            }
            List<int> picks = ChestPick.Candidates(views, spots, Middle(spots), range, onlyWhereHeld, followSetups, leaveOthersMarked, chestCap);
            if (picks.Count > 0)
            {
                var chosen = new List<Container>(picks.Count);
                foreach (int pick in picks)
                {
                    chosen.Add(near[pick]);
                }
                StackRules stack = Stow.Rules(chestRules, onlyWhereHeld, followSetups, leaveOthersMarked);
                // No farther than the range from the item itself.
                stack.Filter = (chest, item) => item != null && chest != null && where.TryGetValue(item, out Spot at) &&
                                                SpotOf(chest.transform.position).Within(at, range);
                StowResult result = Stow.Items(drops, chosen, stack);
                foreach (KeyValuePair<Container, Dictionary<string, int>> into in result.Into)
                {
                    foreach (KeyValuePair<string, int> item in into.Value)
                    {
                        outcome.Moved.Add((into.Key, item.Key, item.Value));
                    }
                }
            }
            for (int i = 0; i < drops.Count; i++)
            {
                ItemDrop drop = drops[i];
                if (drop != null && GroundPut.Ours(drop, drop.GetComponent<ZNetView>()) && drop.m_itemData.m_stack > 0)
                {
                    outcome.Unmoved.Add(still[i]);
                }
            }
            return outcome;
        }

        public void Say(object chest, IList<KeyValuePair<string, int>> what)
        {
            LooseSay how = Settings.LooseSayPut.Value;
            Container box = chest as Container;
            if (how == LooseSay.Off || box == null)
            {
                return;
            }
            string text = SayText.Of(what, Names.Item);
            Vector3 at = box.transform.position + Vector3.up * 1.2f;
            if (how == LooseSay.OverTheChest && PutText.OverTheChest(at, text))
            {
                return;
            }
            float away = Player.m_localPlayer != null ? Vector3.Distance(Player.m_localPlayer.transform.position, box.transform.position) : 0f;
            int units = 0;
            foreach (KeyValuePair<string, int> each in what)
            {
                units += each.Value;
            }
            cornerLines.Add($"Put away {text} (a chest {away:0} m away)");
            cornerUnits += units;
        }

        public void Finished(SweepReport report)
        {
            // The top-left lines: one per chest up to three, else one for them all (the corner shows one a second; code
            // review n6).
            if (cornerLines.Count > MostCornerLines)
            {
                QoLMods.Kit.Say.TopLeft($"Put away {cornerUnits} loose items into {cornerLines.Count} chests", log: true);
            }
            else
            {
                foreach (string line in cornerLines)
                {
                    QoLMods.Kit.Say.TopLeft(line, log: true);
                }
            }
            cornerLines.Clear();
            cornerUnits = 0;
            if (report.Units > 0 && Settings.LogFeeds.Value)
            {
                Module.Log.LogInfo($"Loose items: put away {report.Units} into {report.Chests} chest(s) over {report.PutFrames} frame(s); " +
                                   $"{report.Found} of the {report.Looked} items loaded were found to put away, {report.Waiting} wait for room.");
            }
            loadedChests = null;
        }

        /// <summary>What a sweep noted, forgotten.</summary>
        internal void Forget()
        {
            items.Clear();
            players.Clear();
            eaters.Clear();
            eaten.Clear();
            atOutputs.Clear();
            loadedChests = null;
            cornerLines.Clear();
            cornerUnits = 0;
        }

        // The questions, cheap first (Qualify.Early), then the body, what's under it, tar, stations and tames (Qualify.Why).
        private Left Check(ItemDrop drop, Waits waits, out ZNetView view)
        {
            view = null;
            var facts = new ItemFacts();
            if (drop != null)
            {
                view = drop.GetComponent<ZNetView>();
                facts.Ours = GroundPut.Ours(drop, view) && drop.m_itemData.m_shared != null;
            }
            if (!facts.Ours)
            {
                return Left.NotOurs;
            }
            ItemDrop.ItemData item = drop.m_itemData;
            ZDO zdo = view.GetZDO();
            facts.IsPiece = drop.IsPiece();
            facts.QuestItem = item.m_shared.m_questItem;
            facts.AutoPickup = drop.m_autoPickup;
            facts.Waiting = waits != null && waits.Has(new ItemKey(zdo.m_uid.UserID, zdo.m_uid.ID), Time.time);
            facts.Age = AgeOf(zdo);
            Spot at = SpotOf(drop.transform.position);
            facts.FromYou = at.Distance(you);
            facts.NearestPlayer = NearestPlayer(at);
            facts.OnNeverList = !never.IsEmpty && never.Has(PrefabOf(item, drop), TypeName(item.m_shared.m_itemType));
            facts.LoneEgg = rules.Eggs && item.m_stack == 1 && drop.GetComponent<EggGrow>() != null;
            Fish fish = drop.GetComponent<Fish>();
            facts.FishInWater = fish != null && !fish.IsOutOfWater();
            Left early = Qualify.Early(facts, rules);
            if (early != Left.None)
            {
                return early;
            }
            Rigidbody body = drop.GetComponent<Rigidbody>();
            facts.Body = body != null && !body.isKinematic;
            if (facts.Body)
            {
                facts.Speed = body.linearVelocity.magnitude;
                if (facts.Speed <= PickupRules.MostSpeed)
                {
                    // Floating in water lies still as well as on the ground (code review m1): the game's Floating knows a
                    // water level for it, and it isn't in tar.
                    Floating floating = drop.GetComponent<Floating>();
                    facts.Afloat = floating != null && floating.HaveLiquidLevel() && !floating.IsInTar();
                    facts.Resting = !facts.Afloat && Resting(drop, body);   // afloat, no need to look under it
                }
            }
            if (facts.Body && (facts.Resting || facts.Afloat))
            {
                facts.InTar = drop.InTar();
                facts.AtHandledOutput = atOutputs.Contains(drop.GetInstanceID());
                facts.NearEater = rules.Fodder && eaten.Contains(item.m_shared.m_name) && Fodder.Near(eaters, at, item.m_shared.m_name);
            }
            return Qualify.Why(facts, rules);
        }

        // Seconds since it was made, by the world's clock (its record's spawn time, which the game sets as it's made); an
        // unreadable time counts as long ago.
        private static double AgeOf(ZDO zdo)
        {
            long born = zdo.GetLong(ZDOVars.s_spawnTime, 0L);
            if (born <= 0L || born > DateTime.MaxValue.Ticks)
            {
                return double.MaxValue;
            }
            return (ZNet.instance.GetTime() - new DateTime(born)).TotalSeconds;
        }

        private void NotePlayers()
        {
            players.Clear();
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null)
                {
                    players.Add(SpotOf(player.transform.position));
                }
            }
        }

        private float NearestPlayer(Spot at)
        {
            float nearest = float.MaxValue;
            foreach (Spot player in players)
            {
                nearest = Math.Min(nearest, at.Distance(player));
            }
            return nearest;
        }

        // Something under it besides itself: a sphere at the bottom of its shape, on the layers its own layer collides with.
        private bool Resting(ItemDrop drop, Rigidbody body)
        {
            Collider shape = drop.GetComponent<Collider>();
            if (shape == null)
            {
                shape = drop.GetComponentInChildren<Collider>();
            }
            if (shape == null)
            {
                return false;
            }
            Bounds bounds = shape.bounds;
            var bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float radius = Mathf.Min(0.6f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.75f + 0.1f);
            int count = Physics.OverlapSphereNonAlloc(bottom, radius, touching, MaskFor(drop.gameObject.layer), QueryTriggerInteraction.Ignore);
            bool resting = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = touching[i];
                if (other != null && other.attachedRigidbody != body)
                {
                    resting = true;
                }
                touching[i] = null;
            }
            return resting;
        }

        private int MaskFor(int layer)
        {
            if (layer < 0 || layer > 31)
            {
                return Physics.DefaultRaycastLayers;
            }
            if (!masksKnown[layer])
            {
                int mask = 0;
                for (int other = 0; other < 32; other++)
                {
                    if (!Physics.GetIgnoreLayerCollision(layer, other))
                    {
                        mask |= 1 << other;
                    }
                }
                masks[layer] = mask;
                masksKnown[layer] = true;
            }
            return masks[layer];
        }

        // The stations whose products the core puts away now (Products.Look's own test): what lies at their outputs is
        // theirs, whatever their settings then do with it (put away, linked, or left where it fell). While a rival feeding
        // mod runs, every station's output is left to it (it may put products away itself), as the rival's warning says
        // (code review n4). "At its output" is exactly what the station's put-away looks at (the same search, Lying.At
        // within Products.Reach), so nothing lies between the two: a margin of 0.5 m here once left a product 1.5 to 2 m
        // from its output to neither (card B30).
        private void NoteOutputs()
        {
            bool rival = Rivals.Any;
            if (!Settings.FeedStations.Value && !rival)
            {
                return;
            }
            foreach (Fed fed in Registry.All)
            {
                if (fed == null || !fed.Alive || fed.IsLight || !Stations.Feedable(fed) ||
                    (!rival && (!fed.Mine || !Stations.IsFed(fed))))
                {
                    continue;
                }
                Transform output = Stations.OutputOf(fed);
                if (output == null)
                {
                    continue;
                }
                HashSet<string> products = Stations.ProductsOf(fed);
                foreach (ItemDrop drop in Lying.At(output.position, Products.Reach))
                {
                    if (drop != null && drop.m_autoPickup && drop.m_itemData?.m_shared != null && products.Contains(drop.m_itemData.m_shared.m_name))
                    {
                        atOutputs.Add(drop.GetInstanceID());
                    }
                }
            }
        }

        // Tamed animals and those that can be tamed, with what they eat off the ground (MonsterAI.m_consumeItems).
        private void NoteEaters()
        {
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character == null || character.GetComponent<Tameable>() == null)
                {
                    continue;
                }
                MonsterAI ai = character.GetBaseAI() as MonsterAI;
                if (ai == null || ai.m_consumeItems == null || ai.m_consumeItems.Count == 0)
                {
                    continue;
                }
                var eats = new HashSet<string>(StringComparer.Ordinal);
                foreach (ItemDrop food in ai.m_consumeItems)
                {
                    if (food != null && food.m_itemData?.m_shared != null)
                    {
                        eats.Add(food.m_itemData.m_shared.m_name);
                    }
                }
                if (eats.Count == 0)
                {
                    continue;
                }
                eaters.Add(new Eater { At = SpotOf(character.transform.position), Reach = Fodder.ReachOf(ai.m_consumeSearchRange), Eats = eats });
                eaten.UnionWith(eats);
            }
        }

        // The chests near one kind's items that could ever take it this sweep (by this computer's copies: holding it, or
        // set up for it, or any with "only where held" off) and that the rules let him use, nearest the items first; read
        // once per kind per sweep, each frame then asks which of them may be changed now and has room.
        private List<Container> ChestsFor(Job job, ItemDrop.ItemData like, float range, ChestRules chestRules, bool onlyWhereHeld, bool followSetups)
        {
            if (job.Tag is List<Container> known)
            {
                return known;
            }
            if (loadedChests == null)
            {
                loadedChests = ChestFinder.Loaded();
            }
            long me = ChestClaims.PlayerId();
            float reach = range + job.Radius;
            var near = new List<Container>();
            var away = new Dictionary<Container, float>();
            foreach (Container chest in loadedChests)
            {
                if (chest == null)
                {
                    continue;
                }
                Spot at = SpotOf(chest.transform.position);
                if (!at.Within(job.Centre, reach) || Obliterator.Is(chest))
                {
                    continue;   // never the Obliterator (code review M1; Settings.Rules' Skip leaves it out of MayUse too)
                }
                Inventory inventory = chest.GetInventory();
                if (inventory == null)
                {
                    continue;
                }
                Func<ItemDrop.ItemData, int> setup = followSetups ? Setups.Reading(chest) : null;
                if (setup != null ? setup(like) <= 0 : onlyWhereHeld && !inventory.ContainsItemByName(job.Name))
                {
                    continue;
                }
                if (!ChestFinder.MayUse(chest, chestRules, me))
                {
                    continue;
                }
                near.Add(chest);
                away[chest] = at.DistanceSquared(job.Centre);
            }
            near.Sort((a, b) => away[a].CompareTo(away[b]));
            job.Tag = near;
            return near;
        }

        private static ChestView ViewOf(Container chest, ItemDrop.ItemData like, bool useReserved, bool followSetups, long me)
        {
            Inventory inventory = chest != null ? chest.GetInventory() : null;
            if (inventory == null)
            {
                return new ChestView();
            }
            // As Stow and GroundPut will ask (ChestUse), and not while another player is about to open it.
            bool obliterator = Obliterator.Is(chest);
            bool usable = ChestUse.WhyNot(ChestClaims.MayChange(chest), obliterator, ChestClaims.IsKeptForCraft(chest),
                                          ChestReserve.IsReserved(chest), useReserved) == null && !ChestClaims.IsWaitedOn(chest);
            Func<ItemDrop.ItemData, int> setup = followSetups ? Setups.Reading(chest) : null;
            return new ChestView
            {
                At = SpotOf(chest.transform.position),
                Usable = usable,
                Destroys = obliterator,
                Holds = inventory.ContainsItemByName(like.m_shared.m_name),
                HasSetup = setup != null,
                SetupTakes = setup != null && setup(like) > 0,
                OthersMark = ChestMine.MarkedByOther(chest, me),
                Room = usable ? ChestPut.Room(inventory, like, topUpOnly: false) : 0,
            };
        }

        private void ReadNever()
        {
            string text = Settings.LooseNever.Value ?? string.Empty;
            if (text == neverText)
            {
                return;
            }
            neverText = text;
            never = NeverList.Parse(text);
            var unknown = new List<string>();
            foreach (string word in never.Words)
            {
                if (RuleLists.Item(word) == null && !IsTypeName(word))
                {
                    unknown.Add(word);
                }
            }
            if (unknown.Count > 0 || never.Bad.Count > 0)
            {
                Module.Warn("Some words in Topper-Upper's \"Never pick up\" match nothing: " +
                            (unknown.Count > 0 ? $"not an item type or an item the game knows: {string.Join(", ", unknown)}" : string.Empty) +
                            (unknown.Count > 0 && never.Bad.Count > 0 ? "; " : string.Empty) +
                            (never.Bad.Count > 0 ? $"not a plain name: {string.Join(", ", never.Bad)}" : string.Empty) +
                            ". Name item types as the game does (Trophy, Ammo, Fish) and items by prefab name (BoneFragments).");
            }
        }

        private static bool IsTypeName(string word)
        {
            foreach (string name in Enum.GetNames(typeof(ItemDrop.ItemData.ItemType)))
            {
                if (string.Equals(name, word, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string TypeName(ItemDrop.ItemData.ItemType type)
        {
            if (!TypeNames.TryGetValue(type, out string name))
            {
                TypeNames[type] = name = type.ToString();
            }
            return name;
        }

        private static string PrefabOf(ItemDrop.ItemData item, ItemDrop drop) =>
            item.m_dropPrefab != null ? item.m_dropPrefab.name : Utils.GetPrefabName(drop.gameObject);

        /// <summary>At the module's start: the game's own list of every item loaded on this computer (ItemDrop.Awake adds,
        /// OnDestroy takes out), found by name; if the game has changed it, the start log says the physics search around him
        /// stands in.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            FieldInfo field = typeof(ItemDrop).GetField("s_instances", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            instancesField = field != null && field.FieldType == typeof(List<ItemDrop>) ? field : null;
            if (instancesField == null)
            {
                missingOptional.Add($"the loose-item pickup's full list of items on the ground (the game has changed: ItemDrop.s_instances " +
                                    $"not found; it looks only within {FallbackReach:0} m of you, at most 256 items at a time)");
            }
        }

        // The game's list, or null (then the physics search around him stands in).
        private static List<ItemDrop> Instances() => instancesField?.GetValue(null) as List<ItemDrop>;

        private static Spot SpotOf(Vector3 at) => new Spot(at.x, at.y, at.z);

        private static Spot Middle(List<Spot> spots)
        {
            float x = 0f, y = 0f, z = 0f;
            foreach (Spot spot in spots)
            {
                x += spot.X;
                y += spot.Y;
                z += spot.Z;
            }
            return new Spot(x / spots.Count, y / spots.Count, z / spots.Count);
        }

        // An item's data by reference (its own equality isn't overridden, but say so).
        private sealed class SameItem : IEqualityComparer<ItemDrop.ItemData>
        {
            internal static readonly SameItem Instance = new SameItem();

            public bool Equals(ItemDrop.ItemData a, ItemDrop.ItemData b) => ReferenceEquals(a, b);

            public int GetHashCode(ItemDrop.ItemData item) => RuntimeHelpers.GetHashCode(item);
        }
    }
}
