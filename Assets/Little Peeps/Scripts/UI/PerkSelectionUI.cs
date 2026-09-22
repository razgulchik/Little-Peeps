using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The perk cards. Owns the CONTENT of the screen and nothing else: it does not apply the perk, does
    // not know the run, and does not decide whether it is on screen. A confirmed card is published as
    // PerkSelectedEvent and PerkSelectionState does the rest.
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
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.PerkPick — but the object
    // must stay ACTIVE for that to work. Awake runs when a GameObject first becomes active, not when the
    // scene loads, so a panel authored inactive would run its Awake inside the very reveal that shows it.
    // Staying active keeps startup and reveal in a fixed order; the CanvasGroup is what hides it.
    [RequireComponent(typeof(CanvasGroup))]
    public class PerkSelectionUI : MonoBehaviour
    {
        [SerializeField] private PerkCardUI cardPrefab;

        [Tooltip("Parent for the spawned cards — give it a Vertical Layout Group.")]
        [SerializeField] private Transform cardContainer;

        private readonly List<PerkCardUI> cards = new();

        // Fill the screen with one card per offered perk. Never called with an empty list: PerkSelection-
        // State skips the whole step instead, so there is no "and if there are none" state to draw. It
        // does not reveal anything: the state published UIMode.PerkPick first, so the panel is already
        // up. This owns the CONTENT of the screen, not the screen.
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
        }

        public void Hide()
        {
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

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) Destroy(cards[i].gameObject);

            cards.Clear();
        }
    }
}
