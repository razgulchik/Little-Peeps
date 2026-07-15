using UnityEngine;

// Scene-view debug overlay for the island grid: at every EXISTING cell it draws a wire square and
// labels the cell's signed coordinate ("0,0", "-1,2", ...) at the cell center. Reads the live
// IslandGrid from IslandSystem, so it mirrors the real island — it appears after "Generate Island"
// in edit mode, or once a run has built the grid in play mode. Handy for laying out AgeDef
// expansionBlocks (RectInt), whose coordinates line up 1:1 with these labels.
//
// Pure editor tooling: the drawing compiles only in the editor and contributes nothing to a build.
public class IslandGridGizmo : MonoBehaviour
{
    [Tooltip("Island whose grid is drawn. Auto-found in the scene if left empty.")]
    [SerializeField] private IslandSystem islandSystem;

    [SerializeField] private bool drawCells = true;
    [SerializeField] private bool drawLabels = true;

    [Tooltip("Wire square drawn around each existing cell.")]
    [SerializeField] private Color cellColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color labelColor = Color.white;

    [Tooltip("Colour of cell (0,0) and its label, to anchor orientation.")]
    [SerializeField] private Color originColor = new Color(1f, 0.6f, 0.1f, 1f);

    [Tooltip("Label text size in points.")]
    [SerializeField] private int labelFontSize = 10;

    // Assign the island automatically when the component is first added.
    private void Reset() => islandSystem = FindFirstObjectByType<IslandSystem>();

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (islandSystem == null) islandSystem = FindFirstObjectByType<IslandSystem>();
        var grid = islandSystem != null ? islandSystem.Grid : null;
        if (grid == null) return; // grid not built yet (edit mode before "Generate Island")

        float size = grid.CellSize;
        var labelStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = labelFontSize
        };

        foreach (var kv in grid.Cells)
        {
            Vector2Int coord = kv.Key;
            bool isOrigin = coord == Vector2Int.zero;
            Vector2 center = grid.GridToWorld(coord);
            var center3 = new Vector3(center.x, center.y, 0f);

            if (drawCells)
            {
                Gizmos.color = isOrigin ? originColor : cellColor;
                Gizmos.DrawWireCube(center3, new Vector3(size, size, 0f));
            }

            if (drawLabels)
            {
                labelStyle.normal.textColor = isOrigin ? originColor : labelColor;
                UnityEditor.Handles.Label(center3, $"{coord.x},{coord.y}", labelStyle);
            }
        }
    }
#endif
}
