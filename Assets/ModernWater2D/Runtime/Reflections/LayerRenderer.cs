using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;


namespace Water2D
{

    public enum LayerRendererType
    {
        spriteRendererSingle,
        spriteRendererMultiple,
        screenSingle,
        screenMultiple
    }

    [Serializable]
    public class LayerRenderer
    {
        [HideInInspector] [SerializeField] protected ModernWater2D water2D;
        [HideInInspector] [SerializeField] protected RenderTexture layerTexture;
        [HideInInspector] [SerializeField] protected LayerRendererType rendererType;
        [HideInInspector] [SerializeField] protected SpriteRenderer sr;
        [HideInInspector] [SerializeField] protected RenderTextureFormat format = RenderTextureFormat.ARGB32;
        [HideInInspector] [SerializeField] protected FilterMode fliterMode = FilterMode.Point;
        [HideInInspector] [SerializeField] protected float bitDepth = 0;
        [HideInInspector] [SerializeField] protected string layerName = "nl";
        [HideInInspector] [SerializeField] protected int layerMask = -1;
        [HideInInspector] [SerializeField] protected Camera mainCamera;

        [HideInInspector] [SerializeField] protected Transform holder;

        [HideInInspector] [SerializeField] [Range(0f, 1f)] protected float res;
        [HideInInspector] [SerializeField] [Range(1f, 2f)] protected Vector2 scale = new Vector2(1f, 1f);
        [HideInInspector] [SerializeField] private bool centerPerspectiveMargins = false;
        [NonSerialized] private bool resetCopiedWorldToCameraMatrix = false;
        [HideInInspector] [SerializeField] int reflectionLayerIdx = -1;

        private bool _run;
        private bool _runDirty;
        [HideInInspector] [SerializeField] internal bool copyMainBackground = false;
        [HideInInspector] [SerializeField] float lastOrtographicSize = 0f;
        [HideInInspector] [SerializeField] float lastAspectRatio = 0f;

        private bool isSetupPending = false;
        private bool isSetupComplete = false;
        private Action pendingSetupAction;
        private bool layerTextureMinimized = false;

        private const int MinimizedTextureSize = 1;

        Camera mCamera => Camera.main;
        Camera CameraRenderingScene => Camera.main;
        Transform follow => Camera.main != null ? Camera.main.transform : null;

        // Only stores the desired state. Camera is enabled/disabled in Loop() or ApplyRunState()
        // so it happens once per frame after all water sources have voted via WaterFeatureLayerRenderer.
        public bool run
        {
            get { return _run; }
            set { _run = value; _runDirty = true; }
        }

        void ApplyRunState()
        {
            if (!_runDirty) return;
            _runDirty = false;

            if (mainCamera == null) return;

            if (_run)
            {
                RestoreRenderTextureIfNeeded();

                if ((rendererType == LayerRendererType.screenSingle || rendererType == LayerRendererType.screenMultiple) && CameraRenderingScene != null)
                    ApplyScaledScreenCameraView(CameraRenderingScene);
                else
                {
                    mainCamera.aspect = lastAspectRatio;
                    mainCamera.orthographicSize = lastOrtographicSize;
                }

                mainCamera.targetTexture = layerTexture;
                mainCamera.enabled = true;
            }
            else
            {
                if (CameraRenderingScene != null)
                {
                    if (CameraRenderingScene.orthographic)
                    {
                        lastAspectRatio = CameraRenderingScene.aspect * (scale.x / scale.y);
                        lastOrtographicSize = CameraRenderingScene.orthographicSize * scale.y;
                    }
                    else
                    {
                        lastAspectRatio = CameraRenderingScene.aspect;
                        lastOrtographicSize = CameraRenderingScene.orthographicSize;
                    }
                }

                mainCamera.orthographicSize = 0;
                mainCamera.enabled = false;
                MinimizeRenderTexture();
            }
        }

        public RenderTexture LayerTexture() { return layerTexture; }

        private IEnumerator WaitForMainCameraAndExecute(Action setupAction)
        {
            while (Camera.main == null)
                yield return null;

            yield return new WaitForEndOfFrame();

            setupAction?.Invoke();
            isSetupComplete = true;
            isSetupPending = false;
        }

        private void StartSetupCoroutine(MonoBehaviour context, Action setupAction)
        {
            if (context != null)
            {
                isSetupPending = true;
                isSetupComplete = false;
                pendingSetupAction = setupAction;
                context.StartCoroutine(WaitForMainCameraAndExecute(setupAction));
            }
            else
            {
                if (Camera.main != null)
                {
                    setupAction?.Invoke();
                    isSetupComplete = true;
                }
            }
        }

        public void TryCompletePendingSetup()
        {
            if (isSetupPending && Camera.main != null && pendingSetupAction != null)
            {
                pendingSetupAction.Invoke();
                isSetupComplete = true;
                isSetupPending = false;
                pendingSetupAction = null;
            }
        }

        public bool IsSetupComplete()
        {
            return isSetupComplete;
        }

        public void OnCameraSwapped()
        {
            if (isSetupComplete && pendingSetupAction != null)
            {
                if (Camera.main != null)
                    pendingSetupAction.Invoke();
            }
        }

        public void Setup(SpriteRenderer sr, Transform holder, string layerName, float resolution = 1, RenderTextureFormat format = RenderTextureFormat.ARGB32, FilterMode filterMode = FilterMode.Point, float bitdepth = 0, MonoBehaviour context = null)
        {
            rendererType = LayerRendererType.spriteRendererSingle;
            mainCamera = holder.GetComponent<Camera>();

            this.holder = holder;
            this.layerName = layerName;
            this.format = format;
            this.fliterMode = filterMode;
            this.bitDepth = bitdepth;
            this.res = resolution;
            this.sr = sr;

            Action setupAction = () => ExecuteSpriteRendererSingleSetup(sr);

            if (Camera.main != null)
            {
                setupAction.Invoke();
                isSetupComplete = true;
            }
            else if (context != null)
                StartSetupCoroutine(context, setupAction);
            else
            {
                isSetupPending = true;
                pendingSetupAction = setupAction;
            }
        }

        private void ExecuteSpriteRendererSingleSetup(SpriteRenderer sr)
        {
            if (mCamera == null) return;

            mainCamera.aspect = sr.bounds.extents.x / sr.bounds.extents.y;

            StripCamera();
            mainCamera.orthographicSize = sr.bounds.size.y / 2f;
            mainCamera.backgroundColor = Color.clear;

            if (Screen.width == 0) return;
            if (layerTexture != null) layerTexture.Release();

            CreateRTSpriteRenderer(sr, mCamera);

#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(layerName))
                WaterLayers.CreateLayer(layerName);
#endif
            reflectionLayerIdx = Obstructor.GetLayerIdx(layerName);

            mainCamera.depth = mCamera.depth - 1;
            if (reflectionLayerIdx != -1)
            {
                int cmask = (1 << reflectionLayerIdx);
                mainCamera.cullingMask = cmask;
                mainCamera.targetTexture = layerTexture;
                RemoveLayerFromMainCamera();
            }
        }

        public void Setup(SpriteRenderer sr, Transform holder, int layers, float resolution = 1, RenderTextureFormat format = RenderTextureFormat.ARGB32, FilterMode filterMode = FilterMode.Point, float bitdepth = 0, MonoBehaviour context = null)
        {
            rendererType = LayerRendererType.spriteRendererMultiple;
            mainCamera = holder.GetComponent<Camera>();

            this.holder = holder;
            this.layerMask = layers;
            this.format = format;
            this.fliterMode = filterMode;
            this.bitDepth = bitdepth;
            this.res = resolution;
            this.sr = sr;

            Action setupAction = () => ExecuteSpriteRendererMultipleSetup(sr);

            if (Camera.main != null)
            {
                setupAction.Invoke();
                isSetupComplete = true;
            }
            else if (context != null)
                StartSetupCoroutine(context, setupAction);
            else
            {
                isSetupPending = true;
                pendingSetupAction = setupAction;
            }
        }

        private void ExecuteSpriteRendererMultipleSetup(SpriteRenderer sr)
        {
            if (mCamera == null) return;

            mainCamera.aspect = sr.bounds.extents.x / sr.bounds.extents.y;

            StripCamera();
            mainCamera.orthographicSize = sr.bounds.size.y / 2f;
            mainCamera.backgroundColor = Color.clear;

            if (Screen.width == 0) return;
            if (layerTexture != null) layerTexture.Release();

            CreateRTSpriteRenderer(sr, mCamera);

            mainCamera.cullingMask = layerMask;
            mainCamera.targetTexture = layerTexture;
            mainCamera.depth = mCamera.depth - 1;
        }

        private void CreateRT(SpriteRenderer sr, Camera mCamera, LayerRendererType type)
        {
            if (mCamera == null) return;

            switch (type)
            {
                case LayerRendererType.spriteRendererSingle:
                case LayerRendererType.spriteRendererMultiple:
                    CreateRTSpriteRenderer(sr, mCamera);
                    break;
                case LayerRendererType.screenSingle:
                case LayerRendererType.screenMultiple:
                    CreateRTCamera(mCamera);
                    break;
            }
        }

        private void CreateRTSpriteRenderer(SpriteRenderer sr, Camera mCamera)
        {
            if (mCamera == null || sr == null) return;

            float viewWidth = mCamera.ViewportToWorldPoint(new Vector3(1, 0, 0)).x - mCamera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x;
            float viewHeight = mCamera.ViewportToWorldPoint(new Vector3(0, 1, 0)).y - mCamera.ViewportToWorldPoint(new Vector3(0, 0, 0)).y;
            if (Mathf.Abs(viewWidth) <= Mathf.Epsilon || Mathf.Abs(viewHeight) <= Mathf.Epsilon) return;

            float perX = sr.bounds.size.x / viewWidth;
            float perY = sr.bounds.size.y / viewHeight;
            int finalWidth = Mathf.Max(1, Mathf.RoundToInt(mCamera.scaledPixelWidth * res * perX * Mathf.Max(1f, scale.x)));
            int finalHeight = Mathf.Max(1, Mathf.RoundToInt(mCamera.scaledPixelHeight * res * perY * Mathf.Max(1f, scale.y)));

            ResizeOrCreateRenderTexture(finalWidth, finalHeight, 32);
            mainCamera.targetTexture = layerTexture;
            layerTextureMinimized = false;
        }

        private void CreateRTCamera(Camera mCamera)
        {
            if (mCamera == null) return;

            int baseWidth;
            int baseHeight;
            GetBaseCameraRenderSize(mCamera, out baseWidth, out baseHeight);

            int finalWidth = Mathf.Max(1, Mathf.RoundToInt(baseWidth * res * Mathf.Max(1f, scale.x)));
            int finalHeight = Mathf.Max(1, Mathf.RoundToInt(baseHeight * res * Mathf.Max(1f, scale.y)));

            ResizeOrCreateRenderTexture(finalWidth, finalHeight, 32);
            mainCamera.targetTexture = layerTexture;
            layerTextureMinimized = false;
        }

        private void ResizeOrCreateRenderTexture(int width, int height, int depth)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            if (layerTexture == null)
            {
                layerTexture = new RenderTexture(width, height, depth, format);
            }
            else if (layerTexture.width != width || layerTexture.height != height)
            {
                layerTexture.Release();
                layerTexture.width = width;
                layerTexture.height = height;
            }

            WaterUnityCompatibility.SetRenderTextureDepthStencil(layerTexture, GraphicsFormat.D32_SFloat);
            layerTexture.filterMode = FilterMode.Bilinear;
            layerTexture.enableRandomWrite = false;

            if (!layerTexture.IsCreated())
                layerTexture.Create();
        }

        private void MinimizeRenderTexture()
        {
            if (layerTexture == null) return;

            if (layerTexture.width != MinimizedTextureSize || layerTexture.height != MinimizedTextureSize)
            {
                layerTexture.Release();
                layerTexture.width = MinimizedTextureSize;
                layerTexture.height = MinimizedTextureSize;
            }

            layerTexture.filterMode = FilterMode.Point;
            layerTexture.enableRandomWrite = false;

            if (!layerTexture.IsCreated())
                layerTexture.Create();

            if (mainCamera != null)
                mainCamera.targetTexture = layerTexture;

            layerTextureMinimized = true;
        }

        private void RestoreRenderTextureIfNeeded()
        {
            if (!layerTextureMinimized) return;
            if (!isSetupComplete) return;

            Camera sourceCamera = CameraRenderingScene;
            if (sourceCamera == null) return;

            CreateRT(sr, sourceCamera, rendererType);
        }

        private static void GetBaseCameraRenderSize(Camera sourceCamera, out int baseWidth, out int baseHeight)
        {
            WaterUnityCompatibility.TryGetPixelPerfectRenderSize(sourceCamera, out baseWidth, out baseHeight);
        }

        void StripCamera()
        {
            mainCamera.depthTextureMode = DepthTextureMode.None;
        }

        private void ApplyScaledScreenCameraView(Camera sourceCamera)
        {
            if (mainCamera == null || sourceCamera == null) return;

            mainCamera.ResetProjectionMatrix();
            mainCamera.ResetCullingMatrix();

            if (sourceCamera.orthographic)
            {
                // Keep the original orthographic layout:
                // the render texture is 1.5x taller, and the existing shader samples y * 0.666...
                mainCamera.aspect = sourceCamera.aspect * (scale.x / scale.y);
                mainCamera.orthographicSize = sourceCamera.orthographicSize * scale.y;
            }
            else
            {
                mainCamera.aspect = sourceCamera.aspect;
                mainCamera.orthographicSize = sourceCamera.orthographicSize;

                Matrix4x4 projection = sourceCamera.projectionMatrix;

                if (!Mathf.Approximately(scale.x, 1f))
                {
                    for (int i = 0; i < 4; i++)
                        projection[0, i] /= scale.x;
                }

                if (!Mathf.Approximately(scale.y, 1f))
                {
                    if (centerPerspectiveMargins)
                    {
                        for (int i = 0; i < 4; i++)
                            projection[1, i] /= scale.y;
                    }
                    else
                    {
                        float invScaleY = 1f / scale.y;

                        for (int i = 0; i < 4; i++)
                            projection[1, i] = projection[1, i] * invScaleY + projection[3, i] * (invScaleY - 1f);
                    }
                }

                mainCamera.projectionMatrix = projection;
                mainCamera.cullingMatrix = projection * mainCamera.worldToCameraMatrix;
            }

            lastOrtographicSize = mainCamera.orthographicSize;
            lastAspectRatio = mainCamera.aspect;
        }

        public Vector4 GetScreenUvTransform()
        {
            float sx = Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x;
            float sy = Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y;
            float ox = (1f - sx) * 0.5f;
            float oy = (mainCamera != null && !mainCamera.orthographic && !centerPerspectiveMargins) ? 0f : (1f - sy) * 0.5f;
            return new Vector4(sx, sy, ox, oy);
        }

        public Vector4 GetScreenSourceRect()
        {
            Vector4 t = GetScreenUvTransform();
            float minX = -t.z / Mathf.Max(0.0001f, t.x);
            float minY = -t.w / Mathf.Max(0.0001f, t.y);
            float maxX = (1f - t.z) / Mathf.Max(0.0001f, t.x);
            float maxY = (1f - t.w) / Mathf.Max(0.0001f, t.y);
            return new Vector4(minX, minY, maxX, maxY);
        }

        public void Setup(Transform holder, string layerName, Vector2 scale, float resolution = 1, RenderTextureFormat format = RenderTextureFormat.ARGB32, FilterMode filterMode = FilterMode.Point, float bitdepth = 0, MonoBehaviour context = null, bool centerPerspectiveMargins = false)
        {
            rendererType = LayerRendererType.screenSingle;
            mainCamera = holder.GetComponent<Camera>();

            StripCamera();

            this.holder = holder;
            this.layerName = layerName;
            this.format = format;
            this.fliterMode = filterMode;
            this.bitDepth = bitdepth;
            this.res = resolution;
            this.scale = scale;
            this.centerPerspectiveMargins = centerPerspectiveMargins;

            Action setupAction = () => ExecuteScreenSingleSetup();

            if (Camera.main != null)
            {
                setupAction.Invoke();
                isSetupComplete = true;
            }
            else if (context != null)
                StartSetupCoroutine(context, setupAction);
            else
            {
                isSetupPending = true;
                pendingSetupAction = setupAction;
            }
        }

        private void ExecuteScreenSingleSetup()
        {
            if (mCamera == null) return;

            mainCamera.CopyFrom(mCamera);
            mainCamera.depth = mCamera.depth - 1;
            ApplyScaledScreenCameraView(mCamera);
            if (!copyMainBackground) mainCamera.backgroundColor = Color.clear;
            else mainCamera.backgroundColor = mCamera.backgroundColor;

            RTSetup();
        }

        public void Setup(Transform holder, int layers, Vector2 scale, float resolution = 1, RenderTextureFormat format = RenderTextureFormat.ARGB32, FilterMode filterMode = FilterMode.Point, float bitdepth = 0, MonoBehaviour context = null, bool centerPerspectiveMargins = false, bool resetCopiedWorldToCameraMatrix = false)
        {
            rendererType = LayerRendererType.screenMultiple;
            mainCamera = holder.GetComponent<Camera>();

            StripCamera();

            this.holder = holder;
            this.layerMask = layers;
            this.format = format;
            this.fliterMode = filterMode;
            this.bitDepth = bitdepth;
            this.res = resolution;
            this.scale = scale;
            this.centerPerspectiveMargins = centerPerspectiveMargins;
            this.resetCopiedWorldToCameraMatrix = resetCopiedWorldToCameraMatrix;

            Action setupAction = () => ExecuteScreenMultipleSetup();

            if (Camera.main != null)
            {
                setupAction.Invoke();
                isSetupComplete = true;
            }
            else if (context != null)
                StartSetupCoroutine(context, setupAction);
            else
            {
                isSetupPending = true;
                pendingSetupAction = setupAction;
            }
        }

        private void ExecuteScreenMultipleSetup()
        {
            if (mCamera == null) return;

            mainCamera.CopyFrom(mCamera);
            if (resetCopiedWorldToCameraMatrix)
                mainCamera.ResetWorldToCameraMatrix();
            mainCamera.depth = mCamera.depth - 1;
            ApplyScaledScreenCameraView(mCamera);
            if (!copyMainBackground) mainCamera.backgroundColor = Color.clear;
            else mainCamera.backgroundColor = mCamera.backgroundColor;

            RTSetupExtended();
        }

        private void RTSetupExtended()
        {
            if (Screen.width == 0) return;
            if (layerTexture != null) layerTexture.Release();

            CreateRTCamera(mCamera);
            mainCamera.cullingMask = layerMask;
            mainCamera.targetTexture = layerTexture;
        }

        private void RTSetup()
        {
            if (Screen.width == 0) return;
            if (layerTexture != null) layerTexture.Release();
            CreateRTCamera(mCamera);

#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(layerName))
                WaterLayers.CreateLayer(layerName);
#endif

            reflectionLayerIdx = Obstructor.GetLayerIdx(layerName);

            if (reflectionLayerIdx != -1)
            {
                int cmask = (1 << reflectionLayerIdx);
                if (follow != null) mainCamera.rect = follow.GetComponent<Camera>().rect;
                mainCamera.cullingMask = cmask;
                mainCamera.targetTexture = layerTexture;
                RemoveLayerFromMainCamera();
            }
        }

        public void Loop()
        {
            TryCompletePendingSetup();
            ApplyRunState();

            if (!_run)
            {
                RemoveLayerFromMainCamera();
                return;
            }

            if (!isSetupComplete) return;

            SyncCameraViewData();
            RemoveLayerFromMainCamera();
            FollowCamera();
            UpdateCameraSize();
        }

        private void SyncCameraViewData()
        {
            if (mainCamera == null || CameraRenderingScene == null) return;
            if (rendererType != LayerRendererType.screenSingle && rendererType != LayerRendererType.screenMultiple) return;

            RenderTexture target = mainCamera.targetTexture;
            int mask = mainCamera.cullingMask;
            float depth = mainCamera.depth;
            Color background = mainCamera.backgroundColor;
            CameraClearFlags clearFlags = mainCamera.clearFlags;
            Rect rect = mainCamera.rect;

            mainCamera.CopyFrom(CameraRenderingScene);
            if (resetCopiedWorldToCameraMatrix)
                mainCamera.ResetWorldToCameraMatrix();

            mainCamera.targetTexture = target;
            mainCamera.cullingMask = mask;
            mainCamera.depth = depth;
            mainCamera.clearFlags = clearFlags;
            mainCamera.backgroundColor = copyMainBackground ? CameraRenderingScene.backgroundColor : background;
            mainCamera.rect = rect;

            ApplyScaledScreenCameraView(CameraRenderingScene);
        }

        private void UpdateCameraSize()
        {
            if (CameraRenderingScene == null) return;

            bool recreate = false;

            if (CameraRenderingScene.orthographic)
            {
                float expectedSize = CameraRenderingScene.orthographicSize * scale.y;
                float expectedAspect = CameraRenderingScene.aspect * (scale.x / scale.y);

                if (Mathf.Abs(mainCamera.orthographicSize - expectedSize) > 0.01f)
                    recreate = true;

                if (Mathf.Abs(mainCamera.aspect - expectedAspect) > 0.01f)
                    recreate = true;
            }
            else
            {
                if (Mathf.Abs(mainCamera.aspect - CameraRenderingScene.aspect) > 0.01f)
                    recreate = true;
            }

            if (recreate)
            {
                ApplyScaledScreenCameraView(CameraRenderingScene);
                CreateRT(sr, CameraRenderingScene, rendererType);
            }

            if (follow != null && !Rect.Equals(mainCamera.rect, follow.GetComponent<Camera>().rect)) mainCamera.rect = follow.GetComponent<Camera>().rect;
        }

        private void RemoveLayerFromMainCamera()
        {
            if (reflectionLayerIdx < 0) return;
            if (follow == null) return;
            Camera followCamera = follow.GetComponent<Camera>();
            if (followCamera == null) return;
            if ((followCamera.cullingMask & (1 << reflectionLayerIdx)) != 0) followCamera.cullingMask &= ~(1 << reflectionLayerIdx);
        }

        private void FollowCamera()
        {
            if (follow == null) return;
           
            holder.position = follow.position;
            holder.rotation = follow.rotation;
            holder.SetGlobalScale(follow.lossyScale);
        }

        public void Release()
        {
            if (mainCamera != null)
                mainCamera.targetTexture = null;

            MinimizeRenderTexture();
            isSetupComplete = false;
            isSetupPending = false;
            pendingSetupAction = null;
        }
    }

}