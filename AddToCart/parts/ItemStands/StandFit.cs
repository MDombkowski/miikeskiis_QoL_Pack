// The QoL Mods item stands part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemStands\. Every mod that uses
// it carries a copy in its own parts\ItemStands\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace QoLMods.ItemStands
{
    /// <summary>Which of the counted stands take an item: the wall item stand (vertical), the flat one (horizontal), both,
    /// or neither; and, apart from those, the armor stand. One mark shows Wall, Flat or Both, another Armor: a caller
    /// hands each its own part of an item's fit (Both and Armor as masks).</summary>
    [Flags]
    internal enum Fit
    {
        None = 0,
        Wall = 1,
        Flat = 2,
        Both = Wall | Flat,

        /// <summary>The armor stand: one of its slots takes the item.</summary>
        Armor = 4,
    }

    /// <summary>How the rule is asked, from the calling module's settings (a part has none of its own).</summary>
    internal sealed class StandRule
    {
        internal static readonly StandRule Default = new StandRule(true, StandFit.DefaultStands, false, StandFit.DefaultArmorStands);

        internal StandRule(bool live, string stands, bool otherStands, string armorStands)
        {
            Live = live;
            Stands = stands ?? string.Empty;
            OtherStands = otherStands;
            ArmorStands = armorStands ?? string.Empty;
        }

        /// <summary>True: the game's own check (ItemStand.CanAttach), so another mod's change to it shows. False: the part's
        /// own copy of the check, reading only the stands' lists and the item's model.</summary>
        internal bool Live { get; }

        /// <summary>The stands that count, by prefab name, separated by commas or spaces.</summary>
        internal string Stands { get; }

        /// <summary>Also every other buildable piece that holds an item the way an item stand does, or wears gear the way
        /// the armor stand does.</summary>
        internal bool OtherStands { get; }

        /// <summary>The armor stands that count, by prefab name, separated by commas or spaces.</summary>
        internal string ArmorStands { get; }

        internal bool SameAs(StandRule other) =>
            other != null && other.Live == Live && other.OtherStands == OtherStands && string.Equals(other.Stands, Stands, StringComparison.Ordinal)
            && string.Equals(other.ArmorStands, ArmorStands, StringComparison.Ordinal);
    }

    /// <summary>
    /// Which item stands take an item (card A51), asked of the game's own rule on the stands' prefabs, so another mod that
    /// lets more items onto stands shows too. The rule (ItemStand.CanAttach, private): the item needs a model to hang (a
    /// direct child named "attach"), mustn't be on the stand's refused list, and must be on its listed items (an empty
    /// list takes all) or of a type it allows. Which way a stand faces never refuses anything; it only names the side:
    /// Vertical is the wall stand, Horizontal the flat one, both (or neither named) counts for both. The boss stones, the
    /// altars' holders and the Chiselled Platform (stands that take their one item by themselves, or can't give it back,
    /// or grant a power) never count, whatever the list says. Answers are kept per item prefab until the world, the rule or
    /// the counted stands change. Checked against the game's ItemStand of 2026-09-30 (Valheim 1.0.16).
    /// The armor stand (his 0.16.0 ask): each of its slots (ArmorStand.CanAttach, private) takes the types it lists (an
    /// empty list takes all), by the item's attach type where it has one, else its own type; and the game then hangs it
    /// only if it has a model to show (a direct child named "attach" or "attach_skin"), which a chest piece or legs
    /// never need (ArmorStand.UseItem). An item fits the armor stand if one slot takes it.
    /// </summary>
    internal static class StandFit
    {
        internal const string DefaultStands = "itemstand, itemstandh";

        /// <summary>The game's one armor stand.</summary>
        internal const string DefaultArmorStands = "ArmorStand";

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Func<ItemStand, ItemDrop.ItemData, bool> canAttach;
        private static Func<ArmorStand, ArmorStand.ArmorStandSlot, ItemDrop.ItemData, bool> armorCanAttach;
        private static bool resolved;
        private static string missingCheck;
        private static string missingArmorCheck;

        private static ZNetScene scene;
        private static ObjectDB sceneDb;
        private static ObjectDB db;
        private static StandRule ruleUsed;
        private static readonly List<Counted> Stands = new List<Counted>();
        private static readonly List<ArmorStand> Armors = new List<ArmorStand>();
        private static readonly HashSet<string> CountedNames = new HashSet<string>();
        private static readonly HashSet<string> ArmorNames = new HashSet<string>();
        private static readonly Dictionary<GameObject, Fit> ByPrefab = new Dictionary<GameObject, Fit>();
        private static readonly Dictionary<string, GameObject> ByName = new Dictionary<string, GameObject>();
        private static bool byNameFilled;

        /// <summary>Called when a live check failed on an item (another mod's patch on it threw): the first time only, with
        /// what happened. The module that uses the part sets it (its log).</summary>
        internal static Action<string> Warn { get; set; }

        private static bool warned;
        private static bool armorWarned;
        private static readonly HashSet<string> ToldStands = new HashSet<string>();

        private readonly struct Counted
        {
            internal readonly ItemStand Stand;
            internal readonly Fit Side;

            internal Counted(ItemStand stand, Fit side)
            {
                Stand = stand;
                Side = side;
            }
        }

        /// <summary>
        /// Called from each using module's Resolve, at the game's start: finds the game's own check. Without it the marks
        /// follow the part's own copy of the check (the stands' lists and the item's model), which a mod patching the check
        /// wouldn't change; that goes to <paramref name="missingOptional"/>.
        /// </summary>
        internal static void Resolve(List<string> missingOptional)
        {
            if (!resolved)
            {
                resolved = true;
                MethodInfo method = typeof(ItemStand).GetMethod("CanAttach", AnyInstance, null, new[] { typeof(ItemDrop.ItemData) }, null);
                if (method == null || method.ReturnType != typeof(bool))
                {
                    missingCheck = "ItemStand.CanAttach (the marks then follow the stands' own lists; a mod that changes the check isn't seen)";
                }
                else
                {
                    try
                    {
                        canAttach = (Func<ItemStand, ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemStand, ItemDrop.ItemData, bool>), method);
                    }
                    catch (Exception error)
                    {
                        missingCheck = $"ItemStand.CanAttach ({error.Message}; the marks then follow the stands' own lists)";
                    }
                }
                MethodInfo armor = typeof(ArmorStand).GetMethod("CanAttach", AnyInstance, null,
                                                                new[] { typeof(ArmorStand.ArmorStandSlot), typeof(ItemDrop.ItemData) }, null);
                if (armor == null || armor.ReturnType != typeof(bool))
                {
                    missingArmorCheck = "ArmorStand.CanAttach (the armor stand's marks then follow its slots' own lists; a mod that changes the check isn't seen)";
                }
                else
                {
                    try
                    {
                        armorCanAttach = (Func<ArmorStand, ArmorStand.ArmorStandSlot, ItemDrop.ItemData, bool>)Delegate.CreateDelegate(
                            typeof(Func<ArmorStand, ArmorStand.ArmorStandSlot, ItemDrop.ItemData, bool>), armor);
                    }
                    catch (Exception error)
                    {
                        missingArmorCheck = $"ArmorStand.CanAttach ({error.Message}; the armor stand's marks then follow its slots' own lists)";
                    }
                }
            }
            if (missingCheck != null)
            {
                missingOptional.Add(missingCheck);
            }
            if (missingArmorCheck != null)
            {
                missingOptional.Add(missingArmorCheck);
            }
        }

        /// <summary>True once the game's own check was found (the live rule can be asked).</summary>
        internal static bool HasLiveCheck => canAttach != null;

        /// <summary>True once the armor stand's own check was found.</summary>
        internal static bool HasLiveArmorCheck => armorCanAttach != null;

        /// <summary>The stands counted now, by prefab name, and each one's side; empty before a world is loaded.</summary>
        internal static IEnumerable<KeyValuePair<string, Fit>> Counting(StandRule rule)
        {
            Prepare(rule);
            foreach (Counted counted in Stands)
            {
                if (counted.Stand != null)
                {
                    yield return new KeyValuePair<string, Fit>(counted.Stand.gameObject.name, counted.Side);
                }
            }
        }

        /// <summary>Which counted stands take the item this prefab drops (an item's own prefab, as the crafting menu, the
        /// ingredients and the trader know it): Wall, Flat or Both for the item stands, with Armor added for the armor
        /// stand.</summary>
        internal static Fit Of(GameObject itemPrefab, StandRule rule)
        {
            if (itemPrefab == null || rule == null)
            {
                return Fit.None;
            }
            Prepare(rule);
            if (scene == null)
            {
                return Fit.None;   // no world: no stands to ask
            }
            if (ByPrefab.TryGetValue(itemPrefab, out Fit fit))
            {
                return fit;
            }
            fit = Work(itemPrefab, rule);
            ByPrefab[itemPrefab] = fit;
            return fit;
        }

        /// <summary>Which counted stands take an item he has (in an inventory, a chest, the hotbar): by the prefab it drops
        /// as, or by its name when it doesn't know it.</summary>
        internal static Fit Of(ItemDrop.ItemData item, StandRule rule) => Of(PrefabOf(item), rule);

        /// <summary>The prefab an item he has drops as, or the first item prefab of its name when it doesn't know it.</summary>
        internal static GameObject PrefabOf(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return null;
            }
            return item.m_dropPrefab != null ? item.m_dropPrefab : PrefabNamed(item.m_shared.m_name);
        }

        /// <summary>Which counted stands take the item of this shared name ("$item_club"), by the first item prefab of that
        /// name (a few pairs share one name, such as the two troll trophies; each pair fits alike).</summary>
        internal static Fit OfName(string sharedName, StandRule rule) => Of(PrefabNamed(sharedName), rule);

        /// <summary>Whether an item stand in the world is one the rule counts: a piece it names (or any, with "other stand
        /// pieces"), and a stand that counts. For the stand he aims at.</summary>
        internal static bool IsCounted(ItemStand stand, StandRule rule)
        {
            if (stand == null || rule == null || !Counts(stand))
            {
                return false;
            }
            Prepare(rule);
            return CountedNames.Contains(PieceName(stand));
        }

        /// <summary>Whether an armor stand in the world is one the rule counts.</summary>
        internal static bool IsCounted(ArmorStand armor, StandRule rule)
        {
            if (armor == null || rule == null)
            {
                return false;
            }
            Prepare(rule);
            return ArmorNames.Contains(PieceName(armor));
        }

        /// <summary>Whether this item stand in the world (the one he aims at) takes the item this prefab drops.</summary>
        internal static bool TakenBy(ItemStand stand, GameObject itemPrefab, StandRule rule)
        {
            ItemDrop.ItemData copy = CopyOf(itemPrefab);
            return copy != null && stand != null && rule != null && Takes(stand, copy, rule);
        }

        /// <summary>Whether this slot of an armor stand in the world (the one he aims at) takes the item this prefab
        /// drops, and the stand would hang it.</summary>
        internal static bool TakenBy(ArmorStand armor, ArmorStand.ArmorStandSlot slot, GameObject itemPrefab, StandRule rule)
        {
            ItemDrop.ItemData copy = CopyOf(itemPrefab);
            return copy != null && armor != null && slot != null && rule != null && HangsOnArmorStand(copy) && SlotTakes(armor, slot, copy, rule);
        }

        // The piece a stand belongs to, by its prefab name (the stand may sit on a child of it, as on the boss stones).
        private static string PieceName(Component part)
        {
            ZNetView view = part.GetComponentInParent<ZNetView>();
            return Utils.GetPrefabName(view != null ? view.gameObject : part.gameObject);
        }

        /// <summary>Forgets every answer and the counted stands (the next question reads them again).</summary>
        internal static void Forget()
        {
            scene = null;
            sceneDb = null;
            db = null;
            ruleUsed = null;
            Stands.Clear();
            Armors.Clear();
            CountedNames.Clear();
            ArmorNames.Clear();
            ByPrefab.Clear();
            ByName.Clear();
            byNameFilled = false;
        }

        private static GameObject PrefabNamed(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName))
            {
                return null;
            }
            ObjectDB objects = ObjectDB.instance;
            if (objects == null)
            {
                return null;
            }
            if (db != objects)
            {
                ByName.Clear();
                byNameFilled = false;
            }
            if (!byNameFilled)
            {
                byNameFilled = true;
                db = objects;   // the item list of this world
                foreach (GameObject prefab in objects.m_items)
                {
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    string name = drop?.m_itemData?.m_shared?.m_name;
                    if (!string.IsNullOrEmpty(name) && !ByName.ContainsKey(name))
                    {
                        ByName[name] = prefab;
                    }
                }
            }
            ByName.TryGetValue(sharedName, out GameObject found);
            return found;
        }

        // Starts over when the world (its ZNetScene or ObjectDB) or the rule changed, and reads the counted stands again.
        private static void Prepare(StandRule rule)
        {
            ZNetScene nowScene = ZNetScene.instance;
            ObjectDB nowDb = ObjectDB.instance;
            if (nowScene == scene && nowDb == sceneDb && rule.SameAs(ruleUsed))
            {
                return;
            }
            Stands.Clear();
            Armors.Clear();
            CountedNames.Clear();
            ArmorNames.Clear();
            ByPrefab.Clear();
            scene = nowScene;
            sceneDb = nowDb;
            ruleUsed = rule;
            if (scene == null)
            {
                return;
            }
            var seen = new HashSet<ItemStand>();
            foreach (string name in Names(rule.Stands))
            {
                GameObject prefab = scene.GetPrefab(name);
                ItemStand stand = prefab != null ? StandOn(prefab) : null;
                if (stand != null && Counts(stand))
                {
                    CountedNames.Add(prefab.name);
                    if (seen.Add(stand))
                    {
                        Stands.Add(new Counted(stand, SideOf(stand)));
                    }
                }
                else if (ToldStands.Add(name))
                {
                    // Said once a session (review 1): a misspelt name would otherwise just leave the marks out.
                    Warn?.Invoke(prefab == null ? $"\"{name}\" in \"Stands that count\" is no piece of the game (the names are the pieces' prefab names, such as itemstand, exact to the letter, lower and upper case included)."
                                 : stand == null ? $"\"{name}\" in \"Stands that count\" holds no item, so it isn't asked."
                                 : $"\"{name}\" in \"Stands that count\" is never counted: it takes its one item by itself, can't give it back, or grants a power.");
                }
            }
            var seenArmor = new HashSet<ArmorStand>();
            foreach (string name in Names(rule.ArmorStands))
            {
                GameObject prefab = scene.GetPrefab(name);
                ArmorStand armor = prefab != null ? ArmorOn(prefab) : null;
                if (armor != null && HasSlots(armor))
                {
                    ArmorNames.Add(prefab.name);
                    if (seenArmor.Add(armor))
                    {
                        Armors.Add(armor);
                    }
                }
                else if (ToldStands.Add("armor:" + name))
                {
                    Warn?.Invoke(prefab == null ? $"\"{name}\" in \"Armor stands that count\" is no piece of the game (the names are the pieces' prefab names, such as ArmorStand, exact to the letter, lower and upper case included)."
                                 : armor == null ? $"\"{name}\" in \"Armor stands that count\" is no armor stand, so it isn't asked."
                                 : $"\"{name}\" in \"Armor stands that count\" has no slots, so it takes nothing.");
                }
            }
            if (rule.OtherStands)
            {
                foreach (GameObject prefab in scene.m_prefabs)
                {
                    if (prefab == null || prefab.GetComponent<Piece>() == null)
                    {
                        continue;
                    }
                    ItemStand stand = StandOn(prefab);
                    if (stand != null && Counts(stand))
                    {
                        CountedNames.Add(prefab.name);
                        if (seen.Add(stand))
                        {
                            Stands.Add(new Counted(stand, SideOf(stand)));
                        }
                    }
                    ArmorStand armor = ArmorOn(prefab);
                    if (armor != null && HasSlots(armor))
                    {
                        ArmorNames.Add(prefab.name);
                        if (seenArmor.Add(armor))
                        {
                            Armors.Add(armor);
                        }
                    }
                }
            }
        }

        private static ArmorStand ArmorOn(GameObject prefab)
        {
            ArmorStand armor = prefab.GetComponent<ArmorStand>();
            return armor != null ? armor : prefab.GetComponentInChildren<ArmorStand>(true);
        }

        private static bool HasSlots(ArmorStand armor) => armor.m_slots != null && armor.m_slots.Count > 0;

        /// <summary>The prefab names in a setting's line: separated by commas, semicolons or spaces, blanks ignored.</summary>
        internal static IEnumerable<string> Names(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                yield break;
            }
            foreach (string part in line.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return part.Trim();
            }
        }

        private static ItemStand StandOn(GameObject prefab)
        {
            ItemStand stand = prefab.GetComponent<ItemStand>();
            return stand != null ? stand : prefab.GetComponentInChildren<ItemStand>(true);
        }

        /// <summary>A stand counts unless it takes its one item by itself (the boss stones, the altars' holders, the
        /// Chiselled Platform), can't give an item back, or grants a power.</summary>
        internal static bool Counts(ItemStand stand) =>
            stand != null && Counts(stand.m_autoAttach, stand.m_canBeRemoved, stand.m_guardianPower != null);

        internal static bool Counts(bool takesByItself, bool canBeRemoved, bool grantsPower) => !takesByItself && canBeRemoved && !grantsPower;

        /// <summary>The side a stand counts for: its orientation's, and both when it names both or neither.</summary>
        internal static Fit SideOf(ItemStand stand) => SideOf(stand.m_orientationType);

        internal static Fit SideOf(ItemStand.Orientation orientation)
        {
            bool vertical = (orientation & ItemStand.Orientation.Vertical) != 0;
            bool horizontal = (orientation & ItemStand.Orientation.Horizontal) != 0;
            if (vertical == horizontal)
            {
                return Fit.Both;
            }
            return vertical ? Fit.Wall : Fit.Flat;
        }

        /// <summary>An item's fit from the stands asked: the sides of those that take it (each asked only while its side
        /// isn't already known).</summary>
        internal static Fit Combine<T>(IEnumerable<T> stands, Func<T, Fit> side, Func<T, bool> takes)
        {
            Fit fit = Fit.None;
            foreach (T stand in stands)
            {
                Fit its = side(stand);
                if ((fit & its) != its && takes(stand))
                {
                    fit |= its;
                }
            }
            return fit;
        }

        // Asks every counted stand about one item prefab.
        private static Fit Work(GameObject prefab, StandRule rule)
        {
            ItemDrop.ItemData copy = CopyOf(prefab);
            if (copy == null)
            {
                return Fit.None;
            }
            Fit fit = Combine(Stands, counted => counted.Stand != null ? counted.Side : Fit.None,
                              counted => counted.Stand != null && Takes(counted.Stand, copy, rule));
            if (Armors.Count > 0 && HangsOnArmorStand(copy))
            {
                foreach (ArmorStand armor in Armors)
                {
                    if (armor != null && AnySlotTakes(armor, copy, rule))
                    {
                        fit |= Fit.Armor;
                        break;
                    }
                }
            }
            return fit;
        }

        // A copy of a prefab's item data that knows its prefab: the prefab's own never does (only an item in the world,
        // in an inventory or on a stand is told), and the checks look for the model through it.
        private static ItemDrop.ItemData CopyOf(GameObject prefab)
        {
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData?.m_shared == null)
            {
                return null;
            }
            ItemDrop.ItemData copy = drop.m_itemData.Clone();
            copy.m_dropPrefab = prefab;
            return copy;
        }

        private static bool AnySlotTakes(ArmorStand armor, ItemDrop.ItemData item, StandRule rule)
        {
            foreach (ArmorStand.ArmorStandSlot slot in armor.m_slots)
            {
                if (slot != null && SlotTakes(armor, slot, item, rule))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SlotTakes(ArmorStand armor, ArmorStand.ArmorStandSlot slot, ItemDrop.ItemData item, StandRule rule)
        {
            if (rule.Live && armorCanAttach != null)
            {
                try
                {
                    return armorCanAttach(armor, slot, item);
                }
                catch (Exception error)
                {
                    if (!armorWarned)
                    {
                        armorWarned = true;
                        Warn?.Invoke($"The game's armor stand check failed on {item.m_dropPrefab.name} (another mod's change to it?), " +
                                     $"so the slots' own lists are used for it, and for any other item it fails on: {error.Message}");
                    }
                }
            }
            return SlotAllows(slot.m_supportedTypes, item.m_shared.m_itemType, item.m_shared.m_attachOverride);
        }

        /// <summary>An armor stand slot's check, copied (ArmorStand.CanAttach): an empty list takes every type; else the
        /// item's attach type where it has one, or its own type, must be listed. A missing list takes nothing (the game
        /// would fail on it).</summary>
        internal static bool SlotAllows(IList<ItemDrop.ItemData.ItemType> supported, ItemDrop.ItemData.ItemType type,
                                        ItemDrop.ItemData.ItemType attachOverride)
        {
            if (supported == null)
            {
                return false;
            }
            return supported.Count == 0 || supported.Contains(attachOverride != ItemDrop.ItemData.ItemType.None ? attachOverride : type);
        }

        /// <summary>Whether the armor stand would hang the item once a slot takes it (ArmorStand.UseItem): a chest piece or
        /// legs always; anything else only with a model to show, a direct child named "attach" or "attach_skin".</summary>
        internal static bool HangsOnArmorStand(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || item.m_dropPrefab == null)
            {
                return false;
            }
            Transform root = item.m_dropPrefab.transform;
            bool model = false;
            for (int child = 0; child < root.childCount && !model; child++)
            {
                model = IsModelName(root.GetChild(child).gameObject.name);
            }
            return HangsOnArmorStand(item.m_shared.m_itemType, model);
        }

        /// <summary>The names the armor stand looks for among an item prefab's direct children: exact, case included.</summary>
        internal static bool IsModelName(string name) => name == "attach" || name == "attach_skin";

        /// <summary>The same on plain facts: the item's own type (not its attach type, as the game reads it) and whether it
        /// has a model to show.</summary>
        internal static bool HangsOnArmorStand(ItemDrop.ItemData.ItemType type, bool model) =>
            type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Chest || model;

        private static bool Takes(ItemStand stand, ItemDrop.ItemData item, StandRule rule)
        {
            if (rule.Live && canAttach != null)
            {
                try
                {
                    return canAttach(stand, item);
                }
                catch (Exception error)
                {
                    if (!warned)
                    {
                        warned = true;
                        Warn?.Invoke($"The game's item stand check failed on {item.m_dropPrefab.name} (another mod's change to it?), " +
                                     $"so the stands' own lists are used for it, and for any other item it fails on: {error.Message}");
                    }
                }
            }
            return ByLists(stand, item);
        }

        /// <summary>The game's check, copied: a model to hang, not refused, then listed (an empty list takes all) or of an
        /// allowed type. Only the stand's lists and the item's prefab are read, so a patch on the game's check isn't
        /// followed.</summary>
        internal static bool ByLists(ItemStand stand, ItemDrop.ItemData item) =>
            Decide(item.m_dropPrefab != null && item.m_dropPrefab.transform.Find("attach") != null, item.m_shared.m_name, item.m_shared.m_itemType,
                   name => Named(stand.m_unsupportedItems, name), stand.m_supportedItems.Count, name => Named(stand.m_supportedItems, name),
                   type => stand.m_supportedTypes.Contains(type));

        /// <summary>The game's check on plain facts, in its order: a model to hang; not refused by name; listed by name, or
        /// the list empty (which takes every item with a model); else allowed by its type.</summary>
        internal static bool Decide(bool hasModel, string name, ItemDrop.ItemData.ItemType type, Func<string, bool> refused, int listedCount,
                                    Func<string, bool> listed, Func<ItemDrop.ItemData.ItemType, bool> typeAllowed)
        {
            if (!hasModel || refused(name))
            {
                return false;
            }
            if (listedCount == 0 || listed(name))
            {
                return true;
            }
            return typeAllowed(type);
        }

        // Whether a stand's list names an item (by its shared name, as the game compares them); a missing entry never does.
        private static bool Named(List<ItemDrop> list, string name)
        {
            foreach (ItemDrop each in list)
            {
                if (each != null && each.m_itemData?.m_shared?.m_name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
