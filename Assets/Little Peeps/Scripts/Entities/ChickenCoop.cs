using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A building with a fixed flock. A working hit by the releasing profession (a farmer) lets every ready
    // bird out, each through its own random free side, the same exits a house launches through. Out on the
    // island a bird is a moving ResourceSource: the unit that catches it is paid and the bird is back in the
    // pen, where it rests and then waits for the next release. Hitting the coop more cannot replace birds
    // that are still out, which is what the flock being fixed means.
    //
    // A hit that finds nobody ready, or a coop sealed in on every side, lets nothing out and reads Blocked.
    // Registered with SpawnSystem via IStructureSpawner, like the other spawners.
    [RequireComponent(typeof(Structure))]
    public class ChickenCoop : MonoBehaviour, ICollisionEffect, IStructureSpawner
    {
        [SerializeField] private GameObject chickenPrefab;

        [Tooltip("Birds this coop keeps, before the DenCapacity modifier.")]
        [SerializeField, Min(1)] private int flockSize = 5;

        [Tooltip("Seconds a bird rests in the pen after it comes back. 0 = ready at once.")]
        [SerializeField, Min(0f)] private float restTime = 5f;

        [Tooltip("The profession whose working hit lets the flock out.")]
        [SerializeField] private UnitType releasedBy = UnitType.Farmer;

        [Tooltip("Played on a hit that lets birds out. Empty = the hit just knocks like a bounce.")]
        [SerializeField] private SoundDef releaseSound;

        [SerializeField] private ChickenPenView pen;

        [Header("Release")]
        [Tooltip("Gap between the building's edge and the bird as it comes out.")]
        [SerializeField, Min(0f)] private float releaseGap = 0.3f;

        [Tooltip("Random spread on the chosen side's direction, so birds don't all leave on exact lines.")]
        [SerializeField, Min(0f)] private float releaseJitterDegrees = 25f;

        private SpawnSystem spawnSystem;
        private ResourceSystem resourceSystem;
        private IslandGrid grid;
        private StructureInstance instance;

        // The bird's own def, read off the prefab once: DenCapacity is scoped by it, so "more geese" leaves
        // the dens alone. Same as AnimalSpawner.animalDef.
        private ResourceSourceDef chickenDef;

        private readonly ChickenFlock flock = new();
        private readonly List<Chicken> flying = new();
        private readonly List<Vector2> exitDirs = new();   // reused per release
        private bool registered;

        // The birds out right now. Read by a sale, which sends them off in a puff.
        public IReadOnlyList<Chicken> Flying => flying;

        // Runtime injection (StructureSystem calls this when placing a structure).
        public void Initialize(SpawnSystem system, ResourceSystem resources, IslandGrid grid, StructureInstance instance)
        {
            spawnSystem = system;
            resourceSystem = resources;
            this.grid = grid;
            this.instance = instance;
        }

        private void Awake()
        {
            var source = chickenPrefab != null ? chickenPrefab.GetComponentInChildren<ResourceSource>(true) : null;
            chickenDef = source != null ? source.Def : null;
        }

        private void Start()
        {
            if (chickenPrefab == null) Debug.LogError($"ChickenCoop on '{name}' has no chicken prefab assigned.", this);
            if (pen == null) Debug.LogError($"ChickenCoop on '{name}' has no pen view assigned.", this);
            if (grid == null || instance == null || instance.Def == null)
                Debug.LogError($"ChickenCoop on '{name}' stands on no grid, so it has no exits. Build it through " +
                               "StructureSystem, not by hand in the scene.", this);

            Warmup();
        }

        // IStructureSpawner — placement: register once and fill the pen.
        public void Warmup()
        {
            if (!registered && spawnSystem != null)
            {
                spawnSystem.RegisterSpawner(this);
                registered = true;
            }
            RefreshFromStats();
        }

        // IStructureSpawner — the sheet changed: a bigger flock arrives ready in the pen. Never below one,
        // a coop with nothing in it is not a coop, whatever a penalty says.
        public void RefreshFromStats()
        {
            var stats = spawnSystem != null ? spawnSystem.Stats : null;
            int size = stats != null ? stats.ApplyCount(flockSize, StatId.DenCapacity, source: chickenDef) : flockSize;
            flock.GrowTo(Mathf.Max(1, size));
            ShowPen();
        }

        private void Update()
        {
            if (flock.Tick(Time.deltaTime)) ShowPen();
        }

        // ICollisionEffect — whom the coop is for, the moment aside.
        public bool Serves(Unit unit) => unit != null && unit.Type == releasedBy;

        // ICollisionEffect — a tired unit never gets here (CollisionTarget.HandleHit).
        public HitOutcome OnHit(Unit unit, CollisionTarget target)
        {
            if (!Serves(unit) || ReleaseReady() == 0) return HitOutcome.None;

            unit.MakeSound(releaseSound);
            return HitOutcome.Worked;
        }

        private int ReleaseReady()
        {
            if (chickenPrefab == null || grid == null || instance == null || instance.Def == null) return 0;

            int released = 0;
            while (flock.Ready > 0
                   && StructureExits.TryPickDirection(grid, instance.Cell, instance.Def.Footprint, instance.Def.border,
                                                      exitDirs, releaseJitterDegrees, out Vector2 dir))
            {
                flock.TakeOut();
                LetOut(dir);
                released++;
            }

            if (released > 0) ShowPen();
            return released;
        }

        private void LetOut(Vector2 dir)
        {
            Vector2 at = StructureExits.ExitPoint(grid, instance.Cell, instance.Def.Footprint, dir, releaseGap);
            var go = Instantiate(chickenPrefab, at, Quaternion.identity);

            // An animal stands on no cells, so there is nothing for its source to wait on.
            foreach (var source in go.GetComponentsInChildren<ResourceSource>(true))
                source.Initialize(resourceSystem, spawnSystem, null);

            var chicken = go.GetComponent<Chicken>();
            flying.Add(chicken);
            chicken.Launch(this, dir);
        }

        // Called by a bird as it is destroyed, however it left. A no-op for the ones Teardown already took.
        public void NotifyGone(Chicken chicken)
        {
            if (!flying.Remove(chicken)) return;

            flock.Return(restTime);
            ShowPen();
        }

        public void Puff(Vector2 at)
        {
            if (spawnSystem != null) spawnSystem.Puff(at);
        }

        private void ShowPen()
        {
            if (pen != null) pen.Show(flock.Ready);
        }

        // IStructureSpawner — hand everything back synchronously, see the interface.
        public void Teardown() => Unregister();

        private void OnDestroy() => Unregister();

        private void Unregister()
        {
            for (int i = 0; i < flying.Count; i++)
                if (flying[i] != null) Destroy(flying[i].gameObject);
            flying.Clear();
            flock.ForgetOut();

            if (!registered) return;
            registered = false;
            if (spawnSystem != null) spawnSystem.UnregisterSpawner(this);
        }
    }
}
