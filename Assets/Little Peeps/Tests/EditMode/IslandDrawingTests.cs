using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps.Tests
{
    // What IslandDrawing does to the scene's tilemaps beyond painting the land: how it sorts them, the coast's
    // waves — where they go, their mirrored right side, their frame changes — and the island's shadow on the water.
    // Needs real Tilemap components and tiles, so it runs in Unity's Test Runner only.
    public class IslandDrawingTests
    {
        private GameObject root;
        private readonly List<Object> made = new();   // assets a test made, destroyed after it
        private CoastWaveTiles waveTiles;

        [SetUp]
        public void SetUp()
        {
            root = null;
            made.Clear();
            waveTiles = null;
        }

        [TearDown]
        public void TearDown()
        {
            waveTiles?.Dispose();
            if (root != null) Object.DestroyImmediate(root);
            foreach (var asset in made)
                if (asset != null) Object.DestroyImmediate(asset);
        }

        private TilemapRenderer NewTilemap(string name, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        [Test]
        public void BorderLayers_AreSortedByTheCascade_OverTheGroundDecorAndTrim()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 0);
            var trim = NewTilemap("TrimHorizontal", "Default", 1);
            var decor = NewTilemap("Decor", "Default", 4);
            // Typed in upside down, and the meadow on another sorting layer altogether.
            var meadow = NewTilemap("BordersMeadow", "Water", 0);
            var forest = NewTilemap("BordersForest", "Default", 9);

            var borders = new List<IslandBorderLayer>
            {
                new(TerrainType.Grass, null, meadow.GetComponent<Tilemap>()),
                new(TerrainType.Forest, null, forest.GetComponent<Tilemap>()),
            };
            new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(), trim.GetComponent<Tilemap>(),
                              decor.GetComponent<Tilemap>(), 0, borders);

            Assert.That(forest.sortingLayerID, Is.EqualTo(ground.sortingLayerID));
            Assert.That(meadow.sortingLayerID, Is.EqualTo(ground.sortingLayerID));
            Assert.That(forest.sortingOrder, Is.EqualTo(5), "just over the highest land layer below it (decor, 4)");
            Assert.That(meadow.sortingOrder, Is.EqualTo(6), "listed above the forest, so drawn over it");
        }

        [Test]
        public void ARowWithoutATilemap_KeepsItsPlace_ButMovesNothing()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 0);
            var forest = NewTilemap("BordersForest", "Default", 7);

            var borders = new List<IslandBorderLayer>
            {
                new(TerrainType.Grass, null, null),
                new(TerrainType.Forest, null, forest.GetComponent<Tilemap>()),
            };
            new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(), null, null, 0, borders);

            Assert.That(forest.sortingOrder, Is.EqualTo(1));
            Assert.That(ground.sortingOrder, Is.EqualTo(0), "the ground itself is never re-sorted");
        }

        // ---- the coast's waves ----

        // Two frames of waves, every slot its own sprite (so a test can tell which piece and frame a cell shows),
        // one second each.
        private CoastWaveTiles NewWaveTiles()
        {
            var texture = new Texture2D(16, 16);
            made.Add(texture);
            Sprite NewSprite()
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
                made.Add(sprite);
                return sprite;
            }

            var set = ScriptableObject.CreateInstance<CoastWaveSet>();
            made.Add(set);
            set.frames = new CoastWaveSet.Frame[2];
            for (int i = 0; i < set.frames.Length; i++)
                set.frames[i] = new CoastWaveSet.Frame
                {
                    leftEdgeTop = NewSprite(), leftEdgeMid = NewSprite(), leftEdgeBottom = NewSprite(),
                    bottomEdgeLeft = NewSprite(), bottomEdgeMid = NewSprite(),
                    cornerBay = NewSprite(), cornerStep = NewSprite(), cornerNotch = NewSprite(),
                };
            set.steps = new[] { new CoastWaveSet.Step(0, 1f), new CoastWaveSet.Step(1, 1f) };
            return waveTiles = CoastWaveTiles.Create(set);
        }

        // A 3×3 square of land with the trim and waves on their own layers; its tile set has no art, so only
        // the waves draw anything on the water.
        private (IslandDrawing drawing, IslandTilePainter.Land land, Tilemap horizontal, Tilemap vertical) WavySquare()
        {
            var cells = new List<Vector2Int>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    cells.Add(new Vector2Int(x, y));
            return Wavy(cells);
        }

        // A strip along y = 1 from x = 0 to 4 with a two-cell wall down from each end: water at (1, 0) is a bay's
        // left corner (land north and west), at (3, 0) its right one, and (2, 0) the band between them.
        private (IslandDrawing drawing, IslandTilePainter.Land land, Tilemap horizontal, Tilemap vertical) WavyBay()
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x <= 4; x++) cells.Add(new Vector2Int(x, 1));
            foreach (int x in new[] { 0, 4 })
                for (int y = -1; y <= 0; y++)
                    cells.Add(new Vector2Int(x, y));
            return Wavy(cells);
        }

        private (IslandDrawing drawing, IslandTilePainter.Land land, Tilemap horizontal, Tilemap vertical) Wavy(List<Vector2Int> cells)
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var grid = new IslandGrid(1f);
            foreach (var cell in cells) grid.SetCell(cell, TerrainType.Grass);

            var tiles = ScriptableObject.CreateInstance<IslandTileSet>();
            made.Add(tiles);
            var horizontal = NewTilemap("WaveHorizontal", "Default", 0).GetComponent<Tilemap>();
            var vertical = NewTilemap("WaveVertical", "Default", 0).GetComponent<Tilemap>();
            var drawing = new IslandDrawing(grid, _ => tiles, NewTilemap("Ground", "Default", 0).GetComponent<Tilemap>(),
                                            NewTilemap("TrimHorizontal", "Default", 0).GetComponent<Tilemap>(), null, 0, null,
                                            NewTilemap("TrimVertical", "Default", 1).GetComponent<Tilemap>(),
                                            horizontal, vertical, NewWaveTiles());
            drawing.RepaintAll();
            return (drawing, new IslandTilePainter.Land(grid), horizontal, vertical);
        }

        private static readonly Matrix4x4 Mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        // Every water cell shows, on each layer, its outline piece's wave in `frame` — a corner's cell its corner,
        // on the vertical layer alone — and nothing else does.
        private void AssertWavesFollowTheOutline(IslandTilePainter.Land land, Tilemap horizontal, Tilemap vertical, int frame)
        {
            var plan = IslandTilePainter.Plan(land);
            for (int x = -3; x <= 7; x++)
                for (int y = -4; y <= 3; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var at = new Vector3Int(x, y, 0);
                    TileBase wantHorizontal = null, wantVertical = null;
                    if (plan.TryGetValue(cell, out var paint) && !paint.land)
                    {
                        var corner = waveTiles.Corner(frame, paint.horizontal, paint.vertical);
                        wantHorizontal = corner != null ? null : waveTiles.Get(frame, paint.horizontal);
                        wantVertical = corner != null ? corner : waveTiles.Get(frame, paint.vertical);
                    }
                    Assert.That(horizontal.GetTile(at), Is.EqualTo(wantHorizontal), $"horizontal at {cell}");
                    Assert.That(vertical.GetTile(at), Is.EqualTo(wantVertical), $"vertical at {cell}");
                }
        }

        [Test]
        public void Waves_LieUnderTheWholeIsland_InTheTrimsMaterial()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 3);
            var trimHorizontal = NewTilemap("TrimHorizontal", "Default", 3);
            var trimVertical = NewTilemap("TrimVertical", "Default", 9);   // made trimHorizontal + 1 by the drawing
            // Typed in anyhow, one of them on another sorting layer altogether.
            var waveHorizontal = NewTilemap("WaveHorizontal", "Water", 20);
            var waveVertical = NewTilemap("WaveVertical", "Default", 0);
            var bending = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Bending" };   // only its identity matters
            made.Add(bending);
            trimHorizontal.sharedMaterial = bending;

            new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(), trimHorizontal.GetComponent<Tilemap>(),
                              null, 0, null, trimVertical.GetComponent<Tilemap>(), waveHorizontal.GetComponent<Tilemap>(),
                              waveVertical.GetComponent<Tilemap>(), NewWaveTiles());

            Assert.That(waveVertical.sortingOrder, Is.EqualTo(2), "one under the island's lowest layer (3)");
            Assert.That(waveHorizontal.sortingOrder, Is.EqualTo(1), "under the vertical waves");
            foreach (var wave in new[] { waveHorizontal, waveVertical })
            {
                Assert.That(wave.sortingLayerID, Is.EqualTo(trimHorizontal.sortingLayerID), wave.name);
                Assert.That(wave.sharedMaterial, Is.SameAs(bending), $"{wave.name} bends with the land as the trim does");
            }
        }

        [Test]
        public void Waves_GoWhereTheOutlineGoes_AndMirrorTheRightEdge()
        {
            var (_, land, horizontal, vertical) = WavySquare();

            AssertWavesFollowTheOutline(land, horizontal, vertical, 0);
            Assert.That(horizontal.HasTile(new Vector3Int(0, 2, 0)) || vertical.HasTile(new Vector3Int(0, 2, 0)), Is.False,
                        "no waves along the north coast");
            Assert.That(vertical.GetTransformMatrix(new Vector3Int(-2, 0, 0)), Is.EqualTo(Matrix4x4.identity), "left side as drawn");
            Assert.That(vertical.GetTransformMatrix(new Vector3Int(2, 0, 0)), Is.EqualTo(Mirror), "right side mirrored");
            Assert.That(horizontal.GetTransformMatrix(new Vector3Int(1, -2, 0)), Is.EqualTo(Mirror), "the band's right end mirrored");
            Assert.That(((Tile)vertical.GetTile(new Vector3Int(2, 0, 0))).sprite,
                        Is.SameAs(((Tile)vertical.GetTile(new Vector3Int(-2, 0, 0))).sprite), "the right side is the left's art");
        }

        [Test]
        public void AFrameChange_SwapsEveryWave_AndKeepsTheMirror()
        {
            var (drawing, land, horizontal, vertical) = WavySquare();

            Assert.That(drawing.ShowWavesAt(0.5), Is.False, "still the frame it was painted in");
            Assert.That(drawing.ShowWavesAt(1.5), Is.True);
            AssertWavesFollowTheOutline(land, horizontal, vertical, 1);
            Assert.That(vertical.GetTransformMatrix(new Vector3Int(2, 0, 0)), Is.EqualTo(Mirror), "still mirrored after the swap");
            Assert.That(drawing.ShowWavesAt(1.9), Is.False, "the same frame again changes nothing");

            // A repaint while the second frame shows paints in it.
            drawing.RepaintAll();
            AssertWavesFollowTheOutline(land, horizontal, vertical, 1);
        }

        [Test]
        public void ABaysCorners_TakeOneCornerWaveEach_TheLeftOneMirrored_AndSwapWithTheFrames()
        {
            var (drawing, land, horizontal, vertical) = WavyBay();
            var leftCorner = new Vector3Int(1, 0, 0);    // wall to the west
            var rightCorner = new Vector3Int(3, 0, 0);   // wall to the east, as the art is drawn

            Assert.That(vertical.GetTile(rightCorner),
                        Is.EqualTo(waveTiles.Corner(0, IslandTrimPiece.BottomEdgeMid, IslandTrimPiece.LeftEdgeMid)));
            Assert.That(vertical.GetTile(rightCorner), Is.Not.Null, "the bay's corner has its own wave");
            Assert.That(horizontal.HasTile(rightCorner), Is.False, "and no band wave under it");
            Assert.That(vertical.GetTransformMatrix(rightCorner), Is.EqualTo(Matrix4x4.identity));
            Assert.That(vertical.GetTransformMatrix(leftCorner), Is.EqualTo(Mirror));
            Assert.That(((Tile)vertical.GetTile(leftCorner)).sprite, Is.SameAs(((Tile)vertical.GetTile(rightCorner)).sprite));
            AssertWavesFollowTheOutline(land, horizontal, vertical, 0);

            drawing.ShowWavesAt(1.5);
            AssertWavesFollowTheOutline(land, horizontal, vertical, 1);
            Assert.That(vertical.GetTransformMatrix(leftCorner), Is.EqualTo(Mirror), "still mirrored after the swap");
        }

        // An island with a shadow and nothing under the island but it; the land's tiles do not matter to it.
        private (IslandDrawing drawing, IslandGrid grid, Tilemap shadow, TileBase tile) Shaded(List<Vector2Int> cells)
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var grid = new IslandGrid(1f);
            foreach (var cell in cells) grid.SetCell(cell, TerrainType.Grass);

            var tiles = ScriptableObject.CreateInstance<IslandTileSet>();
            made.Add(tiles);
            var tile = ScriptableObject.CreateInstance<Tile>();
            made.Add(tile);
            var shadow = NewTilemap("CoastShadow", "Default", 0).GetComponent<Tilemap>();
            var drawing = new IslandDrawing(grid, _ => tiles, NewTilemap("Ground", "Default", 0).GetComponent<Tilemap>(),
                                            NewTilemap("TrimHorizontal", "Default", 0).GetComponent<Tilemap>(), null, 0, null,
                                            NewTilemap("TrimVertical", "Default", 1).GetComponent<Tilemap>(),
                                            coastShadowTilemap: shadow, coastShadowTile: tile);
            drawing.RepaintAll();
            return (drawing, grid, shadow, tile);
        }

        // The strip and walls of WavyBay.
        private static List<Vector2Int> BayCells()
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x <= 4; x++) cells.Add(new Vector2Int(x, 1));
            foreach (int x in new[] { 0, 4 })
                for (int y = -1; y <= 0; y++)
                    cells.Add(new Vector2Int(x, y));
            return cells;
        }

        // Every water cell under land has the shadow's tile, and no other cell has anything.
        private static void AssertShadowFollowsTheBands(IslandTilePainter.Land land, Tilemap shadow, TileBase tile, string when)
        {
            for (int x = -3; x <= 7; x++)
                for (int y = -5; y <= 4; y++)
                {
                    var cell = new Vector2Int(x, y);
                    bool underLand = !land.Contains(cell) && land.Contains(cell + Vector2Int.up);
                    Assert.That(shadow.GetTile(new Vector3Int(x, y, 0)), Is.EqualTo(underLand ? tile : null), $"at {cell} {when}");
                }
        }

        [Test]
        public void TheShadow_LiesUnderTheWaves_InTheTrimsMaterial()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 3);
            var trimHorizontal = NewTilemap("TrimHorizontal", "Default", 3);
            var waveHorizontal = NewTilemap("WaveHorizontal", "Default", 0);
            var waveVertical = NewTilemap("WaveVertical", "Default", 0);
            // Typed in anyhow, on another sorting layer altogether.
            var shadow = NewTilemap("CoastShadow", "Water", 20);
            var bending = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Bending" };   // only its identity matters
            made.Add(bending);
            trimHorizontal.sharedMaterial = bending;
            var tile = ScriptableObject.CreateInstance<Tile>();
            made.Add(tile);

            var drawing = new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(),
                                            trimHorizontal.GetComponent<Tilemap>(), waveHorizontalTilemap: waveHorizontal.GetComponent<Tilemap>(),
                                            waveVerticalTilemap: waveVertical.GetComponent<Tilemap>(), waveTiles: NewWaveTiles(),
                                            coastShadowTilemap: shadow.GetComponent<Tilemap>(), coastShadowTile: tile);

            Assert.That(drawing.CoastShadowTilemap, Is.SameAs(shadow.GetComponent<Tilemap>()));
            Assert.That(shadow.sortingOrder, Is.LessThan(waveHorizontal.sortingOrder), "under the waves");
            Assert.That(shadow.sortingLayerID, Is.EqualTo(trimHorizontal.sortingLayerID));
            Assert.That(shadow.sharedMaterial, Is.SameAs(bending), "its edge bends with the land as the trim does");
        }

        [Test]
        public void TheProjections_LieOverTheShadow_UnderTheWaves_AndNotBent()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 3);
            var trimHorizontal = NewTilemap("TrimHorizontal", "Default", 3);
            var waveHorizontal = NewTilemap("WaveHorizontal", "Default", 0);
            var waveVertical = NewTilemap("WaveVertical", "Default", 0);
            var shadow = NewTilemap("CoastShadow", "Default", 0);
            var tile = ScriptableObject.CreateInstance<Tile>();
            made.Add(tile);

            var drawing = new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(),
                                            trimHorizontal.GetComponent<Tilemap>(), waveHorizontalTilemap: waveHorizontal.GetComponent<Tilemap>(),
                                            waveVerticalTilemap: waveVertical.GetComponent<Tilemap>(), waveTiles: NewWaveTiles(),
                                            coastShadowTilemap: shadow.GetComponent<Tilemap>(), coastShadowTile: tile);
            var look = drawing.ProjectionLook(null, null);

            Assert.That(look.lightOrder, Is.LessThan(waveHorizontal.sortingOrder), "the foam breaks over them");
            Assert.That(look.darkOrder, Is.LessThan(look.lightOrder), "the light block over the dark one");
            Assert.That(shadow.sortingOrder, Is.LessThan(look.darkOrder), "and both over the island's shadow");
            Assert.That(look.sortingLayerId, Is.EqualTo(trimHorizontal.sortingLayerID));
            Assert.That(drawing.LowestUnderIslandOrder, Is.EqualTo(shadow.sortingOrder), "what a tile under water passes under");
            Assert.That(IslandBend.Bends(look.material), Is.False, "they ride with their structure");
        }

        [Test]
        public void TheShadow_WithoutWaves_StillLiesUnderTheIsland()
        {
            var (_, _, shadow, _) = Shaded(BayCells());

            Assert.That(shadow.GetComponent<TilemapRenderer>().sortingOrder, Is.LessThan(0));
        }

        [Test]
        public void WithoutATile_ThereIsNoShadow_AndItsTilemapIsLeftAlone()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var grid = new IslandGrid(1f);
            foreach (var cell in BayCells()) grid.SetCell(cell, TerrainType.Grass);
            var tiles = ScriptableObject.CreateInstance<IslandTileSet>();
            made.Add(tiles);
            var shadow = NewTilemap("CoastShadow", "Default", 7);

            var drawing = new IslandDrawing(grid, _ => tiles, NewTilemap("Ground", "Default", 0).GetComponent<Tilemap>(),
                                            NewTilemap("TrimHorizontal", "Default", 0).GetComponent<Tilemap>(),
                                            coastShadowTilemap: shadow.GetComponent<Tilemap>());
            drawing.RepaintAll();

            Assert.That(drawing.CoastShadowTilemap, Is.Null);
            Assert.That(shadow.sortingOrder, Is.EqualTo(7));
            Assert.That(shadow.GetComponent<Tilemap>().GetUsedTilesCount(), Is.Zero);
        }

        [Test]
        public void TheShadow_FallsUnderEveryBand_AndNowhereElse()
        {
            var (_, grid, shadow, tile) = Shaded(BayCells());

            AssertShadowFollowsTheBands(new IslandTilePainter.Land(grid), shadow, tile, "on the whole island");
            Assert.That(shadow.GetTile(new Vector3Int(2, 0, 0)), Is.EqualTo(tile), "under the strip, in the bay");
            Assert.That(shadow.GetTile(new Vector3Int(0, -2, 0)), Is.EqualTo(tile), "under a wall's foot");
            Assert.That(shadow.HasTile(new Vector3Int(2, -1, 0)), Is.False, "the lower cell is the hanging sprite's, no tile of its own");
            Assert.That(shadow.HasTile(new Vector3Int(-1, 0, 0)), Is.False, "beside the wall: a column, not a band");
            Assert.That(shadow.HasTile(new Vector3Int(2, 2, 0)), Is.False, "north of the land");
        }

        [Test]
        public void TheShadow_FollowsTheCoast_AsAZoneComesUpTileByTile()
        {
            var cells = BayCells();
            // The bay's floor comes up under the strip, row by row, as a zone would.
            var zone = new List<Vector2Int>();
            foreach (int y in new[] { 0, -1 })
                for (int x = 1; x <= 3; x++)
                    zone.Add(new Vector2Int(x, y));
            cells.AddRange(zone);
            var (drawing, grid, shadow, tile) = Shaded(cells);
            var shown = new IslandTilePainter.Land(grid, drawing.IsHidden);

            drawing.HideLand(zone);
            AssertShadowFollowsTheBands(shown, shadow, tile, "with the zone held back");

            foreach (var cell in zone)
            {
                drawing.RevealLand(cell);
                AssertShadowFollowsTheBands(shown, shadow, tile, $"after {cell} came up");
            }
            Assert.That(shadow.HasTile(new Vector3Int(2, 0, 0)), Is.False, "the land covers the old shadow's cell");
            Assert.That(shadow.GetTile(new Vector3Int(2, -2, 0)), Is.EqualTo(tile), "and the shadow fell under the new coast");
        }

        [Test]
        public void AZonesCell_KeepsItsFinalPieceWhileItRises_AndIsOrdinaryLandOnceItIsUp()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var grid = new IslandGrid(1f);
            var strip = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
            foreach (var cell in strip) grid.SetCell(cell, TerrainType.Grass);

            // Cell (1, 0) falls on the checkerboard's dark squares.
            var tiles = ScriptableObject.CreateInstance<IslandTileSet>();
            made.Add(tiles);
            var plain = ScriptableObject.CreateInstance<Tile>();
            var corner = ScriptableObject.CreateInstance<Tile>();
            made.Add(plain);
            made.Add(corner);
            tiles.groundDark = plain;
            tiles.cornerTopRightDark = corner;

            var ground = NewTilemap("Ground", "Default", 0).GetComponent<Tilemap>();
            var drawing = new IslandDrawing(grid, _ => tiles, ground, null);
            var middle = new Vector3Int(1, 0, 0);

            drawing.HideLand(new[] { strip[1], strip[2] });
            drawing.RevealLand(strip[1]);
            Assert.That(ground.GetTile(middle), Is.EqualTo(plain), "plain, with its eastern neighbour still to land");
            Assert.That(drawing.FinalGroundTileOf(strip[1]), Is.EqualTo(plain), "as its stand-in was");

            drawing.RevealAllLand();
            drawing.HideLand(new[] { strip[2] });
            Assert.That(ground.GetTile(middle), Is.EqualTo(corner), "old land by the next zone: a corner until that lands");
        }
    }
}
