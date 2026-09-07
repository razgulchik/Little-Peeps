using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Owns the ordered catalogue of ages and answers "what's next / can we afford it". Pure state/query
    // — it does NOT drive the transition (GameplayContainerState + AgeTransitionState do). The current
    // age lives on RunContext, so this system stays stateless between runs.
    public class AgeSystem : MonoBehaviour
    {
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

        // The AgeDef for the transition OUT of `age` and INTO the next one, or null past the last age.
        // A pure catalogue lookup that touches no run state, so a caller which learned the age from an
        // event payload (the cost UI does) gets the right answer no matter whether this system's own
        // RunStartedEvent handler happened to run before or after theirs — EventBus makes no promise
        // about subscriber order, and nothing here should depend on one.
        public AgeDef TransitionFrom(int age) => (age >= 0 && age < ages.Count) ? ages[age] : null;

        // The AgeDef for advancing into the NEXT age, or null when the final age has been reached.
        // ages[currentAge] is the definition of the transition OUT of the current age into the next.
        public AgeDef NextAge => runContext != null ? TransitionFrom(runContext.currentAge) : null;

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
