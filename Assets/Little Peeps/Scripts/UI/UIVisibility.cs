using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The one table of what is on screen in each UI mode. Every gameplay state announces its own mode
    // (UIModeChangedEvent) and this maps that onto CanvasGroups — so "the age panel is hidden while
    // building" is an inspector row, not a line of code in the build panel.
    //
    // It replaced four private SetVisible(bool) methods that each wrote out the same alpha /
    // interactable / blocksRaycasts triple, and one panel reaching across the hierarchy to switch off
    // another's root (ZoneSelectionUI held a `hud` field for exactly that). A panel that needs another
    // panel hidden now says so by mode, in a place where the whole screen can be read at once.
    //
    // Groups NEST: a row for the HUD root and a row for the age panel inside it both work, because
    // CanvasGroup alpha multiplies down the chain and a non-interactable parent disables its children.
    // So a group only needs a row when it differs from its parent.
    [DisallowMultipleComponent]
    public class UIVisibility : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public CanvasGroup group;

            [Tooltip("The modes this group is visible in. A mask — tick every mode it survives.")]
            public UIMode visibleIn;
        }

        [Tooltip("One row per CanvasGroup whose visibility depends on the mode. Anything not listed is " +
                 "left alone, so a group that is always on needs no row.")]
        [SerializeField] private List<Entry> groups = new();

        // The mode the game boots into, applied before any event can arrive. GameBootstrap.Awake builds
        // the FSM — and so publishes the first mode — and Unity gives no order between that Awake and
        // this one. Establishing the boot mode here makes the order irrelevant instead of depending on
        // it: either the event arrives and is applied, or it was missed and this already said the same
        // thing. Same rule BuildModeButton and BuildPanelUI follow.
        private void Awake() => Apply(UIMode.Playing);

        private void OnEnable() => EventBus<UIModeChangedEvent>.Subscribe(OnModeChanged);
        private void OnDisable() => EventBus<UIModeChangedEvent>.Unsubscribe(OnModeChanged);

        // A row with no group is a half-finished line in the table, and the panel it was meant to name
        // would sit on screen for the whole run. Checked in Start so the warning lands once, after every
        // Awake has had its chance to wire things.
        private void Start()
        {
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].group == null)
                    Debug.LogWarning($"[UIVisibility] row {i} has no CanvasGroup — whatever it was meant " +
                                     "to show or hide is not under this table's control.", this);
        }

        private void OnModeChanged(UIModeChangedEvent e) => Apply(e.Mode);

        // Every row, every time: the table is the state, so nothing can drift out of sync with a mode it
        // happened not to be told about.
        private void Apply(UIMode mode)
        {
            for (int i = 0; i < groups.Count; i++)
                Set(groups[i].group, (groups[i].visibleIn & mode) != 0);
        }

        // The show/hide triple, in one place and private: after the table took the job over, no panel
        // sets its own visibility any more, so there is nothing to expose it to. blocksRaycasts matters
        // as much as alpha — an invisible panel that still swallows clicks leaves a dead rectangle over
        // the island.
        private static void Set(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
