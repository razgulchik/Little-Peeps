using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // How much a given worker type harvests per hit. The set of entries also defines who is
    // allowed to harvest at all: a worker not listed can't harvest, and an empty list means
    // nobody can (see TryGetYield).
    [System.Serializable]
    public struct WorkerYield
    {
        public UnitType worker;
        [Min(0f)] public float amount;
    }

    // What a source does once it is used up — the one axis every resource in the game differs on.
    // Regrow is first so that it is the default: most sources are trees and fields.
    public enum Depletion
    {
        Regrow,   // used up after hitsToDeplete, stays where it is and comes back after regrowTime:
                  // tree, field, rock, bush, a shorn alpaca that keeps walking
        Despawn,  // used up after hitsToDeplete and removed; its den brings a new one: boar, fox
        Never     // pays on every hit and never runs out: market, smithy
    }

    // Per-type config for a resource source, static (Tree, Wheat, Forge) or mobile (alpaca, boar,
    // fox). The runtime behaviour and per-instance state (hits left, depleted, regrow timer) live in
    // the ResourceSource component, which reads this def. Mirrors StructureDef/UnitDef.
    [CreateAssetMenu(menuName = "LittlePeeps/ResourceSourceDef")]
    public class ResourceSourceDef : ScriptableObject
    {
        public string id;
        public ResourceType resource;

        [Tooltip("Per-worker harvest amount; listed workers are the only ones allowed to harvest " +
                 "(e.g. Farmer 1, Lumberjack 2). Empty = nobody can harvest this source.")]
        public WorkerYield[] workerYields;

        [Header("Harvest VFX")]
        [Tooltip("Pickup effect fired at the harvest point on every credited hit: the ear of wheat " +
                 "leaving a reaped field, a log, a coin. A self-contained ParticleSystem prefab — every " +
                 "curve, the sprite and the sorting layer are the artist's. Empty = this source has no " +
                 "pickup effect (only the floating number).")]
        public ParticleSystem pickupFx;

        [Tooltip("Particles emitted per harvest hit.")]
        [Min(1)] public int pickupFxCount = 1;

        [Header("Sound")]
        [Tooltip("Played on every paying hit: the axe in the tree. It stands for the hit — the unit's plain " +
                 "bounce off this source stays silent. Empty = a paying hit just knocks like a bounce.")]
        public SoundDef hitSound;

        [Tooltip("Played on the hit that uses the source up, over the hit sound: the tree falls. Regrow and " +
                 "Despawn only. Empty = nothing extra.")]
        public SoundDef depleteSound;

        [Header("Depletion / respawn")]
        [Tooltip("What happens once the source is used up. Regrow: it stays and comes back after " +
                 "Regrow Time (tree, field, a shorn alpaca). Despawn: it is removed and its den brings " +
                 "a new one (boar, fox) — only for animals, a structure can't vanish from its cells. " +
                 "Never: pays on every hit forever (market, smithy).")]
        public Depletion depletion = Depletion.Regrow;

        [Tooltip("Paying hits that use the source up. Regrow and Despawn only.")]
        [FormerlySerializedAs("hitsBeforeDespawn")]
        public int hitsToDeplete = 3;

        // An animal that despawns is replaced by its AnimalSpawner, whose spawnCooldown sets that
        // cadence — this is only the in-place regrow.
        [Tooltip("Seconds a used-up source takes to come back. Regrow only.")]
        [FormerlySerializedAs("respawnTime")]
        public float regrowTime = 5f;

        [Tooltip("Regrow only. On: while regrowing the body stays exactly as it is — solid stays solid, " +
                 "a trigger stays a trigger — and a hit on it pays nothing (a shorn alpaca). Off: its " +
                 "colliders switch off and units cross it untouched (a stump, a reaped field).")]
        public bool keepBodyWhileDepleted;

        // Resolves how much `type` harvests per hit. Returns false if the worker isn't listed
        // (an empty workerYields means nobody can harvest this source).
        public bool TryGetYield(UnitType type, out float amount)
        {
            if (workerYields != null)
                for (int i = 0; i < workerYields.Length; i++)
                    if (workerYields[i].worker == type)
                    {
                        amount = workerYields[i].amount;
                        return true;
                    }
            amount = 0f;
            return false;
        }

        // The node's BODY is not stored here: each state is a separate root configured in the prefab
        // (own SpriteRenderer + Sorting Layer + pivot), swapped by ResourceSource. `depletion` already
        // tells whether the source has two states (Regrow) or a single visual (Despawn, Never).
        //
        // `pickupFx` is the exception and belongs here rather than in the prefab, because it answers
        // "what was harvested", not "what does this object look like" — the same question the yield
        // modifiers ask, and they are scoped by this def for the same reason. Keying it on ResourceType
        // instead would fly an ear of wheat out of a boar: both are Food.
    }
}
