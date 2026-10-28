using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // LeanView on the hit report: which crossings bend the object, which way, and when — past the middle, or on
    // the way out if never. The report comes through a real CollisionTarget — its Resolve, called by hand with a
    // made-up crossing whose middle is the origin, and its OnTriggerExit2D — so the subscriptions are what is
    // tested. The spring is stepped by hand (Tick) instead of by frames. Edit Mode runs no Awake or OnEnable —
    // called by hand. Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class LeanViewTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject field;
        private GameObject unitGo;
        private CollisionTarget target;
        private LeanView view;
        private Transform visual;
        private Unit unit;

        [SetUp]
        public void SetUp()
        {
            field = new GameObject("Field");
            field.AddComponent<BoxCollider2D>().isTrigger = true;
            target = field.AddComponent<CollisionTarget>();
            visual = new GameObject("Visual").transform;
            visual.SetParent(field.transform, false);
            view = field.AddComponent<LeanView>();
            typeof(LeanView).GetField("leanTarget", PrivateInstance).SetValue(view, visual);
            Call(target, typeof(CollisionTarget), "Awake");
            Call(view, typeof(LeanView), "Awake");
            Call(view, typeof(LeanView), "OnEnable");

            unitGo = new GameObject("Unit");
            unitGo.AddComponent<Rigidbody2D>();
            unitGo.AddComponent<CircleCollider2D>();
            unit = unitGo.AddComponent<Unit>();
            Call(unit, typeof(Unit), "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(field);
            Object.DestroyImmediate(unitGo);
        }

        [Test]
        public void ACrossingToTheRight_TiltsTheTopRight()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            FlyPast();
            Tick(0.05f);

            Assert.That(Mathf.DeltaAngle(0f, visual.localEulerAngles.z), Is.LessThan(-0.5f), "clockwise = top to the right");
        }

        [Test]
        public void ACrossingUpTheScreen_Stretches()
        {
            Cross(new Vector2(0f, 3f), HitOutcome.None);
            FlyPast();
            Tick(0.05f);

            Assert.That(visual.localScale.y, Is.GreaterThan(1.001f));
        }

        // The wake, not the touch: the way in bends nothing.
        [Test]
        public void BeforeTheMiddle_NothingBends()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            Tick(0.05f);

            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void LeavingBeforeTheMiddle_BendsOnTheWayOut()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            Leave();
            Tick(0.05f);

            Assert.That(Mathf.DeltaAngle(0f, visual.localEulerAngles.z), Is.LessThan(-0.5f));
        }

        // Past the middle and then out: one kick, not two. Read off the spring's speed, which only a kick changes
        // while no time passes.
        [Test]
        public void ACrossing_KicksOnce()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            FlyPast();
            Tick(0f);
            Vector2 once = Swing;
            Assert.That(once.x, Is.GreaterThan(0f), "kicked past the middle");

            Leave();
            Tick(0f);
            Assert.That(Swing, Is.EqualTo(once), "not again on the way out");
        }

        // Back in after leaving is a new crossing.
        [Test]
        public void TheSameUnitBackIn_KicksAgain()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            FlyPast();
            Tick(0f);
            Leave();
            Vector2 once = Swing;

            Cross(new Vector2(3f, 0f), HitOutcome.None);
            FlyPast();
            Tick(0f);

            Assert.That(Swing.x, Is.GreaterThan(once.x));
        }

        // The last hit on a source is the harvest effect's to show.
        [Test]
        public void TheLastHit_BendsNothing()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.UsedUp);
            FlyPast();
            Tick(0.05f);

            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        }

        // ...and drops the crossings still on their way: the harvest shows the field from rest.
        [Test]
        public void TheLastHit_DropsTheCrossingsStillOnTheirWay()
        {
            Cross(new Vector2(3f, 0f), HitOutcome.None);
            Resolve(HitOutcome.UsedUp, new HitReport { Contact = HitContact.Pass });
            FlyPast();
            Tick(0.05f);

            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
        }

        // A bounce is BumpView's.
        [Test]
        public void ABounce_BendsNothing()
        {
            SetVelocity(new Vector2(3f, 0f));
            Resolve(HitOutcome.None, new HitReport { Contact = HitContact.Bounce, Side = new Vector2Int(-1, 0) });
            Tick(0.05f);

            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void AfterTheSwing_ItStandsStraightAgain()
        {
            Cross(new Vector2(3f, 1f), HitOutcome.None);
            FlyPast();
            for (int i = 0; i < 300; i++) Tick(1f / 30f);

            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        }

        // In through the near side of the middle (the origin), heading across it.
        private void Cross(Vector2 velocity, HitOutcome outcome)
        {
            SetVelocity(velocity);
            unitGo.transform.position = -velocity.normalized * 0.5f;
            Resolve(outcome, new HitReport { Contact = HitContact.Pass, Point = Vector2.zero });
        }

        private void FlyPast() => unitGo.transform.position = unit.Velocity.normalized * 0.1f;

        private void Leave()
            => typeof(CollisionTarget).GetMethod("OnTriggerExit2D", PrivateInstance)
                                      .Invoke(target, new object[] { unitGo.GetComponent<Collider2D>() });

        private Vector2 Swing => (Vector2)typeof(LeanView).GetField("swing", PrivateInstance).GetValue(view);

        private void SetVelocity(Vector2 velocity) => unitGo.GetComponent<Rigidbody2D>().linearVelocity = velocity;

        private void Resolve(HitOutcome outcome, HitReport report)
            => typeof(CollisionTarget).GetMethod("Resolve", PrivateInstance).Invoke(target, new object[] { unit, outcome, report });

        private void Tick(float dt) => typeof(LeanView).GetMethod("Tick", PrivateInstance).Invoke(view, new object[] { dt });

        private static void Call(object component, System.Type type, string method)
            => type.GetMethod(method, PrivateInstance).Invoke(component, null);
    }
}
