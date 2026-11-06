using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that starts every run one age further in; a second such upgrade skips one age more. A
    // skipped age is entered for free — its bonus and its zone of island — so the run stands where a run
    // that bought it would. Ages give no perk pick (perks come from the offerings), so neither does a
    // skipped one.
    //
    // The zone choice is skipped too (the user's call): the island grows straight away, every skipped zone in
    // ONE biome set here, since the start is the moment for the most universal land.
    //
    // Only the intent goes into the run here — this has neither the age list nor the island. RunManager
    // carries it out once the island exists (SkipStartAges), through the same TriggerAgeCmd.EnterAge a
    // bought age uses.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Start Age")]
    public class StartAgeUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("The biome every skipped age grows the island by — no zone pick at the start. Use one from " +
                 "IslandSystem's Biomes list, so its tiles are known. Empty = the skipped ages grow no land.")]
        public BiomeDef zoneBiome;

        public override string GeneratedDescription() => "+1 STARTING AGE";

        public override void ApplyToRun(RunContext run)
        {
            if (run == null) return;

            run.startAgesToSkip++;
            run.skippedAgeBiome = zoneBiome;
        }
    }
}
