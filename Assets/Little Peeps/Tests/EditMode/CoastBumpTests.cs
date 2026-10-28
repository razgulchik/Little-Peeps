using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The coast ripple's two rules that are pure numbers: it starts where the water does — past the outline the art
    // draws outside the land, a band to the south, a column to the east and west, nothing to the north
    // (CoastBump.Waterline) — and it is throttled per place, not per object, since the coast is one object round
    // the whole island (CoastBump.RecentPlaces).
    public class CoastBumpTests
    {
        private static Vector2 Waterline(Vector2Int toWater) => CoastBump.Waterline(new Vector2(3f, 2f), toWater, 4, 1, 0, 16);

        [Test]
        public void South_StepsOverTheBand()
        {
            Assert.That(Waterline(Vector2Int.down), Is.EqualTo(new Vector2(3f, 2f - 0.25f)));
        }

        [Test]
        public void EastAndWest_StepOverTheColumn()
        {
            Assert.That(Waterline(Vector2Int.right), Is.EqualTo(new Vector2(3f + 0.0625f, 2f)));
            Assert.That(Waterline(Vector2Int.left), Is.EqualTo(new Vector2(3f - 0.0625f, 2f)));
        }

        [Test]
        public void North_HasNoOutline()
        {
            Assert.That(Waterline(Vector2Int.up), Is.EqualTo(new Vector2(3f, 2f)));
        }

        [Test]
        public void Throttle_ANearbyPlace_IsSkippedForAWhile()
        {
            var recent = new CoastBump.RecentPlaces(4);

            Assert.IsTrue(recent.TryClaim(new Vector2(0f, 0f), 10f, 0.75f, 0.25f));
            Assert.IsFalse(recent.TryClaim(new Vector2(0.5f, 0f), 10.1f, 0.75f, 0.25f), "near and recent");
            Assert.IsTrue(recent.TryClaim(new Vector2(0.5f, 0f), 10.3f, 0.75f, 0.25f), "near, but the interval is over");
        }

        [Test]
        public void Throttle_AnotherPlace_IsNotHeldUp()
        {
            var recent = new CoastBump.RecentPlaces(4);

            Assert.IsTrue(recent.TryClaim(new Vector2(0f, 0f), 10f, 0.75f, 0.25f));
            Assert.IsTrue(recent.TryClaim(new Vector2(3f, 0f), 10f, 0.75f, 0.25f), "elsewhere on the coast");
        }

        [Test]
        public void Throttle_RemembersOnlyTheLastFew()
        {
            var recent = new CoastBump.RecentPlaces(2);

            recent.TryClaim(new Vector2(0f, 0f), 10f, 0.75f, 0.25f);
            recent.TryClaim(new Vector2(5f, 0f), 10f, 0.75f, 0.25f);
            recent.TryClaim(new Vector2(10f, 0f), 10f, 0.75f, 0.25f);   // pushes the first one out

            Assert.IsTrue(recent.TryClaim(new Vector2(0f, 0f), 10f, 0.75f, 0.25f));
        }
    }
}
