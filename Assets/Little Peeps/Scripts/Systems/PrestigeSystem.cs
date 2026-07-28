using UnityEngine;

namespace LittlePeeps
{
    // Calculates prestige points from a run and resets to a new run
    public class PrestigeSystem : MonoBehaviour
    {
        [SerializeField] private RunManager runManager;
        [SerializeField] private SaveSystem saveSystem;

        [Tooltip("The payout: points per age, plus a curve on everything the run harvested.")]
        [SerializeField] private PrestigeFormula formula = new();

        private MetaContext metaContext;

        public void Initialize(MetaContext meta)
        {
            metaContext = meta;
        }

        // What this run is worth in prestige points. Pure and side-effect free — the confirmation screen
        // (B2) shows the projection by calling exactly this. The formula itself lives in PrestigeFormula
        // so the arithmetic is inspector-tunable and testable without a scene.
        //
        // The records are read here rather than passed in: what has already been paid out belongs to the
        // profile, not to the run, so no caller has to know about them to get an honest number.
        public int Calculate(RunContext context)
        {
            return formula != null ? formula.Points(context, metaContext) : 0;
        }

        // Award points, persist MetaContext, then start a new run
        public void ExecutePrestige(RunContext context)
        {
            // TODO: metaContext.prestigePoints += Calculate(context); saveSystem.Save(metaContext); runManager.StartNewRun()
        }
    }
}
