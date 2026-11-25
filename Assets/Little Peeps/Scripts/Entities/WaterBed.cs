using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A water cell as a wall that a bridge opens: while one stands over it the wall is off and everything
    // crosses, and when the bridge goes the wall is back (StructureSystem calls both). Only the wall — the
    // water itself stays as it was, drawn under the bridge.
    //
    // The wall comes back with whoever was on the bridge still over the water, so it comes back as a
    // OneWayWall: they walk off, nobody else walks on. A coop's bird over it goes home in a puff, as from a
    // construction site. Animals need nothing: one standing on ground it refuses may always walk off it.
    public class WaterBed : MonoBehaviour
    {
        [Tooltip("The collider units bounce off: off while a bridge stands over this water.")]
        [SerializeField] private Collider2D wall;

        private SpawnSystem spawns;
        private IReadOnlyList<Rect> area;
        private OneWayWall closing;   // the wall coming back; null until a bridge first went

        // Ticks only while the wall is coming back — see Close.
        private void Awake()
        {
            enabled = false;
            if (wall == null) Debug.LogError($"WaterBed on '{name}' has no wall collider assigned.", this);
        }

        // Injected by StructureSystem: `cells` are the water's own, one rect each.
        public void Initialize(SpawnSystem spawnSystem, IReadOnlyList<Rect> cells)
        {
            spawns = spawnSystem;
            area = cells;
        }

        public void Open()
        {
            if (wall == null) return;
            wall.enabled = false;
            enabled = false;
        }

        // Called from a click (Update), so whoever is inside is let through before the next physics step.
        public void Close()
        {
            if (wall == null || wall.enabled) return;

            wall.enabled = true;
            closing ??= new OneWayWall(wall, area, spawns);
            closing.AdoptUnitsInside();
            Goose.SendHomeFrom(area);
            enabled = true;
        }

        private void FixedUpdate()
        {
            closing.LetOut();
            if (closing.IsWhole) enabled = false;
        }
    }
}
