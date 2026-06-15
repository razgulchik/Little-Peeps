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

    public void OnHit(Unit unit, Building building)
    {
        // TODO: if unit.Type != requiredUnitType return
        if (activeCoroutine != null)
            building.StopCoroutine(activeCoroutine);
        activeCoroutine = building.StartCoroutine(ProductionCoroutine(building));
    }

    private IEnumerator ProductionCoroutine(Building building)
    {
        // TODO: Idle→Active: each frame for duration seconds, resourceSystem.AddResource(resourceType, amountPerSecond * Time.deltaTime); then Idle
        yield break;
    }
}
