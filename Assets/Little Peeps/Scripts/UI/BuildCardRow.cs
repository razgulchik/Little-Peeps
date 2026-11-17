using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // Sibling order controls drawing, but a card must keep its original place in the row when raised.
    // Staying on the parent canvas also keeps every card inside the scroller's RectMask2D.
    //
    // A card only records its priority; the row reorders its children itself, once a frame. A card says
    // where it stands from OnEnable / OnDisable too, and while the row or a parent of it is being switched
    // on or off Unity refuses to reorder its children — exiting Play mode with a card in hand did that.
    [AddComponentMenu("Layout/Build Card Row")]
    public class BuildCardRow : HorizontalLayoutGroup
    {
        private readonly List<RectTransform> layoutOrder = new();
        private readonly List<RectTransform> drawOrder = new();
        private readonly Dictionary<RectTransform, int> drawPriorities = new();
        private bool drawOrderStale;

        public override void CalculateLayoutInputHorizontal()
        {
            RememberLayoutOrder();
            base.CalculateLayoutInputHorizontal();
            rectChildren.Sort(CompareLayoutOrder);
        }

        public void SetDrawPriority(RectTransform card, int priority)
        {
            if (card == null || card.parent != transform || PriorityOf(card) == priority) return;

            if (priority == 0) drawPriorities.Remove(card);
            else drawPriorities[card] = priority;
            drawOrderStale = true;
        }

        // After every card's Update and tweens, before the canvas draws: no frame shows the old order. A
        // row switched off does not run it, and catches up on its first frame back.
        private void LateUpdate()
        {
            RememberLayoutOrder();
            if (!drawOrderStale) return;
            drawOrderStale = false;

            drawOrder.Clear();
            drawOrder.AddRange(layoutOrder);
            drawOrder.Sort((a, b) =>
            {
                int comparison = PriorityOf(a).CompareTo(PriorityOf(b));
                return comparison != 0 ? comparison : CompareLayoutOrder(a, b);
            });

            for (int i = 0; i < drawOrder.Count; i++)
                if (drawOrder[i].GetSiblingIndex() != i)
                    drawOrder[i].SetSiblingIndex(i);
        }

        // A card that came or went changes the draw order too: a new one lands last among the children,
        // above a raised card.
        private void RememberLayoutOrder()
        {
            for (int i = layoutOrder.Count - 1; i >= 0; i--)
            {
                RectTransform card = layoutOrder[i];
                if (card != null && card.parent == transform) continue;
                drawPriorities.Remove(card);
                layoutOrder.RemoveAt(i);
                drawOrderStale = true;
            }

            // Remember inactive cards too: changing tabs must not change their left-to-right order.
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i) is RectTransform card && !layoutOrder.Contains(card))
                {
                    layoutOrder.Add(card);
                    drawOrderStale = true;
                }
        }

        private int PriorityOf(RectTransform card) => drawPriorities.TryGetValue(card, out int value) ? value : 0;

        private int CompareLayoutOrder(RectTransform a, RectTransform b) =>
            layoutOrder.IndexOf(a).CompareTo(layoutOrder.IndexOf(b));
    }
}
