using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps.Tests
{
    // The hit feedback's table, cell by cell: what CollisionTarget reports for a hit — Useful (it worked),
    // Blocked (the target is for this unit, but not now or not here), Useless (nothing on it is for this
    // unit). Real participants (house, tavern, payers, rack, and the forge, mill, market and paddy gates) on
    // a real CollisionTarget. A hit goes in through the trigger callback, the way a unit crossing a field
    // arrives: a Collision2D can't be built by hand, and the verdict doesn't depend on the way in. Edit Mode
    // runs no Awake or OnEnable, so Build calls them once everything is on the object, as a spawn would.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class HitVerdictTests
    {
        private const BindingFlags AnyInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly UnitType[] Everyone = (UnitType[])Enum.GetValues(typeof(UnitType));

        private readonly List<Object> made = new();
        private ResourceSystem resources;
        private SpawnSystem spawns;
        private UnitDef unitDef;

        [SetUp]
        public void SetUp()
        {
            resources = Make("ResourceSystem").AddComponent<ResourceSystem>();
            SetField(resources, "logChanges", false);
            resources.Initialize(new RunContext());

            // No UnitPool: a house warms up with its slots empty and never spawns anyone of its own.
            spawns = Make("SpawnSystem").AddComponent<SpawnSystem>();
            spawns.Initialize(new RunContext());

            unitDef = Keep(ScriptableObject.CreateInstance<UnitDef>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in made) if (obj != null) Object.DestroyImmediate(obj);
            made.Clear();
        }

        // --- house ----------------------------------------------------------------------------------

        [Test]
        public void House_TiredUnit_GoesIn_Useful() => Expect(HitVerdict.Useful, MakeHouse(1), Tired(UnitType.Farmer));

        [Test]
        public void House_DrunkWorker_GoesIn_Useful() => Expect(HitVerdict.Useful, MakeHouse(1), Drunk(UnitType.Farmer));

        [Test]
        public void House_Full_TiredUnit_Blocked()
        {
            var house = MakeHouse(1);
            Expect(HitVerdict.Useful, house, Tired(UnitType.Farmer));   // takes the only slot

            Expect(HitVerdict.Blocked, house, Tired(UnitType.Miner));
        }

        [Test]
        public void House_Full_DrunkWorker_Blocked()
        {
            var house = MakeHouse(1);
            Expect(HitVerdict.Useful, house, Tired(UnitType.Farmer));

            Expect(HitVerdict.Blocked, house, Drunk(UnitType.Miner));
        }

        [Test]
        public void House_WorkingSoberUnit_Useless() => Expect(HitVerdict.Useless, MakeHouse(1), Worker(UnitType.Farmer));

        // --- tavern ---------------------------------------------------------------------------------

        [Test]
        public void Tavern_SoberWorker_GoesIn_Useful() => Expect(HitVerdict.Useful, MakeTavern(2, Everyone), Worker(UnitType.Farmer));

        // The service exception: a tired worker may not work, but it is served and pays.
        [Test]
        public void Tavern_TiredSoberWorker_GoesIn_Useful() => Expect(HitVerdict.Useful, MakeTavern(2, Everyone), Tired(UnitType.Farmer));

        [Test]
        public void Tavern_DrunkWorker_Blocked() => Expect(HitVerdict.Blocked, MakeTavern(2, Everyone), Drunk(UnitType.Farmer));

        [Test]
        public void Tavern_Full_Blocked()
        {
            var tavern = MakeTavern(1, Everyone);
            Expect(HitVerdict.Useful, tavern, Worker(UnitType.Farmer));   // takes the only seat

            Expect(HitVerdict.Blocked, tavern, Worker(UnitType.Miner));
        }

        [Test]
        public void Tavern_UnlistedWorker_Useless() => Expect(HitVerdict.Useless, MakeTavern(2, UnitType.Farmer), Worker(UnitType.Miner));

        // --- forge ----------------------------------------------------------------------------------

        [Test]
        public void Forge_Smith_Useful() => Expect(HitVerdict.Useful, MakeForge(out _), Worker(UnitType.Blacksmith));

        [Test]
        public void Forge_TiredSmith_Blocked() => Expect(HitVerdict.Blocked, MakeForge(out _), Tired(UnitType.Blacksmith));

        [Test]
        public void Forge_Overheated_SmithIsBlocked()
        {
            var forge = MakeForge(out var heat);
            Overheat(forge, heat);

            Expect(HitVerdict.Blocked, forge, Worker(UnitType.Blacksmith));
        }

        // The heat gate refuses everyone while overheated, a lumberjack too — but the forge was never for it.
        // Whom a target is for is asked of the payer, never read off a gate.
        [Test]
        public void Forge_Overheated_LumberjackIsUseless()
        {
            var forge = MakeForge(out var heat);
            Overheat(forge, heat);

            Expect(HitVerdict.Useless, forge, Worker(UnitType.Lumberjack));
        }

        // --- mill -----------------------------------------------------------------------------------

        [Test]
        public void Mill_WithSacks_Useful()
        {
            var mill = MakeMill(out var store);
            SetField(store, "stored", 2);

            Expect(HitVerdict.Useful, mill, Worker(UnitType.Farmer));
        }

        [Test]
        public void Mill_Empty_Blocked() => Expect(HitVerdict.Blocked, MakeMill(out _), Worker(UnitType.Farmer));

        [Test]
        public void Mill_TiredWorker_Blocked()
        {
            var mill = MakeMill(out var store);
            SetField(store, "stored", 2);

            Expect(HitVerdict.Blocked, mill, Tired(UnitType.Farmer));
        }

        // --- market ---------------------------------------------------------------------------------

        [Test]
        public void Market_InsideWithAVisitLeft_Useful()
        {
            var market = MakeMarket(out var passage);
            var trader = Worker(UnitType.Farmer);
            Enter(passage, trader);

            Expect(HitVerdict.Useful, market, trader);
        }

        [Test]
        public void Market_InsideOnceTheVisitIsSpent_Blocked()
        {
            var market = MakeMarket(out var passage);
            var trader = Worker(UnitType.Farmer);
            Enter(passage, trader);

            // However many hits a visit is worth: spend them all.
            int paid = 0;
            while (Hit(market, trader).Verdict == HitVerdict.Useful)
                Assert.That(++paid, Is.LessThan(100), "a visit has to run out");

            Assert.That(paid, Is.GreaterThan(0), "sanity: the visit paid before it ran out");
            Expect(HitVerdict.Blocked, market, trader);
        }

        // Blocked, not Useless: the market is for everyone, so a hit from outside is one that would have paid
        // from inside — the hint to go in.
        [Test]
        public void Market_FromOutside_Blocked() => Expect(HitVerdict.Blocked, MakeMarket(out _), Worker(UnitType.Farmer));

        [Test]
        public void Market_TiredWorkerFromOutside_Blocked() => Expect(HitVerdict.Blocked, MakeMarket(out _), Tired(UnitType.Farmer));

        // --- trees ----------------------------------------------------------------------------------

        [Test]
        public void Tree_OneHit_IsUsefulAndFinal_ReportedOnceTheSourceSettled()
        {
            var tree = MakeTree(1);
            var source = tree.GetComponent<ResourceSource>();
            bool readyWhenReported = true;
            tree.HitResolved += _ => readyWhenReported = source.IsReady;

            var report = Hit(tree, Worker(UnitType.Lumberjack));

            Assert.That(report.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(report.Final, Is.True, "a one-hit tree's only hit is its last");
            Assert.That(readyWhenReported, Is.False, "reported after the source settled the hit");
        }

        [Test]
        public void Birch_FirstHitIsNotFinal_SecondIs()
        {
            var birch = MakeTree(2);
            var lumberjack = Worker(UnitType.Lumberjack);

            var first = Hit(birch, lumberjack);
            var second = Hit(birch, lumberjack);

            Assert.That(first.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(first.Final, Is.False);
            Assert.That(second.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(second.Final, Is.True);
        }

        [Test]
        public void Tree_Unassigned_Useful() => Expect(HitVerdict.Useful, MakeTree(1), Worker(UnitType.Unassigned));

        [Test]
        public void Tree_TiredLumberjack_Blocked() => Expect(HitVerdict.Blocked, MakeTree(1), Tired(UnitType.Lumberjack));

        [Test]
        public void Tree_Farmer_Useless() => Expect(HitVerdict.Useless, MakeTree(1), Worker(UnitType.Farmer));

        // --- walk-through ---------------------------------------------------------------------------

        [Test]
        public void Wheat_Farmer_IsUsefulAndFinal_AsAPass()
        {
            var report = Hit(MakeWheat(), Worker(UnitType.Farmer));

            Assert.That(report.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(report.Final, Is.True);
            Assert.That(report.Contact, Is.EqualTo(HitContact.Pass));
        }

        [Test]
        public void Wheat_TiredFarmer_Blocked() => Expect(HitVerdict.Blocked, MakeWheat(), Tired(UnitType.Farmer));

        [Test]
        public void Wheat_OtherProfession_Useless() => Expect(HitVerdict.Useless, MakeWheat(), Worker(UnitType.Lumberjack));

        [Test]
        public void Paddy_Ripe_FarmerIsUsefulAndFinal()
        {
            var paddy = MakePaddy(out var crop);
            Ripen(crop);

            var report = Hit(paddy, Worker(UnitType.Farmer));

            Assert.That(report.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(report.Final, Is.True);
        }

        [Test]
        public void Paddy_Growing_FarmerIsBlocked() => Expect(HitVerdict.Blocked, MakePaddy(out _), Worker(UnitType.Farmer));

        [Test]
        public void Paddy_Ripe_UnlistedUnitIsUseless()
        {
            var paddy = MakePaddy(out var crop);
            Ripen(crop);

            Expect(HitVerdict.Useless, paddy, Worker(UnitType.Swordsman));
        }

        [Test]
        public void Rack_UnassignedTakesTheTool_Useful() => Expect(HitVerdict.Useful, MakeRack(), Worker(UnitType.Unassigned));

        [Test]
        public void Rack_Empty_UnassignedIsBlocked()
        {
            var rack = MakeRack();
            Expect(HitVerdict.Useful, rack, Worker(UnitType.Unassigned));   // takes the one tool

            Expect(HitVerdict.Blocked, rack, Worker(UnitType.Unassigned));
        }

        [Test]
        public void Rack_TiredUnassigned_Blocked() => Expect(HitVerdict.Blocked, MakeRack(), Tired(UnitType.Unassigned));

        [Test]
        public void Rack_EquippedWorker_Useless() => Expect(HitVerdict.Useless, MakeRack(), Worker(UnitType.Farmer));

        // --- the rest -------------------------------------------------------------------------------

        [Test]
        public void Fence_IsUselessToEveryone()
        {
            var fence = MakeFence();

            Expect(HitVerdict.Useless, fence, Worker(UnitType.Farmer));
            Expect(HitVerdict.Useless, fence, Tired(UnitType.Farmer));
            Expect(HitVerdict.Useless, fence, Drunk(UnitType.Farmer));
        }

        // No door and no tired check for an arrow (CollisionTarget.HitFromRange), but the same rule for its
        // verdict — reported as Ranged.
        [Test]
        public void Arrow_IsJudgedTheSameWay_AsRanged()
        {
            var boar = Build(Source(Def("Boar", Depletion.Regrow, 3, UnitType.Unassigned, UnitType.Hunter)));
            var hunter = Worker(UnitType.Hunter);

            var report = Single(boar, () => boar.HitFromRange(hunter));

            Assert.That(report.Verdict, Is.EqualTo(HitVerdict.Useful));
            Assert.That(report.Final, Is.False);
            Assert.That(report.Contact, Is.EqualTo(HitContact.Ranged));
        }

        // --- hitting --------------------------------------------------------------------------------

        private static void Expect(HitVerdict verdict, CollisionTarget target, Unit unit)
            => Assert.That(Hit(target, unit).Verdict, Is.EqualTo(verdict), $"{unit.name} on {target.name}");

        // One hit, the way a unit crossing the target arrives — and the one report it has to end in.
        private static HitReport Hit(CollisionTarget target, Unit unit)
        {
            var report = Single(target, () => Invoke(target, "OnTriggerEnter2D", unit.GetComponent<Collider2D>()));
            Assert.That(report.Unit, Is.SameAs(unit));
            return report;
        }

        private static HitReport Single(CollisionTarget target, Action hit)
        {
            var reports = new List<HitReport>();
            void Record(HitReport report) => reports.Add(report);

            target.HitResolved += Record;
            try { hit(); }
            finally { target.HitResolved -= Record; }

            Assert.That(reports, Has.Count.EqualTo(1), "a hit ends in exactly one report");
            return reports[0];
        }

        // A smith hammers it until it trips, however many hits the tuning takes.
        private void Overheat(CollisionTarget forge, ForgeHeat heat)
        {
            var smith = Worker(UnitType.Blacksmith);
            for (int i = 0; i < 1000 && !heat.IsOverheated; i++) Hit(forge, smith);
            Assert.That(heat.IsOverheated, Is.True, "sanity: the forge overheats");
        }

        // The unit walks into the passage — what the zone's own trigger hears.
        private static void Enter(VisitZone passage, Unit unit)
            => Invoke(passage, "OnTriggerEnter2D", unit.GetComponent<Collider2D>());

        // Grown the whole grow time, however long that is.
        private static void Ripen(RicePaddy crop) => SetField(crop, "grown", float.MaxValue);

        // --- units ----------------------------------------------------------------------------------

        // Out on the field with stamina: a house launched it.
        private Unit Worker(UnitType type)
        {
            var unit = MakeUnit(type);
            unit.Launch(Vector2.right);
            return unit;
        }

        // Never launched, so no stamina: tired, on its way home.
        private Unit Tired(UnitType type) => MakeUnit(type);

        // Working, and drunk from the tavern.
        private Unit Drunk(UnitType type)
        {
            var unit = Worker(type);
            unit.MakeDrunk(10f, 0.6f);
            return unit;
        }

        private Unit MakeUnit(UnitType type)
        {
            var go = Make($"Unit({type})");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            var unit = go.AddComponent<Unit>();
            unit.def = unitDef;
            Wake(unit);

            // What a rack does, minus the rack. Unassigned is no profession at all.
            if (type != UnitType.Unassigned) unit.Equip(Profession(type), null);
            return unit;
        }

        private ProfessionDef Profession(UnitType type)
        {
            var profession = Keep(ScriptableObject.CreateInstance<ProfessionDef>());
            profession.type = type;
            return profession;
        }

        // --- targets --------------------------------------------------------------------------------

        private CollisionTarget MakeHouse(int capacity)
        {
            var go = Make("House");
            var house = go.AddComponent<Spawner>();   // [RequireComponent] pulls in the Structure
            house.unitDef = unitDef;
            house.capacity = capacity;
            house.Initialize(spawns, null, null);
            house.Warmup();
            return Build(go);
        }

        private CollisionTarget MakeTavern(int capacity, params UnitType[] served)
        {
            var go = Make("Tavern");
            var tavern = go.AddComponent<Tavern>();   // [RequireComponent] pulls in the Structure
            SetField(tavern, "capacity", capacity);
            SetField(tavern, "coinSource", Def("Coins", Depletion.Never, 0, served));
            // Grid context is all the door asks for; nobody is ever let back out here.
            tavern.Initialize(null, new IslandGrid(1f),
                              new StructureInstance { Def = Keep(ScriptableObject.CreateInstance<StructureDef>()) });
            return Build(go);
        }

        private CollisionTarget MakeTree(int hits)
            => Build(Source(Def("Tree", Depletion.Regrow, hits, UnitType.Unassigned, UnitType.Lumberjack)));

        private CollisionTarget MakeWheat()
            => Build(Source(Def("Wheat", Depletion.Regrow, 1, UnitType.Farmer), trigger: true));

        private CollisionTarget MakeForge(out ForgeHeat heat)
        {
            var go = Source(Def("Forge", Depletion.Never, 0, UnitType.Unassigned, UnitType.Blacksmith));
            heat = go.AddComponent<ForgeHeat>();
            return Build(go);
        }

        private CollisionTarget MakeMill(out Mill store)
        {
            var go = Source(Def("Mill", Depletion.Never, 0, Everyone));
            store = go.AddComponent<Mill>();
            return Build(go);
        }

        // The market: the payer on the root, the passage a trigger child with a body of its own (VisitZone).
        private CollisionTarget MakeMarket(out VisitZone passage)
        {
            var go = Source(Def("Market", Depletion.Never, 0, Everyone));
            var passageGo = Make("Passage");
            passageGo.transform.SetParent(go.transform, false);
            passageGo.AddComponent<BoxCollider2D>().isTrigger = true;
            passageGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            passage = passageGo.AddComponent<VisitZone>();
            return Build(go);
        }

        // Listed like the real paddy: everyone but the military.
        private CollisionTarget MakePaddy(out RicePaddy crop)
        {
            var def = Def("RicePaddy", Depletion.Regrow, 1, UnitType.Unassigned, UnitType.Farmer, UnitType.Lumberjack,
                          UnitType.Hunter, UnitType.Miner, UnitType.Blacksmith);
            def.keepBodyWhileDepleted = true;
            var go = Source(def, trigger: true);
            crop = go.AddComponent<RicePaddy>();
            return Build(go);
        }

        private CollisionTarget MakeRack()
        {
            var go = Make("Rack");
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            var rack = go.AddComponent<ToolRack>();   // [RequireComponent] pulls in the CollisionTarget
            SetField(rack, "profession", Profession(UnitType.Lumberjack));
            return Build(go);
        }

        private CollisionTarget MakeFence()
        {
            var go = Make("Fence");
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<Structure>();
            return Build(go);
        }

        // A payer on its own object, not woken yet — the add-ons go on first. `trigger` = walk-through.
        private GameObject Source(ResourceSourceDef def, bool trigger = false)
        {
            var go = Make(def.name);
            go.AddComponent<BoxCollider2D>().isTrigger = trigger;
            var source = go.AddComponent<ResourceSource>();   // [RequireComponent] pulls in the CollisionTarget
            SetField(source, "def", def);
            source.Initialize(resources);
            return go;
        }

        private ResourceSourceDef Def(string name, Depletion depletion, int hits, params UnitType[] workers)
        {
            var def = Keep(ScriptableObject.CreateInstance<ResourceSourceDef>());
            def.name = name;
            def.depletion = depletion;
            def.hitsToDeplete = hits;
            def.workerYields = new WorkerYield[workers.Length];
            for (int i = 0; i < workers.Length; i++)
                def.workerYields[i] = new WorkerYield { worker = workers[i], amount = 1f };
            return def;
        }

        // Everything is on the object: wake it the way a spawn would, children included.
        private static CollisionTarget Build(GameObject go)
        {
            foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true)) Wake(component);
            return go.GetComponent<CollisionTarget>();
        }

        // --- plumbing -------------------------------------------------------------------------------

        private GameObject Make(string name) => Keep(new GameObject(name));

        private T Keep<T>(T obj) where T : Object
        {
            made.Add(obj);
            return obj;
        }

        // What Unity calls on spawn and Edit Mode doesn't: Awake, then OnEnable, where the component has them.
        private static void Wake(Component component)
        {
            Find(component.GetType(), "Awake")?.Invoke(component, null);
            Find(component.GetType(), "OnEnable")?.Invoke(component, null);
        }

        private static void Invoke(object target, string method, params object[] args)
            => (Find(target.GetType(), method) ?? throw new MissingMethodException(target.GetType().Name, method))
               .Invoke(target, args);

        private static void SetField(object target, string field, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, AnyInstance);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        private static MethodInfo Find(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, AnyInstance);
                if (method != null) return method;
            }
            return null;
        }
    }
}
