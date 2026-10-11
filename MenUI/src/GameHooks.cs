using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MenUI
{
    /// <summary>
    /// The game's members this mod reads, looked up once at startup. Checked against the game's 1.0.17 code:
    /// FejdStartup (the main menu: its public m_menuList, whose MenuEntries child holds the buttons other mods copy
    /// theirs into, and its private m_menuButtons, the arrow keys' order, gathered once in Awake) and Menu (the in-game
    /// menu: its public m_menuDialog).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo menuList;
        private static FieldInfo menuButtons;
        private static FieldInfo menuDialog;
        private static FieldInfo menuEntries;

        internal static bool CanSetArrowKeys => menuButtons != null;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            menuList = typeof(FejdStartup).GetField("m_menuList", AnyInstance);
            if (menuList == null || menuList.FieldType != typeof(GameObject))
            {
                menuList = null;
                missing.Add("FejdStartup.m_menuList");
            }

            menuButtons = typeof(FejdStartup).GetField("m_menuButtons", AnyInstance);
            if (menuButtons == null || menuButtons.FieldType != typeof(Button[]))
            {
                menuButtons = null;
                missingOptional.Add("FejdStartup.m_menuButtons (the arrow keys then reach only the game's own buttons)");
            }

            menuDialog = typeof(Menu).GetField("m_menuDialog", AnyInstance);
            if (menuDialog == null || menuDialog.FieldType != typeof(Transform))
            {
                menuDialog = null;
                missingOptional.Add("Menu.m_menuDialog (the in-game menu is then left as it is)");
            }

            menuEntries = typeof(Menu).GetField("menuEntriesParent", AnyInstance);
            if (menuEntries == null || menuEntries.FieldType != typeof(RectTransform))
            {
                menuEntries = null;
            }
        }

        /// <summary>The main menu's list while it shows, else null.</summary>
        internal static RectTransform MainMenuList()
        {
            FejdStartup startup = FejdStartup.instance;
            if (startup == null || menuList == null)
            {
                return null;
            }
            GameObject list = menuList.GetValue(startup) as GameObject;
            return list != null && list.activeInHierarchy ? list.transform as RectTransform : null;
        }

        /// <summary>The in-game menu's dialog while it shows, else null.</summary>
        internal static RectTransform InGameMenu()
        {
            Menu menu = Menu.instance;
            if (menu == null || menuDialog == null)
            {
                return null;
            }
            Transform dialog = menuDialog.GetValue(menu) as Transform;
            return dialog != null && dialog.gameObject.activeInHierarchy ? dialog as RectTransform : null;
        }

        /// <summary>Where the in-game menu's buttons are (its public menuEntriesParent, else its "MenuEntries" child, as
        /// ConfigurationManager finds it), so a button elsewhere in the dialog isn't measured; the dialog itself if
        /// neither is there.</summary>
        internal static RectTransform InGameEntries(RectTransform dialog)
        {
            if (dialog == null)
            {
                return null;
            }
            Menu menu = Menu.instance;
            RectTransform entries = menu != null && menuEntries != null ? menuEntries.GetValue(menu) as RectTransform : null;
            if (entries != null && entries.IsChildOf(dialog))
            {
                return entries;
            }
            return dialog.Find("MenuEntries") as RectTransform ?? dialog;
        }

        internal static Button[] ArrowKeyButtons()
        {
            FejdStartup startup = FejdStartup.instance;
            return startup != null && menuButtons != null ? menuButtons.GetValue(startup) as Button[] : null;
        }

        internal static void SetArrowKeyButtons(Button[] buttons)
        {
            FejdStartup startup = FejdStartup.instance;
            if (startup != null && menuButtons != null)
            {
                menuButtons.SetValue(startup, buttons);
            }
        }
    }
}
