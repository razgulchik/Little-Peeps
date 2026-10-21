using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // Sibling order controls drawing, but a card must keep its original place in the row when raised.
    // Staying on the parent canvas also keeps every card inside the scroller's RectMask2D.
    [AddComponentMenu("Layout/Build Card Row")]
    public class BuildCardRow : HorizontalLayoutGroup
    {
        private readonly List<RectTransform> layoutOrder = new();
        private readonly List<RectTransform> drawOrder = new();
        private readonly Dictionary<RectTransform, int> drawPriorities = new();

        public override void CalculateLayoutInputHorizontal()
        {
            RememberLayoutOrder();
            base.CalculateLayoutInputHorizontal();
            rectChildren.Sort(CompareLayoutOrder);
        }

        public void SetDrawPriority(RectTransform card, int priority)
        {
            if (card == null || card.parent != transform) return;

            bool childrenChanged = RememberLayoutOrder();
            if (!childrenChanged && PriorityOf(card) == priority) return;

            if (priority == 0) drawPriorities.Remove(card);
            else drawPriorities[card] = priority;

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

        private bool RememberLayoutOrder()
        {
            bool changed = false;
            for (int i = layoutOrder.Count - 1; i >= 0; i--)
            {
                RectTransform card = layoutOrder[i];
                if (card != null && card.parent == transform) continue;
                drawPriorities.Remove(card);
                layoutOrder.RemoveAt(i);
                changed = true;
            }

            // Remember inactive cards too: changing tabs must not change their left-to-right order.
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i) is RectTransform card && !layoutOrder.Contains(card))
                {
                    layoutOrder.Add(card);
                    changed = true;
                }

            return changed;
        }

        private int PriorityOf(RectTransform card) => drawPriorities.TryGetValue(card, out int value) ? value : 0;

        private int CompareLayoutOrder(RectTransform a, RectTransform b) =>
            layoutOrder.IndexOf(a).CompareTo(layoutOrder.IndexOf(b));
    }
}
