using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public BuildingDef def;

    private readonly List<ICollisionEffect> effects = new();
    private BoxCollider2D col;
    private float currentHealth;

    private void Awake()
    {
        // TODO: col = GetComponent<BoxCollider2D>(), currentHealth = def.maxHealth (add field to BuildingDef when implementing)
    }

    // Obstacle path: unit bounces off (isTrigger = false on collider)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Unit>(out var unit))
            HandleHit(unit);
    }

    // Interactable path: unit passes through (isTrigger = true on collider)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Unit>(out var unit))
            HandleHit(unit);
    }

    private void HandleHit(Unit unit)
    {
        effects.ForEach(e => e.OnHit(unit, this));
        EventBus<CollisionEvent>.Publish(new CollisionEvent(unit, this));
    }

    // Add an effect (called at runtime when building is placed, based on def.effects)
    public void AddEffect(ICollisionEffect effect)
    {
        // TODO: effects.Add(effect)
    }

    // Reduce health; publish damaged/destroyed events at appropriate thresholds
    public void TakeDamage(float amount)
    {
        // TODO: currentHealth -= amount; publish BuildingDamagedEvent; if currentHealth <= 0 publish BuildingDestroyedEvent
    }

    // Enable or disable the collider (used by DragController during drag)
    public void SetColliderEnabled(bool enabled)
    {
        // TODO: col.enabled = enabled
    }
}
