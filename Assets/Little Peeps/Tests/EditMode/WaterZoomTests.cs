using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Water2D;

namespace LittlePeeps.Tests
{
    // Render the actual history shader and read both height channels back. An analytic world-space
    // field makes camera-induced movement visible without depending on wave propagation or frame timing.
    public class WaterZoomTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject cameraObject;
        private GameObject waterObject;
        private Camera camera;
        private WaterSimulationAdvanced simulation;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Water zoom tests need a graphics device; run without -nographics.");

            cameraObject = new GameObject("Water zoom test camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(3f, -2f, -10f);
            waterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterObject.transform.localScale = new Vector3(30f, 30f, 1f);
            simulation = new WaterSimulationAdvanced();
            simulation.Setup(camera, 1f, new Vector2Int(64, 64), waterObject.GetComponent<Renderer>(), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (simulation != null)
            {
                foreach (string name in new[] { "CurrentState", "Temporary", "_waveShader", "_offsetShader" })
                {
                    var resource = typeof(WaterSimulationAdvanced).GetField(name, PrivateInstance).GetValue(simulation) as Object;
                    if (resource is RenderTexture rt) rt.Release();
                    if (resource != null) Object.DestroyImmediate(resource);
                }
            }
            if (waterObject != null) Object.DestroyImmediate(waterObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        }

        [TestCase(3f, 1f, 0f, 0f, TestName = "ZoomIn_PreservesWorldPositionAndBothHeights")]
        [TestCase(8f, 1f, 0f, 0f, TestName = "ZoomOut_PreservesWorldPositionAndBothHeights")]
        [TestCase(5f, 1f, 2f, -1f, TestName = "Pan_PreservesWorldPositionAndBothHeights")]
        [TestCase(3f, 1.6f, 2f, -1f, TestName = "ZoomPanAndAspectChange_PreserveWorldPositionAndBothHeights")]
        public void ViewChange_PreservesTheWorldField(float size, float aspect, float dx, float dy)
        {
            SeedWorldField();
            camera.orthographicSize = size;
            camera.aspect = aspect;
            camera.transform.position += new Vector3(dx, dy, 0f);
            Reproject();

            var pixels = ReadState();
            var rt = simulation.GetRT();
            foreach (int y in new[] { rt.height / 2 - 8, rt.height / 2 + 8 })
                foreach (int x in new[] { rt.width / 2 - 8, rt.width / 2 + 8 })
                {
                    Color expected = WorldField(WorldAtPixel(x, y));
                    Color actual = pixels[y * rt.width + x];
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.006f), "current height at the same world point");
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.006f), "previous height at the same world point");
                }

            Reproject();
            Assert.That(ReadState(), Is.EqualTo(pixels), "another fixed step with the same view must not move or blur history");
        }

        [Test]
        public void ZoomOut_NewlyExposedWaterDoesNotRepeatEdgeWaves()
        {
            SeedWorldField();
            camera.orthographicSize = 10f;
            Reproject();
            var pixels = ReadState();
            var rt = simulation.GetRT();
            foreach (int index in new[] { 0, rt.width - 1, pixels.Length - rt.width, pixels.Length - 1 })
            {
                Assert.That(pixels[index].r, Is.Zero);
                Assert.That(pixels[index].g, Is.Zero);
            }
        }

        [Test]
        public void Setup_StartsWithCalmWaterAtANonzeroCameraPosition()
        {
            Reproject();
            Assert.That(ReadState().All(c => c.r == 0f && c.g == 0f), Is.True);
        }

        [TestCase("Assets/ModernWater2D/Resources/Shaders/water/waterg.shadergraph")]
        [TestCase("Assets/ModernWater2D/Resources/Shaders/water/watergcheap.shadergraph")]
        public void WaterShader_CompilesWithAntialiasedHighlights(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                for (int pass = 0; pass < material.passCount; pass++)
                    material.SetPass(pass);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error");
                Assert.That(errors.Select(m => m.message), Is.Empty);
                Assert.That(shader.isSupported, Is.True);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                Object.DestroyImmediate(material);
            }
        }

        private void Reproject()
        {
            typeof(WaterSimulationAdvanced).GetMethod("RenderOffset", PrivateInstance).Invoke(simulation, null);
        }

        private Vector3 WorldAtPixel(int x, int y)
        {
            var rt = simulation.GetRT();
            // Advanced simulation covers the camera plus its 25% margin on every side.
            return camera.ViewportToWorldPoint(new Vector3(
                -0.25f + 1.5f * (x + 0.5f) / rt.width,
                -0.25f + 1.5f * (y + 0.5f) / rt.height, 0f));
        }

        private static Color WorldField(Vector3 world)
        {
            float height = -2f + world.x * 0.125f + world.y * 0.0625f;
            return new Color(height, height + 0.25f, 0f, 0f);
        }

        private void SeedWorldField()
        {
            Reproject();
            var rt = simulation.GetRT();
            var source = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[rt.width * rt.height];
            for (int y = 0; y < rt.height; y++)
                for (int x = 0; x < rt.width; x++)
                    pixels[y * rt.width + x] = WorldField(WorldAtPixel(x, y));
            source.SetPixels(pixels);
            source.Apply();
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = previous;
            Object.DestroyImmediate(source);
        }

        private Color[] ReadState()
        {
            var rt = simulation.GetRT();
            var previous = RenderTexture.active;
            var readback = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }
    }
}
