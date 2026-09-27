using System;
using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// The question to the server, and its answers. It is the question a vegvisir asks ("where are the places named
    /// X?"), which every unmodded server answers with one message per place, built or not yet. The question carries this
    /// mod's tag where a vegvisir puts the pin's name, and the server sends that name back with each answer, so
    /// ReplyCatchPatch knows the answers as this mod's and keeps them from the game. A trader's places are never written
    /// to the log or anywhere else: they stay in memory until the world is left.
    /// </summary>
    internal static class ServerQuestion
    {
        /// <summary>The start of every pin name this mod sends. The rest is "&lt;question&gt;|&lt;trader&gt;".</summary>
        private const string Tag = "TraderCircles|";

        private static readonly System.Random random = new System.Random();

        // A new number for each world played, so an answer to a question asked in an earlier world is caught and dropped.
        private static int question;

        private static bool warnedNoCatch;

        /// <summary>Whether a pin name is this mod's: then the answer is caught, whatever the mod's state.</summary>
        internal static bool IsOurs(string pinName) => pinName != null && pinName.StartsWith(Tag, StringComparison.Ordinal);

        /// <summary>A new world is being played: answers to earlier questions no longer count.</summary>
        internal static void NewWorld()
        {
            question = random.Next(1, int.MaxValue);
        }

        /// <summary>
        /// Asks the server where a trader could be, only while the mod is on and its catch sits in front of the game's
        /// handler for the answers (checked right now, every time). Returns false, and asks nothing, otherwise. On a
        /// server or in single player the answers arrive before this returns.
        /// </summary>
        internal static bool Ask(Trader trader)
        {
            if (!Module.IsOn || ZRoutedRpc.instance == null)
            {
                return false;
            }
            if (!GameHooks.CatchInPlace())
            {
                if (!warnedNoCatch)
                {
                    warnedNoCatch = true;
                    Module.Log.LogWarning("The catch for the server's answers is not in place, so the server is not asked anything: " +
                                          "without it every answer would become a pin on your map. No circles are drawn.");
                }
                return false;
            }
            trader.Asked = true;
            trader.AskedAt = Time.unscaledTime;
            string pinName = $"{Tag}{question}|{trader.Index}";
            // The server's handler takes (name, point, pin name, pin type, show map, discover all). "Discover all" asks
            // for every place of that name, so the point doesn't count; "show map" is off.
            ZRoutedRpc.instance.InvokeRoutedRPC(GameHooks.AskRpc, trader.LocationName, Vector3.zero, pinName, (int)Minimap.PinType.None, false, true);
            Module.Log.LogInfo($"Asked the server where {trader.DisplayName} could be.");
            return true;
        }

        /// <summary>One answer: one place the trader could be. Kept if it answers this world's question.</summary>
        internal static void Receive(string pinName, Vector3 place)
        {
            string[] parts = pinName.Substring(Tag.Length).Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int askedIn) || askedIn != question ||
                !int.TryParse(parts[1], out int index) || index < 0 || index >= Trader.All.Length)
            {
                return;
            }
            Trader trader = Trader.All[index];
            if (trader.Asked && !trader.Found)
            {
                trader.AddSpot(place);
            }
        }
    }
}
