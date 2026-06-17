using UnityEngine;

// A placed structure — anything that stands on the grid. Collision handling + effect dispatch
// live in the CollisionTarget base; Structure adds structure-specific data (definition, health).
// Spawner structures add a Spawner component; resource structures (Tree/Wheat/Forge/Church) add
// a ResourceSource component.
public class Structure : CollisionTarget
{
    public StructureDef def;

    private float currentHealth;

    protected override void Awake()
    {
        base.Awake();
        // TODO: currentHealth = def.maxHealth (add maxHealth to StructureDef when implementing)
    }

    // Reduce health; publish damaged/destroyed events at appropriate thresholds.
    public void TakeDamage(float amount)
    {
        // TODO: currentHealth -= amount; publish StructureDamagedEvent; if currentHealth <= 0 publish StructureDestroyedEvent
    }
}
