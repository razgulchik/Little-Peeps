using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Fires the pickup effect for every harvest: the ear of wheat leaving a reaped field, and its
    // equivalent for every other source.
    //
    // Driven by HarvestedEvent, which ResourceSystem.AddHarvest publishes on the single production
    // gateway — so this class never has to be wired to anything that harvests, and nothing that
    // harvests can forget to tell it. Which effect to play is keyed by the harvested
    // ResourceSourceDef (see HarvestedEvent for why not by ResourceType).
    //
    // ONE SHARED EMITTER PER DEF (see SharedEmitter for why, and what that asks of the prefab), created
    // on that def's first harvest and reused for the rest of the session. The effect is authored as a
    // self-contained prefab (def.pickupFx); this class only decides WHERE and HOW MANY.
    //
    // Place on a GameObject in the gameplay scene; it needs no wiring beyond the camera (and that is
    // optional). Emitters are parented under it, so the hierarchy shows exactly which effects are live.
    public class HarvestVfxSystem : MonoBehaviour
    {
        [Tooltip("Used only to skip effects nobody can see. Empty = Camera.main is taken on Awake.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Extra margin around the screen edge, in viewport units, so an effect just outside the " +
                 "view still plays instead of popping in. 0.1 = a tenth of the screen on each side.")]
        [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;

        // def → its shared emitter. A def whose prefab was rejected is cached as a null entry, so the
        // error is logged once rather than once per hit for the rest of the run.
        private readonly Dictionary<ResourceSourceDef, ParticleSystem> emitters = new();

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable() => EventBus<HarvestedEvent>.Subscribe(OnHarvested);
        private void OnDisable() => EventBus<HarvestedEvent>.Unsubscribe(OnHarvested);

        private void OnHarvested(HarvestedEvent e)
        {
            if (e.Source == null || e.Source.pickupFx == null) return;   // no effect authored for it
            if (!IsOnScreen(e.Position)) return;

            var emitter = Resolve(e.Source);
            if (emitter == null) return;

            SharedEmitter.EmitAt(emitter, e.Position, Mathf.Max(1, e.Source.pickupFxCount));
        }

        // The def's shared emitter, created on first use. The result is cached either way: a null entry
        // means the prefab was rejected and must not be re-tested — or re-logged — on every later hit.
        private ParticleSystem Resolve(ResourceSourceDef def)
        {
            if (emitters.TryGetValue(def, out var cached)) return cached;

            var instance = SharedEmitter.Create(def.pickupFx, transform, $"PickupFx_{def.name}",
                                                $"HarvestVfxSystem: '{def.pickupFx.name}' (pickupFx of '{def.name}')",
                                                "ResourceSourceDef.pickupFxCount");
            emitters[def] = instance;
            return instance;
        }

        // An effect nobody can see is not worth emitting. The island grows with every age, so by the
        // late game most harvesting happens outside the view.
        private bool IsOnScreen(Vector3 world)
        {
            if (viewCamera == null) return true;   // nothing to test against: never swallow the effect

            Vector3 v = viewCamera.WorldToViewportPoint(world);
            float m = offscreenMargin;
            return v.z >= 0f && v.x >= -m && v.x <= 1f + m && v.y >= -m && v.y <= 1f + m;
        }
    }
}
