using UnityEngine;

namespace LittlePeeps
{
    // A ParticleSystem prefab run as ONE shared emitter: instantiated once, played with nothing of its own
    // to emit, then moved to every point that wants the effect and fed a count by hand.
    //
    // A ParticleSystem is at its cheapest when a single system emits many particles: a thousand ears of
    // wheat, or every splash of a rising island, then cost one draw call, no GameObject each and no
    // per-particle managed code. Spawning one system per event would pay the GameObject cost AND lose the
    // batching, which is the worst of both. The effect is still AUTHORED as a self-contained prefab —
    // every curve, the sprite, the spread and the sorting layer belong to the artist. The caller only
    // decides WHERE and HOW MANY.
    //
    // That only works for a prefab built for it, so Create checks it and refuses — loudly, once, naming
    // the asset — rather than quietly fixing the setting in code: a value the inspector shows but the
    // game ignores is the worst thing to hand whoever tunes the effect next.
    public static class SharedEmitter
    {
        // The prefab instantiated under `parent`, cleared and playing, ready for EmitAt — or null when it
        // cannot work as a shared emitter. `label` names it in the messages ("'Splash' (splash of the
        // island rise)"); `countSetting` names what sets the count, for the hint about self-emission.
        // `pausedPlay`: the effect plays while the game is paused (timeScale 0), so it must run on
        // unscaled time or every particle would hang where it was born.
        public static ParticleSystem Create(ParticleSystem prefab, Transform parent, string name, string label,
                                            string countSetting, bool pausedPlay = false)
        {
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, parent);
            instance.name = name;

            if (!Validate(prefab, instance, label, countSetting, pausedPlay))
            {
                // Destroy is refused in Edit Mode, where the island rise's tuning window makes these too.
                if (Application.isPlaying) Object.Destroy(instance.gameObject);
                else Object.DestroyImmediate(instance.gameObject);
                return null;
            }

            // Manual emission needs the system PLAYING but producing nothing on its own: particles handed
            // to Emit() are only simulated while the system runs. Clear() first drops whatever a
            // Play-On-Awake prefab spat out at this object's position while being created.
            instance.Clear();
            instance.Play();
            return instance;
        }

        // Move the shared emitter to the point, then emit there. Deliberately not ParticleSystem.EmitParams:
        // moving the transform keeps the Shape module working exactly as it previews in the inspector, so an
        // authored spawn spread still applies and nothing the artist sets is silently bypassed. Safe only
        // because the prefab is required to simulate in World space — particles already in flight ignore
        // the move.
        public static void EmitAt(ParticleSystem emitter, Vector3 position, int count)
        {
            if (emitter == null || count <= 0) return;
            emitter.transform.position = position;
            emitter.Emit(count);
        }

        private static bool Validate(ParticleSystem prefab, ParticleSystem fx, string label, string countSetting, bool pausedPlay)
        {
            var main = fx.main;

            if (pausedPlay && !main.useUnscaledTime)
            {
                Debug.LogError(
                    $"{label} must have Main > Use Unscaled Time ON. It plays while the game is paused, and a " +
                    "system on game time would freeze every particle where it was born. Effect disabled.", prefab);
                return false;
            }

            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                Debug.LogError(
                    $"{label} must use Main > Simulation Space = World. One emitter is shared by every point " +
                    "that plays this effect and is moved to each of them, so in Local space every particle " +
                    "still in flight would be dragged along to the newest one. Effect disabled.", prefab);
                return false;
            }

            if (!main.loop)
            {
                Debug.LogError(
                    $"{label} must have Main > Looping ON. The shared emitter is played once and then fed by " +
                    "hand; a non-looping system stops itself after its Duration and silently ignores " +
                    "everything after that. Emission stays at 0 either way, so looping costs nothing. " +
                    "Effect disabled.", prefab);
                return false;
            }

            var emission = fx.emission;
            if (emission.enabled && (emission.rateOverTime.constantMax > 0f ||
                                     emission.rateOverDistance.constantMax > 0f))
            {
                Debug.LogWarning(
                    $"{label} emits on its own — Emission > Rate over Time/Distance is not 0. The shared " +
                    "emitter parks at the last point it played at, so it will keep dribbling particles there " +
                    $"in between. Set both rates to 0 and let {countSetting} drive the count.", prefab);
            }

            return true;
        }
    }
}
