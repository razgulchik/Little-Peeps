using System.Collections.Generic;
using UnityEngine;

// The set of structures the player can build, shown as cards in the build panel. All entries are
// available for now; MetaContext unlock-gating comes later. Order here = card order in the panel.
[CreateAssetMenu(menuName = "LittlePeeps/BuildPalette")]
public class BuildPaletteDef : ScriptableObject
{
    public List<StructureDef> structures = new();
}
