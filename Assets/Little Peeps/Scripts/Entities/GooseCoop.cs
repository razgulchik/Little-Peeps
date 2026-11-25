using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // A building with a fixed flock. A working hit by anyone the coop is for (releasedBy) lets every ready
    // bird out, each through its own random free side, the same exits a house launches through. Out on the
    // island a bird is a moving ResourceSource: the unit that catches it is paid and the bird is back in the
    // pen, where it rests and then waits for the next release. Hitting the coop more cannot replace birds
    // that are still out, which is what the flock being fixed means.
    //
    // A hit that finds nobody ready, or a coop sealed in on every side, lets nothing out and reads Blocked.
    // Registered with SpawnSystem via IStructureSpawner, like the other spawners.
    [RequireComponent(typeof(Structure))]
    public class GooseCoop : MonoBehaviour, ICollisionEffect, IStructureSpawner
    {
        [FormerlySerializedAs("chickenPrefab")]
        [SerializeField] private GameObject goosePrefab;

        [Tooltip("Birds this coop keeps, before the DenCapacity modifier.")]
        [SerializeField, Min(1)] private int flockSize = 5;

        [Tooltip("Seconds a bird rests in the pen after it comes back. 0 = ready at once.")]
        [SerializeField, Min(0f)] private float restTime = 5f;

        [Tooltip("The professions whose working hit lets the flock out. Empty = any peep.")]
        [SerializeField] private UnitType[] releasedBy;

        [Tooltip("Played on a hit that lets birds out. Empty = the hit just knocks like a bounce.")]
        [SerializeField] private SoundDef releaseSound;

        [SerializeField] private GoosePenView pen;

        [Header("Release")]
        [Tooltip("Gap between the building's edge and the middle of the bird's body as it comes out.")]
        [SerializeField, Min(0f)] private float releaseGap = 0.3f;

        [Tooltip("Random spread on the chosen side's direction, so birds don't all leave on exact lines.")]
        [SerializeField, Min(0f)] private float releaseJitterDegrees = 25f;

        private SpawnSystem spawnSystem;
        private ResourceSystem resourceSystem;
        private IslandGrid grid;
        private StructureInstance instance;

        // The bird's own def, read off the prefab once: DenCapacity is scoped by it, so "more geese" leaves
        // the dens alone. Same as AnimalSpawner.animalDef.
        private ResourceSourceDef gooseDef;

        private readonly GooseFlock flock = new();
        private readonly List<Goose> flying = new();
        private readonly List<Vector2> exitDirs = new();   // reused per release
        private bool registered;

        // The birds out right now. Read by a sale, which sends them off in a puff.
        public IReadOnlyList<Goose> Flying => flying;

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
            var source = goosePrefab != null ? goosePrefab.GetComponentInChildren<ResourceSource>(true) : null;
            gooseDef = source != null ? source.Def : null;
        }

        private void Start()
        {
            if (goosePrefab == null) Debug.LogError($"GooseCoop on '{name}' has no goose prefab assigned.", this);
            if (pen == null) Debug.LogError($"GooseCoop on '{name}' has no pen view assigned.", this);
            if (grid == null || instance == null || instance.Def == null)
                Debug.LogError($"GooseCoop on '{name}' stands on no grid, so it has no exits. Build it through " +
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
            int size = stats != null ? stats.ApplyCount(flockSize, StatId.DenCapacity, source: gooseDef) : flockSize;
            flock.GrowTo(Mathf.Max(1, size));
            ShowPen();
        }

        private void Update()
        {
            if (flock.Tick(Time.deltaTime)) ShowPen();
        }

        // ICollisionEffect — whom the coop is for, the moment aside.
        public bool Serves(Unit unit) => unit != null && IsReleasedBy(unit.Type);

        private bool IsReleasedBy(UnitType type)
        {
            if (releasedBy == null || releasedBy.Length == 0) return true;
            for (int i = 0; i < releasedBy.Length; i++)
                if (releasedBy[i] == type) return true;
            return false;
        }

        // ICollisionEffect — a tired unit never gets here (CollisionTarget.HandleHit).
        public HitOutcome OnHit(Unit unit, CollisionTarget target)
        {
            if (!Serves(unit) || ReleaseReady() == 0) return HitOutcome.None;

            unit.MakeSound(releaseSound);
            return HitOutcome.Worked;
        }

        private int ReleaseReady()
        {
            if (goosePrefab == null || grid == null || instance == null || instance.Def == null) return 0;

            int released = 0;
            while (flock.Ready > 0
                   && StructureExits.TryPickExit(grid, instance.Cell, instance.Def.Footprint, instance.Def.border,
                                                 exitDirs, releaseGap, releaseJitterDegrees,
                                                 out Vector2 point, out Vector2 dir))
            {
                flock.TakeOut();
                LetOut(point, dir);
                released++;
            }

            if (released > 0) ShowPen();
            return released;
        }

        private void LetOut(Vector2 point, Vector2 dir)
        {
            var go = Instantiate(goosePrefab, point, Quaternion.identity);

            // An animal stands on no cells, so there is nothing for its source to wait on.
            foreach (var source in go.GetComponentsInChildren<ResourceSource>(true))
                source.Initialize(resourceSystem, spawnSystem, null);

            var goose = go.GetComponent<Goose>();
            flying.Add(goose);
            goose.Launch(this, point, dir);
        }

        // Called by a bird as it is destroyed, however it left. A no-op for the ones Teardown already took.
        public void NotifyGone(Goose goose)
        {
            if (!flying.Remove(goose)) return;

            flock.Return(restTime);
            ShowPen();
        }

        public void Puff(Vector2 at)
        {
            if (spawnSystem != null) spawnSystem.SmallPuff(at);
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
