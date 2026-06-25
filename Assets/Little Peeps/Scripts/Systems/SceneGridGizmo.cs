using UnityEngine;

// Scene-view-only reference grid. Draws a square lattice of `cellSize` (1 unit by default)
// via Gizmos so you can eyeball placement while editing — it is NOT rendered at runtime and
// has no gameplay role (that's GridOverlay). Lines are drawn around this object's position,
// snapped to the cellSize lattice. Put this component on an empty GameObject; toggle Gizmos
// in the Scene view to show/hide.
public class SceneGridGizmo : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;          // world units per cell
    [SerializeField] private Vector2 halfExtent = new Vector2(20f, 20f); // how far the grid reaches from center, per axis
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.6f); // the two lines through center
    [SerializeField] private bool onlyWhenSelected = false;

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

        // Snap the grid's center to the lattice so cell borders land on whole-unit coordinates
        // regardless of where the GameObject sits.
        Vector3 c = transform.position;
        float cx = Mathf.Round(c.x / cellSize) * cellSize;
        float cy = Mathf.Round(c.y / cellSize) * cellSize;

        float left = cx - halfExtent.x, right = cx + halfExtent.x;
        float bottom = cy - halfExtent.y, top = cy + halfExtent.y;

        // Vertical lines
        for (float x = cx; x <= right + 0.0001f; x += cellSize)
        {
            Gizmos.color = Mathf.Approximately(x, cx) ? axisColor : lineColor;
            Gizmos.DrawLine(new Vector3(x, bottom, c.z), new Vector3(x, top, c.z));
        }
        for (float x = cx - cellSize; x >= left - 0.0001f; x -= cellSize)
        {
            Gizmos.color = lineColor;
            Gizmos.DrawLine(new Vector3(x, bottom, c.z), new Vector3(x, top, c.z));
        }

        // Horizontal lines
        for (float y = cy; y <= top + 0.0001f; y += cellSize)
        {
            Gizmos.color = Mathf.Approximately(y, cy) ? axisColor : lineColor;
            Gizmos.DrawLine(new Vector3(left, y, c.z), new Vector3(right, y, c.z));
        }
        for (float y = cy - cellSize; y >= bottom - 0.0001f; y -= cellSize)
        {
            Gizmos.color = lineColor;
            Gizmos.DrawLine(new Vector3(left, y, c.z), new Vector3(right, y, c.z));
        }
    }
}
