namespace CraftFromChests
{
    /// <summary>The key that switches the mod on and off (its Enabled setting), as AzuCraftyBoxes' Left Alt + O switched
    /// its pulling. The kit runs the module's Update also while it's switched off, so the key can switch it back on.</summary>
    internal static class Toggle
    {
        internal static void Update()
        {
            if (!Settings.Pressed(Settings.ToggleKey.Value) || !TakesKeys())
            {
                return;
            }
            Settings.Enabled.Value = !Settings.Enabled.Value;
            Stock.Invalidate();
            Messages.Center(Settings.Enabled.Value ? "Crafting from chests: on" : "Crafting from chests: off (only what you carry)");
        }

        // As the game decides whether a key is meant for it: a player, and no chat, console, text box or menu in the way.
        // The inventory and crafting window may be open, so he can switch it while crafting.
        private static bool TakesKeys()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !Menu.IsVisible() && (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen();
        }
    }
}
