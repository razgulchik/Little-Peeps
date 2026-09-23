using System;
using UnityEngine;

namespace Water2D
{
    /// <summary>
    /// Base class for water feature managers.
    /// Individual water sources vote run=true or run=false each frame.
    /// The manager's own Update tallies all votes THEN applies the result to the LayerRenderer,
    /// ensuring the camera state is set exactly once per frame after all sources have voted —
    /// regardless of script execution order.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [Serializable]
    public abstract class WaterFeatureLayerRenderer : MonoBehaviour
    {
        [HideInInspector] [SerializeField] protected LayerRenderer _layerRenderer;
        [HideInInspector] [SerializeField] bool _run;

        [HideInInspector]
        public bool run
        {
            get { return _run; }
            set
            {
                // Only count the vote. Do NOT touch _layerRenderer here.
                // Update() applies the final result after all sources have voted.
                if (value) _runTrue++;
                else _runFalse++;
            }
        }

        private int _runTrue = 0;
        private int _runFalse = 0;

        private void TallyVotes()
        {
            int rt = _runTrue;
            int rf = _runFalse;
            _runTrue = _runFalse = 0;

            if (rt > 0) _run = true;
            else if (rf > 0) _run = false;
            // if both zero: no votes this frame, keep previous state
        }

        protected virtual void Update()
        {
            TallyVotes();
            if (_layerRenderer != null) _layerRenderer.run = _run;
        }

        protected void SetRunStateImmediate(bool value)
        {
            _runTrue = 0;
            _runFalse = 0;
            _run = value;
            if (_layerRenderer != null) _layerRenderer.run = _run;
        }
    }
}