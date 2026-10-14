using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Paint = LittlePeeps.IslandTilePainter.CellPaint;

namespace LittlePeeps.Tests
{
    // The coast painter's two ways of painting must agree. The island rise brings a zone up tile by tile
    // and redraws only the 3×3 around each one; that has to leave exactly what a full repaint of the same
    // land draws — at every step, not just the last, because every step is on screen. Compared as plans
    // (which piece each cell draws), not as tiles, so no tile set asset and no Tilemap are involved and
    // the whole file runs offline.
    public class IslandTilePainterTests
    {
        private static IslandGrid GridOf(IEnumerable<Vector2Int> cells)
        {
            var grid = new IslandGrid(1f);
            foreach (var cell in cells) grid.SetCell(cell, TerrainType.Grass);
            return grid;
        }

        // What RepaintAround does to the tilemaps, done to a plan: redraw the 3×3, clearing what now draws
        // nothing.
        private static void RepaintAround(Dictionary<Vector2Int, Paint> plan, IslandTilePainter.Land land, Vector2Int cell)
        {
            foreach (var coord in IslandTilePainter.Around(cell))
            {
                var paint = IslandTilePainter.PaintOf(land, coord);
                if (paint.DrawsSomething) plan[coord] = paint;
                else plan.Remove(coord);
            }
        }

        private static void AssertSamePlan(Dictionary<Vector2Int, Paint> expected, Dictionary<Vector2Int, Paint> actual, string when)
        {
            var wrong = new List<string>();
            foreach (var coord in expected.Keys.Union(actual.Keys))
            {
                bool inExpected = expected.TryGetValue(coord, out var e);
                bool inActual = actual.TryGetValue(coord, out var a);
                if (inExpected != inActual || !e.Equals(a))
                    wrong.Add($"{coord}: full {(inExpected ? e.ToString() : "nothing")}, step by step {(inActual ? a.ToString() : "nothing")}");
            }
            Assert.That(wrong, Is.Empty, when);
        }

        // An island as the game grows one: a start and two ages, so the old coast has bays and staircases
        // of its own, and the next age's zone. Null when the rules leave no room for that zone.
        private static (List<Vector2Int> old, List<Vector2Int> zone) GrownIsland(int seed)
        {
            var generator = new IslandGenerator(new IslandRules(), seed);
            generator.Commit(generator.GenerateStart());
            for (int age = 0; age < 2; age++)
            {
                var candidates = generator.Propose(1);
                if (candidates.Count == 0) return (null, null);
                generator.Commit(candidates[0]);
            }
            var next = generator.Propose(1);
            if (next.Count == 0) return (null, null);
            return (generator.Land.ToList(), next[0].Cells.ToList());
        }

        [Test]
        public void RevealingAZoneTileByTile_DrawsWhatAFullRepaintDraws_AtEveryStep()
        {
            var groundSeen = new HashSet<IslandGroundPiece>();
            var trimSeen = new HashSet<IslandTrimPiece>();
            int islands = 0;

            for (int seed = 1; seed <= 20; seed++)
            {
                var (old, zone) = GrownIsland(seed);
                if (zone == null) continue;
                islands++;

                var hidden = new HashSet<Vector2Int>(zone);
                var land = new IslandTilePainter.Land(GridOf(old.Concat(zone)), hidden.Contains);
                var drawn = IslandTilePainter.Plan(land);

                new IslandRng(seed).Shuffle(zone);
                foreach (var cell in zone)
                {
                    hidden.Remove(cell);
                    RepaintAround(drawn, land, cell);
                    AssertSamePlan(IslandTilePainter.Plan(land), drawn, $"seed {seed}, revealed {cell}");

                    foreach (var paint in drawn.Values)
                        if (paint.land) groundSeen.Add(paint.ground);
                        else trimSeen.Add(paint.trim);
                }
            }

            Assert.That(islands, Is.GreaterThanOrEqualTo(15), "too few islands grew a zone to test anything");

            // The claim is only as strong as the shapes it met: every piece must have come up somewhere.
            var allGround = Enum.GetValues(typeof(IslandGroundPiece)).Cast<IslandGroundPiece>();
            var allTrim = Enum.GetValues(typeof(IslandTrimPiece)).Cast<IslandTrimPiece>().Where(p => p != IslandTrimPiece.None);
            Assert.That(allGround.Except(groundSeen), Is.Empty, "ground pieces never drawn");
            Assert.That(allTrim.Except(trimSeen), Is.Empty, "outline pieces never drawn");
        }

        [Test]
        public void HidingAZoneTileByTile_AlsoMatchesAFullRepaint()
        {
            // The way back: replaying a rise hides the zone again, and the tuning window scrubs backwards.
            for (int seed = 1; seed <= 10; seed++)
            {
                var (old, zone) = GrownIsland(seed);
                if (zone == null) continue;

                var hidden = new HashSet<Vector2Int>();
                var land = new IslandTilePainter.Land(GridOf(old.Concat(zone)), hidden.Contains);
                var drawn = IslandTilePainter.Plan(land);

                new IslandRng(seed + 100).Shuffle(zone);
                foreach (var cell in zone)
                {
                    hidden.Add(cell);
                    RepaintAround(drawn, land, cell);
                    AssertSamePlan(IslandTilePainter.Plan(land), drawn, $"seed {seed}, hid {cell}");
                }
            }
        }

        [Test]
        public void HiddenLand_IsDrawnExactlyAsIfItWereNotOnTheGrid()
        {
            var (old, zone) = GrownIsland(3);
            Assume.That(zone, Is.Not.Null);

            var hidden = new HashSet<Vector2Int>(zone);
            var withZoneHidden = IslandTilePainter.Plan(new IslandTilePainter.Land(GridOf(old.Concat(zone)), hidden.Contains));
            var withoutZone = IslandTilePainter.Plan(new IslandTilePainter.Land(GridOf(old)));

            AssertSamePlan(withoutZone, withZoneHidden, "zone hidden");
        }

        [Test]
        public void ASquare_DrawsRoundedCorners_AndAnOutlineOnEverySideButTheTop()
        {
            // Pins the pieces themselves, so splitting the painter could not have changed what it draws.
            var square = new List<Vector2Int>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    square.Add(new Vector2Int(x, y));
            var plan = IslandTilePainter.Plan(new IslandTilePainter.Land(GridOf(square)));

            Assert.That(plan[new Vector2Int(-1, 1)].ground, Is.EqualTo(IslandGroundPiece.CornerTopLeft));
            Assert.That(plan[new Vector2Int(1, 1)].ground, Is.EqualTo(IslandGroundPiece.CornerTopRight));
            Assert.That(plan[new Vector2Int(-1, -1)].ground, Is.EqualTo(IslandGroundPiece.CornerBottomLeft));
            Assert.That(plan[new Vector2Int(1, -1)].ground, Is.EqualTo(IslandGroundPiece.CornerBottomRight));
            Assert.That(plan[new Vector2Int(0, 0)].ground, Is.EqualTo(IslandGroundPiece.Plain));

            Assert.That(plan[new Vector2Int(-1, -2)].trim, Is.EqualTo(IslandTrimPiece.BottomEdgeLeft));
            Assert.That(plan[new Vector2Int(0, -2)].trim, Is.EqualTo(IslandTrimPiece.BottomEdgeMid));
            Assert.That(plan[new Vector2Int(1, -2)].trim, Is.EqualTo(IslandTrimPiece.BottomEdgeRight));

            Assert.That(plan[new Vector2Int(-2, 1)].trim, Is.EqualTo(IslandTrimPiece.LeftEdgeTop));
            Assert.That(plan[new Vector2Int(-2, 0)].trim, Is.EqualTo(IslandTrimPiece.LeftEdgeMid));
            Assert.That(plan[new Vector2Int(-2, -2)].trim, Is.EqualTo(IslandTrimPiece.LeftEdgeBottom));
            Assert.That(plan[new Vector2Int(-2, -2)].trimSource, Is.EqualTo(new Vector2Int(-1, -1)));
            Assert.That(plan[new Vector2Int(2, 1)].trim, Is.EqualTo(IslandTrimPiece.RightEdgeTop));
            Assert.That(plan[new Vector2Int(2, -2)].trim, Is.EqualTo(IslandTrimPiece.RightEdgeBottom));

            for (int x = -2; x <= 2; x++)
                Assert.That(plan.ContainsKey(new Vector2Int(x, 2)), Is.False, $"nothing along the top at x = {x}");
            Assert.That(plan.Count, Is.EqualTo(9 + 3 + 2 * 4), "9 land, 3 along the bottom, 4 down each side");
        }

        // The decor is the cell's paint plus its own roll, so the step-by-step tests above already hold for
        // it: a cell whose paint comes back the same comes back with the same decor. These pin the rest.

        [Test]
        public void Decor_MayGoOnEveryLandCell_CornersToo_AndNeverOnTheWater()
        {
            // A 3×3 square: four rounded corners, four sides, a middle — and the outline ring around it.
            var square = new List<Vector2Int>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    square.Add(new Vector2Int(x, y));
            var plan = IslandTilePainter.Plan(new IslandTilePainter.Land(GridOf(square)));

            var decorated = plan.Where(kv => IslandTilePainter.DrawsDecor(kv.Value)).Select(kv => kv.Key);
            Assert.That(decorated, Is.EquivalentTo(square));
        }

        [Test]
        public void DecorSlot_IsTheCellsOwn_AndSpreadsOverEveryTile()
        {
            foreach (int count in new[] { 4, 5 })
            {
                var hits = new int[count];
                for (int x = -20; x < 20; x++)
                    for (int y = -20; y < 20; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        int slot = IslandTilePainter.DecorSlot(cell, 7, count);
                        Assert.That(slot, Is.InRange(0, count - 1));
                        Assert.That(IslandTilePainter.DecorSlot(cell, 7, count), Is.EqualTo(slot), $"{cell} asked again");
                        hits[slot]++;
                    }

                // 1600 cells: an even split, give or take a quarter — five standard deviations either way.
                int even = 1600 / count;
                for (int i = 0; i < count; i++)
                    Assert.That(hits[i], Is.InRange(even * 3 / 4, even * 5 / 4), $"tile {i} of {count}");
            }
        }

        [Test]
        public void DecorSlot_ChangesWithTheSeed()
        {
            int cells = 0, same = 0;
            for (int x = -10; x < 10; x++)
                for (int y = -10; y < 10; y++)
                {
                    var cell = new Vector2Int(x, y);
                    cells++;
                    if (IslandTilePainter.DecorSlot(cell, 1, 5) == IslandTilePainter.DecorSlot(cell, 2, 5)) same++;
                }
            // Chance alone matches one cell in five.
            Assert.That(same, Is.LessThan(cells / 3), "another run draws the same decor");
        }

        [Test]
        public void HasDecor_TakesTheChancesShareOfTheCells_EachTheCellsOwn()
        {
            foreach (float chance in new[] { 0f, 0.3f, 1f })
            {
                int hits = 0;
                for (int x = -20; x < 20; x++)
                    for (int y = -20; y < 20; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        bool has = IslandTilePainter.HasDecor(cell, 7, chance);
                        Assert.That(IslandTilePainter.HasDecor(cell, 7, chance), Is.EqualTo(has), $"{cell} asked again");
                        if (has) hits++;
                    }

                // 1600 cells at 0.3: 480, give or take a sixth — over four standard deviations either way.
                int expected = Mathf.RoundToInt(1600 * chance);
                Assert.That(hits, Is.InRange(expected - expected / 6, expected + expected / 6), $"chance {chance}");
            }
        }

        [Test]
        public void TheCellsThatGetDecor_StillSpreadOverEveryTile()
        {
            // Whether and which are separate rolls: read off one, a low chance would only ever pass the cells
            // whose roll also picks the first tiles.
            const int count = 5;
            var hits = new int[count];
            int decorated = 0;
            for (int x = -30; x < 30; x++)
                for (int y = -30; y < 30; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IslandTilePainter.HasDecor(cell, 7, 0.3f)) continue;
                    hits[IslandTilePainter.DecorSlot(cell, 7, count)]++;
                    decorated++;
                }

            // About 1080 cells: 216 a tile, give or take a third — over five standard deviations either way.
            int even = decorated / count;
            for (int i = 0; i < count; i++)
                Assert.That(hits[i], Is.InRange(even * 2 / 3, even * 4 / 3), $"tile {i} of {count}");
        }
    }
}
