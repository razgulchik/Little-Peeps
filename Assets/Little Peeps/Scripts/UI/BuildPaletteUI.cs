using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The build palette: the cards of the open tab, on the panel at the bottom of the screen. One of the
    // build panel's widgets, each owning one thing — the tab column (BuildTabBar) which tab is open, the
    // sell button (SellButtonUI) its own tool, the build toggle (BuildPanelToggle) whether the panel is
    // shown at all. This one owns the cards:
    //
    //  - A tab's cards are spawned the first time BuildTabChangedEvent opens it and live in that tab from
    //    then on; a switch only shows / hides them, so a hidden card keeps its state. The tab arrives in the
    //    event, so the palette keeps no copy of BuildPaletteDef.
    //  - A click ASKS PlacementController for the card's structure, or to put it down if it is the one in
    //    hand. Which card is selected — the open one, with the building's name and terrains — is read back
    //    from the controller (ToolChanged), never kept here — so the sell button taking the hand, a
    //    right-click or the end of normal play close the card without anybody telling the palette.
    //  - Prices, counts and locks follow the age, the run, the resources and every build or sale.
    //
    // Live in UIMode.Playing, asleep over any other screen; whether it is on screen is UIVisibility's table,
    // not this class.
    public class BuildPaletteUI : MonoBehaviour, IUIElement
    {
        [SerializeField] private BuildCardUI cardPrefab;
        [SerializeField] private Transform cardContainer;   // parent with a Horizontal Layout Group
        [SerializeField] private BuildPanelScroller scroller;

        private PlacementController placement;   // from UIServices: the tool in hand
        private ResourceSystem wallet;           // from UIServices: the cards' wallet

        private readonly Dictionary<BuildTab, List<BuildCardUI>> tabCards = new();
        private readonly List<BuildCardUI> cards = new();   // every card spawned so far, whatever its tab
        private BuildTab currentTab;

        // Boots live. The game boots straight into normal play (GameplayContainerState enters PlayingState
        // inside GameBootstrap.Awake), and that first UIModeChangedEvent can fire before this object
        // subscribes — Unity gives no order between the two. Starting where the boot lands makes the order
        // irrelevant: the event either arrives and changes nothing, or was missed and this already said it.
        // UIVisibility's rule.
        private bool live = true;

        private int currentAge = RunContext.FirstAge;
        private RunContext run;   // for the structures a perk opened and how many stand (StructureDef.LockState)
        private bool cardsDirty;  // a count or the wallet changed — refresh in LateUpdate (OnStructureCountChanged)

        // From UIRoot, once, in GameBootstrap.Awake — possibly before this object's own Awake, so it only
        // takes what it needs. The run is read here as well: the first one starts in that same Awake, before
        // this object can hear RunStartedEvent. The controller is heard for the palette's whole life (until
        // OnDestroy), not per enable: it lives as long as the Canvas.
        public void Initialize(UIServices ui)
        {
            if (placement != null) placement.ToolChanged -= OnToolChanged;
            placement = ui.PlacementController;
            wallet = ui.ResourceSystem;
            if (placement != null) placement.ToolChanged += OnToolChanged;

            run = ui.RunManager != null ? ui.RunManager.CurrentRun : null;
            if (run != null) currentAge = run.currentAge;
        }

        // Nothing to build here: the cards come with the first tab (BuildTabBar announces it from its Start).
        private void Start()
        {
            if (cardPrefab == null || cardContainer == null)
                Debug.LogWarning("[BuildPaletteUI] Card Prefab or Card Container is not assigned — the " +
                                 "palette stays empty.", this);
        }

        private void OnDestroy()
        {
            if (placement != null) placement.ToolChanged -= OnToolChanged;
        }

        private void OnEnable()
        {
            EventBus<UIModeChangedEvent>.Subscribe(OnUIMode);
            EventBus<BuildTabChangedEvent>.Subscribe(OnTabChanged);
            EventBus<BuildDeniedEvent>.Subscribe(OnBuildDenied);
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<StructureCountChangedEvent>.Subscribe(OnStructureCountChanged);
            EventBus<ResourceChangedEvent>.Subscribe(OnResourceChanged);
        }

        private void OnDisable()
        {
            EventBus<UIModeChangedEvent>.Unsubscribe(OnUIMode);
            EventBus<BuildTabChangedEvent>.Unsubscribe(OnTabChanged);
            EventBus<BuildDeniedEvent>.Unsubscribe(OnBuildDenied);
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<StructureCountChangedEvent>.Unsubscribe(OnStructureCountChanged);
            EventBus<ResourceChangedEvent>.Unsubscribe(OnResourceChanged);
        }

        // Shows the open tab's cards and hides the rest. The tool in hand is left alone: a hidden card stays
        // in hand, and shows as selected again when its tab comes back.
        private void OnTabChanged(BuildTabChangedEvent e)
        {
            BuildTab tab = e.Tab;
            if (tab == null || tab == currentTab) return;

            if (currentTab != null)
            {
                foreach (var card in tabCards[currentTab])
                {
                    card.ResetInteractionVisuals();   // drop a hover that would resume half-way on return
                    card.gameObject.SetActive(false);
                }
            }

            currentTab = tab;
            foreach (var card in CardsOf(tab)) card.gameObject.SetActive(true);

            if (scroller != null) scroller.ResetToStart();
        }

        // A tab's cards, spawned the first time it opens. Each is brought up to date at once: the age, the
        // run and the hand may all have moved on since the game booted.
        private List<BuildCardUI> CardsOf(BuildTab tab)
        {
            if (tabCards.TryGetValue(tab, out var list)) return list;

            list = new List<BuildCardUI>();
            tabCards.Add(tab, list);
            if (cardPrefab == null || cardContainer == null) return list;

            foreach (var def in tab.structures)
            {
                if (def == null) continue;

                var card = Instantiate(cardPrefab, cardContainer);
                card.Init(def, OnCardClicked, wallet);
                list.Add(card);
                cards.Add(card);

                Refresh(card);
                card.SetSelected(placement != null && placement.HeldStructure == def);
            }

            return list;
        }

        // A card asks; the controller decides. Clicking the card already in hand puts it down.
        private void OnCardClicked(BuildCardUI card)
        {
            if (card == null || card.IsLocked || placement == null) return;

            if (placement.HeldStructure == card.Def) placement.PutDown();
            else placement.Select(card.Def);
        }

        // Every change of the hand, whoever made it — a card, the sell button, a right-click, a limit
        // reached, the end of normal play. Hidden cards included, so the tab coming back shows the truth.
        private void OnToolChanged()
        {
            StructureDef held = placement.HeldStructure;
            foreach (var card in cards) card.SetSelected(card.Def == held);
        }

        // The cards' CONTENT follows the mode; whether they are on screen is UIVisibility's row. Only the
        // crossing is acted on: every screen reports itself, and a perk pick right after an age transition
        // must not put the cards to sleep twice. Nothing here puts the tool down — leaving normal play ends
        // the controller, which empties the hand and says so (ToolChanged).
        private void OnUIMode(UIModeChangedEvent e)
        {
            bool nowLive = e.Mode == UIMode.Playing;
            if (nowLive == live) return;
            live = nowLive;

            if (live)
            {
                RefreshCards();   // whatever moved while the palette slept
                if (scroller != null) scroller.RefreshBounds();
            }
            else
            {
                foreach (var card in cards) card.ResetInteractionVisuals();
            }
        }

        // On waking, after every build or sale (OnStructureCountChanged), and whenever the resources move
        // (OnResourceChanged) — the game keeps harvesting with the panel up, so a card can become affordable
        // while the player looks at it. Beyond those, only the age, the run and a perk's unlock change a
        // card — and the perk pick is its own screen, never taken with this panel live.
        private void RefreshCards()
        {
            foreach (var card in cards) Refresh(card);
        }

        private void Refresh(BuildCardUI card)
        {
            StructureDef def = card.Def;
            card.ShowCount(run);   // first: the limit text in SetLocked reads the count
            StructureLock state = def.LockState(currentAge, run);
            card.SetLocked(state);
            card.SetAffordable(wallet == null || wallet.CanAfford(def, run != null ? run.CountOf(def) : 0));

            // Filling the limit takes the card out of the hand.
            if (state != StructureLock.None && placement != null && placement.HeldStructure == def)
                placement.PutDown();
        }

        // A build or a sale changes the next price, the count against the limit, and the resources. The
        // refresh waits for LateUpdate on purpose: this fires from INSIDE the tool's click
        // (PlaceTool → StructureSystem), and a card locking on its limit would put the tool down while that
        // click is still running. Deferred, the tool finishes its click first; one refresh per frame also
        // absorbs a burst (an island's worth of generated trees).
        private void OnStructureCountChanged(StructureCountChangedEvent e) => cardsDirty = true;

        // Harvests land every few frames while the island works; the same deferred refresh folds them into
        // at most one per frame, and only while the palette is live (LateUpdate checks it).
        private void OnResourceChanged(ResourceChangedEvent e) => cardsDirty = true;

        private void LateUpdate()
        {
            if (!cardsDirty) return;
            cardsDirty = false;
            if (live) RefreshCards();
        }

        private void OnAgeStarted(AgeStartedEvent e)
        {
            currentAge = e.Age;
            RefreshCards();
        }

        // A prestige replaces the run: re-bind before refreshing, or the counts and the perk unlocks would be
        // the finished run's.
        private void OnRunStarted(RunStartedEvent e)
        {
            run = e.Run;
            currentAge = e.Run != null ? e.Run.currentAge : RunContext.FirstAge;
            RefreshCards();
        }

        // PlaceTool publishes it for the structure in hand.
        private void OnBuildDenied(BuildDeniedEvent e)
        {
            foreach (var card in cards)
                if (card.Def == e.Def) card.PlayDeniedCue();
        }
    }
}
