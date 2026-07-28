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

        [Tooltip("The age at which the pier starts working. Below it, clicking the pier does nothing — " +
                 "a run that young isn't worth cashing in. RunContext.currentAge counts transitions " +
                 "from 0, so 3 means 'from the third age transition onwards'.")]
        [Min(0)]
        [SerializeField] private int pierUnlockAge = 3;

        private MetaContext metaContext;

        // Public so the confirmation screen (B2) can say "available from age N" rather than leaving the
        // player clicking what looks like a dead prop.
        public int PierUnlockAge => pierUnlockAge;

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

        // Whether the pier does anything for this run yet. The single source of truth for the unlock —
        // the UI asks it to grey itself out, and ExecutePrestige asks it again before acting.
        public bool CanPrestige(RunContext context)
        {
            return context != null && context.currentAge >= pierUnlockAge;
        }

        // Cash the run in: credit what it beat, raise the records it beat, persist, and start the next
        // run. This is the one place a run ends by a player's choice.
        public void ExecutePrestige(RunContext context)
        {
            if (context == null || metaContext == null || runManager == null) return;

            if (!CanPrestige(context))
            {
                // A warning, not a quiet return: reaching here means a caller skipped the gate, which is
                // a wiring bug. The player-facing "not yet" lives at the click site.
                Debug.LogWarning($"ExecutePrestige at age {context.currentAge}, but the pier opens at " +
                                 $"{pierUnlockAge}. Refused — check the caller's gate.", this);
                return;
            }

            // Read EVERYTHING the finished run is worth before anything moves: Calculate subtracts the
            // records as they stand now, so raising them first would pay the run nothing.
            int payout          = Calculate(context);
            int agePoints       = formula != null ? formula.AgePoints(context)     : 0;
            int harvestPoints   = formula != null ? formula.HarvestPoints(context) : 0;

            metaContext.BankPayout(payout, agePoints, harvestPoints);

            // The only place the payout is visible until the meta screen lands (B4): prestigePoints is
            // on no HUD, and every other effect of a prestige looks exactly like a plain restart.
            Debug.Log($"[Prestige] +{payout} (ages {agePoints}, harvest {harvestPoints}) → " +
                      $"{metaContext.prestigePoints} total; records now {metaContext.agePointsAwarded} / " +
                      $"{metaContext.harvestPointsAwarded}");

            // A no-op until track C implements it, but this is its call site: meta state has just
            // changed, and the run that produced it is about to stop existing.
            if (saveSystem != null) saveSystem.Save(metaContext);

            // Tears the finished run down and builds the next one. Nothing below may read `context` —
            // by the time this returns it is a dead object that RunManager has already replaced.
            runManager.StartNewRun();
        }
    }
}
