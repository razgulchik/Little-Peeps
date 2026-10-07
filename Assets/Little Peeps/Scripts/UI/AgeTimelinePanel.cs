using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The age timeline down the left edge: the age we are standing IN as the highlighted card at the
    // BOTTOM, and every age still ahead stacked above it — the next one to buy right above. The whole
    // ladder is on screen from the first frame and the column shortens as ages are bought — it is
    // the present and what is coming, not a log of what happened.
    //
    // "The age we leave drops out of the bottom and the rest slide down" is the LAYOUT, not code: the
    // container's Vertical Layout Group is bottom-aligned, so the last card spawned sits at the
    // bottom and the stack grows upward out of a RectMask2D that crops the top. Buying an age
    // simply leaves one card fewer, so the column is a row shorter, every remaining card lands a row
    // lower and the bought age is now the highlighted one. Nothing here positions or animates
    // anything — which is also why the column cannot drift out of sync with itself.
    //
    // Set up in the inspector: the card prefab and the container. GameBootstrap injects the system.
    public class AgeTimelinePanel : MonoBehaviour
    {
        [SerializeField] private AgeTimelineCard cardPrefab;
        [SerializeField] private Transform container;   // bottom-aligned Vertical Layout Group

        private AgeSystem ageSystem;

        // The age we are standing in. Taken from event payloads rather than read back off AgeSystem,
        // so the column never depends on which of the two handled RunStartedEvent first — EventBus
        // promises no subscriber order. Same reasoning as AgeCostPanel.
        private int currentAge = RunContext.FirstAge;

        private readonly List<AgeTimelineCard> cards = new();

        // Injected by GameBootstrap once the run exists.
        public void Initialize(AgeSystem ageSystem, RunContext run)
        {
            this.ageSystem = ageSystem;
            if (run != null) currentAge = run.currentAge;
        }

        private void OnEnable()
        {
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        }

        private void OnDisable()
        {
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        }

        // Build in Start (not Awake): by now GameBootstrap.Awake has injected the system and the run
        // exists. Same rule as ResourcePanel — see the Awake note in GameBootstrap / SCENE_SETUP.md.
        private void Start()
        {
            if (ageSystem == null)
            {
                Debug.LogWarning("[AgeTimelinePanel] never initialized — assign it on GameBootstrap. " +
                                 "The timeline stays empty.", this);
                return;
            }

            Transform parent = container != null ? container : transform;
            if (parent.childCount > 0)
                Debug.LogWarning($"[AgeTimelinePanel] '{parent.name}' already holds {parent.childCount} " +
                                 "child object(s). Cards are spawned at runtime — delete the hand-placed " +
                                 "one or it will sit in the column next to the real ages.", this);

            Rebuild();
        }

        private void OnAgeStarted(AgeStartedEvent e)
        {
            currentAge = e.Age;
            Rebuild();
        }

        // A prestige starts the ladder over — rebuild against the new run's age, not the finished one's.
        private void OnRunStarted(RunStartedEvent e)
        {
            currentAge = e.Run != null ? e.Run.currentAge : RunContext.FirstAge;
            Rebuild();
        }

        // Rebuilt whole rather than appended to: a prestige has to drop the finished run's column
        // anyway, and one path that always produces the right list is worth more than saving nine
        // Instantiate calls on a screen that changes once per age.
        private void Rebuild()
        {
            if (ageSystem == null || cardPrefab == null) return;

            Clear();

            Transform parent = container != null ? container : transform;

            // Counted DOWN from the last age so the current one is spawned last: the bottom-aligned
            // layout puts it at the bottom with the future stacked above it. Each card shows the bonus
            // its own age grants — the current one's is already in effect. The Age I has no AgeDef
            // (never bought), so its card carries no bonus line. On the final age only its own card
            // is left.
            for (int number = ageSystem.LastAge; number >= currentAge; number--)
            {
                AgeDef def = ageSystem.DefOf(number);
                var card = Instantiate(cardPrefab, parent);
                card.Bind(number, def != null ? def.BonusText : string.Empty, number == currentAge);
                cards.Add(card);
            }
        }

        private void Clear()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;

                // Deactivate before Destroy: the object survives until the end of the frame, and the
                // layout group would count it alongside the replacements spawned a line later.
                cards[i].gameObject.SetActive(false);
                Destroy(cards[i].gameObject);
            }

            cards.Clear();
        }
    }
}
