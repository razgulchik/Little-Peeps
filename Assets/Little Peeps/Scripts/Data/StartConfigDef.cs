using System.Collections.Generic;
using UnityEngine;

// The whole starting state of a run, bundled into one asset: island size, structure layout,
// resources, and stat modifiers. RunManager reads it in StartNewRun so a run's start is fully
// data-driven. Keep several assets around (default / debug / test presets) and swap the reference
// on RunManager to change what a run begins with — no code edits, no scene surgery.
[CreateAssetMenu(menuName = "LittlePeeps/StartConfig")]
public class StartConfigDef : ScriptableObject
{
    [Tooltip("Grid dimensions of the island generated at run start.")]
    public Vector2Int islandSize = new Vector2Int(10, 10);

    [Tooltip("Structures present at run start and their cells. Referenced (not embedded) so the same " +
             "layout can be shared across configs. Placed through the normal StructureSystem path.")]
    public StartingLayoutDef layout;

    [Tooltip("Resources the run begins with, one entry per type. Types not listed start at 0.")]
    public List<ResourceCost> startingResources = new();

    [Tooltip("Stat modifiers applied at run start (base+modifiers system). Real in-run sources " +
             "(ages/perks/meta) push their own later; use this for a run's baseline or debug bonuses.")]
    public List<StatModifier> startingModifiers = new();
}
