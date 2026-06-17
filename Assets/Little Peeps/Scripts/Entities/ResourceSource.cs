using UnityEngine;

// Resource node behaviour: grants def.resource each time an allowed worker hits the host
// CollisionTarget, depletes after def.hitsBeforeDespawn hits, then respawns after
// def.respawnTime. Config lives in the ResourceSourceDef asset; per-instance state lives here.
// Attach to a natural node (tree/wheat/stone) or a building-source (Forge/Church, infinite def).
[RequireComponent(typeof(CollisionTarget))]
public class ResourceSource : MonoBehaviour, ICollisionEffect
{
    [SerializeField] private ResourceSourceDef def;
    [SerializeField] private ResourceSystem resourceSystem; // scene ref — can't live in the SO

    private CollisionTarget host;
    private SpriteRenderer spriteRenderer;
    private int hitsLeft;
    private bool depleted;
    private float respawnTimer;

    private void Awake()
    {
        host = GetComponent<CollisionTarget>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (def != null) hitsLeft = def.hitsBeforeDespawn;
    }

    private void Start()
    {
        if (def == null)
            Debug.LogError($"ResourceSource on '{name}' has no ResourceSourceDef assigned.", this);
        if (resourceSystem == null)
            Debug.LogError($"ResourceSource on '{name}' has no ResourceSystem assigned.", this);
    }

    // ICollisionEffect — dispatched by CollisionTarget.HandleHit when a unit hits the host.
    public void OnHit(Unit unit, CollisionTarget target)
    {
        if (depleted || def == null || resourceSystem == null || unit == null) return;
        if (!IsAllowedWorker(unit.Type)) return;

        resourceSystem.AddResource(def.resource, def.amountPerHit);

        if (def.infinite) return;
        if (--hitsLeft <= 0) Deplete();
    }

    private void Update()
    {
        if (!depleted) return;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer <= 0f) Respawn();
    }

    private bool IsAllowedWorker(UnitType type)
    {
        var allowed = def.allowedWorkers;
        if (allowed == null || allowed.Length == 0) return true;
        for (int i = 0; i < allowed.Length; i++)
            if (allowed[i] == type) return true;
        return false;
    }

    // The node "disappears" while used up: no collisions, no visual, until it respawns.
    private void Deplete()
    {
        depleted = true;
        respawnTimer = def.respawnTime;
        host.SetColliderEnabled(false);
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    private void Respawn()
    {
        depleted = false;
        hitsLeft = def.hitsBeforeDespawn;
        host.SetColliderEnabled(true);
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }
}
