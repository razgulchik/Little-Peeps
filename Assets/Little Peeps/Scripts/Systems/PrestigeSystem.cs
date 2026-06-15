using UnityEngine;

// Calculates prestige points from a run and resets to a new run
public class PrestigeSystem : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private SaveSystem saveSystem;

    private MetaContext metaContext;

    public void Initialize(MetaContext meta)
    {
        metaContext = meta;
    }

    // Derive prestige points from the run's age, buildings, and resources
    public int Calculate(RunContext context)
    {
        // TODO: formula: base points from context.currentAge * multiplier + bonus per building type
        return 0;
    }

    // Award points, persist MetaContext, then start a new run
    public void ExecutePrestige(RunContext context)
    {
        // TODO: metaContext.prestigePoints += Calculate(context); saveSystem.Save(metaContext); runManager.StartNewRun()
    }
}
