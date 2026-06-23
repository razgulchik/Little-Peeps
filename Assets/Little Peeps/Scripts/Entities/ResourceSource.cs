using UnityEngine;

// Resource node behaviour: grants def.resource each time an allowed worker hits the host
// CollisionTarget, depletes after def.hitsBeforeDespawn hits, then respawns after
// def.respawnTime. Config lives in the ResourceSourceDef asset; per-instance state lives here.
// Attach to a natural node (tree/wheat/stone) or a building-source (Forge/Church, infinite def).
//
// Two visual states (skipped for infinite sources):
//   Ready     — ripe/grown, harvestable; shows def.readySprite.
//   Harvested — used up, collider off, regrowing; shows def.harvestedSprite. After def.respawnTime
//               it returns to Ready.
[RequireComponent(typeof(CollisionTarget))]
public class ResourceSource : MonoBehaviour, ICollisionEffect
{
    private enum State { Ready, Harvested }

    [SerializeField] private ResourceSourceDef def;
    [SerializeField] private ResourceSystem resourceSystem; // scene ref — can't live in the SO

    private CollisionTarget host;
    private SpriteRenderer spriteRenderer;
    private int hitsLeft;
    private State state = State.Ready;
    private float respawnTimer;

    private void Awake()
    {
        host = GetComponent<CollisionTarget>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
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

        // Infinite sources (Forge/Church) never change state, so they don't need state sprites.
        if (def != null && !def.infinite)
        {
            if (def.readySprite == null)
                Debug.LogError($"ResourceSource on '{name}' has no readySprite assigned.", this);
            if (def.harvestedSprite == null)
                Debug.LogError($"ResourceSource on '{name}' has no harvestedSprite assigned.", this);
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

    // Swaps the sprite to match the current state. Falls back to the sprite already on the
    // renderer when a state sprite isn't set (e.g. infinite sources keep their single sprite).
    private void ApplyStateVisual()
    {
        if (spriteRenderer == null || def == null) return;

        var sprite = state == State.Harvested ? def.harvestedSprite : def.readySprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }
}
