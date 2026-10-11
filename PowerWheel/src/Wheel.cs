using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Valheim.UI;

namespace PowerWheel
{
    /// <summary>
    /// The root the mod opens the game's wheel with. The game's wheel gives a page the full opening (its controls loaded,
    /// its state reset, a death closing it at once) only when the page opened is an OpenRadialConfig
    /// (RadialBase.Open's "config is OpenRadialConfig"); the game's own root then opens its main page, and this one opens
    /// the power page instead. It re-implements the game's interface, so the wheel's calls through it reach this class;
    /// nothing of the game's root runs, and no patch on it is needed.
    /// </summary>
    internal sealed class PowerRoot : OpenRadialConfig, IRadialConfig
    {
        string IRadialConfig.LocalizedName => Wheel.Title;

        Sprite IRadialConfig.Sprite => null;

        void IRadialConfig.InitRadialConfig(RadialBase radial) => Wheel.RootOpened(radial);
    }

    /// <summary>The power page: its elements were made before the wheel opened (Wheel.Open).</summary>
    internal sealed class PowerPage : IRadialConfig
    {
        public string LocalizedName => Wheel.Title;

        public Sprite Sprite => null;

        public void InitRadialConfig(RadialBase radial) => Wheel.PageOpened(radial);
    }

    /// <summary>What a power's element is made from (GroupElement.Init takes its name and icon from a page). Never opened:
    /// the element's Interact picks the power instead.</summary>
    internal sealed class PowerChoice : IRadialConfig
    {
        internal PowerChoice(string name, Sprite icon)
        {
            LocalizedName = name;
            Sprite = icon;
        }

        public string LocalizedName { get; }

        public Sprite Sprite { get; }

        public void InitRadialConfig(RadialBase radial)
        {
        }
    }

    /// <summary>One element of the wheel showing now: a power, or an empty slot that fills the ring.</summary>
    internal sealed class Slot
    {
        /// <summary>The power's status effect name; null for an empty slot.</summary>
        internal string Power;

        internal StatusEffect Effect;

        /// <summary>Its name as the game shows it.</summary>
        internal string ShownName;

        internal RadialMenuElement Element;

        internal bool Current;

        internal bool Locked;

        /// <summary>Locked and shown as a padlock alone, with no icon or name (card B06, "How a locked power looks").</summary>
        internal bool Hidden;

        /// <summary>Why it is locked, under "Locked" in the wheel's centre, while hidden.</summary>
        internal string LockedReason;

        /// <summary>The line last written under its name (the cooldown, or "Locked").</summary>
        internal string SubTitle;

        internal bool SubTitleChanged;
    }

    /// <summary>
    /// The power wheel on the game's own radial menu (card A75's decision 1; the research's section 3): the game draws it,
    /// animates it, reads the mouse's direction and the right stick, the clicks and Escape, shows the cursor and holds the
    /// camera still, as for G, and shows the hovered power's name in its centre. The mod makes one element per power from
    /// the game's own element prefab, fills the ring with the game's empty slots, starts it on the power he has, and
    /// picks through each element's Interact (Picks). Its controls (Controls) read the key's release and the right click
    /// for the settings that change them; everything else is the game's.
    /// </summary>
    internal static class Wheel
    {
        internal const string Title = "Forsaken powers";

        private static readonly PowerPage Page = new PowerPage();
        private static PowerRoot root;

        // Made for the opening under way; the page hands them to the game (PageOpened).
        private static List<Slot> pending;
        private static int pendingStart = -1;

        // The wheel showing now (or last shown).
        private static List<Slot> slots = new List<Slot>();
        private static int startSlot = -1;

        /// <summary>The distance on screen from the wheel's centre to its icons, measured as it opened (for the centre,
        /// PowerRules.InCentre); 0 when unknown.</summary>
        internal static float IconDistance { get; private set; }

        private static RadialBase Radial => Hud.instance != null ? Hud.instance.m_radialMenu : null;

        /// <summary>The game's wheel and its parts exist (it is made with the HUD).</summary>
        internal static bool CanOpen => MissingPart() == null;

        /// <summary>The first of the wheel's parts not there, in words for the key log; null when all are.</summary>
        internal static string MissingPart()
        {
            if (Radial == null)
            {
                return Hud.instance == null ? "the HUD" : "the HUD's wheel (Hud.m_radialMenu)";
            }
            RadialDataSO data = WheelData.Get();   // made here if the game's own wheel was never opened yet
            if (data == null)
            {
                return "the wheel's data (RadialData.SO)";
            }
            if (data.GroupElement == null)
            {
                return "the wheel's element (RadialDataSO.GroupElement)";
            }
            if (data.EmptyElement == null)
            {
                return "the wheel's empty slot (RadialDataSO.EmptyElement)";
            }
            return ObjectDB.instance == null ? "the game's item and effect list (ObjectDB)" : null;
        }

        /// <summary>The wheel's page is the power page.</summary>
        internal static bool IsOurs(RadialBase radial) => radial != null && ReferenceEquals(radial.CurrentConfig, Page);

        /// <summary>The power wheel shows: its page is open, or a wheel is closing (its page is gone by then).</summary>
        internal static bool Shows
        {
            get
            {
                RadialBase radial = Radial;
                return radial != null && Hud.InRadial() && (IsOurs(radial) || radial.CurrentConfig == null);
            }
        }

        /// <summary>The element of the power he has, while it shows; null otherwise.</summary>
        internal static RadialMenuElement CurrentElement
        {
            get
            {
                foreach (Slot slot in slots)
                {
                    if (slot.Current && slot.Element != null)
                    {
                        return slot.Element;
                    }
                }
                return null;
            }
        }

        private static PowerRoot Root
        {
            get
            {
                // Remade if Unity unloaded it with a scene (a runtime asset nothing of Unity's own refers to).
                if (root == null)
                {
                    root = ScriptableObject.CreateInstance<PowerRoot>();
                    root.name = "PowerWheel_Root";
                    root.hideFlags = HideFlags.HideAndDontSave;
                }
                return root;
            }
        }

        /// <summary>
        /// Opens the power wheel; false if it didn't open (nothing to show, or the game's wheel refused), and nothing is
        /// left behind then. Every element is made before the game's wheel is asked, so nothing of the mod's can fail
        /// halfway through the game's opening. <paramref name="why"/>: what happened, in words, for the key log.
        /// </summary>
        internal static bool Open(Player player, out string why)
        {
            RadialBase radial = Radial;
            string missing = MissingPart();
            if (radial == null || missing != null)
            {
                why = "the game's wheel isn't there yet (" + (missing ?? "the HUD's wheel") + " is missing)";
                return false;
            }
            List<Slot> made = Make(radial, player, out int start);
            if (made == null)
            {
                why = "no power to show (none in the game's list, or \"Which powers show\" is EarnedOnly and none is earned)";
                return false;
            }

            bool couldOpen = radial.CanOpen;
            bool adopted;
            pending = made;
            pendingStart = start;
            try
            {
                radial.CanOpen = true;   // set by the game only at its own wheel keys' press (Player.HandleRadialInput)
                radial.Open(Root);
            }
            finally
            {
                adopted = pending == null;
                if (!adopted)
                {
                    // The game's wheel refused (it never opened the page): nothing of this opening stays.
                    pending = null;
                    Destroy(made);
                    if (!Hud.InRadial())
                    {
                        radial.CanOpen = couldOpen;
                    }
                }
            }
            if (!adopted)
            {
                why = player.InPlaceMode()
                    ? "the game's wheel won't open while a build tool, hoe or cultivator is in your hand"
                    : "the game's wheel refused to open its page";
                return false;
            }

            slots = made;
            startSlot = start;
            if (!IsOurs(radial) || !Hud.InRadial())
            {
                // Opened and closed at once (the game's own reasons); its elements are the game's now.
                why = "the game's wheel opened and closed at once (" + (Hud.InRadial() ? "another page took its place" : "it closed") + ")";
                return false;
            }
            AfterOpen(radial);
            int locked = 0;
            int powers = 0;
            foreach (Slot slot in slots)
            {
                if (slot.Power != null)
                {
                    powers++;
                    locked += slot.Locked ? 1 : 0;
                }
            }
            Slot first = start >= 0 && start < slots.Count ? slots[start] : null;
            why = $"{powers} powers ({locked} locked), starting on {(first?.Power != null ? first.ShownName : first != null ? "an empty slot" : "none in particular")}";
            return true;
        }

        /// <summary>The root's opening, inside the game's RadialBase.Open: as the game's own root (its interaction delay,
        /// its animation), the mod's controls over the ones just loaded, then the power page. Only assignments before the
        /// game's own Open, so nothing here can fail.</summary>
        internal static void RootOpened(RadialBase radial)
        {
            radial.OnInteractionDelay = delay => PlayerController.SetTakeInputDelay(delay);
            radial.ShouldAnimateIn = true;
            Controls.Install(radial);
            radial.Open(Page);
        }

        /// <summary>The page's opening, inside the game's RadialBase.Open: the elements made by Open, laid out by the game,
        /// starting on the power he has.</summary>
        internal static void PageOpened(RadialBase radial)
        {
            List<Slot> made = pending;
            pending = null;
            if (made == null)
            {
                // Opened again by the game itself (a refresh): this mod only shows the page it made, so it closes.
                KeyLog.Line("The game opened the power page again by itself: the wheel closes.");
                radial.QueuedClose();
                return;
            }
            Controls.Via = Via.Click;
            int segments = PowerRules.SegmentsFor(made.Count, RadialData.SO.MaxElementsRange);
            radial.StartItemIndex = PowerRules.StartIndexFor(pendingStart, radial.MaxElementsPerLayer, segments);
            var elements = new List<RadialMenuElement>(made.Count);
            foreach (Slot slot in made)
            {
                elements.Add(slot.Element);
            }
            radial.ConstructRadial(elements);
        }

        /// <summary>Closes the power wheel if it is the one open (the mod switched off or failed); the game's own wheels are
        /// left alone.</summary>
        internal static void CloseIfOurs()
        {
            RadialBase radial = Radial;
            if (radial != null && radial.gameObject.activeSelf &&
                (IsOurs(radial) || (root != null && ReferenceEquals(radial.CurrentConfig, root))))
            {
                radial.QueuedClose();
            }
        }

        /// <summary>
        /// Once a frame after every Update, while the power wheel shows: the line under each name (the cooldown, which
        /// counts down, or "Locked"), the one in the centre for the power under the pointer, and the description beside the
        /// wheel. The game losing the focus closes it without a change (a key held then reads as let go).
        /// </summary>
        internal static void Tick()
        {
            RadialBase radial = Radial;
            if (radial == null || !IsOurs(radial))
            {
                Looks.Hide();
                return;
            }
            if (!Application.isFocused)
            {
                radial.QueuedClose();
                Looks.Hide();
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            float cooldown = player.m_guardianPowerCooldown;
            string ready = Localization.instance.Localize("$hud_ready");
            string timeLeft = StatusEffect.GetTimeString(cooldown);
            bool showCooldown = Settings.ShowCooldown.Value;
            foreach (Slot slot in slots)
            {
                if (slot.Power != null && slot.Element != null)
                {
                    WriteSubTitle(slot, SubtitleOf(slot, showCooldown, cooldown, ready, timeLeft));
                }
            }

            Slot selected = SlotOf(radial.Selected);
            if (selected != null && selected.SubTitleChanged)
            {
                // The centre shows an element's line only as it is selected (ElementInfo.Set): kept counting down here.
                TMP_Text centre = GameHooks.CentreSubTitle(radial);
                if (centre != null)
                {
                    centre.text = selected.SubTitle;
                }
            }
            foreach (Slot slot in slots)
            {
                slot.SubTitleChanged = false;
            }

            Looks.Describe(radial, selected);
        }

        /// <summary>Makes the wheel's elements: one per power shown, in progression order, then empty slots to fill the
        /// ring. Null when there's nothing to show (with a message). <paramref name="start"/>: the element to start on (the
        /// power he has, else the first empty slot, else none).</summary>
        private static List<Slot> Make(RadialBase radial, Player player, out int start)
        {
            start = -1;
            var effects = new Dictionary<string, StatusEffect>(StringComparer.Ordinal);
            foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects)
            {
                if (effect != null && PowerRules.IsPower(effect.name) && !effects.ContainsKey(effect.name))
                {
                    effects.Add(effect.name, effect);
                }
            }

            ShowChoice show = Settings.WhichPowers.Value;
            EarnedChoice rule = Settings.Earned.Value;
            LockedLookChoice look = Settings.LockedLook.Value;
            bool mark = Settings.MarkCurrent.Value;
            bool showCooldown = Settings.ShowCooldown.Value;
            float cooldown = player.m_guardianPowerCooldown;
            string ready = Localization.instance.Localize("$hud_ready");
            string timeLeft = StatusEffect.GetTimeString(cooldown);
            RadialDataSO data = WheelData.Get();
            var made = new List<Slot>();
            try
            {
                foreach (string power in PowerRules.Order(effects.Keys))
                {
                    PowerFacts facts = Powers.FactsOf(player, power);
                    if (!PowerRules.Shown(facts, show, rule))
                    {
                        continue;
                    }
                    StatusEffect effect = effects[power];
                    var slot = new Slot
                    {
                        Power = power,
                        Effect = effect,
                        ShownName = Localization.instance.Localize(effect.m_name),
                        Current = facts.IsCurrent,
                        Locked = PowerRules.Locked(facts, show, rule),
                    };
                    slot.Hidden = PowerRules.Hidden(slot.Locked, look);
                    slot.LockedReason = slot.Hidden ? PowerRules.LockedReason(rule, facts.BossBeaten != null) : null;
                    GroupElement element = UnityEngine.Object.Instantiate(data.GroupElement);
                    slot.Element = element;
                    made.Add(slot);   // before anything else, so a failure below destroys it too
                    element.name = "PowerWheel_" + power;
                    // A hidden power is a padlock alone: the game's element shows the padlock as its icon and "Locked" as
                    // its name in the wheel's centre (card B06).
                    element.Init(slot.Hidden ? new PowerChoice(PowerRules.HiddenTitle, Looks.PadlockIconOrNone()) : new PowerChoice(slot.ShownName, effect.m_icon),
                                 null, radial);
                    string chosen = power;
                    element.Interact = () => Picks.Choose(chosen);
                    element.CloseOnInteract = () => true;
                    element.Activated = mark && slot.Current ? 1f : 0f;
                    WriteSubTitle(slot, SubtitleOf(slot, showCooldown, cooldown, ready, timeLeft));
                    if (slot.Current)
                    {
                        start = made.Count - 1;
                    }
                }
                if (made.Count == 0)
                {
                    Picks.Tell(player, PowerRules.NoPowersText, null);
                    return null;
                }
                int pads = PowerRules.PadsFor(made.Count, data.MaxElementsRange);
                for (int i = 0; i < pads; i++)
                {
                    EmptyElement empty = UnityEngine.Object.Instantiate(data.EmptyElement);
                    made.Add(new Slot { Element = empty });
                    empty.name = "PowerWheel_Empty";
                    empty.Init();
                    if (start < 0)
                    {
                        start = made.Count - 1;   // no power yet: letting go on an empty slot changes nothing
                    }
                }
            }
            catch
            {
                Destroy(made);
                throw;
            }
            Looks.Dress(made);
            return made;
        }

        /// <summary>After the game opened the page: where the mouse pointer starts, the centre's size, the names under the
        /// icons.</summary>
        private static void AfterOpen(RadialBase radial)
        {
            Slot start = startSlot >= 0 && startSlot < slots.Count ? slots[startSlot] : null;
            RadialMenuElement first = slots.Count > 0 ? slots[0].Element : null;
            Transform ring = first != null ? first.transform.parent : null;
            IconDistance = ring != null ? Mathf.Abs(RadialData.SO.ElementsDistance * ring.lossyScale.x) : 0f;

            if (!ZInput.IsExclusiveGamepadActive() && Cursor.lockState != CursorLockMode.Locked)
            {
                // The game has put the pointer on the element it started on. In the centre (the setting, or no power yet)
                // the pointer means the power he has, or nothing; on a power, the game's own start can miss it after a
                // page with another number of segments, which the pointer then puts right.
                if (Settings.CursorStarts.Value == CursorChoice.InTheCentre || start == null || start.Power == null)
                {
                    ZInput.SetMousePosition(radial.InfoPosition);
                }
                else if (start.Element != null && radial.Selected != start.Element)
                {
                    ZInput.SetMousePosition(start.Element.ElementTransform.position);
                }
            }
            Looks.Label(radial, slots);
        }

        private static Slot SlotOf(RadialMenuElement element)
        {
            if (element == null)
            {
                return null;
            }
            foreach (Slot slot in slots)
            {
                if (slot.Power != null && ReferenceEquals(slot.Element, element))
                {
                    return slot;
                }
            }
            return null;
        }

        // The line under a power's name: why it's locked for a hidden one, else "Locked" or its cooldown (PowerRules).
        private static string SubtitleOf(Slot slot, bool showCooldown, float cooldown, string ready, string timeLeft) =>
            slot.Hidden ? slot.LockedReason : PowerRules.Subtitle(slot.Locked, showCooldown, cooldown, ready, timeLeft);

        private static void WriteSubTitle(Slot slot, string text)
        {
            if (text != slot.SubTitle && GameHooks.SetSubTitle(slot.Element, text))
            {
                slot.SubTitle = text;
                slot.SubTitleChanged = true;
            }
        }

        private static void Destroy(List<Slot> made)
        {
            foreach (Slot slot in made)
            {
                if (slot.Element != null)
                {
                    UnityEngine.Object.Destroy(slot.Element.gameObject);
                }
            }
        }
    }
}
