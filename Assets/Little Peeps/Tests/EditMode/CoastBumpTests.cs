using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The coast's rules that are pure numbers. The ripple starts where the water does — past the outline the art
    // draws outside the land, a band to the south, a column to the east and west, nothing to the north
    // (CoastBump.Waterline) — and it is throttled per place, not per object, since the coast is one object round
    // the whole island (CoastBump.RecentPlaces). The bulge pushes the corners near the hit (Spread, Falloff, Snap).
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

        [Test]
        public void Falloff_FullAtTheHit_NothingAtTheReach_AndFallingBetween()
        {
            Assert.That(CoastBump.Falloff(0f), Is.EqualTo(1f));
            Assert.That(CoastBump.Falloff(1f), Is.EqualTo(0f));
            Assert.That(CoastBump.Falloff(2f), Is.EqualTo(0f), "beyond the reach");
            Assert.That(CoastBump.Falloff(0.25f), Is.GreaterThan(CoastBump.Falloff(0.5f)));
            Assert.That(CoastBump.Falloff(0.5f), Is.GreaterThan(CoastBump.Falloff(0.75f)));
        }

        [Test]
        public void Spread_SouthCoast_PushesTheRowItIsOn_Symmetrically()
        {
            var pushes = new Dictionary<Vector2Int, Vector2>();
            CoastBump.Spread(new Vector2(5f, 3.01f), Vector2Int.down, Vector2.down, 2f, pushes);

            Assert.That(pushes.Keys, Is.EquivalentTo(new[]
            {
                new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3)
            }), "the reach's own corners get nothing");
            Assert.That(pushes[new Vector2Int(5, 3)], Is.EqualTo(Vector2.down));
            Assert.That(pushes[new Vector2Int(4, 3)], Is.EqualTo(pushes[new Vector2Int(6, 3)]));
            Assert.That(pushes[new Vector2Int(4, 3)].y, Is.InRange(-1f, 0f).And.Not.EqualTo(0f));
        }

        [Test]
        public void Spread_EastCoast_PushesTheColumnItIsOn()
        {
            var pushes = new Dictionary<Vector2Int, Vector2>();
            CoastBump.Spread(new Vector2(7.98f, 2.5f), Vector2Int.right, Vector2.right, 1f, pushes);

            Assert.That(pushes.Keys, Is.EquivalentTo(new[] { new Vector2Int(8, 2), new Vector2Int(8, 3) }));
            foreach (var push in pushes.Values)
            {
                Assert.That(push.y, Is.EqualTo(0f));
                Assert.That(push.x, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Spread_HitsCloseTogether_AddUp()
        {
            var pushes = new Dictionary<Vector2Int, Vector2>();
            CoastBump.Spread(new Vector2(5f, 3f), Vector2Int.down, Vector2.down, 2f, pushes);
            CoastBump.Spread(new Vector2(5f, 3f), Vector2Int.down, Vector2.down, 2f, pushes);

            Assert.That(pushes[new Vector2Int(5, 3)], Is.EqualTo(2f * Vector2.down));
        }

        [Test]
        public void Snap_RoundsToWholeArtPixels()
        {
            Assert.That(CoastBump.Snap(new Vector2(0.09f, -0.05f), 16), Is.EqualTo(new Vector2(0.0625f, -0.0625f)));
            Assert.That(CoastBump.Snap(new Vector2(0.02f, 0f), 16), Is.EqualTo(Vector2.zero));
        }
    }
}
