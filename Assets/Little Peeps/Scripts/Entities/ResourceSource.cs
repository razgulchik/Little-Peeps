using UnityEngine;

namespace LittlePeeps
{
    // Resource node behaviour: grants def.resource each time an allowed worker hits the host
    // CollisionTarget, depletes after def.hitsBeforeDespawn hits, then respawns after
    // def.respawnTime. Config lives in the ResourceSourceDef asset; per-instance state lives here.
    // Attach to a natural node (tree/wheat/stone) or a building-source (Forge/Church, infinite def).
    //
    // Two visual states (skipped for infinite sources):
    //   Ready     — ripe/grown, harvestable; shows readyRoot.
    //   Harvested — used up, collider off, regrowing; shows harvestedRoot. After def.respawnTime
    //               it returns to Ready.
    // Each root is fully configured in the prefab (its own SpriteRenderer + Sorting Layer + pivot),
    // so a tall Ready node (wheat/tree) can Y-sort against passing units while the flat Harvested
    // node sits on a lower layer that units always walk over. Infinite sources (Forge/Church) keep
    // their single visual and leave both roots untouched.
    //
    // The Ready→Harvested switch is not instant: the ready root fades out over fadeOutTime while the
    // harvested one shows through underneath, which is what reads as the field being reaped. It is
    // presentation only — the collider is off and the regrow clock is running from the moment of the
    // hit, so no length of fade can ever be harvested through. Speed is per-prefab: a field and a tree
    // vanish at their own rates.
    //
    // swapStateVisuals controls how the two roots are composited:
    //   off (default base) — harvestedRoot is the always-on background base; readyRoot is an overlay
    //                        on top that switches off once harvested. Not mutually exclusive.
    //   on                 — mutually exclusive swap: exactly one root is shown for the current state.
    // Gameplay (collider toggle, deplete, respawn) is identical either way; only the visual differs.
    [RequireComponent(typeof(CollisionTarget))]
    public class ResourceSource : MonoBehaviour, ICollisionEffect
    {
        private enum State { Ready, Harvested }

        [SerializeField] private ResourceSourceDef def;
        [SerializeField] private ResourceSystem resourceSystem; // scene ref — can't live in the SO

        [Header("State visuals (leave empty for infinite sources)")]
        [SerializeField] private GameObject readyRoot;
        [SerializeField] private GameObject harvestedRoot;
        [Tooltip("On: swap one root for the other per state (mutually exclusive). " +
                 "Off (base): harvestedRoot stays on as the background; readyRoot is an overlay that " +
                 "switches off once harvested.")]
        [SerializeField] private bool swapStateVisuals;

        [Header("Harvest VFX")]
        [Tooltip("Where the pickup effect and the floating number leave from. Empty = this transform. " +
                 "Wheat's Visual root is offset from the prefab root, so without an anchor the ear " +
                 "would fly out of the cell's pivot rather than the middle of the field.")]
        [SerializeField] private Transform fxAnchor;

        [Tooltip("Seconds the ready visual takes to fade out when the node is harvested; the harvested " +
                 "sprite underneath shows through as it goes. 0 = it simply disappears. Lives on the " +
                 "prefab rather than the def on purpose — a field and a tree are allowed to vanish at " +
                 "different speeds.")]
        [Min(0f)] [SerializeField] private float fadeOutTime = 0.2f;

        [Tooltip("Alpha across the fade, left to right. Default is a straight 1 → 0.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        private CollisionTarget host;
        private int hitsLeft;
        private State state = State.Ready;
        private float respawnTimer;

        // Cached once: the fade runs per harvest and must not allocate. Covers the whole ready root, so
        // a multi-sprite visual (trunk + crown) fades as one piece without extra wiring.
        private SpriteRenderer[] readyRenderers;
        private float fadeTimer = -1f;   // < 0 = not fading

        private void Awake()
        {
            host = GetComponent<CollisionTarget>();
            if (def != null) hitsLeft = def.hitsBeforeDespawn;
            if (readyRoot != null) readyRenderers = readyRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }

        // Optional runtime injection (StructureSystem calls this when placing a structure at runtime,
        // since a prefab can't serialize a reference to a scene system). Mirrors Spawner.Initialize.
        public void Initialize(ResourceSystem system)
        {
            resourceSystem = system;
        }

        private void Start()
        {
            if (def == null)
                Debug.LogError($"ResourceSource on '{name}' has no ResourceSourceDef assigned.", this);
            if (resourceSystem == null)
                Debug.LogError($"ResourceSource on '{name}' has no ResourceSystem assigned.", this);

            // Infinite sources (Forge/Church) never change state, so they don't need state roots.
            if (def != null && !def.infinite)
            {
                if (readyRoot == null)
                    Debug.LogError($"ResourceSource on '{name}' has no readyRoot assigned.", this);
                if (harvestedRoot == null)
                    Debug.LogError($"ResourceSource on '{name}' has no harvestedRoot assigned.", this);
            }

            ApplyStateVisual();
        }

        // ICollisionEffect — dispatched by CollisionTarget.HandleHit when a unit hits the host.
        public void OnHit(Unit unit, CollisionTarget target)
        {
            if (state == State.Harvested || def == null || resourceSystem == null || unit == null) return;
            if (!def.TryGetYield(unit.Type, out float amount)) return;

            // Through the production gateway: the base amount is scaled by the worker's yield modifier
            // and the global production multiplier before being credited. The def goes along because it
            // is the yield modifier's source scope, not just where the ResourceType came from.
            resourceSystem.AddHarvest(def, unit.Type, amount, FxOrigin);

            if (def.infinite) return;
            if (--hitsLeft <= 0) Deplete();
        }

        // Where harvest feedback leaves from. Authored in the prefab so art decides it, not code.
        private Vector3 FxOrigin => fxAnchor != null ? fxAnchor.position : transform.position;

        private void Update()
        {
            if (fadeTimer >= 0f) TickFade();

            if (state != State.Harvested) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
        }

        // Advances the fade of the ready visual. Deliberately in the existing Update rather than a
        // coroutine: the node already ticks every frame for its regrow timer, so the fade rides along
        // for one float compare and costs no allocation per harvest — which matters when hundreds of
        // fields are being reaped.
        private void TickFade()
        {
            fadeTimer += Time.deltaTime;

            if (fadeTimer < fadeOutTime)
            {
                ApplyAlpha(fadeCurve.Evaluate(fadeTimer / fadeOutTime));
                return;
            }

            EndFade();
        }

        // Settles the roots for the current state and — the part that matters — puts the alpha BACK to
        // 1. These renderers are the same objects the regrown node shows: leaving them transparent
        // would bring the field back invisible seconds later, far from anything that looks like a cause.
        private void EndFade()
        {
            fadeTimer = -1f;
            ApplyAlpha(1f);
            ApplyStateVisual();
        }

        // SpriteRenderer.color is a per-renderer vertex colour, NOT a material property: tinting here
        // creates no material instance and so cannot quietly break batching across hundreds of nodes.
        private void ApplyAlpha(float alpha)
        {
            if (readyRenderers == null) return;

            for (int i = 0; i < readyRenderers.Length; i++)
            {
                var r = readyRenderers[i];
                if (r == null) continue;

                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }

        // Regrow delay with the run modifier applied. The stats sheet is asked for at the point of use,
        // never cached: it belongs to the run, and a node placed in one run outlives it. A perk that
        // speeds regrowth is a NEGATIVE percent — this is the delay in seconds, not a rate. Scoped by
        // the def, so "trees regrow faster" leaves wheat alone; a modifier with no source hits all.
        private float ResolveRespawnTime()
        {
            var stats = resourceSystem != null ? resourceSystem.Stats : null;
            return stats != null
                ? stats.Apply(def.respawnTime, StatId.SourceRespawn, source: def)
                : def.respawnTime;
        }

        // Harvested: used up, collider off, showing the harvested sprite until it regrows.
        private void Deplete()
        {
            state = State.Harvested;
            respawnTimer = ResolveRespawnTime();
            host.SetColliderEnabled(false);

            if (fadeOutTime <= 0f || readyRenderers == null || readyRenderers.Length == 0)
            {
                ApplyStateVisual();
                return;
            }

            // Gameplay is already over for this node — the collider is off and the regrow clock is
            // running — so the fade is pure presentation and its length can be whatever looks right
            // without ever handing out a free harvest.
            //
            // ApplyStateVisual is NOT called yet: it would switch the ready root off outright, which is
            // the very thing being animated. The harvested sprite is brought up front by hand instead,
            // because it is what has to show THROUGH the fading one — in swapStateVisuals mode nothing
            // else would turn it on until the fade ended, and the field would dissolve into bare grass.
            fadeTimer = 0f;
            if (harvestedRoot != null) harvestedRoot.SetActive(true);
            ApplyAlpha(1f);
        }

        // Ready again: regrown, harvestable, showing the ready sprite.
        private void Respawn()
        {
            state = State.Ready;
            hitsLeft = def.hitsBeforeDespawn;
            host.SetColliderEnabled(true);

            // A regrow can land mid-fade whenever a def's respawn time is shorter than the fade (or a
            // perk drags it there). The node has to come back solid either way, so the fade is dropped
            // rather than left to finish over a visual that is already Ready again.
            fadeTimer = -1f;
            ApplyAlpha(1f);

            ApplyStateVisual();
        }

        // Drives the two roots from the current state. Infinite sources keep their single visual, so
        // both roots are left as the prefab set them (typically only one is present and active).
        private void ApplyStateVisual()
        {
            if (def == null || def.infinite) return;

            if (!swapStateVisuals)
            {
                // Base: harvestedRoot is the always-on background; readyRoot overlays it while Ready.
                if (harvestedRoot != null) harvestedRoot.SetActive(true);
                if (readyRoot != null) readyRoot.SetActive(state == State.Ready);
            }
            else
            {
                // Mutually exclusive: exactly the root for the current state is shown.
                if (readyRoot != null) readyRoot.SetActive(state == State.Ready);
                if (harvestedRoot != null) harvestedRoot.SetActive(state == State.Harvested);
            }
        }
    }
}
