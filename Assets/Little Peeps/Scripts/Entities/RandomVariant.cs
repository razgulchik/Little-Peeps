using UnityEngine;

namespace LittlePeeps
{
    // Picks one look for this instance when it spawns: one object from the list stays on, the rest go off for
    // good. Presentation only — nothing in the game reads the choice, so it stays out of the island's seed and
    // the same seed may dress a rock differently from run to run.
    //
    // In Awake, so a variant that goes off never reaches its own Start: SpriteShadow makes its shadow object
    // there, and only the chosen variant ends up with shadows. Whatever fades or hides the parent (the ready
    // root of a ResourceSourceView) leaves the choice alone, so a regrown node comes back looking the same.
    //
    // Author the first variant on and the others off: the prefab then reads as one rock in Prefab Mode.
    public class RandomVariant : MonoBehaviour
    {
        [Tooltip("The looks to choose from, each with equal odds. Exactly one stays active.")]
        [SerializeField] private GameObject[] variants;

        private void Awake()
        {
            if (variants == null || variants.Length == 0)
            {
                Debug.LogError($"RandomVariant on '{name}' has no variants assigned.", this);
                return;
            }

            int chosen = Random.Range(0, variants.Length);
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) variants[i].SetActive(i == chosen);
        }
    }
}
