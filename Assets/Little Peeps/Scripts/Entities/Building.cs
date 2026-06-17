using UnityEngine;

// A player-placed building. Collision handling + effect dispatch live in the CollisionTarget
// base; Building adds building-specific data (definition, health). Spawner buildings add a
// Spawner component; resource buildings (Forge/Church) add a ResourceSource component.
public class Building : CollisionTarget
{
    public BuildingDef def;

    private float currentHealth;

    protected override void Awake()
    {
        base.Awake();
        // TODO: currentHealth = def.maxHealth (add maxHealth to BuildingDef when implementing)
    }

    // Reduce health; publish damaged/destroyed events at appropriate thresholds.
    public void TakeDamage(float amount)
    {
        // TODO: currentHealth -= amount; publish BuildingDamagedEvent; if currentHealth <= 0 publish BuildingDestroyedEvent
    }
}
