using UnityEngine;

namespace LittlePeeps
{
    // A river cell's art: one of three pieces, turned to join the river cells beside it. The piece is
    // decided by the placement code from the grid (RiverShapes, via StructureSystem.ApplyPlacementVisual,
    // which also re-picks the neighbours' pieces whenever water lands or leaves); this component only
    // knows how to show one. Show works on a disabled component, as on the build-mode ghost.
    //
    // Only the model turns, about its own position — the root, its collider and the rest of the prefab
    // stay put. So the sprites must be pivoted at their centre and the model sit on the cell's centre.
    // A drop shadow (SpriteShadow) on the model would turn with it; the river has none.
    public class RiverVisual : MonoBehaviour
    {
        [Tooltip("The renderer that shows the piece. It is the one turned, so it must sit on the cell's centre.")]
        [SerializeField] private SpriteRenderer model;

        [Tooltip("The river's end, drawn open to the WEST: the river comes in from the west, the cap faces east.")]
        [SerializeField] private Sprite end;

        [Tooltip("A straight run, drawn from WEST to EAST.")]
        [SerializeField] private Sprite straight;

        [Tooltip("A bend, drawn joining EAST and SOUTH.")]
        [SerializeField] private Sprite corner;

        public void Show(RiverShape shape)
        {
            if (model == null) return;

            var sprite = shape.Piece switch
            {
                RiverPiece.End => end,
                RiverPiece.Corner => corner,
                _ => straight,
            };
            if (sprite != null) model.sprite = sprite;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * shape.QuarterTurns);
        }
    }
}
