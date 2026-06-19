using UnityEngine;

// Entry point — attach to the Bootstrap GameObject in the Bootstrap scene.
//
// Required scene hierarchy:
//   Bootstrap   [this component + all [SerializeField] system components below, or on child objects]
//   Island      [IslandSystem + Tilemap/TilemapRenderer children]
//   Camera      [Camera component; add a CameraController when implementing camera movement]
//   UI (Canvas) [ResourceUI × 6, AgeUI, PerkSelectionUI as child GameObjects]
//   Pier        [Pier component + CircleCollider2D with isTrigger = true]
//
// Initialization (all in Awake — order-independent, see note on Awake below):
//   1. Application.runInBackground
//   2. Load MetaContext from disk (RunContext is owned by RunManager)
//   3. Wire run-independent systems (runManager / prestigeSystem)
//   4. RunManager.StartNewRun → wire run-dependent systems (tapSystem / perkSelectionUI)
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
    [SerializeField] private AgeSequencer ageSequencer;
    [SerializeField] private PerkSystem perkSystem;
    [SerializeField] private PrestigeSystem prestigeSystem;
    [SerializeField] private RunManager runManager;
    [SerializeField] private SaveSystem saveSystem;

    [Header("UI")]
    [SerializeField] private PerkSelectionUI perkSelectionUI;

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
        runManager.Initialize(metaContext);
        prestigeSystem.Initialize(metaContext);

        // 3. Start the first run: RunManager creates the RunContext, seeds resources
        //    (via ResourceSystem.Initialize) and asks IslandSystem to generate the island.
        runManager.StartNewRun();
        RunContext run = runManager.CurrentRun;

        // 4. Wire run-dependent systems.
        tapSystem.Initialize(run);
        if (perkSelectionUI != null) perkSelectionUI.Initialize(perkSystem, run); // UI optional this milestone

        // 5. App FSM. Boot is synchronous for now, so we enter Boot and advance straight to
        //    Gameplay (when async loading lands, BootState.Tick will own this transition).
        //    The inner gameplay FSM starts in PlayingState; bouncing units / spawners run on
        //    their own MonoBehaviours — the FSM is the orchestration backbone for later states.
        appStateMachine = new StateMachine();
        appStateMachine.Push(new BootState(appStateMachine, saveSystem, metaContext));

        var gameplayFsm = new StateMachine();
        var playingState = new PlayingState(gameplayFsm, run);
        var buildModeState = new BuildModeState(spawnSystem, placementController);
        appStateMachine.ChangeState(new GameplayContainerState(gameplayFsm, playingState, buildModeState, buildModeCooldown));
    }

    private void Update()
    {
        appStateMachine.Tick();
    }
}
