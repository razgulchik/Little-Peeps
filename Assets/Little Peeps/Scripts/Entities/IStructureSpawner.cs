// Contract between structure-mounted spawners (Spawner for units, AnimalSpawner for animals)
// and SpawnSystem's build-mode flow. Each spawner registers itself on its first Warmup and
// unregisters in OnDestroy; SpawnSystem then drives build-mode transitions through this
// interface without knowing the concrete spawner kind. Internals are deliberately NOT shared:
// the unit launch->return->rest slot cycle and the animal die->cooldown->replace cycle have
// nothing in common beyond this contract.
public interface IStructureSpawner
{
    // Build-mode enter: this spawner's spawned entities are gone (or must go) — clear the
    // per-entity bookkeeping so the next Warmup starts fresh.
    void ResetForBuildMode();

    // Placement and build-mode exit: (re)create this spawner's entities.
    void Warmup();
}
