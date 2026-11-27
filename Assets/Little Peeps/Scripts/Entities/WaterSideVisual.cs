using UnityEngine;

namespace LittlePeeps
{
    // A watermill's art per wheel spot (WaterSide): one child root per spot, each fully configured in the
    // prefab (its own sprite, offset and sorting — the wheel turns in a different river cell each time),
    // exactly one shown. The spot is decided by the placement code when the structure lands
    // (StructureSystem.ApplyPlacementVisual); this component only knows "show the root for this spot".
    // Used by the placed building and the placement ghost alike, so the preview matches
    // 1:1 — and Show works on a disabled component, as the ghost's are.
    //
    // None shows the south-right root, the default: a ghost over a spot with no water (tinted red) is the
    // plain front view, whatever roots the prefab happens to have switched on.
    //
    // Every spot gets its own drawing. Until one is drawn, its root shows the opposite spot's art with
    // Flip X — no mirroring in code, so a drawn sprite drops into its root as it is.
    public class WaterSideVisual : MonoBehaviour
    {
        [Tooltip("River below the right cell: the wheel in front, bottom right (watermill_horizontal_up).")]
        [SerializeField] private GameObject southRightRoot;

        [Tooltip("River below the left cell: the wheel in front, bottom left. Until its own sprite is drawn, " +
                 "the south-right art with Flip X.")]
        [SerializeField] private GameObject southLeftRoot;

        [Tooltip("River past the west end: the wheel on the left (watermill_vertical).")]
        [SerializeField] private GameObject westRoot;

        [Tooltip("River past the east end: the wheel on the right. Until its own sprite is drawn, the west " +
                 "art with Flip X.")]
        [SerializeField] private GameObject eastRoot;

        [Tooltip("River above the right cell: the wheel behind the roof, top right (watermill_horizontal_bottom).")]
        [SerializeField] private GameObject northRightRoot;

        [Tooltip("River above the left cell: the wheel behind the roof, top left. Until its own sprite is " +
                 "drawn, the north-right art with Flip X.")]
        [SerializeField] private GameObject northLeftRoot;

        public void Show(WaterSide spot)
        {
            if (spot == WaterSide.None) spot = WaterSide.SouthRight;
            Set(southRightRoot, spot == WaterSide.SouthRight);
            Set(southLeftRoot, spot == WaterSide.SouthLeft);
            Set(westRoot, spot == WaterSide.West);
            Set(eastRoot, spot == WaterSide.East);
            Set(northRightRoot, spot == WaterSide.NorthRight);
            Set(northLeftRoot, spot == WaterSide.NorthLeft);
        }

        private static void Set(GameObject root, bool on)
        {
            if (root != null) root.SetActive(on);
        }
    }
}
