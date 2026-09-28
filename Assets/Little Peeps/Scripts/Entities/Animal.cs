using UnityEngine;

namespace LittlePeeps
{
    // A den's animal (alpaca, boar, fox). The harvest itself — who is paid, when it is used up,
    // whether it despawns or stands there shorn until its wool regrows — is an ordinary
    // ResourceSource on the same prefab, read from its ResourceSourceDef like a tree's. This component
    // is only the link back to the AnimalSpawner that made it, so the den can refill its slot.
    // Movement lives in AnimalWander.
    public class Animal : MonoBehaviour
    {
        private AnimalSpawner owner;   // null for a scene-placed animal (nobody replaces it)

        // Runtime injection (AnimalSpawner calls this on spawn, since a prefab can't serialize a
        // reference to a scene object).
        public void Initialize(AnimalSpawner spawner)
        {
            owner = spawner;
        }

        // Every way an animal leaves — a Despawn source used up, build mode, the den torn down — ends
        // here, so the slot is freed the same way each time and ResourceSource never has to know
        // what a den is.
        private void OnDestroy()
        {
            if (owner != null) owner.NotifyGone(this);
        }
    }
}
