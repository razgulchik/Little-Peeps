using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One tab of the build panel: a button (label and/or icon, whichever the tab prefab shows) and the
    // structures listed under it. Which tab a structure sits in is the palette's call, not the def's —
    // StructureDef carries no category, so regrouping the panel is an edit to this asset only.
    [Serializable]
    public class BuildTab
    {
        public string name;   // first string field — the inspector titles the list element with it
        public Sprite icon;
        public List<StructureDef> structures = new();
    }

    // The set of structures the player can build, grouped into the build panel's tabs. All entries are
    // available for now; MetaContext unlock-gating comes later. Tab order here = tab order in the panel,
    // and digit N opens the N-th tab shown (an empty tab gets no button and no number); structure order
    // inside a tab = card order.
    [CreateAssetMenu(menuName = "LittlePeeps/BuildPalette")]
    public class BuildPaletteDef : ScriptableObject
    {
        public List<BuildTab> tabs = new();
    }
}
