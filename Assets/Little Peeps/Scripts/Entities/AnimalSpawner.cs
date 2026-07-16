using System.Collections.Generic;
using UnityEngine;

// Placed on a structure (stable, forest den); keeps up to maxAnimals Animal instances
// wandering in the structure's territory. Unlike Spawner's launch -> return -> rest slot
// cycle, animals never come back: a harvested one is destroyed and a replacement is spawned
// after spawnCooldown (one per cooldown while below maxAnimals). Registered with SpawnSystem
// via IStructureSpawner so build mode clears and re-warms it together with the unit spawners.
[RequireComponent(typeof(Structure))]
public class AnimalSpawner : MonoBehaviour, IStructureSpawner
{
    [SerializeField] private SpawnSystem spawnSystem;       // scene refs — runtime-injected on placement;
    [SerializeField] private ResourceSystem resourceSystem; // hand-wire only for scene-placed spawners

    [Header("Animals")]
    [SerializeField] private GameObject animalPrefab;
    [SerializeField] private int maxAnimals = 1;
    [SerializeField] private float spawnCooldown = 5f;
    [Tooltip("Territory radius in cells around the footprint; animals spawn and wander inside it.")]
    [SerializeField] private int territoryRadiusCells = 2;

    // Grid context injected on placement (same pattern as Spawner). `instance.Cell` auto-updates
    // when the structure is moved, so after a build-mode move the territory follows the building.
    // Both stay null for a scene-placed spawner, which then uses a plain circle around itself.
    private IslandGrid grid;
    private StructureInstance instance;

    private readonly List<Animal> animals = new();
    private float respawnTimer;
    private bool registered;

    private readonly List<Vector2> freeCells = new();      // reused per pick — unoccupied land in territory
    private readonly List<Vector2> occupiedCells = new();  // reused per pick — occupied-land fallback

    // Optional runtime injection (StructureSystem calls this when placing a structure at runtime).
    public void Initialize(SpawnSystem system, ResourceSystem resources, IslandGrid grid, StructureInstance instance)
    {
        spawnSystem = system;
        resourceSystem = resources;
        this.grid = grid;
        this.instance = instance;
    }

    private void Start()
    {
        if (animalPrefab == null)
        {
            Debug.LogError($"AnimalSpawner on '{name}' has no animal prefab assigned.", this);
            return;
        }
        if (resourceSystem == null)
            Debug.LogError($"AnimalSpawner on '{name}' has no ResourceSystem assigned.", this);
        if (spawnSystem == null)
            Debug.LogWarning($"AnimalSpawner on '{name}' has no SpawnSystem — it won't reset/re-warm with build mode.", this);

        Warmup();
    }

    // IStructureSpawner — placement and build-mode exit: fill the territory up to maxAnimals at
    // once. Instant refill is not farmable: the animal COUNT is capped, and only post-harvest
    // replacements are rate-limited (by spawnCooldown in Update) — toggling build mode swaps
    // animals one-for-one, it never mints extra ones.
    public void Warmup()
    {
        if (animalPrefab == null) return;

        if (!registered && spawnSystem != null)
        {
            spawnSystem.RegisterSpawner(this);
            registered = true;
        }

        // Placed during build mode: register only. Build-mode exit runs WarmupAllSpawners with the
        // flag already cleared, which fills the territory then — so animals appear when the player
        // leaves build mode, not the moment the den is dropped.
        if (spawnSystem != null && spawnSystem.IsBuildMode) return;

        maxAnimals = Mathf.Max(1, maxAnimals);
        while (animals.Count < maxAnimals)
            if (!SpawnAnimal()) break;   // no valid spot right now — Update keeps retrying on cooldown

        respawnTimer = spawnCooldown;
    }

    // IStructureSpawner — build-mode enter: remove every live animal (the animal counterpart of
    // the units' despawn-all; animals aren't pooled units, so we destroy them ourselves).
    public void ResetForBuildMode()
    {
        for (int i = 0; i < animals.Count; i++)
            if (animals[i] != null) Destroy(animals[i].gameObject);
        animals.Clear();
    }

    private void Update()
    {
        // No respawns while building. Explicit (not just relying on timeScale=0): a den placed in
        // build mode has respawnTimer==0 until its first real Warmup on exit, so without this the
        // dt==0 frame would slip past the timer check and spawn an animal mid-build.
        if (spawnSystem != null && spawnSystem.IsBuildMode) return;

        if (animals.Count >= maxAnimals) return;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer > 0f) return;

        SpawnAnimal();
        respawnTimer = spawnCooldown;   // one replacement per cooldown, even when several are missing
    }

    // Called by a depleted Animal right before it destroys itself: free its slot so Update
    // starts counting down toward the replacement.
    public void NotifyHarvested(Animal animal)
    {
        animals.Remove(animal);
    }

    private bool SpawnAnimal()
    {
        if (!TryPickPointInTerritory(out Vector2 pos)) return false;

        var go = Instantiate(animalPrefab, pos, Quaternion.identity);
        var animal = go.GetComponentInChildren<Animal>(true);
        if (animal == null)
        {
            Debug.LogError($"AnimalSpawner on '{name}': prefab '{animalPrefab.name}' has no Animal component.", this);
            Destroy(go);
            return false;
        }

        animal.Initialize(resourceSystem, this);
        foreach (var wander in go.GetComponentsInChildren<AnimalWander>(true))
            wander.Initialize(this);

        animals.Add(animal);
        return true;
    }

    // Random point inside the territory: a land cell within territoryRadiusCells of the footprint
    // (the footprint itself excluded). Prefers unoccupied cells but falls back to occupied land —
    // a den sitting in a forest is surrounded by tree cells, and animals pass through everything
    // anyway (kinematic body). Also used by AnimalWander for destinations, so spawn spots and
    // wander targets follow one rule. Without grid context: a plain circle around the spawner.
    public bool TryPickPointInTerritory(out Vector2 point)
    {
        if (grid == null || instance == null || instance.Def == null)
        {
            point = (Vector2)transform.position + Random.insideUnitCircle * territoryRadiusCells;
            return true;
        }

        CollectTerritoryCells();
        var pool = freeCells.Count > 0 ? freeCells : occupiedCells;
        if (pool.Count == 0) { point = default; return false; }

        // Jitter inside the chosen cell so animals don't all stand on exact cell centers.
        point = pool[Random.Range(0, pool.Count)]
              + Random.insideUnitCircle * (grid.CellSize * 0.35f);
        return true;
    }

    // World-space passability probe for AnimalWander's look-ahead: off-island is always
    // blocked, an occupied cell only when its structure is flagged impassable (trees and
    // fields stay walk-through, so forest animals keep roaming among the trees). A gridless
    // (scene-placed) spawner has nothing to check against and blocks nothing.
    public bool IsBlocked(Vector2 worldPos)
    {
        if (grid == null) return false;

        var cell = grid.GetCell(grid.WorldToGrid(worldPos));
        if (cell == null) return true;   // off-island — the water's edge
        return cell.occupant != null && cell.occupant.Def != null && cell.occupant.Def.impassable;
    }

    private void CollectTerritoryCells()
    {
        freeCells.Clear();
        occupiedCells.Clear();

        Vector2Int o = instance.Cell;
        Vector2Int s = instance.Def.size;
        int r = Mathf.Max(1, territoryRadiusCells);

        for (int x = o.x - r; x < o.x + s.x + r; x++)
            for (int y = o.y - r; y < o.y + s.y + r; y++)
            {
                bool inFootprint = x >= o.x && x < o.x + s.x && y >= o.y && y < o.y + s.y;
                if (inFootprint) continue;

                var coord = new Vector2Int(x, y);
                var cell = grid.GetCell(coord);
                if (cell == null) continue;   // off-island

                // Our own border ring counts as free — it's claimed spacing, visually empty.
                bool free = cell.occupant == null || cell.occupant == instance;
                (free ? freeCells : occupiedCells).Add(grid.GridToWorld(coord));
            }
    }

    private void OnDestroy()
    {
        ResetForBuildMode();   // destroy any remaining animals so they don't outlive their den
        if (registered && spawnSystem != null)
            spawnSystem.UnregisterSpawner(this);
    }
}
