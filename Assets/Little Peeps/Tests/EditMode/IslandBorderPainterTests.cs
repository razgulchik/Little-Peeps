using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The soft borders between terrains. Compared as plans (which piece each cell draws on which terrain's
    // layer), never as tiles, so no border art and no Tilemap are involved and the whole file runs offline.
    public class IslandBorderPainterTests
    {
        private const TerrainType Meadow = TerrainType.Grass;
        private const TerrainType Forest = TerrainType.Forest;
        private const TerrainType Desert = TerrainType.Desert;

        // The game's order: meadow over forest over desert. Desert is left out on purpose — a terrain the
        // list does not name lies under all of them.
        private static readonly IslandBorderPainter.Cascade Game = new(new[] { Meadow, Forest });

        // A map drawn top row first, as it looks on screen: M = meadow, F = forest, D = desert, '.' = sea.
        // The bottom row is y = 0, the left column x = 0.
        private static IslandTilePainter.Land LandOf(params string[] rows)
        {
            var grid = new IslandGrid(1f);
            for (int r = 0; r < rows.Length; r++)
            {
                int y = rows.Length - 1 - r;
                for (int x = 0; x < rows[r].Length; x++)
                {
                    switch (rows[r][x])
                    {
                        case 'M': grid.SetCell(new Vector2Int(x, y), Meadow); break;
                        case 'F': grid.SetCell(new Vector2Int(x, y), Forest); break;
                        case 'D': grid.SetCell(new Vector2Int(x, y), Desert); break;
                    }
                }
            }
            return new IslandTilePainter.Land(grid);
        }

        private static IslandBorderPiece At(IslandTilePainter.Land land, int x, int y, TerrainType layer) =>
            IslandBorderPainter.PieceOf(land, new Vector2Int(x, y), layer, Game);

        [Test]
        public void AForestSquareInTheDesert_SpillsOverEverySide_AndEdgesItself()
        {
            var land = LandOf(
                "DDDDD",
                "DFFFD",
                "DFFFD",
                "DFFFD",
                "DDDDD");

            // The fringe on the desert, facing the forest.
            Assert.That(At(land, 2, 4, Forest), Is.EqualTo(IslandBorderPiece.OverSouth));
            Assert.That(At(land, 2, 0, Forest), Is.EqualTo(IslandBorderPiece.OverNorth));
            Assert.That(At(land, 0, 2, Forest), Is.EqualTo(IslandBorderPiece.OverEast));
            Assert.That(At(land, 4, 2, Forest), Is.EqualTo(IslandBorderPiece.OverWest));

            // The forest's own edge, facing the desert; corners take the two-sided piece.
            Assert.That(At(land, 2, 3, Forest), Is.EqualTo(IslandBorderPiece.EdgeNorth));
            Assert.That(At(land, 2, 1, Forest), Is.EqualTo(IslandBorderPiece.EdgeSouth));
            Assert.That(At(land, 1, 2, Forest), Is.EqualTo(IslandBorderPiece.EdgeWest));
            Assert.That(At(land, 3, 2, Forest), Is.EqualTo(IslandBorderPiece.EdgeEast));
            Assert.That(At(land, 1, 3, Forest), Is.EqualTo(IslandBorderPiece.EdgeNorthWest));
            Assert.That(At(land, 3, 3, Forest), Is.EqualTo(IslandBorderPiece.EdgeNorthEast));
            Assert.That(At(land, 1, 1, Forest), Is.EqualTo(IslandBorderPiece.EdgeSouthWest));
            Assert.That(At(land, 3, 1, Forest), Is.EqualTo(IslandBorderPiece.EdgeSouthEast));

            // Diagonal-only neighbours and the forest's middle draw nothing.
            Assert.That(At(land, 0, 4, Forest), Is.EqualTo(IslandBorderPiece.None));
            Assert.That(At(land, 4, 0, Forest), Is.EqualTo(IslandBorderPiece.None));
            Assert.That(At(land, 2, 2, Forest), Is.EqualTo(IslandBorderPiece.None));

            Assert.That(IslandBorderPainter.Plan(land, Forest, Game).Count, Is.EqualTo(3 * 4 + 8),
                        "three fringe pieces along each side, eight edge pieces round the forest");
            Assert.That(IslandBorderPainter.Plan(land, Meadow, Game), Is.Empty, "no meadow, no meadow border");
            Assert.That(IslandBorderPainter.Plan(land, Desert, Game), Is.Empty, "the bottom terrain covers nothing");
        }

        [Test]
        public void ACellInANotch_TakesTheCornerFringe()
        {
            var land = LandOf(
                "FFD",
                "FFD",
                "FFF");

            Assert.That(At(land, 2, 1, Forest), Is.EqualTo(IslandBorderPiece.OverSouthWest));
            Assert.That(At(land, 2, 2, Forest), Is.EqualTo(IslandBorderPiece.OverWest));
        }

        [Test]
        public void WhereThreeTerrainsMeet_ACellCarriesAPieceOnEachLayer()
        {
            var land = LandOf(
                "MMFF",
                "MMDD",
                "DDDD");

            // Desert with the forest above and the meadow to the left: both fringes.
            Assert.That(At(land, 2, 1, Forest), Is.EqualTo(IslandBorderPiece.OverNorth));
            Assert.That(At(land, 2, 1, Meadow), Is.EqualTo(IslandBorderPiece.OverWest));

            // Forest with the meadow to the left and the desert below: the meadow's fringe over it, its own
            // edge toward the desert.
            Assert.That(At(land, 2, 2, Meadow), Is.EqualTo(IslandBorderPiece.OverWest));
            Assert.That(At(land, 2, 2, Forest), Is.EqualTo(IslandBorderPiece.EdgeSouth));

            // The meadow edges toward both terrains under it, and the forest never spills onto it.
            Assert.That(At(land, 1, 2, Meadow), Is.EqualTo(IslandBorderPiece.EdgeEast));
            Assert.That(At(land, 1, 1, Meadow), Is.EqualTo(IslandBorderPiece.EdgeSouthEast));
            Assert.That(At(land, 1, 2, Forest), Is.EqualTo(IslandBorderPiece.None));
        }

        [Test]
        public void ATerrainMissingFromTheOrder_LiesUnderEveryListedOne()
        {
            var land = LandOf(
                "MMFF",
                "MMFF");

            // Listed above the forest, the meadow is the one that spills — even with no art of its own yet —
            // and the forest draws nothing toward it.
            Assert.That(IslandBorderPainter.Plan(land, Forest, Game), Is.Empty);
            Assert.That(At(land, 2, 0, Meadow), Is.EqualTo(IslandBorderPiece.OverWest));
            Assert.That(At(land, 1, 0, Meadow), Is.EqualTo(IslandBorderPiece.EdgeEast));

            // Left out of the order, it counts as lower, and the forest spills onto it instead.
            var forestOnly = new IslandBorderPainter.Cascade(new[] { Forest });
            Assert.That(IslandBorderPainter.PieceOf(land, new Vector2Int(1, 0), Forest, forestOnly),
                        Is.EqualTo(IslandBorderPiece.OverEast));
            Assert.That(IslandBorderPainter.PieceOf(land, new Vector2Int(2, 0), Forest, forestOnly),
                        Is.EqualTo(IslandBorderPiece.EdgeWest));
        }

        [Test]
        public void AnAllRoundEdge_FacesTheTerrainAboveAndTheSea_ButTheFringeStaysOffTheMeadow()
        {
            var land = LandOf(
                "FFF.",
                "FFF.",
                "MMM.");

            IslandBorderPiece AllRound(int x, int y) =>
                IslandBorderPainter.PieceOf(land, new Vector2Int(x, y), Forest, Game, edgeAllRound: true);

            Assert.That(AllRound(0, 2), Is.EqualTo(IslandBorderPiece.EdgeNorthWest), "sea above and to the left");
            Assert.That(AllRound(1, 2), Is.EqualTo(IslandBorderPiece.EdgeNorth));
            Assert.That(AllRound(2, 2), Is.EqualTo(IslandBorderPiece.EdgeNorthEast));
            Assert.That(AllRound(0, 1), Is.EqualTo(IslandBorderPiece.EdgeSouthWest), "meadow below, sea to the left");
            Assert.That(AllRound(1, 1), Is.EqualTo(IslandBorderPiece.EdgeSouth));
            Assert.That(AllRound(2, 1), Is.EqualTo(IslandBorderPiece.EdgeSouthEast));
            Assert.That(AllRound(1, 0), Is.EqualTo(IslandBorderPiece.None), "the meadow lies over the forest");

            // Without it, the forest edges only toward what it covers, and here it covers nothing.
            Assert.That(IslandBorderPainter.Plan(land, Forest, Game), Is.Empty);
        }

        [Test]
        public void AFringeOverTheSea_ReachesOutOverTheWater_ButNeverOntoTheTerrainAbove()
        {
            var land = LandOf(
                "FF.",
                "FF.",
                "MM.");

            IslandBorderPiece OverSea(int x, int y) =>
                IslandBorderPainter.PieceOf(land, new Vector2Int(x, y), Forest, Game, overSea: true);

            Assert.That(OverSea(2, 2), Is.EqualTo(IslandBorderPiece.OverWest), "out over the sea");
            Assert.That(OverSea(2, 1), Is.EqualTo(IslandBorderPiece.OverWest));
            Assert.That(OverSea(0, 3), Is.EqualTo(IslandBorderPiece.OverSouth));
            Assert.That(OverSea(-1, 1), Is.EqualTo(IslandBorderPiece.OverEast));
            Assert.That(OverSea(0, 0), Is.EqualTo(IslandBorderPiece.None), "the meadow lies over the forest: its border, not the forest's");
            Assert.That(OverSea(2, 0), Is.EqualTo(IslandBorderPiece.None), "the forest is only diagonal to it");
            Assert.That(OverSea(-1, 3), Is.EqualTo(IslandBorderPiece.None), "nor to this corner of the sea");

            // The plan finds the sea's cells by itself: 2 above, 2 to the left, 2 to the right.
            Assert.That(IslandBorderPainter.Plan(land, Forest, Game, overSea: true).Count, Is.EqualTo(6));

            // Without it, nothing: the forest covers neither the meadow nor the sea.
            Assert.That(IslandBorderPainter.Plan(land, Forest, Game), Is.Empty);
        }

        [Test]
        public void TheSeaIsNeitherCoveringNorCovered()
        {
            var land = LandOf(
                "FF.",
                "FF.");

            Assert.That(IslandBorderPainter.Plan(land, Forest, Game), Is.Empty);
            Assert.That(At(land, 2, 0, Forest), Is.EqualTo(IslandBorderPiece.None), "nothing is drawn on the water");
        }

        // ---- the rise ----

        // An island as the game grows one — a start and two ages — each section on its own terrain, and the
        // next zone on yet another, so its join meets borders of both directions. Null when the rules leave
        // no room for that zone.
        private static (IslandGrid grid, List<Vector2Int> zone) MixedIsland(int seed)
        {
            var terrains = new[] { Desert, Forest, Meadow };
            var grid = new IslandGrid(1f);
            var generator = new IslandGenerator(new IslandRules(), seed);
            var start = generator.Commit(generator.GenerateStart());
            foreach (var cell in start.Cells) grid.SetCell(cell, terrains[seed % 3]);

            for (int age = 1; age <= 2; age++)
            {
                var candidates = generator.Propose(1);
                if (candidates.Count == 0) return (null, null);
                var section = generator.Commit(candidates[0]);
                foreach (var cell in section.Cells) grid.SetCell(cell, terrains[(seed + age) % 3]);
            }

            var next = generator.Propose(1);
            if (next.Count == 0) return (null, null);
            var zone = next[0].Cells.ToList();
            foreach (var cell in zone) grid.SetCell(cell, terrains[(seed + 1) % 3]);
            return (grid, zone);
        }

        // Both ways of drawing a border: the meadow only toward what it covers; the forest as the game's
        // forest set draws it — edged all round, its fringe out over the sea too.
        private static readonly (TerrainType terrain, bool wide)[] Layers = { (Meadow, false), (Forest, true) };

        private static Dictionary<(TerrainType, Vector2Int), IslandBorderPiece> FullPlan(IslandTilePainter.Land land)
        {
            var plan = new Dictionary<(TerrainType, Vector2Int), IslandBorderPiece>();
            foreach (var (terrain, wide) in Layers)
                foreach (var kv in IslandBorderPainter.Plan(land, terrain, Game, edgeAllRound: wide, overSea: wide))
                    plan[(terrain, kv.Key)] = kv.Value;
            return plan;
        }

        // What RepaintAround does to the border tilemaps, done to a plan.
        private static void RepaintAround(Dictionary<(TerrainType, Vector2Int), IslandBorderPiece> plan,
                                          IslandTilePainter.Land land, Vector2Int cell)
        {
            foreach (var (terrain, wide) in Layers)
                foreach (var coord in IslandTilePainter.Around(cell))
                {
                    var piece = IslandBorderPainter.PieceOf(land, coord, terrain, Game, edgeAllRound: wide, overSea: wide);
                    if (piece != IslandBorderPiece.None) plan[(terrain, coord)] = piece;
                    else plan.Remove((terrain, coord));
                }
        }

        private static void AssertSamePlan(Dictionary<(TerrainType, Vector2Int), IslandBorderPiece> expected,
                                           Dictionary<(TerrainType, Vector2Int), IslandBorderPiece> actual, string when)
        {
            var wrong = new List<string>();
            foreach (var key in expected.Keys.Union(actual.Keys))
            {
                bool inExpected = expected.TryGetValue(key, out var e);
                bool inActual = actual.TryGetValue(key, out var a);
                if (inExpected != inActual || e != a)
                    wrong.Add($"{key.Item1} layer at {key.Item2}: full {(inExpected ? e.ToString() : "nothing")}, " +
                              $"step by step {(inActual ? a.ToString() : "nothing")}");
            }
            Assert.That(wrong, Is.Empty, when);
        }

        [Test]
        public void RevealingAZoneTileByTile_DrawsWhatAFullRepaintDraws_AtEveryStep()
        {
            var seen = new HashSet<IslandBorderPiece>();
            int islands = 0;

            for (int seed = 1; seed <= 30; seed++)
            {
                var (grid, zone) = MixedIsland(seed);
                if (zone == null) continue;
                islands++;

                var hidden = new HashSet<Vector2Int>(zone);
                var land = new IslandTilePainter.Land(grid, hidden.Contains);
                var drawn = FullPlan(land);

                new IslandRng(seed).Shuffle(zone);
                foreach (var cell in zone)
                {
                    hidden.Remove(cell);
                    RepaintAround(drawn, land, cell);
                    AssertSamePlan(FullPlan(land), drawn, $"seed {seed}, revealed {cell}");
                }
                seen.UnionWith(drawn.Values);
            }

            Assert.That(islands, Is.GreaterThanOrEqualTo(20), "too few islands grew a zone to test anything");

            // Only as strong as the shapes it met: every piece must have come up somewhere.
            var all = Enum.GetValues(typeof(IslandBorderPiece)).Cast<IslandBorderPiece>().Where(p => p != IslandBorderPiece.None);
            Assert.That(all.Except(seen), Is.Empty, "border pieces never drawn");
        }

        [Test]
        public void HidingAZoneTileByTile_AlsoMatchesAFullRepaint()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var (grid, zone) = MixedIsland(seed);
                if (zone == null) continue;

                var hidden = new HashSet<Vector2Int>();
                var land = new IslandTilePainter.Land(grid, hidden.Contains);
                var drawn = FullPlan(land);

                new IslandRng(seed + 100).Shuffle(zone);
                foreach (var cell in zone)
                {
                    hidden.Add(cell);
                    RepaintAround(drawn, land, cell);
                    AssertSamePlan(FullPlan(land), drawn, $"seed {seed}, hid {cell}");
                }
            }
        }

        [Test]
        public void HiddenLand_HasNoBorder_AndGivesNoneToItsNeighbours()
        {
            var (grid, zone) = MixedIsland(3);
            Assume.That(zone, Is.Not.Null);

            var hidden = new HashSet<Vector2Int>(zone);
            var withZoneHidden = FullPlan(new IslandTilePainter.Land(grid, hidden.Contains));

            var withoutZone = new IslandGrid(1f);
            foreach (var kv in grid.Cells)
                if (!hidden.Contains(kv.Key)) withoutZone.SetCell(kv.Key, kv.Value.terrain);

            AssertSamePlan(FullPlan(new IslandTilePainter.Land(withoutZone)), withZoneHidden, "zone hidden");
        }

        [Test]
        public void TheGeneratorNeverSqueezesACellBetweenAnythingElseOnOppositeSides()
        {
            // What the 16 pieces rely on (see IslandBorderPainter.PieceOf): with it, no cell ever needs art
            // for two opposite sides at once — not even an all-round edge, for which the sea counts too, nor
            // a fringe on the sea's cells.
            for (int seed = 1; seed <= 30; seed++)
            {
                var (grid, zone) = MixedIsland(seed);
                if (zone == null) continue;
                var land = new IslandTilePainter.Land(grid);
                int shore = 0;

                foreach (var coord in land.Cells())
                {
                    var own = land.TerrainOf(coord);
                    bool Else(int dx, int dy)
                    {
                        var c = new Vector2Int(coord.x + dx, coord.y + dy);
                        return !land.Contains(c) || land.TerrainOf(c) != own;
                    }
                    Assert.That(Else(0, 1) && Else(0, -1), Is.False, $"seed {seed}: {coord} ({own}) ends above and below");
                    Assert.That(Else(-1, 0) && Else(1, 0), Is.False, $"seed {seed}: {coord} ({own}) ends left and right");

                    foreach (var sea in IslandTilePainter.Around(coord))
                    {
                        if (land.Contains(sea)) continue;
                        shore++;
                        bool Land(int dx, int dy) => land.Contains(new Vector2Int(sea.x + dx, sea.y + dy));
                        Assert.That(Land(0, 1) && Land(0, -1), Is.False, $"seed {seed}: sea at {sea} has land above and below");
                        Assert.That(Land(-1, 0) && Land(1, 0), Is.False, $"seed {seed}: sea at {sea} has land left and right");
                    }
                }
                Assert.That(shore, Is.GreaterThan(0));
            }
        }
    }
}
