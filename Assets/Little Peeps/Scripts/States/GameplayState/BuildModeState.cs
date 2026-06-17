using UnityEngine;

// Build mode: gameplay frozen (Time.timeScale = 0) and all units returned to the pool; they
// respawn from their structures on exit. Placement / ghost / grid overlay arrive in Phase 2.
// The 5s re-entry cooldown is owned by GameplayContainerState, not here.
public class BuildModeState : IState
{
    private readonly SpawnSystem spawnSystem;

    public BuildModeState(SpawnSystem spawnSystem)
    {
        this.spawnSystem = spawnSystem;
    }

    public void Enter()
    {
        Time.timeScale = 0f;
        spawnSystem.DespawnAllAndResetSpawners();
    }

    public void Exit()
    {
        spawnSystem.WarmupAllSpawners();
        Time.timeScale = 1f;
    }

    public void Tick() { }
}
