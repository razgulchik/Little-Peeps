using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The numbers the card paints. The card does it itself, so a hand-written description needs no colour
    // tags — which makes "what counts as a number" the rule an author relies on without reading code.
    //
    // Pure string work, so these run offline as well as in the Editor.
    public class PerkCardNumberTests
    {
        private static readonly Color Green = new Color32(0x68, 0xD5, 0x00, 0xFF);

        private static string Paint(string number) => "<color=#68D500>" + number + "</color>";

        [TestCase("x2 CREATURES IN THE WOODS", "x2", " CREATURES IN THE WOODS")]
        [TestCase("+50% COINS (ALPAKA)", "+50%", " COINS (ALPAKA)")]
        [TestCase("+0.5 SPEED", "+0.5", " SPEED")]
        [TestCase("-30% REGROW TIME", "-30%", " REGROW TIME")]
        [TestCase("+1 HOUSE SLOTS", "+1", " HOUSE SLOTS")]
        public void ALeadingNumberIsPaintedWhole(string text, string number, string rest)
        {
            Assert.That(PerkCardUI.HighlightNumbers(text, Green), Is.EqualTo(Paint(number) + rest));
        }

        // StatModifierText writes both buckets of one modifier as "+1, +10%", and a perk's lines one under
        // another — every number on the card is painted, not only the first.
        [Test]
        public void EveryNumberIsPainted()
        {
            Assert.That(PerkCardUI.HighlightNumbers("+1, +10% SPEED\n+2 STAMINA", Green),
                        Is.EqualTo(Paint("+1") + ", " + Paint("+10%") + " SPEED\n" + Paint("+2") + " STAMINA"));
        }

        [TestCase("UNLOCKS RICE PADDY")]
        [TestCase("AGE2 FORGE")]
        [TestCase("THE M6 HAMMER")]
        public void DigitsInsideAWordAreLeftAlone(string text)
        {
            Assert.That(PerkCardUI.HighlightNumbers(text, Green), Is.EqualTo(text));
        }

        // A hand-written description may carry its own tags; painting inside one would break it.
        [Test]
        public void ANumberInsideARichTextTagIsLeftAlone()
        {
            Assert.That(PerkCardUI.HighlightNumbers("<size=12>+5</size> WOOD", Green),
                        Is.EqualTo("<size=12>" + Paint("+5") + "</size> WOOD"));
        }

        [Test]
        public void NoTextGivesAnEmptyLabel()
        {
            Assert.That(PerkCardUI.HighlightNumbers(null, Green), Is.EqualTo(string.Empty));
            Assert.That(PerkCardUI.HighlightNumbers(string.Empty, Green), Is.EqualTo(string.Empty));
        }
    }

    // The window keeps its three-card size, so a short offer is centred in the room for three. Every
    // card must land on a whole pixel, whatever the parity of the leftover room.
    public class PerkCardLayoutTests
    {
        private const float Card = 86f;
        private const int Gap = 3;
        private const float RoomForThree = 3 * Card + 2 * Gap;

        [Test]
        public void ThreeCardsFillTheRoomFromTheTop()
        {
            Assert.That(PerkSelectionUI.CardTop(0, 3, RoomForThree, Card, Gap), Is.EqualTo(0f));
            Assert.That(PerkSelectionUI.CardTop(1, 3, RoomForThree, Card, Gap), Is.EqualTo(89f));
            Assert.That(PerkSelectionUI.CardTop(2, 3, RoomForThree, Card, Gap), Is.EqualTo(178f));
        }

        [Test]
        public void OneCardSitsInTheMiddle()
        {
            Assert.That(PerkSelectionUI.CardTop(0, 1, RoomForThree, Card, Gap), Is.EqualTo(89f));
        }

        // 264 − 175 leaves 89, an odd number: the half pixel is dropped rather than put under the cards.
        [Test]
        public void TwoCardsAreCentredOnWholePixels()
        {
            Assert.That(PerkSelectionUI.CardTop(0, 2, RoomForThree, Card, Gap), Is.EqualTo(44f));
            Assert.That(PerkSelectionUI.CardTop(1, 2, RoomForThree, Card, Gap), Is.EqualTo(133f));
        }
    }

    // Which resource one modifier names, for the card's icon row. Asked through the same StatMeta mask the
    // runtime asks, so a scope RunStats throws away names nothing here either.
    //
    // Pure — the modifiers here name no source asset — so these run offline too.
    public class PerkResourceOfModifierTests
    {
        [Test]
        public void AYieldNamesItsResource()
        {
            var yield = new StatModifier { id = StatId.ResourceYield, resourceScope = ResourceType.Wood };

            Assert.That(StatPerkDef.TryGetResource(yield, out ResourceType resource), Is.True);
            Assert.That(resource, Is.EqualTo(ResourceType.Wood));
        }

        // resourceScope defaults to Food on every modifier; a stat that never reads it must not show Food.
        [TestCase(StatId.UnitStamina)]
        [TestCase(StatId.UnitSpeed)]
        [TestCase(StatId.HouseCapacity)]
        [TestCase(StatId.TapRadius)]
        [TestCase(StatId.StructureLimit)]
        public void AStatWithoutAResourceNamesNone(StatId id)
        {
            var modifier = new StatModifier { id = id, resourceScope = ResourceType.Food };

            Assert.That(StatPerkDef.TryGetResource(modifier, out _), Is.False);
        }

        // An empty source means EVERY source — there is no one resource to name.
        [Test]
        public void ASourceStatWithNoSourceNamesNone()
        {
            var modifier = new StatModifier { id = StatId.SourceRespawn };

            Assert.That(StatPerkDef.TryGetResource(modifier, out _), Is.False);
        }
    }

    // Levels, the card text and the resource row on real perk assets. ScriptableObject.CreateInstance
    // needs the Editor, so these run in Unity's Test Runner only.
    public class PerkDataTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            created.Add(made);
            return made;
        }

        private StatPerkDef Perk(params StatModifier[] modifiers)
        {
            var perk = Make<StatPerkDef>();
            perk.modifiers = new List<LevelledModifier>();
            for (int i = 0; i < modifiers.Length; i++) perk.modifiers.Add(OneLevel(modifiers[i]));
            return perk;
        }

        // A one-level bonus: the modifier's value as its only step.
        private static LevelledModifier OneLevel(StatModifier m) => new LevelledModifier
        {
            id = m.id,
            unitScope = m.unitScope,
            anyProfession = m.anyProfession,
            resourceScope = m.resourceScope,
            sourceScope = m.sourceScope,
            structureScope = m.structureScope,
            kind = m.kind,
            steps = new List<float> { m.value },
        };

        private static StatModifier Yield(ResourceType resource) =>
            new StatModifier { id = StatId.ResourceYield, resourceScope = resource, kind = ModifierKind.Percent, value = 0.5f };

        [Test]
        public void AStatPerkCanBeTakenAsOftenAsItHasLevels()
        {
            var perk = Perk();
            Assert.That(perk.MaxLevel, Is.EqualTo(1));

            perk.levels = 3;
            Assert.That(perk.MaxLevel, Is.EqualTo(3));

            // A zero typed past the inspector's [Min] still leaves one level, never a perk nobody can take.
            perk.levels = 0;
            Assert.That(perk.MaxLevel, Is.EqualTo(1));
        }

        [Test]
        public void ACodePerkHasOneLevel()
        {
            Assert.That(Make<UnlockStructurePerkDef>().MaxLevel, Is.EqualTo(1));
        }

        [Test]
        public void TheCardOfALevel_ShowsItsStepAndTheTotal()
        {
            var perk = Perk();
            perk.levels = 3;
            perk.modifiers.Add(new LevelledModifier
            {
                id = StatId.TapRadius, kind = ModifierKind.Percent, steps = new List<float> { 0.1f, 0.1f, 0.2f },
            });

            Assert.That(perk.DescriptionAt(1), Is.EqualTo("+10% TAP RADIUS"));
            Assert.That(perk.DescriptionAt(3), Is.EqualTo("+20% TAP RADIUS [TOTAL +40%]"));

            // A hand-worded card is the same on every level — the reason a levelled perk is left to its data.
            perk.description = "BIGGER TAPS";
            Assert.That(perk.DescriptionAt(3), Is.EqualTo("BIGGER TAPS"));
        }

        [Test]
        public void ResourcesComeOnceEach_InResourceTypeOrder()
        {
            var perk = Perk(Yield(ResourceType.Coins), Yield(ResourceType.Wood), Yield(ResourceType.Coins));
            var resources = new List<ResourceType> { ResourceType.Metal };   // stale, must be cleared

            perk.GetResources(resources);

            Assert.That(resources, Is.EqualTo(new[] { ResourceType.Wood, ResourceType.Coins }));
        }

        // Plentiful harvest: wheat regrows faster, and wheat is Food.
        [Test]
        public void ASourceStatNamesTheResourceOfItsSource()
        {
            var wheat = Make<ResourceSourceDef>();
            wheat.resource = ResourceType.Food;
            var perk = Perk(new StatModifier { id = StatId.SourceRespawn, sourceScope = wheat, kind = ModifierKind.Percent, value = -0.3f });

            var resources = new List<ResourceType>();
            perk.GetResources(resources);

            Assert.That(resources, Is.EqualTo(new[] { ResourceType.Food }));
        }

        [Test]
        public void APerkWithoutResourcesShowsAnEmptyRow()
        {
            var perk = Perk(new StatModifier { id = StatId.UnitStamina, kind = ModifierKind.Flat, value = 1f });

            var resources = new List<ResourceType>();
            perk.GetResources(resources);

            Assert.That(resources, Is.Empty);
        }

        // Unl Rice Paddy: the building pays Food, read from the ResourceSource on its prefab root.
        [Test]
        public void AnUnlockPerkNamesWhatTheBuildingPays()
        {
            var paddy = Make<ResourceSourceDef>();
            paddy.resource = ResourceType.Food;

            var prefab = new GameObject("RicePaddy");
            created.Add(prefab);
            var so = new SerializedObject(prefab.AddComponent<ResourceSource>());
            so.FindProperty("def").objectReferenceValue = paddy;
            so.ApplyModifiedPropertiesWithoutUndo();

            var structure = Make<StructureDef>();
            structure.prefab = prefab;
            var perk = Make<UnlockStructurePerkDef>();
            perk.structure = structure;

            var resources = new List<ResourceType>();
            perk.GetResources(resources);

            Assert.That(resources, Is.EqualTo(new[] { ResourceType.Food }));
        }
    }
}
