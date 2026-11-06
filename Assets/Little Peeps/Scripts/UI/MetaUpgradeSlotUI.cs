using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One meta perk on the Ancestral Wisdom screen: its icon on the frame, its price in the corner. Bright when
    // taken, dim when not. Left click takes it, right click returns it; whether that goes through is
    // PrestigeSystem's call, made by MetaUpgradesUI, which then redraws every cell. While the pointer rests on
    // it the tip shows its name and description.
    //
    // The root's Image is the raycast target that catches the pointer.
    public class MetaUpgradeSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Switched off while the upgrade has no icon — an Image without a sprite draws a white box — " +
                 "so such a cell shows the bare frame.")]
        [SerializeField] private Image icon;

        [SerializeField] private TMP_Text costLabel;

        [Header("Taken")]
        [SerializeField] private Color takenIconColor = Color.white;
        [SerializeField] private Color takenCostColor = new Color(0.4196f, 0.5216f, 0.8235f);

        [Header("Not taken")]
        [SerializeField] private Color notTakenIconColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color notTakenCostColor = new Color(0.1647f, 0.1882f, 0.2353f);

        private Action<GlobalUpgradeDef> onTake;
        private Action<GlobalUpgradeDef> onReturn;
        private MetaUpgradeTipUI tip;

        public GlobalUpgradeDef Def { get; private set; }

        public void Init(GlobalUpgradeDef def, Action<GlobalUpgradeDef> onTake, Action<GlobalUpgradeDef> onReturn,
                         MetaUpgradeTipUI tip)
        {
            Def = def;
            this.onTake = onTake;
            this.onReturn = onReturn;
            this.tip = tip;

            if (costLabel != null) costLabel.text = def != null ? def.Cost.ToString() : string.Empty;
            if (icon != null)
            {
                Sprite sprite = def != null ? def.icon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        public void Refresh(bool taken)
        {
            if (icon != null) icon.color = taken ? takenIconColor : notTakenIconColor;
            if (costLabel != null) costLabel.color = taken ? takenCostColor : notTakenCostColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) onTake?.Invoke(Def);
            else if (eventData.button == PointerEventData.InputButton.Right) onReturn?.Invoke(Def);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tip != null && Def != null) tip.Show(Def.title, Def.ScreenText, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tip != null) tip.Hide();
        }
    }
}
