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
    //   1. Application.runInBackground, the saved settings (volume), test mode (presets + its own profile)
    //   2. Load MetaContext from disk (RunContext is owned by RunManager)
    //   3. Wire run-independent systems (prestigeSystem)
    //   4. RunManager.StartNewRun → wire run-dependent systems (tapSystem / ageSystem / UIRoot)
    //   5. Create App FSM → push BootState → the main menu, over the first island, frozen; its Play
    //      enters GameplayContainer
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private UnitSystem unitSystem;
        [SerializeField] private SpawnSystem spawnSystem;
        [SerializeField] private StructureSystem buildingSystem;
        [SerializeField] private TapSystem tapSystem;
        [SerializeField] private CarrySystem carrySystem;
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

        [Header("Debug")]
        [Tooltip("Test presets, used only while the menu switch Little Peeps → Test Mode is on (per machine, " +
                 "editor only). Empty = no test mode in this scene.")]
        [SerializeField] private TestMode testMode;

        private MetaContext metaContext;
        private StateMachine appStateMachine;

        // Asked when the game is closed: whether the run is in normal play, where it can be snapshotted as it
        // stands (OnApplicationQuit).
        private GameplayContainerState gameplayState;

        // All bootstrap work runs in Awake. Unity guarantees every Awake completes before any
        // Start, so by the time any other system's Start runs, everything is wired and the run
        // has started — initialization is order-independent and needs NO Script Execution Order
        // tweak. The one rule for new systems: never read injected state (contexts / other
        // systems) in your own Awake/OnEnable — only from Start onward. See SCENE_SETUP.md.
        private void Awake()
        {
            Application.runInBackground = true;
            GameSettings.ApplySaved();

            //    Test mode first: it swaps in its presets and its own profile before anything reads them, so
            //    the profile loaded below and the first island are already the test ones; they hold for every
            //    run of this play session.
            if (TestMode.IsOn)
            {
                if (testMode != null) testMode.Apply(runManager, offeringSystem, saveSystem);
                else Debug.LogWarning("[GameBootstrap] test mode is on (Little Peeps → Test Mode), but no " +
                                      "TestMode is assigned — the real presets and the real profile are used.",
                                      this);
            }

            // 1. Load persistent data.
            metaContext = saveSystem.Load();

            // 2. Wire run-independent systems (Meta only).
            prestigeSystem.Initialize(metaContext);

            //    Every run starts with the meta perks bought so far, so RunManager learns where they come
            //    from before it builds the first one — and what a saved run is rebuilt with.
            runManager.Initialize(prestigeSystem, ageSystem, perkSystem, saveSystem);

            // 3. The first run: the one left last time when there is one to continue, else a fresh one —
            //    RunManager creates the RunContext, seeds resources (via ResourceSystem.Initialize) and builds
            //    the island. Either way it is the main menu's backdrop; a saved one stays on disk until it is
            //    continued, so quitting from the menu keeps it.
            RunSnapshot savedRun = saveSystem.LoadRun();
            bool runRestored = savedRun != null && runManager.TryRestoreRun(savedRun);
            if (savedRun != null && !runRestored) saveSystem.SetAsideRun();   // kept, before the fresh run discards it
            if (!runRestored) runManager.StartNewRun();
            RunContext run = runManager.CurrentRun;

            // 4. Wire run-dependent systems.
            tapSystem.Initialize(run);

            //    Loud but not fatal: without it the hand never has room for anyone, and only taps.
            if (carrySystem != null) carrySystem.Initialize(run);
            else Debug.LogError("[GameBootstrap] no CarrySystem assigned — the hand cannot carry units.", this);

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
            MainMenuUI mainMenuScreen = uiRoot != null ? uiRoot.MainMenu : null;
            PauseMenuUI pauseScreen = uiRoot != null ? uiRoot.PauseMenu : null;
            SettingsUI settingsWindow = uiRoot != null ? uiRoot.Settings : null;
            ConfirmDialog confirmDialog = uiRoot != null ? uiRoot.ConfirmDialog : null;

            // 5. App FSM. Boot is synchronous for now, so we enter Boot and advance straight to the
            //    main menu (when async loading lands, BootState.Tick will own this transition).
            //    The inner gameplay FSM starts in PlayingState; bouncing units / spawners run on
            //    their own MonoBehaviours — the FSM is the orchestration backbone for later states.
            appStateMachine = new StateMachine();
            appStateMachine.Push(new BootState(appStateMachine, saveSystem, metaContext));

            //    The states are handed runManager, NOT the RunContext: they are built once here and live
            //    through every prestige, so a captured context would be the finished run's.
            var gameplayFsm = new StateMachine();

            //    Every time normal play is left the run is snapshotted as it stands, so a game closed in the
            //    window that follows is saved as it was just before it (SaveSystem).
            var playingState = new PlayingState(placementController, CaptureRun);
            var perkSelectionState = new PerkSelectionState(gameplayFsm, perkSystem, perkScreen, runManager, playingState);

            //    The pause's Main Menu writes the run and leaves the gameplay states for the menu, which is built
            //    last because it enters them — so the pause reaches it through the variable, filled in below
            //    before anything can be clicked.
            MainMenuState mainMenuState = null;
            var pauseState = new PauseState(gameplayFsm, pauseScreen, settingsWindow, confirmDialog, () =>
            {
                saveSystem.WriteRun();
                appStateMachine.ChangeState(mainMenuState);
            });

            //    The one way a run ends: the gods' wrath at the end of an offering cycle → cash-in → the meta
            //    screen, whose Play starts the next run.
            var metaUpgradesState = new MetaUpgradesState(gameplayFsm, prestigeSystem, runManager, metaScreen,
                                                          playingState);
            var offeringEndState = new OfferingEndState(gameplayFsm, offeringSystem, prestigeSystem, runManager,
                                                        offeringWindow, playingState, perkSelectionState,
                                                        metaUpgradesState);
            gameplayState = new GameplayContainerState(gameplayFsm, playingState,
                                                       perkSelectionState, ageSystem, ageSequencer, resourceSystem,
                                                       islandSystem, zoneScreen, runManager,
                                                       offeringSystem, offeringEndState, pauseState);

            mainMenuState = new MainMenuState(appStateMachine, gameplayState, runManager, prestigeSystem, saveSystem,
                                              mainMenuScreen, settingsWindow, confirmDialog, runRestored);
            appStateMachine.ChangeState(mainMenuState);
        }

        private void Update()
        {
            appStateMachine.Tick();
        }

        // The run as it stands, held to be written when the game is left (SaveSystem.HoldRun).
        private void CaptureRun()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            RunSnapshot snapshot = runManager.Capture();
            saveSystem.HoldRun(snapshot, clock.Elapsed.TotalMilliseconds);
        }

        // Every way the game is left — Quit, the window's close button, Alt+F4, stopping Play in the editor — writes
        // the run's bookmark: from normal play as it stands, from anywhere else as it was when normal play was last
        // left. A crash or a killed process never gets here, and then the run is lost by design (RunFile).
        private void OnApplicationQuit()
        {
            if (saveSystem == null) return;

            if (appStateMachine != null && gameplayState != null && appStateMachine.Current == gameplayState &&
                gameplayState.InNormalPlay)
                CaptureRun();
            saveSystem.WriteRun();
        }
    }
}
