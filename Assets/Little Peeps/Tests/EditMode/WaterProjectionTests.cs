using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // How wide a structure's projection blocks are and what Show builds. The width is plain code; what Show builds
    // needs real renderers, so those tests run in Unity's Test Runner only.
    public class WaterProjectionTests
    {
        [Test]
        public void TheLightBlock_IsNarrowerAtBothSides_InArtPixels()
        {
            Assert.That(WaterProjection.LightWidth(2f, 2, 16f), Is.EqualTo(1.75f).Within(1e-5f));
            Assert.That(WaterProjection.LightWidth(1f, 0, 16f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TheLightBlock_NeverGoesNegative()
        {
            Assert.That(WaterProjection.LightWidth(0.25f, 8, 16f), Is.Zero);
        }

        // ---- what Show builds (Unity only) ----

        private GameObject root;
        private readonly List<Object> made = new();

        [SetUp]
        public void SetUp()
        {
            root = null;
            made.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var asset in made)
                if (asset != null) Object.DestroyImmediate(asset);
        }

        private Sprite HangingSprite()
        {
            var texture = new Texture2D(16, 16);
            made.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 1f), 16f);
            made.Add(sprite);
            return sprite;
        }

        private WaterProjectionLook Look() => new(HangingSprite(), HangingSprite(), null, 0, -4, -3);

        private SpriteRenderer Block(string blockName) => root.transform.Find(blockName).GetComponent<SpriteRenderer>();

        [Test]
        public void Show_HangsBothBlocksFromTheRoot_TheLightOneNarrowerAndOverTheDark()
        {
            root = new GameObject("House");
            var projection = root.AddComponent<WaterProjection>();

            projection.Show(Look(), 2f);

            var dark = Block("Water Projection Dark");
            var light = Block("Water Projection Light");
            Assert.That(dark.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(light.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(dark.transform.localScale.x, Is.EqualTo(2f).Within(1e-5f), "as wide as the footprint");
            Assert.That(light.transform.localScale.x, Is.EqualTo(1.75f).Within(1e-5f), "2 art pixels in at each side");
            Assert.That(light.sortingOrder, Is.GreaterThan(dark.sortingOrder));
        }

        [Test]
        public void WithoutArt_ThereIsNoProjection()
        {
            root = new GameObject("House");

            root.AddComponent<WaterProjection>().Show(default, 2f);

            Assert.That(root.transform.childCount, Is.Zero);
        }

        [Test]
        public void ItsBlocks_AreKnownForProjections_TheStructuresOwnRenderersAreNot()
        {
            root = new GameObject("House");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            var modelRenderer = model.AddComponent<SpriteRenderer>();
            root.AddComponent<WaterProjection>().Show(Look(), 2f);

            Assert.That(WaterProjection.IsProjection(Block("Water Projection Dark")), Is.True);
            Assert.That(WaterProjection.IsProjection(Block("Water Projection Light")), Is.True);
            Assert.That(WaterProjection.IsProjection(modelRenderer), Is.False);
        }
    }
}
