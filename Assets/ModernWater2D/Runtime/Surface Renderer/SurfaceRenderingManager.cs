using UnityEngine;

namespace Water2D
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class SurfaceRenderingManager : WaterFeatureLayerRenderer
    {
        #region Singleton
        public static SurfaceRenderingManager instance;

        private void Awake()
        {
            Singleton();
        }

        protected override void Update()
        {
            base.Update();

            SyncAllCamerasToRunState();

            if (!run) return;

            if (_layerRenderer != null && !_layerRenderer.IsSetupComplete())
            {
                _layerRenderer.TryCompletePendingSetup();

                if (_layerRenderer.IsSetupComplete())
                    Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, _layerRenderer.LayerTexture());
            }
        }

        void Singleton()
        {
            if (instance == null) { instance = this; }
            else if (this == instance) return;
            else DestroyImmediate(gameObject);
        }
        #endregion

        private bool isMainCameraAvailable => Camera.main != null;
        private Camera lastMainCamera;

        public LayerRenderer layerRenderer
        {
            get
            {
                if (_layerRenderer == null)
                {
                    _layerRenderer = new LayerRenderer();

                    if (isMainCameraAvailable)
                        SetupLayerRendererInternal();
                }
                return _layerRenderer;
            }
            set { _layerRenderer = value; }
        }

        void SetAllCamerasEnabled(bool enabled)
        {
            foreach (var c in GetComponentsInChildren<Camera>(true))
                c.enabled = enabled;
        }

        void SyncAllCamerasToRunState()
        {
            SetAllCamerasEnabled(run);
        }

        private void SetupLayerRendererInternal()
        {
            if (!isMainCameraAvailable) return;

            int cullingMask = Camera.main.cullingMask;
            cullingMask &= ~(1 << Obstructor.GetLayerIdx(ModernWater2D.srLayer));
            cullingMask &= ~(1 << Obstructor.GetLayerIdx(ModernWater2D.sr2Layer));

            _layerRenderer.Setup(transform, cullingMask, Vector2.one, 1f);

            if (_layerRenderer.IsSetupComplete())
                Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, _layerRenderer.LayerTexture());

            lastMainCamera = Camera.main;
        }

        public void SetupLayerRenderer()
        {
            if (!isMainCameraAvailable)
            {
                Debug.LogWarning("[SurfaceRenderingManager] Main camera not available yet. Setup will be attempted automatically.");
                return;
            }

            int cullingMask = Camera.main.cullingMask;
            cullingMask &= ~(1 << Obstructor.GetLayerIdx(ModernWater2D.srLayer));
            cullingMask &= ~(1 << Obstructor.GetLayerIdx(ModernWater2D.sr2Layer));

            layerRenderer.Setup(transform, cullingMask, Vector2.one, 1f);

            if (layerRenderer.IsSetupComplete())
                Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, layerRenderer.LayerTexture());

            lastMainCamera = Camera.main;
        }

        public void OnMainCameraChanged()
        {
            if (_layerRenderer != null && isMainCameraAvailable)
            {
                _layerRenderer.OnCameraSwapped();

                if (_layerRenderer.IsSetupComplete())
                    Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, _layerRenderer.LayerTexture());

                lastMainCamera = Camera.main;
            }
        }

        private void CheckForCameraChange()
        {
            if (isMainCameraAvailable && Camera.main != lastMainCamera)
                OnMainCameraChanged();
        }

        public void SyncLateAfterCamera(bool shouldRun)
        {
            SetRunStateImmediate(shouldRun);
            SyncAllCamerasToRunState();

            if (_layerRenderer != null) _layerRenderer.Loop();
            if (!shouldRun) return;

            if (_layerRenderer != null && !_layerRenderer.IsSetupComplete())
            {
                _layerRenderer.TryCompletePendingSetup();

                if (_layerRenderer.IsSetupComplete())
                    Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, _layerRenderer.LayerTexture());
            }
            else if (_layerRenderer != null && _layerRenderer.IsSetupComplete())
                Shader.SetGlobalTexture(WaterShaderIdsSUR.surfaceTexture, _layerRenderer.LayerTexture());
        }

        private void LateUpdate()
        {
            CheckForCameraChange();
        }

        private void OnEnable()
        {
            if (_layerRenderer != null && isMainCameraAvailable && !_layerRenderer.IsSetupComplete())
                SetupLayerRendererInternal();

            SyncAllCamerasToRunState();
        }

        private void OnDisable()
        {
            if (_layerRenderer != null) _layerRenderer.Release();
            SetAllCamerasEnabled(false);
        }
    }
}