using UnityEngine;

// How much a given worker type harvests per hit. The set of entries also defines who is
// allowed to harvest at all (a worker not listed can't harvest, unless the list is empty).
[System.Serializable]
public struct WorkerYield
{
    public UnitType worker;
    [Min(0f)] public float amount;
}

// Per-type config for a resource source (Tree, Wheat, Stone, Forge, Church). The runtime
// behaviour and per-instance state (hits left, depleted, respawn timer) live in the
// ResourceSource component, which reads this def. Mirrors StructureDef/UnitDef.
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
    public float respawnTime = 5f;

    [Header("Visual states")]
    [Tooltip("Ready/ripe — the source can be harvested (e.g. grown wheat, full tree).")]
    public Sprite readySprite;
    [Tooltip("Harvested — used up, waiting to regrow (e.g. cut field, stump). Ignored for infinite sources.")]
    public Sprite harvestedSprite;
}
