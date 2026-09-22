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
    //   UI (Canvas) [UIRoot — the only UI reference this component holds; every panel is wired inside
    //                the Canvas prefab. UIVisibility sits beside it and owns what is on screen per mode]
    //
    // The pier is NOT in this list: PierSystem instantiates it per run from the "Pier" StructureDef and
    // parks it in the island's bottom-right corner. What that def's PREFAB needs is a Collider2D (on the
    // root or any child) plus the Pier marker component ON THE ROOT — TapSystem resolves a click with
    // GetComponentInParent<Pier>(), walking up from whichever collider was hit.
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
        [SerializeField] private RunManager runManager;
        [SerializeField] private SaveSystem saveSystem;

        [Header("UI")]
        [SerializeField] private UIRoot uiRoot;

        [Header("Build mode")]
        [SerializeField] private PlacementController placementController;
        [SerializeField] private float buildModeCooldown = 5f;

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

            // 3. Start the first run: RunManager creates the RunContext, seeds resources
            //    (via ResourceSystem.Initialize) and asks IslandSystem to generate the island.
            runManager.StartNewRun();
            RunContext run = runManager.CurrentRun;

            // 4. Wire run-dependent systems.
            tapSystem.Initialize(run);
            ageSystem.Initialize(run);

            // Every panel that needs the run, in one call. Which panels those ARE is UIRoot's business
            // and lives in the Canvas prefab — bootstrap names the UI once and never its parts. One
            // missing reference here takes the WHOLE UI with it, so it is loud; the run still boots
            // blind rather than dying inside Awake, which would leave nothing on screen to read the
            // error from.
            if (uiRoot == null)
                Debug.LogError("[GameBootstrap] no UIRoot assigned — the game boots with no UI at all. " +
                               "Assign the Canvas's UIRoot.", this);
            else
                uiRoot.Initialize(ageSystem, resourceSystem, run);

            PerkSelectionUI perkScreen = uiRoot != null ? uiRoot.PerkScreen : null;
            ZoneSelectionUI zoneScreen = uiRoot != null ? uiRoot.ZoneScreen : null;

            // 5. App FSM. Boot is synchronous for now, so we enter Boot and advance straight to
            //    Gameplay (when async loading lands, BootState.Tick will own this transition).
            //    The inner gameplay FSM starts in PlayingState; bouncing units / spawners run on
            //    their own MonoBehaviours — the FSM is the orchestration backbone for later states.
            appStateMachine = new StateMachine();
            appStateMachine.Push(new BootState(appStateMachine, saveSystem, metaContext));

            //    The states are handed runManager, NOT the RunContext: they are built once here and live
            //    through every prestige, so a captured context would be the finished run's.
            var gameplayFsm = new StateMachine();
            var playingState = new PlayingState(gameplayFsm, runManager, prestigeSystem);
            var buildModeState = new BuildModeState(spawnSystem, placementController);
            var perkSelectionState = new PerkSelectionState(gameplayFsm, perkSystem, perkScreen, runManager, playingState);
            appStateMachine.ChangeState(new GameplayContainerState(gameplayFsm, playingState, buildModeState,
                                                                   perkSelectionState, buildModeCooldown,
                                                                   ageSystem, ageSequencer, resourceSystem,
                                                                   islandSystem, zoneScreen, runManager));

            // Exit-to-menu hotkey (GameHotkeys → ExitToMenuRequestedEvent). Owned here because the app FSM
            // and runManager live here; leaving the container restores timeScale via its Exit().
            EventBus<ExitToMenuRequestedEvent>.Subscribe(OnExitToMenu);
        }

        // The hotkey and the event stay wired, but the transition is DECLINED while MainMenuState is
        // still a stub. Entering it used to strand the player: the container's Exit() drops the build-mode
        // and age-advance subscriptions, the menu draws nothing and listens for nothing, and gameplay
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
