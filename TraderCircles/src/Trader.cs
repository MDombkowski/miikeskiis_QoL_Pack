using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// One of the three traders, and what this mod knows about it in the world being played: whether the server has been
    /// asked, the places it could still be (only ever in memory, never written anywhere), their circles, and whether it
    /// has been found. The places are forgotten when the world is left.
    /// </summary>
    internal sealed class Trader
    {
        internal static readonly Trader Haldor = new Trader(0, "Haldor", "Haldor", "Vendor_BlackForest", new Color(1f, 0.88f, 0.15f));
        internal static readonly Trader Hildir = new Trader(1, "Hildir", "Hildir", "Hildir_camp", new Color(1f, 0.42f, 0.75f));
        internal static readonly Trader BogWitch = new Trader(2, "BogWitch", "the Bog Witch", "BogWitch_Camp", new Color(0.2f, 0.85f, 0.25f));

        /// <summary>Every trader, in the order the game added them, which is also the order of their settings.</summary>
        internal static readonly Trader[] All = { Haldor, Hildir, BogWitch };

        private Trader(int index, string key, string displayName, string locationName, Color defaultColour)
        {
            Index = index;
            Key = key;
            DisplayName = displayName;
            LocationName = locationName;
            DefaultColour = defaultColour;
        }

        internal int Index { get; }

        /// <summary>The start of its settings' keys. Never changes.</summary>
        internal string Key { get; }

        internal string DisplayName { get; }

        /// <summary>The name the game and the server know its camp by: the camp's asset name, exactly.</summary>
        internal string LocationName { get; }

        internal Color DefaultColour { get; }

        /// <summary>Its own on/off switch (off by default).</summary>
        internal ConfigEntry<bool> Shown { get; set; }

        internal ConfigEntry<Color> Colour { get; set; }

        // ---- What is known in the world being played. Reset by Forget. ----

        /// <summary>The server has been asked about it in this world (once is enough).</summary>
        internal bool Asked { get; set; }

        /// <summary>When it was asked (unscaled time), to notice a server that never answers.</summary>
        internal float AskedAt { get; set; }

        /// <summary>Its camp's map icon has reached this game, so someone has come near it: found, for good.</summary>
        internal bool Found { get; set; }

        /// <summary>The places the server named: every place it could still be. Kept only in memory.</summary>
        internal readonly List<Vector3> Spots = new List<Vector3>();

        /// <summary>One circle per place, at CirclesRadius; fewer while places wait to be placed.</summary>
        internal readonly List<Circle> Circles = new List<Circle>();

        /// <summary>The radius the circles were placed at (0: none yet).</summary>
        internal float CirclesRadius { get; set; }

        /// <summary>After a size change: the new size's circles, placed a few per frame, shown once all are placed.</summary>
        internal readonly List<Circle> NextCircles = new List<Circle>();

        /// <summary>The radius NextCircles are being placed at (0: none).</summary>
        internal float NextRadius { get; set; }


        /// <summary>The ground where it can appear in this world, worked out from the game's own rules when first needed.</summary>
        internal Ground Ground { get; set; }

        /// <summary>When the last place arrived (unscaled time), so the log's summary waits for the whole answer.</summary>
        internal float LastSpotAt { get; set; }

        /// <summary>The count and radius last written to the log, so each summary is written once.</summary>
        internal int LoggedCircles { get; set; } = -1;

        internal float LoggedRadius { get; set; }

        /// <summary>The radius its circles are to be drawn at: half the size setting. Each size has circles of its own.</summary>
        internal float Radius => Settings.Diameter.Value / 2f;

        internal bool WarnedNoAnswer { get; set; }

        /// <summary>Whether its circles are to be drawn now.</summary>
        internal bool ShowsCircles => Shown.Value && !Found && Circles.Count > 0;

        /// <summary>A place the server named. The same place twice is kept once.</summary>
        internal void AddSpot(Vector3 spot)
        {
            foreach (Vector3 known in Spots)
            {
                if ((known - spot).sqrMagnitude < 1f)
                {
                    return;
                }
            }
            Spots.Add(spot);
            LastSpotAt = Time.unscaledTime;
        }

        /// <summary>Forgets its places and circles (it has been found, or the world was left).</summary>
        internal void ForgetPlaces()
        {
            Spots.Clear();
            Circles.Clear();
            CirclesRadius = 0f;
            NextCircles.Clear();
            NextRadius = 0f;
        }

        /// <summary>Forgets everything about the world being played.</summary>
        internal void Forget()
        {
            ForgetPlaces();
            Asked = false;
            AskedAt = 0f;
            Found = false;
            Ground = null;
            LastSpotAt = 0f;
            LoggedCircles = -1;
            LoggedRadius = 0f;
            WarnedNoAnswer = false;
        }
    }
}
