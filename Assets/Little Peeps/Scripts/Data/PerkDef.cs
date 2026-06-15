using UnityEngine;

// Abstract base — create concrete subclasses in code with [CreateAssetMenu] to define perks
public abstract class PerkDef : ScriptableObject
{
    public string id;
    [TextArea] public string description;
    public float weight = 1f;

    // Apply this perk's permanent effect to the current run state
    public abstract void ApplyPerk(RunContext context);
}
