using UnityEngine;

// On a fence (edge-placed structure) prefab: the fence is ONE object holding two orientation "poses"
// as child GameObjects — a horizontal child and a vertical child — and shows exactly one depending on
// the edge it sits on. Each pose is fully configured in the prefab in the editor (its own sprite,
// pivot, and later its collider), so orientation-specific setup lives in the prefab, not in code.
// Used by both the placed fence (StructureSystem.BuildEdge) and the build-mode ghost
// (PlacementController), so the preview matches the real fence 1:1, including the correct pose.
public class EdgeStructureVisual : MonoBehaviour
{
    [SerializeField] private GameObject horizontalRoot;
    [SerializeField] private GameObject verticalRoot;

    // Show the pose matching the edge orientation, hide the other.
    public void Apply(bool horizontal)
    {
        if (horizontalRoot != null) horizontalRoot.SetActive(horizontal);
        if (verticalRoot != null) verticalRoot.SetActive(!horizontal);
    }
}
