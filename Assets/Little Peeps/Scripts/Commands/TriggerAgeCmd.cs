// Spend the age cost and apply its permanent effects: bump the age counter and push the age's stat
// modifiers into the run. Pure state mutation — the transition animation is driven by
// AgeTransitionState, not here, so the command stays free of any FSM / sequencer knowledge.
public class TriggerAgeCmd : ICommand
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

    public bool CanExecute() => ageDef != null && resourceSystem.CanAfford(ageDef.resourceCost);

    public void Execute()
    {
        resourceSystem.Spend(ageDef.resourceCost);
        runContext.currentAge++;
        runContext.stats.Add(ageDef.modifiers);   // production/yield/speed/... — all data-driven
    }
}
