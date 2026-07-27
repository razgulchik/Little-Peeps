namespace LittlePeeps
{
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

        // Placement and build-mode exit: register (once), then (re)create this spawner's entities.
        // Called on placement during build mode too, where it registers but SKIPS spawning (reads
        // SpawnSystem.IsBuildMode) — the entities are created by the build-mode-exit warmup, once the
        // flag has cleared, so a structure built mid-session doesn't materialize its entities until the
        // player leaves build mode.
        void Warmup();

        // Run teardown: give everything back to SpawnSystem RIGHT NOW rather than in OnDestroy.
        // Destroy() defers OnDestroy to the end of the frame, and a prestige ends one run and starts
        // the next within a single frame — so the old spawner's unregistration would land AFTER the new
        // run had already registered its own, corrupting the fresh run's books. Implementations must
        // leave OnDestroy a no-op afterwards, and must stay safe to call twice.
        void Teardown();
    }
}
