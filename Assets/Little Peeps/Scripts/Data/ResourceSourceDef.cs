using UnityEngine;

// How much a given worker type harvests per hit. The set of entries also defines who is
// allowed to harvest at all: a worker not listed can't harvest, and an empty list means
// nobody can (see TryGetYield).
[System.Serializable]
public struct WorkerYield
{
    public UnitType worker;
    [Min(0f)] public float amount;
}

// Per-type config for a resource source, static (Tree, Wheat, Forge, Church) or mobile
// (Animal: alpaca, boar, fox). The runtime behaviour and per-instance state (hits left,
// depleted, respawn timer) live in the ResourceSource / Animal component, which reads this
// def. Mirrors StructureDef/UnitDef.
[CreateAssetMenu(menuName = "LittlePeeps/ResourceSourceDef")]
public class ResourceSourceDef : ScriptableObject
{
    public string id;
    public ResourceType resource;

    [Tooltip("Per-worker harvest amount; listed workers are the only ones allowed to harvest " +
             "(e.g. Farmer 1, Lumberjack 2). Empty = nobody can harvest this source.")]
    public WorkerYield[] workerYields;

    [Header("Depletion / respawn")]
    public bool infinite = false;
    public int hitsBeforeDespawn = 3;
    // In-place regrow delay for static sources (ResourceSource). Unused for animals — a
    // harvested animal is replaced by its AnimalSpawner, whose spawnCooldown sets the cadence.
    public float respawnTime = 5f;

    // Resolves how much `type` harvests per hit. Returns false if the worker isn't listed
    // (an empty workerYields means nobody can harvest this source).
    public bool TryGetYield(UnitType type, out float amount)
    {
        if (workerYields != null)
            for (int i = 0; i < workerYields.Length; i++)
                if (workerYields[i].worker == type)
                {
                    amount = workerYields[i].amount;
                    return true;
                }
        amount = 0f;
        return false;
    }

    // Visuals are not stored here: each state is a separate root configured in the prefab
    // (own SpriteRenderer + Sorting Layer + pivot), swapped by ResourceSource. `infinite`
    // already tells whether the source has two states (false) or a single visual (true).
}
