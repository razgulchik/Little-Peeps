using UnityEngine;

// Resource node behaviour: grants def.resource each time an allowed worker hits the host
// CollisionTarget, depletes after def.hitsBeforeDespawn hits, then respawns after
// def.respawnTime. Config lives in the ResourceSourceDef asset; per-instance state lives here.
// Attach to a natural node (tree/wheat/stone) or a building-source (Forge/Church, infinite def).
//
// Two visual states (skipped for infinite sources):
//   Ready     — ripe/grown, harvestable; shows readyRoot.
//   Harvested — used up, collider off, regrowing; shows harvestedRoot. After def.respawnTime
//               it returns to Ready.
// Each root is fully configured in the prefab (its own SpriteRenderer + Sorting Layer + pivot),
// so a tall Ready node (wheat/tree) can Y-sort against passing units while the flat Harvested
// node sits on a lower layer that units always walk over. Infinite sources (Forge/Church) keep
// their single visual and leave both roots untouched.
//
// swapStateVisuals controls how the two roots are composited:
//   off (default base) — harvestedRoot is the always-on background base; readyRoot is an overlay
//                        on top that switches off once harvested. Not mutually exclusive.
//   on                 — mutually exclusive swap: exactly one root is shown for the current state.
// Gameplay (collider toggle, deplete, respawn) is identical either way; only the visual differs.
[RequireComponent(typeof(CollisionTarget))]
public class ResourceSource : MonoBehaviour, ICollisionEffect
{
    private enum State { Ready, Harvested }

    [SerializeField] private ResourceSourceDef def;
    [SerializeField] private ResourceSystem resourceSystem; // scene ref — can't live in the SO

    [Header("State visuals (leave empty for infinite sources)")]
    [SerializeField] private GameObject readyRoot;
    [SerializeField] private GameObject harvestedRoot;
    [Tooltip("On: swap one root for the other per state (mutually exclusive). " +
             "Off (base): harvestedRoot stays on as the background; readyRoot is an overlay that " +
             "switches off once harvested.")]
    [SerializeField] private bool swapStateVisuals;

    private CollisionTarget host;
    private int hitsLeft;
    private State state = State.Ready;
    private float respawnTimer;

    private void Awake()
    {
        host = GetComponent<CollisionTarget>();
        if (def != null) hitsLeft = def.hitsBeforeDespawn;
    }

    // Optional runtime injection (StructureSystem calls this when placing a structure at runtime,
    // since a prefab can't serialize a reference to a scene system). Mirrors Spawner.Initialize.
    public void Initialize(ResourceSystem system)
    {
        resourceSystem = system;
    }

    private void Start()
    {
        if (def == null)
            Debug.LogError($"ResourceSource on '{name}' has no ResourceSourceDef assigned.", this);
        if (resourceSystem == null)
            Debug.LogError($"ResourceSource on '{name}' has no ResourceSystem assigned.", this);

        // Infinite sources (Forge/Church) never change state, so they don't need state roots.
        if (def != null && !def.infinite)
        {
            if (readyRoot == null)
                Debug.LogError($"ResourceSource on '{name}' has no readyRoot assigned.", this);
            if (harvestedRoot == null)
                Debug.LogError($"ResourceSource on '{name}' has no harvestedRoot assigned.", this);
        }

        ApplyStateVisual();
    }

    // ICollisionEffect — dispatched by CollisionTarget.HandleHit when a unit hits the host.
    public void OnHit(Unit unit, CollisionTarget target)
    {
        if (state == State.Harvested || def == null || resourceSystem == null || unit == null) return;
        if (!TryGetYield(unit.Type, out float amount)) return;

        resourceSystem.AddResource(def.resource, amount);

        if (def.infinite) return;
        if (--hitsLeft <= 0) Deplete();
    }

    private void Update()
    {
        if (state != State.Harvested) return;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer <= 0f) Respawn();
    }

    // Resolves how much `type` harvests per hit. Returns false if the worker isn't listed
    // (an empty workerYields means nobody can harvest this source).
    private bool TryGetYield(UnitType type, out float amount)
    {
        var yields = def.workerYields;
        if (yields != null)
            for (int i = 0; i < yields.Length; i++)
                if (yields[i].worker == type)
                {
                    amount = yields[i].amount;
                    return true;
                }
        amount = 0f;
        return false;
    }

    // Harvested: used up, collider off, showing the harvested sprite until it regrows.
    private void Deplete()
    {
        state = State.Harvested;
        respawnTimer = def.respawnTime;
        host.SetColliderEnabled(false);
        ApplyStateVisual();
    }

    // Ready again: regrown, harvestable, showing the ready sprite.
    private void Respawn()
    {
        state = State.Ready;
        hitsLeft = def.hitsBeforeDespawn;
        host.SetColliderEnabled(true);
        ApplyStateVisual();
    }

    // Drives the two roots from the current state. Infinite sources keep their single visual, so
    // both roots are left as the prefab set them (typically only one is present and active).
    private void ApplyStateVisual()
    {
        if (def == null || def.infinite) return;

        if (!swapStateVisuals)
        {
            // Base: harvestedRoot is the always-on background; readyRoot overlays it while Ready.
            if (harvestedRoot != null) harvestedRoot.SetActive(true);
            if (readyRoot != null) readyRoot.SetActive(state == State.Ready);
        }
        else
        {
            // Mutually exclusive: exactly the root for the current state is shown.
            if (readyRoot != null) readyRoot.SetActive(state == State.Ready);
            if (harvestedRoot != null) harvestedRoot.SetActive(state == State.Harvested);
        }
    }
}
