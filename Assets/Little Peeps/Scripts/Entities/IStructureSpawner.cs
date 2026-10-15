namespace LittlePeeps
{
    // Contract between structure-mounted spawners (Spawner for units, AnimalSpawner for animals)
    // and SpawnSystem. Each spawner registers itself on its first Warmup and unregisters in Teardown /
    // OnDestroy; SpawnSystem then reaches every live one through this interface without knowing the
    // concrete spawner kind. Internals are deliberately NOT shared: the unit launch->return->rest slot
    // cycle and the animal die->cooldown->replace cycle have nothing in common beyond this contract.
    public interface IStructureSpawner
    {
        // Placement: register (once), then create this spawner's entities — a resting unit per slot, the
        // den's animals let out. Once per structure: nothing empties a spawner mid-run any more, so there
        // is no second warmup to come.
        void Warmup();

        // The run's stat sheet changed mid-run (age, perk): re-resolve whatever this spawner
        // MATERIALISED from it at Warmup — a house's slot count — and grow to match. Reached from
        // RunStats.Changed through SpawnSystem, on every live spawner. A spawner that materialises
        // nothing from the sheet is a no-op here.
        void RefreshFromStats();

        // Give everything back to SpawnSystem RIGHT NOW rather than in OnDestroy. Two callers, one reason:
        // Destroy() defers OnDestroy to the end of the frame. A run teardown ends one run and starts the
        // next within a single frame, so the old spawner's unregistration would land AFTER the new run had
        // already registered its own, corrupting the fresh run's books; and a sale (StructureSystem's live
        // removal) needs the capacity gone before it trims the surplus residents. Implementations must
        // leave OnDestroy a no-op afterwards, and must stay safe to call twice.
        void Teardown();
    }
}
