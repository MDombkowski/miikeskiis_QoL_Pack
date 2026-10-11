using System;
using System.Collections.Generic;
using System.Reflection;

namespace CraftFromChests
{
    /// <summary>
    /// The game's private members the mod reads, looked up once at start by name (checked against the game of
    /// 2026-09-25, Valheim 1.0.16). The crafting menu's are needed for crafting from chests; without them the whole mod
    /// stays off with a line saying which were missing. Each kind of station's are needed only for that station, so a
    /// renamed member switches off only filling that one kind, with a line in the log.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Player.m_knownStations, for the build menu's "have some" of a piece that needs a station; without it
        /// such a piece is shown as the game shows it.</summary>
        internal static FieldInfo KnownStations;

        // The crafting menu (InventoryGui)
        internal static FieldInfo CraftTimer;
        internal static FieldInfo CraftRecipe;
        internal static FieldInfo CraftUpgradeItem;
        internal static FieldInfo MultiCrafting;

        // Each station's network view, and what it knows about itself
        internal static FieldInfo SmelterView;
        internal static MethodInfo SmelterQueueSize;
        internal static MethodInfo SmelterFuel;
        internal static MethodInfo SmelterAllows;
        internal static FieldInfo FireplaceView;
        internal static FieldInfo CookingView;
        internal static MethodInfo CookingFireLit;
        internal static MethodInfo CookingAllows;
        internal static MethodInfo CookingHasDoneItem;
        internal static MethodInfo CookingFuel;
        internal static FieldInfo FermenterView;
        internal static MethodInfo FermenterStatus;
        internal static FieldInfo FermenterRoof;
        internal static FieldInfo FermenterExposed;
        internal static MethodInfo FermenterUpdateCover;
        internal static FieldInfo ShieldView;
        internal static MethodInfo ShieldFuel;
        internal static FieldInfo TurretView;
        internal static MethodInfo TurretAllows;

        /// <summary>Which kinds of station can be filled from chests: those whose members were all found.</summary>
        internal static bool Smelters, Fireplaces, CookingStations, Fermenters, ShieldGenerators, Turrets;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            var needed = new List<string>();
            CraftTimer = Field(typeof(InventoryGui), "m_craftTimer", typeof(float), needed);
            CraftRecipe = Field(typeof(InventoryGui), "m_craftRecipe", typeof(Recipe), needed);
            CraftUpgradeItem = Field(typeof(InventoryGui), "m_craftUpgradeItem", typeof(ItemDrop.ItemData), needed);
            MultiCrafting = Field(typeof(InventoryGui), "m_multiCrafting", typeof(bool), needed);
            missing.AddRange(needed);

            var optional = new List<string>();
            KnownStations = Field(typeof(Player), "m_knownStations", typeof(Dictionary<string, int>), optional);
            if (optional.Count > 0)
            {
                missingOptional.Add($"showing pieces that need a station in the build menu's \"have some\" list ({string.Join(", ", optional)} not found)");
            }

            Smelters = Station("smelters, kilns and the like", missingOptional, found =>
            {
                SmelterView = Field(typeof(Smelter), "m_nview", typeof(ZNetView), found);
                SmelterQueueSize = Method(typeof(Smelter), "GetQueueSize", typeof(int), Type.EmptyTypes, found);
                SmelterFuel = Method(typeof(Smelter), "GetFuel", typeof(float), Type.EmptyTypes, found);
                SmelterAllows = Method(typeof(Smelter), "IsItemAllowed", typeof(bool), new[] { typeof(string) }, found);
            });
            Fireplaces = Station("fires and torches", missingOptional, found =>
            {
                FireplaceView = Field(typeof(Fireplace), "m_nview", typeof(ZNetView), found);
            });
            CookingStations = Station("cooking stations and the oven", missingOptional, found =>
            {
                CookingView = Field(typeof(CookingStation), "m_nview", typeof(ZNetView), found);
                CookingFireLit = Method(typeof(CookingStation), "IsFireLit", typeof(bool), Type.EmptyTypes, found);
                CookingAllows = Method(typeof(CookingStation), "IsItemAllowed", typeof(bool), new[] { typeof(string) }, found);
                CookingHasDoneItem = Method(typeof(CookingStation), "HaveDoneItem", typeof(bool), Type.EmptyTypes, found);
                CookingFuel = Method(typeof(CookingStation), "GetFuel", typeof(float), Type.EmptyTypes, found);
            });
            Fermenters = Station("the fermenter", missingOptional, found =>
            {
                FermenterView = Field(typeof(Fermenter), "m_nview", typeof(ZNetView), found);
                FermenterStatus = Method(typeof(Fermenter), "GetStatus", null, Type.EmptyTypes, found);
                FermenterRoof = Field(typeof(Fermenter), "m_hasRoof", typeof(bool), found);
                FermenterExposed = Field(typeof(Fermenter), "m_exposed", typeof(bool), found);
                FermenterUpdateCover = Method(typeof(Fermenter), "UpdateCover", typeof(void), new[] { typeof(float), typeof(bool) }, found);
            });
            ShieldGenerators = Station("the shield generator", missingOptional, found =>
            {
                ShieldView = Field(typeof(ShieldGenerator), "m_nview", typeof(ZNetView), found);
                ShieldFuel = Method(typeof(ShieldGenerator), "GetFuel", typeof(float), Type.EmptyTypes, found);
            });
            Turrets = Station("the ballista", missingOptional, found =>
            {
                TurretView = Field(typeof(Turret), "m_nview", typeof(ZNetView), found);
                TurretAllows = Method(typeof(Turret), "IsItemAllowed", typeof(bool), new[] { typeof(string) }, found);
            });
        }

        // Looks up one kind of station's members; if any is missing, filling that kind stays off and the log says so.
        private static bool Station(string what, List<string> missingOptional, Action<List<string>> lookUp)
        {
            var notFound = new List<string>();
            lookUp(notFound);
            if (notFound.Count == 0)
            {
                return true;
            }
            missingOptional.Add($"filling {what} from chests (the game has changed: {string.Join(", ", notFound)} not found)");
            return false;
        }

        private static FieldInfo Field(Type type, string name, Type fieldType, List<string> notFound)
        {
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field == null || field.FieldType != fieldType)
            {
                notFound.Add($"{type.Name}.{name}");
                return null;
            }
            return field;
        }

        // returnType null: any (a private enum, read by its name).
        private static MethodInfo Method(Type type, string name, Type returnType, Type[] parameters, List<string> notFound)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, parameters, null);
            if (method == null || (returnType != null && method.ReturnType != returnType))
            {
                notFound.Add($"{type.Name}.{name}");
                return null;
            }
            return method;
        }

        internal static T Read<T>(FieldInfo field, object instance) => (T)field.GetValue(instance);

        internal static T Call<T>(MethodInfo method, object instance, params object[] arguments) => (T)method.Invoke(instance, arguments);
    }
}
