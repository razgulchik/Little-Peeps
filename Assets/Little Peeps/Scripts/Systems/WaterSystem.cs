using UnityEngine;
using UnityEngine.Tilemaps;
using Water2D;

namespace LittlePeeps
{
    // The sea around the island: Modern 2D Water spawned from a prefab at run time, plus the island's
    // outline in the water's obstruction layer so ripples break on the coast instead of running under it.
    //
    // Why a runtime prefab and not a scene object: the package runs in edit mode ([ExecuteAlways]) — a
    // water placed in the scene embeds a fresh Material into SampleScene on every enable and parks hidden
    // manager cameras there, so every scene save would churn against the art branch. Spawned from here,
    // none of that reaches the scene file; the prefab holds the whole look.
    //
    // Coast: ObstructorTilemap copies a tilemap's tiles into a hidden twin on the Obstructors layer — but
    // only once, and it only ever adds. Our island is painted at run time and repainted on every
    // expansion, so the components are added here (not in the scene, same churn reason) and each twin is
    // rebuilt from scratch after every repaint.
    //
    // Wire: waterPrefab → the water prefab; coastTilemaps → Ground and GroundTrim (the trim draws the
    // coastline on water cells, so it is part of the coast the ripples meet).
    public class WaterSystem : MonoBehaviour
    {
        [SerializeField] private ModernWater2D waterPrefab;
        [SerializeField] private Tilemap[] coastTilemaps;

        private ObstructorTilemap[] coast;   // null until Start

        private void OnEnable()  => EventBus<IslandRepaintedEvent>.Subscribe(OnIslandRepainted);
        private void OnDisable() => EventBus<IslandRepaintedEvent>.Unsubscribe(OnIslandRepainted);

        // The water in Awake: its reflection system must exist before any content Starts. The first
        // island's trees and houses are instantiated in GameBootstrap.Awake, and a Reflector reaches for
        // that system in its own Start — without it, a NullReferenceException per reflecting object.
        // Every Awake still runs before any Start, whichever of the two Awakes goes first.
        private void Awake()
        {
            if (waterPrefab == null)
            {
                Debug.LogWarning("WaterSystem: no water prefab assigned — the island stands on the camera's background.", this);
                return;
            }
            var water = Instantiate(waterPrefab);

            // Ripples off outside the island rise, which turns them on for its own duration. The package
            // follows a camera PAN inside its ripple field but never rescales it on a ZOOM, so every zoom
            // step shifts the whole coast inside the field and it rings with waves — and in normal play
            // nothing moves in the water to need them. Off here, not in the prefab: the package only
            // builds the ripple field if it is on when the water enables, and could not turn it on later.
            water.enableSimulation.value = false;
        }

        // The coast in Start: the first island is painted in GameBootstrap.Awake, which is only certain
        // to be over here — the ObstructorTilemaps copy it as they enable.
        private void Start()
        {
            if (waterPrefab == null) return;

            coast = new ObstructorTilemap[coastTilemaps.Length];
            for (int i = 0; i < coastTilemaps.Length; i++)
                if (coastTilemaps[i] != null)
                    coast[i] = coastTilemaps[i].gameObject.AddComponent<ObstructorTilemap>();
        }

        // A repaint before Start (the first island) needs nothing: Start's copies are taken after it.
        private void OnIslandRepainted(IslandRepaintedEvent _)
        {
            if (coast == null) return;

            foreach (var twin in coast)
            {
                if (twin == null) continue;
                if (twin.obstructor != null)
                    twin.obstructor.GetComponent<Tilemap>().ClearAllTiles();   // CreateData only ever adds
                twin.CreateData();
            }
        }
    }
}
