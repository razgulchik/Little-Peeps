using UnityEngine;

namespace LittlePeeps
{
    public struct ResourceChangedEvent
    {
        public ResourceType ResourceType;
        public float NewValue;
    }

    // Published by ResourceSystem.AddHarvest for EVERY credited harvest. It sits on the single
    // production gateway on purpose: a resource path that skipped it would have no resources either,
    // so no source can ever ship without its feedback. Consumed by HarvestVfxSystem (pickup
    // particles) and, later, by the floating number — neither of which the gameplay side knows about.
    //
    // Key visuals on `Source`, NOT on `Type`: Wheat, Boar and Fox are all Food, and Alpaka and Market
    // are both Coins, so a per-type table would fly an ear of wheat out of a boar. Same reason
    // AddHarvest itself takes the whole def rather than the ResourceType.
    //
    // `Amount` is what actually reached the wallet, after the yield and production modifiers — the
    // number that floats up must be the number the player got.
    public struct HarvestedEvent
    {
        public ResourceSourceDef Source;
        public ResourceType Type;
        public float Amount;
        public Vector3 Position;
    }

    public struct AgeStartedEvent
    {
        public int Age;
    }

    // Published by RunManager at the end of StartNewRun, once the new run is fully built. Systems that
    // CACHE the RunContext subscribe and re-bind, so a prestige doesn't leave them reading the run that
    // just ended. Systems RunManager initialises itself (Resource/Structure/Spawn) are not among them —
    // they are ordered dependencies of StartNewRun, not observers of it.
    public struct RunStartedEvent
    {
        public RunContext Run;
    }

    // Published by IslandSystem in its LateUpdate, once, on any frame in which it repainted the ground and
    // trim tilemaps — at run start, on every age expansion, and as a rising zone's tiles land one by one
    // (dozens in a couple of seconds, hence once a frame rather than once a repaint). For whatever mirrors
    // the drawn island (WaterSystem's coast obstruction), so it follows the tiles themselves rather than
    // guessing which of the run/age events came after the paint.
    public struct IslandRepaintedEvent { }

    // Published by the AgeAdvancePanel "Next Age" button; handled by GameplayContainerState (enters ZoneSelection).
    public struct AgeAdvanceRequestedEvent { }

    // Published by ZoneSelectionUI when the player picks a zone card; handled by ZoneSelectionState,
    // which moves on to the age transition that grows the island by that zone. Same split as the perk
    // pick: the UI shows and reports, the state acts.
    public struct ZoneSelectedEvent
    {
        public ZoneOffer Offer;
    }

    // Published by the build-mode toggle button OR the build hotkey; handled by GameplayContainerState.
    public struct BuildModeToggleRequestedEvent { }

    // Published by the sell hotkey (GameHotkeys). BuildPanelUI toggles its Sell tool when open, so the
    // button highlight and the PlacementController stay in sync; a no-op outside build mode (panel hidden).
    public struct SellModeRequestedEvent { }

    // Published by the exit-to-menu hotkey (GameHotkeys). GameBootstrap transitions the app FSM to MainMenu.
    public struct ExitToMenuRequestedEvent { }

    // Which screen the game is showing. Published by whichever gameplay state puts that screen up, so
    // "what is on screen" has exactly one source and the FSM stack IS that source — UIVisibility maps it
    // onto CanvasGroups, and no panel has to know about any other panel.
    //
    // [Flags] so a UI group can be authored as "visible in THESE modes" with one mask field in the
    // inspector. The event itself always carries exactly one of them.
    [System.Flags]
    public enum UIMode
    {
        Playing       = 1 << 0,
        Build         = 1 << 1,
        ZonePick      = 1 << 2,
        PerkPick      = 1 << 3,
        AgeTransition = 1 << 4,
    }

    // Published by a gameplay state when its screen goes up. Deliberately at the point the screen is
    // actually shown and not at the top of Enter: a state that aborts before showing anything (an age
    // whose cost moved between the click and here, a perk roll with nothing eligible in it) must not
    // make the mode blink for a frame, hiding the HUD behind a screen that never appeared.
    public struct UIModeChangedEvent
    {
        public UIMode Mode;
    }

    // Pushed by GameplayContainerState for the build-mode toggle BUTTON alone — its label and whether it
    // can be clicked. What is VISIBLE in build mode is not this event's business: that is UIMode, and the
    // button is the one thing the mode cannot express, because the cooldown outlives the mode change.
    public struct BuildModeButtonStateEvent
    {
        public bool InBuildMode;   // true → button shows the resume/play icon (click resumes)
        public bool Interactable;  // false while the 5s post-exit cooldown is running
    }

    // Published by PlacementController when the player tries to build on a valid cell but can't
    // afford it; the BuildPanelUI plays a cue on the selected card.
    public struct BuildDeniedEvent
    {
        public StructureDef Def;
    }

    // Published by TapSystem when the player clicks the pier; handled by PlayingState. Deliberately by
    // the STATE rather than by PrestigeSystem: the subscription then lasts exactly as long as normal
    // play, so a run can never be ended from build mode or mid-age-transition.
    public struct PrestigeTriggeredEvent { }

    // Published by PerkSelectionUI when the player confirms a card; handled by PerkSelectionState, which
    // applies the perk and returns to normal play. The UI deliberately does NOT apply it itself: that is
    // what let the old stub cache a RunContext, and a cached run goes stale the first time a prestige
    // starts a new one.
    public struct PerkSelectedEvent
    {
        public PerkDef Perk;
    }
}
