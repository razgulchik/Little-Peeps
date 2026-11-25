using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that takes the way home off a tired unit: the moment it runs out of stamina it vanishes in a
    // puff and is in its own house (Unit.OnBecameTired, SpawnSystem.SendHome). Without it a tired unit is slow and
    // gets home by itself, or by a tap.
    //
    // A yes or a no, so there is nothing to author beyond what every meta perk has, and a second one adds nothing.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Tired Go Home")]
    public class TiredGoHomeUpgradeDef : GlobalUpgradeDef
    {
        public override string GeneratedDescription() => "TIRED PEEPS GO STRAIGHT HOME";

        public override void ApplyToRun(RunContext run)
        {
            if (run == null) return;

            run.tiredGoHome = true;
        }
    }
}
