using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Lays the footprint of the structure being edited over Prefab Mode, so art can be lined up against its
    // cells without entering play. Purely a drawing in the Scene view: nothing is added to the prefab and
    // nothing reaches a build. Switched per machine from the Little Peeps menu.
    //
    // Everything is drawn from the prefab's ROOT, because the root is the one point the game places: it lands on
    // the bottom centre of the footprint's box (IslandGrid.OriginToWorldAnchor), whatever the root's own position
    // in the prefab says. Art follows from there as Visual + Model offsets + the sprite's pivot, and the ghost, a
    // site's blueprint and the finished building all read it the same way. So the cells below come from that
    // same rule, and moving the root in the prefab changes nothing in the game: move Model or Visual.
    //
    // For every selected sprite it adds the sprite's canvas (its whole rectangle, transparent margins included)
    // and the line it sorts on. The canvas turns green when it lies exactly on the footprint's cells. That is a
    // hint for art exported to whole cells, not a rule: a watermill's wheel hangs below its cells on purpose, and
    // art cropped to its drawing (a bush, a house) has no such place at all. The pixel lattice is for those.
    //
    // A def points at its prefab, not the other way round, so the prefab's def is looked up among the assets when
    // the stage opens. A prefab no def uses gets no grid, and neither does an edge structure (a fence): it stands
    // on the line between two cells and has no footprint.
    [InitializeOnLoad]
    public static class PrefabFootprintGrid
    {
        private const string MenuPath = "Little Peeps/Footprint Grid In Prefabs";
        private const string PrefKey = "LittlePeeps.FootprintGridInPrefabs";

        // The size of a cell lives on the scene's IslandSystem, out of reach from a prefab. This mirrors it, as
        // SceneGridGizmo does: if the island's cell size ever changes, change this too.
        private const float CellSize = 1f;

        private const float PixelLatticeMarginCells = 2f;   // art overhangs its box: a pier is wider than it stands
        private const float MinPixelOnScreen = 4f;          // below this the lattice is a grey wash, not a ruler
        private const float BoxLineWidth = 2f;
        private const float CanvasLineWidth = 2f;
        private const float MarkerRadiusCells = 0.05f;
        private const float SortLineOverhangCells = 0.125f; // past the canvas on each side, to find it under art

        private static readonly Color CellFill = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color CellLine = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color BoxLine = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color HoleFill = new Color(1f, 0.25f, 0.2f, 0.25f);
        private static readonly Color PixelLine = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color RootColor = new Color(1f, 0.6f, 0.1f, 1f);
        private static readonly Color CanvasColor = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color CanvasOnCellsColor = new Color(0.3f, 1f, 0.4f, 1f);
        private static readonly Color SortColor = new Color(1f, 0.35f, 0.9f, 1f);

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Vector3[] Outline = new Vector3[5];

        private static string lookedUpPath;
        private static StructureDef def;
        private static GUIStyle labelStyle;

        static PrefabFootprintGrid()
        {
            SceneView.duringSceneGui += OnSceneGui;
            // Looked up afresh on every opening, so a def pointed at this prefab a moment ago is seen by reopening.
            PrefabStage.prefabStageOpened += _ => lookedUpPath = null;
        }

        private static bool IsOn => EditorPrefs.GetBool(PrefKey, true);

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !IsOn);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, IsOn);
            return true;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || !IsOn) return;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null) return;

            if (lookedUpPath != stage.assetPath)
            {
                lookedUpPath = stage.assetPath;
                def = FindDef(stage.assetPath);
            }
            if (def == null || def.placement != PlacementKind.Cell) return;

            var root = stage.prefabContentsRoot.transform;
            var footprint = def.Footprint;

            DrawPixelLattice(root, footprint.Size);
            DrawCells(root, footprint);
            DrawRoot(root, footprint.Size);
            DrawSelectedSprites(stage, root, footprint.Size);
        }

        private static StructureDef FindDef(string prefabPath)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(StructureDef)))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<StructureDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.prefab != null &&
                    AssetDatabase.GetAssetPath(candidate.prefab) == prefabPath)
                    return candidate;
            }
            return null;
        }

        // Plain arithmetic, everything relative to the root. Apart from the drawing because nothing here needs
        // the editor to be running.
        internal static class Geometry
        {
            private static readonly IslandGrid Grid = new IslandGrid(CellSize);

            // Cell (x, y) of the box, asking the game's own rules where the root and the cell go.
            internal static Rect CellRect(Vector2Int size, int x, int y)
            {
                Vector2 centre = Grid.GridToWorld(new Vector2Int(x, y));
                Vector2 anchor = Grid.OriginToWorldAnchor(Vector2Int.zero, size);
                Vector2 half = Vector2.one * (CellSize / 2f);
                return new Rect(centre - anchor - half, Vector2.one * CellSize);
            }

            internal static Rect BoxRect(Vector2Int size)
            {
                return new Rect(CellRect(size, 0, 0).position, (Vector2)size * CellSize);
            }

            // The canvas around a sprite's pivot, in the renderer's local space. A flip mirrors it across the pivot.
            internal static Rect CanvasRect(Vector2 canvasSize, Vector2 normalizedPivot, bool flipX, bool flipY)
            {
                float x = flipX ? 1f - normalizedPivot.x : normalizedPivot.x;
                float y = flipY ? 1f - normalizedPivot.y : normalizedPivot.y;
                return new Rect(-x * canvasSize.x, -y * canvasSize.y, canvasSize.x, canvasSize.y);
            }

            // Every edge of the canvas on a cell line of the box, and none of them outside it.
            internal static bool LiesOnCells(Rect canvas, Vector2Int size)
            {
                const float tolerance = 0.001f;   // of a cell: float noise, far under the 1/16 of one pixel of art

                Rect box = BoxRect(size);
                if (canvas.xMin < box.xMin - tolerance || canvas.xMax > box.xMax + tolerance ||
                    canvas.yMin < box.yMin - tolerance || canvas.yMax > box.yMax + tolerance)
                    return false;

                return OnLine(canvas.xMin - box.xMin) && OnLine(canvas.xMax - box.xMin) &&
                       OnLine(canvas.yMin - box.yMin) && OnLine(canvas.yMax - box.yMin);

                bool OnLine(float distance)
                {
                    float cells = distance / CellSize;
                    return Mathf.Abs(cells - Mathf.Round(cells)) < tolerance;
                }
            }
        }

        // --- Drawing ------------------------------------------------------------------------------------------

        private static void DrawPixelLattice(Transform root, Vector2Int size)
        {
            float pixelsPerUnit = PixelsPerUnit(root);
            if (pixelsPerUnit <= 0f) return;

            float pixel = 1f / pixelsPerUnit;
            Vector2 onScreen = HandleUtility.WorldToGUIPoint(root.position) -
                               HandleUtility.WorldToGUIPoint(root.position + Vector3.right * pixel);
            if (onScreen.magnitude < MinPixelOnScreen) return;

            Rect box = Geometry.BoxRect(size);
            float margin = PixelLatticeMarginCells * CellSize;
            var area = new Rect(box.x - margin, box.y - margin, box.width + 2f * margin, box.height + 2f * margin);

            int columns = Mathf.RoundToInt(area.width / pixel);
            int rows = Mathf.RoundToInt(area.height / pixel);
            var segments = new Vector3[(columns + rows + 2) * 2];
            int n = 0;
            for (int i = 0; i <= columns; i++)
            {
                float x = area.xMin + i * pixel;
                segments[n++] = At(root, x, area.yMin);
                segments[n++] = At(root, x, area.yMax);
            }
            for (int i = 0; i <= rows; i++)
            {
                float y = area.yMin + i * pixel;
                segments[n++] = At(root, area.xMin, y);
                segments[n++] = At(root, area.xMax, y);
            }

            Handles.color = PixelLine;
            Handles.DrawLines(segments);
        }

        // Game art shares one PPU; any sprite of the prefab tells it.
        private static float PixelsPerUnit(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite != null) return renderer.sprite.pixelsPerUnit;
            return 0f;
        }

        private static void DrawCells(Transform root, Footprint footprint)
        {
            Vector2Int size = footprint.Size;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                {
                    SetCorners(root, Geometry.CellRect(size, x, y));
                    Handles.DrawSolidRectangleWithOutline(Corners, footprint.Contains(x, y) ? CellFill : HoleFill, CellLine);
                }

            SetCorners(root, Geometry.BoxRect(size));
            Handles.color = BoxLine;
            DrawClosed(BoxLineWidth);
        }

        private static void DrawRoot(Transform root, Vector2Int size)
        {
            Handles.color = RootColor;
            Handles.DrawWireDisc(root.position, Vector3.forward, MarkerRadiusCells * CellSize);

            Rect box = Geometry.BoxRect(size);
            Label(At(root, box.xMin, box.yMin), $"{def.name} {size.x}x{size.y}", BoxLine);
        }

        // A sprite selected directly is drawn even while switched off (one state of a stand, a side of a mill);
        // through a parent only the ones showing, so selecting the root does not pile up every variant.
        private static void DrawSelectedSprites(PrefabStage stage, Transform root, Vector2Int size)
        {
            foreach (var selected in Selection.gameObjects)
            {
                if (selected.scene != stage.scene) continue;

                foreach (var renderer in selected.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null) continue;
                    if (renderer.gameObject != selected && !renderer.gameObject.activeInHierarchy) continue;
                    DrawSprite(renderer, root, size);
                }
            }
        }

        private static void DrawSprite(SpriteRenderer renderer, Transform root, Vector2Int size)
        {
            var sprite = renderer.sprite;
            Vector2 canvasSize = renderer.drawMode == SpriteDrawMode.Simple
                ? sprite.rect.size / sprite.pixelsPerUnit
                : renderer.size;
            Vector2 pivot = sprite.pivot / sprite.rect.size;
            Rect local = Geometry.CanvasRect(canvasSize, pivot, renderer.flipX, renderer.flipY);

            var t = renderer.transform;
            Corners[0] = t.TransformPoint(local.xMin, local.yMin, 0f);
            Corners[1] = t.TransformPoint(local.xMax, local.yMin, 0f);
            Corners[2] = t.TransformPoint(local.xMax, local.yMax, 0f);
            Corners[3] = t.TransformPoint(local.xMin, local.yMax, 0f);

            // Read back relative to the root as a plain box: offsets, scale and any turn already applied.
            Rect canvas = Bounds(Corners, root.position);
            bool onCells = Geometry.LiesOnCells(canvas, size);

            Color colour = onCells ? CanvasOnCellsColor : CanvasColor;
            Handles.color = colour;
            DrawClosed(CanvasLineWidth);
            Label(Corners[3], $"{renderer.name} {sprite.rect.width:0}x{sprite.rect.height:0}", colour);

            // Sorting with Pivot goes by the renderer's own position, with Center by the middle of the canvas.
            bool byPivot = renderer.spriteSortPoint == SpriteSortPoint.Pivot;
            float sortY = byPivot ? t.position.y : root.position.y + canvas.center.y;
            float overhang = SortLineOverhangCells * CellSize;
            var from = new Vector3(root.position.x + canvas.xMin - overhang, sortY, t.position.z);
            var to = new Vector3(root.position.x + canvas.xMax + overhang, sortY, t.position.z);

            Handles.color = SortColor;
            Handles.DrawAAPolyLine(CanvasLineWidth, from, to);
            Handles.DrawWireDisc(t.position, Vector3.forward, MarkerRadiusCells * CellSize);
            Label(to, byPivot ? "sort" : "sort (center)", SortColor);
        }

        private static Vector3 At(Transform root, float x, float y)
        {
            return root.position + new Vector3(x, y, 0f);
        }

        private static void SetCorners(Transform root, Rect rect)
        {
            Corners[0] = At(root, rect.xMin, rect.yMin);
            Corners[1] = At(root, rect.xMax, rect.yMin);
            Corners[2] = At(root, rect.xMax, rect.yMax);
            Corners[3] = At(root, rect.xMin, rect.yMax);
        }

        private static Rect Bounds(Vector3[] corners, Vector3 origin)
        {
            Vector2 min = corners[0] - origin, max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 p = corners[i] - origin;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void DrawClosed(float width)
        {
            for (int i = 0; i < Corners.Length; i++) Outline[i] = Corners[i];
            Outline[Corners.Length] = Corners[0];
            Handles.DrawAAPolyLine(width, Outline);
        }

        private static void Label(Vector3 position, string text, Color colour)
        {
            labelStyle ??= new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = colour;
            Handles.Label(position, text, labelStyle);
        }
    }
}
