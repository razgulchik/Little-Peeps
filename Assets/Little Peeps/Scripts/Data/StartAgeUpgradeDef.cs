using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that starts every run some ages in: level 1 = the Age II, level 2 = the Age III. Each
    // skipped age is entered for free — its bonus, its zone of island, its perk pick — so the run stands
    // where a run that bought those ages would.
    //
    // The zone choice is skipped too (the user's call): the island grows straight away, every skipped zone in
    // ONE biome set here, since the start is the moment for the most universal land. The perk picks are kept
    // and come one after another before play begins (RunContext.perkPicksOwed).
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

        public override string GeneratedDescription => "+1 STARTING AGE";

        public override void ApplyToRun(RunContext run, int level)
        {
            if (run == null || level <= 0) return;

            run.startAgesToSkip += level;
            run.skippedAgeBiome = zoneBiome;
        }

        // A fresh asset offers Age II and Age III, the two steps asked for. The base default (3) would reach
        // the Age IV.
        private void Reset()
        {
            maxLevel = 2;
        }
    }
}
