using UnityEngine;

namespace AddToCart
{
    /// <summary>How big the window is drawn, over the game's own GUI scale (the setting "Window size").</summary>
    internal enum WindowSize
    {
        Normal,
        Large,
        ExtraLarge,
    }

    /// <summary>How a thumbnail the chests can't cover looks (the setting "Thumbnails the chests can't cover").</summary>
    internal enum UnavailableLook
    {
        GreyAndFaded,
        Grey,
        Faded,
    }

    /// <summary>
    /// The window's looks that are decisions rather than drawing: how much bigger it is drawn, and how a thumbnail the
    /// chests can't cover shows. Plain arithmetic, so the scratch harness checks it without the game.
    /// </summary>
    internal static class Looks
    {
        /// <summary>The opacity setting's range and default: 0.1, his call after 0.3.2 (it was 0.3, 0.3.0's fade). At most
        /// 0.9, so Faded never looks the same as available.</summary>
        internal const float LeastOpacity = 0.05f;

        internal const float MostOpacity = 0.9f;

        internal const float DefaultOpacity = 0.1f;

        /// <summary>
        /// How much bigger than the game's GUI scale the window is drawn: the setting's size, or as much of it as fits
        /// the screen with a margin, and never smaller than the game's own scale (a window that doesn't fit at Normal
        /// stays as it always was).
        /// </summary>
        internal static float SizeFactor(WindowSize size, float gameScale, float screenWidth, float screenHeight, float width, float height)
        {
            float wanted = size == WindowSize.ExtraLarge ? 1.5f : size == WindowSize.Large ? 1.25f : 1f;
            float fits = Mathf.Min(screenWidth * 0.98f / width, screenHeight * 0.96f / height) / Mathf.Max(0.01f, gameScale);
            return Mathf.Max(1f, Mathf.Min(wanted, fits));
        }

        /// <summary>
        /// How a thumbnail shows: grey or in colour, and how opaque. An available one is in colour and opaque. An
        /// unavailable one is grey (GreyAndFaded, Grey) and faded to the opacity setting (GreyAndFaded, Faded).
        /// <paramref name="greyReady"/> is false while its grey copy isn't made (or can't be): it then shows in colour, and
        /// faded even with Grey, so it never looks available.
        /// </summary>
        internal static void Unavailable(bool unavailable, UnavailableLook look, float opacity, bool greyReady, out bool grey, out float alpha)
        {
            float faded = Mathf.Clamp(opacity, LeastOpacity, MostOpacity);
            grey = unavailable && look != UnavailableLook.Faded && greyReady;
            alpha = !unavailable ? 1f : grey && look == UnavailableLook.Grey ? 1f : faded;
        }
    }
}
