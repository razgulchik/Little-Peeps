using UnityEngine;

namespace LittlePeeps
{
    // The UI's front door: the panels the rest of the game has to reach, listed once and wired INSIDE
    // the Canvas prefab instead of from the Bootstrap object out in the scene. GameBootstrap holds one
    // reference to this and names no panel of its own.
    //
    // That move is not only tidiness. A reference from the scene into a prefab instance makes the SCENE
    // carry a stripped-component stub per panel; five panels meant five stubs and five links in
    // SampleScene.unity, all of them things a merge can fight over. One link survives, the rest live in
    // the prefab where both ends already are.
    //
    // Deliberately NOT a singleton and deliberately without logic: it is a list of what exists, read by
    // the composition root and nobody else. Only what a gameplay state has to drive is exposed at all —
    // the two pick screens, the confirm dialog and the meta screen. The age panels and the prestige
    // button are pure internal wiring, and nothing outside this file names them. A panel that wants
    // another panel hidden asks by MODE (see UIVisibility); a panel that wants another panel's data asks
    // the EventBus. Adding a property here to shortcut either of those is how
    // this class turns into the global the rest of the architecture was built to avoid.
    [DisallowMultipleComponent]
    public class UIRoot : MonoBehaviour
    {
        [Header("Screens driven by a gameplay state")]
        [SerializeField] private ZoneSelectionUI zoneScreen;
        [SerializeField] private PerkSelectionUI perkScreen;

        [Tooltip("The one yes/no question for the whole game. PrestigeMenuState asks \"End this run?\" through it.")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Tooltip("The screen after a prestige, where points are spent on meta perks before the next run.")]
        [SerializeField] private MetaUpgradesUI metaScreen;

        [Header("Run-dependent panels (wired here, not named outside)")]
        [SerializeField] private AgeAdvancePanel ageAdvance;
        [SerializeField] private AgeCostPanel ageCost;
        [SerializeField] private AgeTimelinePanel ageTimeline;
        [SerializeField] private PrestigeButton prestigeButton;

        public ZoneSelectionUI ZoneScreen => zoneScreen;
        public PerkSelectionUI PerkScreen => perkScreen;
        public ConfirmDialog ConfirmDialog => confirmDialog;
        public MetaUpgradesUI MetaScreen => metaScreen;

        // Wiring tripwire, and a loud one for the screens. They are read ONCE — by GameBootstrap.Awake,
        // straight into the states that drive them — so a missing reference stays invisible until the
        // player buys an age, and then costs them a zone pick and a perk with only a log to show for it.
        // The age panels get a warning instead: each one dying alone is a cosmetic loss, not a lost run.
        //
        // In Start rather than Awake so every Awake has had its chance to wire things first.
        private void Start()
        {
            if (zoneScreen == null)
                Debug.LogError("[UIRoot] Zone Screen is not assigned — every age transition will take the " +
                               "first zone offer unasked.", this);

            if (perkScreen == null)
                Debug.LogError("[UIRoot] Perk Screen is not assigned — the perk pick will be skipped for " +
                               "the whole run.", this);

            // The prestige pair is loud too: without either one there is no way to end a run at all.
            if (confirmDialog == null)
                Debug.LogError("[UIRoot] Confirm Dialog is not assigned — the prestige button will do " +
                               "nothing, since a run is never ended without asking.", this);

            if (prestigeButton == null)
                Debug.LogError("[UIRoot] Prestige Button is not assigned — there is no way to prestige.", this);

            if (metaScreen == null)
                Debug.LogError("[UIRoot] Meta Screen is not assigned — after a prestige the next run starts at " +
                               "once, with no way to spend prestige points.", this);

            if (ageAdvance == null) Debug.LogWarning("[UIRoot] Age Advance is not assigned.", this);
            if (ageCost == null) Debug.LogWarning("[UIRoot] Age Cost is not assigned.", this);
            if (ageTimeline == null) Debug.LogWarning("[UIRoot] Age Timeline is not assigned.", this);
        }

        // The panels that need the run, in one call from GameBootstrap.Awake. The null checks that used
        // to sit in bootstrap live here — same tripwires, one place. They stay null-tolerant rather than
        // loud because a half-wired Canvas should lose one panel, not the whole boot.
        public void Initialize(AgeSystem ageSystem, ResourceSystem resourceSystem, PrestigeSystem prestigeSystem,
                               RunContext run)
        {
            if (ageAdvance != null) ageAdvance.Initialize(ageSystem, run);
            if (ageCost != null) ageCost.Initialize(ageSystem, resourceSystem, run);
            if (ageTimeline != null) ageTimeline.Initialize(ageSystem, run);
            if (prestigeButton != null) prestigeButton.Initialize(prestigeSystem, run);
        }
    }
}
