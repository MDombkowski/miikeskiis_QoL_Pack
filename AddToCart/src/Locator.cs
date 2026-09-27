using System.Collections.Generic;
using Jotunn.Managers;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>
    /// "Find my chest" for the Take tab: a label over every chest in range holding the item (its name, how many, how far),
    /// seen through walls and hills, since it is drawn on the screen rather than in the world, on the canvas Jötunn keeps
    /// behind the game's own windows. Only he sees it. A chest's label goes once he opens that chest (or, with the setting
    /// "Locate: opening one chest clears every label", every label goes); all go with Stop, or once none is left. The
    /// labels follow the chests' contents about once a second, from this computer's copies.
    /// </summary>
    internal static class Locator
    {
        private const float Above = 1.6f;
        private const float RefreshEvery = 1f;

        private sealed class Marker
        {
            internal Container Chest;
            internal Text Label;
            internal int Count;
        }

        private static readonly List<Marker> Markers = new List<Marker>();
        private static string itemKey;
        private static string itemName;
        private static GameObject layer;
        private static Text hint;
        private static float nextRefresh;

        /// <summary>True while labels are showing.</summary>
        internal static bool Active => itemKey != null;

        /// <summary>What is being looked for, as he sees it.</summary>
        internal static string ItemName => itemName;

        /// <summary>
        /// Shows a label over every chest within range (reserved ones too: pointing at a chest takes nothing) that holds
        /// the item. Returns how many chests hold it; 0 starts nothing.
        /// </summary>
        internal static int Start(string key, string name)
        {
            Stop();
            Player player = Player.m_localPlayer;
            if (player == null || GUIManager.CustomGUIBack == null)
            {
                return 0;
            }
            layer = new GameObject("AddToCart_Locate", typeof(RectTransform));
            layer.transform.SetParent(GUIManager.CustomGUIBack.transform, false);
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            hint = Label(layer.transform, 22, TextAnchor.UpperCenter);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -90f);
            hintRect.sizeDelta = new Vector2(900f, 60f);

            itemKey = key;
            itemName = name;
            foreach (Container chest in ChestFinder.Find(player.transform.position, Settings.Rules(), withReserved: true))
            {
                int count = ChestStock.Count(chest, key, leaveOne: false);
                if (count > 0)
                {
                    Text label = Label(layer.transform, 18, TextAnchor.LowerCenter);
                    ((RectTransform)label.transform).sizeDelta = new Vector2(320f, 50f);
                    Markers.Add(new Marker { Chest = chest, Label = label, Count = count });
                }
            }
            if (Markers.Count == 0)
            {
                Stop();
                return 0;
            }
            nextRefresh = Time.time + RefreshEvery;
            return Markers.Count;
        }

        /// <summary>Takes every label away. Also the module's undo step.</summary>
        internal static void Stop()
        {
            Markers.Clear();
            if (layer != null)
            {
                Object.Destroy(layer);
            }
            layer = null;
            hint = null;
            itemKey = null;
            itemName = null;
        }

        /// <summary>After every frame, from Module.LateUpdate: puts each label over its chest as the camera now sees it.</summary>
        internal static void LateUpdate()
        {
            if (!Active)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            Camera camera = Utils.GetMainCamera();
            if (!Module.IsOn || player == null || layer == null || camera == null)
            {
                Stop();
                return;
            }
            bool refresh = Time.time >= nextRefresh;
            if (refresh)
            {
                nextRefresh = Time.time + RefreshEvery;
            }
            for (int i = Markers.Count - 1; i >= 0; i--)
            {
                Marker marker = Markers[i];
                bool opened = marker.Chest != null && ChestHooks.OpenOnMyScreen(marker.Chest);
                if (opened && Settings.LocateClearsAll.Value)
                {
                    // His setting (off by default): finding the item in one chest ends the search.
                    Stop();
                    return;
                }
                // Gone: unloaded or destroyed, opened by him (found), or emptied of the item meanwhile.
                bool gone = marker.Chest == null || opened;
                if (!gone && refresh)
                {
                    marker.Count = ChestStock.Count(marker.Chest, itemKey, leaveOne: false);
                    gone = marker.Count <= 0;
                }
                if (gone)
                {
                    if (marker.Label != null)
                    {
                        Object.Destroy(marker.Label.gameObject);
                    }
                    Markers.RemoveAt(i);
                    continue;
                }
                Place(marker, camera, player);
            }
            if (Markers.Count == 0)
            {
                Stop();
                return;
            }
            hint.text = $"Looking for {itemName}: {Markers.Count} {(Markers.Count == 1 ? "chest" : "chests")}. " +
                        (Settings.LocateClearsAll.Value ? "Opening any of them clears every label" : "Open a chest to clear its label") +
                        "; Stop, in the Add to Cart window, clears them all.";
        }

        private static void Place(Marker marker, Camera camera, Player player)
        {
            Vector3 at = marker.Chest.transform.position + Vector3.up * Above;
            // Scaled as the game places its own labels over the world (EnemyHud, DamageText): with a render scale below
            // 100%, the camera draws fewer pixels than the screen has.
            Vector3 screen = camera.WorldToScreenPointScaled(at);
            bool seen = screen.z > 0f;
            marker.Label.gameObject.SetActive(seen);
            if (!seen)
            {
                return;   // behind him: he turns round to see it
            }
            var canvas = (RectTransform)layer.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            var rect = (RectTransform)marker.Label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = local;
            float metres = Vector3.Distance(player.transform.position, marker.Chest.transform.position);
            marker.Label.text = $"{itemName} ×{marker.Count}\n{metres:0} m";
        }

        private static Text Label(Transform parent, int size, TextAnchor align)
        {
            GUIManager gui = GUIManager.Instance;
            GameObject made = gui.CreateText(string.Empty, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                             gui.AveriaSerifBold, size, gui.ValheimOrange, true, Color.black, 320f, 50f, false);
            Text text = made.GetComponent<Text>();
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
