using UnityEngine;

// Build mode: gameplay frozen (Time.timeScale = 0) and all units returned to the pool; they
// respawn from their structures on exit. The PlacementController drives the ghost + grid overlay
// while we're here. The 5s re-entry cooldown is owned by GameplayContainerState, not here.
public class BuildModeState : IState
{
    private readonly SpawnSystem spawnSystem;
    private readonly PlacementController placement;

    public BuildModeState(SpawnSystem spawnSystem, PlacementController placement)
    {
        this.spawnSystem = spawnSystem;
        this.placement = placement;
    }

    public void Enter()
    {
        Time.timeScale = 0f;
        spawnSystem.DespawnAllAndResetSpawners();
        placement.Begin();
    }

    public void Exit()
    {
        placement.End();
        spawnSystem.WarmupAllSpawners();
        Time.timeScale = 1f;
    }

    public void Tick() { }
}
