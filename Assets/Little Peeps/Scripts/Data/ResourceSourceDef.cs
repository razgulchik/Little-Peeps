using UnityEngine;

// Per-type config for a resource source (Tree, Wheat, Stone, Forge, Church). The runtime
// behaviour and per-instance state (hits left, depleted, respawn timer) live in the
// ResourceSource component, which reads this def. Mirrors BuildingDef/UnitDef.
[CreateAssetMenu(menuName = "LittlePeeps/ResourceSourceDef")]
public class ResourceSourceDef : ScriptableObject
{
    public string id;
    public ResourceType resource;
    public float amountPerHit = 1f;

    [Tooltip("Empty = any worker can harvest")]
    public UnitType[] allowedWorkers;

    [Header("Depletion / respawn")]
    public bool infinite = false;
    public int hitsBeforeDespawn = 3;
    public float respawnTime = 5f;
}
