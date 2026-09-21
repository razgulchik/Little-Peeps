using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The island shape generator, pinned the way the prototype pinned it (Prototypes/IslandShapeLab/
    // test_island_shapes.py): geometry fixtures for each rule, reproducibility from a seed, and a seed
    // sweep that re-checks every invariant on every step. All of it is plain C# — no scene, no assets —
    // so these run under the offline harness as well as the Test Runner.
    //
    // Seeds are NOT the prototype's: a different PRNG means different islands from the same number.
    // What carries over is every property; what is pinned here is the C# port's own determinism.
    public class IslandShapeTests
    {
        private static HashSet<Vector2Int> Rect(int x, int y, int w, int h)
        {
            var cells = new HashSet<Vector2Int>();
            for (int a = x; a < x + w; a++)
                for (int b = y; b < y + h; b++)
                    cells.Add(new Vector2Int(a, b));
            return cells;
        }

        private static HashSet<Vector2Int> Minus(HashSet<Vector2Int> a, IEnumerable<Vector2Int> b)
        {
            var result = new HashSet<Vector2Int>(a);
            result.ExceptWith(b);
            return result;
        }

        private static HashSet<Vector2Int> Union(HashSet<Vector2Int> a, IEnumerable<Vector2Int> b)
        {
            var result = new HashSet<Vector2Int>(a);
            result.UnionWith(b);
            return result;
        }

        private static HashSet<Vector2Int> Rotated(IEnumerable<Vector2Int> cells, int dx = 0, int dy = 0)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var c in cells) result.Add(new Vector2Int(-c.y + dx, c.x + dy));
            return result;
        }

        private static HashSet<Vector2Int> Set(IReadOnlyCollection<Vector2Int> cells) => new(cells);

        private static IslandRules Rules(int startArea = 36, int sectionArea = 48, int attempts = 2000) =>
            new() { startArea = startArea, sectionArea = sectionArea, attempts = attempts };

        // Scripted candidate masses, handed out in order. Stands in for IslandShape.MakeMass so a test
        // can drive the selection logic with shapes it chose.
        private static MassSource Scripted(params HashSet<Vector2Int>[] masses)
        {
            int next = 0;
            return (rng, area, width) =>
            {
                if (next >= masses.Length) throw new InvalidOperationException("scripted masses exhausted");
                return new HashSet<Vector2Int>(masses[next++]);
            };
        }

        private static IslandSection Expand(IslandGenerator g)
        {
            var proposals = g.Propose(1);
            Assert.IsNotEmpty(proposals, $"seed {g.Seed}: no expansion found");
            return g.Commit(proposals[0]);
        }

        // --- rng ------------------------------------------------------------------------------------

        [Test]
        public void Rng_IsDeterministicAndStaysInRange()
        {
            var a = new IslandRng(12345);
            var b = new IslandRng(12345);
            for (int i = 0; i < 1000; i++)
            {
                int va = a.Range(-3, 5), vb = b.Range(-3, 5);
                Assert.AreEqual(va, vb, "same seed, same sequence");
                Assert.That(va, Is.InRange(-3, 5));
                double d = a.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                b.NextDouble();
            }
            Assert.AreNotEqual(new IslandRng(1).NextUInt(), new IslandRng(2).NextUInt(), "different seeds diverge");

            var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            new IslandRng(9).Shuffle(list);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, list, "shuffle is a permutation");
        }

        // --- selection -------------------------------------------------------------------------------

        [Test]
        public void DuplicatesAndRotations_DoNotIncreaseSelectionWeight()
        {
            var exact = Rect(0, 0, 6, 6);
            var other = Minus(Rect(0, 0, 7, 6), new[] { new Vector2Int(0, 0), new Vector2Int(6, 0), new Vector2Int(0, 5), new Vector2Int(6, 5) });
            var rotated = Rotated(other, 20, -30);
            var pickedAreas = new HashSet<int>();

            for (int seed = 0; seed < 20; seed++)
            {
                var plain = new IslandGenerator(Rules(attempts: 2), seed, Scripted(exact, other));
                plain.Commit(plain.GenerateStart());

                var repeatedMasses = new List<HashSet<Vector2Int>>();
                for (int i = 0; i < 8; i++) repeatedMasses.Add(exact);
                repeatedMasses.Add(rotated);
                repeatedMasses.Add(other);
                var repeated = new IslandGenerator(Rules(attempts: 10), seed, Scripted(repeatedMasses.ToArray()));
                repeated.Commit(repeated.GenerateStart());

                Assert.IsTrue(Set(plain.Land).SetEquals(repeated.Land), $"seed {seed}: proposal frequency changed the pick");
                pickedAreas.Add(plain.Land.Count);
            }
            CollectionAssert.AreEquivalent(new[] { 36, 38 }, pickedAreas, "both families get picked over 20 seeds");
        }

        [Test]
        public void ShapeIdentity_IgnoresTranslationAndRotation()
        {
            var land = Minus(Rect(0, 0, 7, 6), new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) });
            Assert.AreEqual(IslandShape.Silhouette(land), IslandShape.Silhouette(Rotated(land, 10, -17)));
            Assert.AreEqual(4, IslandShape.Orientations(land).Count);
        }

        [Test]
        public void DeeperAsymmetricCornerCuts_CanPassWidthChecks()
        {
            // The envelope MakeMass picks for area 36 at uniform 1.0: 6 × 7. Cuts deeper than an equal
            // split of the margin still yield a valid start-sized mass.
            var mass = IslandShape.Chamfer(6, 7, new[] { 2, 0, 1, 0 });
            Assert.AreEqual(38, mass.Count);
            Assert.IsFalse(IslandShape.NarrowLand(mass, 3));
            Assert.IsTrue(IslandShape.Connected(mass));
            Assert.IsEmpty(IslandShape.Holes(mass));
        }

        [Test]
        public void SeedSample_HasShapeAndOrientationVariety()
        {
            var families = new Dictionary<SilhouetteKey, int>();
            int horizontal = 0, vertical = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var g = new IslandGenerator(Rules(), seed);
                var land = Set(g.Commit(g.GenerateStart()).Cells);
                var key = IslandShape.Silhouette(land);
                families[key] = families.TryGetValue(key, out int n) ? n + 1 : 1;
                var b = IslandShape.Bounds(land);
                if (b.Width > b.Height) horizontal++;
                if (b.Height > b.Width) vertical++;
            }
            int max = 0;
            foreach (var n in families.Values) max = Math.Max(max, n);
            Assert.That(families.Count, Is.GreaterThanOrEqualTo(15), "distinct families over 200 seeds");
            Assert.That(max, Is.LessThan(40), "no family dominates");
            double ratio = horizontal / (double)(horizontal + vertical);
            Assert.That(ratio, Is.GreaterThan(0.35).And.LessThan(0.65), "orientation is not biased");
        }

        // --- rules -----------------------------------------------------------------------------------

        [Test]
        public void AreaLimits_RoundInward()
        {
            var rules = Rules();
            rules.AreaLimits(36, out int lo, out int hi); Assert.AreEqual((33, 39), (lo, hi));
            rules.AreaLimits(48, out lo, out hi);         Assert.AreEqual((44, 52), (lo, hi));
            rules.AreaLimits(25, out lo, out hi);         Assert.AreEqual((23, 27), (lo, hi));
        }

        [Test]
        public void Start_RejectsBothUndersizedAndOversizedCandidates()
        {
            var g = new IslandGenerator(Rules(attempts: 3), 1, Scripted(Rect(0, 0, 5, 5), Rect(0, 0, 7, 6), Rect(0, 0, 6, 6)));
            g.Commit(g.GenerateStart());
            Assert.AreEqual(36, g.Land.Count);
        }

        [Test]
        public void AreaBands_AcrossSizes_IncludeShoreRepairs()
        {
            foreach (int target in new[] { 25, 36, 48, 64, 100 })
                for (int seed = 0; seed < 3; seed++)
                {
                    var g = new IslandGenerator(Rules(startArea: target, sectionArea: target), seed);
                    for (int step = 0; step < 5; step++)
                    {
                        var section = step == 0 ? g.Commit(g.GenerateStart()) : Expand(g);
                        // Independent assertion: actual new area, including repair, is within 10%.
                        Assert.That(Math.Abs(section.Cells.Count - target) * 10, Is.LessThanOrEqualTo(target),
                                    $"target {target}, seed {seed}, step {step}: {section.Cells.Count} cells");
                    }
                }
        }

        [Test]
        public void ImpossibleConfiguration_ExplainsFailure()
        {
            var ex = Assert.Throws<ArgumentException>(() => new IslandGenerator(Rules(startArea: 9), 1));
            StringAssert.Contains("area targets", ex.Message);
        }

        // --- geometry fixtures ----------------------------------------------------------------------

        [Test]
        public void Repair_RejectsOversizedMassEvenWithoutPockets()
        {
            Assert.IsNull(IslandShape.Repair(Rect(0, 0, 6, 6), Rules(), 35));
        }

        [Test]
        public void BroadShallowBay_IsPreserved()
        {
            var land = Minus(Rect(0, 0, 12, 10), Rect(4, 7, 4, 3));
            Assert.IsEmpty(IslandShape.PocketCells(land, 4, 0.75));
            Assert.IsTrue(IslandShape.Repair(land, Rules(), 120).SetEquals(land));
        }

        [Test]
        public void NarrowBay_IsFilled()
        {
            var land = Minus(Rect(0, 0, 12, 10), Rect(4, 7, 3, 3));
            Assert.IsNotEmpty(IslandShape.PocketCells(land, 4, 0.75));
            Assert.IsTrue(IslandShape.Repair(land, Rules(), 120).SetEquals(Rect(0, 0, 12, 10)));
        }

        [Test]
        public void DeepBay_IsFilled()
        {
            var land = Minus(Rect(0, 0, 12, 10), Rect(4, 3, 4, 7));
            Assert.IsNotEmpty(IslandShape.PocketCells(land, 4, 0.75));
            Assert.IsTrue(IslandShape.Repair(land, Rules(), 120).SetEquals(Rect(0, 0, 12, 10)));
        }

        [Test]
        public void BayRules_ApplyAfterRotation()
        {
            var land = Minus(Rect(0, 0, 12, 10), Rect(4, 3, 4, 7));
            for (int turn = 0; turn < 4; turn++)
            {
                Assert.IsNotEmpty(IslandShape.PocketCells(land, 4, 0.75), $"turn {turn}");
                var fixedLand = IslandShape.Repair(land, Rules(), 120);
                Assert.IsNotNull(fixedLand, $"turn {turn}");
                Assert.IsEmpty(IslandShape.PocketCells(fixedLand, 4, 0.75), $"turn {turn}");
                land = Rotated(land);
            }
        }

        [Test]
        public void EnclosedWater_IsFilled()
        {
            var missing = Rect(4, 4, 3, 3);
            var land = Minus(Rect(0, 0, 12, 12), missing);
            Assert.IsTrue(IslandShape.Holes(land).SetEquals(missing));
            Assert.IsTrue(IslandShape.Repair(land, Rules(), 144).SetEquals(Rect(0, 0, 12, 12)));
        }

        [Test]
        public void Repair_RespectsAreaBudgetAndInput()
        {
            var land = Minus(Rect(0, 0, 12, 10), Rect(4, 3, 4, 7));
            var original = new HashSet<Vector2Int>(land);
            Assert.IsNull(IslandShape.Repair(land, Rules(), land.Count));
            Assert.IsTrue(land.SetEquals(original), "repair must not mutate its input");
        }

        [Test]
        public void DiagonalContact_IsNotConnectivity()
        {
            Assert.IsFalse(IslandShape.Connected(new HashSet<Vector2Int> { new(0, 0), new(1, 1) }));
        }

        [Test]
        public void ThinSpur_IsRejected()
        {
            var land = Union(Rect(0, 0, 6, 6), Rect(6, 2, 3, 1));
            Assert.IsTrue(IslandShape.NarrowLand(land, 3));
            Assert.IsFalse(IslandShape.NarrowLand(Rect(0, 0, 6, 6), 3));
        }

        [Test]
        public void Join_RequiresContiguousBoundary()
        {
            var old = Rect(0, 0, 6, 6);
            Assert.IsFalse(IslandShape.BroadJoin(old, new HashSet<Vector2Int> { new(6, 0), new(6, 2), new(6, 4) }, 3));
            Assert.IsTrue(IslandShape.BroadJoin(old, Rect(6, 1, 4, 3), 3));
        }

        // --- generator contract ---------------------------------------------------------------------

        [Test]
        public void ReproducibleSequence_AndUnchangedOldLand()
        {
            var a = new IslandGenerator(Rules(), 481);
            var b = new IslandGenerator(Rules(), 481);
            Assert.IsTrue(Set(a.Commit(a.GenerateStart()).Cells).SetEquals(b.Commit(b.GenerateStart()).Cells));

            for (int step = 0; step < 8; step++)
            {
                var prior = new List<HashSet<Vector2Int>>();
                foreach (var s in a.Sections) prior.Add(Set(s.Cells));
                var oldLand = Set(a.Land);

                var addedA = Expand(a);
                var addedB = Expand(b);
                Assert.IsTrue(Set(addedA.Cells).SetEquals(addedB.Cells), $"step {step}: same seed, different zone");

                Assert.AreEqual(prior.Count + 1, a.Sections.Count);
                for (int i = 0; i < prior.Count; i++)
                    Assert.IsTrue(prior[i].SetEquals(a.Sections[i].Cells), $"step {step}: section {i} changed");
                Assert.IsTrue(oldLand.IsSubsetOf(a.Land), $"step {step}: land was removed");
            }
        }

        [Test]
        public void Propose_ReturnsDistinctFamilies_EachValidAlone()
        {
            var rules = Rules();
            var g = new IslandGenerator(rules, 7);
            g.Commit(g.GenerateStart());
            var land = Set(g.Land);
            rules.AreaLimits(rules.sectionArea, out int lower, out int upper);

            var proposals = g.Propose(3);
            Assert.That(proposals.Count, Is.InRange(1, 3));

            var keys = new HashSet<SilhouetteKey>();
            foreach (var c in proposals)
            {
                var added = Set(c.Cells);
                Assert.IsTrue(keys.Add(IslandShape.Silhouette(added)), "two candidates from one shape family");
                Assert.IsFalse(added.Overlaps(land), "a zone is new land only");
                Assert.That(added.Count, Is.InRange(lower, upper));
                Assert.IsTrue(IslandShape.Connected(added));
                Assert.IsTrue(IslandShape.BroadJoin(land, added, rules.minWidth));

                var combined = Union(land, added);
                Assert.IsTrue(IslandShape.Connected(combined));
                Assert.IsEmpty(IslandShape.Holes(combined));
                Assert.IsEmpty(IslandShape.PocketCells(combined, rules.minBay, rules.depthRatio));
                Assert.IsFalse(IslandShape.NarrowLand(combined, rules.minWidth));
            }
            Assert.IsTrue(Set(g.Land).SetEquals(land), "proposing must not grow the island");
        }

        [Test]
        public void Commit_RefusesACandidateFromAnOlderIsland()
        {
            var g = new IslandGenerator(Rules(), 3);
            g.Commit(g.GenerateStart());
            var proposals = g.Propose(2);
            Assume.That(proposals.Count, Is.EqualTo(2), "seed 3 should offer two zones");

            g.Commit(proposals[0]);
            Assert.Throws<InvalidOperationException>(() => g.Commit(proposals[1]));
            Assert.Throws<InvalidOperationException>(() => g.GenerateStart(), "a start exists already");
        }

        // The prototype's --check sweep as a test: every invariant, every step, many seeds. This is the
        // guard behind IslandSystem.Expand reporting failure instead of loosening the rules — with the
        // default rules, failure must simply never happen.
        [Test]
        public void SeedSweep_KeepsEveryInvariantOnEveryStep()
        {
            const int seeds = 100, steps = 8;
            var rules = Rules();
            for (int seed = 0; seed < seeds; seed++)
            {
                var g = new IslandGenerator(rules, seed);
                g.Commit(g.GenerateStart());
                for (int step = 0; step <= steps; step++)
                {
                    string at = $"seed {seed}, step {step}";
                    var land = Set(g.Land);
                    Assert.IsTrue(IslandShape.Connected(land), $"{at}: disconnected island");
                    Assert.IsEmpty(IslandShape.Holes(land), $"{at}: enclosed water");
                    Assert.IsEmpty(IslandShape.PocketCells(land, rules.minBay, rules.depthRatio), $"{at}: shore pocket");
                    Assert.IsFalse(IslandShape.NarrowLand(land, rules.minWidth), $"{at}: thin land");

                    int sum = 0;
                    foreach (var s in g.Sections) sum += s.Cells.Count;
                    Assert.AreEqual(land.Count, sum, $"{at}: overlapping sections");

                    int target = step == 0 ? rules.startArea : rules.sectionArea;
                    rules.AreaLimits(target, out int lower, out int upper);
                    int last = g.Sections[g.Sections.Count - 1].Cells.Count;
                    Assert.That(last, Is.InRange(lower, upper), $"{at}: section area outside ±10%");

                    if (step == steps) break;
                    var added = Set(Expand(g).Cells);
                    Assert.IsTrue(land.IsSubsetOf(g.Land), $"{at}: removed land");
                    Assert.IsTrue(IslandShape.Connected(added), $"{at}: disconnected addition");
                    Assert.IsTrue(IslandShape.BroadJoin(land, added, rules.minWidth), $"{at}: narrow join");
                }
            }
        }
    }
}
