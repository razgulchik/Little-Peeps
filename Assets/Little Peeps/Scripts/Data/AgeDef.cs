using System.Collections.Generic;
using UnityEngine;

// One age/epoch the player can advance into. Authored as data: what it costs, how the island grows,
// and which permanent bonuses it grants (via the RunStats modifier system).
[CreateAssetMenu(menuName = "LittlePeeps/AgeDef")]
public class AgeDef : ScriptableObject
{
    [Tooltip("Shown on the transition banner. Falls back to \"Age N\" when empty.")]
    public string title;

    [Tooltip("Resources spent to advance into this age.")]
    public List<ResourceCost> resourceCost;

    [Tooltip("Permanent bonuses granted on entering this age (production, yield, speed, ...). " +
             "Pushed into RunStats — no code needed to add a new kind of bonus, just data.")]
    public List<StatModifier> modifiers;

    [Tooltip("Island cells added this age, as absolute grid rectangles. Multiple blocks can form " +
             "L-shapes / uneven growth; existing cells and their occupants are never touched.")]
    public List<RectInt> expansionBlocks;
}
