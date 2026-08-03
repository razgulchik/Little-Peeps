using UnityEngine;

namespace LittlePeeps
{
    // Scene-view-only grid reference — pure editor tooling with no gameplay role (that's GridOverlay)
    // and no contribution to a build: the whole drawing path compiles out outside the editor.
    //
    // Everything is anchored to the WORLD ORIGIN on exactly IslandGrid's convention — cell c spans
    // [c*cellSize, (c+1)*cellSize) with its center at (c + 0.5) * cellSize — so what you see here is
    // where real cells are. Two layers:
    //   1. The lattice + a signed coordinate label ("0,0", "-1,2", ...) in every cell. This is pure
    //      arithmetic, so it is ALWAYS there: no island, no play mode, no "Generate Island" needed.
    //      Handy for laying out AgeDef expansionBlocks (RectInt), whose coordinates line up 1:1 with
    //      the labels.
    //   2. When an IslandSystem has a live grid, its EXISTING cells are outlined on top — so you can
    //      read actual land against the reference lattice (after "Generate Island" in edit mode, or
    //      once a run has built the grid in play mode).
    //
    // The GameObject's position only picks WHICH cell the lattice is centered on (snapped down to a
    // whole cell); the drawing itself always sits on the island plane at z = 0.
    // Put the component on an empty GameObject; toggle Gizmos in the Scene view to show/hide.
    public class SceneGridGizmo : MonoBehaviour
    {
        // Safety rails: the lattice is cheap, but Handles.Label is not — a huge halfExtent would stall
        // the Scene view. Clamp the reach, and drop the labels (keeping the lines) past a cell budget.
        private const int MaxHalfExtent = 64;
        private const int MaxLabelCells = 4096;

        [Tooltip("Island whose existing cells are outlined. Auto-found in the scene if left empty; " +
                 "without one only the reference lattice is drawn.")]
        [SerializeField] private IslandSystem islandSystem;

        [Tooltip("World units per cell. Must match IslandSystem's cellSize to line up with real cells.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("How far the lattice reaches from its center cell, IN CELLS, per axis.")]
        [SerializeField] private Vector2Int halfExtent = new Vector2Int(12, 12);

        [Tooltip("Labels are skipped (lines stay) when the lattice exceeds ~4096 cells.")]
        [SerializeField] private bool drawLabels = true;
        [SerializeField] private int labelFontSize = 10;
        [SerializeField] private bool onlyWhenSelected = false;

        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("The two lines through the world origin.")]
        [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color labelColor = Color.white;
        [Tooltip("Outline of cells that actually exist on the island.")]
        [SerializeField] private Color landColor = new Color(0.4f, 1f, 0.5f, 0.5f);
        [Tooltip("Colour of cell (0,0) and its label, to anchor orientation.")]
        [SerializeField] private Color originColor = new Color(1f, 0.6f, 0.1f, 1f);

        // Assign the island automatically when the component is first added.
        private void Reset() => islandSystem = FindFirstObjectByType<IslandSystem>();

    #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!onlyWhenSelected) Draw();
        }

        private void OnDrawGizmosSelected()
        {
            if (onlyWhenSelected) Draw();
        }

        private void Draw()
        {
            if (cellSize <= 0f) return;

            // Center on the cell CONTAINING this object, so the lattice shifts by whole cells and its
            // borders always land on true cell borders, whatever cellSize is.
            var center = new Vector2Int(Mathf.FloorToInt(transform.position.x / cellSize),
                                        Mathf.FloorToInt(transform.position.y / cellSize));
            var half = new Vector2Int(Mathf.Clamp(halfExtent.x, 0, MaxHalfExtent),
                                      Mathf.Clamp(halfExtent.y, 0, MaxHalfExtent));
            Vector2Int min = center - half, max = center + half;

            DrawLattice(min, max);
            DrawLabels(min, max);
            DrawIslandCells();

            // Last, so the origin wins over both the lattice and any land outline on top of it.
            Gizmos.color = originColor;
            Gizmos.DrawWireCube(CellCenter(0, 0), new Vector3(cellSize, cellSize, 0f));
        }

        // Cell borders run from min to max+1 inclusive — max+1 closes the last cell.
        private void DrawLattice(Vector2Int min, Vector2Int max)
        {
            float x0 = min.x * cellSize, x1 = (max.x + 1) * cellSize;
            float y0 = min.y * cellSize, y1 = (max.y + 1) * cellSize;

            for (int cx = min.x; cx <= max.x + 1; cx++)
            {
                float x = cx * cellSize;
                Gizmos.color = cx == 0 ? axisColor : lineColor;
                Gizmos.DrawLine(new Vector3(x, y0, 0f), new Vector3(x, y1, 0f));
            }

            for (int cy = min.y; cy <= max.y + 1; cy++)
            {
                float y = cy * cellSize;
                Gizmos.color = cy == 0 ? axisColor : lineColor;
                Gizmos.DrawLine(new Vector3(x0, y, 0f), new Vector3(x1, y, 0f));
            }
        }

        private void DrawLabels(Vector2Int min, Vector2Int max)
        {
            if (!drawLabels) return;
            long cells = (long)(max.x - min.x + 1) * (max.y - min.y + 1);
            if (cells > MaxLabelCells) return;

            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = labelFontSize
            };

            for (int cx = min.x; cx <= max.x; cx++)
                for (int cy = min.y; cy <= max.y; cy++)
                {
                    style.normal.textColor = (cx == 0 && cy == 0) ? originColor : labelColor;
                    UnityEditor.Handles.Label(CellCenter(cx, cy), $"{cx},{cy}", style);
                }
        }

        // Outline the cells the island really has. Drawn from the grid's OWN cellSize rather than this
        // component's, so a mismatch between the two shows up as visibly offset squares instead of
        // hiding behind a lattice that silently agrees with itself.
        private void DrawIslandCells()
        {
            if (islandSystem == null) islandSystem = FindFirstObjectByType<IslandSystem>();
            var grid = islandSystem != null ? islandSystem.Grid : null;
            if (grid == null) return; // grid not built yet (edit mode before "Generate Island")

            float size = grid.CellSize;
            var box = new Vector3(size, size, 0f);
            Gizmos.color = landColor;

            foreach (var kv in grid.Cells)
            {
                Vector2 c = grid.GridToWorld(kv.Key);
                Gizmos.DrawWireCube(new Vector3(c.x, c.y, 0f), box);
            }
        }

        private Vector3 CellCenter(int x, int y)
        {
            return new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
        }
    #endif
    }
}
