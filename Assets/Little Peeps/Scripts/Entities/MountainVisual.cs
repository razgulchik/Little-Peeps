using UnityEngine;

namespace LittlePeeps
{
    // A mountain cell's art: one of the nine pieces of the massif sheet, and over it the inner corners where
    // the massif turns in. Which ones is decided by the placement code from the grid (MountainShapes, via
    // StructureSystem.ApplyPlacementVisual, which also re-picks the cells around whenever a mountain lands or
    // leaves); this component only knows how to show them. Show works on a disabled component, as on the
    // placement ghost. Being on a structure's root is also what makes its cells count as mountain.
    //
    // An inner corner is drawn OVER the piece, not instead of it: four overlays then serve every piece a
    // corner can land on, where whole tiles would take one per piece-and-corner pair. So corner art holds
    // only its own pixels, transparent around them, and no holes inside — the piece under a hole would show,
    // not the ground. Its renderer sits on the model, one sorting order above it. The art is never mirrored:
    // the light comes from the left, so each corner is drawn on its own. A corner with no sprite yet doesn't
    // show, and the piece under it stays as it is.
    //
    // All the sprites are pivoted at their centre (as the sheet's slices are) and the model sits on the
    // cell's centre. The renderers draw with the land's bending material — a mountain is drawn as part of the
    // land and bends with it while a zone rises (IslandRise) — and cast no shadow, as the river.
    public class MountainVisual : MonoBehaviour
    {
        [Tooltip("The renderer that shows the piece, on the cell's centre.")]
        [SerializeField] private SpriteRenderer model;

        [Tooltip("The massif sheet's slices, mountain_9tiles_0 … _8 in reading order. A piece is named by the " +
                 "cell's open edges: Top = no mountain to the north, Left = none to the west, Centre = mountain all round.")]
        [SerializeField] private Sprite topLeft, top, topRight, left, centre, right, bottomLeft, bottom, bottomRight;

        [Tooltip("The renderers of the inner corners, on the model and one sorting order above it.")]
        [SerializeField] private SpriteRenderer northWestCorner, northEastCorner, southWestCorner, southEastCorner;

        [Tooltip("The inner corners' art: a corner of the massif turning in — mountain on both sides of it, none on " +
                 "the diagonal between. Only its own pixels, transparent around them; pivot Center. Empty = not drawn yet.")]
        [SerializeField] private Sprite innerNorthWest, innerNorthEast, innerSouthWest, innerSouthEast;

        public void Show(MountainShape shape)
        {
            if (model != null)
            {
                var sprite = PieceSprite(shape.Piece);
                if (sprite != null) model.sprite = sprite;
            }

            ShowCorner(northWestCorner, innerNorthWest, shape.Has(MountainCorners.NorthWest));
            ShowCorner(northEastCorner, innerNorthEast, shape.Has(MountainCorners.NorthEast));
            ShowCorner(southWestCorner, innerSouthWest, shape.Has(MountainCorners.SouthWest));
            ShowCorner(southEastCorner, innerSouthEast, shape.Has(MountainCorners.SouthEast));
        }

        private Sprite PieceSprite(MountainPiece piece) => piece switch
        {
            MountainPiece.TopLeft => topLeft,
            MountainPiece.Top => top,
            MountainPiece.TopRight => topRight,
            MountainPiece.Left => left,
            MountainPiece.Right => right,
            MountainPiece.BottomLeft => bottomLeft,
            MountainPiece.Bottom => bottom,
            MountainPiece.BottomRight => bottomRight,
            _ => centre,
        };

        private static void ShowCorner(SpriteRenderer corner, Sprite art, bool shown)
        {
            if (corner == null) return;
            corner.sprite = art;
            corner.enabled = shown && art != null;
        }
    }
}
