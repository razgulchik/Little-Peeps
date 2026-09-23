using System;
using System.Collections.Generic;




#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace Water2D
{

    public class ObstructorManager : WaterFeatureLayerRenderer
    {
        public static ObstructorManager instance;

        #region Singleton

        private void Awake()
        {
            Singleton();
            UpdateReflectionsShader();
            GenTextures();
        }

        void Singleton()
        {
            if (instance == null) { instance = this; }
            else if (this == instance) return;
            else DestroyImmediate(gameObject);
        }

        public static ObstructorManager GetInstance()
        {
            if (instance == null)
                instance = WaterUnityCompatibility.FindFirstObjectByTypeCompat<ObstructorManager>();
            return instance;
        }
        #endregion

        #region Variables

        public void UpdateSettings(ObstructorSettings os)
        {
            obstructionObjectsVisible.value = os.obstructionObjectsVisible.value;
            cameraVisible.value = os.cameraVisible.value;
            _textureResolution.value = os.textureResolution.value;
        }


        [HideInInspector] [SerializeField] public WaterCryo<bool> overrideMainCamera = new WaterCryo<bool>(false);
        [HideInInspector] [SerializeField] public WaterCryo<bool> obstructionObjectsVisible = new WaterCryo<bool>(false);
        [HideInInspector] [SerializeField] public WaterCryo<bool> cameraVisible = new WaterCryo<bool>(false);
        [HideInInspector] [SerializeField] public WaterCryo<float> _textureResolution = new WaterCryo<float>(1);
        [HideInInspector]
        public float textureResolution
        {
            get
            {
                if (!genSDF) return _textureResolution.value;
                else return 1920f / (float)Screen.width / 2f;
            }
        }
        [HideInInspector] [SerializeField] public Camera cam;
        [HideInInspector] [SerializeField] public Shader jfShader;
        [HideInInspector] [SerializeField] public Shader uvShader;
        [HideInInspector] [SerializeField] public Material jfMaterial;
        [HideInInspector] [SerializeField] public Material uvMaterial;
        [SerializeField] public RenderTexture jfRenderTexture1;
        [SerializeField] public RenderTexture jfRenderTexture2;


        public const string rlayer = "Obstructors";
        public const string redMatPath = "Materials/Red";
        public const string red3dMatPath = "Materials/Red3d";


        private static Material _red;
        private static Material _red3d;
        public static Material red
        {
            get { if (_red == null) _red = (Material)Resources.Load(redMatPath, typeof(Material)); return _red; }
            set { _red = value; }
        }

        public static Material red3d
        {
            get { if (_red3d == null) _red3d = (Material)Resources.Load(red3dMatPath, typeof(Material)); return _red3d; }
            set { _red3d = value; }
        }

        public LayerRenderer layerRenderer
        {
            get
            {
                if (_layerRenderer == null)
                {
                    _layerRenderer = new LayerRenderer();
                    _layerRenderer.Setup(transform, rlayer, sizeMLP, textureResolution, RenderTextureFormat.RG16, FilterMode.Point, 0f, null, true);
                }
                return _layerRenderer;
            }
            set { _layerRenderer = value; }
        }

        [NonSerialized] Dictionary<Transform, ObstructorSO> _obstructors = new Dictionary<Transform, ObstructorSO>();

        public Vector2 sizeMLP
        {
            get
            {
                float value = genSDF ? 2f : 1.25f;
                if (stableSimulationMargins) value = Mathf.Max(value, 3f);
                return new Vector2(value, value);
            }
        }

        Dictionary<Transform, ObstructorSO> obstructors
        {
            get { if (_obstructors == null) _obstructors = new Dictionary<Transform, ObstructorSO>(); return _obstructors; }
            set { _obstructors = value; }
        }


        [NonSerialized] private static Dictionary<int, ObstructorPair> _obstructionSprites;
        [SerializeField] [HideInInspector] public bool genSDF;
        private bool stableSimulationMargins;

        private static Dictionary<int, ObstructorPair> obstructionSprites
        {
            get { if (_obstructionSprites == null) _obstructionSprites = new Dictionary<int, ObstructorPair>(); return _obstructionSprites; }
            set { _obstructionSprites = value; }
        }

        public void SetStableSimulationMargins(bool value)
        {
            if (stableSimulationMargins == value) return;
            stableSimulationMargins = value;

            if (_layerRenderer != null)
            {
                _layerRenderer.Setup(transform, rlayer, sizeMLP, textureResolution, RenderTextureFormat.RG16, FilterMode.Point, 0f, this, true);
                UpdateReflectionsShader();
                if (genSDF) GenTextures();
            }
        }

        #endregion

        #region Callbacks

        void SetCallbacks()
        {
            overrideMainCamera.onValueChanged = OnSettingsChangedScene;
            obstructionObjectsVisible.onValueChanged = OnSettingsChangedScene;
            cameraVisible.onValueChanged = OnSettingsChangedScene;
        }

        private void OnSettingsChangedScene()
        {
            GetAllObstructors();
            ObstructionObjectsVisible(obstructionObjectsVisible.value);
        }

        private void ObstructionObjectsVisible(bool value)
        {
            foreach (var t in obstructors) t.Value.child.gameObject.hideFlags = value ? HideFlags.None : HideFlags.HideInHierarchy;
        }

        #endregion

        #region Common

        public void AddObstructor(ObstructorSO obs)
        {
            if (!obstructors.ContainsKey(obs.source)) obstructors.Add(obs.source, obs);
        }

        public void RemoveObstructor(Transform t)
        {
            if (obstructors.ContainsKey(t)) obstructors.Remove(t);
        }

        public void GetAllObstructors()
        {
            foreach (var r in WaterUnityCompatibility.FindObjectsByTypeCompat<Obstructor>(true))
            {
                if (!obstructors.ContainsKey(r.transform)) AddObstructor(r.GetComponent<Obstructor>().data);
            }
        }

        #endregion

        #region Disable&Enable

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += Update;
#endif
            Singleton();
            SetCallbacks();

            layerRenderer.Setup(transform, rlayer, sizeMLP, textureResolution, RenderTextureFormat.RG16, FilterMode.Point, 0f, this, true);
            UpdateReflectionsShader();
            jfShader = Shader.Find("Hidden/Jump Flood");

            SyncAllCamerasToRunState();
        }


        private void Start()
        {
            GetAllObstructors();
        }

        private void OnDisable()
        {
            if (_layerRenderer != null) _layerRenderer.Release();
#if UNITY_EDITOR
            EditorApplication.update -= Update;
#endif
            MinimizeJumpFloodTextures();
            SetAllCamerasEnabled(false);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.update -= Update;
#endif
            CleanupRTs();
        }

        #endregion

        void CleanupRTs()
        {
            ReleaseRT(jfRenderTexture1);
            ReleaseRT(jfRenderTexture2);
        }

        void ReleaseRT(RenderTexture rt)
        {
            if (rt != null)
                rt.Release();
        }

        void MinimizeJumpFloodTextures()
        {
            MinimizeRT(jfRenderTexture1);
            MinimizeRT(jfRenderTexture2);
        }

        void MinimizeRT(RenderTexture rt)
        {
            if (rt == null) return;

            if (rt.width != 1 || rt.height != 1)
            {
                rt.Release();
                rt.width = 1;
                rt.height = 1;
            }

            rt.filterMode = FilterMode.Point;
            rt.enableRandomWrite = false;

            if (!rt.IsCreated())
                rt.Create();
        }

        bool IsMinimized(RenderTexture rt)
        {
            return rt != null && rt.width <= 1 && rt.height <= 1;
        }

        public static void AdaptiveDestroy(UnityEngine.Object gameObject)
        {
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(gameObject);
#else
            UnityEngine.Object.Destroy(gameObject);
#endif
        }

        void SetAllCamerasEnabled(bool enabled)
        {
            foreach (var c in GetComponentsInChildren<Camera>(true))
                c.enabled = enabled;
        }

        void SyncAllCamerasToRunState()
        {
            SetAllCamerasEnabled(run);

            if (!run)
                MinimizeJumpFloodTextures();
            else if (genSDF && (IsMinimized(jfRenderTexture1) || IsMinimized(jfRenderTexture2)))
                GenTextures();
        }

        private void GenTextures()
        {
            if (_layerRenderer == null) return;
            var obsTex = layerRenderer.LayerTexture();
            if (obsTex == null || obsTex.width <= 1 || obsTex.height <= 1) return;
            if (jfRenderTexture1 != null)
            {
                jfRenderTexture1.Release();
                AdaptiveDestroy(jfRenderTexture1);
            }
            if (jfRenderTexture2 != null)
            {
                jfRenderTexture2.Release();
                AdaptiveDestroy(jfRenderTexture2);
            }
            int width = Mathf.Max(1, obsTex.width / 2);
            int height = Mathf.Max(1, obsTex.height / 2);
            jfRenderTexture1 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            jfRenderTexture2 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            jfRenderTexture1.enableRandomWrite = false;
            jfRenderTexture2.enableRandomWrite = false;
            jfRenderTexture1.filterMode = FilterMode.Point;
            jfRenderTexture2.filterMode = FilterMode.Point;
            jfRenderTexture1.wrapMode = TextureWrapMode.Repeat;
            jfRenderTexture2.wrapMode = TextureWrapMode.Repeat;
            jfRenderTexture1.Create();
            jfRenderTexture2.Create();
        }

        public void GenerateSDF()
        {
            var obsTex = layerRenderer.LayerTexture();
            if (obsTex == null || Screen.width == 0 || Screen.height == 0) return;
            if (jfRenderTexture1 == null || jfRenderTexture1.width <= 1 || jfRenderTexture1.height <= 1) GenTextures();
            if (jfRenderTexture2 == null || jfRenderTexture2.width <= 1 || jfRenderTexture2.height <= 1) GenTextures();

            if (jfShader == null) jfShader = Shader.Find("Hidden/Jump Flood");
            if (uvShader == null) uvShader = Shader.Find("Hidden/UV Encoding");
            if (jfMaterial == null) jfMaterial = new Material(jfShader);
            if (uvMaterial == null) uvMaterial = new Material(uvShader);

            int iterations = Mathf.Max(1, Mathf.CeilToInt(Mathf.Log(jfRenderTexture1.width * jfRenderTexture1.height)));

            Vector2 stepSize = new Vector2(jfRenderTexture1.width, jfRenderTexture1.height);

            Graphics.Blit(obsTex, jfRenderTexture1, uvMaterial);

            jfMaterial.SetVector("stepSize", stepSize);
            jfMaterial.SetVector("screenSize", stepSize);

            for (int i = 0; i < iterations; i++)
            {
                stepSize /= 2.0f;
                jfMaterial.SetVector("stepSize", stepSize);

                if (i % 2 == 0) Graphics.Blit(jfRenderTexture1, jfRenderTexture2, jfMaterial);
                else Graphics.Blit(jfRenderTexture2, jfRenderTexture1, jfMaterial);
            }

            if (iterations % 2 == 0) Graphics.Blit(jfRenderTexture1, jfRenderTexture2);

            if (!Shader.IsKeywordEnabled(WaterShaderIdsOBS.DepthTexture)) Shader.SetGlobalTexture(WaterShaderIdsOBS.DepthTexture, jfRenderTexture2);
        }

        #region Update

        protected override void Update()
        {
#if UNITY_EDITOR
            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive == false) return;
#endif
            base.Update();

            SyncAllCamerasToRunState();

            if (!run) return;

            if (_layerRenderer != null) _layerRenderer.Loop();

            if (cam == null) cam = GetComponent<Camera>();
            UpdateObstructionSprites();
        }

        public void SyncLateAfterCamera(bool shouldRun)
        {
            SetRunStateImmediate(shouldRun);
            SyncAllCamerasToRunState();

            if (_layerRenderer != null) _layerRenderer.Loop();
            if (!shouldRun) return;

            if (cam == null) cam = GetComponent<Camera>();
            UpdateObstructionSprites();
        }

        private void LateUpdate()
        {
            if (run && genSDF) GenerateSDF();
        }

        MaterialPropertyBlock mpb;

        private void UpdateObstructionSprites()
        {
            mpb = new MaterialPropertyBlock();
            foreach (var i in obstructors)
            {
                if (i.Key == null) continue;
                var obs = i.Value;
                var child = obs.childSr;
                var source = obs.sourceSr;

                if (source.flipX != child.flipX)
                    child.flipX = source.flipX;

                if (source.sprite != child.sprite)
                {
                    child.sprite = source.sprite;

                    var sprite = child.sprite;
                    var tex = sprite.texture;
                    float texH = tex.height;
                    var rect = sprite.rect;

                    child.GetPropertyBlock(mpb);
                    mpb.SetFloat("_ss", tex.height <= rect.height ? 0f : 1f);
                    mpb.SetFloat("_minY", rect.yMin / texH);
                    mpb.SetFloat("_maxY", rect.yMax / texH);
                    child.SetPropertyBlock(mpb);
                }
            }
        }

        private void UpdateReflectionsShader()
        {
            if (_layerRenderer == null) return;
            var tex = layerRenderer.LayerTexture();
            if (tex != null && !Shader.IsKeywordEnabled(WaterShaderIdsOBS.OBStexture))
                Shader.SetGlobalTexture(WaterShaderIdsOBS.OBStexture, tex);
        }

        #endregion
    }
}