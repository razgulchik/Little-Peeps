using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Which hits count as a strike on a sawmill end — the gate's whole question, asked without a sawmill. How
    // the ends take turns on a real CollisionTarget is in HitVerdictTests (Editor Test Runner only).
    // The bench here spans x -1.5..1.5, like the real 3×1 one.
    public class SawmillStrikeTests
    {
        private const float MinX = -1.5f, MaxX = 1.5f;

        private static HitReport Bounce(int sideX, int sideY, float x = 0f)
            => new() { Contact = HitContact.Bounce, Side = new Vector2Int(sideX, sideY), Point = new Vector2(x, 0.5f) };

        private static bool Strikes(Sawmill.End end, HitReport hit, float reach) => Sawmill.Strikes(end, hit, MinX, MaxX, reach);

        [TestCase(Sawmill.End.Left, -1, 0, true)]
        [TestCase(Sawmill.End.Left, 1, 0, false)]
        [TestCase(Sawmill.End.Right, 1, 0, true)]
        [TestCase(Sawmill.End.Right, -1, 0, false)]
        [TestCase(Sawmill.End.Left, 0, 1, false)]
        [TestCase(Sawmill.End.Right, 0, -1, false)]
        public void NoReach_StrikesOnlyThatEndsFace(Sawmill.End end, int x, int y, bool strikes)
        {
            float pointX = x != 0 ? x * 1.5f : 0f;   // a face hit lands on that face
            Assert.That(Strikes(end, Bounce(x, y, pointX), 0f), Is.EqualTo(strikes));
        }

        // Half a cell of reach: the top and bottom edges count for an end up to half a cell in from it.
        [TestCase(Sawmill.End.Left, 1, -1.4f, true)]
        [TestCase(Sawmill.End.Left, -1, -1.0f, true)]     // right on the zone's inner edge
        [TestCase(Sawmill.End.Left, 1, -0.9f, false)]
        [TestCase(Sawmill.End.Left, 1, 1.4f, false)]      // near the OTHER end
        [TestCase(Sawmill.End.Right, -1, 1.4f, true)]
        [TestCase(Sawmill.End.Right, 1, 0.9f, false)]
        [TestCase(Sawmill.End.Right, 1, 0f, false)]       // the middle of the bench
        public void HalfCellReach_LongEdgesNearTheEnd(Sawmill.End end, int sideY, float x, bool strikes)
            => Assert.That(Strikes(end, Bounce(0, sideY, x), 0.5f), Is.EqualTo(strikes));

        // The opposite end face never counts, however wide the reach.
        [TestCase(0.5f)]
        [TestCase(1.5f)]
        [TestCase(10f)]
        public void Reach_NeverTakesTheOtherEndsFace(float reach)
            => Assert.That(Strikes(Sawmill.End.Left, Bounce(1, 0, MaxX), reach), Is.False);

        // Capped at half the bench: the ends meet in the middle and never overlap.
        [Test]
        public void Reach_IsCappedAtHalfTheBench()
        {
            Assert.That(Strikes(Sawmill.End.Left, Bounce(0, 1, -0.01f), 10f), Is.True);
            Assert.That(Strikes(Sawmill.End.Left, Bounce(0, 1, 0.01f), 10f), Is.False);
        }

        // No contact point reported (Side zero): its Point means nothing, so no reach can make it a strike.
        [Test]
        public void BounceWithNoFace_NeverStrikes()
            => Assert.That(Strikes(Sawmill.End.Left, Bounce(0, 0, MinX), 0.5f), Is.False);

        // A walk-through or an arrow has no face of its own — even a stray Side or Point on it is not a strike.
        [TestCase(HitContact.Pass)]
        [TestCase(HitContact.Ranged)]
        public void NoBounce_NeverStrikes(HitContact contact)
        {
            var hit = new HitReport { Contact = contact, Side = Vector2Int.left, Point = new Vector2(MinX, 0.5f) };

            Assert.That(Strikes(Sawmill.End.Left, hit, 0.5f), Is.False);
        }

        [Test]
        public void Other_SwapsTheEnds()
        {
            Assert.That(Sawmill.Other(Sawmill.End.Left), Is.EqualTo(Sawmill.End.Right));
            Assert.That(Sawmill.Other(Sawmill.End.Right), Is.EqualTo(Sawmill.End.Left));
        }
    }
}
