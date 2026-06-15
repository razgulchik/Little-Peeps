using UnityEngine;
using UnityEngine.Tilemaps;

// MonoBehaviour that owns IslandGrid and IslandGenerator; reacts to age events
public class IslandSystem : MonoBehaviour
{
    [SerializeField] private Vector2Int initialSize = new Vector2Int(10, 10);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase grassTile;

    public IslandGrid Grid { get; private set; }
    public IslandGenerator Generator { get; private set; }

    // Generate the island for a new run. RunManager.StartNewRun() owns the timing —
    // IslandSystem no longer auto-builds in Awake so generation isn't duplicated or
    // ordered by chance. Editor preview still goes through the [ContextMenu] below.
    public void GenerateForRun()
    {
        Build();
    }

    private void OnEnable()
    {
        // TODO: EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted)
    }

    private void OnDisable()
    {
        // TODO: EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted)
    }

    // Expand island and regenerate terrain when a new age begins
    private void OnAgeStarted(AgeStartedEvent e)
    {
        // TODO: Generator.Expand(e.Age); RefreshTilemap()
    }

    // Right-click the component in Inspector → Generate Island
    [ContextMenu("Generate Island")]
    private void GenerateIsland()
    {
        Build();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tilemap);
#endif
    }

    private void Build()
    {
        var origin = new Vector2(-initialSize.x * cellSize / 2f, -initialSize.y * cellSize / 2f);
        Grid = new IslandGrid(initialSize, origin, cellSize);
        Generator = new IslandGenerator(Grid);
        Generator.Generate(0);
        RefreshTilemap();
    }

    private void RefreshTilemap()
    {
        if (tilemap == null || grassTile == null) return;

        tilemap.ClearAllTiles();
        for (int x = 0; x < Grid.GridSize.x; x++)
        {
            for (int y = 0; y < Grid.GridSize.y; y++)
            {
                var cell = Grid.GetCell(new Vector2Int(x, y));
                TileBase tile = cell.terrain == TerrainType.Grass ? grassTile : null;
                if (tile == null) continue;

                Vector2 worldPos = Grid.GridToWorld(new Vector2Int(x, y));
                Vector3Int tilemapPos = tilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0f));
                tilemap.SetTile(tilemapPos, tile);
            }
        }
    }
}
