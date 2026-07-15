using System.Collections;
using UnityEngine;

// On hit, activates a timed production burst; a second hit resets the timer
public class TimedResourceEffect : ICollisionEffect
{
    public ResourceType resourceType;
    public float amountPerSecond;
    public float duration;
    public UnitType requiredUnitType;

    private readonly ResourceSystem resourceSystem;
    private Coroutine activeCoroutine;

    public TimedResourceEffect(ResourceSystem resourceSystem)
    {
        this.resourceSystem = resourceSystem;
    }

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: if unit.Type != requiredUnitType return
        if (activeCoroutine != null)
            target.StopCoroutine(activeCoroutine);
        activeCoroutine = target.StartCoroutine(ProductionCoroutine(target));
    }

    private IEnumerator ProductionCoroutine(CollisionTarget target)
    {
        // TODO: Idle→Active: each frame for duration seconds, resourceSystem.AddHarvest(resourceType, requiredUnitType, amountPerSecond * Time.deltaTime); then Idle
        //       (AddHarvest routes the gain through the yield + production modifiers)
        yield break;
    }
}
