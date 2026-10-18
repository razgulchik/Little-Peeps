using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // TEMPORARY offering HUD for stage 1 of the offering cycle: one text and one button, so the cycle can be
    // played before its real UI exists. The text shows the cycle, the time left and the quota against the
    // stock ("Wood 32/40"); once paid, the next cycle's quota. The button pays early — it only publishes
    // OfferRequestedEvent, GameplayContainerState decides. Replaced by the timeline strip in stage 4.
    //
    // The text is rebuilt only when what it shows has changed (a second ticked, the stock moved, the cycle
    // turned), not every frame.
    public class OfferingPanel : MonoBehaviour, IUIElement
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button offerButton;

        private OfferingSystem offerings;
        private ResourceSystem resources;

        private readonly StringBuilder sb = new();
        private bool dirty = true;
        private int shownSecond = -1;
        private int shownCycle = -1;
        private bool shownOffered;

        // From UIRoot (UIServices).
        public void Initialize(UIServices ui)
        {
            offerings = ui.OfferingSystem;
            resources = ui.ResourceSystem;
            dirty = true;
        }

        private void OnEnable()
        {
            if (offerButton != null) offerButton.onClick.AddListener(OnOfferClicked);
            EventBus<ResourceChangedEvent>.Subscribe(OnResourceChanged);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            dirty = true;
        }

        private void OnDisable()
        {
            if (offerButton != null) offerButton.onClick.RemoveListener(OnOfferClicked);
            EventBus<ResourceChangedEvent>.Unsubscribe(OnResourceChanged);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        }

        private void OnOfferClicked() => EventBus<OfferRequestedEvent>.Publish(new OfferRequestedEvent());
        private void OnResourceChanged(ResourceChangedEvent _) => dirty = true;
        private void OnRunStarted(RunStartedEvent _) => dirty = true;

        private void Update()
        {
            if (offerings == null) return;

            int second = Mathf.CeilToInt(offerings.TimeLeft);
            if (second != shownSecond || offerings.Cycle != shownCycle || offerings.Offered != shownOffered)
                dirty = true;
            if (!dirty) return;

            dirty = false;
            shownSecond = second;
            shownCycle = offerings.Cycle;
            shownOffered = offerings.Offered;

            if (offerButton != null) offerButton.interactable = offerings.CanOffer;
            if (label != null) label.text = Compose(second);
        }

        private string Compose(int second)
        {
            sb.Clear();
            if (!offerings.IsActive) return "No offerings";

            sb.Append("Cycle ").Append(offerings.Cycle);
            if (offerings.IsSpecial) sb.Append(" (special)");
            sb.Append("  ").Append(second / 60).Append(':').Append((second % 60).ToString("00"));

            if (offerings.Offered)
            {
                sb.Append("\nOffered. Next");
                if (offerings.IsSpecialCycle(offerings.Cycle + 1)) sb.Append(" (special)");
                sb.Append(':');
                AppendQuota(offerings.NextQuota, false);
            }
            else
            {
                AppendQuota(offerings.Quota, true);
            }

            return sb.ToString();
        }

        private void AppendQuota(IReadOnlyList<ResourceCost> quota, bool withStock)
        {
            for (int i = 0; i < quota.Count; i++)
            {
                sb.Append('\n').Append(quota[i].resourceType).Append(' ');
                if (withStock && resources != null)
                    sb.Append(Mathf.FloorToInt(resources.GetResource(quota[i].resourceType))).Append('/');
                sb.Append(Mathf.CeilToInt(quota[i].amount));
            }
        }
    }
}
