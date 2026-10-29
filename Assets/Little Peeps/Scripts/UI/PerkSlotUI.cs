using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One cell of the HUD perk panel: a taken perk's icon on the perk_place frame, its level in the corner.
    // Spawned and bound by PerkPanel; a perk taken again keeps its cell and the number goes up.
    //
    // While the pointer rests on it, the cell asks HintView for the perk's name and description — to its
    // LEFT: the cells stand in a column at the right edge, and a window above would cover the perks over it.
    // The root's Image is the raycast target that catches the pointer.
    public class PerkSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The perk's small picture (PerkDef.smallIcon). Switched off while the perk has none — an " +
                 "Image without a sprite draws a white box — so such a cell shows the bare frame.")]
        [SerializeField] private Image icon;

        [Tooltip("The number in the corner: the level of the perk the run has taken.")]
        [SerializeField] private TMP_Text levelLabel;

        private readonly HintContent hint = new();
        private bool hintShown;

        public PerkDef Perk { get; private set; }
        public int Level { get; private set; }

        public void Bind(PerkDef perk, int level)
        {
            Perk = perk;
            Level = level;
            if (levelLabel != null) levelLabel.text = level.ToString();
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

        // The same text the selection card of the taken level showed: the hand-worded description, or the
        // one the data makes — with the total so far once there is more than one level.
        private void ShowHint()
        {
            if (Perk == null) return;

            hint.Clear();
            hint.title = Perk.title;
            hint.body = Perk.DescriptionAt(Level);

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
