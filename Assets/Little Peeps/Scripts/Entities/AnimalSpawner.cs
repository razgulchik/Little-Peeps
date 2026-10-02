using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Placed on a structure (stable, forest den); keeps up to maxAnimals Animal instances (DenCapacity
    // applied, see ResolveMaxAnimals)
    // wandering in the structure's territory. Each one comes OUT of the building through a free side —
    // the house's exits (StructureExits) — and walks off into the territory; a building boxed in on
    // every side lets nobody out until a side opens again. Unlike Spawner's launch -> return -> rest slot
    // cycle, animals never come back: one whose source despawns (boar, fox) is destroyed and a
    // replacement is let out after spawnCooldown (one per cooldown while below maxAnimals). One that
    // regrows (a shorn alpaca) stays out and keeps its slot. Registered with SpawnSystem
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
        [Tooltip("Territory radius in cells around the footprint; animals wander inside it.")]
        [SerializeField] private int territoryRadiusCells = 2;

        [Header("Release")]
        [Tooltip("Gap between the building's edge and the animal's feet as it comes out.")]
        [SerializeField, Min(0f)] private float releaseGap = 0.3f;

        [Tooltip("Random spread on the chosen side's direction, so animals don't all come out on exact lines.")]
        [SerializeField, Min(0f)] private float releaseJitterDegrees = 12f;

        // Grid context injected on placement (same pattern as Spawner). `instance.Cell` auto-updates
        // when the structure is moved, so after a build-mode move the territory follows the building.
        // Both stay null for a scene-placed spawner, which then uses a plain circle around itself.
        private IslandGrid grid;
        private StructureInstance instance;

        // The animal's own def, read off the prefab once: DenCapacity is scoped by it, which is what lets
        // "more boars" leave the stable's alpacas alone. Null when the prefab carries no ResourceSource —
        // the query then reaches only the modifiers authored for every den.
        private ResourceSourceDef animalDef;

        private readonly List<Animal> animals = new();
        private float respawnTimer;

        // The animals out right now. Read by the island rise, which pops a new den's animals up with it.
        public IReadOnlyList<Animal> Animals => animals;
        private bool registered;

        private readonly List<Vector2> freeCells = new();      // reused per pick — unoccupied land in territory
        private readonly List<Vector2> occupiedCells = new();  // reused per pick — occupied-land fallback
        private readonly List<Vector2> exitDirs = new();       // reused per release — directions toward free sides

        // Optional runtime injection (StructureSystem calls this when placing a structure at runtime).
        public void Initialize(SpawnSystem system, ResourceSystem resources, IslandGrid grid, StructureInstance instance)
        {
            spawnSystem = system;
            resourceSystem = resources;
            this.grid = grid;
            this.instance = instance;
        }

        private void Awake()
        {
            var source = animalPrefab != null ? animalPrefab.GetComponentInChildren<ResourceSource>(true) : null;
            animalDef = source != null ? source.Def : null;
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

        // IStructureSpawner — placement and build-mode exit: let out up to maxAnimals at once, each
        // through its own random free side. Instant refill is not farmable: the animal COUNT is capped,
        // and only post-harvest replacements are rate-limited (by spawnCooldown in Update) — toggling
        // build mode swaps animals one-for-one, it never mints extra ones.
        public void Warmup()
        {
            if (animalPrefab == null) return;

            if (!registered && spawnSystem != null)
            {
                spawnSystem.RegisterSpawner(this);
                registered = true;
            }

            // Placed during build mode: register only. Build-mode exit runs WarmupAllSpawners with the
            // flag already cleared, which lets the animals out then — so they appear when the player
            // leaves build mode, not the moment the den is dropped.
            if (spawnSystem != null && spawnSystem.IsBuildMode) return;

            int max = ResolveMaxAnimals();
            while (animals.Count < max)
                if (!SpawnAnimal()) break;   // sealed in right now — Update keeps retrying on cooldown

            respawnTimer = spawnCooldown;
        }

        // IStructureSpawner — nothing here is materialised from the stat sheet: DenCapacity is read at
        // every check (ResolveMaxAnimals), so a sheet change has nothing to refresh.
        public void RefreshFromStats() { }

        // Animals this den keeps out: maxAnimals with the run modifier applied. Read at the point of use,
        // like MarketVisitHits, so a perk bought mid-run needs no push — the next Update sees the higher
        // cap and lets the extra animal out one spawnCooldown later, through a free side like any
        // replacement. A cap LOWERED below the animals already out removes nobody; it only holds back
        // replacements until the count falls under it. Never below one: a den with nothing in it is not
        // a den, whatever a penalty says.
        private int ResolveMaxAnimals()
        {
            var stats = spawnSystem != null ? spawnSystem.Stats : null;
            int resolved = stats != null
                ? stats.ApplyCount(maxAnimals, StatId.DenCapacity, source: animalDef)
                : maxAnimals;
            return Mathf.Max(1, resolved);
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

            if (animals.Count >= ResolveMaxAnimals()) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer > 0f) return;

            SpawnAnimal();
            respawnTimer = spawnCooldown;   // one replacement per cooldown, even when several are missing
        }

        // Called by an Animal as it is destroyed: free its slot so Update starts counting down toward
        // the replacement. A no-op for the ones ResetForBuildMode already cleared out of the list.
        public void NotifyGone(Animal animal)
        {
            animals.Remove(animal);
        }

        private bool SpawnAnimal()
        {
            if (!TryPickReleasePoint(out Vector2 pos, out Vector2 dir)) return false;

            var go = Instantiate(animalPrefab, pos, Quaternion.identity);
            var animal = go.GetComponentInChildren<Animal>(true);
            if (animal == null)
            {
                Debug.LogError($"AnimalSpawner on '{name}': prefab '{animalPrefab.name}' has no Animal component.", this);
                Destroy(go);
                return false;
            }

            animal.Initialize(this);
            // Same injection StructureSystem gives a placed structure's sources.
            foreach (var source in go.GetComponentsInChildren<ResourceSource>(true))
                source.Initialize(resourceSystem);
            foreach (var wander in go.GetComponentsInChildren<AnimalWander>(true))
                wander.Initialize(this, dir);

            animals.Add(animal);
            return true;
        }

        // Where a new animal comes out: just outside the building on a random free side, the same exits
        // a house launches through (StructureExits — land, no fence on that edge, no solid neighbour; a
        // field or a bush leaves the side open). False = sealed in on every side; the caller lets nobody
        // out and Update tries again after the next cooldown. The generator leaves every den at least
        // one free side (IslandContent's access rule), so only the player can close the last one. `dir`
        // is the way out, for AnimalWander's first walk. Without grid context (a scene-placed spawner):
        // a random point in the territory circle, and no direction.
        private bool TryPickReleasePoint(out Vector2 point, out Vector2 dir)
        {
            if (grid == null || instance == null || instance.Def == null)
            {
                dir = Vector2.zero;
                return TryPickPointInTerritory(out point);
            }

            var footprint = instance.Def.Footprint;
            if (!StructureExits.TryPickDirection(grid, instance.Cell, footprint, instance.Def.border,
                                                 exitDirs, releaseJitterDegrees, out dir))
            {
                point = default;
                return false;
            }

            point = StructureExits.ExitPoint(grid, instance.Cell, footprint, dir, releaseGap);
            return true;
        }

        // Random point inside the territory: a land cell within territoryRadiusCells of the footprint
        // (the footprint itself excluded). Prefers unoccupied cells but falls back to occupied land —
        // a den sitting in a forest is surrounded by tree cells, and animals pass through everything
        // anyway (kinematic body). AnimalWander's destinations, and the spawn spot of a scene-placed
        // spawner. Without grid context: a plain circle around the spawner.
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
        // blocked, an occupied cell only when its structure is flagged animalsAvoid (trees and
        // fields stay walk-through, so forest animals keep roaming among the trees). A gridless
        // (scene-placed) spawner has nothing to check against and blocks nothing.
        public bool IsBlocked(Vector2 worldPos)
        {
            if (grid == null) return false;

            var cell = grid.GetCell(grid.WorldToGrid(worldPos));
            if (cell == null) return true;   // off-island — the water's edge
            return cell.occupant != null && cell.occupant.Def != null && cell.occupant.Def.animalsAvoid;
        }

        private void CollectTerritoryCells()
        {
            freeCells.Clear();
            occupiedCells.Clear();

            Vector2Int o = instance.Cell;
            var footprint = instance.Def.Footprint;
            Vector2Int s = footprint.Size;
            int r = Mathf.Max(1, territoryRadiusCells);

            // The ring `r` cells deep around the SHAPE (Footprint.Claims), minus the shape itself: for a
            // box that is the box grown by r, for an L the ring hugs the L and fills its notch.
            for (int x = -r; x < s.x + r; x++)
                for (int y = -r; y < s.y + r; y++)
                {
                    if (footprint.Contains(x, y)) continue;
                    if (!footprint.Claims(x, y, r)) continue;

                    var coord = new Vector2Int(o.x + x, o.y + y);
                    var cell = grid.GetCell(coord);
                    if (cell == null) continue;   // off-island

                    // Our own border ring counts as free — it's claimed spacing, visually empty.
                    bool free = cell.occupant == null || cell.occupant == instance;
                    (free ? freeCells : occupiedCells).Add(grid.GridToWorld(coord));
                }
        }

        // IStructureSpawner — hand everything back synchronously, before the object is destroyed.
        // See the interface for why the deferred OnDestroy is too late during a run teardown.
        public void Teardown() => Unregister();

        private void OnDestroy() => Unregister();

        // Idempotent: `registered` is cleared first, so the OnDestroy following a Teardown is a no-op.
        // ResetForBuildMode is safe to repeat on its own (it empties `animals`), but the unregistration
        // is not — SpawnSystem would keep re-scanning a list this spawner already left.
        private void Unregister()
        {
            ResetForBuildMode();   // destroy any remaining animals so they don't outlive their den
            if (!registered) return;
            registered = false;
            if (spawnSystem != null) spawnSystem.UnregisterSpawner(this);
        }
    }
}
