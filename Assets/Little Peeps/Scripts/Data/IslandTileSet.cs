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
    // the matching field below.
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
        InnerCornerLeft,
        InnerCornerRight,
    }

    // The tiles one island terrain is drawn with: the ground fill in its two checkerboard shades, and the
    // single-colour coastline outline.
    //
    // The outline art is drawn OUTSIDE the land cell — a 1px column in the neighbour to the side, a 4px
    // band in the neighbour below — so trim tiles land on water cells and must go on their own
    // collider-less tilemap. That keeps the island's logical size exactly the playable cells: the ring is
    // derived decoration, not part of the grid (contrast the old approach of building the island one row
    // and two columns bigger to leave room for the border).
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

        [Header("Trim — inner (bay) top corners")]
        public TileBase innerCornerLeft;  // land to the N and W: band plus a column down the cell's left side
        public TileBase innerCornerRight; // land to the N and E: band plus a column down the cell's right side

        [Header("Checkerboard")]
        [Tooltip("Flip which cell parity gets the light shade.")]
        public bool invertCheckerboard;

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
                case IslandTrimPiece.InnerCornerLeft:  return innerCornerLeft;
                case IslandTrimPiece.InnerCornerRight: return innerCornerRight;
                default:                               return null;
            }
        }
    }
}
