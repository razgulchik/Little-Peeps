using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // The UI's front door. Two directions go through it, and each has exactly one way:
    //
    //  - IN: what an element needs from the world. GameBootstrap packs it into UIServices and Initialize
    //    hands that to every IUIElement under the Canvas, found here, not listed. A new element needs no
    //    field on this class, no drag in the Inspector and no override in the scene.
    //  - OUT: the UI pieces the game itself drives — the screens a gameplay state shows, the banner the
    //    AgeSequencer plays. Those are listed below, once, and GameBootstrap reads them from here.
    //
    // So the scene holds a single link into the Canvas (GameBootstrap → this), and the Canvas none into
    // the scene. A reference from the scene into a prefab instance makes the SCENE carry a stub per
    // target and an override per field, all of them things a merge can fight over and a Revert can wipe.
    //
    // Deliberately NOT a singleton and deliberately without logic: it is the list of what the game drives,
    // read by the composition root and nobody else. A panel that wants another panel hidden asks by MODE
    // (see UIVisibility); a panel that wants another panel's data asks the EventBus. Adding a property
    // here to shortcut either of those is how this class turns into the global the rest of the
    // architecture was built to avoid.
    [DisallowMultipleComponent]
    public class UIRoot : MonoBehaviour
    {
        [Header("Screens driven by a gameplay state")]
        [SerializeField] private ZoneSelectionUI zoneScreen;
        [SerializeField] private PerkSelectionUI perkScreen;

        [Tooltip("The one yes/no question for the whole game. OfferingEndState asks it at the end of a cycle: " +
                 "offer, or accept the gods' wrath.")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Tooltip("The screen after a prestige, where points are spent on meta perks before the next run.")]
        [SerializeField] private MetaUpgradesUI metaScreen;

        [Header("Age banner, played by AgeSequencer")]
        [Tooltip("The CanvasGroup holding the banner (AgeTransition), faded in around the title and out again.")]
        [SerializeField] private CanvasGroup ageBanner;

        [Tooltip("The banner's title (BannerText). It prints the age's number.")]
        [SerializeField] private TMP_Text ageTitle;

        public ZoneSelectionUI ZoneScreen => zoneScreen;
        public PerkSelectionUI PerkScreen => perkScreen;
        public ConfirmDialog ConfirmDialog => confirmDialog;
        public MetaUpgradesUI MetaScreen => metaScreen;
        public CanvasGroup AgeBanner => ageBanner;
        public TMP_Text AgeTitle => ageTitle;

        // Wiring tripwire, and a loud one for the screens. They are read ONCE — by GameBootstrap.Awake,
        // straight into the states that drive them — so a missing reference stays invisible until the
        // player buys an age, and then costs them a zone pick and a perk with only a log to show for it.
        // The banner gets a warning instead: without it an age changes unannounced, which is a cosmetic
        // loss, not a lost run.
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

            // Loud too: without it the end of a cycle cannot ask, so the gods take what they can and the run
            // never ends — a run is never ended without asking.
            if (confirmDialog == null)
                Debug.LogError("[UIRoot] Confirm Dialog is not assigned — the end of an offering cycle cannot " +
                               "ask, so no run can ever end.", this);

            if (metaScreen == null)
                Debug.LogError("[UIRoot] Meta Screen is not assigned — after a prestige the next run starts at " +
                               "once, with no way to spend prestige points.", this);

            if (ageBanner == null || ageTitle == null)
                Debug.LogWarning("[UIRoot] Age Banner or Age Title is not assigned — ages change with no banner.",
                                 this);
        }

        // Every element under the Canvas that needs the world, in one call from GameBootstrap.Awake —
        // inactive ones included, since a panel that switches itself off (AgeCostPanel at the final age)
        // still needs its systems for the next run. Called once: elements are reused across runs.
        public void Initialize(UIServices ui)
        {
            foreach (var element in GetComponentsInChildren<IUIElement>(true))
                element.Initialize(ui);
        }
    }
}
