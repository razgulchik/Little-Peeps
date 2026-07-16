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
    // The size comes from the run's StartConfig; the parameterless overload (and the
    // context menu) fall back to the serialized initialSize when no config drives it.
    public void GenerateForRun()
    {
        Build(initialSize);
    }

    public void GenerateForRun(Vector2Int size)
    {
        Build(size);
    }

    // Grow the island for an age: add its expansion blocks and redraw the tilemap. Driven explicitly
    // by AgeSequencer (not an event) so expansion happens exactly once, in order, during the transition.
    public void Expand(AgeDef def)
    {
        if (def == null || Generator == null) return;
        Generator.Expand(def.expansionBlocks);
        RefreshTilemap();
    }

    // Right-click the component in Inspector → Generate Island
    [ContextMenu("Generate Island")]
    private void GenerateIsland()
    {
        Build(initialSize);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tilemap);
#endif
    }

    private void Build(Vector2Int size)
    {
        Grid = new IslandGrid(cellSize, size.x * size.y);
        Generator = new IslandGenerator(Grid, size);
        Generator.Generate(0);
        RefreshTilemap();
    }

    private void RefreshTilemap()
    {
        if (tilemap == null || grassTile == null) return;

        tilemap.ClearAllTiles();
        foreach (var kv in Grid.Cells)
        {
            if (kv.Value.terrain != TerrainType.Grass) continue;

            Vector2 worldPos = Grid.GridToWorld(kv.Key);
            Vector3Int tilemapPos = tilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0f));
            tilemap.SetTile(tilemapPos, grassTile);
        }
    }
}
