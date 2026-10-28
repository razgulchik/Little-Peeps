using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // BumpView on the hit report: which hits announce a wave (BumpedEvent), with which verdict. The report comes
    // through a real CollisionTarget — its Resolve, called by hand with a made-up bounce, since a Collision2D
    // can't be built — so the subscription is what is tested, not a private shortcut. The view has no squash
    // target: nothing runs a motion in Edit Mode, and only the announcement is watched. The target has nothing
    // on it, so a hit that did nothing reads Useless. Edit Mode runs no Awake or OnEnable — called by hand.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class BumpViewTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject structure;
        private GameObject unitGo;
        private CollisionTarget target;
        private Unit unit;
        private readonly List<BumpedEvent> announced = new();

        [SetUp]
        public void SetUp()
        {
            structure = new GameObject("Structure");
            structure.AddComponent<BoxCollider2D>();
            target = structure.AddComponent<CollisionTarget>();
            var view = structure.AddComponent<BumpView>();
            Call(target, typeof(CollisionTarget), "Awake");
            Call(view, typeof(BumpView), "Awake");
            Call(view, typeof(BumpView), "OnEnable");

            unitGo = new GameObject("Unit");
            unit = unitGo.AddComponent<Unit>();

            announced.Clear();
            EventBus<BumpedEvent>.Subscribe(Record);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<BumpedEvent>.Unsubscribe(Record);
            Object.DestroyImmediate(structure);
            Object.DestroyImmediate(unitGo);
        }

        private void Record(BumpedEvent e) => announced.Add(e);

        [Test]
        public void AHitThatWorked_AnnouncesItsWave_WithTheVerdictAndTheFace()
        {
            Bounce(HitOutcome.Worked, new Vector2Int(1, 0));

            Assert.That(announced, Has.Count.EqualTo(1));
            Assert.That(announced[0].Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(announced[0].Side, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That((Vector2)announced[0].Position, Is.EqualTo(new Vector2(0.5f, 0.25f)));
        }

        // Whether a useless hit gets a wave is the wave system's call (none by default), not this view's.
        [Test]
        public void AUselessHit_IsAnnouncedToo()
        {
            Bounce(HitOutcome.None, new Vector2Int(0, -1));

            Assert.That(announced, Has.Count.EqualTo(1));
            Assert.That(announced[0].Verdict, Is.EqualTo(HitVerdict.Useless));
        }

        // The last hit on a source is the harvest effect's to show.
        [Test]
        public void TheLastHit_ShowsNothing()
        {
            Bounce(HitOutcome.UsedUp, new Vector2Int(1, 0));

            Assert.That(announced, Is.Empty);
        }

        [Test]
        public void AUnitCrossingATrigger_ShowsNothing()
        {
            Resolve(HitOutcome.Worked, new HitReport { Contact = HitContact.Pass });

            Assert.That(announced, Is.Empty);
        }

        [Test]
        public void ABounceWithNoFace_HasNoWaveToAnnounce()
        {
            Bounce(HitOutcome.Worked, Vector2Int.zero);

            Assert.That(announced, Is.Empty);
        }

        // A resource node shows the squash alone — its prefab switches the waves off.
        [Test]
        public void WithWavesOff_NothingIsAnnounced()
        {
            typeof(BumpView).GetField("waves", PrivateInstance).SetValue(structure.GetComponent<BumpView>(), false);

            Bounce(HitOutcome.Worked, new Vector2Int(1, 0));

            Assert.That(announced, Is.Empty);
        }

        // Edit Mode time stands still inside a test, so every bump after the first is inside the interval.
        [Test]
        public void InsideTheInterval_OnlyAStrongerVerdictGetsThrough()
        {
            Bounce(HitOutcome.None, new Vector2Int(0, 1));     // useless: shown
            Bounce(HitOutcome.None, new Vector2Int(0, 1));     // useless again: skipped
            Bounce(HitOutcome.Worked, new Vector2Int(0, 1));   // useful: stronger, shown
            Bounce(HitOutcome.None, new Vector2Int(0, 1));     // useless: skipped

            Assert.That(announced.ConvertAll(e => e.Verdict),
                        Is.EqualTo(new[] { HitVerdict.Useless, HitVerdict.Useful }));
        }

        private void Bounce(HitOutcome outcome, Vector2Int side)
            => Resolve(outcome, new HitReport { Contact = HitContact.Bounce, Point = new Vector2(0.5f, 0.25f), Side = side });

        private void Resolve(HitOutcome outcome, HitReport report)
            => typeof(CollisionTarget).GetMethod("Resolve", PrivateInstance).Invoke(target, new object[] { unit, outcome, report });

        private static void Call(object component, System.Type type, string method)
            => type.GetMethod(method, PrivateInstance).Invoke(component, null);
    }
}
