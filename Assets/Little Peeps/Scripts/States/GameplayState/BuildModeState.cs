namespace LittlePeeps
{
    // Build mode: the build panel is up and the PlacementController drives the ghost + grid overlay. The
    // game is NOT stopped — units keep bouncing and nobody leaves the field. That is the whole point: a mode
    // that froze the world and sent everyone home was a free reset button (fresh stamina, full dens, every
    // trajectory re-rolled), so building now happens in the running game.
    public class BuildModeState : IState
    {
        private readonly PlacementController placement;

        public BuildModeState(PlacementController placement)
        {
            this.placement = placement;
        }

        public void Enter()
        {
            // The palette and the sell button come up with this, the age panel goes down — all of it
            // authored in UIVisibility's table rather than wired from here.
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.Build });
            placement.Begin();
        }

        public void Exit()
        {
            placement.End();
        }

        public void Tick() { }
    }
}
