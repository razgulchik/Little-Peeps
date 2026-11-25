using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps.Tests
{
    // How a unit that has run out of stamina gets home: by itself at once in a run whose tired go home
    // (TiredGoHomeUpgradeDef), by a tap in any run, and otherwise not before it finds a door.
    // Houses warm up with their slots empty (no UnitPool) and the unit is told which of them is its own, as the one
    // that spawned it would. Edit Mode runs no Awake, OnEnable or FixedUpdate, so the test calls them.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class TiredUnitTests
    {
        private readonly List<Object> made = new();
        private RunContext run;
        private SpawnSystem spawns;
        private UnitDef unitDef;

        [SetUp]
        public void SetUp()
        {
            run = new RunContext();
            spawns = Make("SpawnSystem").AddComponent<SpawnSystem>();
            spawns.Initialize(run);

            unitDef = Keep(ScriptableObject.CreateInstance<UnitDef>());
            unitDef.speed = 2f;
            unitDef.stamina = Time.fixedDeltaTime * 1.5f;   // out of it on the second step
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in made) if (obj != null) Object.DestroyImmediate(obj);
            made.Clear();
        }

        // --- out of stamina -------------------------------------------------------------------------

        [Test]
        public void WhetherTheTiredGoHome_IsTheRunsOwn()
        {
            run.tiredGoHome = true;
            Assert.That(spawns.TiredGoHome, Is.True);

            spawns.Initialize(new RunContext());
            Assert.That(spawns.TiredGoHome, Is.False, "the next run starts without it");
        }

        [Test]
        public void WithoutIt_ATiredUnit_StaysOnTheField()
        {
            var house = House(Vector2.zero);
            var unit = Launched(house);

            Tire(unit);

            Assert.That(unit.IsTired, Is.True);
            Assert.That(unit.IsOnField, Is.True);
            Assert.That(house.HasFreeSlot, Is.True);
        }

        [Test]
        public void WithIt_ATiredUnit_IsInItsOwnHouse_HoweverFar()
        {
            run.tiredGoHome = true;
            var near = House(Vector2.zero);
            var own = House(new Vector2(50f, 0f));
            var unit = Launched(own);

            Tire(unit);

            Assert.That(unit.IsOnField, Is.False);
            Assert.That(own.HasFreeSlot, Is.False);
            Assert.That(near.HasFreeSlot, Is.True);
        }

        [Test]
        public void WithIt_AWorkingUnit_StaysOnTheField()
        {
            run.tiredGoHome = true;
            var unit = Launched(House(Vector2.zero));

            Step(unit);

            Assert.That(unit.IsTired, Is.False, "sanity");
            Assert.That(unit.IsOnField, Is.True);
        }

        [Test]
        public void WithIt_ItsOwnHouseFull_ATiredUnit_IsInTheNearest()
        {
            run.tiredGoHome = true;
            var own = House(Vector2.zero);
            var far = House(new Vector2(50f, 0f));
            var near = House(new Vector2(5f, 0f));
            var unit = Launched(own);
            Assert.That(own.TryShelter(Launched(far)), Is.True, "sanity");

            Tire(unit);

            Assert.That(unit.IsOnField, Is.False);
            Assert.That(near.HasFreeSlot, Is.False);
            Assert.That(far.HasFreeSlot, Is.True);
        }

        [Test]
        public void WithIt_EverySlotTaken_ATiredUnit_StaysOnTheField()
        {
            run.tiredGoHome = true;
            var own = House(Vector2.zero);
            var unit = Launched(own);
            Assert.That(own.TryShelter(Launched(own)), Is.True, "sanity");

            Tire(unit);

            Assert.That(unit.IsTired, Is.True);
            Assert.That(unit.IsOnField, Is.True);
        }

        // Its outing is over like any other: the tool is given up and the drink slept off.
        [Test]
        public void WithIt_ADrunkWorker_GoesHomeSober_AndWithoutItsProfession()
        {
            run.tiredGoHome = true;
            var lumberjack = Keep(ScriptableObject.CreateInstance<ProfessionDef>());
            lumberjack.type = UnitType.Lumberjack;
            var unit = Launched(House(Vector2.zero));
            unit.Equip(lumberjack, null);
            unit.MakeDrunk(10f, 0.6f);

            Tire(unit);

            Assert.That(unit.IsOnField, Is.False);
            Assert.That(unit.IsDrunk, Is.False);
            Assert.That(unit.Type, Is.EqualTo(UnitType.Unassigned));
        }

        // The stamina clock stands still in the hand, so nobody vanishes out of it.
        [Test]
        public void WithIt_ACarriedUnit_StaysInTheHand()
        {
            run.tiredGoHome = true;
            var carrier = Make("CarrySystem").AddComponent<CarrySystem>();
            var unit = Launched(House(Vector2.zero));
            unit.PickUp(carrier, 0, 0);

            Step(unit);
            Step(unit);
            Step(unit);

            Assert.That(unit.IsCarried, Is.True);
            Assert.That(unit.IsTired, Is.False);
        }

        // --- the tap --------------------------------------------------------------------------------

        [Test]
        public void ATap_SendsATiredUnitHome()
        {
            var house = House(Vector2.zero);
            var unit = Launched(house);
            Tire(unit);

            Tap().Push(new[] { unit });

            Assert.That(unit.IsOnField, Is.False);
            Assert.That(house.HasFreeSlot, Is.False);
        }

        [Test]
        public void ATap_BoostsAWorkingUnit()
        {
            var house = House(Vector2.zero);
            var unit = Launched(house);

            Tap().Push(new[] { unit });

            Assert.That(unit.IsOnField, Is.True);
            Assert.That(unit.Velocity.magnitude, Is.GreaterThan(2f));
            Assert.That(house.HasFreeSlot, Is.True);
        }

        [Test]
        public void ATap_BoostsATiredUnit_WhenEverySlotIsTaken()
        {
            unitDef.tiredSpeedMultiplier = 1f;
            var house = House(Vector2.zero);
            var unit = Launched(house);
            Tire(unit);
            Assert.That(house.TryShelter(Launched(house)), Is.True, "sanity");

            Tap().Push(new[] { unit });

            Assert.That(unit.IsOnField, Is.True);
            Assert.That(unit.Velocity.magnitude, Is.GreaterThan(2f));
        }

        // --- plumbing -------------------------------------------------------------------------------

        private TapSystem Tap()
        {
            var tap = Make("TapSystem").AddComponent<TapSystem>();
            tap.Initialize(run);
            return tap;
        }

        // One slot, empty.
        private Spawner House(Vector2 position)
        {
            var go = Make("House");   // [RequireComponent] pulls in Structure
            go.transform.position = position;
            var house = go.AddComponent<Spawner>();
            house.unitDef = unitDef;
            house.capacity = 1;
            house.Initialize(spawns, null, null);
            house.Warmup();
            return house;
        }

        // On the field at the origin, with full stamina.
        private Unit Launched(Spawner birthHouse)
        {
            var go = Make("Unit");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            var unit = go.AddComponent<Unit>();
            unit.def = unitDef;
            Call(unit, "Awake");
            Call(unit, "OnEnable");
            unit.SetSpawns(spawns);
            unit.SetBirthHouse(birthHouse);
            unit.Launch(Vector2.right);
            return unit;
        }

        private static void Tire(Unit unit)
        {
            Step(unit);
            Step(unit);
        }

        private static void Step(Unit unit) => Call(unit, "FixedUpdate");

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
