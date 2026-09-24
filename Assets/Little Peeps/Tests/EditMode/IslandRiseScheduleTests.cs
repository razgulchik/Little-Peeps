using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The island rise's clock: when each tile of a new zone starts, breaks the surface and settles, when
    // its content pops, and when the banner is due. Plain C# like the island shape tests — no scene, no
    // profile asset (the timing is plain data for exactly this reason) — so these run offline too.
    //
    // The fixture is an old island west of x = 0 and a zone east of it, joined along the whole of column
    // x = 0: for Join the wave's rings are then simply the zone's columns. Coordinates run negative on
    // purpose, as they do on the real island.
    public class IslandRiseScheduleTests
    {
        private const float Eps = 1e-4f;

        private static HashSet<Vector2Int> Rect(int x, int y, int w, int h)
        {
            var cells = new HashSet<Vector2Int>();
            for (int a = x; a < x + w; a++)
                for (int b = y; b < y + h; b++)
                    cells.Add(new Vector2Int(a, b));
            return cells;
        }

        private static readonly HashSet<Vector2Int> Old = Rect(-5, -2, 5, 5);   // x -5..-1
        private static HashSet<Vector2Int> Zone(int width = 6) => Rect(0, -2, width, 5);

        // Deterministic wave: no jitter, and a crescendo of 1 unless a test is about it.
        private static IslandRiseTiming Timing(IslandRiseOrder order = IslandRiseOrder.Join, float jitter = 0f,
                                               float crescendo = 1f) =>
            new() { order = order, jitter = jitter, crescendo = crescendo };

        private static IslandRiseSchedule Build(IslandRiseTiming timing, HashSet<Vector2Int> zone = null,
                                                List<IslandRiseItem> items = null, int seed = 7) =>
            new(timing, zone ?? Zone(), Old.Contains, items, seed);

        private static float StartAt(IslandRiseSchedule s, int x, int y)
        {
            Assert.That(s.TryGetIndex(new Vector2Int(x, y), out int i), $"({x}, {y}) is not in the zone");
            return s.StartOf(i);
        }

        private static IEnumerable<float> Starts(IslandRiseSchedule s) =>
            Enumerable.Range(0, s.Cells.Count).Select(s.StartOf);

        // ---- the wave ----

        [Test]
        public void Join_TheColumnAgainstTheOldCoastStartsFirst_AndTheWaveMovesOutward()
        {
            var timing = Timing();
            var s = Build(timing);

            for (int y = -2; y <= 2; y++)
                Assert.That(StartAt(s, 0, y), Is.EqualTo(timing.leadIn).Within(Eps), $"join cell (0, {y})");

            for (int x = 1; x < 6; x++)
                for (int y = -2; y <= 2; y++)
                    Assert.That(StartAt(s, x, y), Is.GreaterThan(StartAt(s, x - 1, y)), $"({x}, {y})");
        }

        [TestCase(IslandRiseOrder.JoinNoise)]
        [TestCase(IslandRiseOrder.Join)]
        [TestCase(IslandRiseOrder.Radial)]
        [TestCase(IslandRiseOrder.Sweep)]
        [TestCase(IslandRiseOrder.Random)]
        public void EveryOrder_SpansExactlyFromLeadInToTheEndOfTheCascade(IslandRiseOrder order)
        {
            var timing = Timing(order);
            var s = Build(timing);

            Assert.That(Starts(s).Min(), Is.EqualTo(timing.leadIn).Within(Eps));
            Assert.That(Starts(s).Max(), Is.EqualTo(timing.leadIn + timing.cascade).Within(Eps));
        }

        [Test]
        public void TheCascadeTakesAsLongForASmallZoneAsForABigOne()
        {
            var timing = Timing();
            var small = Build(timing, Zone(3));
            var big = Build(timing, Zone(12));

            Assert.That(Starts(small).Max() - Starts(small).Min(),
                        Is.EqualTo(Starts(big).Max() - Starts(big).Min()).Within(Eps));
        }

        [Test]
        public void Crescendo_TheWaveSpeedsUp()
        {
            var s = Build(Timing(crescendo: 1.8f));

            float previousGap = float.MaxValue;
            for (int x = 1; x < 6; x++)
            {
                float gap = StartAt(s, x, 0) - StartAt(s, x - 1, 0);
                Assert.That(gap, Is.LessThan(previousGap), $"column {x}");
                previousGap = gap;
            }
        }

        [Test]
        public void NoCrescendo_TheWaveKeepsAnEvenPace()
        {
            var s = Build(Timing(crescendo: 1f));

            float first = StartAt(s, 1, 0) - StartAt(s, 0, 0);
            for (int x = 2; x < 6; x++)
                Assert.That(StartAt(s, x, 0) - StartAt(s, x - 1, 0), Is.EqualTo(first).Within(Eps), $"column {x}");
        }

        [Test]
        public void Jitter_ShufflesStartsButNeverLeavesTheCascade()
        {
            var timing = Timing(IslandRiseOrder.Join, jitter: 1f);
            var s = Build(timing);

            foreach (float start in Starts(s))
                Assert.That(start, Is.InRange(timing.leadIn - Eps, timing.leadIn + timing.cascade + Eps));

            // The join column no longer starts in one beat.
            var join = Enumerable.Range(-2, 5).Select(y => StartAt(s, 0, y)).ToList();
            Assert.That(join.Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void JoinNoise_WithNoStrength_IsPlainJoin()
        {
            var noise = Timing(IslandRiseOrder.JoinNoise);
            noise.noiseStrength = 0f;
            var a = Build(noise);
            var b = Build(Timing(IslandRiseOrder.Join));

            Assert.That(Starts(a), Is.EqualTo(Starts(b)).Within(Eps));
        }

        [Test]
        public void TheSameZoneAndSeed_GiveTheSameSchedule_AndAnotherSeedAnother()
        {
            var timing = Timing(IslandRiseOrder.JoinNoise, jitter: 0.4f, crescendo: 1.8f);

            Assert.That(Starts(Build(timing, seed: 3)), Is.EqualTo(Starts(Build(timing, seed: 3))));
            Assert.That(Starts(Build(timing, seed: 3)), Is.Not.EqualTo(Starts(Build(timing, seed: 4))));
        }

        [Test]
        public void AZoneWithNoJoin_GrowsFromOneCellInsteadOfAllAtOnce()
        {
            var timing = Timing();
            var s = new IslandRiseSchedule(timing, Zone(), _ => false, null, 7);

            Assert.That(Starts(s).Count(start => Mathf.Abs(start - timing.leadIn) < Eps), Is.EqualTo(1));
        }

        [Test]
        public void TheZoneItselfIsNeverTakenForOldLand()
        {
            // The caller may pass the island's grid AFTER the zone went into it: every cell then reads as
            // land. Only land outside the zone may count as the old coast.
            var zone = Zone();
            var land = new HashSet<Vector2Int>(Old);
            land.UnionWith(zone);

            var withGrid = new IslandRiseSchedule(Timing(), zone, land.Contains, null, 7);
            Assert.That(Starts(withGrid), Is.EqualTo(Starts(Build(Timing()))).Within(Eps));
        }

        // ---- one tile ----

        [Test]
        public void EachTile_BreaksTheSurface_TouchesDown_AndSettles_OnTheTimingsClock()
        {
            var timing = Timing();
            var s = Build(timing);

            for (int i = 0; i < s.Cells.Count; i++)
            {
                Assert.That(s.BreachOf(i), Is.EqualTo(s.StartOf(i) + timing.depthRise).Within(Eps));
                Assert.That(s.TouchDownOf(i), Is.EqualTo(s.BreachOf(i) + timing.hop).Within(Eps));
                Assert.That(s.SettleOf(i), Is.EqualTo(s.TouchDownOf(i) + timing.squash).Within(Eps));
            }
        }

        [Test]
        public void TileAt_WalksThroughThePhasesInOrder()
        {
            var timing = Timing();
            var s = Build(timing);
            Assert.That(s.TryGetIndex(new Vector2Int(3, 0), out int i));
            float start = s.StartOf(i);

            Assert.That(s.TileAt(i, start - 0.01f).phase, Is.EqualTo(IslandRisePhase.Waiting));

            var under = s.TileAt(i, start + timing.depthRise * 0.5f);
            Assert.That(under.phase, Is.EqualTo(IslandRisePhase.Underwater));
            Assert.That(under.progress, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(under.sinceBreach, Is.LessThan(0f));

            var hop = s.TileAt(i, s.BreachOf(i) + timing.hop * 0.25f);
            Assert.That(hop.phase, Is.EqualTo(IslandRisePhase.Hop));
            Assert.That(hop.progress, Is.EqualTo(0.25f).Within(Eps));
            Assert.That(hop.sinceBreach, Is.EqualTo(timing.hop * 0.25f).Within(Eps));

            Assert.That(s.TileAt(i, s.TouchDownOf(i) + timing.squash * 0.5f).phase, Is.EqualTo(IslandRisePhase.Squash));
            Assert.That(s.TileAt(i, s.SettleOf(i) + 1e-3f).phase, Is.EqualTo(IslandRisePhase.Settled));
        }

        [Test]
        public void NoSquash_ATileSettlesTheMomentItTouchesDown()
        {
            var timing = Timing();
            timing.squash = 0f;
            var s = Build(timing);

            Assert.That(s.TileAt(0, s.TouchDownOf(0) + 1e-3f).phase, Is.EqualTo(IslandRisePhase.Settled));
            Assert.That(s.SettleOf(0), Is.EqualTo(s.TouchDownOf(0)).Within(Eps));
        }

        [Test]
        public void Notes_ClimbInTheOrderTheTilesBreakTheSurface()
        {
            var s = Build(Timing(IslandRiseOrder.JoinNoise, jitter: 0.4f, crescendo: 1.8f));

            var notes = Enumerable.Range(0, s.Cells.Count).Select(s.NoteOf).OrderBy(n => n).ToList();
            Assert.That(notes, Is.EqualTo(Enumerable.Range(0, s.Cells.Count).ToList()), "one note per tile, no gaps");

            for (int rank = 1; rank < s.CellsInOrder.Count; rank++)
            {
                int before = s.CellsInOrder[rank - 1], after = s.CellsInOrder[rank];
                Assert.That(s.BreachOf(after), Is.GreaterThanOrEqualTo(s.BreachOf(before)));
                Assert.That(s.NoteOf(after), Is.EqualTo(s.NoteOf(before) + 1));
            }
        }

        // ---- act 2 ----

        private static IslandRiseItem Item(float duration, params Vector2Int[] cells) => new(cells, duration);

        [Test]
        public void AnItemPopsOnlyOnceAllOfItsLandHasSettled()
        {
            var timing = Timing();
            timing.contentStart = 0f;       // as early as the timing allows: only the land can hold it back
            timing.contentJitter = 0f;
            var farCell = new Vector2Int(5, 0);
            var den = new[] { new Vector2Int(0, 0), new Vector2Int(5, 1) };   // one early cell, one late
            var s = Build(timing, items: new List<IslandRiseItem> { Item(0.35f, farCell), Item(0.35f, den) });

            s.TryGetIndex(farCell, out int far);
            s.TryGetIndex(den[1], out int late);
            Assert.That(s.ItemStartOf(0), Is.GreaterThanOrEqualTo(s.SettleOf(far) + timing.contentGap - Eps));
            Assert.That(s.ItemStartOf(1), Is.GreaterThanOrEqualTo(s.SettleOf(late) + timing.contentGap - Eps));
        }

        [Test]
        public void NoItemPopsBeforeActTwo()
        {
            var timing = Timing();
            timing.contentJitter = 0f;
            var items = Zone().Select(c => Item(0.35f, c)).ToList();
            var s = Build(timing, items: items);

            float act2 = timing.leadIn + timing.cascade * timing.contentStart;
            for (int j = 0; j < s.ItemCount; j++)
                Assert.That(s.ItemStartOf(j), Is.GreaterThanOrEqualTo(act2 - Eps));
        }

        [Test]
        public void ItemsInOrder_IsTheOrderTheyStartIn()
        {
            var items = Zone().Select(c => Item(0.35f, c)).ToList();
            var s = Build(Timing(IslandRiseOrder.JoinNoise, jitter: 0.4f), items: items);

            for (int rank = 1; rank < s.ItemsInOrder.Count; rank++)
                Assert.That(s.ItemStartOf(s.ItemsInOrder[rank]), Is.GreaterThanOrEqualTo(s.ItemStartOf(s.ItemsInOrder[rank - 1])));
        }

        [Test]
        public void ItemProgress_RunsFromZeroToOneOverThePop()
        {
            var s = Build(Timing(), items: new List<IslandRiseItem> { Item(0.4f, new Vector2Int(2, 0)) });
            float start = s.ItemStartOf(0);

            Assert.That(s.ItemStartedAt(0, start - 0.01f), Is.False);
            Assert.That(s.ItemStartedAt(0, start), Is.True);
            Assert.That(s.ItemProgressAt(0, start + 0.1f), Is.EqualTo(0.25f).Within(Eps));
            Assert.That(s.ItemProgressAt(0, start + 1f), Is.EqualTo(1f));
        }

        // ---- the end ----

        [Test]
        public void TheFinaleWaitsForTheLastTileAndTheLastPop()
        {
            var timing = Timing();
            var items = new List<IslandRiseItem> { Item(2f, new Vector2Int(5, 0)) };   // a slow mountain at the far edge
            var s = Build(timing, items: items);

            float lastSettle = Enumerable.Range(0, s.Cells.Count).Max(s.SettleOf);
            float lastPop = s.ItemStartOf(0) + s.ItemDurationOf(0);
            Assert.That(s.Finale, Is.EqualTo(Mathf.Max(lastSettle, lastPop) + timing.finaleDelay).Within(Eps));

            // The banner: after the finale's own length, or once the breath has crossed the zone (5 steps
            // from the join to its far side), whichever comes later.
            float breathOver = 5f / timing.breathSpeed + timing.breathTime;
            Assert.That(s.Duration, Is.EqualTo(s.Finale + Mathf.Max(timing.finaleLength, breathOver)).Within(Eps));

            var bare = Build(timing);
            Assert.That(bare.Finale, Is.EqualTo(lastSettle + timing.finaleDelay).Within(Eps));
        }

        [Test]
        public void TheBreath_RunsOutFromTheJoin_AndTheBannerWaitsForIt()
        {
            var timing = Timing();
            timing.finaleLength = 0f;   // only the breath holds the banner back
            var s = Build(timing);

            for (int y = -2; y <= 2; y++)
                Assert.That(s.BreathStartOf(Index(s, 0, y)), Is.EqualTo(s.Finale).Within(Eps), $"join cell (0, {y})");
            for (int x = 1; x < 6; x++)
                Assert.That(s.BreathStartOf(Index(s, x, 0)), Is.GreaterThan(s.BreathStartOf(Index(s, x - 1, 0))), $"column {x}");

            float lastBreathEnd = Enumerable.Range(0, s.Cells.Count).Max(s.BreathStartOf) + timing.breathTime;
            Assert.That(s.Duration, Is.EqualTo(lastBreathEnd).Within(Eps));
        }

        [Test]
        public void BreathAt_IsTrueOnlyWhileTheCellBreathes()
        {
            var timing = Timing();
            var s = Build(timing);
            int cell = Index(s, 3, 0);
            float start = s.BreathStartOf(cell);

            Assert.That(s.BreathAt(cell, start - 0.01f, out _), Is.False);
            Assert.That(s.BreathAt(cell, start + timing.breathTime * 0.5f, out float progress), Is.True);
            Assert.That(progress, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(s.BreathAt(cell, start + timing.breathTime + 0.01f, out _), Is.False);
        }

        [Test]
        public void TheRiver_RunsInFromTheEndThatLandsFirst_OneStepAtATime()
        {
            var timing = Timing();
            timing.contentJitter = 0f;

            // A river along y = 0, its path listed from the FAR end (x = 5) to the join (x = 0). The join end
            // lands first, so that is where it must start.
            var items = new List<IslandRiseItem>();
            for (int x = 5; x >= 0; x--)
                items.Add(new IslandRiseItem(new[] { new Vector2Int(x, 0) }, 0.2f, flowIndex: 5 - x));
            var s = Build(timing, items: items);

            int ItemAt(int x) => 5 - x;
            for (int x = 1; x <= 5; x++)
            {
                Assert.That(s.ItemStartOf(ItemAt(x)), Is.GreaterThanOrEqualTo(s.ItemStartOf(ItemAt(x - 1)) + timing.riverStep - Eps),
                            $"x = {x} follows x = {x - 1}");
                Assert.That(s.ItemStartOf(ItemAt(x)), Is.GreaterThanOrEqualTo(s.SettleOf(Index(s, x, 0)) + timing.contentGap - Eps),
                            $"x = {x} waits for its land");
            }
        }

        [Test]
        public void TheRiver_SpacesItsCellsEvenWhenTheirLandIsReadyAtOnce()
        {
            var timing = Timing();
            timing.contentJitter = 0f;
            timing.contentSpread = 0f;   // left to themselves, every pop would start together

            // Along the join column: every cell of it starts, and lands, at the same moment.
            var items = new List<IslandRiseItem>();
            for (int y = -2; y <= 2; y++)
                items.Add(new IslandRiseItem(new[] { new Vector2Int(0, y) }, 0.2f, flowIndex: y + 2));
            var s = Build(timing, items: items);

            for (int j = 1; j < items.Count; j++)
                Assert.That(s.ItemStartOf(j) - s.ItemStartOf(j - 1), Is.EqualTo(timing.riverStep).Within(Eps), $"cell {j}");
        }

        private static int Index(IslandRiseSchedule s, int x, int y)
        {
            Assert.That(s.TryGetIndex(new Vector2Int(x, y), out int i), $"({x}, {y}) is not in the zone");
            return i;
        }

        [Test]
        public void AnEmptyZone_HasNothingToPlay()
        {
            var s = new IslandRiseSchedule(Timing(), new HashSet<Vector2Int>(), Old.Contains, null, 7);

            Assert.That(s.Cells, Is.Empty);
            Assert.That(s.ItemCount, Is.EqualTo(0));
            Assert.That(s.Duration, Is.EqualTo(0f));
        }
    }
}
