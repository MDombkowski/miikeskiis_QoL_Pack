using System.Collections.Generic;
using HarmonyLib;
using QoLMods.Chests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftFromChests
{
    /// <summary>
    /// The padlock on an amount in the requirement lists (card A12, his answer 5): how many more of it the reserved chests
    /// nearby hold, beside Reserve Stock's padlock, just above the amount's right end, in the padlock's orange. Made once
    /// for each amount, as a child of it, and shown or hidden each time the game draws the amount (setting "Reserved
    /// chests": LeftOutWithLock). Hidden wherever the mod doesn't count chests.
    /// </summary>
    internal static class LockBadge
    {
        private const string ObjectName = "CraftFromChests_Reserved";

        /// <summary>The badge's size: the padlock is as tall as the badge, the number fills the rest to its left.</summary>
        internal const float Width = 48f;

        internal const float Height = 13f;

        // The amounts given a badge, to take them all away after an error.
        private static readonly HashSet<TMP_Text> Made = new HashSet<TMP_Text>();

        /// <summary>True once any badge has been made this session: until then there is nothing to hide.</summary>
        internal static bool AnyMade => Made.Count > 0;

        /// <summary>Shows how many are reserved on this amount, or hides the badge for 0.</summary>
        internal static void Show(TMP_Text amount, int reserved)
        {
            Transform badge = amount.transform.Find(ObjectName);
            if (reserved <= 0)
            {
                Hide(badge);
                return;
            }
            if (badge == null)
            {
                badge = Make(amount);
            }
            if (!badge.gameObject.activeSelf)
            {
                badge.gameObject.SetActive(true);
            }
            TMP_Text number = badge.GetComponentInChildren<TMP_Text>(true);
            string text = Display.Short(reserved);
            if (number != null && number.text != text)
            {
                number.text = text;
            }
        }

        /// <summary>Hides this amount's badge, if it has one.</summary>
        internal static void Hide(TMP_Text amount) => Hide(amount.transform.Find(ObjectName));

        /// <summary>Hides the badge of one of the game's requirement rows (its res_amount), if it has one.</summary>
        internal static void HideIn(GameObject row)
        {
            Transform amount = row != null ? row.transform.Find("res_amount") : null;
            Hide(amount != null ? amount.Find(ObjectName) : null);
        }

        /// <summary>Takes every badge away: the undo step after an error.</summary>
        internal static void RemoveAll()
        {
            foreach (TMP_Text amount in Made)
            {
                Transform badge = amount != null ? amount.transform.Find(ObjectName) : null;
                if (badge != null)
                {
                    Object.Destroy(badge.gameObject);
                }
            }
            Made.Clear();
        }

        private static void Hide(Transform badge)
        {
            if (badge != null && badge.gameObject.activeSelf)
            {
                badge.gameObject.SetActive(false);
            }
        }

        private static Transform Make(TMP_Text amount)
        {
            if (Made.Count >= 64)
            {
                Made.RemoveWhere(each => each == null);   // menus rebuilt after a world change
            }
            Made.Add(amount);

            // Made inactive, and shown by Show: its text wakes only once its font is set. TextMeshPro looks for its own
            // default font the moment it wakes, and the game has none, so a text woken without one logs "The
            // LiberationSans SDF Font Asset was not found" (his log of 0.8.0, found by 15-B).
            var badge = new GameObject(ObjectName, typeof(RectTransform));
            badge.SetActive(false);
            var rect = (RectTransform)badge.transform;
            rect.SetParent(amount.transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = new Vector2(0f, 1f);

            var lockObject = new GameObject(ObjectName + "_Lock", typeof(RectTransform), typeof(Image));
            var lockRect = (RectTransform)lockObject.transform;
            lockRect.SetParent(rect, false);
            lockRect.anchorMin = new Vector2(1f, 0.5f);
            lockRect.anchorMax = new Vector2(1f, 0.5f);
            lockRect.pivot = new Vector2(1f, 0.5f);
            lockRect.sizeDelta = new Vector2(Height, Height);
            lockRect.anchoredPosition = Vector2.zero;
            Image picture = lockObject.GetComponent<Image>();
            picture.sprite = ReserveLock.Closed;
            picture.color = ReserveLock.ReservedColor;
            picture.raycastTarget = false;
            picture.preserveAspect = true;

            var numberObject = new GameObject(ObjectName + "_Amount", typeof(RectTransform));
            var numberRect = (RectTransform)numberObject.transform;
            numberRect.SetParent(rect, false);   // inside the inactive badge first, so the text below doesn't wake yet
            numberRect.anchorMin = Vector2.zero;
            numberRect.anchorMax = Vector2.one;
            numberRect.pivot = new Vector2(1f, 0.5f);
            numberRect.offsetMin = Vector2.zero;
            numberRect.offsetMax = new Vector2(-(Height + 1f), 0f);
            TextMeshProUGUI number = numberObject.AddComponent<TextMeshProUGUI>();
            number.font = amount.font;   // the amount's own font and outline, so it reads the same over any picture
            number.fontSharedMaterial = amount.fontSharedMaterial;
            number.enableAutoSizing = true;
            number.fontSizeMin = 8f;
            number.fontSizeMax = 12f;
            number.fontSize = 12f;
            number.alignment = TextAlignmentOptions.Right;
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.overflowMode = TextOverflowModes.Overflow;
            number.color = ReserveLock.ReservedColor;
            number.raycastTarget = false;
            return rect;
        }
    }

    [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
    internal static class NoPadlockOnTheStationRow
    {
        /// <summary>
        /// The build panel shows the piece's crafting station in the row after its last material, writing that row's
        /// amount itself, without the game's SetupRequirement: a padlock left there by an earlier piece with more
        /// materials would stay. Hidden here, on every row past the materials, also while the mod is switched off.
        /// </summary>
        private static void Postfix(Hud __instance, Piece piece)
        {
            if (__instance == null || __instance.m_requirementItems == null)
            {
                return;
            }
            int used = piece != null && piece.m_resources != null ? piece.m_resources.Length : 0;
            if (!LockBadge.AnyMade || used >= __instance.m_requirementItems.Length)
            {
                return;
            }
            Module.Guard("hiding a padlock on the build panel's station row", () =>
            {
                for (int row = used; row < __instance.m_requirementItems.Length; row++)
                {
                    LockBadge.HideIn(__instance.m_requirementItems[row]);
                }
            });
        }
    }
}
