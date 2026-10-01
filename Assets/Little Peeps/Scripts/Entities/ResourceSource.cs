using System;
using UnityEngine;

namespace LittlePeeps
{
    // The resource on anything that pays when a worker hits it: grants def.resource each time an allowed
    // worker hits the host CollisionTarget, and after def.hitsToDeplete paying hits does what def.depletion
    // says — regrow in place, despawn, or never run out at all. Config lives in the ResourceSourceDef asset;
    // per-instance state lives here. The one harvest component for every source: a natural node
    // (tree/wheat/stone), a building-source (Forge/Market, Never) and an animal (alpaca/boar/fox,
    // next to Animal + AnimalWander) alike.
    //
    // Two states (Regrow sources only):
    //   Ready     — ripe/grown, harvestable.
    //   Harvested — used up, regrowing. Its colliders are off unless the def keeps the body (a shorn
    //               alpaca still bumps into units, it just pays nothing). After def.regrowTime it
    //               returns to Ready.
    //
    // What the states LOOK like is not this component's business: each kind of source has its own view
    // next to it — ResourceSourceView for nodes (ready and harvested roots, the reap fade), ForgeHeatView
    // for the forge, and so on — which reads IsReady and listens to Depleted / Regrown. A source with no
    // view simply never changes its look.
    //
    // A building with rules of its own (the forge's heat, the paddy's crop, the mill's store) is an add-on
    // next to this, never a second payer: an IHitGate says WHETHER a hit pays, an IYieldScale HOW MUCH, and
    // Paid tells it the hit did. The def, the payout and the pickup effect stay here, one place for all.
    //
    // Despawn destroys the whole object on the hit that uses it up. Only a den's animal may do that:
    // the den (AnimalSpawner, told through Animal) replaces it, while a structure would leave its grid
    // cells taken by nothing — Start refuses that combination loudly.
    [RequireComponent(typeof(CollisionTarget))]
    public class ResourceSource : MonoBehaviour, ICollisionEffect
    {
        private enum State { Ready, Harvested }

        [SerializeField] private ResourceSourceDef def;
        [SerializeField] private ResourceSystem resourceSystem; // scene ref — can't live in the SO

        [Header("Harvest VFX")]
        [Tooltip("Where the pickup effect and the floating number leave from. Empty = this transform. " +
                 "Wheat's Visual root is offset from the prefab root, so without an anchor the ear " +
                 "would fly out of the cell's pivot rather than the middle of the field.")]
        [SerializeField] private Transform fxAnchor;

        private CollisionTarget host;
        private IYieldScale[] yieldScales;
        private int hitsLeft;
        private State state = State.Ready;
        private float respawnTimer;

        // A hit has just paid — after the credit, before any depletion. What an add-on settles its own
        // state on (the mill empties its store), since only here is it certain the hit paid.
        public event Action Paid;

        // Used up (Regrow only) — fired on the hit that does it, so a view's reaction lands on that frame.
        public event Action Depleted;

        // Back to Ready after the regrow time.
        public event Action Regrown;

        public ResourceSourceDef Def => def;

        // Harvestable right now. Always true for Never and Despawn sources.
        public bool IsReady => state == State.Ready;

        private void Awake()
        {
            host = GetComponent<CollisionTarget>();
            // Own GameObject only, not children: a scale belongs to the source it sits on, and in a
            // composite prefab (forest) each child tree is its own source with its own scales.
            yieldScales = GetComponents<IYieldScale>();
            if (def != null) hitsLeft = def.hitsToDeplete;
        }

        // Optional runtime injection (StructureSystem calls this when placing a structure at runtime,
        // since a prefab can't serialize a reference to a scene system). Mirrors Spawner.Initialize.
        public void Initialize(ResourceSystem system)
        {
            resourceSystem = system;
        }

        // The run's stat sheet, for add-ons on this source that resolve their own numbers (ForgeHeat).
        // Null before injection or outside a run — callers fall back to their base value.
        public RunStats Stats => resourceSystem != null ? resourceSystem.Stats : null;

        // Whether a worker of this type gets paid here right now. The def decides who, the state decides
        // when — a source that keeps its body while regrowing still takes hits, and none of them pay.
        // Exposed so a gate on this object can tell a paying hit from a stray bounce BEFORE OnHit
        // settles it.
        public bool Accepts(UnitType type) => def != null && state == State.Ready && def.TryGetYield(type, out _);

        private void Start()
        {
            if (def == null)
                Debug.LogError($"ResourceSource on '{name}' has no ResourceSourceDef assigned.", this);
            if (resourceSystem == null)
                Debug.LogError($"ResourceSource on '{name}' has no ResourceSystem assigned.", this);

            if (def != null && def.depletion == Depletion.Despawn && GetComponentInParent<Structure>(true) != null)
                Debug.LogError($"ResourceSource on '{name}': '{def.name}' is set to Despawn, but this is a " +
                               "structure — destroying it would leave its grid cells taken. Despawn is for " +
                               "a den's animals; use Regrow here.", this);
        }

        // ICollisionEffect — dispatched by CollisionTarget.HandleHit when a unit hits the host.
        public void OnHit(Unit unit, CollisionTarget target)
        {
            if (state == State.Harvested || def == null || resourceSystem == null || unit == null) return;
            if (!def.TryGetYield(unit.Type, out float amount)) return;

            // Add-on scales (ForgeHeat) multiply the BASE amount, so they sit under the run's modifiers
            // exactly like a bigger authored yield would — see IYieldScale.
            for (int i = 0; i < yieldScales.Length; i++) amount *= yieldScales[i].Factor(unit);

            // Through the production gateway: the base amount is scaled by the worker's yield modifier
            // and the global production multiplier before being credited. The def goes along because it
            // is the yield modifier's source scope, not just where the ResourceType came from.
            resourceSystem.AddHarvest(def, unit.Type, amount, FxOrigin);
            Paid?.Invoke();

            if (def.depletion == Depletion.Never) return;
            if (--hitsLeft > 0) return;

            // Despawn: gone for good. The feedback above was already sent with the position, so it
            // outlives the node; the den hears about the loss from Animal.OnDestroy.
            if (def.depletion == Depletion.Despawn) Destroy(gameObject);
            else Deplete();
        }

        // Where harvest feedback leaves from. Authored in the prefab so art decides it, not code.
        private Vector3 FxOrigin => fxAnchor != null ? fxAnchor.position : transform.position;

        private void Update()
        {
            if (state != State.Harvested) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
        }

        // Regrow delay with the run modifier applied. The stats sheet is asked for at the point of use,
        // never cached: it belongs to the run, and a node placed in one run outlives it. A perk that
        // speeds regrowth is a NEGATIVE percent — this is the delay in seconds, not a rate. Scoped by
        // the def, so "trees regrow faster" leaves wheat alone; a modifier with no source hits all.
        private float ResolveRespawnTime()
        {
            var stats = resourceSystem != null ? resourceSystem.Stats : null;
            return stats != null
                ? stats.Apply(def.regrowTime, StatId.SourceRespawn, source: def)
                : def.regrowTime;
        }

        // Harvested: used up until it regrows. The colliders go off unless the def keeps the body — then
        // units still bump into it (or cross its trigger) and OnHit, gated on the state, pays nothing.
        private void Deplete()
        {
            state = State.Harvested;
            respawnTimer = ResolveRespawnTime();
            if (!def.keepBodyWhileDepleted) host.SetColliderEnabled(false);
            Depleted?.Invoke();
        }

        // Ready again: regrown, harvestable.
        private void Respawn()
        {
            state = State.Ready;
            hitsLeft = def.hitsToDeplete;
            if (!def.keepBodyWhileDepleted) host.SetColliderEnabled(true);
            Regrown?.Invoke();
        }
    }
}
