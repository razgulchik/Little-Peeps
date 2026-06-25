using UnityEngine;

// A structure visual that holds two interchangeable child "roots" and shows exactly one of them,
// chosen by the placement code at the moment the structure lands on the grid. Two unrelated structures
// share this one switch for different reasons:
//   - a fence picks by edge orientation  (firstRoot = horizontal pose, secondRoot = vertical pose);
//   - a forest picks by grid-row parity  (firstRoot = even-row layout, secondRoot = odd-row layout),
//     so neighbouring forests interlock into a brick-laid pattern.
// The deciding condition lives in the CALLER (StructureSystem / PlacementController), not here — this
// component only knows "show one of two". Each root is fully configured in the prefab (its own
// sprites/children), so per-pose art lives in the prefab, not in code. Used by both the placed
// structure and the build-mode ghost, so the preview matches the real structure 1:1.
public class DualVisual : MonoBehaviour
{
    [SerializeField] private GameObject firstRoot;
    [SerializeField] private GameObject secondRoot;

    // Show firstRoot when `first` is true, secondRoot otherwise; hide the other.
    public void Show(bool first)
    {
        if (firstRoot != null) firstRoot.SetActive(first);
        if (secondRoot != null) secondRoot.SetActive(!first);
    }
}
