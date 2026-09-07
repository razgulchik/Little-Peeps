using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The price tag under the "New Age" button: one ResourceUnit per entry in the NEXT age's
    // AgeDef.resourceCost. Spawned at runtime from the same prefab the resource bar uses, so a cost of
    // one resource or of five reads correctly with no hand-placed slots to keep in sync — and an
    // icon/format change lands on the wallet and on the price at the same time.
    //
    // Set up in the inspector: the ResourceUnit prefab, the container (the child with the Grid Layout
    // Group), and the shared ResourceIconSet. GameBootstrap injects the systems.
    public class AgeCostPanel : MonoBehaviour
    {
        [SerializeField] private ResourceUnit unitPrefab;
        [SerializeField] private Transform container;        // parent with the Grid Layout Group; defaults to this
        [SerializeField] private ResourceIconSet iconSet;

        [Tooltip("Amount label colour while the player cannot pay that resource. The affordable colour " +
                 "is whatever the ResourceUnit prefab authors — nothing to keep in sync here.")]
        [SerializeField] private Color unaffordableColor = new Color(0.85f, 0.25f, 0.25f);

        [Tooltip("Switched off once the final age is reached and there is nothing left to buy. " +
                 "Leave empty to hide this object.")]
        [SerializeField] private GameObject visibilityRoot;

        private AgeSystem ageSystem;
        private ResourceSystem resourceSystem;

        // The age we are standing IN; the price shown is the transition out of it. Taken from event
        // payloads rather than read back off AgeSystem, so the panel never depends on which of the two
        // handled RunStartedEvent first.
        private int currentAge;

        private readonly List<ResourceUnit> units = new();
        private readonly List<ResourceCost> shown = new();   // the entry each unit displays, same index

        // Injected by GameBootstrap once the run exists.
        public void Initialize(AgeSystem ageSystem, ResourceSystem resourceSystem, RunContext run)
        {
            this.ageSystem = ageSystem;
            this.resourceSystem = resourceSystem;
            if (run != null) currentAge = run.currentAge;
        }

        // Subscribed in Awake/OnDestroy, NOT OnEnable/OnDisable: this panel switches itself off at the
        // final age, and a subscription tied to enabled-ness would go with it — leaving nothing to hear
        // the RunStartedEvent that must bring it back for the next run.
        private void Awake()
        {
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<ResourceChangedEvent>.Subscribe(OnResourceChanged);
        }

        private void OnDestroy()
        {
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<ResourceChangedEvent>.Unsubscribe(OnResourceChanged);
        }

        // Build in Start (not Awake): by now GameBootstrap.Awake has injected the systems and the run
        // exists. Same rule as ResourcePanel — see the Awake note in GameBootstrap / SCENE_SETUP.md.
        private void Start()
        {
            if (ageSystem == null)
            {
                Debug.LogWarning("[AgeCostPanel] never initialized — assign it on GameBootstrap. " +
                                 "The age cost stays blank.", this);
                return;
            }

            Transform parent = container != null ? container : transform;
            if (parent.childCount > 0)
                Debug.LogWarning($"[AgeCostPanel] '{parent.name}' already holds {parent.childCount} " +
                                 "child object(s). Cost units are spawned at runtime — delete the " +
                                 "hand-placed ones or they will sit next to the real prices.", this);

            Rebuild();
        }

        private void OnAgeStarted(AgeStartedEvent e)
        {
            currentAge = e.Age;
            Rebuild();
        }

        // A prestige restarts the ladder at the new run's age — rebuild against that, not the finished run's.
        private void OnRunStarted(RunStartedEvent e)
        {
            currentAge = e.Run != null ? e.Run.currentAge : 0;
            Rebuild();
        }

        // Prices never change with the wallet, only their reachability does — so a resource tick
        // re-tints the existing units instead of rebuilding them.
        private void OnResourceChanged(ResourceChangedEvent e) => RefreshAffordability();

        private void Rebuild()
        {
            if (ageSystem == null) return;

            Clear();

            AgeDef next = ageSystem.TransitionFrom(currentAge);
            GameObject root = visibilityRoot != null ? visibilityRoot : gameObject;
            if (root.activeSelf != (next != null)) root.SetActive(next != null);

            if (next == null || next.resourceCost == null || unitPrefab == null) return;

            Transform parent = container != null ? container : transform;
            for (int i = 0; i < next.resourceCost.Count; i++)
            {
                ResourceCost entry = next.resourceCost[i];
                if (entry == null) continue;

                var unit = Instantiate(unitPrefab, parent);
                unit.Bind(iconSet != null ? iconSet.IconFor(entry.resourceType) : null, entry.amount);
                units.Add(unit);
                shown.Add(entry);
            }

            RefreshAffordability();
        }

        private void RefreshAffordability()
        {
            if (resourceSystem == null) return;

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == null) continue;
                if (resourceSystem.GetResource(shown[i].resourceType) >= shown[i].amount) units[i].ClearTint();
                else units[i].Tint(unaffordableColor);
            }
        }

        private void Clear()
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == null) continue;

                // Deactivate before Destroy: the object survives until the end of the frame, and a
                // layout group would count it alongside the replacements spawned a line later.
                units[i].gameObject.SetActive(false);
                Destroy(units[i].gameObject);
            }

            units.Clear();
            shown.Clear();
        }
    }
}
