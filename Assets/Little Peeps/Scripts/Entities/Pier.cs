using UnityEngine;

namespace LittlePeeps
{
    // Marker for the pier. Nothing reads it at the moment: the prestige button that hung over the pier left
    // with the offering cycle (a run ends by the gods' wrath now). Kept on the prefab for what the pier
    // becomes next — boats coming in, or a mechanism of its own — so that whatever it is can find the run's
    // pier the way the button did: GetComponentInChildren<Pier> on the start structures.
    public class Pier : MonoBehaviour
    {
    }
}
