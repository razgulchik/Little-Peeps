using UnityEngine;

namespace LittlePeeps
{
    // Marker for the pier: the prestige button finds the run's pier by this component (anywhere in the
    // prefab) and hangs over it on screen. The pier itself is not clickable any more — the button is the
    // one way into a prestige, and it only appears once the run can be cashed in.
    public class Pier : MonoBehaviour
    {
        [Tooltip("Where the prestige button stands: an empty child placed at the spot, usually just above " +
                 "the deck. Empty = the pier's own position.")]
        [SerializeField] private Transform buttonAnchor;

        public Vector3 ButtonPoint => (buttonAnchor != null ? buttonAnchor : transform).position;
    }
}
