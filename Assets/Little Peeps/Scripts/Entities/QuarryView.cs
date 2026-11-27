using UnityEngine;

namespace LittlePeeps
{
    // The quarry's wear: whole until the first paying hit, cracked after it. Presentation only — reads the
    // ResourceSource next to it (HitsLeft) and never writes to it, so removing this leaves the quarry working
    // with its authored look. Sits on the quarry's root, like every view; the renderer it drives is wired in.
    //
    // Only the looks while the quarry still stands live here: the renderer inside ResourceSourceView's ready
    // root shows stages[hits taken] — [0] whole, [1] after one hit, and so on; past the end the last one holds,
    // so a def tuned to more hits than there are sprites still looks right. The broken look while it recharges
    // is ResourceSourceView's harvested root, and the reap between the two is its fade: the last stage is what
    // fades. Author the renderer with stages[0] — the placement ghost is this prefab with every behaviour
    // disabled, so it shows what is authored.
    //
    // The swap is instant. The hit that cracks the quarry pays, and BumpView already squashes the whole Visual
    // for a paying hit; a squash of our own on the same frame would fight that one.
    //
    // Read in LateUpdate rather than off Paid: Paid fires before the source counts the hit down, and a regrow
    // (the source's own Update) or a loaded run changes the count with no hit at all. By LateUpdate every one
    // of them has landed for this frame, whatever order the Updates ran in. One int compare per quarry.
    public class QuarryView : MonoBehaviour
    {
        [Tooltip("The renderer inside the ready root (Model_Ready). Author it with the first stage.")]
        [SerializeField] private SpriteRenderer rock;

        [Tooltip("The look by paying hits taken: [0] whole, [1] after the first hit, and so on. The last one " +
                 "holds for any further hits. The broken look is ResourceSourceView's harvested root.")]
        [SerializeField] private Sprite[] stages;

        private ResourceSource source;

        private void Awake()
        {
            source = GetComponent<ResourceSource>();
            if (source == null)
                Debug.LogError($"QuarryView on '{name}' has no ResourceSource on it.", this);
            if (rock == null)
                Debug.LogError($"QuarryView on '{name}' has no rock renderer assigned.", this);
        }

        private void LateUpdate()
        {
            if (source == null || source.Def == null || rock == null || stages == null || stages.Length == 0) return;

            var look = stages[StageIndex(source.Def.hitsToDeplete - source.HitsLeft, stages.Length)];
            if (look != null && rock.sprite != look) rock.sprite = look;
        }

        // Which of `count` stages shows after `hitsTaken` paying hits: one per hit, the last one holding.
        public static int StageIndex(int hitsTaken, int count) => Mathf.Clamp(hitsTaken, 0, count - 1);
    }
}
