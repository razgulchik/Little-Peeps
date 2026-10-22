using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // Which variant a LAND cell draws. The art rounds off the island's OUTER corners by eating a few
    // pixels of grass, so there is one variant per corner. There are deliberately no inner-corner
    // variants: a concave corner is drawn entirely by the trim ring, not by the ground tile.
    public enum IslandGroundPiece
    {
        Plain,
        CornerTopLeft,
        CornerTopRight,
        CornerBottomLeft,
        CornerBottomRight,
    }

    // Which piece of the coastline outline a NON-land cell draws. Named after the island edge the piece
    // hugs (that is how the sheet is laid out); the neighbour condition that selects it is documented on
    // the matching field below. A cell may draw two: a bottom-edge band and a side column, on two layers.
    public enum IslandTrimPiece
    {
        None,
        LeftEdgeTop,
        LeftEdgeMid,
        LeftEdgeBottom,
        RightEdgeTop,
        RightEdgeMid,
        RightEdgeBottom,
        BottomEdgeLeft,
        BottomEdgeMid,
        BottomEdgeRight,
    }

    // The tiles one island terrain is drawn with: the ground fill in its two checkerboard shades, the decor
    // scattered over it, and the single-colour coastline outline.
    //
    // The outline art is drawn OUTSIDE the land cell — a 1px column in the neighbour to the side, a 4px
    // band in the neighbour below — so trim tiles land on water cells and must go on their own
    // collider-less tilemaps. That keeps the island's logical size exactly the playable cells: the ring is
    // derived decoration, not part of the grid (contrast the old approach of building the island one row
    // and two columns bigger to leave room for the border). Bands and columns go on two tilemaps, the
    // columns over the bands: a bay's inner corner is one of each, so it needs no art of its own.
    //
    // The north side has no outline at all — the art is drawn that way on purpose, so there are no
    // top-edge trim slots to fill.
    [CreateAssetMenu(menuName = "LittlePeeps/IslandTileSet")]
    public class IslandTileSet : ScriptableObject
    {
        [Header("Ground — fill")]
        public TileBase groundLight;
        public TileBase groundDark;

        [Header("Ground — outer corners")]
        public TileBase cornerTopLeftLight;
        public TileBase cornerTopLeftDark;
        public TileBase cornerTopRightLight;
        public TileBase cornerTopRightDark;
        public TileBase cornerBottomLeftLight;
        public TileBase cornerBottomLeftDark;
        public TileBase cornerBottomRightLight;
        public TileBase cornerBottomRightDark;

        [Header("Trim — island's left edge (land is EAST of the cell)")]
        public TileBase leftEdgeTop;      // nothing land to the NE: the column starts 2px lower to meet the corner tile
        public TileBase leftEdgeMid;      // land to the NE: full-height column. Doubles as a bay's RIGHT wall.
        public TileBase leftEdgeBottom;   // only the NE diagonal is land: 2px stub closing the column from below

        [Header("Trim — island's right edge (land is WEST of the cell)")]
        public TileBase rightEdgeTop;     // nothing land to the NW
        public TileBase rightEdgeMid;     // land to the NW. Doubles as a bay's LEFT wall.
        public TileBase rightEdgeBottom;  // only the NW diagonal is land

        [Header("Trim — island's bottom edge (land is NORTH of the cell)")]
        public TileBase bottomEdgeLeft;   // NW is water: the band's left end is rounded off
        public TileBase bottomEdgeMid;    // NW and NE are land: full band
        public TileBase bottomEdgeRight;  // NE is water: the band's right end is rounded off

        [Header("Decor — scattered over the ground")]
        [Tooltip("Specks on transparency, drawn over the light ground, corners included (keep the pixels the " +
                 "corners cut away bare). A light cell that rolls decor takes one of these at random.")]
        public TileBase[] decorLight;
        [Tooltip("Specks on transparency, drawn over the dark ground, corners included (keep the pixels the " +
                 "corners cut away bare). A dark cell that rolls decor takes one of these at random.")]
        public TileBase[] decorDark;
        [Tooltip("The share of this terrain's cells that get decor: 0 = none, 1 = every cell. Each cell rolls " +
                 "on its own, so decor now and then bunches up or leaves a gap.")]
        [Range(0f, 1f)] public float decorChance = 0.3f;

        [Header("Checkerboard")]
        [Tooltip("Flip which cell parity gets the light shade.")]
        public bool invertCheckerboard;

        // The decor tiles a cell of this shade picks from; empty when the shade has none.
        public TileBase[] GetDecor(bool light) => (light ? decorLight : decorDark) ?? System.Array.Empty<TileBase>();

        // Ground tile for a cell. An unassigned corner slot falls back to the plain fill so a half-filled
        // asset shows a slightly square corner rather than a hole in the island.
        public TileBase GetGround(bool light, IslandGroundPiece piece)
        {
            TileBase tile;
            switch (piece)
            {
                case IslandGroundPiece.CornerTopLeft:     tile = light ? cornerTopLeftLight     : cornerTopLeftDark;     break;
                case IslandGroundPiece.CornerTopRight:    tile = light ? cornerTopRightLight    : cornerTopRightDark;    break;
                case IslandGroundPiece.CornerBottomLeft:  tile = light ? cornerBottomLeftLight  : cornerBottomLeftDark;  break;
                case IslandGroundPiece.CornerBottomRight: tile = light ? cornerBottomRightLight : cornerBottomRightDark; break;
                default:                                  tile = null;                                                  break;
            }
            return tile != null ? tile : (light ? groundLight : groundDark);
        }

        public TileBase GetTrim(IslandTrimPiece piece)
        {
            switch (piece)
            {
                case IslandTrimPiece.LeftEdgeTop:      return leftEdgeTop;
                case IslandTrimPiece.LeftEdgeMid:      return leftEdgeMid;
                case IslandTrimPiece.LeftEdgeBottom:   return leftEdgeBottom;
                case IslandTrimPiece.RightEdgeTop:     return rightEdgeTop;
                case IslandTrimPiece.RightEdgeMid:     return rightEdgeMid;
                case IslandTrimPiece.RightEdgeBottom:  return rightEdgeBottom;
                case IslandTrimPiece.BottomEdgeLeft:   return bottomEdgeLeft;
                case IslandTrimPiece.BottomEdgeMid:    return bottomEdgeMid;
                case IslandTrimPiece.BottomEdgeRight:  return bottomEdgeRight;
                default:                               return null;
            }
        }
    }
}
