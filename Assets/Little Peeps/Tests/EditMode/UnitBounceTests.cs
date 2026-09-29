using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The unit's bounce rule (Unit.BendAwayFromNormal): what keeps a unit from shuttling forever between two
    // parallel fences.
    public class UnitBounceTests
    {

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
