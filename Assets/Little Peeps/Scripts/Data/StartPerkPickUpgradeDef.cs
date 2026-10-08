using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that opens every run with a perk pick: the same screen and the same roll an age transition
    // gives, before anything else happens on the fresh island. Extra — the picks after each age stay as they
    // are, and a perk taken here is not offered again this run.
    //
    // Bought ONCE, no levels (the user's call): maxLevel is held at 1. The perk only owes the run a pick
    // (RunContext.perkPicksOwed); GameplayContainerState sees it from normal play and opens the pick. With
    // StartAgeUpgradeDef it stacks: one more pick on top of one per skipped age.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Start Perk Pick")]
    public class StartPerkPickUpgradeDef : GlobalUpgradeDef
    {
        public override string GeneratedDescription => "START WITH A PERK PICK";

        public override void ApplyToRun(RunContext run, int level)
        {
            if (run != null && level > 0) run.perkPicksOwed++;
        }

        // One purchase: a second level would have nothing to add. ApplyUpgrades clamps to maxLevel, so a
        // level saved before this rule can never apply more.
        private void OnValidate()
        {
            maxLevel = 1;
        }
    }
}
