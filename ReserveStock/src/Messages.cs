namespace ReserveStock
{
    /// <summary>What he's told, in the middle of the screen, as the game's own chest messages are.</summary>
    internal static class Messages
    {
        internal static void Center(string text)
        {
            Player player = Player.m_localPlayer;
            if (player != null)   // Unity's own null test: a destroyed player isn't null to ?.
            {
                player.Message(MessageHud.MessageType.Center, text);
            }
        }
    }
}
