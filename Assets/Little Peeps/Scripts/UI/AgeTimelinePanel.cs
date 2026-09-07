using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The age timeline down the left edge: one card per age already bought, oldest at the top, with
    // the age being bought right now as the highlighted card at the BOTTOM.
    //
    // "The bought age slides up and pushes the others" is the LAYOUT, not code: the container's
    // Vertical Layout Group is bottom-aligned, so a card appended at the end lands at the bottom and
    // shoves the stack upward, and a RectMask2D crops whatever leaves the top. Nothing here positions
    // or animates anything — which is also why the column cannot drift out of sync with itself.
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
        private int currentAge;

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
            currentAge = e.Run != null ? e.Run.currentAge : 0;
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
            IReadOnlyList<AgeDef> ages = ageSystem.Ages;

            // ages[i] is the transition INTO age i+1, so age number N is authored in ages[N-1]. Bought
            // ages are 1..currentAge, and the age being bought now is currentAge+1 — spawned last, so
            // the bottom-aligned layout puts it at the bottom with the history stacked above it.
            for (int number = 1; number <= currentAge + 1; number++)
            {
                int index = number - 1;
                if (index >= ages.Count) break;   // past the final age: nothing is being bought

                AgeDef def = ages[index];
                var card = Instantiate(cardPrefab, parent);
                card.Bind(number, def != null ? def.BonusText : string.Empty, number == currentAge + 1);
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
