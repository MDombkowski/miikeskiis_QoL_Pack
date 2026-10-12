namespace ReserveStock
{
    /// <summary>What he's told, in the middle of the screen, as the game's own chest messages are.</summary>
    internal static class Messages
    {
        internal static void Center(string text) => QoLMods.Kit.Say.Center(text);   // fitted to the screen (card B14)
    }
}
