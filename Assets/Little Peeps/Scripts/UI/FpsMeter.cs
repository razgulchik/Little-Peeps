using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // Debug FPS readout. On/off is the GameObject's own checkbox in the Inspector — there is no other
    // toggle. Counts on unscaled time so it keeps reading through freezes (island rise, perk pick),
    // averages the frames of one window and rewrites the label once per window: steady digits, no garbage.
    [RequireComponent(typeof(TMP_Text))]
    public class FpsMeter : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.1f)] private float window = 0.5f;   // seconds per reading

        private int frames;
        private float elapsed;

        private void Reset()
        {
            label = GetComponent<TMP_Text>();
        }

        private void Awake()
        {
            if (label == null) label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            frames = 0;
            elapsed = 0f;
            label.SetText("-- FPS");   // until the first window closes
        }

        private void Update()
        {
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < window) return;

            label.SetText("{0:0} FPS", frames / elapsed);
            frames = 0;
            elapsed = 0f;
        }
    }
}
