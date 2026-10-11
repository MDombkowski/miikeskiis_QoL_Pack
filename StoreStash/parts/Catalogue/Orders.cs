// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// The orders of the lists on the left, and of the headings that follow them: his orders by default, each one a
    /// setting of the module that uses the part (his rule 3). A setting is one line of names separated by commas; a
    /// family's members follow it in brackets, "Metals (Copper, Tin, …)". Names left out go at the end in the default
    /// order, and names the part doesn't know are ignored, so a typo can't hide anything. Case, spaces and "&amp;" for
    /// "and" don't matter.
    /// </summary>
    internal sealed class Orders
    {
        /// <summary>Build: the categories in the left list (Building Structures among them; its six are its members).</summary>
        internal readonly List<string> Categories;

        internal readonly List<string> BuildingSubs;

        /// <summary>Build: the material families in the left list, and the wood, stone and metal members of each.</summary>
        internal readonly List<string> MaterialFamilies;

        internal readonly Dictionary<string, List<string>> MaterialMembers;

        /// <summary>Craft: the kinds, and the stations (Made At) with By Hand.</summary>
        internal readonly List<string> Kinds;

        internal readonly List<string> Stations;

        /// <summary>Take: the families, and each family's members.</summary>
        internal readonly List<string> Families;

        internal readonly Dictionary<string, List<string>> FamilyMembers;

        private readonly Dictionary<string, int> categoryIndex;
        private readonly Dictionary<string, int> materialIndex;
        private readonly Dictionary<string, int> kindIndex;
        private readonly Dictionary<string, int> stationIndex;
        private readonly Dictionary<string, int> familyIndex;

        /// <summary>His orders, as agreed.</summary>
        internal static readonly Orders Default = new Orders(null, null, null, null, null);

        private Orders(string categories, string materials, string kinds, string stations, string families)
        {
            (Categories, Dictionary<string, List<string>> buildMembers) = Apply(categories, CategoryParents(), new Dictionary<string, List<string>>
            {
                { Taxonomy.BuildingParent, Taxonomy.BuildingSubs.ToList() },
            });
            BuildingSubs = buildMembers[Taxonomy.BuildingParent];
            (MaterialFamilies, MaterialMembers) = Apply(materials, Taxonomy.MaterialFamilies.ToList(), Taxonomy.StructuralFamilies.ToDictionary(
                family => family, family => Taxonomy.StructuralMaterials.Where(each => each.Family == family).Select(each => each.Label).ToList()));
            (Kinds, _) = Apply(kinds, Taxonomy.KindOrder.ToList(), new Dictionary<string, List<string>>());
            (Stations, _) = Apply(stations, DefaultStations(), new Dictionary<string, List<string>>());
            (Families, FamilyMembers) = Apply(families, Taxonomy.Families.ToList(), Taxonomy.Members.ToDictionary(each => each.Key, each => each.Value.ToList()));

            // Headings follow the left list: a parent, then its members.
            var flatCategories = new List<string>();
            foreach (string category in Categories)
            {
                flatCategories.Add(category);
                if (category == Taxonomy.BuildingParent)
                {
                    flatCategories.AddRange(BuildingSubs);
                }
            }
            categoryIndex = IndexOf(flatCategories);
            var flatMaterials = new List<string>();
            foreach (string family in MaterialFamilies)
            {
                if (MaterialMembers.TryGetValue(family, out List<string> members))
                {
                    flatMaterials.AddRange(members);
                }
            }
            materialIndex = IndexOf(flatMaterials);
            kindIndex = IndexOf(Kinds);
            stationIndex = IndexOf(Stations);
            familyIndex = IndexOf(Families);
        }

        /// <summary>The orders from a module's five settings; an empty or null line keeps his order for that list.</summary>
        internal static Orders From(string categories, string materials, string kinds, string stations, string families) =>
            new Orders(categories, materials, kinds, stations, families);

        // ---- The default lines, as a module's settings show them

        internal static string DefaultCategoryLine => Line(CategoryParents(), new Dictionary<string, List<string>>
        {
            { Taxonomy.BuildingParent, Taxonomy.BuildingSubs.ToList() },
        });

        internal static string DefaultMaterialLine => Line(Taxonomy.MaterialFamilies.ToList(), Default.MaterialMembers);

        internal static string DefaultKindLine => string.Join(", ", Taxonomy.KindOrder);

        internal static string DefaultStationLine => string.Join(", ", DefaultStations());

        internal static string DefaultFamilyLine => Line(Taxonomy.Families.ToList(), Taxonomy.Members.ToDictionary(each => each.Key, each => each.Value.ToList()));

        // ---- Places, for sorting headings: a name the list doesn't hold goes after every one it does

        internal double CategoryPlace(string category) => Place(categoryIndex, category);

        /// <summary>Where "Other Structures" goes: straight after the last of Building Structures' members.</summary>
        internal double OtherStructuresPlace => BuildingSubs.Max(sub => Place(categoryIndex, sub)) + 0.5;

        internal double MaterialPlace(string material) => Place(materialIndex, material);

        internal double KindPlace(string kind) => Place(kindIndex, kind);

        internal double StationPlace(string station) => Place(stationIndex, station);

        internal double FamilyPlace(string family) => Place(familyIndex, family);

        internal double MemberPlace(string family, string member) =>
            FamilyMembers.TryGetValue(family, out List<string> members) && members.IndexOf(member) is int at && at >= 0 ? at : 900;

        private static double Place(Dictionary<string, int> index, string name) =>
            name != null && index.TryGetValue(name, out int at) ? at : 900;

        private static Dictionary<string, int> IndexOf(List<string> names)
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < names.Count; i++)
            {
                if (!index.ContainsKey(names[i]))
                {
                    index[names[i]] = i;
                }
            }
            return index;
        }

        // The categories a hammer can show at the top level: every category but Building Structures' six, and but the
        // serving tray's (Food, Mead, Feasts), which no building piece carries.
        private static List<string> CategoryParents() =>
            Taxonomy.CategoryOrder.Where(category => Array.IndexOf(Taxonomy.BuildingSubs, category) < 0 && category != "Food" && category != "Mead" && category != "Feasts").ToList();

        private static List<string> DefaultStations()
        {
            var stations = new List<string> { Taxonomy.ByHand };
            stations.AddRange(Taxonomy.Stations.Select(each => each.Label));
            return stations;
        }

        private static string Line(List<string> parents, Dictionary<string, List<string>> members) =>
            string.Join(", ", parents.Select(parent => members.TryGetValue(parent, out List<string> list) && list.Count > 0 ? $"{parent} ({string.Join(", ", list)})" : parent));

        // ---- Reading a line

        /// <summary>
        /// Puts the parents in the line's order, then the ones it leaves out in the default order; and each parent's
        /// members in the order its brackets give, else in the order they appear bare in the line (the mock-up's
        /// category line lists Building Structures' six that way), then the rest in the default order.
        /// </summary>
        internal static (List<string> Parents, Dictionary<string, List<string>> Members) Apply(string line, List<string> parents,
                                                                                                Dictionary<string, List<string>> members)
        {
            List<(string Name, List<string> Members)> parsed = Parse(line);
            var order = new List<string>();
            var bare = new List<string>();
            var bracketed = new Dictionary<string, List<string>>();
            foreach ((string name, List<string> listed) in parsed)
            {
                string parent = Match(name, parents);
                if (parent != null)
                {
                    if (!order.Contains(parent))
                    {
                        order.Add(parent);
                    }
                    if (listed.Count > 0 && !bracketed.ContainsKey(parent))
                    {
                        bracketed[parent] = listed;
                    }
                }
                else
                {
                    bare.Add(name);
                }
            }
            foreach (string parent in parents)
            {
                if (!order.Contains(parent))
                {
                    order.Add(parent);
                }
            }
            var resolved = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<string>> family in members)
            {
                var list = new List<string>();
                IEnumerable<string> hints = bracketed.TryGetValue(family.Key, out List<string> given) ? given : bare;
                foreach (string hint in hints)
                {
                    string member = Match(hint, family.Value);
                    if (member != null && !list.Contains(member))
                    {
                        list.Add(member);
                    }
                }
                foreach (string member in family.Value)
                {
                    if (!list.Contains(member))
                    {
                        list.Add(member);
                    }
                }
                resolved[family.Key] = list;
            }
            return (order, resolved);
        }

        /// <summary>"A, B (x, y), C" as names with their bracketed members. Brackets that never close still count.</summary>
        internal static List<(string Name, List<string> Members)> Parse(string line)
        {
            var result = new List<(string, List<string>)>();
            if (string.IsNullOrWhiteSpace(line))
            {
                return result;
            }
            var name = new StringBuilder();
            var member = new StringBuilder();
            List<string> members = null;
            void EndMember()
            {
                if (member.ToString().Trim().Length > 0)
                {
                    members.Add(member.ToString().Trim());
                }
                member.Clear();
            }
            void EndName()
            {
                if (members != null)
                {
                    EndMember();
                }
                string text = name.ToString().Trim();
                if (text.Length > 0)
                {
                    result.Add((text, members ?? new List<string>()));
                }
                name.Clear();
                members = null;
            }
            foreach (char c in line)
            {
                if (members != null)
                {
                    if (c == ')')
                    {
                        EndMember();
                        result.Add((name.ToString().Trim(), members));
                        name.Clear();
                        members = null;
                        continue;
                    }
                    if (c == ',')
                    {
                        EndMember();
                        continue;
                    }
                    member.Append(c);
                    continue;
                }
                if (c == '(')
                {
                    members = new List<string>();
                    continue;
                }
                if (c == ',')
                {
                    EndName();
                    continue;
                }
                name.Append(c);
            }
            EndName();
            result.RemoveAll(each => each.Item1.Length == 0);
            return result;
        }

        /// <summary>The known name this one means, ignoring case, spaces, "&amp;" for "and" and a trailing full stop.</summary>
        internal static string Match(string name, IEnumerable<string> known)
        {
            string wanted = Normal(name);
            foreach (string each in known)
            {
                if (Normal(each) == wanted)
                {
                    return each;
                }
            }
            return null;
        }

        private static string Normal(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }
            string text = name.Replace("&", " and ").ToLowerInvariant();
            var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words).TrimEnd('.');
        }
    }
}
