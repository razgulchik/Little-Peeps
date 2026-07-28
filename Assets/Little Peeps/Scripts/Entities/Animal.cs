using UnityEngine;

namespace LittlePeeps
{
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

        [Tooltip("Where the pickup effect and the floating number leave from. Empty = this transform. " +
                 "Mirrors ResourceSource.fxAnchor — the effect should leave the animal's body, not the " +
                 "root the wander code drives.")]
        [SerializeField] private Transform fxAnchor;

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
            // yield modifier and the global production multiplier before being credited. The def also
            // carries the source scope, so a perk on alpaca doesn't leak onto every other Coins source.
            // The position goes in BEFORE the animal can destroy itself below: the effect outlives the
            // node it came from, which is exactly why it is spawned by a system and not parented here.
            resourceSystem.AddHarvest(def, unit.Type, amount, fxAnchor != null ? fxAnchor.position : transform.position);

            if (def.infinite) return;
            if (--hitsLeft > 0) return;

            if (owner != null) owner.NotifyHarvested(this);
            Destroy(gameObject);
        }
    }
}
