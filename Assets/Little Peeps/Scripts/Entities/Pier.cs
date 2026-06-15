using UnityEngine;

// Marker component on the pier; click detection handled by TapSystem via Physics2D raycast.
// Requires a Collider2D on this GameObject so the raycast can find it.
// Units should not collide with it (place on a separate physics layer or use isTrigger).
public class Pier : MonoBehaviour
{
}
