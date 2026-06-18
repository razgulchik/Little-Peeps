using System.Collections.Generic;
using UnityEngine;

// Data-driven starting layout for a run: which structures exist at run start and on which
// grid cell. RunManager places each entry through StructureSystem on StartNewRun, so starting
// structures share the exact same placement path (and grid alignment) as player-built ones.
[CreateAssetMenu(menuName = "LittlePeeps/StartingLayout")]
public class StartingLayoutDef : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public StructureDef def;
        public Vector2Int cell; // origin (bottom-left) cell of the footprint
    }

    public List<Entry> entries = new();
}
