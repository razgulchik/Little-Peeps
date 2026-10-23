using UnityEngine;

namespace LittlePeeps
{
    // Entry point — attach to the Bootstrap GameObject in the Bootstrap scene.
    //
    // Required scene hierarchy:
    //   Bootstrap   [this component + all [SerializeField] system components below, or on child objects]
    //   Island      [IslandSystem + Tilemap/TilemapRenderer children]
    //   Camera      [Camera + CinemachineBrain; a CinemachineCamera follows the CameraTarget object]
    //   CameraTarget[CameraController — moves this object; the vcam follows it with damping]
    //   UI (Canvas) [UIRoot — the only reference into the UI. Everything the Canvas uses from the world
    //                goes in through UIServices, built below, so no Canvas element references the scene.
    //                UIVisibility sits beside it and owns what is on screen per mode]
    //
    // The pier is NOT in this list: it is start content, like the house — IslandSystem places it per run
    // from StartConfig.pier on the start's east coast, where it stays. It no longer ends a run (the gods'
    // wrath does); its Pier marker component waits for whatever the pier becomes next.
    //
    // Initialization (all in Awake — order-independent, see note on Awake below):
    //   1. Application.runInBackground
    //   2. Load MetaContext from disk (RunContext is owned by RunManager)
    //   3. Wire run-independent systems (prestigeSystem)
    //   4. RunManager.StartNewRun → wire run-dependent systems (tapSystem / ageSystem / UIRoot)
    //   5. Create App FSM → push BootState → auto-transition to GameplayContainer
    //      (MainMenu skipped until its UI exists)
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private UnitSystem unitSystem;
        [SerializeField] private SpawnSystem spawnSystem;
        [SerializeField] private StructureSystem buildingSystem;
        [SerializeField] private TapSystem tapSystem;
        [SerializeField] private AgeSystem ageSystem;
        [SerializeField] private AgeSequencer ageSequencer;
        [SerializeField] private PerkSystem perkSystem;
        [SerializeField] private PrestigeSystem prestigeSystem;
        [SerializeField] private OfferingSystem offeringSystem;
        [SerializeField] private RunManager runManager;
        [SerializeField] private SaveSystem saveSystem;

        [Header("UI")]
        [SerializeField] private UIRoot uiRoot;

        [Tooltip("Handed to the UI (UIServices): the zone pick widens the camera to the offers and sends it " +
                 "to the hovered one.")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("Handed to the UI (UIServices): draws the zone offers on the island.")]
        [SerializeField] private ZonePreviewOverlay zonePreview;

        [Header("Building")]
        [SerializeField] private PlacementController placementController;

        private MetaContext metaContext;
        private StateMachine appStateMachine;

        // All bootstrap work runs in Awake. Unity guarantees every Awake completes before any
        // Start, so by the time any other system's Start runs, everything is wired and the run
        // has started — initialization is order-independent and needs NO Script Execution Order
        // tweak. The one rule for new systems: never read injected state (contexts / other
        // systems) in your own Awake/OnEnable — only from Start onward. See SCENE_SETUP.md.
        private void Awake()
        {
            Application.runInBackground = true;

            // 1. Load persistent data.
            metaContext = saveSystem.Load();

            // 2. Wire run-independent systems (Meta only).
            prestigeSystem.Initialize(metaContext);

            //    Every run starts with the meta perks bought so far, so RunManager learns where they come
            //    from before it builds the first one — and the age list, for the ages a perk skips.
            runManager.Initialize(prestigeSystem, ageSystem);

            // 3. Start the first run: RunManager creates the RunContext, seeds resources
            //    (via ResourceSystem.Initialize) and asks IslandSystem to generate the island.
            runManager.StartNewRun();
            RunContext run = runManager.CurrentRun;

            // 4. Wire run-dependent systems. The tap also learns about placement, which owns the click
            //    whenever a build card or the sell tool is in hand.
            tapSystem.Initialize(run, placementController);
            ageSystem.Initialize(run);

            // Loud but not fatal: without it there is no offering cycle — no quotas, no wrath, no perks
            // from offerings — and the rest of the game plays on.
            if (offeringSystem != null) offeringSystem.Initialize(run);
            else Debug.LogError("[GameBootstrap] no OfferingSystem assigned — the offering cycle is off.", this);

            // Everything the Canvas may use from the world, packed once and handed to UIRoot, which passes
            // it to every element under the Canvas (IUIElement). Which elements those ARE is the Canvas's
            // business — bootstrap names the UI once and never its parts. A new system for the UI is one
            // line here.
            var ui = new UIServices
            {
                ResourceSystem = resourceSystem,
                AgeSystem = ageSystem,
                OfferingSystem = offeringSystem,
                RunManager = runManager,
                PlacementController = placementController,
                CameraController = cameraController,
                ZonePreviewOverlay = zonePreview,
            };
            ui.WarnMissing(this);

            // One missing reference here takes the WHOLE UI with it, so it is loud; the run still boots
            // blind rather than dying inside Awake, which would leave nothing on screen to read the
            // error from.
            if (uiRoot == null)
                Debug.LogError("[GameBootstrap] no UIRoot assigned — the game boots with no UI at all. " +
                               "Assign the Canvas's UIRoot.", this);
            else
                uiRoot.Initialize(ui);

            // The other direction: a piece of UI a system plays. The banner lives in the Canvas, so the
            // AgeSequencer out in the scene gets it from here instead of referencing into the prefab.
            if (ageSequencer != null)
                ageSequencer.Initialize(uiRoot != null ? uiRoot.AgeBanner : null,
                                        uiRoot != null ? uiRoot.AgeTitle : null);

            PerkSelectionUI perkScreen = uiRoot != null ? uiRoot.PerkScreen : null;
            ZoneSelectionUI zoneScreen = uiRoot != null ? uiRoot.ZoneScreen : null;
            OfferingWindow offeringWindow = uiRoot != null ? uiRoot.OfferingWindow : null;
            MetaUpgradesUI metaScreen = uiRoot != null ? uiRoot.MetaScreen : null;

            // 5. App FSM. Boot is synchronous for now, so we enter Boot and advance straight to
            //    Gameplay (when async loading lands, BootState.Tick will own this transition).
            //    The inner gameplay FSM starts in PlayingState; bouncing units / spawners run on
            //    their own MonoBehaviours — the FSM is the orchestration backbone for later states.
            appStateMachine = new StateMachine();
            appStateMachine.Push(new BootState(appStateMachine, saveSystem, metaContext));

            //    The states are handed runManager, NOT the RunContext: they are built once here and live
            //    through every prestige, so a captured context would be the finished run's.
            var gameplayFsm = new StateMachine();
            var playingState = new PlayingState(placementController);
            var perkSelectionState = new PerkSelectionState(gameplayFsm, perkSystem, perkScreen, runManager, playingState);

            //    The one way a run ends: the gods' wrath at the end of an offering cycle → cash-in → the meta
            //    screen, whose Play starts the next run.
            var metaUpgradesState = new MetaUpgradesState(gameplayFsm, prestigeSystem, runManager, metaScreen,
                                                          playingState);
            var offeringEndState = new OfferingEndState(gameplayFsm, offeringSystem, prestigeSystem, runManager,
                                                        offeringWindow, playingState, perkSelectionState,
                                                        metaUpgradesState);
            appStateMachine.ChangeState(new GameplayContainerState(gameplayFsm, playingState,
                                                                   perkSelectionState, ageSystem, ageSequencer, resourceSystem,
                                                                   islandSystem, zoneScreen, runManager,
                                                                   offeringSystem, offeringEndState));

            // Exit-to-menu hotkey (GameHotkeys → ExitToMenuRequestedEvent). Owned here because the app FSM
            // and runManager live here; leaving the container restores timeScale via its Exit().
            EventBus<ExitToMenuRequestedEvent>.Subscribe(OnExitToMenu);
        }

        // The hotkey and the event stay wired, but the transition is DECLINED while MainMenuState is
        // still a stub. Entering it used to strand the player: the container's Exit() drops the age-advance
        // subscription, the menu draws nothing and listens for nothing, and gameplay
        // keeps running underneath with no way back. Restoring this is one line once the menu screen
        // exists — see the ChangeState call this replaced in git history.
        private void OnExitToMenu(ExitToMenuRequestedEvent _)
        {
            Debug.LogWarning("Exit to menu isn't available yet — MainMenuState has no screen. Ignoring.", this);
        }

        private void OnDestroy()
        {
            EventBus<ExitToMenuRequestedEvent>.Unsubscribe(OnExitToMenu);
        }

        private void Update()
        {
            appStateMachine.Tick();
        }
    }
}
