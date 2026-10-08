using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The way into a prestige: a button that hangs over the pier on screen once the run can be cashed in
    // (PrestigeSystem.CanPrestige), labelled with what cashing in would pay right now. A click only
    // publishes PrestigeTriggeredEvent — PlayingState takes it from there and asks the player first.
    //
    // Two CanvasGroups, two owners. The PARENT's group is a row in UIVisibility, visible in Playing only:
    // that is what keeps the button out of build mode and every other mode, and with it the rule that a
    // run never ends from there. The button's OWN group is set here, by whether the run can prestige — a
    // game condition, not a mode — so no row in the table may name it, or the two would overwrite each
    // other. Groups nest, so the button is on screen only when both say so.
    //
    // At +0 it stays clickable and only dims: a run that beats no record is worth nothing (MetaContext),
    // but ending it is still the player's call — it is the only way out of a run that has stalled.
    //
    // It follows the pier in Canvas.preWillRenderCanvases, not in LateUpdate: that runs after every
    // LateUpdate, the Cinemachine brain's included, so the button is placed against the camera of the
    // frame being drawn and does not trail a frame behind a pan. No execution order setting involved.
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class PrestigeButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Tooltip("The label. {0} becomes the payout; text without {0} is shown as it is.")]
        [SerializeField] private string labelFormat = "Prestige +{0}";

        [Tooltip("Opacity while the run would pay 0 — it has not beaten the best run yet. Still clickable. " +
                 "1 = no dimming.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float zeroPayoutAlpha = 0.6f;

        [Tooltip("The camera that draws the world, to find the pier on screen. Empty = Camera.main.")]
        [SerializeField] private Camera worldCamera;

        private PrestigeSystem prestigeSystem;
        private RunContext run;
        private Pier pier;

        private RectTransform rect;
        private RectTransform area;      // the parent; the button is positioned in its space
        private Camera uiCamera;         // null for a Screen Space - Overlay canvas, as Unity expects
        private CanvasGroup group;

        // What was last applied, so a frame that changes nothing writes nothing to the canvas.
        private int shownPayout = int.MinValue;
        private bool shownVisible = true;
        private float shownAlpha = -1f;

        // Injected through UIRoot by GameBootstrap once the first run exists; later runs arrive by
        // RunStartedEvent. Reads no component of its own, so it is safe before Awake.
        public void Initialize(PrestigeSystem prestigeSystem, RunContext run)
        {
            this.prestigeSystem = prestigeSystem;
            Bind(run);
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            area = transform.parent as RectTransform;
            group = GetComponent<CanvasGroup>();

            var canvas = GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            uiCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

            Apply(false, 0f);
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(OnClicked);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            Canvas.preWillRenderCanvases += Refresh;
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnClicked);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            Canvas.preWillRenderCanvases -= Refresh;
        }

        private void Start()
        {
            if (button == null)
                Debug.LogError($"[PrestigeButton] '{name}' has no Button assigned — it can show, but never " +
                               "be clicked, and there is no other way to prestige.", this);
        }

        // A prestige replaces the run and with it the pier: re-bind, or the button would hang over a pier
        // that has just been destroyed and ask about a run that has already ended.
        private void OnRunStarted(RunStartedEvent e) => Bind(e.Run);

        private void Bind(RunContext newRun)
        {
            run = newRun;
            pier = FindPier(newRun);
            shownPayout = int.MinValue;
        }

        // The pier is start content, placed while the run is built — before Initialize on the first run,
        // before RunStartedEvent on every later one — so it is looked up once per run, not per frame. A
        // run whose start biome has no pier simply never shows the button.
        private static Pier FindPier(RunContext run)
        {
            if (run == null) return null;

            foreach (var structure in run.structures.Values)
            {
                if (structure == null || structure.RuntimeObject == null) continue;

                var found = structure.RuntimeObject.GetComponentInChildren<Pier>();
                if (found != null) return found;
            }
            return null;
        }

        private void OnClicked()
        {
            EventBus<PrestigeTriggeredEvent>.Publish(new PrestigeTriggeredEvent());
        }

        private void Refresh()
        {
            if (group == null) return;   // before Awake

            bool available = run != null && pier != null && prestigeSystem != null && prestigeSystem.CanPrestige(run);
            if (!available)
            {
                Apply(false, 0f);
                return;
            }

            int payout = prestigeSystem.Calculate(run);
            if (payout != shownPayout)
            {
                shownPayout = payout;
                if (label != null) label.text = labelFormat.Replace("{0}", payout.ToString());
            }

            FollowPier();
            Apply(true, payout > 0 ? 1f : zeroPayoutAlpha);
        }

        // Put the button's pivot on the pier's anchor point as the camera sees it right now. Pivot it at
        // the bottom centre in the prefab and it stands on that point.
        private void FollowPier()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || area == null) return;

            Vector3 screen = worldCamera.WorldToScreenPoint(pier.ButtonPoint);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, uiCamera, out Vector2 local))
                return;

            // Whole canvas units: the button is pixel art on the UI's grid, and a fractional position
            // would smear it across pixels as the camera pans.
            var target = new Vector3(Mathf.Round(local.x), Mathf.Round(local.y), 0f);
            if (rect.localPosition != target) rect.localPosition = target;
        }

        private void Apply(bool visible, float alpha)
        {
            if (visible == shownVisible && Mathf.Approximately(alpha, shownAlpha)) return;
            shownVisible = visible;
            shownAlpha = alpha;

            UIVisibility.Set(group, visible);
            if (visible) group.alpha = alpha;
        }
    }
}
