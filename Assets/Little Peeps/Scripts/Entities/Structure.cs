using UnityEngine;

namespace LittlePeeps
{
    // A placed structure — anything that stands on the grid. Collision handling + effect dispatch
    // live in the CollisionTarget base; Structure adds the definition it was built from.
    // Spawner structures add a Spawner component; resource structures (Tree/Wheat/Forge/Church) add
    // a ResourceSource component.
    //
    // It is also an effect of its own, which every building has without a line in any prefab: the extra
    // resource a perk makes it pay on every working hit (AdditionalResourcePerHit — "houses pay 0.1 coin a
    // hit"). CollisionTarget picks it up with the other ICollisionEffects, so it runs after the gates like
    // any payer, and a building no modifier names pays nothing and stays the plain wall it was. Only a
    // building StructureSystem has put up is given the ResourceSystem: a construction site is a Structure
    // too, and it pays nothing extra until it stands.
    public class Structure : CollisionTarget, ICollisionEffect
    {
        public StructureDef def;

        [Tooltip("Where the tax leaves from — the extra resource a perk makes this building pay on a hit: its " +
                 "pickup effect and floating number. Empty = this transform. Apart from a ResourceSource's own " +
                 "fx anchor: that one belongs to what the source yields, this one to the tax alone.")]
        [SerializeField] private Transform taxFxAnchor;

        private static readonly ResourceType[] ResourceTypes =
            (ResourceType[])System.Enum.GetValues(typeof(ResourceType));

        private ResourceSystem resourceSystem;

        // Injected by StructureSystem on a building it puts up, like ResourceSource.Initialize.
        public void Initialize(ResourceSystem system) => resourceSystem = system;

        // ICollisionEffect — whether this building pays anything extra at all; only asked when the hit did
        // nothing, to tell a gate's refusal (Blocked) from a wall (Useless).
        public bool Serves(Unit unit)
        {
            for (int i = 0; i < ResourceTypes.Length; i++)
                if (ExtraPerHit(ResourceTypes[i]) > 0f) return true;
            return false;
        }

        // ICollisionEffect — every resource the run's modifiers name for this building, through the
        // production gateway like any harvest. Out of the tax anchor, or the building's root without one —
        // the same default a source without an fx anchor uses.
        public HitOutcome OnHit(Unit unit, CollisionTarget target)
        {
            if (unit == null) return HitOutcome.None;

            var outcome = HitOutcome.None;
            Vector3 origin = taxFxAnchor != null ? taxFxAnchor.position : transform.position;
            for (int i = 0; i < ResourceTypes.Length; i++)
            {
                float amount = ExtraPerHit(ResourceTypes[i]);
                if (amount <= 0f) continue;

                resourceSystem.AddHarvest(ResourceTypes[i], unit.Type, amount, origin);
                outcome = HitOutcome.Worked;
            }
            return outcome;
        }

        // The base is 0: nothing extra until a modifier adds a flat amount. Read at the point of use, so a
        // perk taken mid-run reaches every building already standing.
        private float ExtraPerHit(ResourceType type)
        {
            var stats = resourceSystem != null ? resourceSystem.Stats : null;
            return stats != null ? stats.Apply(0f, StatId.AdditionalResourcePerHit, res: type, structure: def) : 0f;
        }

        // Debug: fell or regrow every source of this structure at once — all the trees of a forest cell, when
        // picked on the forest itself (right-click the component header, Play Mode). One source at a time is
        // ResourceSource's own menu; this only runs it over the lot.
        [ContextMenu("Deplete Sources")]
        private void DebugDepleteSources()
        {
            if (!DebugInPlayMode()) return;
            foreach (var source in GetComponentsInChildren<ResourceSource>()) source.DepleteForDebug();
        }

        [ContextMenu("Regrow Sources")]
        private void DebugRegrowSources()
        {
            if (!DebugInPlayMode()) return;
            foreach (var source in GetComponentsInChildren<ResourceSource>()) source.RegrowForDebug();
        }

        private bool DebugInPlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning($"Structure '{name}': the debug menu works in Play Mode only.", this);
            return false;
        }
    }
}
