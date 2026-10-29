using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Fires the pickup effect for every harvest: the ear of wheat leaving a reaped field, and its
    // equivalent for every other source.
    //
    // Driven by HarvestedEvent, which ResourceSystem.AddHarvest publishes on the single production
    // gateway — so this class never has to be wired to anything that harvests, and nothing that
    // harvests can forget to tell it. Which effect to play: the harvested ResourceSourceDef's own when it
    // has one, else the resource's (ResourceIconSet) — see HarvestedEvent. A gain from no source (a
    // house's tax) takes the resource's.
    //
    // ONE SHARED EMITTER PER EFFECT PREFAB (see SharedEmitter for why, and what that asks of the prefab),
    // created on that effect's first use and reused for the rest of the session — wheat, the mill and a
    // boar flying the same ear share one. The effect is authored as a self-contained prefab; this class
    // only decides WHERE and HOW MANY.
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

        [Tooltip("Each resource's own pickup effect, for whatever pays it without one of its own on the " +
                 "ResourceSourceDef. Empty = only the defs' own effects play.")]
        [SerializeField] private ResourceIconSet resources;

        // effect prefab → its shared emitter. A prefab that was rejected is cached as a null entry, so the
        // error is logged once rather than once per hit for the rest of the run.
        private readonly Dictionary<ParticleSystem, ParticleSystem> emitters = new();

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable() => EventBus<HarvestedEvent>.Subscribe(OnHarvested);
        private void OnDisable() => EventBus<HarvestedEvent>.Unsubscribe(OnHarvested);

        private void OnHarvested(HarvestedEvent e)
        {
            if (!TryPick(e, out var fx, out int count)) return;   // no effect authored for it
            if (!IsOnScreen(e.Position)) return;

            var emitter = Resolve(fx);
            if (emitter == null) return;

            SharedEmitter.EmitAt(emitter, e.Position, count);
        }

        // The source's own effect, else the resource's.
        private bool TryPick(HarvestedEvent e, out ParticleSystem fx, out int count)
        {
            if (e.Source != null && e.Source.pickupFx != null)
            {
                fx = e.Source.pickupFx;
                count = Mathf.Max(1, e.Source.pickupFxCount);
                return true;
            }

            fx = null;
            count = 0;
            return resources != null && resources.TryGetPickupFx(e.Type, out fx, out count);
        }

        // The prefab's shared emitter, created on first use. The result is cached either way: a null entry
        // means the prefab was rejected and must not be re-tested — or re-logged — on every later hit.
        private ParticleSystem Resolve(ParticleSystem fx)
        {
            if (emitters.TryGetValue(fx, out var cached)) return cached;

            var instance = SharedEmitter.Create(fx, transform, $"PickupFx_{fx.name}",
                                                $"HarvestVfxSystem: pickup effect '{fx.name}'",
                                                "pickupFxCount (ResourceSourceDef / ResourceIconSet)");
            emitters[fx] = instance;
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
