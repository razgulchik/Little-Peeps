using LeafUtils;
using UnityEngine;
using UnityEngine.Profiling;

namespace Water2D
{
    public class WaterSimulationAdvanced : WaterSimulation
    {
        private Material _waveShader;
        private Material waveShader
        {
            get { if (_waveShader == null) _waveShader = new Material(Shader.Find("Water2D/Simulations/process")); return _waveShader; }
            set { _waveShader = value; }
        }

        private Material _offsetShader;
        private Material offsetShader
        {
            get { if (_offsetShader == null) _offsetShader = new Material(Shader.Find("Water2D/Simulations/offset")); return _offsetShader; }
            set { _offsetShader = value; }
        }

        [SerializeField] private RenderTexture CurrentState, Temporary;
        [SerializeField] private RenderTexture ObstructionTex;
        [SerializeField] private Vector4 ObstructionTexPos;
        [SerializeField] private Vector2Int resolution;
        [SerializeField] private Renderer sr;
        [SerializeField] private float waveRad = 0.005f;
        [SerializeField] private float waveHeight = 1f;
        [SerializeField] private float dispersion = 0.98f;
        [SerializeField] private float simSpeed = 1f;
        [SerializeField] private int iterations = 3;
        [SerializeField] private int diffusionSize = 3;

        [SerializeField] private float rainSpeed = 1f;
        [SerializeField] private float rainWaveH = 1f;
        [SerializeField] private int rainSizeX = 1;
        [SerializeField] private int rainSizeY = 1;
        [SerializeField] private bool enableRain = false;

        // The world rectangle represented by BOTH height samples in CurrentState. Track it per
        // simulation step, not per Update: there may be zero or several fixed steps in a frame.
        private Vector4 stateBounds;
        private bool hasStateBounds;

        const float defaultMarginMlp = 1.5f;
        const float stableMarginMlp = 3f;
        [SerializeField] private bool moreStableObstructionSimulation = false;

        float marginMlp
        {
            get { return moreStableObstructionSimulation ? stableMarginMlp : defaultMarginMlp; }
        }

        [SerializeField][HideInInspector] Camera _mainCam;
        Camera mainCam
        {
            set { _mainCam = value; }
            get { if (_mainCam == null) _mainCam = ObstructorManager.instance.cam; return _mainCam; }
        }

        public void Setup(Camera mainCam, float simSpeed, Vector2Int resolution, Renderer sr, RenderTexture obstruction, float rainSpeed = 1, int rainSizeX = 1, int rainSizeY = 1, float rainWaveH = 1, float waveRad = 0.005f, float waveHeight = 1f, float dispersion = 0.98f, int iterations = 3, bool enableRain = false, bool moreStableObstructionSimulation = false)
        {
            this.moreStableObstructionSimulation = moreStableObstructionSimulation;
            this.resolution = new Vector2Int((int)(marginMlp * resolution.x), (int)(marginMlp * resolution.x * (1f / (sr.bounds.size.x / sr.bounds.size.y))));
            this.mainCam = mainCam;
            this.sr = sr;
            this.ObstructionTex = obstruction;
            this.waveRad = waveRad;
            this.waveHeight = waveHeight;
            this.dispersion = dispersion;
            this.iterations = iterations;
            this.rainSpeed = rainSpeed;
            this.rainWaveH = rainWaveH;
            this.rainSizeX = rainSizeX;
            this.rainSizeY = rainSizeY;
            this.enableRain = enableRain;
            this.simSpeed = simSpeed;
            Init();
        }



        public override RenderTexture GetRT()
        {
            return CurrentState;
        }

        void InitTex(out RenderTexture rt)
        {
            if (resolution.x == 0 || resolution.y == 0) resolution = new Vector2Int(1024, (int)(1f / (sr.bounds.size.x / sr.bounds.size.y) * 1024f));
            rt = new RenderTexture(resolution.x, resolution.y, 1, RenderTextureFormat.RGHalf);
            rt.enableRandomWrite = false;
            rt.depth = 24;
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        void Init()
        {
            if (CurrentState != null) CurrentState.Release();
            if (Temporary != null) Temporary.Release();

            InitTex(out CurrentState);
            InitTex(out Temporary);
            hasStateBounds = false;
        }

        void CreateIfNull()
        {
            if (CurrentState == null || !CurrentState.IsCreated() || Temporary == null || !Temporary.IsCreated())
                Init();
        }

        Vector4 GetObstructionPositions()
        {
            ObstructorManager manager = ObstructorManager.instance != null ? ObstructorManager.instance : ObstructorManager.GetInstance();
            Camera obstructorCam = manager != null ? manager.cam : null;
            Camera cam = obstructorCam != null ? obstructorCam : GetCameraRenderingScreen();
            if (cam != null) mainCam = cam;

            if (cam == null)
                return new Vector4(0f, 0f, 1f, 1f);

            if (!cam.orthographic)
            {
                if (manager != null && manager.layerRenderer != null)
                    return manager.layerRenderer.GetScreenSourceRect();

                return new Vector4(0f, 0f, 1f, 1f);
            }

            Vector3 v1 = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 v2 = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
            return new Vector4(v1.x, v1.y, v2.x, v2.y);
        }


        private Camera GetCameraRenderingScreen()
        {
            if (mainCam == null)
            {
                ObstructorManager manager = ObstructorManager.instance != null ? ObstructorManager.instance : ObstructorManager.GetInstance();
                if (manager != null) mainCam = manager.cam;
            }
            return mainCam;
        }

        Vector4 GetSimulatedTexturePositions()
        {
            Camera cam = GetCameraRenderingScreen();
            if (cam == null)
                return new Vector4(0f, 0f, 1f, 1f);

            float margin = (marginMlp - 1f) * 0.5f;

            if (!cam.orthographic)
                return new Vector4(-margin, -margin, 1f + margin, 1f + margin);

            Vector3 v1 = cam.ViewportToWorldPoint(new Vector3(-margin, -margin, 0f));
            Vector3 v2 = cam.ViewportToWorldPoint(new Vector3(1f + margin, 1f + margin, 0f));
            return new Vector4(v1.x, v1.y, v2.x, v2.y);
        }

        Vector4 GetCameraPositions()
        {
            Vector2 v1 = GetCameraRenderingScreen().ViewportToWorldPoint(new Vector3(0f, 0f, -10f));
            Vector2 v2 = GetCameraRenderingScreen().ViewportToWorldPoint(new Vector3(1f, 1f, -10f));
            return new Vector4(v1.x, v1.y, v2.x, v2.y);
        }

        Vector4 GetFullTexturePositions()
        {
            Camera cam = GetCameraRenderingScreen();
            if (cam != null && !cam.orthographic && sr != null)
                return GetViewportBounds(cam, sr.bounds);

            Vector2 v1 = sr.bounds.min;
            Vector2 v2 = sr.bounds.max;
            return new Vector4(v1.x, v1.y, v2.x, v2.y);
        }

        static Vector4 GetViewportBounds(Camera cam, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] points =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            Vector3 v = cam.WorldToViewportPoint(points[0]);
            float minX = v.x;
            float maxX = v.x;
            float minY = v.y;
            float maxY = v.y;

            for (int i = 1; i < points.Length; i++)
            {
                v = cam.WorldToViewportPoint(points[i]);
                minX = Mathf.Min(minX, v.x);
                maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }

            return new Vector4(minX, minY, maxX, maxY);
        }


        void Render()
        {
            //blit here
            RenderUtils.RenderToRT2D(waveShader, Temporary);
            Graphics.CopyTexture(Temporary, CurrentState);
        }

        void RenderOffset()
        {
            Vector4 bounds = GetSimulatedTexturePositions();
            if (!hasStateBounds)
            {
                stateBounds = bounds;
                hasStateBounds = true;
                return;
            }
            if (bounds == stateBounds) return;

            // For each pixel in the new view, sample the same world position in the old view.
            // Translation alone handles a pan, but zoom/aspect changes also need a UV scale.
            float oldWidth = stateBounds.z - stateBounds.x;
            float oldHeight = stateBounds.w - stateBounds.y;
            offsetShader.SetTexture("_tex", CurrentState);
            offsetShader.SetVector("_HistoryTransform", new Vector4(
                (bounds.z - bounds.x) / oldWidth,
                (bounds.w - bounds.y) / oldHeight,
                (bounds.x - stateBounds.x) / oldWidth,
                (bounds.y - stateBounds.y) / oldHeight));
            RenderUtils.RenderToRT2D(offsetShader, Temporary);
            Graphics.CopyTexture(Temporary, CurrentState);
            stateBounds = bounds;
        }

        public Vector4 FutureSight()
        {
            return GetObstructionPositions();
        }

        public override void Loop()
        {
            ObstructionTex = ObstructorManager.instance.layerRenderer.LayerTexture();
            // The obstruction camera is finalized in LateUpdate (after Cinemachine). An Update
            // snapshot can still describe the previous zoom, while the texture already uses the new one.
            ObstructionTexPos = GetObstructionPositions();
            CreateIfNull();
            RenderOffset();
            waveShader.SetTexture("_ObstructionTex", ObstructionTex);
            waveShader.SetVector("OstructionTexPos", ObstructionTexPos);

            waveShader.SetVector("OstructionTexRes", new Vector4(ObstructionTex.width, ObstructionTex.height));
            waveShader.SetVector("texPos", stateBounds);

            waveShader.SetTexture("_NState", CurrentState);
            waveShader.SetVector("resolution", new Vector4(resolution.x, resolution.y));
            waveShader.SetFloat("waveRad", waveRad);
            waveShader.SetFloat("dispersion", dispersion);
            waveShader.SetFloat("waveHeight", waveHeight);
            waveShader.SetFloat("diffusionSize", diffusionSize);
            waveShader.SetFloat("enableRain", enableRain ? 1 : 0);
            waveShader.SetFloat("rainSpeed", rainSpeed);
            waveShader.SetFloat("rainWaveH", rainWaveH);
            waveShader.SetFloat("rainSizeX", rainSizeX);
            waveShader.SetFloat("rainSizeY", rainSizeY);

            waveShader.SetFloat("timeFromStart", Time.timeSinceLevelLoad);

            var cS = stateBounds;
            var cF = GetFullTexturePositions();
            float lx = Mathf.LerpUnclamped(0f, 1f, (cS.x - cF.x) / (cF.z - cF.x));
            float rx = Mathf.LerpUnclamped(0f, 1f, (cS.z - cF.x) / (cF.z - cF.x));
            float by = Mathf.LerpUnclamped(0f, 1f, (cS.y - cF.y) / (cF.w - cF.y));
            float ty = Mathf.LerpUnclamped(0f, 1f, (cS.w - cF.y) / (cF.w - cF.y));

            sr.sharedMaterial.SetVector("_simUvs", new Vector4(lx, by, rx, ty));

            Profiler.BeginSample("Rendering Water Simulation");
            for (int i = 0; i < iterations; i++) Render();
            Profiler.EndSample();
        }

        public override void Setup(SimulationSettings value)
        {
            Setup(value.mainCam, value.simulationSpeed.value, value.resolution.value, value.sr, value.obstruction, value.rainSpeed.value, value.rainSizeX.value, value.rainSizeY.value, value.rainWaveHeight.value, value.waveRad.value, value.waveHeight.value, value
                .dispersion.value, value.iterations.value, value.enableRain.value, value.moreStableObstructionSimulation != null && value.moreStableObstructionSimulation.value);
        }

        public override void UpdateSettings(SimulationSettings value)
        {
            Setup(value);
        }

    }
}
