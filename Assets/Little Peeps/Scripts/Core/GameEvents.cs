namespace LittlePeeps
{
    public struct ResourceChangedEvent
    {
        public ResourceType ResourceType;
        public float NewValue;
    }

    public struct AgeStartedEvent
    {
        public int Age;
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

    // Published by TapSystem when the player clicks the pier. No subscriber yet — PrestigeSystem
    // takes it once the prestige flow is implemented.
    public struct PrestigeTriggeredEvent { }
}
