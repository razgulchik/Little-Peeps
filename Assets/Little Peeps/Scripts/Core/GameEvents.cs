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

    // Published by the AgeUI "Next Age" button; handled by GameplayContainerState (enters AgeTransition).
    public struct AgeAdvanceRequestedEvent { }

    // Published by the build-mode toggle button OR the build hotkey; handled by GameplayContainerState.
    public struct BuildModeToggleRequestedEvent { }

    // Published by the sell hotkey (GameHotkeys). BuildPanelUI toggles its Sell tool when open, so the
    // button highlight and the PlacementController stay in sync; a no-op outside build mode (panel hidden).
    public struct SellModeRequestedEvent { }

    // Published by the exit-to-menu hotkey (GameHotkeys). GameBootstrap transitions the app FSM to MainMenu.
    public struct ExitToMenuRequestedEvent { }

    // Pushed by GameplayContainerState so the toggle button reflects mode + cooldown.
    public struct BuildModeUIStateEvent
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
}
