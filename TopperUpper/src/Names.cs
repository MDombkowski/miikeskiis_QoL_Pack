namespace TopperUpper
{
    /// <summary>Names as the game shows them, in his language: an item's, and a light's or station's.</summary>
    internal static class Names
    {
        /// <summary>An item's name from its shared name ("$item_resin" as "Resin").</summary>
        internal static string Item(string sharedName) =>
            string.IsNullOrEmpty(sharedName) ? string.Empty : Localization.instance != null ? Localization.instance.Localize(sharedName) : sharedName;

        /// <summary>A light's or station's name as its hover text shows it ("Sconce", "Blast furnace").</summary>
        internal static string Of(Fed fed)
        {
            if (fed == null || fed.Component == null)
            {
                return "station";
            }
            string token;
            switch (fed.Component)
            {
                case Fireplace fire:
                    token = fire.m_name;
                    break;
                case Smelter smelter:
                    token = smelter.m_name;
                    break;
                case Fermenter fermenter:
                    token = fermenter.m_name;
                    break;
                case ShieldGenerator shield:
                    token = shield.m_name;
                    break;
                case CookingStation cooking:
                    token = cooking.m_name;
                    break;
                case Turret turret:
                    token = turret.m_name;
                    break;
                case Beehive hive:
                    token = hive.m_name;
                    break;
                case SapCollector sap:
                    token = sap.m_name;
                    break;
                default:
                    token = fed.Prefab;
                    break;
            }
            return Item(token);
        }
    }
}
