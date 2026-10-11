using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The personal quick stack button beside his inventory (card A43; its setting is on out of the box since card A63, off
    /// before, MODULES.md rule 4): a SquareButton doing what the personal key does. With the quick stack button on too, the two share the space
    /// between the armour and weight tabs at 32 px each, this one below (Button); with the quick stack button off, it has
    /// that space alone, at the quick stack button's own place and size. Its picture is the quick stack's with a person in
    /// its corner (PersonalIcon). A setting of its own moves it.
    /// </summary>
    internal static class PersonalButton
    {
        /// <summary>Its middle, from the inventory panel's bottom right corner, while it shares the space: below the quick
        /// stack button, 4 px apart.</summary>
        internal static readonly Vector2 SharedPlace = new Vector2(32f, 126.75f);

        internal static readonly SquareButton Square = new SquareButton("StoreStash_PersonalButton", () => PersonalIcon.Sprite, Clicked,
            () => Settings.PersonalButton.Value, Button.Usable,
            () => (Button.Shared ? SharedPlace : Button.Place) + Settings.PersonalButtonOffset.Value,
            () => Button.Shared ? Button.SharedSize : Button.Size,
            Tip);

        /// <summary>A setting its tooltip names changed (the key, what goes in): the tooltip is written again.</summary>
        internal static void TipChanged() => Square.TipChanged();

        /// <summary>Every frame: shown while his inventory is open, the mod and the setting on.</summary>
        internal static void Update() => Square.Update();

        /// <summary>The undo step after an error: the button goes.</summary>
        internal static void Remove() => Square.Remove();

        /// <summary>Its tooltip's words, as the settings say now.</summary>
        internal static string Tip() =>
            $"Personal quick stack ({Settings.KeyText(Settings.PersonalKey.Value)}): what you carry goes into your personal chests nearby" +
            (Settings.PersonalOnlyWhereHeld ? " that already hold it" : string.Empty) + ", and nowhere else. Your favorites stay with you.";

        private static void Clicked()
        {
            Module.Guard("quick stacking into your personal chests from the button", () => QuickStack.Ask(personal: true));
        }
    }

    /// <summary>
    /// The personal quick stack button's picture: the quick stack's (a chest, an arrow down into it) with a person in its
    /// lower right corner, cut out of the chest so a dark rim parts them, and far enough from the picture's edges for its
    /// own rim. Drawn as the quick stack's is (Icon).
    /// </summary>
    internal static class PersonalIcon
    {
        private static Sprite sprite;

        /// <summary>Drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sprite => sprite != null ? sprite : (sprite = Icon.Make("StoreStash_PersonalIcon", Pixels()));

        /// <summary>The pixels, as Icon.Pixels gives them, of this picture.</summary>
        internal static Color[] Pixels() => Icon.Pixels(Distance);

        /// <summary>The signed distance to the picture: the quick stack's, less the person and a gap around it, then the
        /// person.</summary>
        internal static float Distance(Vector2 at)
        {
            float person = Person(at);
            float chest = Mathf.Max(Icon.Distance(at), -(person - 2.2f));
            return Mathf.Min(chest, person);
        }

        // A head and rounded shoulders, bottom right, below the chest's lid: 1.9 px from the right edge and 1.5 px from the
        // bottom, so the dark rim (1.3 px) fits inside the picture.
        private static float Person(Vector2 at)
        {
            float head = (at - new Vector2(25.5f, 10.9f)).magnitude - 2.8f;
            float shoulders = Icon.Box(at, new Vector2(25.5f, 4.4f), new Vector2(4.6f, 2.9f), 2.6f);
            return Mathf.Min(head, shoulders);
        }
    }
}
