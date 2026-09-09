using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The perk cards. Owns the screen and nothing else: it does not apply the perk and does not know the
    // run. A confirmed card is published as PerkSelectedEvent and PerkSelectionState does the rest.
    //
    // That split is the same one BuildCardUI → BuildPanelUI → FSM already uses, and it is why this class
    // holds no RunContext. The previous stub took one in an Initialize() called once from
    // GameBootstrap.Awake — a long-lived UI object caching the run, which is exactly the shape that
    // froze the resource bar after the first prestige.
    //
    // Cards are instantiated per offer rather than living in the prefab as three fixed slots, following
    // BuildPanelUI. A short offer then needs no special case: PerkSystem.RollPerks returns fewer than
    // three when fewer are eligible, and the layout group simply gets fewer children.
    //
    // Hidden by a CanvasGroup and NOT by deactivating this object, for the same reason BuildPanelUI does
    // it: Awake runs when a GameObject first becomes ACTIVE, not when the scene loads. A panel authored
    // inactive would run its Awake inside the very Show() that reveals it — and an Awake that hides the
    // panel would then hide it again immediately, leaving the game frozen at timeScale 0 waiting for a
    // pick the player has no cards to make. Staying active keeps startup and reveal in a fixed order.
    [RequireComponent(typeof(CanvasGroup))]
    public class PerkSelectionUI : MonoBehaviour
    {
        [SerializeField] private PerkCardUI cardPrefab;

        [Tooltip("Parent for the spawned cards — give it a Vertical Layout Group.")]
        [SerializeField] private Transform cardContainer;

        [SerializeField] private CanvasGroup canvasGroup;

        private readonly List<PerkCardUI> cards = new();

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // The idle state is established here rather than trusted from the prefab, the same reason
        // AgeSequencer.Awake zeroes its fade overlay: whatever the panel was left showing when the
        // Canvas was last saved would otherwise be what the player sees on the first frame of a run.
        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            // RequireComponent makes this all but impossible, but without a CanvasGroup there is nothing
            // to hide the panel with and it would sit over the island for the whole run — the exact
            // failure this class was just fixed for, so it gets a tripwire rather than silence.
            if (canvasGroup == null)
            {
                Debug.LogError($"PerkSelectionUI on '{name}' has no CanvasGroup — the perk panel cannot " +
                               "be hidden and will cover the game.", this);
                return;
            }

            SetVisible(false);
        }

        // Show the panel with one card per offered perk. Never called with an empty list: PerkSelection-
        // State skips the whole step instead, so there is no "and if there are none" state to draw.
        public void Show(List<PerkDef> perks)
        {
            ClearCards();

            if (cardPrefab == null || cardContainer == null)
            {
                Debug.LogError($"PerkSelectionUI on '{name}' is missing its card prefab or container — " +
                               "the panel would open empty.", this);
                return;
            }

            for (int i = 0; i < perks.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.Init(perks[i], OnCardConfirmed);
                cards.Add(card);
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
            ClearCards();
        }

        private void OnCardConfirmed(PerkDef perk)
        {
            // Lock every card the moment one of them lands. Hiding only happens once the state has
            // handled the event, and until then the other two are still live objects the player can
            // start a second hold on.
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].SetInteractable(false);

            EventBus<PerkSelectedEvent>.Publish(new PerkSelectedEvent { Perk = perk });
        }

        // blocksRaycasts matters as much as alpha: an invisible panel that still swallows clicks would
        // put a dead rectangle over the island, and the build-mode button sits under exactly that area.
        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) Destroy(cards[i].gameObject);

            cards.Clear();
        }
    }
}
