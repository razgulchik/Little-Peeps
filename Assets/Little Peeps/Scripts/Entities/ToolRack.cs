using UnityEngine;

namespace LittlePeeps
{
    // A tool rack: a one-cell walk-through structure holding ONE tool of ONE profession. Houses
    // provide the population, racks hand out the professions — an Unassigned worker that crosses a
    // rack with its tool on the stand takes it and works as that profession for the rest of the
    // outing; the tool comes back when the outing ends (Unit.Unequip: house entry, despawn), never
    // before. So the number of racks IS the number of workers of that profession the village can
    // field at once.
    //
    // Two states, two visual roots (the ResourceSource idiom — each fully configured in the prefab):
    //   Available — the tool is on the stand; shows availableRoot.
    //   Taken     — a worker is carrying it; shows takenRoot (the empty stand reads as "in use").
    //
    // Nothing else touches this: the rack has no system injection and no build-mode contract of its
    // own, because the one way a tool returns is through the unit that holds it — build mode
    // despawns every unit, and Despawn unequips. A rack sold with its tool out is simply gone; the
    // worker finishes the outing and the tool vanishes with the rack. Moving keeps the instance and
    // therefore the link.
    [RequireComponent(typeof(CollisionTarget))]
    public class ToolRack : MonoBehaviour, ICollisionEffect
    {
        [Tooltip("The profession this rack's tool confers. Not Unassigned — that is the state a worker " +
                 "arrives in, not one a rack can hand out.")]
        [SerializeField] private ProfessionDef profession;

        [Header("State visuals")]
        [SerializeField] private GameObject availableRoot;
        [SerializeField] private GameObject takenRoot;

        public ProfessionDef Profession => profession;

        // The worker carrying the tool; null while it is on the stand.
        public Unit Holder { get; private set; }
        public bool IsAvailable => Holder == null;

        private void Start()
        {
            if (profession == null)
                Debug.LogError($"ToolRack on '{name}' has no ProfessionDef assigned.", this);
            else if (profession.type == UnitType.Unassigned)
                Debug.LogError($"ToolRack on '{name}' hands out Unassigned — assign a real profession.", this);
            if (availableRoot == null)
                Debug.LogError($"ToolRack on '{name}' has no availableRoot assigned.", this);
            if (takenRoot == null)
                Debug.LogError($"ToolRack on '{name}' has no takenRoot assigned.", this);

            ApplyStateVisual();
        }

        // ICollisionEffect — a WORKING unit crossed the rack (a tired one never gets here; it is on
        // its way home and CollisionTarget routes it to shelters only). Only an Unassigned worker
        // takes the tool: one tool per outing, and an equipped worker passes every other rack without
        // taking or swapping. An empty stand is no effect at all — the unit just walks through.
        public void OnHit(Unit unit, CollisionTarget target)
        {
            if (Holder != null || profession == null || unit == null) return;
            if (unit.Type != UnitType.Unassigned) return;

            Holder = unit;
            unit.Equip(profession, this);
            ApplyStateVisual();
        }

        // The tool comes home — called by Unit.Unequip, the one return path. Only the holder can
        // return it: any other caller is a stale reference and changes nothing.
        public void ReturnTool(Unit unit)
        {
            if (unit == null || unit != Holder) return;

            Holder = null;
            ApplyStateVisual();
        }

        private void ApplyStateVisual()
        {
            bool available = Holder == null;
            if (availableRoot != null) availableRoot.SetActive(available);
            if (takenRoot != null) takenRoot.SetActive(!available);
        }
    }
}
