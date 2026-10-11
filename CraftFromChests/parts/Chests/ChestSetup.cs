// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
namespace QoLMods.Chests
{
    /// <summary>
    /// A chest's setup (card A41, StoreStash's "Chest setups"): what the chest takes when things are quick stacked, as
    /// one line of text in the chest's own network record, beside Reserve Stock's mark. The unmodded server keeps it with
    /// the world and every player nearby receives it; a game without the pack sees an ordinary chest. The text is the
    /// catalogue's (QoLMods\parts\Catalogue\ItemPicks.cs): the chest service only keeps it, and the quick stack follows it
    /// through the reading its caller passes (StackRules.Setups), so the service needs no catalogue.
    ///
    /// Written only as the chest's owner (while its window is open, the chest is his), and sent at once. Cleared by
    /// writing an empty text, never by removing the key: the game sends a removal to nobody, so other players would keep
    /// the old setup (ZDO.RemoveString doesn't raise the record's revision, and a record that arrives only adds or
    /// overwrites keys).
    /// </summary>
    internal static class ChestSetup
    {
        /// <summary>The setup's name in the chest's record. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.qolmods.setup";

        /// <summary>The longest setup text written into a record (the catalogue's ItemPicks.MaxLength): far more than any
        /// setup needs, small beside a chest's own items, and every player near the chest receives it.</summary>
        internal const int MaxLength = 4000;

        /// <summary>The chest's setup text, or empty when it has none (or isn't loaded). A cart's or ship's chest keeps it
        /// in the vehicle's record, which is the chest's (ChestHooks.NetView).</summary>
        internal static string Read(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() ? view.GetZDO().GetString(Key, string.Empty) ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Sets a chest's setup text (empty: none) on a chest this computer owns, and sends its record at once. False,
        /// changing nothing, on a chest it doesn't own or that isn't loaded, or for a text longer than MaxLength; true (and
        /// nothing sent) when the text is already that.
        /// </summary>
        internal static bool Write(Container chest, string text)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner() || (text != null && text.Length > MaxLength))
            {
                return false;
            }
            ZDO record = view.GetZDO();
            text = text ?? string.Empty;   // a stored null would make the record's next write of the key throw
            if (record.GetString(Key, string.Empty) == text)
            {
                return true;
            }
            record.Set(Key, text);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            return true;
        }
    }
}
