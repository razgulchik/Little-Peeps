#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;


namespace Water2D
{
    public enum WaterRenderMode
    {
        SpriteRenderer = 0,
        Tilemap = 1
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    public partial class ModernWater2D : MonoBehaviour
    {
        public static ModernWater2DSettings defaultSettings;

        #region Global Settings

        [HideInInspector][SerializeField] public WaterRenderMode renderMode = WaterRenderMode.SpriteRenderer;
        [HideInInspector][SerializeField] WaterRenderMode _previousRenderMode = WaterRenderMode.SpriteRenderer;

        #endregion

        #region Tilemap References

        const string TilemapChildName = "WaterTilemap";

        [HideInInspector][SerializeField] GameObject _renderChild;
        [HideInInspector][SerializeField] Grid _grid;
        [HideInInspector][SerializeField] Tilemap _tilemap;
        [HideInInspector][SerializeField] TilemapRenderer _tilemapRenderer;

        public Grid grid
        {
            get
            {
                if (_grid == null && IsTilemapMode && _renderChild != null) _grid = _renderChild.GetComponent<Grid>();
                return _grid;
            }
        }

        public Tilemap tilemap
        {
            get
            {
                if (_tilemap == null && IsTilemapMode && _renderChild != null) _tilemap = _renderChild.GetComponent<Tilemap>();
                return _tilemap;
            }
        }

        public TilemapRenderer tilemapRenderer
        {
            get
            {
                if (_tilemapRenderer == null && IsTilemapMode && _renderChild != null) _tilemapRenderer = _renderChild.GetComponent<TilemapRenderer>();
                return _tilemapRenderer;
            }
        }

        #endregion

        #region Renderer Abstraction

        public bool IsTilemapMode => renderMode == WaterRenderMode.Tilemap;
        public bool IsSpriteMode => renderMode == WaterRenderMode.SpriteRenderer;

        public Renderer ActiveRenderer
        {
            get
            {
                if (IsTilemapMode) return tilemapRenderer;
                return sr;
            }
        }

        public Material ActiveSharedMaterial
        {
            get
            {
                if (tilemapRenderer)
                {
                    var r = ActiveRenderer;
                    return r != null ? r.sharedMaterial : null;
                }
                else return mat;
            }
            set
            {
                var r = ActiveRenderer;
                if (r != null) r.sharedMaterial = value;
            }
        }

        public Bounds ActiveBounds
        {
            get
            {
                var r = ActiveRenderer;
                if (r != null) return r.bounds;
                return new Bounds(transform.position, Vector3.one);
            }
        }

        #endregion

        // FIX: Single source of truth for "does this pool need SDF from the ObstructorManager?".
        // Both the explicit depthFromObstructors bool AND the distance_from_obstructors coloring type
        // require genSDF=true, _depthFromObstructors=1, and the scaled _obs_transform.
        // Previously only the bool was checked so selecting the coloring type had zero visible effect.
        bool EffectivelyUsesSDF()
        {
            if (settings?._waterSettings == null) return false;
            return settings._waterSettings.depthFromObstructors?.value == true
                || settings._waterSettings.coloringType == ColoringType.distance_from_obstructors;
        }

        [HideInInspector][SerializeField] public SimulationType _waterSimulationType = SimulationType.basic;
        [HideInInspector][NonSerialized] private WaterSimulation _waterSimulation;
        [HideInInspector][SerializeField] private WaveSimulation _wavesSimulation;
        private static ObstructorManager _obstructorManager;
        private static ReflectionsSystem _reflectionsManagerPlatformer;
        private static ReflectionsSystem _reflectionsManagerTopDown;
        private static ReflectionsSystem _reflectionsManagerRayMarch;

        [HideInInspector]
        public ObstructorManager obstructorManager
        {
            get
            {
                if (_obstructorManager == null)
                {
                    _obstructorManager = WaterUnityCompatibility.FindFirstObjectByTypeCompat<ObstructorManager>();
                    if (_obstructorManager == null)
                    {
                        var go = new GameObject("ObstructorManager");
                        go.transform.parent = managersParent;
                        _obstructorManager = go.AddComponent<ObstructorManager>();
                    }
                }

                if (_obstructorManager.transform.parent != managersParent)
                    _obstructorManager.transform.parent = managersParent;

                return _obstructorManager;
            }
            set { }
        }

        [HideInInspector][SerializeField] SurfaceRenderingManager _surfaceRenderer;
        [HideInInspector]
        public SurfaceRenderingManager surfaceRenderer
        {
            get
            {
                if (_surfaceRenderer == null)
                {
                    _surfaceRenderer = WaterUnityCompatibility.FindFirstObjectByTypeCompat<SurfaceRenderingManager>();
                    if (_surfaceRenderer == null)
                    {
                        var go = new GameObject("SurfaceRenderer");
                        go.transform.parent = managersParent;
                        _surfaceRenderer = go.AddComponent<SurfaceRenderingManager>();
                        _surfaceRenderer.SetupLayerRenderer();
                    }
                }

                if (_surfaceRenderer.transform.parent != managersParent)
                    _surfaceRenderer.transform.parent = managersParent;

                return _surfaceRenderer;
            }
            set { }
        }

        // Inactive GO pattern: create GO with SetActive(false) so only Awake fires during AddComponent
        // (sets startupQF true). Set the static BEFORE SetActive(true) so when OnEnable fires,
        // Singleton() sees instanceXxx == this and neither destroy branch executes.
        // Without this, if a valid old instance is in the static, Singleton destroys the new
        // component and SetupSystem() crashes trying to call GetComponent on a destroyed 'this'.

        [HideInInspector]
        public ReflectionsSystem reflectionsManagerPlatformer
        {
            get
            {
                if (_reflectionsManagerPlatformer == null)
                {
                    foreach (var system in WaterUnityCompatibility.FindObjectsByTypeCompat<ReflectionsSystem>(true))
                        if (system.name == "ReflectionsManagerPL") { _reflectionsManagerPlatformer = system; break; }
                }
                if (_reflectionsManagerPlatformer == null)
                {
                    var go = new GameObject("ReflectionsManagerPL");
                    go.SetActive(false);
                    go.transform.parent = managersParent;
                    _reflectionsManagerPlatformer = go.AddComponent<ReflectionsSystem>();
                    _reflectionsManagerPlatformer.topdown = false;
                    _reflectionsManagerPlatformer.raymarch = false;
                    ReflectionsSystem.instancePlatformer = _reflectionsManagerPlatformer;
                    go.SetActive(true);
                }

                _reflectionsManagerPlatformer.topdown = false;
                _reflectionsManagerPlatformer.raymarch = false;
                if (_reflectionsManagerPlatformer.transform.parent != managersParent)
                    _reflectionsManagerPlatformer.transform.parent = managersParent;

                return _reflectionsManagerPlatformer;
            }
            set { _reflectionsManagerPlatformer = value; }
        }

        [HideInInspector]
        public ReflectionsSystem reflectionsManagerRayMarch
        {
            get
            {
                if (_reflectionsManagerRayMarch == null)
                {
                    foreach (var system in WaterUnityCompatibility.FindObjectsByTypeCompat<ReflectionsSystem>(true))
                        if (system.name == "ReflectionsManagerRM") { _reflectionsManagerRayMarch = system; break; }
                }
                if (_reflectionsManagerRayMarch == null)
                {
                    var go = new GameObject("ReflectionsManagerRM");
                    go.SetActive(false);
                    go.transform.parent = managersParent;
                    _reflectionsManagerRayMarch = go.AddComponent<ReflectionsSystem>();
                    _reflectionsManagerRayMarch.topdown = false;
                    _reflectionsManagerRayMarch.raymarch = true;
                    ReflectionsSystem.instanceRayMarch = _reflectionsManagerRayMarch;
                    go.SetActive(true);
                }

                _reflectionsManagerRayMarch.topdown = false;
                _reflectionsManagerRayMarch.raymarch = true;
                if (_reflectionsManagerRayMarch.transform.parent != managersParent)
                    _reflectionsManagerRayMarch.transform.parent = managersParent;

                return _reflectionsManagerRayMarch;
            }
            set { _reflectionsManagerRayMarch = value; }
        }

        [HideInInspector]
        public ReflectionsSystem reflectionsManagerTopDown
        {
            get
            {
                if (_reflectionsManagerTopDown == null)
                {
                    foreach (var system in WaterUnityCompatibility.FindObjectsByTypeCompat<ReflectionsSystem>(true))
                        if (system.name == "ReflectionsManagerTD") { _reflectionsManagerTopDown = system; break; }
                }
                if (_reflectionsManagerTopDown == null)
                {
                    var go = new GameObject("ReflectionsManagerTD");
                    go.SetActive(false);
                    go.transform.parent = managersParent;
                    _reflectionsManagerTopDown = go.AddComponent<ReflectionsSystem>();
                    _reflectionsManagerTopDown.topdown = true;
                    _reflectionsManagerTopDown.raymarch = false;
                    ReflectionsSystem.instanceTopDown = _reflectionsManagerTopDown;
                    go.SetActive(true);
                }

                _reflectionsManagerTopDown.topdown = true;
                _reflectionsManagerTopDown.raymarch = false;
                if (_reflectionsManagerTopDown.transform.parent != managersParent)
                    _reflectionsManagerTopDown.transform.parent = managersParent;

                return _reflectionsManagerTopDown;
            }
            set { _reflectionsManagerTopDown = value; }
        }

        public WaterSimulation waterSimulation
        {
            get
            {
                if (_waterSimulation == null) SetWaterSim(ref _waterSimulation);
                return _waterSimulation;
            }
            set { _waterSimulation = value; }
        }

        public WaveSimulation wavesSimulation
        {
            get
            {
                if (_wavesSimulation == null)
                {
                    _wavesSimulation = new WaveSimulation();
                    _wavesSimulation.SetSettings(gameObject, sr, settings._wavesSettings);
                }
                return _wavesSimulation;
            }
            set { _wavesSimulation = value; }
        }

        [HideInInspector][SerializeField] private LayerRenderer _childPPLayerRenderer;

        [HideInInspector][SerializeField] private GameObject _childPP;
        public GameObject childPP
        {
            get
            {
                if (_childPP == null && settings._blurSettings != null && settings._blurSettings.useBlur != null && settings._blurSettings.useBlur.value && IsSpriteMode) CreateChildPP();
                return _childPP;
            }
            set { _childPP = value; }
        }

        [SerializeField][HideInInspector] public WaterCryo<bool> ManagersVisible = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public WaterCryo<bool> enableObstruction = new WaterCryo<bool>(true);
        [HideInInspector][SerializeField] public WaterCryo<bool> enableReflections = new WaterCryo<bool>(true);
        [HideInInspector][SerializeField] public WaterCryo<bool> enableSimulation = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public WaterCryo<bool> enableWavesSimulation = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public WaterCryo<bool> enableBlur = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public bool lightingWhenBlur = false;
        [HideInInspector][SerializeField] public WaterCryo<bool> normalsPreview = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public WaterCryo<bool> overrideMainCamera = new WaterCryo<bool>(false);
        [HideInInspector][SerializeField] public Camera cameraOverride;
        [SerializeField][HideInInspector] public float raymarchUnits = 0;
        [HideInInspector][SerializeField] public ModernWater2DSettings settings = new ModernWater2DSettings();
        [HideInInspector][SerializeField] public bool customWaterMaterial;

        [HideInInspector][SerializeField] Material _mat;
        public Material mat
        {
            set { _mat = value; }
            get
            {
                if (_mat == null || _mat.name == "Sprite-Lit-Default")
                {
                    if (settings._waterSettings.waterType == WaterType.normal) _mat = new Material(Shader.Find("water2d/waterg"));
                    else _mat = new Material(Shader.Find("water2d/watergcheap")); return _mat;
                }
                else if (_mat.name == "water2d/waterg" && settings._waterSettings.waterType != WaterType.normal)
                {
                    _mat = new Material(Shader.Find("water2d/watergcheap"));
                }
                else if (_mat.name == "water2d/watergcheap" && settings._waterSettings.waterType == WaterType.normal)
                {
                    _mat = new Material(Shader.Find("water2d/waterg"));
                }
                return _mat;
            }
        }

        [HideInInspector][SerializeField] Material _matb;
        public Material matb
        {
            set { _matb = value; }
            get { if (_matb == null) OnBlurMaterialChanged(); return _matb; }
        }

        private static Transform _managersParent;
        public static Transform managersParent
        {
            get
            {
                if (_managersParent == null)
                {
                    var existing = WaterUnityCompatibility.FindFirstObjectByTypeCompat<ManagersParent>(true);
                    if (existing != null)
                        _managersParent = existing.transform;
                    else
                    {
                        _managersParent = new GameObject(managersParentName).transform;
                        _managersParent.gameObject.AddComponent<ManagersParent>();
                    }
                }
                return _managersParent;
            }
            set { _managersParent = value; }
        }

        [HideInInspector][SerializeField] private SpriteRenderer _sr;
        public SpriteRenderer sr
        {
            get
            {
                if (_sr == null) _sr = GetComponent<SpriteRenderer>();
                if (_sr != null && IsSpriteMode) SetMaterials();
                return _sr;
            }
            set { _sr = value; }
        }

        public const string managersParentName = "2DWaterManagers";
        public const string srLayer = "Water";
        public const string sr2Layer = "WaterPostProcessing";

        #region Render Mode Switching

        public void ApplyRenderMode()
        {
            if (IsTilemapMode)
                SetupTilemapMode();
            else
                SetupSpriteMode();

            _previousRenderMode = renderMode;
        }

        public bool HasRenderModeChanged()
        {
            return renderMode != _previousRenderMode;
        }

        void DestroyTilemapChild()
        {
            if (_renderChild != null)
            {
                if (Application.isPlaying) Destroy(_renderChild);
                else DestroyImmediate(_renderChild);
            }
            _renderChild = null;
            _grid = null;
            _tilemap = null;
            _tilemapRenderer = null;
        }

        GameObject FindOrCreateTilemapChild()
        {
            if (_renderChild != null && _renderChild.name == TilemapChildName)
                return _renderChild;

            DestroyTilemapChild();

            _renderChild = new GameObject(TilemapChildName);
            _renderChild.transform.SetParent(transform, false);
            _renderChild.layer = gameObject.layer;
            ApplyCounterScale();
            return _renderChild;
        }

        void ApplyCounterScale()
        {
            if (_renderChild == null) return;
            var ls = transform.lossyScale;
            _renderChild.transform.localScale = new Vector3(
                Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
                Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
                Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z
            );
        }

        void SetupTilemapMode()
        {
            var existingSr = GetComponent<SpriteRenderer>();
            if (existingSr != null) existingSr.enabled = false;

            var child = FindOrCreateTilemapChild();

            _grid = child.GetComponent<Grid>();
            if (_grid == null) _grid = child.AddComponent<Grid>();

            _tilemap = child.GetComponent<Tilemap>();
            if (_tilemap == null) _tilemap = child.AddComponent<Tilemap>();

            _tilemapRenderer = child.GetComponent<TilemapRenderer>();
            if (_tilemapRenderer == null) _tilemapRenderer = child.AddComponent<TilemapRenderer>();

            _tilemapRenderer.enabled = true;
            _tilemapRenderer.sortingLayerName = srLayer;
            _tilemapRenderer.sharedMaterial = mat;

            bool mobEnable = settings._waterSettings.waterType == WaterType.mobile;
            bool cheapEnable = settings._waterSettings.waterType == WaterType.cheap;
            ActiveSharedMaterial.SetInt("_cheapWater", cheapEnable ? 1 : 0);
            ActiveSharedMaterial.SetInt("_mobileWater", mobEnable ? 1 : 0);
            ActiveSharedMaterial.SetTexture("_simTex", (Texture2D)Resources.Load("Sprites/placeholders/blackTex"));
            ActiveSharedMaterial.SetTexture("_wavesHeight", (Texture2D)Resources.Load("Sprites/placeholders/whiteTex"));

            if (settings._reflectionsSettings != null && settings._reflectionsSettings.usePerspective != null && settings._reflectionsSettings.usePerspective.value)
                settings._reflectionsSettings.usePerspective.value = false;

            if (settings._blurSettings != null && settings._blurSettings.useBlur != null && settings._blurSettings.useBlur.value)
            {
                settings._blurSettings.useBlur.value = false;
                if (_childPP != null) DestroyImmediate(_childPP);
            }

            child.layer = Obstructor.GetLayerIdx(srLayer);
            SetLayers();
        }

        void SetupSpriteMode()
        {
            DestroyTilemapChild();

            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.enabled = true;
        }

        public bool IsBlurAvailable => IsSpriteMode;
        public bool IsPerspectiveAvailable => IsSpriteMode;
        public bool IsWavesAvailable => IsSpriteMode;

        #endregion

        // Uses Unity's implicit bool conversion to detect destroyed objects that C# != null misses.
        // Also clears ReflectionsSystem statics — if those hold a valid live instance while ModernWater2D's
        // backing field is null, the getter would create a new instance whose Singleton() check sees
        // instanceXxx != null && this != instanceXxx and destroys it immediately.
        static void ClearDestroyedStaticManagers()
        {
            _reflectionsManagerPlatformer = _reflectionsManagerPlatformer ? _reflectionsManagerPlatformer : null;
            _reflectionsManagerTopDown = _reflectionsManagerTopDown ? _reflectionsManagerTopDown : null;
            _reflectionsManagerRayMarch = _reflectionsManagerRayMarch ? _reflectionsManagerRayMarch : null;
            _obstructorManager = _obstructorManager ? _obstructorManager : null;
            _managersParent = _managersParent ? _managersParent : null;

            ReflectionsSystem.instanceTopDown = ReflectionsSystem.instanceTopDown ? ReflectionsSystem.instanceTopDown : null;
            ReflectionsSystem.instancePlatformer = ReflectionsSystem.instancePlatformer ? ReflectionsSystem.instancePlatformer : null;
            ReflectionsSystem.instanceRayMarch = ReflectionsSystem.instanceRayMarch ? ReflectionsSystem.instanceRayMarch : null;
        }

        private void OnEnable()
        {
            RegisterReflectionViewportCallbacks();
            EnsureSettingsInitialized();
            resolution = new Vector2(Screen.width, Screen.height);

            if (HasRenderModeChanged())
                ApplyRenderMode();

            if (IsSpriteMode)
                EnableSpriteMode();
            else
                EnableTilemapMode();

            SetLayers();
            SetupManagers();
            SetCallbacks();
            CameraSetup();
            OnBlurMaterialChanged();
            OnWavesSimulationChanged();
        }

        void EnsureSettingsInitialized()
        {
            if (settings == null)
            {
                settings = new ModernWater2DSettings();
                return;
            }
            if (settings._waterSettings == null) settings._waterSettings = new WaterSettings();
            if (settings._reflectionsSettings == null) settings._reflectionsSettings = new ReflectionsSettings();
            if (settings._simulationSettings == null) settings._simulationSettings = new SimulationSettings();
            if (settings._obstructorSettings == null) settings._obstructorSettings = new ObstructorSettings();
            if (settings._wavesSettings == null) settings._wavesSettings = new WaveSimulationSettings();
            if (settings._blurSettings == null) settings._blurSettings = new BlurSettings();

            if (settings._waterSettings.depthMlp == null) settings._waterSettings = new WaterSettings();
            if (settings._reflectionsSettings.enableTopDownReflections == null) settings._reflectionsSettings = new ReflectionsSettings();
            if (settings._obstructorSettings.obstructionObjectsVisible == null) settings._obstructorSettings = new ObstructorSettings();
            if (settings._simulationSettings.normalStrength == null) settings._simulationSettings = new SimulationSettings();
            if (settings._simulationSettings.moreStableObstructionSimulation == null) settings._simulationSettings.moreStableObstructionSimulation = new WaterCryo<bool>(false);
            if (settings._wavesSettings.edgeColor == null) settings._wavesSettings = new WaveSimulationSettings();
            if (settings._blurSettings.useBlur == null) settings._blurSettings = new BlurSettings();
        }

        void EnableSpriteMode()
        {
            DestroyTilemapChild();

            sr = GetComponent<SpriteRenderer>();
            if (sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            sr.enabled = true;

            if (sr.sprite == null)
            {
                var defaultTex = Resources.Load<Texture2D>("Sprites/textures/512x512");
                if (defaultTex != null)
                {
                    sr.sprite = Sprite.Create(defaultTex, new Rect(0, 0, defaultTex.width, defaultTex.height), new Vector2(0.5f, 0.5f), 100f);
                    sr.sprite.name = "Water2D_Default";
                }
            }

            if (sr.sharedMaterial == null)
            {
                if (settings._waterSettings.waterType == WaterType.normal) sr.sharedMaterial = new Material(Shader.Find("water2d/waterg"));
                else sr.sharedMaterial = new Material(Shader.Find("water2d/watergcheap"));
            }
            bool mobEnable = settings._waterSettings.waterType == WaterType.mobile;
            bool cheapEnable = settings._waterSettings.waterType == WaterType.cheap;
            sr.sharedMaterial.SetInt("_cheapWater", cheapEnable ? 1 : 0);
            sr.sharedMaterial.SetInt("_mobileWater", mobEnable ? 1 : 0);

            mat = new Material(sr.sharedMaterial);
            sr.sharedMaterial = mat;
            sr.sharedMaterial.SetTexture("_simTex", (Texture2D)Resources.Load("Sprites/placeholders/blackTex"));
            sr.sharedMaterial.SetTexture("_wavesHeight", (Texture2D)Resources.Load("Sprites/placeholders/whiteTex"));
        }

        void EnableTilemapMode()
        {
            if (_renderChild == null || _renderChild.GetComponent<TilemapRenderer>() == null)
                SetupTilemapMode();
            else
            {
                _tilemapRenderer = _renderChild.GetComponent<TilemapRenderer>();
                var existingSr = GetComponent<SpriteRenderer>();
                if (existingSr != null) existingSr.enabled = false;
            }

            if (_tilemapRenderer.sharedMaterial == null)
            {
                mat = new Material(mat);
                _tilemapRenderer.sharedMaterial = mat;
            }

            ActiveSharedMaterial.SetTexture("_simTex", (Texture2D)Resources.Load("Sprites/placeholders/blackTex"));
            ActiveSharedMaterial.SetTexture("_wavesHeight", (Texture2D)Resources.Load("Sprites/placeholders/whiteTex"));
        }

        void SetLayers()
        {
#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(srLayer)) WaterLayers.CreateLayer(srLayer);
            if (!WaterLayers.LayerExists(sr2Layer)) WaterLayers.CreateLayer(sr2Layer);
#endif
            gameObject.layer = Obstructor.GetLayerIdx(srLayer);
            if (_renderChild != null) _renderChild.layer = Obstructor.GetLayerIdx(srLayer);
            SetCameraLayers();
            if (IsSpriteMode) CreateDestroyPostProcessingCamera();
        }

        void CreateDestroyPostProcessingCamera()
        {
            if (!IsSpriteMode) return;
            if (settings._blurSettings == null || settings._blurSettings.useBlur == null) return;
            if (settings._blurSettings.useBlur.value && _childPP == null) CreateChildPP();
            else if (!settings._blurSettings.useBlur.value && _childPP != null) DestroyImmediate(_childPP);
        }

        void OnDisable()
        {
            UnregisterReflectionViewportCallbacks();
            if (_reflectionsManagerTopDown != null) _reflectionsManagerTopDown.UnregisterSource(WaterUnityCompatibility.GetObjectStableId(this));
            if (_reflectionsManagerPlatformer != null) _reflectionsManagerPlatformer.UnregisterSource(WaterUnityCompatibility.GetObjectStableId(this));
            if (_reflectionsManagerRayMarch != null) _reflectionsManagerRayMarch.UnregisterSource(WaterUnityCompatibility.GetObjectStableId(this));
        }

        void OnDestroy()
        {
            OnDisable();
            if (!Application.isPlaying && mat != null)
                DestroyImmediate(mat);
        }

        void CreateChildPP()
        {
            if (!IsSpriteMode) return;

            _childPP = new GameObject(name + " post processing");
            SpriteRenderer childSr = _childPP.AddComponent<SpriteRenderer>();

            SetCameraAboveWaterTransform(_childPP.transform, 5f);

            childSr.color = Color.white;
            childSr.sprite = this.sr.sprite;
            childSr.sharedMaterial = matb;
            childSr.sortingLayerName = this.sr.sortingLayerName;
            childSr.sortingOrder = this.sr.sortingOrder + 1;

            if (Camera.main != null) _childPP.AddComponent<Camera>().CopyFrom(Camera.main);

            if (_childPPLayerRenderer == null) _childPPLayerRenderer = new LayerRenderer();
            _childPPLayerRenderer.Setup(childSr, _childPP.transform, srLayer);

#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(sr2Layer)) WaterLayers.CreateLayer(sr2Layer);
#endif
            _childPP.layer = Obstructor.GetLayerIdx(sr2Layer);

            SetCameraAboveWaterTransform(_childPP.transform, 5f);
        }

        void SetCameraAboveWaterTransform(Transform t, float deltaZ)
        {
            t.parent = transform;
            t.localPosition = new Vector3(0, 0, -Mathf.Abs(deltaZ));
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        Camera GetCameraRenderingScreen()
        {
            return Camera.main;
        }

        void SetCameraLayers()
        {
            if (Camera.main == null) return;
            Camera.main.cullingMask |= (1 << Obstructor.GetLayerIdx(srLayer));
            Camera.main.cullingMask |= (1 << Obstructor.GetLayerIdx(sr2Layer));
        }

        public void SetWaterSim(ref WaterSimulation _waterSimulation)
        {
            switch (_waterSimulationType)
            {
                case SimulationType.basic:
                    _waterSimulation = new WaterSimulationSimple(); break;
                case SimulationType.advanced:
                    _waterSimulation = new WaterSimulationAdvanced(); break;
                default:
                    _waterSimulation = new WaterSimulationSimple(); break;
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (!enableSimulation.value) return;
            if (ActiveSharedMaterial == null) return;
            if (_waterSimulation == null) return;

            var rt = waterSimulation.GetRT();
            if (rt != null) ActiveSharedMaterial.SetTexture("_simTex", rt);
        }

        void SetupManagers()
        {
            ClearDestroyedStaticManagers();

            bool obsExisted = _obstructorManager != null;
            bool tdExisted = _reflectionsManagerTopDown != null;
            bool plExisted = _reflectionsManagerPlatformer != null;
            bool rmExisted = _reflectionsManagerRayMarch != null;
            bool surfExisted = _surfaceRenderer != null;

            ObstructorManager.instance = obstructorManager;

            SimulationSetup();

            if (enableReflections.value)
            {
                if (!tdExisted && settings._reflectionsSettings.enableTopDownReflections.value)
                    reflectionsManagerTopDown.UpdateSettings(settings._reflectionsSettings, true, WaterUnityCompatibility.GetObjectStableId(this));
                if (!plExisted && settings._reflectionsSettings.enablePlatformerReflections.value)
                    reflectionsManagerPlatformer.UpdateSettings(settings._reflectionsSettings, false, WaterUnityCompatibility.GetObjectStableId(this));
                if (!rmExisted && settings._reflectionsSettings.enableRaymarchedReflections.value)
                    reflectionsManagerRayMarch.UpdateSettings(settings._reflectionsSettings, false, WaterUnityCompatibility.GetObjectStableId(this));

                OnReflectionsChanged();
            }

            if (!obsExisted && enableObstruction.value) OnObstructionChanged();
            if (enableSimulation.value) OnOSimulationChanged();
            if (!surfExisted || !surfaceRenderer.layerRenderer.IsSetupComplete()) surfaceRenderer.SetupLayerRenderer();

            if (enableWavesSimulation.value && IsWavesAvailable) SetupWaveSimulation();
            OnWaterChanged();
            managersParent.gameObject.hideFlags = ManagersVisible.value ? HideFlags.None : HideFlags.HideInHierarchy;
        }

        void OnResolutionChanged()
        {
        }

        Vector2 resolution;

        bool CheckForResolutionChanged()
        {
            if (resolution.x != Screen.width || resolution.y != Screen.height)
            {
                resolution = new Vector2(Screen.width, Screen.height);
                return true;
            }
            return false;
        }

        void SimulationSetup()
        {
            if (!enableSimulation.value) return;
            if (ActiveRenderer == null) return;

            var obsInstance = ObstructorManager.GetInstance();
            if (obsInstance == null) return;
            if (obsInstance.layerRenderer == null) return;

            var obsTex = obsInstance.layerRenderer.LayerTexture();
            if (obsTex == null) return;

            settings._simulationSettings.sr = ActiveRenderer;
            settings._simulationSettings.obstruction = obsTex;
            waterSimulation.Setup(settings._simulationSettings);

            if (ActiveSharedMaterial != null)
            {
                var rt = waterSimulation.GetRT();
                if (rt != null) ActiveSharedMaterial.SetTexture("_simTex", rt);
            }
        }

        void CameraSetup()
        {
            if (Camera.main == null) return;
            if (ActiveSharedMaterial == null) return;
            ActiveSharedMaterial.SetMatrix("_projectionMatrix", Camera.main.projectionMatrix);
            ActiveSharedMaterial.SetMatrix("_worldToCamMatrix", Camera.main.worldToCameraMatrix);
            ActiveSharedMaterial.SetVector("_camRect", new Vector4(Camera.main.rect.x, Camera.main.rect.y, Camera.main.rect.width, Camera.main.rect.height));
            ActiveSharedMaterial.SetVector("_camSize", new Vector2(Camera.main.pixelWidth, Camera.main.pixelHeight));

            Vector4 obsTransform = new Vector4(1f, 1f, 0f, 0f);
            var obsManager = ObstructorManager.GetInstance();
            if (obsManager != null && obsManager.layerRenderer != null)
                obsTransform = obsManager.layerRenderer.GetScreenUvTransform();
            ActiveSharedMaterial.SetVector("_obs_transform", obsTransform);

            ActiveSharedMaterial.SetVector("_centerPos", new Vector4(ActiveBounds.center.x, ActiveBounds.center.y, ActiveBounds.center.z, 0f));
            ActiveSharedMaterial.SetVector("_waterSize", new Vector4(ActiveBounds.size.x, ActiveBounds.size.y, 0f, 0f));

        }

        void UpdateTilemapSimBounds()
        {
            if (ActiveSharedMaterial == null) return;
            Bounds b = ActiveBounds;
            ActiveSharedMaterial.SetVector("_simWorldBounds", new Vector4(b.min.x, b.min.y, b.max.x, b.max.y));
        }

        void SetCallbacks()
        {
            enableObstruction.onValueChanged = OnWaterChanged;
            overrideMainCamera.onValueChanged = OnCameraSettingsChanged;
            enableReflections.onValueChanged = OnWaterChanged;
            enableSimulation.onValueChanged = OnWaterChanged;
            enableWavesSimulation.onValueChanged = OnWaterChanged;
            ManagersVisible.onValueChanged = OnInspectorSettingsChanged;

            settings._reflectionsSettings.onValueChanged(OnReflectionsChanged);
            settings._simulationSettings.onValueChanged(OnOSimulationChanged);
            settings._wavesSettings.OnValueChanged(OnWavesSimulationChanged);
            settings._obstructorSettings.onValueChanged(OnObstructionChanged);
            settings._waterSettings.onValueChanged(OnWaterChanged);
            settings._blurSettings.onValueChanged(OnBlurChanged);
        }

        public void OnCameraSettingsChanged()
        {
            bool changeFlag = false;
            if (overrideMainCamera.value == false)
            {
                settings._obstructorSettings.mainCamera = Camera.main;
                settings._reflectionsSettings.mainCamera = Camera.main;
                settings._simulationSettings.mainCam = Camera.main;
                changeFlag = true;
            }
            else if (cameraOverride != null)
            {
                settings._obstructorSettings.mainCamera = cameraOverride;
                settings._reflectionsSettings.mainCamera = cameraOverride;
                settings._simulationSettings.mainCam = cameraOverride;
                changeFlag = true;
            }

            if (changeFlag)
            {
                OnReflectionsChanged();
                OnObstructionChanged();
                CameraSetup();
            }
        }

        void OnOSimulationChanged()
        {
            SetWaterSim(ref _waterSimulation);
            if (!enableSimulation.value) return;
            if (ActiveSharedMaterial == null) return;
            if (settings._simulationSettings == null || settings._simulationSettings.normalStrength == null) return;
            waterSimulation.UpdateSettings(settings._simulationSettings);
            ActiveSharedMaterial.SetFloat("_normStr", settings._simulationSettings.normalStrength.value);
            ActiveSharedMaterial.SetColor("_simFoamColor", settings._simulationSettings.waveColor.value);
            ActiveSharedMaterial.SetVector("_simMinMaxWavesHeightFoam", settings._simulationSettings.waveColorMinMaxHeight.value);
            var rt = waterSimulation.GetRT();
            if (rt != null) ActiveSharedMaterial.SetTexture("_simTex", rt);
        }

        void SetupWaveSimulation()
        {
            if (!IsWavesAvailable) return;
            wavesSimulation.SetSettings(gameObject, sr, settings._wavesSettings);
            wavesSimulation.Setup();
        }

        void OnWavesSimulationChanged()
        {
            if (ActiveSharedMaterial == null) return;
            if (settings._wavesSettings == null || settings._wavesSettings.edgeColor == null) return;
            ActiveSharedMaterial.SetColor("_edgeColor", settings._wavesSettings.edgeColor.value);
            ActiveSharedMaterial.SetFloat("_edgeSize", settings._wavesSettings.edgeColoringSize.value);
            ActiveSharedMaterial.SetFloat("_edgeIgnoreTransparency", settings._wavesSettings.edgeIgnoreTransparency.value ? 1f : 0f);
        }

        void OnObstructionChanged()
        {
            if (!enableObstruction.value) return;
            if (settings._obstructorSettings == null || settings._obstructorSettings.obstructionObjectsVisible == null) return;
            obstructorManager.UpdateSettings(settings._obstructorSettings);
        }

        void OnInspectorSettingsChanged()
        {
            managersParent.gameObject.hideFlags = ManagersVisible.value ? HideFlags.None : HideFlags.HideInHierarchy;
        }

        public void OnBlurMaterialChanged()
        {
            if (!IsSpriteMode) return;
            SetLayers();
            switch (settings._blurSettings.blurType)
            {
                case BlurSettings.BlurType.box:
                    matb = new Material(Shader.Find("hidden/box"));
                    matb.name = "box blur";
                    break;
                case BlurSettings.BlurType.gaussian:
                    matb = new Material(Shader.Find("hidden/gaussian"));
                    matb.name = "gaussian blur";
                    break;
                case BlurSettings.BlurType.bokeh:
                    matb = new Material(Shader.Find("hidden/bokeh"));
                    matb.name = "bokeh blur";
                    break;
            }
            SetMaterials();
            OnBlurChanged();
        }

        void SetMaterials()
        {
            if (!IsSpriteMode) return;
            if (settings._blurSettings == null || settings._blurSettings.useBlur == null) return;
            if (!settings._blurSettings.useBlur.value) return;
            SpriteRenderer childSr = childPP.GetComponent<SpriteRenderer>();
            childSr.sharedMaterial = matb;
        }

        void OnBlurChanged()
        {
            if (!IsSpriteMode) return;
            if (settings._blurSettings == null || settings._blurSettings.useBlur == null) return;
            CreateDestroyPostProcessingCamera();
            SetLayers();
            if (!settings._blurSettings.useBlur.value) return;

            SpriteRenderer childSr = childPP.GetComponent<SpriteRenderer>();

            switch (settings._blurSettings.blurType)
            {
                case BlurSettings.BlurType.box:
                    childSr.sharedMaterial.SetTexture("_MainTex2", _childPPLayerRenderer.LayerTexture());
                    childSr.sharedMaterial.SetInt("_area", settings._blurSettings.boxSamplingRange.value);
                    childSr.sharedMaterial.SetFloat("_sigmaX", settings._blurSettings.boxStrength.value);
                    break;
                case BlurSettings.BlurType.gaussian:
                    childSr.sharedMaterial.SetTexture("_MainTex2", _childPPLayerRenderer.LayerTexture());
                    childSr.sharedMaterial.SetInt("_area", settings._blurSettings.gaussianSamplingRange.value);
                    childSr.sharedMaterial.SetFloat("_sigmaX", settings._blurSettings.gaussianStrengthX.value);
                    break;
                case BlurSettings.BlurType.bokeh:
                    childSr.sharedMaterial.SetTexture("_MainTex2", _childPPLayerRenderer.LayerTexture());
                    childSr.sharedMaterial.SetFloat("_area", settings._blurSettings.bokehArea.value);
                    childSr.sharedMaterial.SetFloat("_ratio", settings._blurSettings.bokehRatio.value);
                    childSr.sharedMaterial.SetFloat("_hardness", settings._blurSettings.bokehHardness.value);
                    childSr.sharedMaterial.SetFloat("_gamma", settings._blurSettings.bokehGamma.value);
                    childSr.sharedMaterial.SetInt("_quality", settings._blurSettings.bokehQuality.value);
                    break;
            }

            childSr.sharedMaterial.SetFloat("_falloffS", settings._blurSettings.falloffStart.value);
            childSr.sharedMaterial.SetFloat("_falloffE", settings._blurSettings.falloffEnd.value);
            childSr.sharedMaterial.SetFloat("_falloffP", settings._blurSettings.falloffStrength.value);
            childSr.sharedMaterial.SetInt("_falloffU", settings._blurSettings.useFalloff.value ? 1 : 0);
        }

        public void OnWaterChanged()
        {
            if (ActiveSharedMaterial == null) return;
            if (settings == null || settings._waterSettings == null) return;
            if (settings._waterSettings.depthMlp == null) { EnsureSettingsInitialized(); return; }

            ActiveSharedMaterial = mat;

            // FIX: genSDF must be true when ANY pool in the scene needs SDF data, where "needs SDF"
            // means either the depthFromObstructors bool OR the distance_from_obstructors coloring type.
            // Previously only the bool was checked so selecting the coloring type had no effect.
            // The OR-across-all-pools ensures a second pool with the option off cannot stomp
            // the first pool's genSDF=true by running its own OnWaterChanged after it.
            if (_obstructorManager != null)
            {
                bool anyNeedsSDF = false;
                foreach (var w in WaterUnityCompatibility.FindObjectsByTypeCompat<ModernWater2D>())
                {
                    if (w.EffectivelyUsesSDF()) { anyNeedsSDF = true; break; }
                }
                _obstructorManager.genSDF = anyNeedsSDF;
            }

            ActiveSharedMaterial.SetInt("_enable_obs", enableObstruction.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_enable_sim", enableSimulation.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_enable_nor", normalsPreview.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_enable_ref", enableReflections.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_dwaves", (enableWavesSimulation.value && IsWavesAvailable) ? 1 : 0);
            ActiveSharedMaterial.SetInt("_tilemapMode", IsTilemapMode ? 1 : 0);

            ActiveSharedMaterial.SetInt("_color_type", (int)settings._waterSettings.coloringType);

            ActiveSharedMaterial.SetFloat("_depthMlp", settings._waterSettings.depthMlp.value);
            ActiveSharedMaterial.SetTexture("_colorGradient", Create(settings._waterSettings.colorGradient.value, 128));

            ActiveSharedMaterial.SetColor("_color", settings._waterSettings.color.value);
            ActiveSharedMaterial.SetFloat("_surfaceAlpha", settings._waterSettings.baseAlpha.value);
            ActiveSharedMaterial.SetTexture("_alphaTexture", settings._waterSettings.alphaTexture);
            ActiveSharedMaterial.SetVector("_tiling", settings._waterSettings.tiling.value);

            ActiveSharedMaterial.SetFloat("_num_of_pixels", settings._waterSettings.numOfPixels.value);
            ActiveSharedMaterial.SetInt("_pixel_perfect", settings._waterSettings.pixelPerfect.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_lighting", settings._waterSettings._useLighting.value ? 1 : 0);

            // FIX: _depthFromObstructors must be 1 for both the explicit bool AND the coloring type
            // that relies on SDF data. Previously only the bool fed this property, so switching to the
            // Distance_from_obstructors coloring type set _color_type correctly but left
            // _depthFromObstructors=0, meaning the shader sampled no SDF and showed nothing.
            ActiveSharedMaterial.SetInt("_depthFromObstructors", EffectivelyUsesSDF() ? 1 : 0);

            ActiveSharedMaterial.SetFloat("_obstruction_width", settings._waterSettings.obstructionWidth.value);
            ActiveSharedMaterial.SetColor("_obstruction_color", settings._waterSettings.obstructionColor.value);
            ActiveSharedMaterial.SetFloat("_obstruction_alpha", settings._waterSettings.obstructionAlpha.value);

            ActiveSharedMaterial.SetColor("_deep_color", settings._waterSettings.depthColor.value);

            ActiveSharedMaterial.SetColor("_foam_color", settings._waterSettings.foamColor.value);
            ActiveSharedMaterial.SetFloat("_foam_size", settings._waterSettings.foamSize.value);
            ActiveSharedMaterial.SetVector("_foam_speed", settings._waterSettings.foamSpeed.value);
            ActiveSharedMaterial.SetFloat("_foam_density", settings._waterSettings.foamDensity.value);
            ActiveSharedMaterial.SetFloat("_foam_alpha", settings._waterSettings.foamAlpha.value);

            if (settings._waterSettings.enableBelowWater.value && _surfaceRenderer != null)
                ActiveSharedMaterial.SetTexture("_belowWaterTex", surfaceRenderer.layerRenderer.LayerTexture());
            else
                ActiveSharedMaterial.SetTexture("_belowWaterTex", null);

            ActiveSharedMaterial.SetFloat("_belowWaterTexDistortionStrength", settings._waterSettings.belowWaterDistortionStrength.value);
            ActiveSharedMaterial.SetFloat("_belowWaterTexAlpha", settings._waterSettings.enableBelowWater.value ? settings._waterSettings.belowWaterAlpha.value : 0f);

            ActiveSharedMaterial.SetVector("_distortion_speed", settings._waterSettings.distortionSpeed.value);
            ActiveSharedMaterial.SetVector("_distortion_strength", settings._waterSettings.distortionStrength.value);
            ActiveSharedMaterial.SetVector("_distortion_tiling", settings._waterSettings.distortionTiling.value);
            ActiveSharedMaterial.SetVector("_distortion_color", settings._waterSettings.distortionColor.value);
            ActiveSharedMaterial.SetVector("_distortion_minmax", settings._waterSettings.distortionMinMax.value);
            ActiveSharedMaterial.SetTexture("_distortion_tex", settings._waterSettings.distortionTexture);

            ActiveSharedMaterial.SetTexture("_surfaceTex", settings._waterSettings.surfaceTexture);
            ActiveSharedMaterial.SetFloat("_surfaceTexAlpha", settings._waterSettings.surfaceAlpha.value);
            ActiveSharedMaterial.SetVector("_surfaceTexTiling", settings._waterSettings.surfaceTiling.value);
            ActiveSharedMaterial.SetVector("_surfaceTexSpeed", settings._waterSettings.surfaceSpeed.value);
            ActiveSharedMaterial.SetFloat("_useFoamSpeedForST", settings._waterSettings.useFoamSpeed.value ? 1.0f : 0.0f);
            ActiveSharedMaterial.SetVector("_surfaceTexUV", new Vector4(0f, 0f, 1f, 1f));

            ActiveSharedMaterial.SetTexture("_sun_strips", settings._waterSettings.sunStripsTexture);
            ActiveSharedMaterial.SetFloat("_strips_speed", settings._waterSettings.stripsSpeed.value);
            ActiveSharedMaterial.SetFloat("_strips_scrolling_speed", settings._waterSettings.stripsScrollingSpeed.value);
            ActiveSharedMaterial.SetFloat("_strips_size", settings._waterSettings.stripsSize.value);
            ActiveSharedMaterial.SetFloat("_strips_alpha", settings._waterSettings.stripsAlpha.value);
            ActiveSharedMaterial.SetFloat("_strips_density", settings._waterSettings.stripsDensity.value);
        }

        void OnReflectionsChanged()
        {
            if (!enableReflections.value) return;
            if (ActiveSharedMaterial == null) return;
            if (settings._reflectionsSettings == null || settings._reflectionsSettings.enableTopDownReflections == null) return;

            ActiveSharedMaterial.SetInt("_enable_td", settings._reflectionsSettings.enableTopDownReflections.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_enable_pl", settings._reflectionsSettings.enablePlatformerReflections.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_enable_rm", settings._reflectionsSettings.enableRaymarchedReflections.value ? 1 : 0);
            ActiveSharedMaterial.SetInt("_distortionFPRH", settings._reflectionsSettings.DistortionFPRH.value ? 1 : 0);

            // Only push settings to a manager when that manager's type is enabled for this water.
            // The else-if pattern was removed because it called UpdateSettings on a wrong manager type
            // (e.g. pushing platformer settings to the raymarched manager), corrupting its culling mask.
            if (settings._reflectionsSettings.enableTopDownReflections.value)
                reflectionsManagerTopDown.UpdateSettings(settings._reflectionsSettings, true, WaterUnityCompatibility.GetObjectStableId(this));

            if (settings._reflectionsSettings.enablePlatformerReflections.value)
                reflectionsManagerPlatformer.UpdateSettings(settings._reflectionsSettings, false, WaterUnityCompatibility.GetObjectStableId(this));

            if (settings._reflectionsSettings.enableRaymarchedReflections.value)
                reflectionsManagerRayMarch.UpdateSettings(settings._reflectionsSettings, false, WaterUnityCompatibility.GetObjectStableId(this));

            ActiveSharedMaterial.SetInt("_usePerspective", (settings._reflectionsSettings.usePerspective.value && IsPerspectiveAvailable) ? 1 : 0);
            ActiveSharedMaterial.SetVector("_perspective", settings._reflectionsSettings.waterPerspective.value);
            ActiveSharedMaterial.SetVector("_perspective2", settings._reflectionsSettings.reflectionsPerspective.value);

            ActiveSharedMaterial.SetInt("_enableFalloff", settings._reflectionsSettings.enableFalloff.value ? 1 : 0);
            ActiveSharedMaterial.SetFloat("_falloffStrength", settings._reflectionsSettings.falloffStrength.value);
            ActiveSharedMaterial.SetFloat("_falloffStart", settings._reflectionsSettings.falloffStart.value);
            ActiveSharedMaterial.SetVector("_reflectionsColor", settings._reflectionsSettings.color.value);

            ActiveSharedMaterial.SetInt("_enable_scrolling", settings._reflectionsSettings.enableScrolling.value ? 1 : 0);
            ActiveSharedMaterial.SetFloat("_scrStrength", settings._reflectionsSettings.scrollingStrength.value);
            if (settings._reflectionsSettings.playerPosition != null)
                ActiveSharedMaterial.SetVector("_playerPosition", settings._reflectionsSettings.playerPosition.position);

            ActiveSharedMaterial.SetFloat("_raymarchSteps",
                settings._reflectionsSettings.enableRaymarchedReflections.value
                    ? GetRaymarchPixelSteps(GetCameraRenderingScreen())
                    : 0);
            ActiveSharedMaterial.SetInt("_rm_type2", settings._reflectionsSettings.type2.value ? 1 : 0);
            ActiveSharedMaterial.SetFloat("_raymarchFalloffStart", settings._reflectionsSettings.raymarchFalloffStart.value);
            ActiveSharedMaterial.SetFloat("_raymarchFalloffEnd", settings._reflectionsSettings.raymarchFalloffEnd.value);

            var t = Shader.GetGlobalTexture(WaterShaderIdsREF.reflectionsTexture3);
            if (t != null) ActiveSharedMaterial.SetVector("_refTexRes", new Vector2(t.width, t.height));
        }

        private void Awake()
        {
            EnsureSettingsInitialized();
            if (enableWavesSimulation.value && IsWavesAvailable && _wavesSimulation != null) wavesSimulation.Start();
        }

        private void Update()
        {
            if (HasRenderModeChanged())
            {
                ApplyRenderMode();
                OnWaterChanged();
                OnReflectionsChanged();
            }

            if (IsTilemapMode) ApplyCounterScale();

            UpdateManagerRunStates();

            if (ActiveSharedMaterial == null) return;

            if (settings._reflectionsSettings.playerPosition != null) ActiveSharedMaterial.SetVector("_playerPosition", settings._reflectionsSettings.playerPosition.position);
            CameraSetup();
            SurfaceSetup();
            BelowWaterSetup();
            ReflectionsUpdate();
            if (enableSimulation.value && Application.isPlaying && _waterSimulation != null) waterSimulation.UpdLoop();
            if (IsTilemapMode && enableSimulation.value) UpdateTilemapSimBounds();
            if (CheckForResolutionChanged()) OnResolutionChanged();
        }

        void UpdateManagerRunStates()
        {
            bool ref_enabled = enableReflections.value;

            if (ref_enabled && settings._reflectionsSettings.enableTopDownReflections.value)
                reflectionsManagerTopDown.run = true;
            else if (_reflectionsManagerTopDown != null)
                _reflectionsManagerTopDown.run = false;

            if (ref_enabled && settings._reflectionsSettings.enablePlatformerReflections.value)
                reflectionsManagerPlatformer.run = true;
            else if (_reflectionsManagerPlatformer != null)
                _reflectionsManagerPlatformer.run = false;

            if (ref_enabled && settings._reflectionsSettings.enableRaymarchedReflections.value)
                reflectionsManagerRayMarch.run = true;
            else if (_reflectionsManagerRayMarch != null)
                _reflectionsManagerRayMarch.run = false;

            if (enableObstruction.value)
                obstructorManager.run = true;
            else if (_obstructorManager != null)
                _obstructorManager.run = false;

            if (settings._waterSettings.enableBelowWater.value)
                surfaceRenderer.run = true;
            else if (_surfaceRenderer != null)
                _surfaceRenderer.run = false;
        }

        public int GetRaymarchPixelSteps(Camera cam)
        {
            if (cam == null || raymarchUnits <= 0f) return 0;

            float visibleWorldHeight = GetVisibleWorldHeightAtWater(cam);
            if (visibleWorldHeight <= 0.0001f) return 0;

            float partOfScreen = raymarchUnits / visibleWorldHeight;
            return Mathf.Clamp(Mathf.RoundToInt(cam.pixelHeight * partOfScreen), 1, 256);
        }

        private float GetVisibleWorldHeightAtWater(Camera cam)
        {
            if (cam == null) return 0f;

            if (cam.orthographic)
                return cam.orthographicSize * 2f;

            Vector3 waterCenter = ActiveBounds.center;
            float viewDistance = Vector3.Dot(waterCenter - cam.transform.position, cam.transform.forward);

            if (viewDistance <= 0.0001f)
                viewDistance = Mathf.Abs(waterCenter.z - cam.transform.position.z);

            viewDistance = Mathf.Max(viewDistance, 0.0001f);

            return 2f * viewDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        void RegisterReflectionViewportCallbacks()
        {
            Camera.onPreCull -= OnCameraPreCullForReflectionViewport;
            Camera.onPreCull += OnCameraPreCullForReflectionViewport;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingForReflectionViewport;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRenderingForReflectionViewport;
        }

        void UnregisterReflectionViewportCallbacks()
        {
            Camera.onPreCull -= OnCameraPreCullForReflectionViewport;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingForReflectionViewport;
        }

        void OnCameraPreCullForReflectionViewport(Camera cam)
        {
            UpdatePlatformerReflectionViewportTransform(cam);
        }

        void OnBeginCameraRenderingForReflectionViewport(ScriptableRenderContext context, Camera cam)
        {
            UpdatePlatformerReflectionViewportTransform(cam);
        }

        void UpdatePlatformerReflectionViewportTransform(Camera cam)
        {
            if (cam == null || cam != GetCameraRenderingScreen()) return;
            if (ActiveSharedMaterial == null) return;

            Rect rect = cam.rect;
            ActiveSharedMaterial.SetVector("_ref_transform", new Vector4(rect.width, rect.height, rect.x, rect.y));
        }

        void ReflectionsUpdate()
        {
            if (ActiveSharedMaterial == null) return;
            Camera cam = GetCameraRenderingScreen();
            if (cam == null) return;
            float waterUVY = settings._reflectionsSettings.customReflectionStart.value ? settings._reflectionsSettings.mirrorY.value : 1f;
            float waterWorldY = Mathf.Lerp(ActiveBounds.min.y, ActiveBounds.max.y, waterUVY);
            Vector3 reflectionStartWorld = new Vector3(ActiveBounds.center.x, waterWorldY, ActiveBounds.center.z);

            ActiveSharedMaterial.SetFloat("_reflectionY", cam.WorldToViewportPoint(reflectionStartWorld).y);

            UpdatePlatformerReflectionViewportTransform(cam);

            if (settings._reflectionsSettings.enableRaymarchedReflections.value)
                ActiveSharedMaterial.SetFloat("_raymarchSteps", GetRaymarchPixelSteps(cam));
        }

        void SurfaceSetup()
        {
            if (!IsSpriteMode) return;
            if (settings._waterSettings.surfaceSprite != null)
            {
                ActiveSharedMaterial.SetTexture("_surfaceTex", settings._waterSettings.surfaceSprite.sprite.texture);

                float x0 = ActiveBounds.min.x;
                float y0 = ActiveBounds.min.y;
                float x1 = ActiveBounds.max.x;
                float y1 = ActiveBounds.max.y;

                float xp0 = settings._waterSettings.surfaceSprite.bounds.min.x;
                float yp0 = settings._waterSettings.surfaceSprite.bounds.min.y;
                float xp1 = settings._waterSettings.surfaceSprite.bounds.max.x;
                float yp1 = settings._waterSettings.surfaceSprite.bounds.max.y;

                Vector4 uvs = new Vector4((x0 - xp0) / Mathf.Abs(xp1 - xp0), (x1 - xp0) / Mathf.Abs(xp1 - xp0), (y0 - yp0) / Mathf.Abs(yp1 - yp0), (y1 - yp0) / Mathf.Abs(yp1 - yp0));
                ActiveSharedMaterial.SetVector("_surfaceTexUV", uvs);
            }
        }

        void BelowWaterSetup()
        {
            if (!settings._waterSettings.enableBelowWater.value) return;

            Camera cam = GetCameraRenderingScreen();
            if (cam == null) return;

            Bounds b = ActiveBounds;
            float z = b.center.z;

            Vector3 v0 = cam.WorldToViewportPoint(new Vector3(b.min.x, b.min.y, z));
            Vector3 v1 = cam.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, z));
            Vector3 v2 = cam.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, z));
            Vector3 v3 = cam.WorldToViewportPoint(new Vector3(b.max.x, b.max.y, z));

            float xMin = Mathf.Min(v0.x, v1.x, v2.x, v3.x);
            float xMax = Mathf.Max(v0.x, v1.x, v2.x, v3.x);
            float yMin = Mathf.Min(v0.y, v1.y, v2.y, v3.y);
            float yMax = Mathf.Max(v0.y, v1.y, v2.y, v3.y);

            ActiveSharedMaterial.SetVector("_belowWaterTexUV", new Vector4(xMin, xMax, yMin, yMax));
        }

        private void FixedUpdate()
        {
            if (enableSimulation.value && _waterSimulation != null)
                waterSimulation.Loop();

            if (enableWavesSimulation.value && IsWavesAvailable && _wavesSimulation != null)
                wavesSimulation.Update(ActiveBounds.size.y / 2f / transform.lossyScale.y);
        }

        //cinemachine fix
        bool lastCameraModeOrto = true;
        float lastCameraFov = 0f;
        private void LateUpdate()
        {
            Camera cam = GetCameraRenderingScreen();
            if (cam == null) return;

            if (!cam.orthographic && !Mathf.Approximately(cam.fieldOfView, lastCameraFov))
                lastCameraFov = cam.fieldOfView;

            if (lastCameraModeOrto != cam.orthographic)
                lastCameraModeOrto = cam.orthographic;

            RebuildManagersWorkingRangesData();
            CameraSetup();
            SurfaceSetup();
            BelowWaterSetup();
            ReflectionsUpdate();

            if (IsTilemapMode && enableSimulation.value)
                UpdateTilemapSimBounds();
        }

        void RebuildManagersWorkingRangesData()
        {
            UpdateManagerRunStates();

            bool anyObstruction = false;
            bool anySurface = false;
            bool anyTopDown = false;
            bool anyPlatformer = false;
            bool anyRaymarch = false;
            bool anyStableObstructionSimulation = false;

            foreach (var water in WaterUnityCompatibility.FindObjectsByTypeCompat<ModernWater2D>())
            {
                if (water == null || !water.isActiveAndEnabled) continue;

                if (water.enableObstruction != null && water.enableObstruction.value)
                {
                    anyObstruction = true;
                    if (water.settings != null && water.settings._obstructorSettings != null)
                        water.obstructorManager.UpdateSettings(water.settings._obstructorSettings);
                }

                if (water.settings != null && water.settings._waterSettings != null && water.settings._waterSettings.enableBelowWater != null && water.settings._waterSettings.enableBelowWater.value)
                    anySurface = true;

                if (water.enableSimulation != null && water.enableSimulation.value && water.settings != null && water.settings._simulationSettings != null && water.settings._simulationSettings.moreStableObstructionSimulation != null && water.settings._simulationSettings.moreStableObstructionSimulation.value)
                    anyStableObstructionSimulation = true;

                int sourceId = WaterUnityCompatibility.GetObjectStableId(water);
                bool reflectionsReady = water.settings != null && water.settings._reflectionsSettings != null && water.settings._reflectionsSettings.enableTopDownReflections != null;
                bool reflectionsOn = water.enableReflections != null && water.enableReflections.value && reflectionsReady;

                if (!reflectionsReady)
                {
                    if (_reflectionsManagerTopDown != null) _reflectionsManagerTopDown.UnregisterSource(sourceId);
                    if (_reflectionsManagerPlatformer != null) _reflectionsManagerPlatformer.UnregisterSource(sourceId);
                    if (_reflectionsManagerRayMarch != null) _reflectionsManagerRayMarch.UnregisterSource(sourceId);
                    continue;
                }

                if (reflectionsOn && water.settings._reflectionsSettings.enableTopDownReflections.value)
                {
                    anyTopDown = true;
                    water.reflectionsManagerTopDown.UpdateSettings(water.settings._reflectionsSettings, true, sourceId);
                }
                else if (_reflectionsManagerTopDown != null)
                    _reflectionsManagerTopDown.UnregisterSource(sourceId);

                if (reflectionsOn && water.settings._reflectionsSettings.enablePlatformerReflections.value)
                {
                    anyPlatformer = true;
                    water.reflectionsManagerPlatformer.UpdateSettings(water.settings._reflectionsSettings, false, sourceId);
                }
                else if (_reflectionsManagerPlatformer != null)
                    _reflectionsManagerPlatformer.UnregisterSource(sourceId);

                if (reflectionsOn && water.settings._reflectionsSettings.enableRaymarchedReflections.value)
                {
                    anyRaymarch = true;
                    water.reflectionsManagerRayMarch.UpdateSettings(water.settings._reflectionsSettings, false, sourceId);
                }
                else if (_reflectionsManagerRayMarch != null)
                    _reflectionsManagerRayMarch.UnregisterSource(sourceId);
            }

            if (_obstructorManager != null || anyObstruction)
            {
                obstructorManager.SetStableSimulationMargins(anyStableObstructionSimulation);
                obstructorManager.SyncLateAfterCamera(anyObstruction);
            }
            if (_surfaceRenderer != null || anySurface) surfaceRenderer.SyncLateAfterCamera(anySurface);
            if (_reflectionsManagerTopDown != null || anyTopDown) reflectionsManagerTopDown.SyncLateAfterCamera(anyTopDown);
            if (_reflectionsManagerPlatformer != null || anyPlatformer) reflectionsManagerPlatformer.SyncLateAfterCamera(anyPlatformer);
            if (_reflectionsManagerRayMarch != null || anyRaymarch) reflectionsManagerRayMarch.SyncLateAfterCamera(anyRaymarch);
        }

        private void OnDrawGizmos()
        {
            if (_waterSimulation != null) _waterSimulation.OnGizmos();

            if (!settings._reflectionsSettings.enablePlatformerReflections.value) return;
            if (Camera.main == null) return;
            float x0 = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 10f)).x;
            float x1 = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 10f)).x;
            float y = Mathf.LerpUnclamped(ActiveBounds.min.y, ActiveBounds.max.y, settings._reflectionsSettings.mirrorY.value);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(x0, y), new Vector3(x1, y));
        }

        public static Texture2D Create(Gradient grad, int width = 32, int height = 1)
        {
            var gradTex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            gradTex.filterMode = FilterMode.Bilinear;
            float inv = 1f / width;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    gradTex.SetPixel(x, y, grad.Evaluate(x * inv));
            gradTex.Apply();
            return gradTex;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!IsWavesAvailable) return;
            if (!enableWavesSimulation.value) return;

            var p = collision.transform.position.x;
            float s = transform.position.x - ActiveBounds.size.x * 0.5f;
            float e = transform.position.x + ActiveBounds.size.x * 0.5f;
            float t = (p - s) / (e - s);
            wavesSimulation.Collision(collision, Mathf.Lerp(0f, 1f, t));
        }
    }

#if UNITY_EDITOR
    public partial class ModernWater2D
    {
        [MenuItem("Window/Water2D/Toggle Managers Visibility %&#m")]
        public static void ToggleManagersVisibilityShortcut()
        {
            Transform parent = managersParent;
            if (parent == null) return;

            GameObject go = parent.gameObject;
            bool isHidden = (go.hideFlags & HideFlags.HideInHierarchy) != 0;

            go.hideFlags = isHidden ? HideFlags.None : HideFlags.HideInHierarchy;
            Debug.Log(isHidden ? "[Water2D] Managers Parent is now VISIBLE." : "[Water2D] Managers Parent is now HIDDEN.");

            EditorApplication.RepaintHierarchyWindow();
        }
    }
#endif
}