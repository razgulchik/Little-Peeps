using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // How carried units hang round the cursor, and so which way each leaves when they are let go: evenly, to all
    // sides, each away from the side it hung on.
    public class CarrySystemTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Alone_HangsRightOnTheCursor()
            => Assert.That(CarrySystem.SpreadOffset(0, 1, 1.3f, 0.2f), Is.EqualTo(Vector2.zero));

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void Several_AreSpreadEvenlyAllRound(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var here = CarrySystem.SpreadDirection(i, count, 0.7f);
                var next = CarrySystem.SpreadDirection((i + 1) % count, count, 0.7f);

                Assert.That(here.magnitude, Is.EqualTo(1f).Within(Tolerance));
                Assert.That(Vector2.Angle(here, next), Is.EqualTo(360f / count).Within(0.1f));
            }
        }

        [Test]
        public void Several_HangOnTheRing_OnTheSideEachLeavesBy()
        {
            for (int i = 0; i < 3; i++)
            {
                var offset = CarrySystem.SpreadOffset(i, 3, -2.1f, 0.25f);
                var expected = CarrySystem.SpreadDirection(i, 3, -2.1f) * 0.25f;

                Assert.That(offset.x, Is.EqualTo(expected.x).Within(Tolerance));
                Assert.That(offset.y, Is.EqualTo(expected.y).Within(Tolerance));
            }
        }

        [Test]
        public void TheFirstOne_IsOnTheGivenSide()
        {
            var up = CarrySystem.SpreadDirection(0, 3, Mathf.PI * 0.5f);

            Assert.That(up.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(up.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Alone_StillHasAWayToLeave()
            => Assert.That(CarrySystem.SpreadDirection(0, 1, 2f).magnitude, Is.EqualTo(1f).Within(Tolerance));
    }

    // How many units the hand has room for: none until something in the run gives it some.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class CarryCapacityTests
    {
        private GameObject go;
        private CarrySystem carry;
        private RunContext run;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("CarrySystem");
            carry = go.AddComponent<CarrySystem>();
            run = new RunContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void WithNoRun_ThereIsNoRoom() => Assert.That(carry.Capacity, Is.Zero);

        [Test]
        public void ARunStartsWithNoRoom()
        {
            carry.Initialize(run);

            Assert.That(carry.Capacity, Is.Zero);
        }

        // The meta perk opens it, a perk makes room for more — read live, so one bought mid-run counts at once.
        [Test]
        public void EveryModifierOnTop_IsRoomForMore()
        {
            carry.Initialize(run);

            run.stats.Add(Room(3f));
            Assert.That(carry.Capacity, Is.EqualTo(3));

            run.stats.Add(Room(1f));
            Assert.That(carry.Capacity, Is.EqualTo(4));
        }

        [Test]
        public void APenalty_NeverTakesItBelowNone()
        {
            carry.Initialize(run);
            run.stats.Add(Room(-2f));

            Assert.That(carry.Capacity, Is.Zero);
        }

        private static StatModifier Room(float units)
            => new StatModifier { id = StatId.CarryCapacity, kind = ModifierKind.Flat, value = units };
    }
}
