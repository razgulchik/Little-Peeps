using UnityEngine;

// Mobile resource node (alpaca, boar, fox): grants def.resource each time an allowed worker
// hits the host CollisionTarget, and despawns entirely after def.hitsBeforeDespawn hits — its
// owning AnimalSpawner replaces it later, unlike a static ResourceSource which regrows in
// place (so def.respawnTime is unused here; the replacement cadence is the spawner's
// spawnCooldown). def.infinite animals never despawn and keep paying out per hit.
// Movement lives in AnimalWander; this component owns only the harvest interaction.
[RequireComponent(typeof(CollisionTarget))]
public class Animal : MonoBehaviour, ICollisionEffect
{
    [SerializeField] private ResourceSourceDef def;
    [SerializeField] private ResourceSystem resourceSystem; // scene ref — only for scene-placed animals

    private AnimalSpawner owner;   // null for a scene-placed animal (nobody replaces it)
    private int hitsLeft;

    private void Awake()
    {
        if (def != null) hitsLeft = def.hitsBeforeDespawn;
    }

    // Runtime injection (AnimalSpawner calls this on spawn, since a prefab can't serialize a
    // reference to a scene system). Mirrors ResourceSource.Initialize.
    public void Initialize(ResourceSystem system, AnimalSpawner spawner)
    {
        resourceSystem = system;
        owner = spawner;
    }

    private void Start()
    {
        if (def == null)
            Debug.LogError($"Animal on '{name}' has no ResourceSourceDef assigned.", this);
        if (resourceSystem == null)
            Debug.LogError($"Animal on '{name}' has no ResourceSystem assigned.", this);
    }

    // ICollisionEffect — dispatched by CollisionTarget.HandleHit when a unit hits the host.
    public void OnHit(Unit unit, CollisionTarget target)
    {
        if (def == null || resourceSystem == null || unit == null) return;
        if (!def.TryGetYield(unit.Type, out float amount)) return;

        // Same production gateway as static sources: the base amount is scaled by the worker's
        // yield modifier and the global production multiplier before being credited.
        resourceSystem.AddHarvest(def.resource, unit.Type, amount);

        if (def.infinite) return;
        if (--hitsLeft > 0) return;

        if (owner != null) owner.NotifyHarvested(this);
        Destroy(gameObject);
    }
}
