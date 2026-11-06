using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that makes the first expansions of every run free. Only the price goes: the player still
    // presses the button when they want and picks the zone themselves, and the age gives its bonus and counts
    // for prestige like a bought one.
    //
    // Only the count goes into the run here. TriggerAgeCmd uses it up; the button (AgeAdvancePanel) and the
    // price tag (AgeCostPanel) read it off the run.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Free Age")]
    public class FreeAgeUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("How many of the run's first ages cost nothing. Several such upgrades add up.")]
        [Min(1)] public int freeAges = 1;

        // Never below one: a zero typed past [Min] would be an upgrade that gives nothing.
        public int FreeAges => Mathf.Max(1, freeAges);

        public override string GeneratedDescription() =>
            FreeAges == 1 ? "FIRST EXPANSION FREE" : $"FIRST {FreeAges} EXPANSIONS FREE";

        public override void ApplyToRun(RunContext run)
        {
            if (run == null) return;

            run.freeAgesLeft += FreeAges;
        }
    }
}
