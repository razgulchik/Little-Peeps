using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The perk cards. Owns the CONTENT of the screen and nothing else: it does not apply the perk, does
    // not know the run, and does not decide whether it is on screen. A confirmed card is published as
    // PerkSelectedEvent and PerkSelectionState does the rest.
    //
    // That split is the same one BuildCardUI → BuildPaletteUI → FSM already uses, and it is why this class
    // holds no RunContext. The previous stub took one in an Initialize() called once from
    // GameBootstrap.Awake — a long-lived UI object caching the run, which is exactly the shape that
    // froze the resource bar after the first prestige.
    //
    // Cards are instantiated per offer rather than living in the prefab as three fixed slots, following
    // BuildPaletteUI. A short offer then needs no special case: PerkSystem.RollPerks returns fewer than
    // three when fewer are eligible, and fewer cards are laid out.
    //
    // The window is drawn for three cards and keeps that size: fewer are centred in the room meant for
    // three. They are placed from code in whole pixels rather than by a layout group, which would put two
    // cards on half a pixel whenever the leftover room is odd — OfferingWindow.LayOut's reason.
    //
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.PerkPick — but the object
    // must stay ACTIVE for that to work. Awake runs when a GameObject first becomes active, not when the
    // scene loads, so a panel authored inactive would run its Awake inside the very reveal that shows it.
    // Staying active keeps startup and reveal in a fixed order; the CanvasGroup is what hides it.
    [RequireComponent(typeof(CanvasGroup))]
    public class PerkSelectionUI : MonoBehaviour
    {
        // The window is drawn for this many cards. PerkSystem.choicesOffered is capped by it, so a roll
        // never offers more than fit.
        public const int MaxCards = 3;

        [SerializeField] private PerkCardUI cardPrefab;

        [Tooltip("The room the cards are laid out in, tall enough for three — fewer are centred in it. No " +
                 "layout group: the cards are placed from code. Its edges must sit on whole pixels.")]
        [SerializeField] private RectTransform cardContainer;

        [Tooltip("Gap between two cards, in Canvas pixels.")]
        [Min(0)] [SerializeField] private int spacing = 3;

        private readonly List<PerkCardUI> cards = new();

        // Fill the screen with one card per offered perk. Never called with an empty list: PerkSelection-
        // State skips the whole step instead, so there is no "and if there are none" state to draw. It
        // does not reveal anything: the state published UIMode.PerkPick first, so the panel is already
        // up. This owns the CONTENT of the screen, not the screen. The run says which level each card is:
        // the one after what it has taken.
        public void Show(List<PerkDef> perks, RunContext run)
        {
            ClearCards();

            if (cardPrefab == null || cardContainer == null)
            {
                Debug.LogError($"PerkSelectionUI on '{name}' is missing its card prefab or container — " +
                               "the panel would open empty.", this);
                return;
            }

            // A layout group left over on the container would fight the placement below every frame.
            if (cardContainer.GetComponent<LayoutGroup>() != null)
                Debug.LogWarning($"PerkSelectionUI on '{name}': remove the layout group from " +
                                 $"'{cardContainer.name}' — the cards are placed from code.", this);

            int count = perks.Count;
            if (count > MaxCards)
            {
                Debug.LogError($"PerkSelectionUI on '{name}' was given {count} perks but has room for " +
                               $"{MaxCards} — showing the first {MaxCards}.", this);
                count = MaxCards;
            }

            for (int i = 0; i < count; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                int level = (run != null ? run.PerkLevel(perks[i]) : 0) + 1;
                card.Init(perks[i], level, OnCardConfirmed);
                cards.Add(card);
            }

            LayOut();
        }

        public void Hide()
        {
            ClearCards();
        }

        // Distance from the container's top edge to the top edge of card `index`, for `count` cards stacked
        // `spacing` apart and centred as one block. Floor keeps every card on a whole pixel whatever the
        // parity of the leftover room. Pure, so the centring is tested without a Canvas.
        public static float CardTop(int index, int count, float areaHeight, float cardHeight, int spacing)
        {
            float block = count * cardHeight + (count - 1) * spacing;
            return Mathf.Floor((areaHeight - block) / 2f) + index * (cardHeight + spacing);
        }

        // The card's size is its prefab root's, read rather than authored twice.
        private void LayOut()
        {
            Vector2 size = ((RectTransform)cardPrefab.transform).rect.size;
            Rect area = cardContainer.rect;
            float left = Mathf.Floor((area.width - size.x) / 2f);

            for (int i = 0; i < cards.Count; i++)
            {
                var box = (RectTransform)cards[i].transform;
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
                box.sizeDelta = size;
                box.anchoredPosition = new Vector2(left, -CardTop(i, cards.Count, area.height, size.y, spacing));
            }
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
