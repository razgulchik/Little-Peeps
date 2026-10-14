using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // Which piece of a soft border a cell draws on the border layer of one COVERING terrain — the terrain
    // that lies over the other where the two meet (the order is IslandSystem's border list). Two families:
    //  - Over…: on a cell of a terrain the covering one lies over: its fringe spills onto the cell from the
    //           named side(s), where the covering terrain is — and, for a set with overSea, on the sea's
    //           cells along its shore (the sea lies under every terrain). Never on a terrain above it: that
    //           border is the upper terrain's to draw;
    //  - Edge…: on a cell of the covering terrain itself: its own edge, on the named side(s), where the
    //           terrain it covers is — or, for a set with edgeAllRound, wherever it ends at all.
    // A corner piece is its two sides at once. A cell that touches the covering terrain only diagonally
    // draws nothing — leaving that corner out is what rounds the fringe off around a convex corner.
    public enum IslandBorderPiece
    {
        None,
        OverNorth,
        OverSouth,
        OverWest,
        OverEast,
        OverNorthWest,
        OverNorthEast,
        OverSouthWest,
        OverSouthEast,
        EdgeNorth,
        EdgeSouth,
        EdgeWest,
        EdgeEast,
        EdgeNorthWest,
        EdgeNorthEast,
        EdgeSouthWest,
        EdgeSouthEast,
    }

    // One terrain's border art: how it spills over the terrains it covers, and how its own edge looks
    // where it does. The pieces are overlays — mostly transparent, drawn on a tilemap of their own above
    // the ground — so one set serves every terrain below it. That only holds while the art uses the
    // covering terrain's own colours (and neutral in-betweens): a pixel in the colour of the terrain
    // underneath would tie the piece to that one neighbour.
    //
    // The tooltips give the art's file names (the forest set's, minus the "forest_tile_" prefix).
    [CreateAssetMenu(menuName = "LittlePeeps/TerrainBorderSet")]
    public class TerrainBorderSet : ScriptableObject
    {
        [Tooltip("Draw this terrain's edge all round it — toward every other terrain and toward the sea — not " +
                 "only toward the terrains it covers. Its fringe still goes only onto the terrains it covers.")]
        public bool edgeAllRound;

        [Tooltip("Spill this terrain's fringe out over the sea along its shore too, not only onto the terrains " +
                 "it covers.")]
        public bool overSea;

        [Header("Over — on the covered cell; this terrain lies to the …")]
        [Tooltip("boarder_top_out: a dense fringe along the top.")]
        public TileBase overNorth;
        [Tooltip("boarder_bottom_out: a dense fringe along the bottom.")]
        public TileBase overSouth;
        [Tooltip("boarder_left_side_outer: a dense fringe down the left side.")]
        public TileBase overWest;
        [Tooltip("boarder_right_side_outer: a dense fringe down the right side.")]
        public TileBase overEast;

        [Header("Over — corners: this terrain lies on both sides")]
        [Tooltip("boarder_top_left_corner_inner: fringe along the top and down the left side.")]
        public TileBase overNorthWest;
        [Tooltip("boarder_top_right_corner_inner: fringe along the top and down the right side.")]
        public TileBase overNorthEast;
        [Tooltip("boarder_bottom_left_corner_inner: fringe along the bottom and down the left side.")]
        public TileBase overSouthWest;
        [Tooltip("boarder_bottom_right_corner_inner: fringe along the bottom and down the right side.")]
        public TileBase overSouthEast;

        [Header("Edge — on this terrain's own cell; the covered terrain lies to the …")]
        [Tooltip("boarder_bottom_inner: sparse specks along the top.")]
        public TileBase edgeNorth;
        [Tooltip("boarder_top_inner: sparse specks along the bottom.")]
        public TileBase edgeSouth;
        [Tooltip("boarder_right_side_inner: sparse specks down the left side.")]
        public TileBase edgeWest;
        [Tooltip("boarder_left_side_inner: sparse specks down the right side.")]
        public TileBase edgeEast;

        [Header("Edge — corners: the covered terrain lies on both sides")]
        [Tooltip("inner_boarder_top_left_corner_inner: specks along the top and down the left side.")]
        public TileBase edgeNorthWest;
        [Tooltip("inner_boarder_top_right_corner_inner: specks along the top and down the right side.")]
        public TileBase edgeNorthEast;
        [Tooltip("inner_boarder_bottom_left_corner_inner: specks along the bottom and down the left side.")]
        public TileBase edgeSouthWest;
        [Tooltip("inner_boarder_bottom_right_corner_inner: specks along the bottom and down the right side.")]
        public TileBase edgeSouthEast;

        // An empty slot draws nothing: a border with a piece missing shows a gap, not someone else's tile.
        public TileBase Get(IslandBorderPiece piece)
        {
            switch (piece)
            {
                case IslandBorderPiece.OverNorth:     return overNorth;
                case IslandBorderPiece.OverSouth:     return overSouth;
                case IslandBorderPiece.OverWest:      return overWest;
                case IslandBorderPiece.OverEast:      return overEast;
                case IslandBorderPiece.OverNorthWest: return overNorthWest;
                case IslandBorderPiece.OverNorthEast: return overNorthEast;
                case IslandBorderPiece.OverSouthWest: return overSouthWest;
                case IslandBorderPiece.OverSouthEast: return overSouthEast;
                case IslandBorderPiece.EdgeNorth:     return edgeNorth;
                case IslandBorderPiece.EdgeSouth:     return edgeSouth;
                case IslandBorderPiece.EdgeWest:      return edgeWest;
                case IslandBorderPiece.EdgeEast:      return edgeEast;
                case IslandBorderPiece.EdgeNorthWest: return edgeNorthWest;
                case IslandBorderPiece.EdgeNorthEast: return edgeNorthEast;
                case IslandBorderPiece.EdgeSouthWest: return edgeSouthWest;
                case IslandBorderPiece.EdgeSouthEast: return edgeSouthEast;
                default:                              return null;
            }
        }
    }
}
