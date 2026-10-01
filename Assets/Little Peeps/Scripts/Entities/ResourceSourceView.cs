using UnityEngine;

namespace LittlePeeps
{
    // How a node looks: ready or harvested, and the reap between the two. Presentation only — reads the
    // ResourceSource next to it (IsReady, Depleted, Regrown) and never writes to it, so removing this
    // component leaves the harvest working and the node simply keeps its authored look. Sits on the same
    // object as the ResourceSource: the prefab root of a node, or each tree's own root inside a forest.
    // Only a Regrow source changes state; on a Never or Despawn source this component does nothing.
    //
    // Two visual roots, one per state:
    //   Ready     — ripe/grown, harvestable; shows readyRoot.
    //   Harvested — used up, regrowing; shows harvestedRoot.
    // Each root is fully configured in the prefab (its own SpriteRenderer + Sorting Layer + pivot),
    // so a tall Ready node (wheat/tree) can Y-sort against passing units while the flat Harvested
    // node sits on a lower layer that units always walk over.
    //
    // The Ready→Harvested switch is not instant: the ready root fades out over fadeOutTime while the
    // harvested one shows through underneath, which is what reads as the field being reaped. A second
    // curve on the same clock squashes and stretches the ready root vertically — the kick of the reap.
    // It is presentation only — the source is Harvested (no hit pays) and the regrow clock is running
    // from the moment of the hit, so no length of fade can ever be harvested through. Speed and shape are
    // per-prefab: a field and a tree vanish at their own rates.
    //
    // swapStateVisuals controls how the two roots are composited:
    //   off (default base) — harvestedRoot is the always-on background base; readyRoot is an overlay
    //                        on top that switches off once harvested. Not mutually exclusive.
    //   on                 — mutually exclusive swap: exactly one root is shown for the current state.
    //
    // The build-mode ghost is the prefab with every behaviour disabled, so it shows the roots exactly as
    // authored — author them in the Ready look.
    public class ResourceSourceView : MonoBehaviour
    {
        [SerializeField] private GameObject readyRoot;
        [SerializeField] private GameObject harvestedRoot;
        [Tooltip("On: swap one root for the other per state (mutually exclusive). " +
                 "Off (base): harvestedRoot stays on as the background; readyRoot is an overlay that " +
                 "switches off once harvested.")]
        [SerializeField] private bool swapStateVisuals;

        [Header("Reap")]
        [Tooltip("Seconds the ready visual takes to fade out when the node is harvested; the harvested " +
                 "sprite underneath shows through as it goes. 0 = it simply disappears. Lives on the " +
                 "prefab rather than the def on purpose — a field and a tree are allowed to vanish at " +
                 "different speeds.")]
        [Min(0f)] [SerializeField] private float fadeOutTime = 0.2f;

        [Tooltip("Alpha across the fade, left to right. Default is a straight 1 → 0.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("Vertical scale of the ready visual across the fade, left to right, as a factor of its " +
                 "authored scale. Flat 1 = no squash. Scales the ready root, so the sprite's pivot " +
                 "decides what stays put: wheat is bottom-centre and squashes toward the ground.")]
        [SerializeField] private AnimationCurve fadeScaleY = AnimationCurve.Constant(0f, 1f, 1f);

        private ResourceSource source;

        // Cached once: the fade runs per harvest and must not allocate. Covers the whole ready root, so
        // a multi-sprite visual (trunk + crown) fades as one piece without extra wiring.
        private SpriteRenderer[] readyRenderers;
        private Transform readyTransform;
        private float readyScaleY = 1f;  // the prefab's own scale; the squash curve is a factor on it
        private float fadeTimer = -1f;   // < 0 = not fading

        private bool Regrows => source != null && source.Def != null && source.Def.depletion == Depletion.Regrow;

        private void Awake()
        {
            source = GetComponentInParent<ResourceSource>();
            if (source == null)
                Debug.LogError($"ResourceSourceView on '{name}' has no ResourceSource on it or above it.", this);

            if (readyRoot != null)
            {
                readyRenderers = readyRoot.GetComponentsInChildren<SpriteRenderer>(true);
                readyTransform = readyRoot.transform;
                readyScaleY = readyTransform.localScale.y;
            }
        }

        private void OnEnable()
        {
            if (source == null) return;
            source.Depleted += OnDepleted;
            source.Regrown += OnRegrown;
        }

        private void OnDisable()
        {
            if (source == null) return;
            source.Depleted -= OnDepleted;
            source.Regrown -= OnRegrown;
        }

        private void Start()
        {
            // Only a regrowing source changes state, so only it needs the state roots.
            if (Regrows)
            {
                if (readyRoot == null)
                    Debug.LogError($"ResourceSourceView on '{name}' has no readyRoot assigned.", this);
                if (harvestedRoot == null)
                    Debug.LogError($"ResourceSourceView on '{name}' has no harvestedRoot assigned.", this);
            }

            ApplyStateVisual();
        }

        private void Update()
        {
            if (fadeTimer >= 0f) TickFade();
        }

        // The source has just been used up — on the very hit, so the reap starts on that frame.
        private void OnDepleted()
        {
            if (fadeOutTime <= 0f || readyRenderers == null || readyRenderers.Length == 0)
            {
                ApplyStateVisual();
                return;
            }

            // ApplyStateVisual is NOT called yet: it would switch the ready root off outright, which is
            // the very thing being animated. The harvested sprite is brought up front by hand instead,
            // because it is what has to show THROUGH the fading one — in swapStateVisuals mode nothing
            // else would turn it on until the fade ended, and the field would dissolve into bare grass.
            //
            // Sampled at 0 right here, not left to the first tick: the squash is the field taking the
            // hit, so it has to land on the same frame as the hit, not one later.
            fadeTimer = 0f;
            if (harvestedRoot != null) harvestedRoot.SetActive(true);
            ApplyFade(0f);
        }

        // A regrow can land mid-fade whenever a def's respawn time is shorter than the fade (or a perk
        // drags it there). The node has to come back whole either way, so the fade is dropped rather than
        // left to finish over a visual that is already Ready again.
        private void OnRegrown()
        {
            fadeTimer = -1f;
            ResetFade();
            ApplyStateVisual();
        }

        // Advances the fade of the ready visual. In Update rather than a coroutine: one float compare per
        // frame and no allocation per harvest — which matters when hundreds of fields are being reaped.
        private void TickFade()
        {
            fadeTimer += Time.deltaTime;

            if (fadeTimer < fadeOutTime)
            {
                ApplyFade(fadeTimer / fadeOutTime);
                return;
            }

            // Settles the roots for the current state and — the part that matters — puts the alpha and
            // the scale BACK to authored. These are the same objects the regrown node shows: leaving them
            // transparent or squashed would bring the field back wrong seconds later, far from anything
            // that looks like a cause.
            fadeTimer = -1f;
            ResetFade();
            ApplyStateVisual();
        }

        // Both curves sampled at normalized fade time `k` (0 = the hit, 1 = gone). The writes go
        // through HarvestFade, shared with the Edit Mode preview, so the tool cannot fade a node any
        // differently from the way the game does.
        private void ApplyFade(float k)
        {
            HarvestFade.ApplyAlpha(readyRenderers, fadeCurve.Evaluate(k));
            HarvestFade.ApplyScaleY(readyTransform, readyScaleY * fadeScaleY.Evaluate(k));
        }

        // The ready visual exactly as the prefab authored it.
        private void ResetFade()
        {
            HarvestFade.ApplyAlpha(readyRenderers, 1f);
            HarvestFade.ApplyScaleY(readyTransform, readyScaleY);
        }

        // Drives the two roots from the source's state. Never and Despawn sources keep their single
        // visual, so both roots are left as the prefab set them.
        private void ApplyStateVisual()
        {
            if (!Regrows) return;

            bool ready = source.IsReady;
            if (!swapStateVisuals)
            {
                // Base: harvestedRoot is the always-on background; readyRoot overlays it while Ready.
                if (harvestedRoot != null) harvestedRoot.SetActive(true);
                if (readyRoot != null) readyRoot.SetActive(ready);
            }
            else
            {
                // Mutually exclusive: exactly the root for the current state is shown.
                if (readyRoot != null) readyRoot.SetActive(ready);
                if (harvestedRoot != null) harvestedRoot.SetActive(!ready);
            }
        }
    }
}
