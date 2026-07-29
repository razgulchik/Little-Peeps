using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittlePeeps
{
    // Edit Mode authoring aid for the combined floating-number + pickup-particle harvest feedback.
    // Put it on an EditorOnly child of HarvestVfxSystem and move that child to the desired preview
    // position. It never publishes gameplay events or credits resources.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HarvestFeedbackPreview : MonoBehaviour
    {
        [Tooltip("Usually the HarvestNumbers component on this object's parent.")]
        [SerializeField] private HarvestNumbers numbers;

        [Tooltip("Chooses the pickup particle. Assign Wheat to preview the wheat-ear effect.")]
        [SerializeField] private ResourceSourceDef source;

        [Tooltip("Value written into the floating number.")]
        [Min(0f)] [SerializeField] private float amount = 1f;

        [Header("Edit Mode Preview")]
        [Tooltip("Play the combined effect in Edit Mode. Has no effect in Play Mode.")]
        [SerializeField] private bool preview;

        [Tooltip("Restart the effect after Interval seconds.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Seconds between preview restarts. Keep this at least as long as both effects.")]
        [Min(0.1f)] [SerializeField] private float interval = 1.2f;

        [Tooltip("Preview-only playback multiplier.")]
        [Range(0.1f, 3f)] [SerializeField] private float playbackSpeed = 1f;

        [Tooltip("Choose a new random point inside HarvestNumbers.Spawn Jitter on every restart. " +
                 "Leave off when tuning motion so every replay starts from the same point.")]
        [SerializeField] private bool includeNumberJitter;

#if UNITY_EDITOR
        private TextMeshPro numberView;
        private ParticleSystem pickupView;
        private Vector3 numberOrigin;
        private double lastEditorTime;
        private float elapsed;
        private bool restartRequested;
        private bool cycleFinished;

        private void Reset()
        {
            ResolveNumbers();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) return;

            ResolveNumbers();
            lastEditorTime = EditorApplication.timeSinceStartup;
            restartRequested = preview;
            EditorApplication.update += TickEditorPreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickEditorPreview;
            Cleanup();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= TickEditorPreview;
            Cleanup();
        }

        private void OnValidate()
        {
            interval = Mathf.Max(0.1f, interval);
            playbackSpeed = Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            ResolveNumbers();
            restartRequested = preview;
            cycleFinished = false;
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [ContextMenu("Restart Preview")]
        private void RestartPreview()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Harvest feedback preview runs in Edit Mode only.", this);
                return;
            }

            preview = true;
            restartRequested = true;
            cycleFinished = false;
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Stop Preview")]
        private void StopPreview()
        {
            preview = false;
            restartRequested = false;
            cycleFinished = true;
            Cleanup();
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }

        private void TickEditorPreview()
        {
            if (this == null || Application.isPlaying) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastEditorTime), 0.1f);
            lastEditorTime = now;

            if (!preview)
            {
                Cleanup();
                return;
            }

            if (restartRequested)
            {
                RestartCycle();
            }
            else if (!cycleFinished)
            {
                elapsed += dt * playbackSpeed;

                if (elapsed >= interval)
                {
                    if (loop) RestartCycle();
                    else
                    {
                        cycleFinished = true;
                        Cleanup();
                    }
                }
                else
                {
                    AdvanceViews(dt * playbackSpeed);
                }
            }

            if (!cycleFinished)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        private void RestartCycle()
        {
            Cleanup();
            ResolveNumbers();

            elapsed = 0f;
            restartRequested = false;
            cycleFinished = false;

            if (numbers != null)
            {
                numberView = numbers.CreateEditorPreviewView(transform, amount);
                if (numberView != null)
                {
                    numberView.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    Vector2 jitter = includeNumberJitter
                        ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f))
                        : Vector2.zero;
                    numberOrigin = numbers.ResolveEditorPreviewOrigin(transform.position, jitter);
                    numbers.PlaceEditorPreview(numberView, numberOrigin, 0f);
                }
            }

            if (source != null && source.pickupFx != null)
            {
                pickupView = Instantiate(source.pickupFx, transform);
                pickupView.name = $"{source.pickupFx.name} (Preview)";
                pickupView.gameObject.hideFlags = HideFlags.HideAndDontSave;
                pickupView.transform.position = transform.position;
                pickupView.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                pickupView.Emit(Mathf.Max(1, source.pickupFxCount));
            }
        }

        private void AdvanceViews(float dt)
        {
            if (numberView != null && numbers != null)
            {
                float lifetime = Mathf.Max(0.0001f, numbers.EditorPreviewLifetime);
                float k = elapsed / lifetime;
                if (k >= 1f) numberView.gameObject.SetActive(false);
                else numbers.PlaceEditorPreview(numberView, numberOrigin, k);
            }

            if (pickupView != null && dt > 0f)
                pickupView.Simulate(dt, true, false, false);
        }

        private void ResolveNumbers()
        {
            if (numbers == null) numbers = GetComponentInParent<HarvestNumbers>();
        }

        private void Cleanup()
        {
            if (numberView != null) DestroyImmediate(numberView.gameObject);
            if (pickupView != null) DestroyImmediate(pickupView.gameObject);
            numberView = null;
            pickupView = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.9f);
            const float size = 0.12f;
            Vector3 p = transform.position;
            Gizmos.DrawLine(p - Vector3.right * size, p + Vector3.right * size);
            Gizmos.DrawLine(p - Vector3.up * size, p + Vector3.up * size);
        }
#endif
    }
}
