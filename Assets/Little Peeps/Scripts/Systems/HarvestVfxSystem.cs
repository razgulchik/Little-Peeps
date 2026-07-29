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
    // ONE SHARED EMITTER PER DEF, created on that def's first harvest and reused for the rest of the
    // session. A ParticleSystem is at its cheapest when a single system emits many particles: a
    // thousand ears then cost one draw call, no GameObject each and no per-particle managed code.
    // Spawning one system per harvest would pay the GameObject cost AND lose the batching, which is
    // the worst of both. The effect is still AUTHORED as a self-contained prefab (def.pickupFx) —
    // every curve, the sprite, the spread and the sorting layer belong to the artist. This class only
    // decides WHERE and HOW MANY.
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

            // Move the shared emitter to the harvest point, then emit there. Deliberately not
            // ParticleSystem.EmitParams: moving the transform keeps the Shape module working exactly as
            // it previews in the inspector, so an authored spawn spread still applies and nothing the
            // artist sets is silently bypassed. Safe only because the prefab is required to simulate in
            // World space — particles already in flight ignore the move. See Validate.
            emitter.transform.position = e.Position;
            emitter.Emit(Mathf.Max(1, e.Source.pickupFxCount));
        }

        // The def's shared emitter, created on first use. The result is cached either way: a null entry
        // means the prefab was rejected and must not be re-tested — or re-logged — on every later hit.
        private ParticleSystem Resolve(ResourceSourceDef def)
        {
            if (emitters.TryGetValue(def, out var cached)) return cached;

            var instance = Instantiate(def.pickupFx, transform);
            instance.name = $"PickupFx_{def.name}";

            if (!Validate(def, instance))
            {
                Destroy(instance.gameObject);
                instance = null;
            }
            else
            {
                // Manual emission needs the system PLAYING but producing nothing on its own: particles
                // handed to Emit() are only simulated while the system runs. Clear() first drops
                // whatever a Play-On-Awake prefab spat out at this object's position while being created.
                instance.Clear();
                instance.Play();
            }

            emitters[def] = instance;
            return instance;
        }

        // Rejects a prefab that cannot work as a shared emitter — loudly, once, naming the asset.
        // Deliberately refuses instead of quietly fixing the setting in code: a value the inspector
        // shows but the game ignores is the worst thing you can hand whoever tunes the effect next.
        private static bool Validate(ResourceSourceDef def, ParticleSystem fx)
        {
            string label = $"'{def.pickupFx.name}' (pickupFx of '{def.name}')";
            var main = fx.main;

            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                Debug.LogError(
                    $"HarvestVfxSystem: {label} must use Main > Simulation Space = World. One emitter is " +
                    "shared by every source of this def and is moved to each harvest point, so in Local " +
                    "space every ear still in flight would be dragged along to the newest hit. " +
                    "Effect disabled.", def.pickupFx);
                return false;
            }

            if (!main.loop)
            {
                Debug.LogError(
                    $"HarvestVfxSystem: {label} must have Main > Looping ON. The shared emitter is played " +
                    "once and then fed by hand; a non-looping system stops itself after its Duration and " +
                    "silently ignores every harvest after that. Emission stays at 0 either way, so " +
                    "looping costs nothing. Effect disabled.", def.pickupFx);
                return false;
            }

            var emission = fx.emission;
            if (emission.enabled && (emission.rateOverTime.constantMax > 0f ||
                                     emission.rateOverDistance.constantMax > 0f))
            {
                Debug.LogWarning(
                    $"HarvestVfxSystem: {label} emits on its own — Emission > Rate over Time/Distance is " +
                    "not 0. The shared emitter parks at the last harvest point, so it will keep dribbling " +
                    "particles there between hits. Set both rates to 0 and let the harvest drive the " +
                    "count through ResourceSourceDef.pickupFxCount.", def.pickupFx);
            }

            return true;
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
