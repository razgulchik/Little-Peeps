using System.Collections.Generic;
using UnityEngine;

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

    // The AgeDef for advancing into the NEXT age, or null when the final age has been reached.
    // ages[currentAge] is the definition of the transition OUT of the current age into the next.
    public AgeDef NextAge =>
        (runContext != null && runContext.currentAge >= 0 && runContext.currentAge < ages.Count)
            ? ages[runContext.currentAge]
            : null;

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
