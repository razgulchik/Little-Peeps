using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The two pure halves of the tavern's aim. The casts themselves need a live physics scene; what is
    // checked here is what decides between the directions that work (PickWindow) and the bounce rule the
    // aim shares with the physics (Unit.BendAwayFromNormal) — if either drifted, shots would be taken on
    // a knife's edge or traced along a path the unit never walks.
    public class BilliardAimTests
    {
        private const int House = 0;

        private static int[] NoneReach()
        {
            var houseAt = new int[BilliardAim.Steps];
            for (int i = 0; i < houseAt.Length; i++) houseAt[i] = -1;
            return houseAt;
        }

        private static void Mark(int[] houseAt, int[] bouncesAt, int from, int to, int house, int bounces)
        {
            for (int i = from; i <= to; i++)
            {
                int k = (i + houseAt.Length) % houseAt.Length;
                houseAt[k] = house;
                bouncesAt[k] = bounces;
            }
        }

        // --- PickWindow ---------------------------------------------------------------------------------

        [Test]
        public void PickWindow_NothingReachesTheHouse_IsNoShot()
        {
            var houseAt = NoneReach();
            var bouncesAt = new int[BilliardAim.Steps];
            Mark(houseAt, bouncesAt, 10, 20, house: 1, bounces: 0);   // another house only

            Assert.IsFalse(BilliardAim.PickWindow(houseAt, bouncesAt, House, out _));
        }

        [Test]
        public void PickWindow_FewerBouncesWin_EvenOverAWiderRun()
        {
            var houseAt = NoneReach();
            var bouncesAt = new int[BilliardAim.Steps];
            Mark(houseAt, bouncesAt, 10, 40, House, bounces: 2);     // wide, but banked twice
            Mark(houseAt, bouncesAt, 100, 102, House, bounces: 0);   // narrow, straight

            Assert.IsTrue(BilliardAim.PickWindow(houseAt, bouncesAt, House, out int index));
            Assert.AreEqual(101, index);
        }

        [Test]
        public void PickWindow_TakesTheMiddleOfTheWidestRun()
        {
            var houseAt = NoneReach();
            var bouncesAt = new int[BilliardAim.Steps];
            Mark(houseAt, bouncesAt, 10, 12, House, bounces: 1);
            Mark(houseAt, bouncesAt, 50, 58, House, bounces: 1);

            Assert.IsTrue(BilliardAim.PickWindow(houseAt, bouncesAt, House, out int index));
            Assert.AreEqual(54, index);
        }

        [Test]
        public void PickWindow_ARunAcrossZero_IsOneRun()
        {
            // 355..359 and 0..4 are one run of ten around the wrap; split, each half (5) would lose to the
            // run at 100..106 (7).
            var houseAt = NoneReach();
            var bouncesAt = new int[BilliardAim.Steps];
            Mark(houseAt, bouncesAt, -5, 4, House, bounces: 0);
            Mark(houseAt, bouncesAt, 100, 106, House, bounces: 0);

            Assert.IsTrue(BilliardAim.PickWindow(houseAt, bouncesAt, House, out int index));
            Assert.AreEqual(359, index, "the middle of 355..4, counted across the wrap");
        }

        [Test]
        public void PickWindow_OtherHousesDoNotBreakOrJoinARun()
        {
            var houseAt = NoneReach();
            var bouncesAt = new int[BilliardAim.Steps];
            Mark(houseAt, bouncesAt, 20, 22, House, bounces: 0);
            Mark(houseAt, bouncesAt, 23, 60, house: 1, bounces: 0);   // right next to it, but not ours

            Assert.IsTrue(BilliardAim.PickWindow(houseAt, bouncesAt, House, out int index));
            Assert.AreEqual(21, index);
        }

        // --- the shared bounce rule -----------------------------------------------------------------------

        [Test]
        public void Bend_ADirectionWellOffTheNormal_IsLeftAlone()
        {
            var dir = new Vector2(1f, 1f).normalized;   // 45° off the normal (0,1)

            var bent = Unit.BendAwayFromNormal(dir, Vector2.up, 10f, 1f);

            Assert.That((bent - dir).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void Bend_ANearPerpendicularExit_IsBentOutOnItsOwnSide()
        {
            float r = 4f * Mathf.Deg2Rad;                       // 4° to the LEFT of straight up
            var dir = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));

            var bent = Unit.BendAwayFromNormal(dir, Vector2.up, 10f, -1f);   // tie side must not matter here

            Assert.AreEqual(10f, Vector2.SignedAngle(Vector2.up, bent), 1e-3f);
        }

        [Test]
        public void Bend_DeadOnTheNormal_GoesToTheTieSide()
        {
            Assert.AreEqual(-10f, Vector2.SignedAngle(Vector2.up, Unit.BendAwayFromNormal(Vector2.up, Vector2.up, 10f, -1f)), 1e-3f);
            Assert.AreEqual(10f, Vector2.SignedAngle(Vector2.up, Unit.BendAwayFromNormal(Vector2.up, Vector2.up, 10f, 1f)), 1e-3f);
        }

        [Test]
        public void Bend_WithTheRuleOff_ChangesNothing()
        {
            Assert.That((Unit.BendAwayFromNormal(Vector2.up, Vector2.up, 0f, 1f) - Vector2.up).magnitude, Is.LessThan(1e-6f));
        }
    }
}
