namespace LittlePeeps
{
    // Normal gameplay: units bounce, resources accumulate, and the player builds and sells from the
    // always-open build panel (PlacementController is active exactly while this state is). Every screen
    // over the game — an age, a perk pick, the end of an offering cycle — is entered from here by
    // GameplayContainerState and comes back here.
    //
    // It holds no run and no way to end one: a run ends only by the gods' wrath at the end of an offering
    // cycle (OfferingEndState). The pier's prestige button and its "End this run?" question are gone.
    public class PlayingState : IState
    {
        // Building is part of normal play: placement runs while this state does, and every other screen
        // (a perk pick, an age transition, the end-of-cycle window) starts by dropping the tool in hand.
        private readonly PlacementController placement;

        public PlayingState(PlacementController placement = null)
        {
            this.placement = placement;
        }

        public void Enter()
        {
            // Normal play is the mode every other one falls back to, and the FSM re-Enters this state on
            // every way back — so announcing it here is the whole "put the HUD back" story.
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.Playing });
            if (placement != null) placement.Begin();
        }

        public void Exit()
        {
            if (placement != null) placement.End();   // whatever was in hand goes back before the next screen
        }

        public void Tick()
        {
            // Nothing per-frame. The age advance, the start-of-run perk picks and the offering clock are
            // GameplayContainerState's: each opens a screen of its own over normal play.
        }
    }
}
