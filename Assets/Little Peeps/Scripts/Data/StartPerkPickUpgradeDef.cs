using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that opens every run with a perk pick: the same screen and the same roll an offering
    // gives, before anything else happens on the fresh island. Extra — the picks the offerings earn stay as
    // they are, and a perk taken here is not offered again this run.
    //
    // The perk only owes the run a pick (RunContext.perkPicksOwed); GameplayContainerState sees it from
    // normal play and opens the pick.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Start Perk Pick")]
    public class StartPerkPickUpgradeDef : GlobalUpgradeDef
    {
        public override string GeneratedDescription() => "START WITH A PERK PICK";

        public override void ApplyToRun(RunContext run)
        {
            if (run != null) run.perkPicksOwed++;
        }
    }
}
