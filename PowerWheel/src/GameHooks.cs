using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Valheim.UI;

namespace PowerWheel
{
    /// <summary>
    /// The game methods this mod hooks, the private members it uses, and the public parts of the game's radial menu it
    /// leans on, looked up once at startup with plain reflection (so a check outside the game can run it too). Checked
    /// against the decompiled game 1.0.17 (2026-10-09): Player.Update reads the "GP" button (and the gamepad's "JoyGP") at
    /// the press, behind Player.TakeInput; the radial menu (Valheim.UI.RadialBase, new in 1.0) opens a page through
    /// OpenRadialConfig, lays it out with ConstructRadial and reads its input through public functions it sets at the
    /// root's opening. A part the wheel can't do without, missing, keeps the mod off and F the game's (MODULES.md rule
    /// 8); a missing nicety only loses itself.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;

        /// <summary>The game's name for its "Forsaken power" button (ZInput's button list; F by default, rebindable).</summary>
        internal const string PowerButton = "GP";

        /// <summary>The gamepad's power button (D-pad down in the classic layout).</summary>
        internal const string PadPowerButton = "JoyGP";

        /// <summary>The gamepad's Alt keys, which the game requires not held for its power button.</summary>
        internal const string PadAltKeys = "JoyAltKeys";

        private static Func<Player, bool> takeInput;
        private static Action<RadialMenuElement, string> setSubTitle;
        private static FieldInfo radialInfo;
        private static FieldInfo infoSubTitle;
        private static Type initializerType;
        private static FieldInfo initializerData;
        private static Action<RadialDataSO> initRadialData;

        /// <summary>Player.Update(): where the game reads the power key.</summary>
        internal static MethodInfo PlayerUpdate { get; private set; }

        /// <summary>RadialBase.ShowThrowHint's getter: the drop hints under the wheel, which mean nothing on the power page.</summary>
        internal static MethodInfo ShowThrowHint { get; private set; }

        /// <summary>BossStone.SetActivated(bool active, bool triggerEffect): a boss stone's check of its trophy, at its start
        /// and every second after (card B06); null when it or the parts Trophies reads beside it weren't found.</summary>
        internal static MethodInfo BossStoneSetActivated { get; private set; }

        /// <summary>Everything Trophies reads was found at start.</summary>
        internal static bool TrophiesKnown { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            PlayerUpdate = typeof(Player).GetMethod("Update", AnyInstance, null, Type.EmptyTypes, null);
            Need(missing, PlayerUpdate != null, "Player.Update");

            MethodInfo takes = typeof(Player).GetMethod("TakeInput", AnyInstance, null, Type.EmptyTypes, null);
            if (takes == null || takes.ReturnType != typeof(bool))
            {
                missing.Add("Player.TakeInput");
            }
            else
            {
                takeInput = (Func<Player, bool>)Delegate.CreateDelegate(typeof(Func<Player, bool>), takes);
            }

            // The power: picking, using, reading.
            Need(missing, Method(typeof(Player), "SetGuardianPower", PublicInstance, typeof(string)), "Player.SetGuardianPower");
            Need(missing, Method(typeof(Player), "StartGuardianPower", PublicInstance), "Player.StartGuardianPower");
            Need(missing, Method(typeof(Player), "GetGuardianPowerName", PublicInstance), "Player.GetGuardianPowerName");
            Need(missing, Method(typeof(Player), "HaveUniqueKey", PublicInstance, typeof(string)), "Player.HaveUniqueKey");
            Need(missing, Method(typeof(Player), "InPlaceMode", PublicInstance), "Player.InPlaceMode");
            Need(missing, Field(typeof(Player), "m_guardianPowerCooldown", typeof(float), PublicInstance), "Player.m_guardianPowerCooldown");
            Need(missing, Field(typeof(ObjectDB), "m_StatusEffects", typeof(List<StatusEffect>), PublicInstance), "ObjectDB.m_StatusEffects");

            // The keys.
            Need(missing, Method(typeof(ZInput), "ResetButtonStatus", PublicStatic, typeof(string)), "ZInput.ResetButtonStatus");
            Need(missing, Method(typeof(ZInput), "GetButtonDef", PublicInstance, typeof(string)), "ZInput.GetButtonDef");
            Need(missing, Property(typeof(ZInput.ButtonDef), "ButtonAction", typeof(InputAction), PublicInstance, set: false), "ZInput.ButtonDef.ButtonAction");

            // The game's radial menu.
            Need(missing, Field(typeof(Hud), "m_radialMenu", typeof(RadialBase), PublicInstance), "Hud.m_radialMenu");
            Need(missing, Method(typeof(Hud), "InRadial", PublicStatic), "Hud.InRadial");
            Need(missing, Method(typeof(Hud), "IsPieceSelectionVisible", PublicStatic), "Hud.IsPieceSelectionVisible");
            Need(missing, Field(typeof(RadialData), "SO", typeof(RadialDataSO), PublicStatic), "RadialData.SO");
            Need(missing, Field(typeof(RadialDataSO), "GroupElement", typeof(GroupElement), PublicInstance), "RadialDataSO.GroupElement");
            Need(missing, Field(typeof(RadialDataSO), "EmptyElement", typeof(EmptyElement), PublicInstance), "RadialDataSO.EmptyElement");
            Need(missing, Field(typeof(RadialDataSO), "MaxElementsRange", typeof(int[]), PublicInstance), "RadialDataSO.MaxElementsRange");
            Need(missing, Field(typeof(RadialDataSO), "ElementsDistance", typeof(float), PublicInstance), "RadialDataSO.ElementsDistance");
            Need(missing, !typeof(OpenRadialConfig).IsSealed && typeof(IRadialConfig).IsAssignableFrom(typeof(OpenRadialConfig)) &&
                          typeof(ScriptableObject).IsAssignableFrom(typeof(OpenRadialConfig)), "OpenRadialConfig");
            Need(missing, Method(typeof(RadialBase), "Open", PublicInstance, typeof(IRadialConfig), typeof(IRadialConfig)), "RadialBase.Open");
            Need(missing, Method(typeof(RadialBase), "ConstructRadial", PublicInstance, typeof(List<RadialMenuElement>)), "RadialBase.ConstructRadial");
            Need(missing, Method(typeof(RadialBase), "QueuedClose", PublicInstance), "RadialBase.QueuedClose");
            Need(missing, Property(typeof(RadialBase), "CanOpen", typeof(bool), PublicInstance, set: true), "RadialBase.CanOpen");
            Need(missing, Property(typeof(RadialBase), "StartItemIndex", typeof(int), PublicInstance, set: true), "RadialBase.StartItemIndex");
            Need(missing, Property(typeof(RadialBase), "MaxElementsPerLayer", typeof(int), PublicInstance, set: false), "RadialBase.MaxElementsPerLayer");
            Need(missing, Property(typeof(RadialBase), "Selected", typeof(RadialMenuElement), PublicInstance, set: false), "RadialBase.Selected");
            Need(missing, Property(typeof(RadialBase), "CurrentConfig", typeof(IRadialConfig), PublicInstance, set: false), "RadialBase.CurrentConfig");
            Need(missing, Property(typeof(RadialBase), "InfoPosition", typeof(Vector2), PublicInstance, set: false), "RadialBase.InfoPosition");
            Need(missing, Property(typeof(RadialBase), "ShouldAnimateIn", typeof(bool), PublicInstance, set: true, get: false), "RadialBase.ShouldAnimateIn");
            Need(missing, Property(typeof(RadialBase), "OnInteractionDelay", typeof(Action<float>), PublicInstance, set: true), "RadialBase.OnInteractionDelay");
            foreach (string input in new[] { "GetReleaseToUse", "GetConfirm", "GetBack" })
            {
                Need(missing, Property(typeof(RadialBase), input, typeof(Func<bool>), PublicInstance, set: true), "RadialBase." + input);
            }
            Need(missing, Property(typeof(RadialBase), "GetMouseDirection", typeof(Func<Vector2>), PublicInstance, set: true), "RadialBase.GetMouseDirection");
            Need(missing, Method(typeof(GroupElement), "Init", PublicInstance, typeof(IRadialConfig), typeof(IRadialConfig), typeof(RadialBase)), "GroupElement.Init");
            Need(missing, Method(typeof(EmptyElement), "Init", PublicInstance), "EmptyElement.Init");
            Need(missing, Property(typeof(RadialMenuElement), "Interact", typeof(Func<bool>), PublicInstance, set: true), "RadialMenuElement.Interact");
            Need(missing, Property(typeof(RadialMenuElement), "CloseOnInteract", typeof(Func<bool>), PublicInstance, set: true), "RadialMenuElement.CloseOnInteract");
            Need(missing, Property(typeof(RadialMenuElement), "Activated", typeof(float), PublicInstance, set: true), "RadialMenuElement.Activated");
            Need(missing, Property(typeof(RadialMenuElement), "Icon", typeof(UnityEngine.UI.Image), PublicInstance, set: false), "RadialMenuElement.Icon");
            Need(missing, Property(typeof(RadialMenuElement), "ElementTransform", typeof(RectTransform), PublicInstance, set: false), "RadialMenuElement.ElementTransform");
            Need(missing, Method(typeof(PlayerController), "SetTakeInputDelay", PublicStatic, typeof(float)), "PlayerController.SetTakeInputDelay");

            // Niceties.
            PropertyInfo subTitle = typeof(RadialMenuElement).GetProperty("SubTitle", PublicInstance);
            MethodInfo subTitleSetter = subTitle?.PropertyType == typeof(string) ? subTitle.GetSetMethod(true) : null;
            if (subTitleSetter == null)
            {
                missingOptional.Add("RadialMenuElement.SubTitle's setter (the wheel's centre shows no cooldown and no \"Locked\")");
            }
            else
            {
                setSubTitle = (Action<RadialMenuElement, string>)Delegate.CreateDelegate(typeof(Action<RadialMenuElement, string>), subTitleSetter);
            }

            radialInfo = typeof(RadialBase).GetField("m_elementInfo", AnyInstance);
            infoSubTitle = typeof(ElementInfo).GetField("m_subTitle", AnyInstance);
            if (radialInfo == null || radialInfo.FieldType != typeof(ElementInfo) || infoSubTitle == null ||
                !typeof(TMP_Text).IsAssignableFrom(infoSubTitle.FieldType))
            {
                radialInfo = infoSubTitle = null;
                missingOptional.Add("RadialBase.m_elementInfo or ElementInfo.m_subTitle (the cooldown in the wheel's centre doesn't " +
                                    "count down while the pointer rests on one power)");
            }

            // The wheel's data before the game's own wheel was first opened (WheelData): the call the game's initializer
            // makes in its Awake, and the data it holds. Without the call the power wheel opens only once G has opened the
            // game's wheel; without the initializer's field it still finds the loaded data itself.
            MethodInfo init = typeof(RadialData).GetMethod("Init", PublicStatic, null, new[] { typeof(RadialDataSO) }, null);
            initRadialData = init != null ? (Action<RadialDataSO>)Delegate.CreateDelegate(typeof(Action<RadialDataSO>), init) : null;
            initializerType = typeof(RadialData).Assembly.GetType("Valheim.UI.RadialDataInitializer");
            initializerData = initializerType != null && typeof(Component).IsAssignableFrom(initializerType)
                ? initializerType.GetField("_dataObject", AnyInstance)
                : null;
            if (initializerData != null && initializerData.FieldType != typeof(RadialDataSO))
            {
                initializerData = null;
            }
            if (initRadialData == null)
            {
                missingOptional.Add("RadialData.Init (the power wheel opens only once the game's own wheel, G, has been opened since the " +
                                    "game started)");
            }
            else if (initializerData == null)
            {
                missingOptional.Add("RadialDataInitializer._dataObject (before the game's own wheel has opened, the power wheel finds " +
                                    "the wheel's data among the game's loaded objects instead)");
            }

            // A boss trophy on its stone (card B06, Trophies): the stone's check, the stand that names its power, the world's
            // id and the character's own data. Without them "Its trophy is on its stone" counts only powers taken there.
            BossStoneSetActivated = typeof(BossStone).GetMethod("SetActivated", AnyInstance, null, new[] { typeof(bool), typeof(bool) }, null);
            TrophiesKnown = BossStoneSetActivated != null &&
                            Field(typeof(BossStone), "m_itemStand", typeof(ItemStand), PublicInstance) &&
                            Field(typeof(ItemStand), "m_guardianPower", typeof(StatusEffect), PublicInstance) &&
                            Field(typeof(Player), "m_customData", typeof(Dictionary<string, string>), PublicInstance) &&
                            typeof(ZNet).GetMethod("GetWorld", PublicInstance, null, Type.EmptyTypes, null)?.ReturnType == typeof(World) &&
                            typeof(ZNet).GetProperty("instance", PublicStatic)?.PropertyType == typeof(ZNet) &&
                            Field(typeof(World), "m_uid", typeof(long), PublicInstance);
            if (!TrophiesKnown)
            {
                BossStoneSetActivated = null;
                missingOptional.Add("BossStone.SetActivated or the parts beside it (\"What counts as earned\" at TrophyAtStone counts " +
                                    "only the powers taken at their stones)");
            }

            PropertyInfo throwHint = typeof(RadialBase).GetProperty("ShowThrowHint", PublicInstance);
            ShowThrowHint = throwHint?.PropertyType == typeof(bool) ? throwHint.GetGetMethod() : null;
            if (ShowThrowHint == null)
            {
                missingOptional.Add("RadialBase.ShowThrowHint (the game's drop hints may show under the power wheel)");
            }
        }

        /// <summary>The call the game's initializer makes in its Awake was found at start.</summary>
        internal static bool CanMakeRadialData => initRadialData != null;

        /// <summary>The game's own RadialData.Init: what its RadialDataInitializer's Awake calls.</summary>
        internal static void InitRadialData(RadialDataSO data) => initRadialData(data);

        /// <summary>The wheel data the game's RadialDataInitializer holds, wherever it sits: under the HUD first (awake or
        /// not), then anywhere the game has loaded one; last, the loaded data itself. Null when none is found.</summary>
        internal static RadialDataSO FindInitializerData()
        {
            if (initializerData != null && Hud.instance != null)
            {
                foreach (Component holder in Hud.instance.GetComponentsInChildren(initializerType, true))
                {
                    if (initializerData.GetValue(holder) is RadialDataSO data && data != null)
                    {
                        return data;
                    }
                }
            }
            if (initializerData != null)
            {
                foreach (UnityEngine.Object holder in Resources.FindObjectsOfTypeAll(initializerType))
                {
                    if (holder != null && initializerData.GetValue(holder) is RadialDataSO data && data != null)
                    {
                        return data;
                    }
                }
            }
            // The data itself, loaded with the HUD that refers to it (the game has one), should an initializer hold none.
            foreach (RadialDataSO data in Resources.FindObjectsOfTypeAll<RadialDataSO>())
            {
                if (data != null)
                {
                    return data;
                }
            }
            return null;
        }

        /// <summary>Whether the game takes the player's input now: false in chat, the console, the inventory, the map,
        /// the menu and the like, and while dead, as the game itself decides before reading the power key.</summary>
        internal static bool TakesInput(Player player) => takeInput(player);

        /// <summary>Sets the line under an element's name in the wheel's centre (a protected setter); false when the
        /// game's member wasn't found.</summary>
        internal static bool SetSubTitle(RadialMenuElement element, string text)
        {
            if (setSubTitle == null)
            {
                return false;
            }
            setSubTitle(element, text);
            return true;
        }

        /// <summary>The text under the title in the wheel's centre, or null.</summary>
        internal static TMP_Text CentreSubTitle(RadialBase radial)
        {
            if (radialInfo == null || radial == null)
            {
                return null;
            }
            object info = radialInfo.GetValue(radial);
            return info == null ? null : infoSubTitle.GetValue(info) as TMP_Text;
        }

        private static void Need(List<string> missing, bool found, string name)
        {
            if (!found)
            {
                missing.Add(name);
            }
        }

        private static bool Method(Type type, string name, BindingFlags flags, params Type[] parameters) =>
            type.GetMethod(name, flags, null, parameters, null) != null;

        private static bool Field(Type type, string name, Type fieldType, BindingFlags flags)
        {
            FieldInfo field = type.GetField(name, flags);
            return field != null && field.FieldType == fieldType;
        }

        private static bool Property(Type type, string name, Type propertyType, BindingFlags flags, bool set, bool get = true)
        {
            PropertyInfo property = type.GetProperty(name, flags);
            return property != null && property.PropertyType == propertyType &&
                   (!get || property.GetGetMethod() != null) && (!set || property.GetSetMethod() != null);
        }
    }
}
