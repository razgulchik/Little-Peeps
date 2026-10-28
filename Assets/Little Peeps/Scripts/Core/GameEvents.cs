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

    // Published by BumpView when a unit bounces off a structure that shows its bumps; BumpVfxSystem runs the
    // wave of arcs out of it — the one it keeps for the hit's verdict. Side is the face that was hit, as its
    // outward axis — one of up, down, left, right: footprints are boxes on the grid, and the wave's pixel art
    // may only turn by quarter turns.
    public struct BumpedEvent
    {
        public Vector3 Position;
        public Vector2Int Side;
        public HitVerdict Verdict;
    }

    // Published by CoastBump when a unit bounces off the island's edge; BumpVfxSystem runs a ripple over the water
    // from it. Position is already on the waterline — past the outline the art draws outside the land.
    public struct CoastBumpedEvent
    {
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

    // Published by the build panel hotkey (GameHotkeys): show the sell button, the tabs and the palette if
    // they are hidden, hide them if they are shown. Handled by BuildPanelToggle, which owns that state (its
    // own click flips it directly).
    public struct BuildPanelToggleRequestedEvent { }

    // Pushed by BuildPanelToggle whenever the player shows or hides the build panel. UIVisibility applies it
    // to the rows marked to follow it. Not a mode: the game runs the same either way — it is only how much
    // of the screen the panel takes.
    public struct BuildPanelShownEvent
    {
        public bool Shown;
    }

    // Published by the sell hotkey (GameHotkeys). SellButtonUI toggles the sell tool exactly as its click
    // does — PlacementController refuses it outside normal play — and BuildPanelToggle brings a hidden
    // panel back.
    public struct SellModeRequestedEvent { }

    // Published by the digit hotkeys (GameHotkeys): open the build panel's tab at this index (0 = key 1).
    // BuildTabBar opens it during normal play (an index past the last tab is ignored); BuildPanelToggle
    // brings a hidden panel back.
    public struct BuildTabRequestedEvent
    {
        public int Index;
    }

    // Published by BuildTabBar whenever the open build tab changes, the first one included (at its Start).
    // BuildPaletteUI shows that tab's cards, spawning them on the tab's first showing. It carries the tab
    // itself rather than its index, so the palette needs no copy of which tabs got a button.
    public struct BuildTabChangedEvent
    {
        public BuildTab Tab;
    }

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
        Playing         = 1 << 0,
        // 1 << 1 was Build — building is part of Playing now. Left unused so masks authored before keep
        // their meaning bit for bit.
        ZonePick        = 1 << 2,
        PerkPick        = 1 << 3,
        AgeTransition   = 1 << 4,
        // 1 << 5 was PrestigeConfirm — the pier's "End this run?" question, gone with the offering cycle
        // (a run ends at OfferingEnd now). Left unused for the same reason as Build's bit.
        MetaUpgrades    = 1 << 6,
        OfferingEnd     = 1 << 7,
    }

    // Published by a gameplay state when its screen goes up. Deliberately at the point the screen is
    // actually shown and not at the top of Enter: a state that aborts before showing anything (an age
    // whose cost moved between the click and here, a perk roll with nothing eligible in it) must not
    // make the mode blink for a frame, hiding the HUD behind a screen that never appeared.
    public struct UIModeChangedEvent
    {
        public UIMode Mode;
    }

    // Published by PlacementController when the player tries to build on a valid cell but can't
    // afford it; BuildPaletteUI plays a cue on the card of that structure.
    public struct BuildDeniedEvent
    {
        public StructureDef Def;
    }

    // Published by StructureSystem whenever one more or one fewer of a structure stands (built, generated,
    // sold, removed — never moved). The build palette refreshes on it: the count drives that card's price
    // and limit, and the build or sale behind it has just changed the resources too.
    public struct StructureCountChangedEvent
    {
        public StructureDef Def;
        public int Count;
    }

    // Published by anything with a hint while the pointer rests on it (a build card, a HUD perk cell).
    // HintView, the one hint window on the Canvas, shows Content next to Anchor after a short delay, on the
    // Side asked for. Publishing again with the same Owner while it is up refreshes the window in place.
    public struct HintShowEvent
    {
        public object Owner;
        public HintContent Content;
        public RectTransform Anchor;
        public HintSide Side;
    }

    // Where the hint window goes against its anchor. Above (the default) flips below when the Canvas top is
    // too close; Left — for a column at the screen's right edge — flips right when the left edge is.
    public enum HintSide
    {
        Above,
        Left,
    }

    // The pointer left that source. Ignored unless Owner is the one whose hint is up or pending — the card
    // just left must not close the hint the next card has already asked for.
    public struct HintHideEvent
    {
        public object Owner;
    }

    // Published by PerkSelectionUI when the player confirms a card; handled by PerkSelectionState, which
    // applies the perk and returns to normal play. The UI deliberately does NOT apply it itself: that is
    // what let the old stub cache a RunContext, and a cached run goes stale the first time a prestige
    // starts a new one.
    public struct PerkSelectedEvent
    {
        public PerkDef Perk;
    }

    // Published by PerkSystem.ApplyPerk once the perk has taken effect and stands in RunContext.perksChosen —
    // the one place a perk enters a run, whichever way it was picked. The HUD's perk panel adds a cell on it.
    // Not PerkSelectedEvent: that one is a request, and the panel would hear it before the perk is applied.
    public struct PerkAppliedEvent
    {
        public PerkDef Perk;
    }
}
