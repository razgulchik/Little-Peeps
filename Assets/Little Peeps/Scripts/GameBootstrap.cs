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
// Initialization order (Awake → Start):
//   1. Application.runInBackground
//   2. Load or create contexts (MetaContext from disk, RunContext + SessionContext fresh)
//   3. Wire contexts into systems
//   4. Create App FSM → push BootState
//   5. (Start) StartNewRun + transition to MainMenuState
public class GameBootstrap : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private UnitPool unitPool;
    [SerializeField] private UnitSystem unitSystem;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private BuildingSystem buildingSystem;
    [SerializeField] private DragController dragController;
    [SerializeField] private TapSystem tapSystem;
    [SerializeField] private AgeSequencer ageSequencer;
    [SerializeField] private PerkSystem perkSystem;
    [SerializeField] private PrestigeSystem prestigeSystem;
    [SerializeField] private RunManager runManager;
    [SerializeField] private SaveSystem saveSystem;

    [Header("UI")]
    [SerializeField] private PerkSelectionUI perkSelectionUI;

    private RunContext runContext;
    private MetaContext metaContext;
    private SessionContext sessionContext;
    private StateMachine appStateMachine;

    private void Awake()
    {
        Application.runInBackground = true;

        // 1. Create / load contexts
        metaContext = saveSystem.Load();
        runContext = new RunContext();
        sessionContext = new SessionContext();

        // 2. Wire contexts into systems
        // TODO: resourceSystem.Initialize(runContext)
        // TODO: sessionContext.unitPool = unitPool
        // TODO: runManager.Initialize(metaContext)
        // TODO: prestigeSystem.Initialize(metaContext)
        // TODO: dragController.Initialize(sessionContext)
        // TODO: tapSystem.Initialize(runContext)
        // TODO: perkSelectionUI.Initialize(perkSystem, runContext)

        // 3. Build App FSM
        appStateMachine = new StateMachine();
        // TODO: appStateMachine.Push(new BootState(appStateMachine, saveSystem, metaContext))
    }

    private void Start()
    {
        // TODO: runManager.StartNewRun() — applies GlobalUpgrade multipliers, seeds island
        // TODO: appStateMachine.ChangeState(new MainMenuState(appStateMachine, runManager))
    }

    private void Update()
    {
        // TODO: appStateMachine.Tick()
    }
}
