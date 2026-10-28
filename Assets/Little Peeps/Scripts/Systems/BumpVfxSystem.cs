using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // Runs the wave of a bump — thin arcs out of the face of a structure a unit just bounced off — for every
    // BumpedEvent (published by BumpView, so nothing has to be wired to the structures). Each verdict has its
    // own wave: the useful one, a shorter one in another colour for a blocked hit, and none for a useless hit
    // unless one is set — the arcs say what the hit meant, so a hit that meant nothing gets none by default. A
    // unit bouncing off the island's edge gets a ripple on the water instead (CoastBumpedEvent, from CoastBump).
    //
    // Every wave is a pixel-art flipbook authored as a self-contained prefab, drawn ONCE, for a hit on a top
    // face: the arcs start at the frame's centre and run up. This class turns it by quarter turns for the other
    // three faces, which keeps every pixel on the grid; the art itself is never scaled, so its lines stay one
    // pixel thin. One shared emitter per wave for the whole game (see SharedEmitter).
    //
    // Place on a GameObject in the gameplay scene; it needs no wiring beyond the prefabs (and the camera,
    // optionally).
    public class BumpVfxSystem : MonoBehaviour
    {
        [Tooltip("The wave of a hit that worked: a ParticleSystem prefab showing one particle per bump as a " +
                 "flipbook (Texture Sheet Animation), drawn for a hit on a TOP face, the arcs starting at the " +
                 "frame's centre. Same rules as every shared emitter: Simulation Space World, Looping on, " +
                 "Emission rates 0.")]
        [FormerlySerializedAs("waveFx")]
        [SerializeField] private ParticleSystem usefulWave;

        [Tooltip("The wave of a Blocked hit — it could have worked, something is in the way (tired, full, " +
                 "overheated, the market from outside). Same layout and rules as the useful wave; shorter and in " +
                 "another colour. Empty = none.")]
        [SerializeField] private ParticleSystem blockedWave;

        [Tooltip("The wave of a Useless hit — a house wall to a working unit, a tree to a farmer. Empty = none: " +
                 "the squash alone shows it.")]
        [SerializeField] private ParticleSystem uselessWave;

        [Tooltip("The ripple of a unit bouncing off the island's edge (CoastBump): a flipbook on the water, round " +
                 "about the frame's centre and never turned. Its sorting must put it ABOVE the water and UNDER the " +
                 "island's tiles, so the land hides the half that would lie on it. Same shared-emitter rules. " +
                 "Empty = none.")]
        [SerializeField] private ParticleSystem coastRipple;

        [Tooltip("Used only to skip waves nobody can see. Empty = Camera.main is taken on Awake.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Extra margin around the screen edge, in viewport units, so a wave just outside the view " +
                 "still plays instead of popping in.")]
        [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;

        [Tooltip("Art pixels per world unit. The wave's centre is snapped to this pixel grid, so its " +
                 "one-pixel lines land on whole pixels. 0 = no snap.")]
        [SerializeField, Min(0)] private int pixelsPerUnit = 16;

        // One shared emitter per verdict, indexed by it, made on first use; a rejected prefab is not tried again.
        private readonly ParticleSystem[] emitters = new ParticleSystem[3];
        private readonly bool[] resolved = new bool[3];
        private ParticleSystem coastEmitter;
        private bool coastResolved;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            EventBus<BumpedEvent>.Subscribe(OnBumped);
            EventBus<CoastBumpedEvent>.Subscribe(OnCoastBumped);
        }

        private void OnDisable()
        {
            EventBus<BumpedEvent>.Unsubscribe(OnBumped);
            EventBus<CoastBumpedEvent>.Unsubscribe(OnCoastBumped);
        }

        private void OnBumped(BumpedEvent e)
        {
            var wave = WaveFor(e.Verdict);
            if (wave == null || !IsOnScreen(e.Position)) return;

            int i = (int)e.Verdict;
            if (!resolved[i])
            {
                resolved[i] = true;
                emitters[i] = SharedEmitter.Create(wave, transform, $"BumpWave{e.Verdict}",
                                                   $"BumpVfxSystem: '{wave.name}' ({e.Verdict} wave)",
                                                   "BumpVfxSystem (one particle per bump)");
            }

            SharedEmitter.EmitAt(emitters[i], Snap(e.Position), 1, TurnFor(e.Side));
        }

        private void OnCoastBumped(CoastBumpedEvent e)
        {
            if (coastRipple == null || !IsOnScreen(e.Position)) return;

            if (!coastResolved)
            {
                coastResolved = true;
                coastEmitter = SharedEmitter.Create(coastRipple, transform, "CoastRipple",
                                                    $"BumpVfxSystem: '{coastRipple.name}' (coast ripple)",
                                                    "BumpVfxSystem (one particle per coast bump)");
            }

            SharedEmitter.EmitAt(coastEmitter, Snap(e.Position), 1);
        }

        private ParticleSystem WaveFor(HitVerdict verdict) => verdict switch
        {
            HitVerdict.Useful => usefulWave,
            HitVerdict.Blocked => blockedWave,
            _ => uselessWave,
        };

        // Degrees to turn the top-face art for a hit on `side`. Particle rotation runs clockwise, so the
        // right face is +90.
        public static float TurnFor(Vector2Int side)
        {
            if (side.x > 0) return 90f;
            if (side.x < 0) return -90f;
            return side.y < 0 ? 180f : 0f;
        }

        private Vector3 Snap(Vector3 world)
        {
            if (pixelsPerUnit <= 0) return world;
            return new Vector3(Mathf.Round(world.x * pixelsPerUnit) / pixelsPerUnit,
                               Mathf.Round(world.y * pixelsPerUnit) / pixelsPerUnit, world.z);
        }

        // A wave nobody can see is not worth emitting — same test as HarvestVfxSystem's.
        private bool IsOnScreen(Vector3 world)
        {
            if (viewCamera == null) return true;   // nothing to test against: never swallow the effect

            Vector3 v = viewCamera.WorldToViewportPoint(world);
            float m = offscreenMargin;
            return v.z >= 0f && v.x >= -m && v.x <= 1f + m && v.y >= -m && v.y <= 1f + m;
        }
    }
}
