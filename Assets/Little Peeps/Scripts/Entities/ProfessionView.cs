using UnityEngine;

namespace LittlePeeps
{
    // The unit's body, drawn as its current profession: the frames of Unit.Profession, cycled at a
    // fixed rate. Presentation only — reads the unit every frame and never writes to it, so removing
    // this component leaves the mechanic untouched (the unit just keeps its authored sprite).
    //
    // This IS the walk animation. There is no Animator: two frames per profession do not need a
    // state machine, and an Animator cannot take its frames from a ScriptableObject anyway — a clip
    // is an asset, so every profession would have meant a clip and an override controller of its own.
    // Here the artist draws the frames into the ProfessionDef and that is the whole pipeline. One
    // frame is a legal look: the unit moves without stepping until the second one is drawn.
    //
    // Sits on the model object under the unit's visual root, so it hides with the unit inside a
    // house for free; SpriteShadow follows whatever sprite is set here. Polled, not event-driven,
    // for the same reason as TiredView: the Update is needed to animate anyway.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ProfessionView : MonoBehaviour
    {
        [Tooltip("Frames per second of the walk cycle, for every profession. The old clip ran at 3.")]
        [SerializeField, Min(0.01f)] private float framesPerSecond = 3f;

        private Unit unit;
        private SpriteRenderer sprite;
        private Sprite authored;       // the prefab's own sprite — what shows when a profession has no frames
        private ProfessionDef shown;   // the profession the frames on screen belong to
        private Sprite[] frames;       // shown.frames, or null when there is nothing to cycle
        private float clock;

        private void Awake()
        {
            unit = GetComponentInParent<Unit>();
            sprite = GetComponent<SpriteRenderer>();
            authored = sprite.sprite;

            if (unit == null)
                Debug.LogError($"ProfessionView on '{name}' has no Unit above it.", this);
        }

        // The pool assigns the def before activating, so this runs once with the real def in place.
        private void Start()
        {
            if (unit != null && unit.def != null && unit.def.profession == null)
                Debug.LogWarning($"Unit def '{unit.def.name}' has no profession — its units keep the " +
                                 "prefab's sprite and harvest nothing until one is assigned.", unit.def);
        }

        // Re-sync the moment the visual root comes back on (the unit leaving a house): Unequip has
        // already put the born profession back by then, so the first frame drawn must be its own.
        private void OnEnable()
        {
            if (unit == null) return;
            Show(unit.Profession);
        }

        private void Update()
        {
            if (unit == null) return;

            if (unit.Profession != shown) Show(unit.Profession);
            if (frames == null) return;

            clock += Time.deltaTime;
            int index = (int)(clock * framesPerSecond) % frames.Length;
            if (sprite.sprite != frames[index]) sprite.sprite = frames[index];
        }

        // Switch to a profession's frames, from its first one. No frames at all (no profession, or an
        // empty def) falls back to the prefab's authored sprite — visible, never blank, and never a
        // stale frame of the profession just given up. A single frame is set once and then left
        // alone: nothing to cycle.
        private void Show(ProfessionDef profession)
        {
            shown = profession;
            clock = 0f;

            var f = profession != null ? profession.frames : null;
            if (f == null || f.Length == 0)
            {
                frames = null;
                sprite.sprite = authored;
                return;
            }

            sprite.sprite = f[0];
            frames = f.Length > 1 ? f : null;
        }
    }
}
