// Spend resources and start the age transition sequence
public class TriggerAgeCmd : ICommand
{
    private readonly ResourceSystem resourceSystem;
    private readonly AgeSequencer ageSequencer;
    private readonly RunContext runContext;
    private readonly AgeDef ageDef;

    public TriggerAgeCmd(ResourceSystem resourceSystem, AgeSequencer ageSequencer, RunContext runContext, AgeDef ageDef)
    {
        this.resourceSystem = resourceSystem;
        this.ageSequencer = ageSequencer;
        this.runContext = runContext;
        this.ageDef = ageDef;
    }

    public bool CanExecute()
    {
        // TODO: for each entry in ageDef.resourceCost, check resourceSystem.GetResource(type) >= amount
        return true;
    }

    public void Execute()
    {
        // TODO: deduct ageDef.resourceCost; runContext.currentAge++; ageSequencer.StartAgeTransition(runContext.currentAge, runContext)
    }
}
