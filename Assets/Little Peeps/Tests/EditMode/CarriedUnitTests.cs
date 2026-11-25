using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps.Tests
{
    // What being carried does to a unit (Unit.PickUp / PutDown): it is off the field and in sight, and comes back
    // exactly as it was taken — the cursor gives it another place and nothing else.
    // Edit Mode runs no Awake or OnEnable, so MakeUnit calls them, as the pool would.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class CarriedUnitTests
    {
        private readonly List<Object> made = new();
        private CarrySystem carrier;
        private UnitDef unitDef;

        [SetUp]
        public void SetUp()
        {
            carrier = Make("CarrySystem").AddComponent<CarrySystem>();
            unitDef = Keep(ScriptableObject.CreateInstance<UnitDef>());
            unitDef.speed = 2f;
            unitDef.stamina = 10f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in made) if (obj != null) Object.DestroyImmediate(obj);
            made.Clear();
        }

        [Test]
        public void PickedUp_IsOffTheField_AndCarried()
        {
            var unit = Launched();

            unit.PickUp(carrier, 0, 0);

            Assert.That(unit.IsCarried, Is.True);
            Assert.That(unit.IsOnField, Is.False);
        }

        [Test]
        public void PutDown_IsBackOnTheField_WhereItWasPut()
        {
            var unit = Launched();
            unit.PickUp(carrier, 0, 0);

            unit.PutDown(new Vector2(-3f, 2f), Vector2.up);

            Assert.That(unit.IsCarried, Is.False);
            Assert.That(unit.IsOnField, Is.True);
            Assert.That(unit.transform.position.x, Is.EqualTo(-3f).Within(1e-4f));
            Assert.That(unit.transform.position.y, Is.EqualTo(2f).Within(1e-4f));
        }

        // Let go of by the player, it leaves boosted; lost to the sea it does not.
        [Test]
        public void PutDown_LeavesAsBoostedAsItIsTold()
        {
            var unit = Launched();

            unit.PickUp(carrier, 0, 0);
            unit.PutDown(Vector2.one, Vector2.up);
            Assert.That(unit.Velocity.magnitude, Is.EqualTo(2f).Within(1e-3f), "its ordinary speed");

            unit.PickUp(carrier, 0, 0);
            unit.PutDown(Vector2.one, Vector2.up, 5f, 5f);
            Assert.That(unit.Velocity.magnitude, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(unit.Velocity.normalized.y, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void AWorkingUnit_ComesBackWorking_WithItsProfession()
        {
            var unit = Launched();
            var lumberjack = Keep(ScriptableObject.CreateInstance<ProfessionDef>());
            lumberjack.type = UnitType.Lumberjack;
            unit.Equip(lumberjack, null);

            Trip(unit);

            Assert.That(unit.IsTired, Is.False);
            Assert.That(unit.Type, Is.EqualTo(UnitType.Lumberjack));
        }

        [Test]
        public void ATiredUnit_ComesBackTired()
        {
            var unit = MakeUnit();   // never launched, so no stamina

            Trip(unit);

            Assert.That(unit.IsTired, Is.True);
        }

        // The tavern sent it home on autopilot; being moved on the way does not call that off.
        [Test]
        public void ADrunkOnAutopilot_IsStillOnIt_AfterATrip()
        {
            var unit = Launched();
            unit.MakeDrunk(10f, 0.6f);
            unit.EnterAutopilot();
            Assert.That(unit.IsOnAutopilot, Is.True, "sanity");

            Trip(unit);

            Assert.That(unit.IsOnAutopilot, Is.True);
        }

        // Out of the game mid-carry (its house sold, the run over): the carrier is told, and it is nobody's.
        [Test]
        public void LeavingTheHand_IsCarriedNoMore_AndStaysOffTheField()
        {
            var unit = Launched();
            unit.PickUp(carrier, 0, 0);

            unit.LeaveHand();

            Assert.That(unit.IsCarried, Is.False);
            Assert.That(unit.IsOnField, Is.False);
        }

        // --- plumbing -------------------------------------------------------------------------------

        private void Trip(Unit unit)
        {
            unit.PickUp(carrier, 0, 0);
            unit.PutDown(Vector2.one, Vector2.left);
        }

        private Unit Launched()
        {
            var unit = MakeUnit();
            unit.Launch(Vector2.right);
            return unit;
        }

        private Unit MakeUnit()
        {
            var go = Make("Unit");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            var unit = go.AddComponent<Unit>();
            unit.def = unitDef;
            Call(unit, "Awake");
            Call(unit, "OnEnable");
            return unit;
        }

        private static void Call(Unit unit, string method)
            => (typeof(Unit).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(Unit), method)).Invoke(unit, null);

        private GameObject Make(string name) => Keep(new GameObject(name));

        private T Keep<T>(T obj) where T : Object
        {
            made.Add(obj);
            return obj;
        }
    }
}
