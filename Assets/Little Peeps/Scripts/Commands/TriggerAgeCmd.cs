namespace LittlePeeps
{
    // Pay for the age and apply its permanent effects: bump the age counter and push the age's stat
    // modifiers into the run. Paying means spending the cost, or using up a free age a meta perk gave the
    // run (FreeAgeUpgradeDef) — the only difference: a free age grants everything a bought one does.
    // Pure state mutation — the transition animation is driven by AgeTransitionState, not here, so the
    // command stays free of any FSM / sequencer knowledge.
    public class TriggerAgeCmd
    {
        private readonly ResourceSystem resourceSystem;
        private readonly RunContext runContext;
        private readonly AgeDef ageDef;

        public TriggerAgeCmd(ResourceSystem resourceSystem, RunContext runContext, AgeDef ageDef)
        {
            this.resourceSystem = resourceSystem;
            this.runContext = runContext;
            this.ageDef = ageDef;
        }

        public bool CanExecute() => CanEnter(resourceSystem, runContext, ageDef);

        public void Execute()
        {
            if (runContext.freeAgesLeft > 0) runContext.freeAgesLeft--;
            else resourceSystem.Spend(ageDef.resourceCost);

            runContext.currentAge++;
            runContext.stats.Add(ageDef.modifiers);   // production/yield/speed/... — all data-driven
        }

        // Whether `run` can enter the age `def` right now: it exists, and it is free or affordable. The one
        // rule, asked by AgeSystem for the button too, so the button and the purchase cannot disagree.
        public static bool CanEnter(ResourceSystem resources, RunContext run, AgeDef def) =>
            def != null && (run.freeAgesLeft > 0 || resources.CanAfford(def.resourceCost));
    }
}
