using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One cell of the HUD perk panel: a taken perk's icon on the perk_place frame. Spawned and bound by
    // PerkPanel. The corner Label is authored in the prefab and not driven from here yet.
    //
    // While the pointer rests on it, the cell asks HintView for the perk's name and description — to its
    // LEFT: the cells stand in a column at the right edge, and a window above would cover the perks over it.
    // The root's Image is the raycast target that catches the pointer.
    public class PerkSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The perk's small picture (PerkDef.smallIcon). Switched off while the perk has none — an " +
                 "Image without a sprite draws a white box — so such a cell shows the bare frame.")]
        [SerializeField] private Image icon;

        private readonly HintContent hint = new();
        private bool hintShown;

        public PerkDef Perk { get; private set; }

        public void Bind(PerkDef perk)
        {
            Perk = perk;
            if (icon != null)
            {
                Sprite sprite = perk != null ? perk.smallIcon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            // A cell reused under the pointer says what it shows now.
            if (hintShown) ShowHint();
        }

        public void OnPointerEnter(PointerEventData eventData) => ShowHint();
        public void OnPointerExit(PointerEventData eventData) => HideHint();

        // A new run switches the spare cells off, possibly with the pointer on one.
        private void OnDisable() => HideHint();

        // The same text the selection card shows: the hand-worded description, or the one the data makes.
        private void ShowHint()
        {
            if (Perk == null) return;

            hint.Clear();
            hint.title = Perk.title;
            hint.body = Perk.DescriptionText;

            hintShown = true;
            EventBus<HintShowEvent>.Publish(new HintShowEvent
            {
                Owner = this,
                Content = hint,
                Anchor = (RectTransform)transform,
                Side = HintSide.Left,
            });
        }

        private void HideHint()
        {
            if (!hintShown) return;
            hintShown = false;
            EventBus<HintHideEvent>.Publish(new HintHideEvent { Owner = this });
        }
    }
}
