using UnityEngine;

namespace LittlePeeps
{
    // A watermill's art per side of the river: one child root per WaterSide, each fully configured in the
    // prefab (its own sprite, offset and sorting — the wheel hangs over a different neighbour each time),
    // exactly one shown. The side is decided by the placement code when the structure lands
    // (StructureSystem.ApplyPlacementVisual); this component only knows "show the root for this side".
    // Used by the placed building, the carried one and the build-mode ghost alike, so the preview matches
    // 1:1 — and Show works on a disabled component, as the ghost's are.
    //
    // None shows the south root, the default: a ghost over a spot with no water side (tinted red) is the
    // plain front view, whatever roots the prefab happens to have switched on.
    //
    // The south side has a second root, the art mirrored, for a river that stops or turns under the
    // wheel (WaterSides.FlipSouth). Without it the plain south root shows.
    public class WaterSideVisual : MonoBehaviour
    {
        [Tooltip("River along the south side: the wheel faces the camera (building_watermill_horizontal).")]
        [SerializeField] private GameObject southRoot;

        [Tooltip("River along the south side, but ending or turning under the wheel (bottom-right) and running " +
                 "on past the left end: the south root mirrored, Flip X, the wheel bottom-left. Empty = the " +
                 "south root shows anyway.")]
        [SerializeField] private GameObject southFlippedRoot;

        [Tooltip("River along the west side: the wheel on the left (building_watermill_vertical).")]
        [SerializeField] private GameObject westRoot;

        [Tooltip("River along the east side: the wheel on the right. Its own sprite; until it is drawn, a " +
                 "copy of the west root with Flip X will do.")]
        [SerializeField] private GameObject eastRoot;

        public void Show(WaterSide side, bool flipSouth)
        {
            if (side == WaterSide.None) side = WaterSide.South;
            bool flipped = side == WaterSide.South && flipSouth && southFlippedRoot != null;
            if (southRoot != null) southRoot.SetActive(side == WaterSide.South && !flipped);
            if (southFlippedRoot != null) southFlippedRoot.SetActive(flipped);
            if (westRoot != null) westRoot.SetActive(side == WaterSide.West);
            if (eastRoot != null) eastRoot.SetActive(side == WaterSide.East);
        }
    }
}
