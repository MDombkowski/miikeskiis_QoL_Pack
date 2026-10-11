using System;
using System.Collections.Generic;
using System.Reflection;

namespace TopperUpper
{
    /// <summary>
    /// The game's private members Topper-Upper reads, looked up once at start by name (checked against Valheim 1.0.17,
    /// decompiled fresh by session 45-B). Each kind of light or station needs only its own: a renamed member switches off
    /// only that kind, with a line in the log, never the rest of the mod.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static FieldInfo FireplaceView;
        internal static FieldInfo SmelterView;
        internal static FieldInfo FermenterView;
        internal static FieldInfo FermenterRoof;
        internal static FieldInfo FermenterExposed;
        internal static FieldInfo ShieldView;
        internal static FieldInfo CookingView;
        internal static MethodInfo CookingHasUncooked;
        internal static FieldInfo TurretView;
        internal static FieldInfo BeehiveView;
        internal static FieldInfo SapView;

        /// <summary>Smelter.QueueOre(string name, bool cheated), private: one item into a smelter's queue, as its own add
        /// message writes it (Adds.Ore). Null without it: each item then goes through the game's own message.</summary>
        internal static Action<Smelter, string, bool> QueueOre;

        /// <summary>Smelter.m_haveRoof, private, refreshed each second by the smelter itself: a station that needs a roof
        /// (the spinning wheel) isn't fed without one. Null without it: fed as the game's own add would be, roof or not.</summary>
        internal static FieldInfo SmelterHaveRoof;

        /// <summary>Turret.UpdateVisualBolt(), private: the bolt's look, refreshed after missiles go in, as the game's own
        /// add does. Null without it: the look follows at its next refresh.</summary>
        internal static MethodInfo TurretUpdateVisualBolt;

        /// <summary>Which kinds can be fed: those whose members were all found.</summary>
        internal static bool Fireplaces, Smelters, Fermenters, ShieldGenerators, CookingStations, Turrets, Beehives, SapCollectors;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            Fireplaces = Kind("lights and fires", missingOptional, found =>
            {
                FireplaceView = Field(typeof(Fireplace), "m_nview", typeof(ZNetView), found);
            });
            Smelters = Kind("smelters, kilns, the windmill, the spinning wheel, the eitr refinery and the hot tub", missingOptional, found =>
            {
                SmelterView = Field(typeof(Smelter), "m_nview", typeof(ZNetView), found);
            });
            var optional = new List<string>();
            MethodInfo queueOre = Method(typeof(Smelter), "QueueOre", typeof(void), new[] { typeof(string), typeof(bool) }, optional);
            QueueOre = queueOre != null ? (Action<Smelter, string, bool>)Delegate.CreateDelegate(typeof(Action<Smelter, string, bool>), queueOre) : null;
            if (queueOre == null)
            {
                missingOptional.Add("writing a smelter's queue straight into it (Smelter.QueueOre not found): each item goes through " +
                                    "the game's own message, with its log line");
            }
            SmelterHaveRoof = Field(typeof(Smelter), "m_haveRoof", typeof(bool), optional);
            TurretUpdateVisualBolt = Method(typeof(Turret), "UpdateVisualBolt", typeof(void), Type.EmptyTypes, optional);
            Fermenters = Kind("the fermenter", missingOptional, found =>
            {
                FermenterView = Field(typeof(Fermenter), "m_nview", typeof(ZNetView), found);
                FermenterRoof = Field(typeof(Fermenter), "m_hasRoof", typeof(bool), found);
                FermenterExposed = Field(typeof(Fermenter), "m_exposed", typeof(bool), found);
            });
            ShieldGenerators = Kind("the shield generator", missingOptional, found =>
            {
                ShieldView = Field(typeof(ShieldGenerator), "m_nview", typeof(ZNetView), found);
            });
            CookingStations = Kind("the Frost Foundry's and the stone oven's fuel", missingOptional, found =>
            {
                CookingView = Field(typeof(CookingStation), "m_nview", typeof(ZNetView), found);
                CookingHasUncooked = Method(typeof(CookingStation), "HaveUncookedItem", typeof(bool), Type.EmptyTypes, found);
            });
            Turrets = Kind("the ballista", missingOptional, found =>
            {
                TurretView = Field(typeof(Turret), "m_nview", typeof(ZNetView), found);
            });
            Beehives = Kind("beehives and birds' nests", missingOptional, found =>
            {
                BeehiveView = Field(typeof(Beehive), "m_nview", typeof(ZNetView), found);
            });
            SapCollectors = Kind("sap extractors", missingOptional, found =>
            {
                SapView = Field(typeof(SapCollector), "m_nview", typeof(ZNetView), found);
            });
        }

        // Looks up one kind's members; if any is missing, that kind stays unfed and the log says so.
        private static bool Kind(string what, List<string> missingOptional, Action<List<string>> lookUp)
        {
            var notFound = new List<string>();
            lookUp(notFound);
            if (notFound.Count == 0)
            {
                return true;
            }
            missingOptional.Add($"feeding {what} (the game has changed: {string.Join(", ", notFound)} not found)");
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

        internal static T Read<T>(FieldInfo field, object instance) => field != null && instance != null ? (T)field.GetValue(instance) : default;

        internal static T Call<T>(MethodInfo method, object instance, params object[] arguments) => (T)method.Invoke(instance, arguments);
    }
}
