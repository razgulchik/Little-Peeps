using System.Collections.Generic;
using UnityEngine;

// Kinematic wandering for animals: pick a point in the owning spawner's territory, walk
// straight to it, pause, repeat. The Rigidbody2D must be Kinematic: the animal then passes
// through terrain (only destinations are validated, by the spawner), while dynamic units
// still get collision callbacks against its collider and bounce off — that bounce is the
// harvest hit (see Animal).
// Reactions are code-driven because kinematic pairs generate no physics callbacks: while
// walking the animal keeps a comfort distance to other animals and probes the ground ahead
// (island edge, impassable structures — see AnimalSpawner.IsBlocked); on either encounter
// it stops, idles, then wanders off biased away from whatever it met.
[RequireComponent(typeof(Rigidbody2D))]
public class AnimalWander : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float pauseMin = 0.5f;
    [SerializeField] private float pauseMax = 2f;
    [Tooltip("Wander radius in world units around the start position — used only by a " +
             "scene-placed animal with no owning spawner (a spawned one asks its spawner " +
             "for destinations).")]
    [SerializeField] private float fallbackRadius = 2f;

    [Header("Reactions")]
    [Tooltip("Visual child mirrored via localScale.x when walking left/right — art must face left.")]
    [SerializeField] private Transform visual;
    [Tooltip("Comfort distance to other animals in world units; getting closer while walking " +
             "means stop and part ways. 0 = animals ignore each other.")]
    [SerializeField] private float personalSpace = 0.6f;
    [Tooltip("Seconds of walking after an encounter during which other animals are ignored, " +
             "so both sides get room to actually part instead of re-triggering in place.")]
    [SerializeField] private float encounterImmunity = 1.5f;
    [Tooltip("Probe distance ahead (world units) for island edge / impassable structures.")]
    [SerializeField] private float lookAhead = 0.3f;

    // Live animals, for the comfort-distance check — a pair of Kinematic bodies never
    // produces collision callbacks, so proximity is polled instead of physics-driven.
    private static readonly List<AnimalWander> alive = new();

    private Rigidbody2D rb;
    private AnimalSpawner owner;   // destination provider; null for scene-placed animals
    private Vector2 fallbackAnchor;
    private Vector2 target;
    private bool moving;
    private float pauseTimer;

    private Vector2 awayBias;      // preferred escape direction after an encounter; zero = none
    private float immunityTimer;   // walking seconds left before neighbor checks resume

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (visual == null)
            Debug.LogError($"AnimalWander on '{name}' has no visual assigned — left/right flip disabled.", this);
    }

    private void OnEnable() => alive.Add(this);
    private void OnDisable() => alive.Remove(this);

    private void Start()
    {
        fallbackAnchor = rb.position;
        // Random initial pause so animals spawned the same frame don't move in lockstep.
        pauseTimer = Random.Range(0f, pauseMax);
    }

    // Runtime injection (AnimalSpawner calls this on spawn).
    public void Initialize(AnimalSpawner spawner) => owner = spawner;

    private void FixedUpdate()
    {
        if (!moving)
        {
            pauseTimer -= Time.fixedDeltaTime;
            if (pauseTimer > 0f) return;

            moving = TryPickTarget(out target);
            if (!moving) { pauseTimer = pauseMax; return; }   // nowhere to go right now — idle and retry

            immunityTimer = awayBias != Vector2.zero ? encounterImmunity : 0f;
            awayBias = Vector2.zero;
            FaceTarget();
            return;
        }

        if (immunityTimer > 0f) immunityTimer -= Time.fixedDeltaTime;
        else if (TryFindCrowding(out Vector2 otherPos))
        {
            StopAndTurnAway(rb.position - otherPos);
            return;
        }

        // Ground probe one step ahead; skipped on the final approach (the destination itself
        // is already validated by the spawner, probing past it would give false positives).
        Vector2 toTarget = target - rb.position;
        if (owner != null && toTarget.sqrMagnitude > lookAhead * lookAhead
            && owner.IsBlocked(rb.position + toTarget.normalized * lookAhead))
        {
            StopAndTurnAway(-toTarget);
            return;
        }

        Vector2 next = Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if ((next - target).sqrMagnitude < 0.0001f)
        {
            moving = false;
            pauseTimer = Random.Range(pauseMin, pauseMax);
        }
    }

    private void StopAndTurnAway(Vector2 away)
    {
        moving = false;
        pauseTimer = Random.Range(pauseMin, pauseMax);
        // Zero vector (exact overlap / degenerate probe) — flee in a random direction instead.
        awayBias = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;
    }

    private bool TryFindCrowding(out Vector2 otherPos)
    {
        otherPos = default;
        if (personalSpace <= 0f) return false;

        float sq = personalSpace * personalSpace;
        for (int i = 0; i < alive.Count; i++)
        {
            var other = alive[i];
            if (other == this) continue;
            Vector2 pos = other.rb.position;
            if ((pos - rb.position).sqrMagnitude >= sq) continue;
            otherPos = pos;
            return true;
        }
        return false;
    }

    private bool TryPickTarget(out Vector2 point)
    {
        // A few samples to honor awayBias: prefer a destination on the far side of whatever
        // we just backed away from; the last sample wins if none qualifies.
        const int tries = 5;
        point = default;
        bool any = false;
        for (int i = 0; i < tries; i++)
        {
            if (!TrySample(out Vector2 candidate)) break;
            any = true;
            point = candidate;
            if (awayBias == Vector2.zero) break;
            if (Vector2.Dot(candidate - rb.position, awayBias) > 0f) break;
        }
        return any;
    }

    private bool TrySample(out Vector2 point)
    {
        if (owner != null) return owner.TryPickPointInTerritory(out point);

        point = fallbackAnchor + Random.insideUnitCircle * fallbackRadius;
        return true;
    }

    private void FaceTarget()
    {
        if (visual == null) return;

        float dx = target.x - rb.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;   // near-vertical walk — keep current facing

        // Animal art faces left, so walking right is the mirrored case.
        Vector3 s = visual.localScale;
        s.x = Mathf.Abs(s.x) * (dx > 0f ? -1f : 1f);
        visual.localScale = s;
    }
}
