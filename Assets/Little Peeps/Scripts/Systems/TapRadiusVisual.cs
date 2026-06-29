using UnityEngine;

// A wireframe ring drawn around the tap cursor so the player can see the AoE that a click will boost.
// Owns a LineRenderer and rebuilds the circle (in local space) whenever the radius changes; TapSystem
// moves this transform to the mouse each frame and toggles visibility. Width/material/color are set on
// the LineRenderer in the inspector — this script only owns the circle geometry and the on/off state.
[RequireComponent(typeof(LineRenderer))]
public class TapRadiusVisual : MonoBehaviour
{
    [SerializeField, Min(8)] private int segments = 48;

    private LineRenderer line;
    private float builtRadius = -1f;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;   // points are local → moving the transform moves the whole ring
        line.loop = true;
    }

    // Rebuilds the ring only when the radius actually changes (cheap to call every frame).
    public void SetRadius(float radius)
    {
        if (Mathf.Approximately(radius, builtRadius)) return;
        builtRadius = radius;

        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    public void SetVisible(bool visible)
    {
        if (line.enabled != visible) line.enabled = visible;
    }
}
