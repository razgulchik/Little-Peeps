using UnityEngine;

// Kinematic wandering for animals: pick a point in the owning spawner's territory, walk
// straight to it, pause, repeat. The Rigidbody2D must be Kinematic: the animal then passes
// through structures and terrain (only destinations are validated, by the spawner), while
// dynamic units still get collision callbacks against its collider and bounce off — that
// bounce is the harvest hit (see Animal).
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

    private Rigidbody2D rb;
    private AnimalSpawner owner;   // destination provider; null for scene-placed animals
    private Vector2 fallbackAnchor;
    private Vector2 target;
    private bool moving;
    private float pauseTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

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
            if (!moving) pauseTimer = pauseMax;   // nowhere to go right now — idle and retry
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

    private bool TryPickTarget(out Vector2 point)
    {
        if (owner != null) return owner.TryPickPointInTerritory(out point);

        point = fallbackAnchor + Random.insideUnitCircle * fallbackRadius;
        return true;
    }
}
