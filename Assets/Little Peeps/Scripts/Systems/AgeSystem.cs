using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Owns the ordered catalogue of ages and answers "what's next / can we afford it". Pure state/query
    // — it does NOT drive the transition (GameplayContainerState + AgeTransitionState do). The current
    // age lives on RunContext, so this system stays stateless between runs.
    public class AgeSystem : MonoBehaviour
    {
        // Only the ages that are BOUGHT. The Age I is where every run starts — never bought, so it has no
        // AgeDef: what a run starts with lives on the StartConfig (and later on the meta upgrades).
        [Tooltip("The ages a run can buy, in order. The first entry is the Age II: the Age I is where " +
                 "every run starts, it is never bought and has no AgeDef.")]
        [SerializeField] private List<AgeDef> ages = new();
        [SerializeField] private ResourceSystem resourceSystem;

        private RunContext runContext;

        public void Initialize(RunContext context)
        {
            runContext = context;
        }

        // Re-bind on every run after the first. Without this a prestige would leave NextAge/CanAdvance
        // reading the finished run's currentAge, so the new run would resume the old one's age ladder.
        private void OnEnable()  => EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        private void OnDisable() => EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);

        private void OnRunStarted(RunStartedEvent e) => runContext = e.Run;

        // The number of the final age (player's number, see RunContext.currentAge): the Age I plus one
        // per bought age. Exposed so the timeline column can draw every age, not only the next one.
        public int LastAge => RunContext.FirstAge + ages.Count;

        // The AgeDef OF the Age `age`: the price of entering it and the bonus it grants. Null for the
        // Age I (never bought) and past the last age — always look ages up through here, never by
        // indexing the list, so the one offset lives in one place.
        //
        // A pure catalogue lookup that touches no run state, so a caller which learned the age from an
        // event payload (the cost and timeline UI do) gets the right answer no matter whether this
        // system's own RunStartedEvent handler happened to run before or after theirs — EventBus makes
        // no promise about subscriber order, and nothing here should depend on one.
        public AgeDef DefOf(int age)
        {
            int i = age - (RunContext.FirstAge + 1);
            return (i >= 0 && i < ages.Count) ? ages[i] : null;
        }

        // The AgeDef of the NEXT age — the one the "New Age" button buys — or null at the final age.
        public AgeDef NextAge => runContext != null ? DefOf(runContext.currentAge + 1) : null;

        // True when there is a next age and its cost is currently affordable.
        public bool CanAdvance
        {
            get
            {
                var next = NextAge;
                return next != null && resourceSystem != null && resourceSystem.CanAfford(next.resourceCost);
            }
        }
    }
}
